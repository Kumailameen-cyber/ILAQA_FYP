
using System.ComponentModel.DataAnnotations;

namespace ILAQA.DTOs
{
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
