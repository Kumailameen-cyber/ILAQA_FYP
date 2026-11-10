using System.ComponentModel.DataAnnotations;

namespace ILAQA.Features.Runner;

// ---------- Requests ----------

public sealed record UpdateProfileRequest
{
    [Required, StringLength(50, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;

    [StringLength(250)]
    public string? Bio { get; init; }

    public bool IsProfilePublic { get; init; }
}

public sealed record ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record DeleteAccountRequest
{
    [Required]
    public string Password { get; init; } = string.Empty;
}

// ---------- Responses (never the ApplicationUser entity itself) ----------

public sealed record ProfileResponse(
    string Id, string? Username, string? Email, string DisplayName, string? Bio,
    string? ProfilePictureUrl, bool IsProfilePublic, DateTime CreatedAt);

public sealed record ProfileUpdatedResponse(string Message, string DisplayName, string? Bio, bool IsProfilePublic);

public sealed record ProfilePictureResponse(string Message, string? ProfilePictureUrl);
