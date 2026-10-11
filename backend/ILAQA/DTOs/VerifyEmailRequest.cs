
using System.ComponentModel.DataAnnotations;

namespace ILAQA.DTOs
{
    public class VerifyEmailRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^[0-9]{6}$")]
        public string Code { get; set; } = string.Empty;
    }
}
