
using System;
using System.ComponentModel.DataAnnotations;

namespace ILAQA.Models
{
    public class PasswordResetOtp
    {
        public int Id { get; set; }

        // The permanent Identity user requesting reset.
        [Required]
        public string UserId { get; set; } = string.Empty;

        // Store only the hashed OTP.
        [Required]
        public string CodeHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } =
            DateTime.UtcNow;

        // OTP remains valid for 10 minutes.
        public DateTime ExpiresAt { get; set; }

        // Maximum five incorrect attempts.
        public int FailedAttempts { get; set; } = 0;

        // Used OTPs cannot be reused.
        public DateTime? UsedAt { get; set; }

        // Enforce 60-second resend cooldown.
        public DateTime? LastOtpSentAt { get; set; }

        // Relationship with ASP.NET Core Identity user.
        public ApplicationUser User { get; set; } = null!;
    }
}
