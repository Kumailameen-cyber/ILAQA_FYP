
using System.ComponentModel.DataAnnotations;

namespace ILAQA.DTOs
{
    public class VerifyResetOtpRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression(
            @"^[0-9]{6}$",
            ErrorMessage = "Enter a valid 6-digit code.")]
        public string Code { get; set; } = string.Empty;
    }
}
