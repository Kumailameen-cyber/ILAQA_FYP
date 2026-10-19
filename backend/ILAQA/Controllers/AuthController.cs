
using ILAQA.Data;
using ILAQA.DTOs;
using ILAQA.Models;
using ILAQA.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
namespace ILAQA.Controllers
{
    [Route("api/auth")]
    [ApiController]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly OtpService _otpService;

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly JwtService _jwtService;
        private readonly EmailService _emailService;
        private readonly ApplicationDbContext _context;
        public AuthController(
            UserManager<ApplicationUser> userManager,JwtService jwtService,ApplicationDbContext context, EmailService emailService,OtpService otpService, IPasswordHasher<ApplicationUser> passwordHasher)
        {
            _userManager = userManager;
            _jwtService = jwtService;
            _context = context;
            _emailService = emailService;
            _otpService = otpService;
            _passwordHasher = passwordHasher;
        }



        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            var username = request.Username.Trim();
            var displayName = request.DisplayName.Trim();
            var email = request.Email.Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;

            // Validate the password using ASP.NET Core Identity rules.
            var temporaryUser = new ApplicationUser
            {
                UserName = username,
                Email = email,
                DisplayName = displayName
            };

            foreach (var validator in _userManager.PasswordValidators)
            {
                var validation = await validator.ValidateAsync(
                    _userManager, temporaryUser, request.Password);

                if (!validation.Succeeded)
                {
                    return BadRequest(new
                    {
                        message = "Invalid password.",
                        errors = validation.Errors
                            .Select(e => e.Description)
                    });
                }
            }

            // Check usernames already used by permanent accounts.
            var normalizedUsername = _userManager.NormalizeName(username);

            var usernameExists = await _context.Users
                .AnyAsync(u => u.NormalizedUserName == normalizedUsername);

            if (usernameExists)
            {
                return Conflict(new
                {
                    message = "This username is already taken. Please choose another."
                });
            }

            // Check emails already used by permanent accounts.
            var normalizedEmail = _userManager.NormalizeEmail(email);

