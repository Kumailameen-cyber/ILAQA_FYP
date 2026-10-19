
using System.ComponentModel.DataAnnotations;

namespace ILAQA.DTOs
{
    public class DeleteAccountRequest
    {
        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
