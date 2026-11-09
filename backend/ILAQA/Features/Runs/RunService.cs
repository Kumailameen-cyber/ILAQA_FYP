using ILAQA.Common;
using ILAQA.Data;
using ILAQA.Models;
using ILAQA.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ILAQA.Features.Runs;

// Business logic for /api/runs: the life of a run (start, pause, resume,
// GPS upload, finish) and the territory map.
// The territory rules themselves live in TerritoryDetectionService (pure
// geometry, unit tested) and TerritoryClaimService (saves the decisions).
// Errors are plain text: the app matches some of these texts, keep them.
public sealed class RunService(
    ApplicationDbContext db,
    TerritoryDetectionService detector,
    TerritoryClaimService claims,
    GpsDistanceService gpsDistance,
    ILogger<RunService> logger)
{
    private const int MaxPointsPerUpload = 500;

    // ===================== Start =====================

    // Safe to retry: the same RunId and start time return the existing run.
    public async Task<(StartRunResponse Run, bool Created)> StartAsync(string userId, StartRunRequest request)
    {
        if (request.RunId == Guid.Empty)
            throw BadRequest("RunId cannot be empty.");

        if (request.StartedAt.Kind != DateTimeKind.Utc)
            throw BadRequest("StartedAt must be UTC.");

        // Small clock differences are fine, future runs are not.
        if (request.StartedAt > DateTime.UtcNow.AddMinutes(5))
            throw BadRequest("StartedAt is too far in the future.");

        var existing = await db.Runs.AsNoTracking()
            .Where(r => r.Id == request.RunId)
            .Select(r => new { r.UserId, r.StartedAt, r.Status })
            .SingleOrDefaultAsync();

        if (existing != null)
        {
            if (existing.UserId != userId)
                throw Conflict("RunId is already in use.");

            if (existing.StartedAt != request.StartedAt)
                throw Conflict("RunId already exists with a different start time.");

            return (new StartRunResponse(request.RunId, existing.StartedAt, existing.Status, true), false);
        }

        var run = new Run
        {
            Id = request.RunId,
            UserId = userId,
            StartedAt = request.StartedAt,
            ActiveStartedAt = request.StartedAt,
            Status = "Running",
            ValidationStatus = RunValidationStatuses.Pending
        };

        db.Runs.Add(run);
        await db.SaveChangesAsync();

        return (new StartRunResponse(run.Id, run.StartedAt, run.Status, false), true);
    }

    // ===================== Pause / Resume =====================

    // Pausing a paused run just returns its state (UC-02, safe retry).
    public async Task<RunStateResponse> PauseAsync(string userId, Guid runId)
    {
        var run = await FindRunAsync(userId, runId);

        if (run.Status == "Paused")
            return new RunStateResponse(run.Id, run.Status, run.ActiveSeconds, null);

        if (run.Status != "Running")
            throw Conflict("Only a running session can be paused.");

        run.ActiveSeconds += ElapsedActiveSeconds(run, DateTime.UtcNow);
        run.ActiveStartedAt = null;
        run.Status = "Paused";
        await db.SaveChangesAsync();

        return new RunStateResponse(run.Id, run.Status, run.ActiveSeconds, null);
    }

    // Resuming a running run just returns its state (UC-02, safe retry).
    // Time spent paused is not counted in ActiveSeconds.
    public async Task<RunStateResponse> ResumeAsync(string userId, Guid runId)
    {
        var run = await FindRunAsync(userId, runId);

        if (run.Status != "Running")
        {
            if (run.Status != "Paused")
                throw Conflict("Only a paused run can be resumed.");

            if (run.ActiveStartedAt != null)
                throw Conflict("This run already has an active start time.");

            run.ActiveStartedAt = DateTime.UtcNow;
            run.Status = "Running";
            await db.SaveChangesAsync();
        }

        return new RunStateResponse(run.Id, run.Status, run.ActiveSeconds, run.ActiveStartedAt);
    }

    // ===================== Upload GPS points =====================

    // Saves a batch of GPS points (retries are safe), then claims any loop
    // the runner has closed so far.
    public async Task<UploadPointsResponse> UploadPointsAsync(string userId, Guid runId, List<RunGpsPointDto>? points)
    {
        if (points == null || points.Count == 0)
            throw BadRequest("At least one GPS point is required.");

        if (points.Count > MaxPointsPerUpload)
            throw BadRequest("Maximum 500 points per request.");

        // The run row stays locked until the claims are committed, so an
        // upload and a Finish for the same run never run at the same time.
        await using var transaction = await db.Database.BeginTransactionAsync();

        var run = await db.LockRunAsync(runId, userId) ?? throw NotFound();

        if (run.Status is not ("Running" or "Paused"))
            throw Conflict("GPS points cannot be uploaded to this run."); // Text matched by the app.

        ValidatePoints(points, run.StartedAt);

        var sequenceNumbers = points.Select(p => p.SequenceNo).ToList();
        var existing = await db.RunGpsPoints.AsNoTracking()
            .Where(p => p.RunId == runId && sequenceNumbers.Contains(p.SequenceNo))
            .ToDictionaryAsync(p => p.SequenceNo);

        // A retry may resend a point, but never change it.
        foreach (var point in points)
        {
            if (existing.TryGetValue(point.SequenceNo, out var saved) && !IsSamePoint(saved, point))
                throw Conflict($"Sequence {point.SequenceNo} already exists with different GPS data.");
        }

        var newPoints = points
            .Where(p => !existing.ContainsKey(p.SequenceNo))
            .Select(p => new RunGpsPoint
            {
                RunId = runId,
                SequenceNo = p.SequenceNo,
                LegNo = p.LegNo,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                AccuracyMeters = p.AccuracyMeters,
                RecordedAt = p.RecordedAt
            })
            .ToList();

        db.RunGpsPoints.AddRange(newPoints);
        await db.SaveChangesAsync();

        var newlyClaimedCount = await ClaimLoopsDuringRunAsync(transaction, runId, userId);

        // All claims of the run (also earlier ones), so a retried upload
        // can restore the map on the phone.
        var claimed = await db.Territories.AsNoTracking()
            .Where(t => t.RunId == runId && t.OwnerUserId == userId)
            .OrderBy(t => t.LoopIndex)
            .ToListAsync();

        var decisions = await GetClaimDecisionsAsync(runId);
        await transaction.CommitAsync();

        return new UploadPointsResponse(runId, points.Count, newPoints.Count, points.Count - newPoints.Count,
            newlyClaimedCount, claimed.Select(RunTerritoryDto.From).ToList(), decisions);
    }

    // Territory work during a run. The GPS points are always kept: if this
    // fails on unusual GPS it is rolled back, logged and tried again on the
    // next upload.
    private async Task<int> ClaimLoopsDuringRunAsync(
        IDbContextTransaction transaction, Guid runId, string userId)
    {
        await transaction.CreateSavepointAsync("territories");
        try
        {
            await claims.SnapPoorPointsAsync(runId); // Move poor GPS points onto the road once.
            var (newTerritories, _) = await claims.SaveNewTerritoriesAsync(runId, userId, runIsFinishing: false);
            return newTerritories.Count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Territory processing failed for upload to run {RunId}.", runId);
            await transaction.RollbackToSavepointAsync("territories");
            db.ChangeTracker.Clear();
            return 0;
        }
    }

    private static void ValidatePoints(List<RunGpsPointDto> points, DateTime runStartedAt)
    {
        if (points.Select(p => p.SequenceNo).Distinct().Count() != points.Count)
            throw BadRequest("Duplicate sequence numbers in request.");

        var latestAllowed = DateTime.UtcNow.AddMinutes(5);

        foreach (var p in points)
        {
            if (p.SequenceNo < 1 || p.LegNo < 1)
                throw BadRequest("Invalid sequence or leg number.");

            if (!double.IsFinite(p.Latitude) || !double.IsFinite(p.Longitude) ||
                p.Latitude is < -90 or > 90 || p.Longitude is < -180 or > 180)
                throw BadRequest("Invalid GPS coordinates.");

            if (p.AccuracyMeters is { } accuracy && (!double.IsFinite(accuracy) || accuracy < 0))
                throw BadRequest("Invalid GPS accuracy.");

            if (p.RecordedAt.Kind != DateTimeKind.Utc)
                throw BadRequest("GPS timestamps must be UTC.");

            if (p.RecordedAt < runStartedAt.AddMinutes(-1))
                throw BadRequest("GPS point is before run start.");

            if (p.RecordedAt > latestAllowed)
                throw BadRequest("GPS point is in the future.");
        }
    }

    private static bool IsSamePoint(RunGpsPoint saved, RunGpsPointDto sent) =>
        saved.LegNo == sent.LegNo &&
        saved.Latitude == sent.Latitude &&
        saved.Longitude == sent.Longitude &&
        saved.AccuracyMeters == sent.AccuracyMeters &&
        saved.RecordedAt.Ticks == sent.RecordedAt.Ticks;

    // ===================== Finish =====================

    // Finishes the run and saves its territories in one transaction.
    // Safe to repeat: a finished run returns its saved result, and a run
    // whose territory work failed is processed again.
    public async Task<EndRunResponse> EndAsync(string userId, Guid runId, EndRunRequest? request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Locked so two Finish requests cannot save territories twice.
        var run = await db.LockRunAsync(runId, userId) ?? throw NotFound();

        var alreadyCompleted = run.Status == "Completed";

        if (!alreadyCompleted && run.Status is not ("Running" or "Paused"))
            throw Conflict("This run cannot be completed.");

        if (alreadyCompleted && run.ValidationStatus == RunValidationStatuses.Failed)
            run = await RetryFailedFinishAsync(transaction, run, userId);

        if (!alreadyCompleted)
        {
            var now = DateTime.UtcNow;
            var serverActiveSeconds = run.ActiveSeconds +
                (run.Status == "Running" ? ElapsedActiveSeconds(run, now) : 0);

            // The phone's end time and active time (also for a Finish pressed
            // offline and synced later), checked against the stored GPS legs (UC-02).
            var legFixes = await LoadLegFixesAsync(runId);
            var timing = TerritoryDetectionService.ResolveFinishTiming(
                run.StartedAt, now, serverActiveSeconds, request?.EndedAt, request?.ActiveSeconds, legFixes);

            run = await claims.CompleteRunAsync(transaction, run, timing, now);
        }

        var territories = await GetLandWonByRunAsync(runId, userId);
        var decisions = await GetClaimDecisionsAsync(runId);
        await transaction.CommitAsync();

        return new EndRunResponse(run.Id, run.Status, run.EndedAt, run.ActiveSeconds, run.ValidationStatus,
            alreadyCompleted, territories.Count, territories, decisions);
    }

    // Claims already saved are never repeated (UC-05/UC-07).
    private async Task<Run> RetryFailedFinishAsync(
        IDbContextTransaction transaction, Run run, string userId)
    {
        var now = DateTime.UtcNow;
        var retry = await claims.ProcessFinishedRunAsync(transaction, run.Id, userId, run.EndedAt ?? now, now);

        // A failed retry cleared the change tracker: load the run again.
        if (!retry.Succeeded)
            run = await db.Runs.SingleAsync(r => r.Id == run.Id);

        run.ValidationStatus = TerritoryDetectionService.DecideValidationStatus(retry.Succeeded, retry.ApprovedClaimCount);
        if (retry.Analysis != null)
            run.TotalDistanceMeters = retry.Analysis.GenuineDistanceMeters;

        await db.SaveChangesAsync();
        return run;
    }

    // Only the timing columns of the run's GPS points.
    private Task<List<RunGpsPoint>> LoadLegFixesAsync(Guid runId) =>
        db.RunGpsPoints.AsNoTracking()
            .Where(p => p.RunId == runId)
            .Select(p => new RunGpsPoint { SequenceNo = p.SequenceNo, LegNo = p.LegNo, RecordedAt = p.RecordedAt })
            .ToListAsync();

    // Land won by this run. A piece merged into another piece of the same
    // run is already inside that piece, so it is left out. Pieces merged
    // into older land are still reported on their own.
    private async Task<List<RunTerritoryDto>> GetLandWonByRunAsync(Guid runId, string userId)
    {
        var saved = await db.Territories.AsNoTracking()
            .Where(t => t.RunId == runId && t.OwnerUserId == userId)
            .OrderBy(t => t.LoopIndex)
            .ToListAsync();

        var savedIds = saved.Select(t => t.Id).ToHashSet();

        return saved
            .Where(t => t.MergedIntoTerritoryId is not { } survivorId || !savedIds.Contains(survivorId))
            .Select(RunTerritoryDto.From)
            .ToList();
    }

    private Task<List<ClaimDecision>> GetClaimDecisionsAsync(Guid runId) =>
        db.TerritoryClaims.AsNoTracking()
            .Where(c => c.RunId == runId)
            .OrderBy(c => c.StartSequenceNo).ThenBy(c => c.EndSequenceNo)
            .Select(c => new ClaimDecision(c.StartSequenceNo, c.EndSequenceNo, c.Status, c.DecisionReason,
                Math.Round(c.ProposedAreaSquareMeters, 2), c.ResultTerritoryId))
            .ToListAsync();

    // ===================== Distance check =====================

    // Distance of a run from its stored GPS, skipping pause gaps, points
    // worse than 30 m accuracy and segments faster than 8 m/s.
    public async Task<RunDistanceResponse> GetDistanceAsync(string userId, Guid runId)
    {
        const double maxAccuracyMeters = 30;
        const double maxSpeedMetersPerSecond = 8;

        if (!await db.Runs.AnyAsync(r => r.Id == runId && r.UserId == userId))
            throw NotFound();

        var points = await db.RunGpsPoints.AsNoTracking()
            .Where(p => p.RunId == runId)
            .OrderBy(p => p.SequenceNo)
            .ToListAsync();

        double totalMeters = 0;
        int accepted = 0, rejected = 0;

        for (int i = 1; i < points.Count; i++)
        {
            var previous = points[i - 1];
            var current = points[i];

            if (previous.LegNo != current.LegNo)
                continue; // Never join across a pause.

            if (previous.AccuracyMeters is not { } a1 || current.AccuracyMeters is not { } a2 ||
                a1 > maxAccuracyMeters || a2 > maxAccuracyMeters)
            {
                rejected++;
                continue;
            }

            var seconds = (current.RecordedAt - previous.RecordedAt).TotalSeconds;
            if (seconds <= 0)
            {
                rejected++;
                continue;
            }

            var meters = gpsDistance.CalculateDistance(
                previous.Latitude, previous.Longitude, current.Latitude, current.Longitude);

            if (meters / seconds > maxSpeedMetersPerSecond)
            {
                rejected++;
                continue;
            }

            totalMeters += meters;
            accepted++;
        }

        return new RunDistanceResponse(runId, points.Count, accepted, rejected,
            Math.Round(totalMeters, 2), Math.Round(totalMeters / 1000, 3));
    }

    // ===================== Map and routes =====================

    // Every active territory on the map, with owner colour and label.
    // Other runners' new land appears only after their run ends, and
    // private runners show as "Anonymous profile" (FR-21).
    public async Task<List<MapTerritoryDto>> GetMapTerritoriesAsync(string viewerUserId)
    {
        var rows = await (
                from t in db.Territories.AsNoTracking()
                where t.Status == "Active" || t.Status == "Vulnerable"
                join u in db.Users on t.OwnerUserId equals u.Id
                join r in db.Runs on t.RunId equals (Guid?)r.Id into runs
                from r in runs.DefaultIfEmpty()
                orderby t.CreatedAt descending
                select new
                {
                    Territory = t,
                    // Only the owner columns the map needs (never password data).
                    Owner = new ApplicationUser
                    {
                        Id = u.Id, UserName = u.UserName, DisplayName = u.DisplayName, IsProfilePublic = u.IsProfilePublic
                    },
                    RunStatus = r == null ? null : r.Status
                })
            .ToListAsync();

        var now = DateTime.UtcNow;

        return rows
            .Where(row => TerritoryDetectionService.IsVisibleOnMap(row.Owner.Id, row.RunStatus, viewerUserId))
            .Select(row => new MapTerritoryDto(
                row.Territory.Id,
                Math.Round(row.Territory.AreaSquareMeters, 2),
                TerritoryDetectionService.OwnerColor(row.Owner.Id),
                TerritoryDetectionService.OwnerLabel(row.Owner, viewerUserId),
                row.Owner.Id == viewerUserId,
                TerritoryDetectionService.IsShielded(row.Territory, now),
                RunTerritoryDto.ToLatLngs(row.Territory.Boundary)))
            .ToList();
    }

    // The runner's finished runs as cleaned lines: no GPS jumps or bad
    // fixes, road-snapped where GPS was poor, split where GPS was missing.
    public async Task<List<RouteDto>> GetSavedRoutesAsync(string userId)
    {
        var runs = await db.Runs.AsNoTracking()
            .Where(r => r.UserId == userId && r.Status == "Completed")
            .OrderByDescending(r => r.StartedAt)
            .Select(r => new { r.Id, r.StartedAt })
            .ToListAsync();

        var runIds = runs.Select(r => r.Id).ToList();
        var pointsByRun = (await db.RunGpsPoints.AsNoTracking()
                .Where(p => runIds.Contains(p.RunId))
                .OrderBy(p => p.SequenceNo)
                .ToListAsync())
            .ToLookup(p => p.RunId);

        return runs
            .Select(run => new RouteDto(run.Id, run.StartedAt,
                detector.BuildDisplayRoute(pointsByRun[run.Id].ToList())
                    .Select((line, index) => new RouteLegDto(index + 1,
                        line.Select(p => new LatLng(p.Latitude, p.Longitude)).ToList()))
                    .ToList()))
            .Where(route => route.Legs.Count > 0)
            .ToList();
    }

    // ===================== Helpers =====================

    private async Task<Run> FindRunAsync(string userId, Guid runId) =>
        await db.Runs.SingleOrDefaultAsync(r => r.Id == runId && r.UserId == userId) ?? throw NotFound();

    // Seconds since the current running period started.
    private static int ElapsedActiveSeconds(Run run, DateTime now)
    {
        if (run.ActiveStartedAt == null)
            throw Conflict("Active start time is missing.");

        var seconds = (now - run.ActiveStartedAt.Value).TotalSeconds;
        return seconds < 0
            ? throw Conflict("Invalid running start time.")
            : (int)Math.Floor(seconds);
    }

    private static ApiException BadRequest(string text) => ApiException.Text(StatusCodes.Status400BadRequest, text);
    private static ApiException Conflict(string text) => ApiException.Text(StatusCodes.Status409Conflict, text);
    private static ApiException NotFound() => ApiException.Text(StatusCodes.Status404NotFound, "Run not found.");
}