            var emailExists = await _context.Users
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail);

            if (emailExists)
            {
                return Conflict(new
                {
                    message = "This email is already registered."
                });
            }

            // Check pending registrations.
            // Expired records will be removed by a cleanup worker later.
            var existingPending = await _context.PendingRegistrations
    .AnyAsync(p =>
        (p.Email == email ||
         p.Username.ToLower() == username.ToLower()) &&
        p.ExpiresAt > now);

            if (existingPending)
            {
                return Conflict(new
                {
                    message = "Username or email is already used"
                });
            }

            // Remove expired pending registrations that would
            // otherwise block the unique database indexes.
            await _context.PendingRegistrations
    .Where(p =>
        (p.Email == email ||
         p.Username.ToLower() == username.ToLower()) &&
        p.ExpiresAt <= now)
    .ExecuteDeleteAsync();

            // Store only the Identity password hash.
            var passwordHash = _passwordHasher.HashPassword(
                temporaryUser, request.Password);

            var otp = _otpService.GenerateOtp();

            var pending = new PendingRegistration
            {
                Username = username,
                DisplayName = displayName,
                Email = email,
                PasswordHash = passwordHash,
                CreatedAt = now,
                ExpiresAt = now.AddHours(24),
                OtpExpiresAt = now.AddMinutes(10),
                FailedAttempts = 0,
                LastOtpSentAt = now
            };

            // Hash the OTP using the normalized email.
            pending.OtpHash = _otpService.HashOtp(email, otp);

            _context.PendingRegistrations.Add(pending);
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendEmailAsync(
                    email,
                    "ILAQA - Verify Your Email",
                    $"Welcome to ILAQA!\n\n" +
                    $"Your verification code is: {otp}\n\n" +
                    "This code expires in 60 seconds.\n\n" +
                    "If you didn't register, ignore this email."
                );
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    message = "Unable to send verification email. " +
                              "Please request a new code."
                });
            }

            return StatusCode(201, new
            {
                message = "Check your email for the verification code.",
                requiresEmailVerification = true,
                email
            });
        }




        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail(
            [FromBody] VerifyEmailRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Find and lock the temporary registration.
            var pending = await _context.PendingRegistrations
                .FromSqlInterpolated(
                    $"SELECT * FROM \"PendingRegistrations\" WHERE \"Email\" = {email} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (pending != null)
            {
                // Registration expires after 24 hours.
                if (pending.ExpiresAt <= now)
                {
                    return BadRequest(new
                    {
                        message = "Registration expired. Please register again."
                    });
                }

                // OTP expires after 1 minutes or 5 failed attempts.
                if (pending.OtpExpiresAt <= now ||
                    pending.FailedAttempts >= 5)
                {
                    return BadRequest(new
                    {
                        message = "Code expired or too many attempts. Request a new code."
                    });
                }

                // Check the OTP against its stored hash.
                if (!_otpService.VerifyOtp(
                    email, request.Code, pending.OtpHash))
                {
                    pending.FailedAttempts++;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return BadRequest(new
                    {
                        message = "Incorrect verification code."
                    });
                }

                // Make sure no permanent account already exists.
                var existingEmail =
                    await _userManager.FindByEmailAsync(email);

                var existingUsername =
                    await _userManager.FindByNameAsync(
                        pending.Username);

                if (existingEmail != null ||
                    existingUsername != null)
                {
                    return Conflict(new
                    {
                        message = "Email or username is already registered."
                    });
                }

                // Create the permanent account.
                var user = new ApplicationUser
                {
                    UserName = pending.Username,
                    Email = pending.Email,
                    DisplayName = pending.DisplayName,
                    PasswordHash = pending.PasswordHash,
                    EmailConfirmed = true,
                    CreatedAt = now
                };

                // CreateAsync without a password preserves the
                // password hash we already stored securely.
                var result = await _userManager.CreateAsync(user);

                if (!result.Succeeded)
                {
                    return BadRequest(new
                    {
                        message = "Unable to complete registration.",
                        errors = result.Errors
                            .Select(e => e.Description)
                    });
                }

                // Remove temporary registration after success.
                _context.PendingRegistrations.Remove(pending);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Email verified successfully! You can now log in."
                });
            }

            // Support older unverified accounts created before
            // we introduced PendingRegistrations.
            var legacyUser =
                await _userManager.FindByEmailAsync(email);

            if (legacyUser == null)
            {
                return BadRequest(new
                {
                    message = "Invalid or expired verification code."
                });
            }

            var lockedUser = await _context.Users
                .FromSqlInterpolated(
                    $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {legacyUser.Id} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (lockedUser == null)
            {
                return BadRequest(new
                {
                    message = "Invalid or expired verification code."
                });
            }

            if (lockedUser.EmailConfirmed)
            {
                return Ok(new
                {
                    message = "Email is already verified."
                });
            }

            var legacyOtp = await _context.EmailVerificationOtps
                .Where(o =>
                    o.UserId == lockedUser.Id &&
                    o.UsedAt == null)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (legacyOtp == null ||
                legacyOtp.ExpiresAt <= now ||
                legacyOtp.FailedAttempts >= 5)
            {
                return BadRequest(new
                {
                    message = "Code expired or too many attempts. Request a new code."
                });
            }

            if (!_otpService.VerifyOtp(
                lockedUser.Id, request.Code, legacyOtp.CodeHash))
            {
                legacyOtp.FailedAttempts++;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return BadRequest(new
                {
                    message = "Incorrect verification code."
                });
            }

            lockedUser.EmailConfirmed = true;
            legacyOtp.UsedAt = now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Email verified successfully! You can now log in."
            });
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {

            // Step 1: Accept either email or username
            var identifier = request.EmailOrUsername.Trim();

            ApplicationUser? user;

            if (identifier.Contains('@'))
            {
                user = await _userManager.FindByEmailAsync(identifier);
            }
            else
            {
                user = await _userManager.FindByNameAsync(identifier);
            }



            if (user == null)
            {
                // The account may still be waiting for email verification.
                var pending = await _context.PendingRegistrations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p =>
                        p.Email == identifier.ToLowerInvariant() ||
                        p.Username == identifier.ToLowerInvariant());

                if (pending != null &&
                    pending.ExpiresAt > DateTime.UtcNow)
                {
                    // Recreate a temporary Identity user for password checking.
                    var temporaryUser = new ApplicationUser
                    {
                        UserName = pending.Username,
                        Email = pending.Email,
                        PasswordHash = pending.PasswordHash
                    };

                    var passwordResult = _passwordHasher.VerifyHashedPassword(
                        temporaryUser,
                        pending.PasswordHash,
                        request.Password
                    );

                    if (passwordResult != PasswordVerificationResult.Failed)
                    {
                        return StatusCode(403, new
                        {
                            message = "Please verify your email before logging in.",
                            requiresEmailVerification = true,
                            email = pending.Email
                        });
                    }
                }

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
            if (!user.EmailConfirmed)
            {
                return StatusCode(403, new
                {
                    message = "Please verify your email before logging in.",
                    requiresEmailVerification = true
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


        [HttpPost("resend-email-otp")]
        public async Task<IActionResult> ResendEmailOtp(
            [FromBody] ResendEmailOtpRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Lock the pending registration to prevent
            // simultaneous resend requests.
            var pending = await _context.PendingRegistrations
                .FromSqlInterpolated(
                    $"SELECT * FROM \"PendingRegistrations\" WHERE \"Email\" = {email} FOR UPDATE")
                .SingleOrDefaultAsync();

            // Do not reveal whether the email exists.

            if (pending == null || pending.ExpiresAt <= now)
            {
                return StatusCode(410, new
                {
                    message = "Registration expired or unavailable. Please register again.",
                    registrationExpired = true
                });
            }


            // Registration is valid for 24 hours.
            if (pending.ExpiresAt <= now)
            {
                return BadRequest(new
                {
                    message = "Registration expired. Please register again."
                });
            }

            // Allow one resend every 60 seconds.
            if (pending.LastOtpSentAt.HasValue &&
                pending.LastOtpSentAt.Value.AddSeconds(60) > now)
            {
                return StatusCode(429, new
                {
                    message = "Please wait 60 seconds before requesting another code."
                });
            }

            // Generate a fresh OTP.
            var otp = _otpService.GenerateOtp();

            // Replace the previous OTP hash.
            pending.OtpHash = _otpService.HashOtp(email, otp);

            // New code expires after 10 minutes.
            pending.OtpExpiresAt = now.AddMinutes(10);

            // Reset incorrect attempt counter.
            pending.FailedAttempts = 0;

            pending.LastOtpSentAt = now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            try
            {
                await _emailService.SendEmailAsync(
                    email,
                    "ILAQA - New Verification Code",
                    $"Your new ILAQA verification code is: {otp}\n\n" +
                    "This code expires in 10 minutes.\n\n" +
                    "If you didn't request this code, ignore this email."
                );
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    message = "Unable to send verification email. Please try again later."
                });
            }

            return Ok(new
            {
                message = "If this email requires verification, a code will be sent."
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



        [HttpPost("logout")]
        public async Task<IActionResult> Logout(
            [FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.RefreshToken))
            {
                return BadRequest(new
                {
                    message = "Refresh token is required."
                });
            }

            // Find the session associated with this refresh token.
            var tokenHash = _jwtService.HashRefreshToken(
                request.RefreshToken);

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var storedToken = await _context.RefreshTokens
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (storedToken == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid refresh token."
                });
            }

            // Lock the user so login, refresh and logout cannot
            // modify the session simultaneously.
            var user = await _context.Users
                .FromSqlInterpolated(
                    $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {storedToken.UserId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (user == null ||
                user.ActiveSessionId == null ||
                user.ActiveSessionId.Value != storedToken.SessionId ||
                !storedToken.IsActive)
            {
                return Unauthorized(new
                {
                    message = "Session is no longer active."
                });
            }

            // Invalidate the current session.
            user.ActiveSessionId = null;

            // Revoke every refresh token belonging to this session.
            await _context.RefreshTokens
                .Where(t =>
                    t.UserId == user.Id &&
                    t.SessionId == storedToken.SessionId &&
                    t.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.RevokedAt, DateTime.UtcNow));

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Logged out successfully."
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] ForgotPasswordRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;

            const string successMessage =
                "If an eligible account exists, a password reset code will be sent.";

            // Only permanent, verified accounts can reset passwords.
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null || !user.EmailConfirmed)
            {
                return Ok(new { message = successMessage });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            await _context.Users
      .FromSqlInterpolated(
          $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {user.Id} FOR UPDATE")
      .SingleAsync();
            var reset = await _context.PasswordResetOtps
                .SingleOrDefaultAsync(x => x.UserId == user.Id);

            // Enforce 60-second resend cooldown.

            if (reset != null &&
                reset.UsedAt == null &&
                reset.ExpiresAt > now &&
                reset.FailedAttempts < 5 &&
                reset.LastOtpSentAt.HasValue &&
                reset.LastOtpSentAt.Value.AddSeconds(60) > now)

            {
                await transaction.CommitAsync();

                return Ok(new { message = successMessage });
            }

            var otp = _otpService.GenerateOtp();

            if (reset == null)
            {
                reset = new PasswordResetOtp
                {
                    UserId = user.Id
                };

                _context.PasswordResetOtps.Add(reset);
            }

            // Use a purpose-specific identifier so registration
            // and password-reset OTPs cannot be mixed.
            reset.CodeHash = _otpService.HashOtp(
                $"password-reset:{user.Id}", otp);

            reset.CreatedAt = now;
            reset.ExpiresAt = now.AddSeconds(60);
            reset.FailedAttempts = 0;
            reset.UsedAt = null;
            reset.LastOtpSentAt = now;
            // Requesting a new OTP invalidates previous reset authorization.
            reset.ResetTokenHash = null;
            reset.ResetTokenExpiresAt = null;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            try
            {
                await _emailService.SendEmailAsync(
                    email,
                    "ILAQA - Password Reset Code",
                    $"Your ILAQA password reset code is: {otp}\n\n" +
                    "This code expires in 60 seconds.\n\n" +
                    "If you did not request a password reset, " +
                    "you can safely ignore this email."
                );
            }
            catch (Exception)
            {
                // Avoid exposing account existence through
                // different responses.
                return Ok(new { message = successMessage });
            }

            return Ok(new { message = successMessage });
        }


        [HttpPost("verify-reset-otp")]
        public async Task<IActionResult> VerifyResetOtp(
            [FromBody] VerifyResetOtpRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null || !user.EmailConfirmed)
            {
                return BadRequest(new
                {
                    message = "Invalid or expired verification code."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Lock the record to prevent simultaneous verification.
            var reset = await _context.PasswordResetOtps
                .FromSqlInterpolated(
                    $"SELECT * FROM \"PasswordResetOtps\" WHERE \"UserId\" = {user.Id} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (reset == null ||
                reset.ExpiresAt <= now ||
                reset.UsedAt != null)
            {
                return BadRequest(new
                {
                    message = "Invalid or expired verification code."
                });
            }

            if (reset.FailedAttempts >= 5)
            {
                return StatusCode(429, new
                {
                    message = "Too many incorrect attempts. Request a new code."
                });
            }

            var valid = _otpService.VerifyOtp(
                $"password-reset:{user.Id}",
                request.Code,
                reset.CodeHash);

            if (!valid)
            {
                reset.FailedAttempts++;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return BadRequest(new
                {
                    message = reset.FailedAttempts >= 5
                        ? "Too many incorrect attempts. Request a new code."
                        : "Incorrect verification code."
                });
            }

            // Generate a cryptographically secure 32-byte reset token.
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var resetToken = Convert.ToHexString(tokenBytes);

            // Store only the SHA-256 hash, never the raw token.
            reset.ResetTokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(resetToken)));

            // Give the user 15 minutes to create a new password.
            reset.ResetTokenExpiresAt = now.AddMinutes(15);

            // The original OTP cannot be verified again.
            reset.UsedAt = now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Code verified successfully.",
                verified = true,
                resetToken,
                expiresIn = 900
            });
        }



        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
            [FromBody] ResetPasswordRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null || !user.EmailConfirmed)
            {
                return BadRequest(new
                {
                    message = "Invalid or expired reset authorization."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Lock user and reset record to prevent concurrent resets.
            var lockedUser = await _context.Users
                .FromSqlInterpolated(
                    $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {user.Id} FOR UPDATE")
                .SingleAsync();

            var reset = await _context.PasswordResetOtps
                .FromSqlInterpolated(
                    $"SELECT * FROM \"PasswordResetOtps\" WHERE \"UserId\" = {user.Id} FOR UPDATE")
                .SingleOrDefaultAsync();

            var now = DateTime.UtcNow;

            // The OTP expiry no longer matters after verification.
            // Only the reset token's expiry matters here.
            if (reset == null ||
                reset.ResetTokenHash == null ||
                reset.ResetTokenExpiresAt == null ||
                reset.ResetTokenExpiresAt <= now ||
                reset.UsedAt == null)
            {
                return BadRequest(new
                {
                    message = "Invalid or expired reset authorization."
                });
            }

            // Hash the token received from the mobile app.
            var receivedTokenHash = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(request.ResetToken)));

            // Compare hashes without a timing-based comparison.
            var expectedHashBytes =
                Convert.FromHexString(reset.ResetTokenHash);

            var receivedHashBytes =
                Convert.FromHexString(receivedTokenHash);

            if (!CryptographicOperations.FixedTimeEquals(
                expectedHashBytes, receivedHashBytes))
            {
                return BadRequest(new
                {
                    message = "Invalid or expired reset authorization."
                });
            }

            // Validate new password using ASP.NET Core Identity rules.
            foreach (var validator in _userManager.PasswordValidators)
            {
                var result = await validator.ValidateAsync(
                    _userManager, lockedUser, request.NewPassword);

                if (!result.Succeeded)
                {
                    return BadRequest(new
                    {
                        message = "New password does not meet security requirements.",
                        errors = result.Errors.Select(e => e.Description)
                    });
                }
            }

            // Hash and save the new password.
            lockedUser.PasswordHash =
                _userManager.PasswordHasher.HashPassword(
                    lockedUser, request.NewPassword);

            // Invalidate existing access-token sessions.
            lockedUser.ActiveSessionId = null;

            // Change the Identity security stamp.
            lockedUser.SecurityStamp = Guid.NewGuid().ToString();

            // Revoke all active refresh tokens.
            await _context.RefreshTokens
                .Where(x =>
                    x.UserId == lockedUser.Id &&
                    x.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.RevokedAt, now));

            // Consume the reset authorization so it cannot be reused.
            reset.ResetTokenHash = null;
            reset.ResetTokenExpiresAt = null;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Password reset successfully. Please sign in again."
            });
        }



        [HttpPost("cancel-password-reset")]
        public async Task<IActionResult> CancelPasswordReset(
            [FromBody] CancelPasswordResetRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _userManager.FindByEmailAsync(email);

            // Do not reveal whether the account exists.
            if (user == null || !user.EmailConfirmed)
            {
                return Ok(new
                {
                    message = "Password reset cancelled."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Lock the reset record to prevent simultaneous changes.
            var reset = await _context.PasswordResetOtps
                .FromSqlInterpolated(
                    $"SELECT * FROM \"PasswordResetOtps\" WHERE \"UserId\" = {user.Id} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (reset == null ||
                reset.ResetTokenHash == null ||
                reset.ResetTokenExpiresAt == null ||
                reset.ResetTokenExpiresAt <= DateTime.UtcNow)
            {
                return Ok(new
                {
                    message = "Password reset cancelled."
                });
            }

            // Hash the token provided by the mobile app.
            var receivedHash = SHA256.HashData(
                Encoding.UTF8.GetBytes(request.ResetToken));

            var expectedHash = Convert.FromHexString(
                reset.ResetTokenHash);

            // Only the correct token can cancel this reset.
            if (!CryptographicOperations.FixedTimeEquals(
                receivedHash, expectedHash))
            {
                return BadRequest(new
                {
                    message = "Invalid reset authorization."
                });
            }

            // Invalidate the reset authorization.
            reset.ResetTokenHash = null;
            reset.ResetTokenExpiresAt = null;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Password reset cancelled."
            });
        }




    }
}