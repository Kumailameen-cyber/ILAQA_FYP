using ILAQA.Data;
using Microsoft.EntityFrameworkCore;

namespace ILAQA.Services;

public sealed class PendingRegistrationCleanupService(IServiceScopeFactory scopeFactory, ILogger<PendingRegistrationCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // Run once when the backend starts.
        await CleanupAsync(stoppingToken);

        // Then run every hour.
        using var timer = new PeriodicTimer(
            TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupAsync(stoppingToken);
        }
    }

    private async Task CleanupAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;

            // Delete all expired temporary registrations.
            var deletedCount = await db.PendingRegistrations
                .Where(p => p.ExpiresAt <= now)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                logger.LogInformation(
                    "Deleted {Count} expired pending registrations.",
                    deletedCount);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to clean up expired pending registrations.");
        }
    }
}
