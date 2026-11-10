using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ILAQA.Models;
using Microsoft.IdentityModel.Tokens;

namespace ILAQA.Services;

// Access tokens (JWT) and refresh tokens.
public sealed class JwtService(IConfiguration configuration)
{
    // FR-02: 15 minutes unless Jwt:ExpiryMinutes says otherwise
    // (set it per environment, e.g. as Jwt__ExpiryMinutes on Azure).
    private const int DefaultExpiryMinutes = 15;

    private readonly int _expiryMinutes = configuration.GetValue("Jwt:ExpiryMinutes", DefaultExpiryMinutes);

    // Sent to the app as "expiresIn".
    public int AccessTokenSeconds => _expiryMinutes * 60;

    // A short-lived JWT tied to the user's current login session.
    public string GenerateToken(ApplicationUser user)
    {
        if (user.ActiveSessionId == null)
            throw new InvalidOperationException("User does not have an active session.");

        var key = new SymmetricSecurityKey(Convert.FromBase64String(configuration["Jwt:Key"]!));

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!),
            new("sessionId", user.ActiveSessionId.Value.ToString()) // Which login session owns this JWT.
        ];

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // 64 random bytes as a URL-safe string.
    public string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

    // Only this SHA-256 hash is stored in the database.
    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
