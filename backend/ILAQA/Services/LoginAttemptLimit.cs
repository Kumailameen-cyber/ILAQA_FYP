using System.Threading.RateLimiting;

namespace ILAQA.Services;

// Per-account limit on login attempts (NFR-05: 10 a minute). The "auth"
// policy in Program.cs already limits each IP address; this one limits
// each account, so one runner's password cannot be guessed from many
// addresses. The account is the email or username typed in, compared
// without case or surrounding spaces. Registered once (singleton).
public sealed class LoginAttemptLimit : IDisposable
{
    public const int DefaultAttemptsPerMinute = 10;

    private readonly PartitionedRateLimiter<string> _limiter;
    private readonly TimeSpan _window;

    public LoginAttemptLimit(int attemptsPerWindow, TimeSpan window)
    {
        _window = window;
        _limiter = PartitionedRateLimiter.Create<string, string>(account =>
            RateLimitPartition.GetFixedWindowLimiter(account, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = attemptsPerWindow,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    // True when this attempt may go ahead. When refused, retryAfter says
    // how long the runner should wait.
    public bool TryAcquire(string emailOrUsername, out TimeSpan retryAfter)
    {
        var account = (emailOrUsername ?? string.Empty).Trim().ToUpperInvariant();
        using var lease = _limiter.AttemptAcquire(account);

        if (lease.IsAcquired)
        {
            retryAfter = TimeSpan.Zero;
            return true;
        }

        retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) && wait > TimeSpan.Zero
            ? wait
            : _window;
        return false;
    }

    public void Dispose() => _limiter.Dispose();
}
