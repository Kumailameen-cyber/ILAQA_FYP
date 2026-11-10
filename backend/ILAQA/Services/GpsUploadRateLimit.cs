using System.Security.Claims;
using System.Threading.RateLimiting;

namespace ILAQA.Services;

// Per-user limit on GPS uploads (NFR-05: initial target 20/minute,
// configurable with RateLimiting:GpsUploadsPerMinute). Each runner has
// their own window, so one runner's offline catch-up never slows
// another. A rejected batch gets 429 + Retry-After; the app keeps the
// points in SQLite and sends them again later.
public static class GpsUploadRateLimit
{
    public const string PolicyName = "gps-upload";
    public const int DefaultUploadsPerMinute = 20;

    public static RateLimitPartition<string> Partition(
        HttpContext context, int uploadsPerMinute)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Not signed in: the endpoint answers 401 anyway.
        if (string.IsNullOrWhiteSpace(userId))
            return RateLimitPartition.GetNoLimiter("anonymous");

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: "user:" + userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = uploadsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }
}
