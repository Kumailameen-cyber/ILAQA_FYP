using System.ComponentModel.DataAnnotations;

namespace ILAQA.Features.Auth;

// ---------- Requests (validated automatically by [ApiController]) ----------

public sealed record RegisterRequest
{
    [Required, StringLength(30, MinimumLength = 3)]
    public string Username { get; init; } = string.Empty;

    [Required, StringLength(50, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;
}

public sealed record VerifyEmailRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, RegularExpression(@"^[0-9]{6}$")]
    public string Code { get; init; } = string.Empty;
}

public sealed record ResendEmailOtpRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

public sealed record LoginRequest
{
    [Required]
    public string EmailOrUsername { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed record RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed record ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

public sealed record VerifyResetOtpRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Enter a valid 6-digit code.")]
    public string Code { get; init; } = string.Empty;
}

public sealed record ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string ResetToken { get; init; } = string.Empty;

    [Required]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record CancelPasswordResetRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string ResetToken { get; init; } = string.Empty;
}

// ---------- Responses ----------

public sealed record RegisterResponse(string Message, bool RequiresEmailVerification, string Email);

public sealed record UserSummary(string Id, string? Username, string DisplayName);

public sealed record LoginResponse(
    string Message, string AccessToken, string RefreshToken, string TokenType, int ExpiresIn, UserSummary User);

public sealed record TokenResponse(
    string Message, string AccessToken, string RefreshToken, string TokenType, int ExpiresIn);

public sealed record ResetOtpVerifiedResponse(string Message, bool Verified, string ResetToken, int ExpiresIn);
