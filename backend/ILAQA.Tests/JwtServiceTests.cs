using System.IdentityModel.Tokens.Jwt;
using ILAQA.Models;
using ILAQA.Services;
using Microsoft.Extensions.Configuration;

namespace ILAQA.Tests;

// FR-02: access tokens last 15 minutes unless configured otherwise, and the
// login answer must report the real lifetime.
public class JwtServiceTests
{
    private static JwtService Service(string? expiryMinutes)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Convert.ToBase64String(new byte[32]),
            ["Jwt:Issuer"] = "ilaqa-test",
            ["Jwt:Audience"] = "ilaqa-test",
            ["Jwt:ExpiryMinutes"] = expiryMinutes
        };
        return new JwtService(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    private static readonly ApplicationUser User = new()
    {
        Id = "user-1", UserName = "runner1", ActiveSessionId = Guid.NewGuid()
    };

    private static double MinutesValid(string token) =>
        (new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo - DateTime.UtcNow).TotalMinutes;

    [Fact]
    public void MissingExpirySetting_ShouldDefaultTo15Minutes()
    {
        var jwt = Service(null);

        Assert.Equal(900, jwt.AccessTokenSeconds);
        Assert.InRange(MinutesValid(jwt.GenerateToken(User)), 14, 15.1);
    }

    [Fact]
    public void ConfiguredExpiry_ShouldBeUsedAndReported()
    {
        var jwt = Service("30");

        Assert.Equal(1800, jwt.AccessTokenSeconds);
        Assert.InRange(MinutesValid(jwt.GenerateToken(User)), 29, 30.1);
    }
}
