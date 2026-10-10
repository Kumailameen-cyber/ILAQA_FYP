
using ILAQA.DTOs;
using ILAQA.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ILAQA.Services;
using ILAQA.Data;
using Microsoft.EntityFrameworkCore;
namespace ILAQA.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly JwtService _jwtService;
        private readonly ApplicationDbContext _context;
        public AuthController(
            UserManager<ApplicationUser> userManager,JwtService jwtService,ApplicationDbContext context)
        {
            _userManager = userManager;
            _jwtService = jwtService;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            var user = new ApplicationUser
            {
                UserName = request.Username,
                Email = request.Email,
                DisplayName = request.DisplayName,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(
                user, request.Password);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Registration failed.",
                    errors = result.Errors.Select(e => e.Description)
                });
            }

            return StatusCode(201, new
            {
                message = "Registration successful!",
                userId = user.Id,
                username = user.UserName,
                displayName = user.DisplayName
            });
        }



        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            // Step 1: Find user by email
            var user = await _userManager.FindByEmailAsync(
                request.Email);

            // Step 2: Return the same error for unknown email
            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            // Step 3: Verify password securely
            bool isPasswordValid =
                await _userManager.CheckPasswordAsync(
                    user, request.Password);

            if (!isPasswordValid)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();
            // Lock this user's database row during login
            var lockedUser = await _context.Users
                .FromSqlInterpolated(
                    $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {user.Id} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (lockedUser == null)
            {
                return Unauthorized(new
                {
                    message = "User account not found."
                });
            }
            // Reload the latest user data while holding the database lock
            await _context.Entry(lockedUser).ReloadAsync();

            // Generate a new session ID for the locked user
            lockedUser.ActiveSessionId = Guid.NewGuid();

            // Save the new session ID in PostgreSQL
            var updateResult = await _userManager.UpdateAsync(lockedUser);

           

            if (!updateResult.Succeeded)
            {
                if (updateResult.Errors.Any(e =>
                    e.Code == "ConcurrencyFailure"))
                {
                    return Conflict(new
                    {
                        message = "Another login happened at the same time. Please try again."
                    });
                }

                return StatusCode(500, new
                {
                    message = "Unable to create login session."
                });
            }



            // Generate JWT access token using the new session
            var token = _jwtService.GenerateToken(lockedUser);

            // Generate a new refresh token
            var refreshToken = _jwtService.GenerateRefreshToken();

            // Hash the refresh token before saving it
            var refreshTokenHash =
                _jwtService.HashRefreshToken(refreshToken);

            // Revoke previous refresh tokens for this runner
            var oldTokens = await _context.RefreshTokens
                .Where(t => t.UserId == lockedUser.Id && t.RevokedAt == null)
                .ToListAsync();

            foreach (var oldToken in oldTokens)
            {
                oldToken.RevokedAt = DateTime.UtcNow;
            }

            // Create a new refresh token database record
            var refreshTokenRecord = new RefreshToken
            {
                UserId = lockedUser.Id,
                SessionId = lockedUser.ActiveSessionId.Value,
                TokenHash = refreshTokenHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            // Save the new record
            _context.RefreshTokens.Add(refreshTokenRecord);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // Return tokens to the mobile app
            return Ok(new
            {
                message = "Login successful!",
                accessToken = token,
                refreshToken = refreshToken,
                tokenType = "Bearer",
                expiresIn = 900,
                user = new
                {
                    id = lockedUser.Id,
                    username = lockedUser.UserName,
                    displayName = lockedUser.DisplayName
                }
            });

        }


        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(
            [FromBody] RefreshTokenRequest request)
        {
            // Step 1: Hash the refresh token received from the app
            var tokenHash = _jwtService.HashRefreshToken(
                request.RefreshToken);

            // Step 2: Find the token in PostgreSQL
            var storedToken = await _context.RefreshTokens
                .SingleOrDefaultAsync(t => t.TokenHash == tokenHash);

            // Step 3: Reject missing, expired, or revoked tokens
            if (storedToken == null ||
                !storedToken.IsActive)
            {
                return Unauthorized(new
                {
                    message = "Invalid or expired refresh token."
                });
            }
            // Start transaction before checking the user's session
            await using var transaction =
                await _context.Database.BeginTransactionAsync();



            // Step 4: Lock and load the runner's current session
            var user = await _context.Users
                .FromSqlInterpolated(
                    $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {storedToken.UserId} FOR UPDATE")
                .SingleOrDefaultAsync();


            if (user == null || user.ActiveSessionId == null ||
    storedToken.SessionId != user.ActiveSessionId.Value)
            {
                return Unauthorized(new
                {
                    message = "Session is no longer active."
                });
            }

            // Step 5: Generate replacement tokens
            var newAccessToken = _jwtService.GenerateToken(user);
            var newRefreshToken = _jwtService.GenerateRefreshToken();


            


            // Step 6: Revoke the old refresh token safely
            var revokedCount = await _context.RefreshTokens
                .Where(t =>
                    t.Id == storedToken.Id &&
                    t.RevokedAt == null &&
                    t.ExpiresAt > DateTime.UtcNow)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.RevokedAt, DateTime.UtcNow));

            if (revokedCount != 1)
            {
                return Unauthorized(new
                {
                    message = "Refresh token has already been used."
                });
            }


            // Step 7: Save the new refresh token's hash
            var newTokenRecord = new RefreshToken
            {
                UserId = user.Id,
                SessionId = user.ActiveSessionId.Value,
                TokenHash = _jwtService.HashRefreshToken(newRefreshToken),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            _context.RefreshTokens.Add(newTokenRecord);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // Step 8: Return the new tokens
            return Ok(new
            {
                message = "Tokens refreshed successfully!",
                accessToken = newAccessToken,
                refreshToken = newRefreshToken,
                tokenType = "Bearer",
                expiresIn = 900
            });
        }

    }
}