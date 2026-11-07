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
            await MarkVulnerableAsync(stoppingToken);

            // Then run every hour.
            using var timer = new PeriodicTimer(
                TimeSpan.FromHours(1));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await MarkVulnerableAsync(stoppingToken);
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
