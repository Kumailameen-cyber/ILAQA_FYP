
using System.ComponentModel.DataAnnotations;

namespace ILAQA.Models
{
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }

        // Store the SHA-256 hash, never the raw token
        [Required]
        [MaxLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        // The runner who owns this token
        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        [Required]
        public Guid SessionId { get; set; }

        // When the token was created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // When the token expires
        public DateTime ExpiresAt { get; set; }

        // Null means the token has not been revoked
        public DateTime? RevokedAt { get; set; }

        // True only if the token is still usable
        public bool IsActive =>
            RevokedAt == null &&
            DateTime.UtcNow < ExpiresAt;
    }
}
