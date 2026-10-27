
using System;
using System.ComponentModel.DataAnnotations;

namespace ILAQA.DTOs
{
    public class StartRunRequest
    {
        // Generated once by the mobile app.
        // Reused if an offline request is retried.
        [Required]
        public Guid RunId { get; set; }

        // When the runner pressed Start (UTC).
        [Required]
        public DateTime StartedAt { get; set; }
    }
}
