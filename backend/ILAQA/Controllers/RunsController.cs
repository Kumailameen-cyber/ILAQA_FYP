
using ILAQA.Data;
using ILAQA.DTOs;
using ILAQA.Models;
using ILAQA.Services;
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
        public RunsController(ApplicationDbContext context, GpsDistanceService gpsDistanceService)
        {
            _context = context;
            _gpsDistanceService = gpsDistanceService;
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

            // Find the run belonging to this user.
            var run = await _context.Runs
                .FirstOrDefaultAsync(r =>
                    r.Id == runId && r.UserId == userId);

            if (run == null)
                return NotFound("Run not found.");

            bool alreadyCompleted = run.Status == "Completed";

            if (!alreadyCompleted &&
                run.Status != "Running" &&
                run.Status != "Paused")
            {
                return Conflict("This run cannot be completed.");
            }

            // Finish the run only once.
            if (!alreadyCompleted)
            {
                var now = DateTime.UtcNow;

                if (run.Status == "Running")
                {
                    if (run.ActiveStartedAt == null)
                        return Conflict("Active start time is missing.");

                    var seconds =
                        (now - run.ActiveStartedAt.Value).TotalSeconds;

                    if (seconds < 0)
                        return Conflict("Invalid running start time.");

                    run.ActiveSeconds += (int)Math.Floor(seconds);
                }

                run.EndedAt = now;
                run.ActiveStartedAt = null;
                run.Status = "Completed";
                run.ValidationStatus = "Pending";

                await _context.SaveChangesAsync();
            }

            // STEP 109: Load this run's GPS points.
            await _context.Entry(run)
                .Collection(r => r.GpsPoints)
                .LoadAsync();

            // Use our existing territory detection service.
            var territoryService = new TerritoryDetectionService(
                new GpsDistanceService());

            // Find valid loops, including loops before and after GPS gaps.
            var polygons = territoryService.FindTerritories(
                run.GpsPoints.ToList());

            // Prepare the result for the mobile app.
            var territories = polygons.Select(p => new
            {
                AreaSquareMeters = Math.Round(
                    territoryService.CalculateTerritoryArea(p), 2),

                Boundary = p.Coordinates.Select(c => new
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
                DetectedTerritories = territories
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

            // Only the owner may upload points to this run.
            var run = await _context.Runs
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.Id == runId && r.UserId == userId);

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
                        existing.RecordedAt != point.RecordedAt)
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

            return Ok(new
            {
                RunId = runId,
                Received = points.Count,
                Inserted = newPoints.Count,
                AlreadyExisting = points.Count - newPoints.Count
            });
        }

    }
}
