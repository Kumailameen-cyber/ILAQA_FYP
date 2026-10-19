
using System.ComponentModel.DataAnnotations;

namespace ILAQA.DTOs
{
    public class UpdateProfileRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Bio { get; set; }

        public bool IsProfilePublic { get; set; }
    }
}
