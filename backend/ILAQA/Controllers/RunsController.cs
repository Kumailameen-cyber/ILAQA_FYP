
using ILAQA.Data;
using ILAQA.DTOs;
using ILAQA.Models;
using ILAQA.Services;
using NetTopologySuite.Geometries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
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
        private readonly TerritoryClaimService _territoryClaimService;
        private readonly ILogger<RunsController> _logger;

        public RunsController(
            ApplicationDbContext context,
            GpsDistanceService gpsDistanceService,
            TerritoryDetectionService territoryDetectionService,
            TerritoryClaimService territoryClaimService,
            ILogger<RunsController> logger)
        {
            _context = context;
            _gpsDistanceService = gpsDistanceService;
            _territoryDetectionService = territoryDetectionService;
            _territoryClaimService = territoryClaimService;
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

            // Pausing an already paused run returns its current state
            // (UC-02 alternate flow: idempotent result, e.g. on a retry).
            if (run.Status == "Paused")
            {
                return Ok(new
                {
                    run.Id,
                    run.Status,
                    run.ActiveSeconds
                });
            }

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
        public async Task<IActionResult> EndRun(
            Guid runId,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
            EndRunRequest? request = null)
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

            // A finished run whose territory processing failed is retried
            // safely: claims already saved are never repeated (UC-05/UC-07).
            if (alreadyCompleted &&
                run.ValidationStatus == RunValidationStatuses.Failed)
            {
                var retryNow = DateTime.UtcNow;
                var retry = await _territoryClaimService.ProcessFinishedRunAsync(
                    transaction, runId, userId, run.EndedAt ?? retryNow, retryNow);

                if (!retry.Succeeded)
                    run = await _context.Runs.SingleAsync(r => r.Id == runId);

                run.ValidationStatus =
                    TerritoryDetectionService.DecideValidationStatus(
                        retry.Succeeded, retry.ApprovedClaimCount);

                if (retry.Analysis != null)
                    run.TotalDistanceMeters = retry.Analysis.GenuineDistanceMeters;
                await _context.SaveChangesAsync();
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

                // The phone's end time and active time (also for a Finish
                // pressed offline and synced later), checked against the
                // stored GPS legs (UC-02).
                var legFixes = await _context.RunGpsPoints
                    .AsNoTracking()
                    .Where(p => p.RunId == runId)
                    .Select(p => new RunGpsPoint
                    {
                        SequenceNo = p.SequenceNo,
                        LegNo = p.LegNo,
                        RecordedAt = p.RecordedAt
                    })
                    .ToListAsync();

                var timing = TerritoryDetectionService.ResolveFinishTiming(
                    run.StartedAt, now, run.ActiveSeconds + addedSeconds,
                    request?.EndedAt, request?.ActiveSeconds, legFixes);

                // Territory work may fail on unusual GPS; the run must still
                // finish and keep its workout (FR-17). A failure is recorded
                // as Failed and retried later. Shields count from the run's
                // real end.
                var processing = await _territoryClaimService.ProcessFinishedRunAsync(
                    transaction, runId, userId, timing.EndedAt, now);

                if (!processing.Succeeded)
                    run = await _context.Runs.SingleAsync(r => r.Id == runId);

                // Mark the run as completed.
                run.ActiveSeconds = timing.ActiveSeconds;
                run.EndedAt = timing.EndedAt;
                run.ActiveStartedAt = null;
                run.Status = "Completed";
                run.ValidationStatus =
                    TerritoryDetectionService.DecideValidationStatus(
                        processing.Succeeded, processing.ApprovedClaimCount);

                // Official distance: cleaned walking/running distance (no
                // GPS teleports or bad fixes); 0 for vehicle-speed runs.
                if (processing.Analysis != null)
                    run.TotalDistanceMeters =
                        processing.Analysis.GenuineDistanceMeters;

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

            // Report only land won by this run: a piece merged into another
            // piece of this same run is already inside that piece. Pieces
            // merged into older land are still reported on their own.
            var mergedInto = savedTerritories
                .Where(t => t.MergedIntoTerritoryId != null)
                .Select(t => t.MergedIntoTerritoryId!.Value)
                .ToList();
            if (mergedInto.Count > 0)
            {
                var survivorsFromThisRun = await _context.Territories
                    .Where(t => mergedInto.Contains(t.Id) && t.RunId == runId)
                    .Select(t => t.Id)
                    .ToListAsync();

                savedTerritories = savedTerritories
                    .Where(t => t.MergedIntoTerritoryId == null ||
                                !survivorsFromThisRun.Contains(
                                    t.MergedIntoTerritoryId.Value))
                    .ToList();
            }

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

            // Resuming a run that is already running returns its current
            // state (UC-02 alternate flow: idempotent result).
            if (run.Status == "Running")
            {
                return Ok(new
                {
                    run.Id,
                    run.Status,
                    run.ActiveStartedAt,
                    run.ActiveSeconds
                });
            }

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
        [EnableRateLimiting(GpsUploadRateLimit.PolicyName)]
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
                await _territoryClaimService.SnapPoorPointsAsync(runId);

                // Detect and save completed loops now, while the run is active.
                (newlyClaimed, _) = await _territoryClaimService.SaveNewTerritoriesAsync(
                    runId, userId, runIsFinishing: false);
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
