
using System.ComponentModel.DataAnnotations;

namespace ILAQA.DTOs
{
    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
