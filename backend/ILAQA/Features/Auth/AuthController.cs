using ILAQA.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ILAQA.Features.Auth;

// /api/auth — sign up, sign in and password reset.
// Thin controller: each endpoint only calls AuthService. Errors are
// thrown by the service and written by GlobalExceptionHandler.
[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    // Sign up: save a pending registration and email a code.
    [HttpPost("register")]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request) =>
        StatusCode(StatusCodes.Status201Created, await auth.RegisterAsync(request));

    // Sign up: confirm the emailed code and create the account.
    [HttpPost("verify-email")]
    public async Task<MessageResponse> VerifyEmail(VerifyEmailRequest request) =>
        await auth.VerifyEmailAsync(request);

    // Sign up: send a new code (once every 60 seconds).
    [HttpPost("resend-email-otp")]
    public async Task<MessageResponse> ResendEmailOtp(ResendEmailOtpRequest request) =>
        await auth.ResendEmailOtpAsync(request);

    // Sign in with email or username; returns access + refresh tokens.
    [HttpPost("login")]
    public async Task<LoginResponse> Login(LoginRequest request) =>
        await auth.LoginAsync(request);

    // Exchange a refresh token for new tokens.
    [HttpPost("refresh")]
    public async Task<TokenResponse> Refresh(RefreshTokenRequest request) =>
        await auth.RefreshAsync(request);

    // Sign out this session.
    [HttpPost("logout")]
    public async Task<MessageResponse> Logout(RefreshTokenRequest request) =>
        await auth.LogoutAsync(request);

    // Forgot password, step 1: email a reset code.
    [HttpPost("forgot-password")]
    public async Task<MessageResponse> ForgotPassword(ForgotPasswordRequest request) =>
        await auth.ForgotPasswordAsync(request);

    // Forgot password, step 2: check the code, get a reset token.
    [HttpPost("verify-reset-otp")]
    public async Task<ResetOtpVerifiedResponse> VerifyResetOtp(VerifyResetOtpRequest request) =>
        await auth.VerifyResetOtpAsync(request);

    // Forgot password, step 3: set the new password.
    [HttpPost("reset-password")]
    public async Task<MessageResponse> ResetPassword(ResetPasswordRequest request) =>
        await auth.ResetPasswordAsync(request);

    // Forgot password: the runner left the new-password screen.
    [HttpPost("cancel-password-reset")]
    public async Task<MessageResponse> CancelPasswordReset(CancelPasswordResetRequest request) =>
        await auth.CancelPasswordResetAsync(request);
}
