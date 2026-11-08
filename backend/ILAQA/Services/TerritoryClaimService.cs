using ILAQA.Data;
using ILAQA.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;

namespace ILAQA.Services
{
    // Saves territory decisions for a run: claims, takeovers, shield
    // renewal and road snapping (FR-12, FR-16 to FR-20). The geometry
    // rules themselves live in TerritoryDetectionService; this class only
    // applies them to the database, so the API and the hourly job share
    // exactly the same logic.
    public sealed record FinishProcessingResult(
        bool Succeeded,
        int ApprovedClaimCount,
        TerritoryAnalysis? Analysis);

    public class TerritoryClaimService
    {
        private readonly ApplicationDbContext _context;
        private readonly TerritoryDetectionService _territoryDetectionService;
        private readonly GoogleRoadsService _googleRoadsService;
        private readonly ILogger<TerritoryClaimService> _logger;

        public TerritoryClaimService(
            ApplicationDbContext context,
            TerritoryDetectionService territoryDetectionService,
            GoogleRoadsService googleRoadsService,
            ILogger<TerritoryClaimService> logger)
        {
            _context = context;
            _territoryDetectionService = territoryDetectionService;
            _googleRoadsService = googleRoadsService;
            _logger = logger;
        }

        // Called only while the caller holds a transaction and a FOR UPDATE
        // lock on the matching Runs row. This makes concurrent upload/finish
        // requests serialize their claims for the same run.
        // Every meaningful candidate becomes one TerritoryClaims row with a
        // status and reason; approved claims also create a Territory and a
        // "Claimed" TerritoryEvent in the same SaveChanges (UC-06, FR-17).
        // During a run (runIsFinishing = false) a claim that would take
        // another runner's land is postponed: nothing is saved for it, so
        // Finish plans and applies it again.
        public async Task<(List<Territory> NewTerritories, TerritoryAnalysis Analysis)>
            SaveNewTerritoriesAsync(
                Guid runId, string userId, bool runIsFinishing,
                DateTime? activityTime = null)
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

            // Activity time: the run's end at Finish, otherwise its latest
            // GPS fix (never later than now, in case the phone clock is fast).
            var lastFix = gpsPoints.Max(p => p.RecordedAt);
            var shieldFrom = activityTime ??
                (lastFix < DateTime.UtcNow ? lastFix : DateTime.UtcNow);

            int nextLoopIndex = (await _context.Territories
                .Where(t => t.RunId == runId)
                .MaxAsync(t => (int?)t.LoopIndex) ?? 0) + 1;

            var now = DateTime.UtcNow;

