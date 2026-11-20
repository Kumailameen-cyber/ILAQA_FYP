using ILAQA.Common;
using ILAQA.Services;

namespace ILAQA.Tests;

// NFR-05: at most 10 login attempts a minute for one account (email or
// username), on top of the per-IP limit, so nobody can guess one runner's
// password from many addresses.
public class LoginAttemptLimitTests
{
    [Fact]
    public void TenAttemptsAreAllowed_TheEleventhIsRefused()
    {
        var limit = new LoginAttemptLimit(attemptsPerWindow: 10, window: TimeSpan.FromMinutes(1));

        for (int i = 0; i < 10; i++)
            Assert.True(limit.TryAcquire("runner1", out _));

        Assert.False(limit.TryAcquire("runner1", out var retryAfter));
        Assert.True(retryAfter > TimeSpan.Zero);
        Assert.True(retryAfter <= TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void AnotherAccountIsNotAffected()
    {
        var limit = new LoginAttemptLimit(attemptsPerWindow: 2, window: TimeSpan.FromMinutes(1));
        limit.TryAcquire("runner1", out _);
        limit.TryAcquire("runner1", out _);

        Assert.False(limit.TryAcquire("runner1", out _));
        Assert.True(limit.TryAcquire("runner2", out _));
    }

    [Fact]
    public void SameAccountWithDifferentCaseOrSpaces_SharesTheLimit()
    {
        var limit = new LoginAttemptLimit(attemptsPerWindow: 2, window: TimeSpan.FromMinutes(1));
        limit.TryAcquire("Runner@Mail.com", out _);
        limit.TryAcquire("  runner@mail.com ", out _);

        Assert.False(limit.TryAcquire("RUNNER@MAIL.COM", out _));
    }

    [Fact]
    public void AttemptsAreAllowedAgainAfterTheWindow()
    {
        var limit = new LoginAttemptLimit(attemptsPerWindow: 1, window: TimeSpan.FromMilliseconds(300));
        Assert.True(limit.TryAcquire("runner1", out _));
        Assert.False(limit.TryAcquire("runner1", out _));

        var deadline = DateTime.UtcNow.AddSeconds(3);
        bool allowedAgain = false;
        while (!allowedAgain && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
            allowedAgain = limit.TryAcquire("runner1", out _);
        }
        Assert.True(allowedAgain);
    }

    [Fact]
    public void DefaultIsTenAttemptsAMinute()
    {
        Assert.Equal(10, LoginAttemptLimit.DefaultAttemptsPerMinute);
    }

    [Fact]
    public void TooManyRequestsError_CarriesRetryAfterSeconds()
    {
        var error = ApiException.TooManyRequests("wait", retryAfter: TimeSpan.FromSeconds(41.2));
        Assert.Equal(429, error.StatusCode);
        Assert.Equal(42, error.RetryAfterSeconds);
    }
}
