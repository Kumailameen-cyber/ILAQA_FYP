using Microsoft.AspNetCore.Identity;

namespace ILAQA.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    public string? ProfilePictureUrl { get; set; }

    public Guid? ActiveSessionId { get; set; }

    // New accounts are public (8 Nov decision): other runners see the
    // runner's name on their land. Runners can switch to private in
    // their profile; private owners show as "Anonymous profile".
    public bool IsProfilePublic { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
