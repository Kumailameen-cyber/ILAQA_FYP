
using System.ComponentModel.DataAnnotations;

namespace ILAQA.DTOs
{
    public class CancelPasswordResetRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string ResetToken { get; set; } = string.Empty;
    }
}