            foreach (var planned in plannedClaims)
            {
                var candidate = planned.Candidate;

                // Shield and overlap rules for approved candidates.
                ClaimResolution? resolution = null;
                if (planned.Status == TerritoryClaimStatuses.Approved)
                {
                    // Existing land touched by this loop (any owner).
                    var overlapping = await _context.Territories
                        .Where(t => (t.Status == "Active" ||
                                     t.Status == "Vulnerable") &&
                                    t.Boundary.Intersects(candidate.Boundary))
                        .ToListAsync();

                    resolution = detector.ResolveClaim(
                        candidate.Boundary, userId, overlapping, now);

                    // Taking a rival's land waits until Finish (fix #1).
                    if (TerritoryDetectionService.ShouldPostponeClaim(
                            resolution, runIsFinishing))
                        continue;
                }

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

                if (resolution != null)
                {
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
                                // New land is protected for 3 days from
                                // the run itself (also for an offline run
                                // synced later, or a retry), so all land of
                                // one run shares one shield end.
                                ShieldExpiresAt = shieldFrom.AddDays(
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
        // activityTime is when the qualifying run ended; on a retry it is
        // earlier than now.
        public async Task RenewShieldsAsync(
            string userId, DateTime activityTime, DateTime now)
        {
            if (TerritoryDetectionService.RenewedShieldExpiry(
                    null, activityTime, now) == null)
                return;

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
                    TerritoryDetectionService.RenewedShieldExpiry(
                        territory.ShieldExpiresAt, activityTime, now);
                territory.UpdatedAt = now;
            }
        }

        // Finish-time territory work for a run, inside the caller's
        // transaction (which must hold the run row lock): catch loops not
        // saved by live uploads, apply postponed takeovers and renew the
        // owner's shields. On any failure everything since the savepoint is
        // rolled back and the change tracker is cleared, so the caller must
        // reload tracked entities. Safe to repeat: claim ranges already
        // submitted for the run are never planned again (UC-06).
        public async Task<FinishProcessingResult> ProcessFinishedRunAsync(
            IDbContextTransaction transaction,
            Guid runId,
            string userId,
            DateTime activityTime,
            DateTime now)
        {
            // Points from the last upload, or ones whose snapping failed
            // earlier, get one more road check before the final decision.
            // Done before the global claim lock, so a slow Google answer
            // never holds up other runners' claims; best effort only.
            await transaction.CreateSavepointAsync("snapping");
            try
            {
                await SnapPoorPointsAsync(runId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Road snapping failed at Finish for run {RunId}; raw GPS is used.",
                    runId);
                await transaction.RollbackToSavepointAsync("snapping");
                foreach (var entry in _context.ChangeTracker.Entries<RunGpsPoint>().ToList())
                    entry.State = EntityState.Detached;
            }

            // Claims, takeovers and shield renewal happen one at a time
            // across all runners (FR-20).
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(7421001)");

            bool succeeded = true;
            TerritoryAnalysis? analysis = null;

            await transaction.CreateSavepointAsync("territories");
            try
            {
                (_, analysis) = await SaveNewTerritoriesAsync(
                    runId, userId, runIsFinishing: true, activityTime);

                // A finished run with 200 m of genuine movement renews all
                // of this runner's shields (FR-18).
                if (_territoryDetectionService.ShouldRefreshShield(analysis))
                {
                    await RenewShieldsAsync(userId, activityTime, now);
                    await _context.SaveChangesAsync();
                }

                // Only at Finish: merging during a run would reveal it.
                await MergeAdjacentLandAsync(runId, userId, now);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Territory processing failed for finished run {RunId}.",
                    runId);
                await transaction.RollbackToSavepointAsync("territories");
                _context.ChangeTracker.Clear();
                succeeded = false;
                analysis = null;
            }

            // Includes claims approved during live uploads.
            int approvedClaimCount = await _context.TerritoryClaims
                .CountAsync(c => c.RunId == runId &&
                                 c.Status == TerritoryClaimStatuses.Approved);

            return new FinishProcessingResult(
                succeeded, approvedClaimCount, analysis);
        }

        // Merge this run's new land into adjacent land of the same owner
        // (UC-07 alternate flow, SRS 7.1). The older territory survives with
        // its id; the other row is kept as history with Status "Merged" and
        // MergedIntoTerritoryId, and both get a Merged event. Rules are in
        // TerritoryDetectionService.CanMerge and MergedBoundary.
        public async Task MergeAdjacentLandAsync(
            Guid runId, string userId, DateTime now)
        {
            var owned = await _context.Territories
                .Where(t => t.OwnerUserId == userId &&
                            (t.Status == "Active" || t.Status == "Vulnerable"))
                .ToListAsync();

            var pending = new Queue<Territory>(owned
                .Where(t => t.RunId == runId)
                .OrderBy(t => t.LoopIndex));
            bool changed = false;

            while (pending.Count > 0)
            {
                var piece = pending.Dequeue();
                if (piece.Status == "Merged")
                    continue;

                foreach (var other in owned)
                {
                    if (other.Status == "Merged" ||
                        !TerritoryDetectionService.CanMerge(piece, other))
                        continue;

                    var merged = _territoryDetectionService.MergedBoundary(
                        piece.Boundary, other.Boundary);
                    if (merged == null)
                        continue;

                    var (survivor, absorbed) = IsOlder(other, piece)
                        ? (other, piece)
                        : (piece, other);
                    Absorb(survivor, absorbed, merged, userId, now);
                    changed = true;

                    // The bigger territory may now touch more land.
                    pending.Enqueue(survivor);
                    break;
                }
            }

            if (changed)
                await _context.SaveChangesAsync();
        }

        // Older land survives a merge; pieces of one run keep loop order.
        private static bool IsOlder(Territory a, Territory b)
        {
            if (a.CreatedAt != b.CreatedAt)
                return a.CreatedAt < b.CreatedAt;
            if (a.LoopIndex != b.LoopIndex)
                return a.LoopIndex < b.LoopIndex;
            return a.Id.CompareTo(b.Id) < 0;
        }

        private void Absorb(
            Territory survivor, Territory absorbed, Polygon merged,
            string userId, DateTime now)
        {
            var survivorBefore = survivor.Boundary.Copy();

            survivor.Boundary = (Polygon)merged.Copy();
            survivor.AreaSquareMeters =
                _territoryDetectionService.CalculateTerritoryArea(merged);
            survivor.UpdatedAt = now;

            absorbed.Status = "Merged";
            absorbed.MergedIntoTerritoryId = survivor.Id;
            absorbed.UpdatedAt = now;

            _context.TerritoryEvents.Add(new TerritoryEvent
            {
                TerritoryId = survivor.Id,
                RelatedTerritoryId = absorbed.Id,
                PreviousOwnerUserId = userId,
                NewOwnerUserId = userId,
                EventType = TerritoryEventTypes.Merged,
                BoundaryBefore = survivorBefore,
                BoundaryAfter = merged.Copy(),
                OccurredAt = now
            });
            _context.TerritoryEvents.Add(new TerritoryEvent
            {
                TerritoryId = absorbed.Id,
                RelatedTerritoryId = survivor.Id,
                PreviousOwnerUserId = userId,
                NewOwnerUserId = userId,
                EventType = TerritoryEventTypes.Merged,
                BoundaryBefore = absorbed.Boundary.Copy(),
                OccurredAt = now
            });
        }

        // Retry one Failed run (hourly job). Returns false when the run is no
        // longer Failed, e.g. because the owner's /end retry got there first.
        public async Task<bool> RetryFailedRunAsync(
            Guid runId, CancellationToken cancellationToken = default)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            var run = await _context.Runs
                .FromSqlInterpolated(
                    $"SELECT * FROM \"Runs\" WHERE \"Id\" = {runId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (run == null ||
                run.Status != "Completed" ||
                run.ValidationStatus != RunValidationStatuses.Failed)
                return false;

            var now = DateTime.UtcNow;
            var result = await ProcessFinishedRunAsync(
                transaction, run.Id, run.UserId, run.EndedAt ?? now, now);

            if (!result.Succeeded)
                run = await _context.Runs.SingleAsync(
                    r => r.Id == runId, cancellationToken);

            run.ValidationStatus = TerritoryDetectionService.DecideValidationStatus(
                result.Succeeded, result.ApprovedClaimCount);

            if (result.Analysis != null)
                run.TotalDistanceMeters = result.Analysis.GenuineDistanceMeters;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result.Succeeded;
        }

