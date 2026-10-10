
using ILAQA.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
namespace ILAQA.Services
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(ApplicationUser user)
        {
            var key = Convert.FromBase64String(
                _configuration["Jwt:Key"]!);

            var securityKey = new SymmetricSecurityKey(key);

            var credentials = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256);


            if (user.ActiveSessionId == null)
            {
                throw new InvalidOperationException(
                    "User does not have an active session.");
            }

            var claims = new List<Claim>
{
    new Claim(JwtRegisteredClaimNames.Sub, user.Id),

    new Claim(JwtRegisteredClaimNames.Jti,
        Guid.NewGuid().ToString()),

    new Claim(ClaimTypes.NameIdentifier, user.Id),

    new Claim(ClaimTypes.Name, user.UserName!),

    // Identify which login session owns this JWT
    new Claim("sessionId", user.ActiveSessionId.Value.ToString())
};


            var expiryMinutes = int.Parse(
                _configuration["Jwt:ExpiryMinutes"]!);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }


        public string GenerateRefreshToken()
        {
            // Generate 64 cryptographically secure random bytes
            byte[] randomBytes = RandomNumberGenerator.GetBytes(64);

            // Convert to a URL-safe string
            return Convert.ToBase64String(randomBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        public string HashRefreshToken(string refreshToken)
        {
            // Convert the refresh token into bytes
            byte[] tokenBytes = Encoding.UTF8.GetBytes(refreshToken);

            // Generate a SHA-256 hash
            byte[] hashBytes = SHA256.HashData(tokenBytes);

            // Convert hash into a 64-character hexadecimal string
            return Convert.ToHexString(hashBytes);
        }

    }
}
