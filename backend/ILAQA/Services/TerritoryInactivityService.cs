using ILAQA.Data;
using ILAQA.Models;
using Microsoft.EntityFrameworkCore;

namespace ILAQA.Services
{
    // Marks territories Vulnerable once their 3-day shield ends (FR-19).
    // Runs at startup and then every hour, like PendingRegistrationCleanupService.
    public class TerritoryInactivityService
        : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TerritoryInactivityService> _logger;

        public TerritoryInactivityService(
            IServiceScopeFactory scopeFactory,
            ILogger<TerritoryInactivityService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            // Run once when the backend starts.
            await FinishAbandonedRunsAsync(stoppingToken);
            await MarkVulnerableAsync(stoppingToken);
            await RetryFailedRunsAsync(stoppingToken);

            // Then run every hour.
            using var timer = new PeriodicTimer(
                TimeSpan.FromHours(1));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await FinishAbandonedRunsAsync(stoppingToken);
                await MarkVulnerableAsync(stoppingToken);
                await RetryFailedRunsAsync(stoppingToken);
            }
        }

        // Finish runs whose phone never sent Finish and has sent no GPS for
        // Territory:AbandonedRunHours (default 6). Each run gets its own scope
        // and transaction, so one bad run cannot block the others (L3).
        private async Task FinishAbandonedRunsAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                var now = DateTime.UtcNow;
                TimeSpan limit;
                List<Guid> due;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var configuration = scope.ServiceProvider
                        .GetRequiredService<IConfiguration>();
                    int hours = configuration.GetValue(
                        "Territory:AbandonedRunHours",
                        TerritoryDetectionService.DefaultAbandonedRunHours);
                    limit = TimeSpan.FromHours(Math.Max(1, hours));

                    due = await scope.ServiceProvider
                        .GetRequiredService<TerritoryClaimService>()
                        .FindAbandonedRunIdsAsync(now, limit, cancellationToken);
                }

                foreach (var runId in due)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        await scope.ServiceProvider
                            .GetRequiredService<TerritoryClaimService>()
                            .FinishAbandonedRunAsync(runId, now, limit, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex,
                            "Could not finish abandoned run {RunId}.", runId);
                    }
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Normal application shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to look up abandoned runs.");
            }
        }

        // Retry Finish processing of recently failed runs (UC-05/UC-07
        // exception flows). Each run gets its own scope and transaction, so
        // one bad run cannot block the others. Runs older than the window
        // are left for the owner's /end retry.
        private async Task RetryFailedRunsAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                List<Guid> due;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider
                        .GetRequiredService<ApplicationDbContext>();
                    var configuration = scope.ServiceProvider
                        .GetRequiredService<IConfiguration>();

                    int windowDays = configuration.GetValue(
                        "Territory:FailedRetryWindowDays",
                        TerritoryDetectionService.DefaultFailedRetryWindowDays);
                    var since = DateTime.UtcNow.AddDays(-windowDays);

                    due = await db.Runs
                        .AsNoTracking()
                        .Where(r => r.Status == "Completed" &&
                                    r.ValidationStatus == RunValidationStatuses.Failed &&
                                    r.EndedAt > since)
                        .OrderBy(r => r.EndedAt)
                        .Select(r => r.Id)
                        .ToListAsync(cancellationToken);
                }

                foreach (var runId in due)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var claims = scope.ServiceProvider
                            .GetRequiredService<TerritoryClaimService>();

                        bool recovered = await claims.RetryFailedRunAsync(
                            runId, cancellationToken);

                        _logger.LogInformation(
                            "Retried territory processing for run {RunId}: {Outcome}.",
                            runId, recovered ? "recovered" : "still failing");
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex,
                            "Retry of territory processing failed for run {RunId}.",
                            runId);
                    }
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Normal application shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to look up runs to retry.");
            }
        }

        private async Task MarkVulnerableAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();
                var detector = scope.ServiceProvider
                    .GetRequiredService<TerritoryDetectionService>();

                var now = DateTime.UtcNow;

                // Same lock as territory claims, so a claim and this job
                // never change the same territory at the same time.
                await using var transaction =
                    await db.Database.BeginTransactionAsync(cancellationToken);

                await db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(7421001)",
                    cancellationToken);

                var active = await db.Territories
                    .Where(t => t.Status == "Active")
                    .ToListAsync(cancellationToken);

                var expired = detector.FindTerritoriesToMarkVulnerable(
                    active, now);

                foreach (var territory in expired)
                {
                    territory.Status = "Vulnerable";
                    territory.UpdatedAt = now;

                    db.TerritoryEvents.Add(new TerritoryEvent
                    {
                        TerritoryId = territory.Id,
                        PreviousOwnerUserId = territory.OwnerUserId,
                        NewOwnerUserId = territory.OwnerUserId,
                        EventType = TerritoryEventTypes.BecameVulnerable,
                        OccurredAt = now
                    });
                }

                if (expired.Count > 0)
                    await db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                if (expired.Count > 0)
                {
                    _logger.LogInformation(
                        "Marked {Count} territories as vulnerable.",
                        expired.Count);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Normal application shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to mark inactive territories as vulnerable.");
            }
        }
    }
}
