
using System;
using System.ComponentModel.DataAnnotations;

namespace ILAQA.Models
{
    public class PasswordResetOtp
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string CodeHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Original OTP expiry: 60 seconds.
        public DateTime ExpiresAt { get; set; }

        public int FailedAttempts { get; set; } = 0;

        public DateTime? UsedAt { get; set; }

        public DateTime? LastOtpSentAt { get; set; }

        // Hash of the secure token issued after OTP verification.
        // Null means no reset authorization has been issued.
        [MaxLength(64)]
        public string? ResetTokenHash { get; set; }

        // Separate expiry for the password creation step.
        public DateTime? ResetTokenExpiresAt { get; set; }

        public ApplicationUser User { get; set; } = null!;
    }
}
