using System.Security.Cryptography;
using System.Text;
using ILAQA.Common;
using ILAQA.Data;
using ILAQA.Models;
using ILAQA.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ILAQA.Features.Auth;

// Business logic for /api/auth: registration with email OTP, login with one
// active session per runner, token refresh, logout and password reset.
// Errors are thrown as ApiException and written by GlobalExceptionHandler.
public sealed class AuthService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IPasswordHasher<ApplicationUser> passwordHasher,
    JwtService jwtService,
    OtpService otpService,
    EmailService emailService,
    ILogger<AuthService> logger)
{
    private const int AccessTokenSeconds = 900;
    private const int MaxOtpAttempts = 5;
    private const string InvalidResetMessage = "Invalid or expired reset authorization.";
    private const string InvalidCodeMessage = "Invalid or expired verification code.";
    private const string CodeExpiredMessage = "Code expired or too many attempts. Request a new code.";
    private const string EmailVerifiedMessage = "Email verified successfully! You can now log in.";
    private const string VerifyFirstMessage = "Please verify your email before logging in.";

    // ===================== Register =====================

    // Saves a pending registration and emails a 6-digit code. The real
    // account is only created once the email is verified.
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var username = request.Username.Trim();
        var displayName = request.DisplayName.Trim();
        var email = NormalizeEmail(request.Email);
        var now = DateTime.UtcNow;

        var newUser = new ApplicationUser { UserName = username, Email = email, DisplayName = displayName };
        await PasswordHelper.ValidateAsync(userManager, newUser, request.Password, "Invalid password.");

        var normalizedUsername = userManager.NormalizeName(username);
        if (await db.Users.AnyAsync(u => u.NormalizedUserName == normalizedUsername))
            throw ApiException.Conflict("This username is already taken. Please choose another.");

        var normalizedEmail = userManager.NormalizeEmail(email);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail))
            throw ApiException.Conflict("This email is already registered.");

        var lowerUsername = username.ToLower();
        var samePending = db.PendingRegistrations
            .Where(p => p.Email == email || p.Username.ToLower() == lowerUsername);

        if (await samePending.AnyAsync(p => p.ExpiresAt > now))
            throw ApiException.Conflict("Username or email is already used");

        // Expired pending rows would still block the unique indexes.
        await samePending.Where(p => p.ExpiresAt <= now).ExecuteDeleteAsync();

        var otp = otpService.GenerateOtp();
        db.PendingRegistrations.Add(new PendingRegistration
        {
            Username = username,
            DisplayName = displayName,
            Email = email,
            PasswordHash = passwordHasher.HashPassword(newUser, request.Password),
            OtpHash = otpService.HashOtp(email, otp),
            CreatedAt = now,
            ExpiresAt = now.AddHours(24),
            OtpExpiresAt = now.AddMinutes(10),
            LastOtpSentAt = now
        });
        await db.SaveChangesAsync();

        var sent = await TrySendEmailAsync(email, "ILAQA - Verify Your Email",
            $"Welcome to ILAQA!\n\nYour verification code is: {otp}\n\n" +
            "This code expires in 10 minutes.\n\nIf you didn't register, ignore this email.");

        if (!sent)
            throw new ApiException(StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("Unable to send verification email. Please request a new code."));

        return new RegisterResponse("Check your email for the verification code.", true, email);
    }

    // ===================== Verify email =====================

    public async Task<MessageResponse> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var now = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync();

        var pending = await db.LockPendingRegistrationAsync(email);
        if (pending == null)
            return await VerifyLegacyAccountAsync(email, request.Code, now, transaction);

        if (pending.ExpiresAt <= now)
            throw ApiException.BadRequest("Registration expired. Please register again.");

        if (pending.OtpExpiresAt <= now || pending.FailedAttempts >= MaxOtpAttempts)
            throw ApiException.BadRequest(CodeExpiredMessage);

        if (!otpService.VerifyOtp(email, request.Code, pending.OtpHash))
        {
            pending.FailedAttempts++;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            throw ApiException.BadRequest("Incorrect verification code.");
        }

        if (await userManager.FindByEmailAsync(email) != null ||
            await userManager.FindByNameAsync(pending.Username) != null)
            throw ApiException.Conflict("Email or username is already registered.");

        // CreateAsync without a password keeps the hash saved at Register.
        var result = await userManager.CreateAsync(new ApplicationUser
        {
            UserName = pending.Username,
            Email = pending.Email,
            DisplayName = pending.DisplayName,
            PasswordHash = pending.PasswordHash,
            EmailConfirmed = true,
            CreatedAt = now
        });

        if (!result.Succeeded)
            throw new ApiException(StatusCodes.Status400BadRequest,
                new ErrorResponse("Unable to complete registration.") { Errors = result.Errors.Select(e => e.Description) });

        db.PendingRegistrations.Remove(pending);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new MessageResponse(EmailVerifiedMessage);
    }

    // Older accounts were created before PendingRegistrations existed and
    // keep their codes in EmailVerificationOtps.
    private async Task<MessageResponse> VerifyLegacyAccountAsync(
        string email, string code, DateTime now, IDbContextTransaction transaction)
    {
        var legacyUserId = await FindUserIdByEmailAsync(email)
            ?? throw ApiException.BadRequest(InvalidCodeMessage);

        var user = await db.LockUserAsync(legacyUserId)
            ?? throw ApiException.BadRequest(InvalidCodeMessage);

        if (user.EmailConfirmed)
            return new MessageResponse("Email is already verified.");

        var otp = await db.EmailVerificationOtps
            .Where(o => o.UserId == user.Id && o.UsedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null || otp.ExpiresAt <= now || otp.FailedAttempts >= MaxOtpAttempts)
            throw ApiException.BadRequest(CodeExpiredMessage);

        if (!otpService.VerifyOtp(user.Id, code, otp.CodeHash))
        {
            otp.FailedAttempts++;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            throw ApiException.BadRequest("Incorrect verification code.");
        }

        user.EmailConfirmed = true;
        otp.UsedAt = now;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new MessageResponse(EmailVerifiedMessage);
    }

    // ===================== Resend email code =====================

    public async Task<MessageResponse> ResendEmailOtpAsync(ResendEmailOtpRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var now = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync();

        // Locked so two quick taps on "Resend" cannot both send a code.
        var pending = await db.LockPendingRegistrationAsync(email);

        if (pending == null || pending.ExpiresAt <= now)
            throw new ApiException(StatusCodes.Status410Gone,
                new ErrorResponse("Registration expired or unavailable. Please register again.") { RegistrationExpired = true });

        if (pending.LastOtpSentAt?.AddSeconds(60) > now)
            throw ApiException.TooManyRequests("Please wait 60 seconds before requesting another code.");

        var otp = otpService.GenerateOtp();
        pending.OtpHash = otpService.HashOtp(email, otp);
        pending.OtpExpiresAt = now.AddMinutes(10);
        pending.FailedAttempts = 0;
        pending.LastOtpSentAt = now;

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var sent = await TrySendEmailAsync(email, "ILAQA - New Verification Code",
            $"Your new ILAQA verification code is: {otp}\n\n" +
            "This code expires in 10 minutes.\n\nIf you didn't request this code, ignore this email.");

        if (!sent)
            throw new ApiException(StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("Unable to send verification email. Please try again later."));

        return new MessageResponse("If this email requires verification, a code will be sent.");
    }

    // ===================== Login =====================

    // A new login replaces any older session: older JWTs stop working
    // (checked in Program.cs) and older refresh tokens are revoked.
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var identifier = request.EmailOrUsername.Trim();

        var user = identifier.Contains('@')
            ? await userManager.FindByEmailAsync(identifier)
            : await userManager.FindByNameAsync(identifier);

        if (user == null)
        {
            await ThrowIfAwaitingVerificationAsync(identifier, request.Password);
            throw ApiException.Unauthorized("Invalid email or password.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
            throw ApiException.Unauthorized("Invalid email or password.");

        if (!user.EmailConfirmed)
            throw new ApiException(StatusCodes.Status403Forbidden,
                new ErrorResponse(VerifyFirstMessage) { RequiresEmailVerification = true });

        await using var transaction = await db.Database.BeginTransactionAsync();

        var lockedUser = await db.LockUserAsync(user.Id)
            ?? throw ApiException.Unauthorized("User account not found.");

        // The row was already tracked, so read its latest values under the lock.
        await db.Entry(lockedUser).ReloadAsync();

        lockedUser.ActiveSessionId = Guid.NewGuid();
        var update = await userManager.UpdateAsync(lockedUser);

        if (!update.Succeeded)
        {
            if (update.Errors.Any(e => e.Code == "ConcurrencyFailure"))
                throw ApiException.Conflict("Another login happened at the same time. Please try again.");

            throw new ApiException(StatusCodes.Status500InternalServerError,
                new ErrorResponse("Unable to create login session."));
        }

        await db.RefreshTokens
            .Where(t => t.UserId == lockedUser.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));

        var refreshToken = AddRefreshToken(lockedUser);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new LoginResponse("Login successful!", jwtService.GenerateToken(lockedUser), refreshToken,
            "Bearer", AccessTokenSeconds,
            new UserSummary(lockedUser.Id, lockedUser.UserName, lockedUser.DisplayName));
    }

    // Right password for a registration that is not verified yet: tell the
    // app to open the "verify email" screen instead of "wrong password".
    private async Task ThrowIfAwaitingVerificationAsync(string identifier, string password)
    {
        var lowered = identifier.ToLowerInvariant();
        var pending = await db.PendingRegistrations
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Email == lowered || p.Username == lowered);

        if (pending == null || pending.ExpiresAt <= DateTime.UtcNow)
            return;

        var tempUser = new ApplicationUser { UserName = pending.Username, Email = pending.Email };
        if (passwordHasher.VerifyHashedPassword(tempUser, pending.PasswordHash, password)
            == PasswordVerificationResult.Failed)
            return;

        throw new ApiException(StatusCodes.Status403Forbidden,
            new ErrorResponse(VerifyFirstMessage) { RequiresEmailVerification = true, Email = pending.Email });
    }

    // ===================== Refresh tokens =====================

    // Swaps a refresh token for a new access + refresh token pair.
    // Each refresh token works once (rotation).
    public async Task<TokenResponse> RefreshAsync(RefreshTokenRequest request)
    {
        var tokenHash = jwtService.HashRefreshToken(request.RefreshToken);

        var stored = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == tokenHash);
        if (stored == null || !stored.IsActive)
            throw ApiException.Unauthorized("Invalid or expired refresh token.");

        await using var transaction = await db.Database.BeginTransactionAsync();

        var user = await db.LockUserAsync(stored.UserId);
        if (user?.ActiveSessionId == null || stored.SessionId != user.ActiveSessionId.Value)
            throw ApiException.Unauthorized("Session is no longer active.");

        // Revoke in one UPDATE so two parallel refreshes cannot both succeed.
        var revoked = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));

        if (revoked != 1)
            throw ApiException.Unauthorized("Refresh token has already been used.");

        var newRefreshToken = AddRefreshToken(user);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new TokenResponse("Tokens refreshed successfully!", jwtService.GenerateToken(user),
            newRefreshToken, "Bearer", AccessTokenSeconds);
    }

    // ===================== Logout =====================

    public async Task<MessageResponse> LogoutAsync(RefreshTokenRequest request)
    {
        var tokenHash = jwtService.HashRefreshToken(request.RefreshToken);

        await using var transaction = await db.Database.BeginTransactionAsync();

        var stored = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == tokenHash)
            ?? throw ApiException.Unauthorized("Invalid refresh token.");

        // Locked so login, refresh and logout cannot change the session together.
        var user = await db.LockUserAsync(stored.UserId);

        if (user?.ActiveSessionId == null || user.ActiveSessionId.Value != stored.SessionId || !stored.IsActive)
            throw ApiException.Unauthorized("Session is no longer active.");

        user.ActiveSessionId = null;
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.SessionId == stored.SessionId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new MessageResponse("Logged out successfully.");
    }

    // ===================== Forgot password (step 1: send code) =====================

    // Always gives the same answer, so nobody can find out which emails exist.
    public async Task<MessageResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var now = DateTime.UtcNow;
        var answer = new MessageResponse("If an eligible account exists, a password reset code will be sent.");

        var userId = await FindVerifiedUserIdAsync(email);
        if (userId == null)
            return answer;

        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.LockUserAsync(userId);

        var reset = await db.PasswordResetOtps.SingleOrDefaultAsync(r => r.UserId == userId);

        // 60-second cooldown while the previous code is still usable.
        if (reset is { UsedAt: null } && reset.ExpiresAt > now &&
            reset.FailedAttempts < MaxOtpAttempts && reset.LastOtpSentAt?.AddSeconds(60) > now)
        {
            await transaction.CommitAsync();
            return answer;
        }

        if (reset == null)
        {
            reset = new PasswordResetOtp { UserId = userId };
            db.PasswordResetOtps.Add(reset);
        }

        var otp = otpService.GenerateOtp();
        reset.CodeHash = otpService.HashOtp(ResetOtpKey(userId), otp);
        reset.CreatedAt = now;
        reset.ExpiresAt = now.AddMinutes(10);
        reset.FailedAttempts = 0;
        reset.UsedAt = null;
        reset.LastOtpSentAt = now;
        reset.ResetTokenHash = null; // A new code cancels any earlier reset permission.
        reset.ResetTokenExpiresAt = null;

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        // Same answer even if the email fails, to hide whether the account exists.
        await TrySendEmailAsync(email, "ILAQA - Password Reset Code",
            $"Your ILAQA password reset code is: {otp}\n\nThis code expires in 10 minutes.\n\n" +
            "If you did not request a password reset, you can safely ignore this email.");

        return answer;
    }

    // ===================== Forgot password (step 2: verify code) =====================

    // A correct code is exchanged for a one-time reset token (15 minutes).
    public async Task<ResetOtpVerifiedResponse> VerifyResetOtpAsync(VerifyResetOtpRequest request)
    {
        var now = DateTime.UtcNow;
        var userId = await FindVerifiedUserIdAsync(NormalizeEmail(request.Email))
            ?? throw ApiException.BadRequest(InvalidCodeMessage);

        await using var transaction = await db.Database.BeginTransactionAsync();

        var reset = await db.LockPasswordResetAsync(userId);
        if (reset == null || reset.ExpiresAt <= now || reset.UsedAt != null)
            throw ApiException.BadRequest(InvalidCodeMessage);

        if (reset.FailedAttempts >= MaxOtpAttempts)
            throw ApiException.TooManyRequests("Too many incorrect attempts. Request a new code.");

        if (!otpService.VerifyOtp(ResetOtpKey(userId), request.Code, reset.CodeHash))
        {
            reset.FailedAttempts++;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            throw ApiException.BadRequest(reset.FailedAttempts >= MaxOtpAttempts
                ? "Too many incorrect attempts. Request a new code."
                : "Incorrect verification code.");
        }

        // Only the hash of the reset token is stored.
        var resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        reset.ResetTokenHash = Convert.ToHexString(HashResetToken(resetToken));
        reset.ResetTokenExpiresAt = now.AddMinutes(15);
        reset.UsedAt = now; // The code cannot be used twice.

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new ResetOtpVerifiedResponse("Code verified successfully.", true, resetToken, 900);
    }

    // ===================== Forgot password (step 3: new password) =====================

    public async Task<MessageResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var userId = await FindVerifiedUserIdAsync(NormalizeEmail(request.Email))
            ?? throw ApiException.BadRequest(InvalidResetMessage);

        await using var transaction = await db.Database.BeginTransactionAsync();

        var user = await db.LockUserAsync(userId) ?? throw ApiException.BadRequest(InvalidResetMessage);
        var reset = await db.LockPasswordResetAsync(userId);
        var now = DateTime.UtcNow;

        if (reset?.UsedAt == null || reset.ResetTokenExpiresAt == null || reset.ResetTokenExpiresAt <= now ||
            !ResetTokenMatches(reset, request.ResetToken))
            throw ApiException.BadRequest(InvalidResetMessage);

        await PasswordHelper.ValidateAsync(userManager, user, request.NewPassword,
            "New password does not meet security requirements.");
        await PasswordHelper.SetNewPasswordAndSignOutAsync(db, userManager, user, request.NewPassword, now);

        reset.ResetTokenHash = null; // The reset token cannot be reused.
        reset.ResetTokenExpiresAt = null;

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new MessageResponse("Password reset successfully. Please sign in again.");
    }

    // ===================== Cancel password reset =====================

    public async Task<MessageResponse> CancelPasswordResetAsync(CancelPasswordResetRequest request)
    {
        var cancelled = new MessageResponse("Password reset cancelled.");

        var userId = await FindVerifiedUserIdAsync(NormalizeEmail(request.Email));
        if (userId == null)
            return cancelled;

        await using var transaction = await db.Database.BeginTransactionAsync();

        var reset = await db.LockPasswordResetAsync(userId);
        if (reset?.ResetTokenHash == null || reset.ResetTokenExpiresAt == null ||
            reset.ResetTokenExpiresAt <= DateTime.UtcNow)
            return cancelled;

        if (!ResetTokenMatches(reset, request.ResetToken))
            throw ApiException.BadRequest("Invalid reset authorization.");

        reset.ResetTokenHash = null;
        reset.ResetTokenExpiresAt = null;

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return cancelled;
    }

    // ===================== Small helpers =====================

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    // Purpose-specific key, so a registration code can never reset a password.
    private static string ResetOtpKey(string userId) => $"password-reset:{userId}";

    private static byte[] HashResetToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    // Constant-time compare, so timing does not leak the stored hash.
    private static bool ResetTokenMatches(PasswordResetOtp reset, string token) =>
        reset.ResetTokenHash != null &&
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(reset.ResetTokenHash), HashResetToken(token));

    private Task<string?> FindUserIdByEmailAsync(string email)
    {
        var normalized = userManager.NormalizeEmail(email);
        return db.Users.AsNoTracking()
            .Where(u => u.NormalizedEmail == normalized)
            .Select(u => u.Id)
            .SingleOrDefaultAsync();
    }

    // Only permanent, verified accounts can reset their password.
    private Task<string?> FindVerifiedUserIdAsync(string email)
    {
        var normalized = userManager.NormalizeEmail(email);
        return db.Users.AsNoTracking()
            .Where(u => u.NormalizedEmail == normalized && u.EmailConfirmed)
            .Select(u => u.Id)
            .SingleOrDefaultAsync();
    }

    // Creates a refresh token for the user's current session. Only its
    // SHA-256 hash is saved; the raw token is returned to the app.
    private string AddRefreshToken(ApplicationUser user)
    {
        var refreshToken = jwtService.GenerateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            SessionId = user.ActiveSessionId!.Value,
            TokenHash = jwtService.HashRefreshToken(refreshToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        return refreshToken;
    }

    private async Task<bool> TrySendEmailAsync(string to, string subject, string body)
    {
        try
        {
            await emailService.SendEmailAsync(to, subject, body);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not send \"{Subject}\" email.", subject);
            return false;
        }
    }
}
