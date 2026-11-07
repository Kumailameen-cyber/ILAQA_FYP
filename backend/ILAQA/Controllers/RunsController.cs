
using ILAQA.Data;
using ILAQA.DTOs;
using ILAQA.Models;
using ILAQA.Services;
using NetTopologySuite.Geometries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ILAQA.Controllers
{
    [ApiController]
    [Route("api/runs")]
    [Authorize]
    public class RunsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly GpsDistanceService _gpsDistanceService;
        private readonly TerritoryDetectionService _territoryDetectionService;
        private readonly GoogleRoadsService _googleRoadsService;
        private readonly ILogger<RunsController> _logger;
        public RunsController(ApplicationDbContext context, GpsDistanceService gpsDistanceService, TerritoryDetectionService territoryDetectionService, GoogleRoadsService googleRoadsService, ILogger<RunsController> logger)
        {
            _context = context;
            _gpsDistanceService = gpsDistanceService;
            _territoryDetectionService = territoryDetectionService;
            _googleRoadsService = googleRoadsService;
            _logger = logger;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartRun(
            [FromBody] StartRunRequest request)
        {
            // The user ID comes from the validated JWT.
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            if (request.RunId == Guid.Empty)
            {
                return BadRequest("RunId cannot be empty.");
            }

            // Reject timestamps that are not UTC.
            if (request.StartedAt.Kind != DateTimeKind.Utc)
            {
                return BadRequest("StartedAt must be UTC.");
            }

            // Allow small clock differences, but not future runs.
            if (request.StartedAt > DateTime.UtcNow.AddMinutes(5))
            {
                return BadRequest("StartedAt is too far in the future.");
            }

            // Prevent the same UUID being used for different users.
            var existingRun = await _context.Runs
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == request.RunId);

            if (existingRun != null)
            {
                if (existingRun.UserId != userId)
                {
                    return Conflict("RunId is already in use.");
                }

                // Same runner retrying the same request.
                if (existingRun.StartedAt != request.StartedAt)
                {
                    return Conflict(
                        "RunId already exists with a different start time.");
                }

                return Ok(new
                {
                    existingRun.Id,
                    existingRun.StartedAt,
                    existingRun.Status,
                    AlreadyExists = true
                });
            }

            var run = new Run
            {
                Id = request.RunId,
                UserId = userId,
                StartedAt = request.StartedAt,
                ActiveStartedAt = request.StartedAt,
                ActiveSeconds = 0,
                TotalDistanceMeters = 0,
                Status = "Running",
                ValidationStatus = "Pending",
                XpAwarded = 0
            };

            _context.Runs.Add(run);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                run.Id,
                run.StartedAt,
                run.Status,
                AlreadyExists = false
            });
        }

        [HttpGet("{runId:guid}/distance")]
        public async Task<IActionResult> GetRunDistance(Guid runId)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var run = await _context.Runs
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.Id == runId && r.UserId == userId);

            if (run == null)
                return NotFound("Run not found.");

            var points = await _context.RunGpsPoints
                .AsNoTracking()
                .Where(p => p.RunId == runId)
                .OrderBy(p => p.SequenceNo)
                .ToListAsync();

            double totalDistance = 0;
            int acceptedSegments = 0;
            int rejectedSegments = 0;

            const double maxAccuracyMeters = 30;
            const double maxSpeedMetersPerSecond = 8;

            for (int i = 1; i < points.Count; i++)
            {
                var previous = points[i - 1];
                var current = points[i];

                // Never connect across a pause/resume boundary.
                if (previous.LegNo != current.LegNo)
                    continue;

                // Reject inaccurate or unknown-accuracy points.
                if (!previous.AccuracyMeters.HasValue ||
                    !current.AccuracyMeters.HasValue ||
                    previous.AccuracyMeters.Value > maxAccuracyMeters ||
                    current.AccuracyMeters.Value > maxAccuracyMeters)
                {
                    rejectedSegments++;
                    continue;
                }

                double seconds =
                    (current.RecordedAt - previous.RecordedAt).TotalSeconds;

                if (seconds <= 0)
                {
                    rejectedSegments++;
                    continue;
                }

                double meters = _gpsDistanceService.CalculateDistance(
                    previous.Latitude,
                    previous.Longitude,
                    current.Latitude,
                    current.Longitude);

                double speed = meters / seconds;

                // Reject unrealistic movement.
                if (speed > maxSpeedMetersPerSecond)
                {
                    rejectedSegments++;
                    continue;
                }

                totalDistance += meters;
                acceptedSegments++;
            }

            return Ok(new
            {
                RunId = runId,
                TotalGpsPoints = points.Count,
                AcceptedSegments = acceptedSegments,
                RejectedSegments = rejectedSegments,
                CalculatedDistanceMeters = Math.Round(totalDistance, 2),
                CalculatedDistanceKm = Math.Round(totalDistance / 1000, 3)
            });
        }

        [HttpPost("{runId:guid}/pause")]
        public async Task<IActionResult> PauseRun(Guid runId)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var run = await _context.Runs
                .FirstOrDefaultAsync(r =>
                    r.Id == runId && r.UserId == userId);

            if (run == null)
                return NotFound("Run not found.");

            if (run.Status != "Running")
                return Conflict("Only a running session can be paused.");

            if (run.ActiveStartedAt == null)
                return Conflict("Active start time is missing.");

            var now = DateTime.UtcNow;

            var elapsedSeconds =
                (now - run.ActiveStartedAt.Value).TotalSeconds;

            if (elapsedSeconds < 0)
                return Conflict("Invalid running start time.");

            run.ActiveSeconds += (int)Math.Floor(elapsedSeconds);
            run.ActiveStartedAt = null;
            run.Status = "Paused";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                run.Id,
                run.Status,
                run.ActiveSeconds
            });
        }


        [HttpPost("{runId:guid}/end")]
        public async Task<IActionResult> EndRun(Guid runId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            // Keep run completion and territory saving together.
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Lock the run so two Finish requests cannot save duplicates.
            var run = await _context.Runs
                .FromSqlInterpolated(
                    $"SELECT * FROM \"Runs\" WHERE \"Id\" = {runId} AND \"UserId\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (run == null)
                return NotFound("Run not found.");

            bool alreadyCompleted = run.Status == "Completed";

            if (!alreadyCompleted &&
                run.Status != "Running" &&
                run.Status != "Paused")
            {
                return Conflict("This run cannot be completed.");
            }

            // Process territories only the first time the run ends.
            if (!alreadyCompleted)
            {
                var now = DateTime.UtcNow;
                int addedSeconds = 0;

                if (run.Status == "Running")
                {
                    if (run.ActiveStartedAt == null)
                        return Conflict("Active start time is missing.");

                    var seconds =
                        (now - run.ActiveStartedAt.Value).TotalSeconds;

                    if (seconds < 0)
                        return Conflict("Invalid running start time.");

                    addedSeconds = (int)Math.Floor(seconds);
                }

                // Claims, takeovers and shield renewal happen one at a time
                // across all runners (FR-20).
                await _context.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(7421001)");

                // Territory work may fail on unusual GPS; the run must still
                // finish and keep its workout (FR-17).
                await transaction.CreateSavepointAsync("territories");
                try
                {
                    // Catch any valid loops not yet saved by live uploads.
                    var (_, analysis) =
                        await SaveNewTerritoriesAsync(runId, userId);

                    // A finished run with 200 m of genuine movement renews
                    // all of this runner's shields (FR-18).
                    if (_territoryDetectionService.ShouldRefreshShield(analysis))
                    {
                        await RenewShieldsAsync(userId, now);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex,
                        "Territory processing failed when finishing run {RunId}.",
                        runId);
                    await transaction.RollbackToSavepointAsync("territories");
                    _context.ChangeTracker.Clear();
                    run = await _context.Runs.SingleAsync(r => r.Id == runId);
                }

                // Mark the run as completed.
                run.ActiveSeconds += addedSeconds;
                run.EndedAt = now;
                run.ActiveStartedAt = null;
                run.Status = "Completed";
                run.ValidationStatus = "Pending";

                // Save the run and claimed territories together.
                await _context.SaveChangesAsync();
            }

            // Get the territories already stored for this run.
            var savedTerritories = await _context.Territories
                .AsNoTracking()
                .Where(t => t.RunId == runId &&
                            t.OwnerUserId == userId)
                .OrderBy(t => t.LoopIndex)
                .ToListAsync();

            var claimDecisions = await GetClaimDecisionsAsync(runId);

            await transaction.CommitAsync();

            // Return saved territories to React Native.
            var territories = savedTerritories.Select(t => new
            {
                TerritoryId = t.Id,
                AreaSquareMeters = Math.Round(t.AreaSquareMeters, 2),
                Boundary = t.Boundary.Coordinates.Select(c => new
                {
                    Latitude = c.Y,
                    Longitude = c.X
                }).ToList()
            }).ToList();

            return Ok(new
            {
                run.Id,
                run.Status,
                run.EndedAt,
                run.ActiveSeconds,
                run.ValidationStatus,
                AlreadyCompleted = alreadyCompleted,
                DetectedTerritoryCount = territories.Count,
                DetectedTerritories = territories,
                ClaimDecisions = claimDecisions
            });
        }

        [HttpPost("{runId:guid}/resume")]
        public async Task<IActionResult> ResumeRun(Guid runId)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            // Only the owner can resume this run.
            var run = await _context.Runs
                .FirstOrDefaultAsync(r =>
                    r.Id == runId && r.UserId == userId);

            if (run == null)
                return NotFound("Run not found.");

            // Only paused runs can be resumed.
            if (run.Status != "Paused")
                return Conflict("Only a paused run can be resumed.");

            // Prevent an inconsistent running timer.
            if (run.ActiveStartedAt != null)
                return Conflict("This run already has an active start time.");

            // Begin a new active running period.
            run.ActiveStartedAt = DateTime.UtcNow;
            run.Status = "Running";

            // Keep ActiveSeconds unchanged.
            // Time spent paused is not counted.

            await _context.SaveChangesAsync();

            return Ok(new
            {
                run.Id,
                run.Status,
                run.ActiveStartedAt,
                run.ActiveSeconds
            });
        }

        [HttpPost("{runId:guid}/points")]
        public async Task<IActionResult> UploadGpsPoints(
            Guid runId,
            [FromBody] List<RunGpsPointDto> points)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            if (points == null || points.Count == 0)
                return BadRequest("At least one GPS point is required.");

            if (points.Count > 500)
                return BadRequest("Maximum 500 points per request.");

            // Serialize live GPS uploads with Finish requests for this run.
            // The lock is held until territory claims are saved and committed.
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var run = await _context.Runs
                .FromSqlInterpolated(
                    $"SELECT * FROM \"Runs\" WHERE \"Id\" = {runId} AND \"UserId\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (run == null)
                return NotFound("Run not found.");

            if (run.Status != "Running" &&
                run.Status != "Paused")
            {
                return Conflict(
                    "GPS points cannot be uploaded to this run.");
            }

            // Reject duplicate sequence numbers in the same request.
            if (points.Select(p => p.SequenceNo).Distinct().Count()
                != points.Count)
            {
                return BadRequest(
                    "Duplicate sequence numbers in request.");
            }

            foreach (var point in points)
            {
                if (point.SequenceNo < 1 || point.LegNo < 1)
                    return BadRequest("Invalid sequence or leg number.");

                if (!double.IsFinite(point.Latitude) ||
                    !double.IsFinite(point.Longitude) ||
                    point.Latitude < -90 || point.Latitude > 90 ||
                    point.Longitude < -180 || point.Longitude > 180)
                {
                    return BadRequest("Invalid GPS coordinates.");
                }

                if (point.AccuracyMeters.HasValue &&
                    (!double.IsFinite(point.AccuracyMeters.Value) ||
                     point.AccuracyMeters.Value < 0))
                {
                    return BadRequest("Invalid GPS accuracy.");
                }

                if (point.RecordedAt.Kind != DateTimeKind.Utc)
                    return BadRequest("GPS timestamps must be UTC.");

                if (point.RecordedAt < run.StartedAt.AddMinutes(-1))
                    return BadRequest("GPS point is before run start.");

                if (point.RecordedAt > DateTime.UtcNow.AddMinutes(5))
                    return BadRequest("GPS point is in the future.");
            }

            var sequenceNumbers = points
                .Select(p => p.SequenceNo)
                .ToList();

            // Find points already saved during earlier uploads.
            var existingPoints = await _context.RunGpsPoints
                .AsNoTracking()
                .Where(p =>
                    p.RunId == runId &&
                    sequenceNumbers.Contains(p.SequenceNo))
                .ToDictionaryAsync(p => p.SequenceNo);

            // A retry must not silently overwrite different GPS data.
            foreach (var point in points)
            {
                if (existingPoints.TryGetValue(
                    point.SequenceNo, out var existing))
                {
                    if (existing.LegNo != point.LegNo ||
                        existing.Latitude != point.Latitude ||
                        existing.Longitude != point.Longitude ||
                        existing.AccuracyMeters != point.AccuracyMeters ||
                        existing.RecordedAt.Ticks != point.RecordedAt.Ticks)
                    {
                        return Conflict(
                            $"Sequence {point.SequenceNo} already exists " +
                            "with different GPS data.");
                    }
                }
            }

            var newPoints = points
                .Where(p => !existingPoints.ContainsKey(p.SequenceNo))
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

            _context.RunGpsPoints.AddRange(newPoints);
            await _context.SaveChangesAsync();

            // The GPS points above are always kept. If territory work fails
            // on unusual GPS, it is logged and retried on the next upload.
            var newlyClaimed = new List<Territory>();
            await transaction.CreateSavepointAsync("territories");
            try
            {
                // Move poor GPS points onto the road once.
                await SnapPoorPointsAsync(runId);

                // Detect and save completed loops now, while the run is active.
                (newlyClaimed, _) = await SaveNewTerritoriesAsync(runId, userId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Territory processing failed for upload to run {RunId}.",
                    runId);
                await transaction.RollbackToSavepointAsync("territories");
                _context.ChangeTracker.Clear();
            }

            // Return all confirmed territories, including claims from earlier
            // uploads, so an upload retry can restore the map correctly.
            var allClaimed = await _context.Territories
                .AsNoTracking()
                .Where(t => t.RunId == runId && t.OwnerUserId == userId)
                .OrderBy(t => t.LoopIndex)
                .ToListAsync();

            var claimDecisions = await GetClaimDecisionsAsync(runId);

            await transaction.CommitAsync();

            return Ok(new
            {
                RunId = runId,
                Received = points.Count,
                Inserted = newPoints.Count,
                AlreadyExisting = points.Count - newPoints.Count,
                NewlyClaimedCount = newlyClaimed.Count,
                ConfirmedTerritories = allClaimed.Select(t => new
                {
                    TerritoryId = t.Id,
                    t.LoopIndex,
                    AreaSquareMeters = Math.Round(t.AreaSquareMeters, 2),
                    Boundary = t.Boundary.Coordinates.Select(c => new
                    {
                        Latitude = c.Y,
                        Longitude = c.X
                    }).ToList()
                }).ToList(),
                ClaimDecisions = claimDecisions
            });
        }

        // Called only while the caller holds a transaction and a FOR UPDATE
        // lock on the matching Runs row. This makes concurrent upload/finish
        // requests serialize their claims for the same run.
        // Every meaningful candidate becomes one TerritoryClaims row with a
        // status and reason; approved claims also create a Territory and a
        // "Claimed" TerritoryEvent in the same SaveChanges (UC-06, FR-17).
        private async Task<(List<Territory> NewTerritories, TerritoryAnalysis Analysis)>
            SaveNewTerritoriesAsync(Guid runId, string userId)
        {
            var gpsPoints = await _context.RunGpsPoints
                .AsNoTracking()
                .Where(p => p.RunId == runId)
                .OrderBy(p => p.SequenceNo)
                .ToListAsync();

            var detector = _territoryDetectionService;
            var analysis = detector.AnalyzeRun(gpsPoints);

            var submittedRanges = await _context.TerritoryClaims
                .AsNoTracking()
                .Where(c => c.RunId == runId)
                .Select(c => new
                {
                    c.StartSequenceNo,
                    c.EndSequenceNo,
                    c.ProposedAreaSquareMeters
                })
                .ToListAsync();

            var plannedClaims = detector.PlanNewClaims(
                analysis,
                submittedRanges
                    .Select(r => (r.StartSequenceNo, r.EndSequenceNo,
                        r.ProposedAreaSquareMeters))
                    .ToList());

            var newlyClaimed = new List<Territory>();
            if (plannedClaims.Count == 0)
                return (newlyClaimed, analysis);

            // Only one territory claim is processed at a time across all
            // runners, so overlapping claims can never both win (FR-20).
            // The lock is released when the caller's transaction ends.
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(7421001)");

            int nextLoopIndex = (await _context.Territories
                .Where(t => t.RunId == runId)
                .MaxAsync(t => (int?)t.LoopIndex) ?? 0) + 1;

            var now = DateTime.UtcNow;

            foreach (var planned in plannedClaims)
            {
                var candidate = planned.Candidate;
                var claim = new TerritoryClaim
                {
                    RunId = runId,
                    StartSequenceNo = candidate.StartSequenceNo,
                    EndSequenceNo = candidate.EndSequenceNo,
                    ProposedBoundary = (Polygon)candidate.Boundary.Copy(),
                    ProposedAreaSquareMeters = candidate.AreaSquareMeters,
                    Status = planned.Status,
                    DecisionReason = planned.DecisionReason,
                    XpAwarded = 0,
                    SubmittedAt = now,
                    DecidedAt = now
                };
                _context.TerritoryClaims.Add(claim);

                if (planned.Status == TerritoryClaimStatuses.Approved)
                {
                    // Existing land touched by this loop (any owner).
                    var overlapping = await _context.Territories
                        .Where(t => (t.Status == "Active" ||
                                     t.Status == "Vulnerable") &&
                                    t.Boundary.Intersects(candidate.Boundary))
                        .ToListAsync();

                    var resolution = detector.ResolveClaim(
                        candidate.Boundary, userId, overlapping, now);

                    if (resolution.RejectionReason != null)
                    {
                        claim.Status = TerritoryClaimStatuses.Rejected;
                        claim.DecisionReason = resolution.RejectionReason;
                    }
                    else
                    {
                        claim.NewlyClaimedAreaSquareMeters =
                            resolution.AwardedAreaSquareMeters;

                        foreach (var piece in resolution.AwardedPieces)
                        {
                            var territory = new Territory
                            {
                                RunId = runId,
                                LoopIndex = nextLoopIndex++,
                                OwnerUserId = userId,
                                Boundary = (Polygon)piece.Copy(),
                                AreaSquareMeters =
                                    detector.CalculateTerritoryArea(piece),
                                ShieldExpiresAt = now.AddDays(
                                    TerritoryDetectionService.ShieldDays),
                                Status = "Active",
                                CreatedAt = now,
                                UpdatedAt = now
                            };

                            claim.ResultTerritoryId ??= territory.Id;
                            _context.Territories.Add(territory);
                            _context.TerritoryEvents.Add(new TerritoryEvent
                            {
                                TerritoryId = territory.Id,
                                ClaimId = claim.Id,
                                NewOwnerUserId = userId,
                                EventType = TerritoryEventTypes.Claimed,
                                BoundaryAfter = piece.Copy(),
                                OccurredAt = now
                            });

                            newlyClaimed.Add(territory);
                        }

                        foreach (var takeover in resolution.Takeovers)
                        {
                            ApplyTakeover(takeover, claim, userId, now);
                        }
                    }
                }

                // Save each claim so the next one sees its result.
                await _context.SaveChangesAsync();
            }

            return (newlyClaimed, analysis);
        }

        // The rival keeps only what the loop did not cover. Extra pieces
        // become separate territories with the same owner and shield.
        private void ApplyTakeover(
            TerritoryTakeover takeover,
            TerritoryClaim claim,
            string newOwnerUserId,
            DateTime now)
        {
            var old = takeover.Territory;
            var boundaryBefore = old.Boundary.Copy();
            var remaining = takeover.RemainingPieces
                .OrderByDescending(piece =>
                    _territoryDetectionService.CalculateTerritoryArea(piece))
                .ToList();

            if (remaining.Count == 0)
            {
                old.Status = "TakenOver";
            }
            else
            {
                old.Boundary = (Polygon)remaining[0].Copy();
                old.AreaSquareMeters =
                    _territoryDetectionService.CalculateTerritoryArea(remaining[0]);
            }
            old.UpdatedAt = now;

            _context.TerritoryEvents.Add(new TerritoryEvent
            {
                TerritoryId = old.Id,
                ClaimId = claim.Id,
                RelatedTerritoryId = claim.ResultTerritoryId,
                PreviousOwnerUserId = old.OwnerUserId,
                NewOwnerUserId = newOwnerUserId,
                EventType = TerritoryEventTypes.TakenOver,
                BoundaryBefore = boundaryBefore,
                BoundaryAfter = remaining.Count == 0
                    ? null
                    : remaining[0].Copy(),
                OccurredAt = now
            });

            foreach (var piece in remaining.Skip(1))
            {
                var split = new Territory
                {
                    RunId = null,
                    LoopIndex = 0,
                    OwnerUserId = old.OwnerUserId,
                    Boundary = (Polygon)piece.Copy(),
                    AreaSquareMeters =
                        _territoryDetectionService.CalculateTerritoryArea(piece),
                    ShieldExpiresAt = old.ShieldExpiresAt,
                    Status = old.Status,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.Territories.Add(split);
                _context.TerritoryEvents.Add(new TerritoryEvent
                {
                    TerritoryId = split.Id,
                    ClaimId = claim.Id,
                    RelatedTerritoryId = old.Id,
                    PreviousOwnerUserId = old.OwnerUserId,
                    NewOwnerUserId = old.OwnerUserId,
                    EventType = TerritoryEventTypes.Split,
                    BoundaryAfter = piece.Copy(),
                    OccurredAt = now
                });
            }
        }

        // Renew every shield of this runner for another 3 days. Vulnerable
        // land becomes protected again (saved by the caller).
        private async Task RenewShieldsAsync(string userId, DateTime now)
        {
            var owned = await _context.Territories
                .Where(t => t.OwnerUserId == userId &&
                            (t.Status == "Active" || t.Status == "Vulnerable"))
                .ToListAsync();

            foreach (var territory in owned)
            {
                if (territory.Status == "Vulnerable")
                {
                    _context.TerritoryEvents.Add(new TerritoryEvent
                    {
                        TerritoryId = territory.Id,
                        PreviousOwnerUserId = userId,
                        NewOwnerUserId = userId,
                        EventType = TerritoryEventTypes.ShieldRestored,
                        OccurredAt = now
                    });
                }

                territory.Status = "Active";
                territory.ShieldExpiresAt =
                    now.AddDays(TerritoryDetectionService.ShieldDays);
                territory.UpdatedAt = now;
            }
        }

        // Road-snap poor GPS points of this run that were not checked yet
        // (FR-12). Each point is sent to Google at most once: a point Google
        // could not place believably stores its raw position as "checked".
        private async Task SnapPoorPointsAsync(Guid runId)
        {
            if (!_googleRoadsService.IsEnabled)
                return;

            var points = await _context.RunGpsPoints
                .Where(p => p.RunId == runId)
                .OrderBy(p => p.SequenceNo)
                .ToListAsync();

            var detector = _territoryDetectionService;
            bool changed = false;

            foreach (var stretch in detector.BuildSnapStretches(points))
            {
                var pending = stretch
                    .Where(p => p.SnappedLatitude == null &&
                                p.AccuracyMeters >
                                    TerritoryDetectionService.SnapAccuracyThresholdMeters &&
                                p.AccuracyMeters <= 30)
                    .ToList();

                if (pending.Count == 0)
                    continue;

                var roadPoints = await _googleRoadsService.SnapAsync(stretch);
                var result = detector.ApplySnappedPoints(stretch, roadPoints);

                foreach (var point in pending)
                {
                    var placed = result.Single(r => r.SequenceNo == point.SequenceNo);
                    point.SnappedLatitude = placed.Latitude;
                    point.SnappedLongitude = placed.Longitude;
                    changed = true;
                }
            }

            if (changed)
                await _context.SaveChangesAsync();
        }

        // Every claim decision for a run, including rejection reasons (FR-17).
        private async Task<List<ClaimDecision>> GetClaimDecisionsAsync(
            Guid runId)
        {
            return await _context.TerritoryClaims
                .AsNoTracking()
                .Where(c => c.RunId == runId)
                .OrderBy(c => c.StartSequenceNo)
                .ThenBy(c => c.EndSequenceNo)
                .Select(c => new ClaimDecision(
                    c.StartSequenceNo,
                    c.EndSequenceNo,
                    c.Status,
                    c.DecisionReason,
                    Math.Round(c.ProposedAreaSquareMeters, 2),
                    c.ResultTerritoryId))
                .ToListAsync();
        }

        public sealed record ClaimDecision(
            int StartSequenceNo,
            int EndSequenceNo,
            string Status,
            string? DecisionReason,
            double AreaSquareMeters,
            Guid? TerritoryId);


        [HttpGet("routes")]
        public async Task<IActionResult> GetMySavedRoutes()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var runs = await _context.Runs
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.Status == "Completed")
                .OrderByDescending(r => r.StartedAt)
                .ToListAsync();

            var runIds = runs.Select(r => r.Id).ToList();

            var points = await _context.RunGpsPoints
                .AsNoTracking()
                .Where(p => runIds.Contains(p.RunId))
                .OrderBy(p => p.SequenceNo)
                .ToListAsync();

            var routes = runs.Select(run => new
            {
                RunId = run.Id,
                StartedAt = run.StartedAt,
                // Cleaned lines: no GPS teleports or bad fixes, road-snapped
                // where GPS was poor, split where GPS was missing.
                Legs = _territoryDetectionService
                    .BuildDisplayRoute(points
                        .Where(p => p.RunId == run.Id)
                        .ToList())
                    .Select((line, index) => new
                    {
                        LegNo = index + 1,
                        Coordinates = line.Select(p => new
                        {
                            Latitude = p.Latitude,
                            Longitude = p.Longitude
                        }).ToList()
                    })
                    .ToList()
            })
            .Where(run => run.Legs.Count > 0)
            .ToList();

            return Ok(routes);
        }
        
        [HttpGet("territories")]
        public async Task<IActionResult> GetMapTerritories()
        {
            var viewerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(viewerUserId))
                return Unauthorized();

            // Only the owner columns the map needs (never password data).
            var rows = await (
                from t in _context.Territories.AsNoTracking()
                where t.Status == "Active" || t.Status == "Vulnerable"
                join u in _context.Users on t.OwnerUserId equals u.Id
                join r in _context.Runs on t.RunId equals (Guid?)r.Id into runs
                from r in runs.DefaultIfEmpty()
                orderby t.CreatedAt descending
                select new
                {
                    Territory = t,
                    Owner = new ApplicationUser
                    {
                        Id = u.Id,
                        UserName = u.UserName,
                        DisplayName = u.DisplayName,
                        IsProfilePublic = u.IsProfilePublic
                    },
                    RunStatus = r == null ? null : r.Status
                })
                .ToListAsync();

            // Other runners' new land appears only after their run ends.
            rows = rows
                .Where(row => TerritoryDetectionService.IsVisibleOnMap(
                    row.Owner.Id, row.RunStatus, viewerUserId))
                .ToList();

            var now = DateTime.UtcNow;

            // Private runners appear as "Anonymous profile" (FR-21).
            return Ok(rows.Select(row => new
            {
                TerritoryId = row.Territory.Id,
                AreaSquareMeters = Math.Round(row.Territory.AreaSquareMeters, 2),
                ColorHex = TerritoryDetectionService.OwnerColor(row.Owner.Id),
                OwnerLabel = TerritoryDetectionService.OwnerLabel(
                    row.Owner, viewerUserId),
                IsMine = row.Owner.Id == viewerUserId,
                IsShielded = TerritoryDetectionService.IsShielded(
                    row.Territory, now),
                Boundary = row.Territory.Boundary.Coordinates.Select(c => new
                {
                    Latitude = c.Y,
                    Longitude = c.X
                }).ToList()
            }));
        }
    }
}