        // Road-snap poor GPS points of this run that were not checked yet
        // (FR-12). Each point is sent to Google at most once: a point Google
        // could not place believably stores its raw position as "checked".
        public async Task SnapPoorPointsAsync(Guid runId)
        {
            if (!_googleRoadsService.IsEnabled)
                return;

            var points = await _context.RunGpsPoints
                .Where(p => p.RunId == runId)
                .OrderBy(p => p.SequenceNo)
                .ToListAsync();

            var detector = _territoryDetectionService;
            bool changed = false;

            // Points already inside a submitted claim keep their position:
            // moving them later would change that claim's shape and area,
            // so the same loop would be planned (claimed) a second time.
            var claimedRanges = await _context.TerritoryClaims
                .Where(c => c.RunId == runId)
                .Select(c => new { c.StartSequenceNo, c.EndSequenceNo })
                .ToListAsync();
            foreach (var point in points.Where(p =>
                         p.SnappedLatitude == null &&
                         claimedRanges.Any(r => p.SequenceNo >= r.StartSequenceNo &&
                                                p.SequenceNo <= r.EndSequenceNo)))
            {
                point.SnappedLatitude = point.Latitude;
                point.SnappedLongitude = point.Longitude;
                changed = true;
            }

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

                // Google failed or is paused: keep these points unchecked
                // (raw GPS is used meanwhile) and retry on a later upload
                // or at Finish. Never block the run on an outage.
                if (roadPoints == null)
                    break;

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
    }
}
