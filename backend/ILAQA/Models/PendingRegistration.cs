
using System;
using System.ComponentModel.DataAnnotations;

namespace ILAQA.Models
{
    public class PendingRegistration
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string OtpHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } =
            DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public DateTime OtpExpiresAt { get; set; }

        public int FailedAttempts { get; set; } = 0;

        public DateTime? LastOtpSentAt { get; set; }
    }
}
