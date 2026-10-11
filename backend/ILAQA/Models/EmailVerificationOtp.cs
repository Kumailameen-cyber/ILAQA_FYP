
using System;
using System.ComponentModel.DataAnnotations;

namespace ILAQA.Models
{
    public class EmailVerificationOtp
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        // Stores the HMAC hash, never the actual OTP.
        [Required]
        public string CodeHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public int FailedAttempts { get; set; } = 0;

        public DateTime? UsedAt { get; set; }

        // The user who requested this verification code.
        public ApplicationUser? User { get; set; }
    }
}
