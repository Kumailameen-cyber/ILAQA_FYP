
using System.ComponentModel.DataAnnotations;

namespace ILAQA.DTOs
{
    public class ResendEmailOtpRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
