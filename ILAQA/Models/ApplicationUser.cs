
using Microsoft.AspNetCore.Identity;

namespace ILAQA.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string DisplayName { get; set; } = string.Empty;

        public string? Bio { get; set; }

        public string? ProfilePictureUrl { get; set; }

        public Guid? ActiveSessionId { get; set; }



        public bool IsProfilePublic { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
