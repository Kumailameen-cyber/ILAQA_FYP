
using System;

namespace ILAQA.Models
{
    public class Run
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // The runner who owns this run.
        public string UserId { get; set; } = string.Empty;

        // UTC timestamps.
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }

        // Start time of the current active running period.
        // Null when paused or finished.
        public DateTime? ActiveStartedAt { get; set; }

        // Completed active seconds, excluding pauses.
        public int ActiveSeconds { get; set; } = 0;

        // Validated running distance.
        public double TotalDistanceMeters { get; set; } = 0;

        // Running, Paused, Completed, or Invalidated.
        public string Status { get; set; } = "Running";
        // Pending, Valid, or Rejected.
        // The server decides whether the run qualifies for XP.
        public string ValidationStatus { get; set; } = "Pending";

        // Distance XP awarded after validation.
        public long XpAwarded { get; set; } = 0;

        // For safe offline upload retries.
        public DateTime? SyncedAt { get; set; }

        // Navigation properties.
        public ApplicationUser User { get; set; } = null!;

        public ICollection<RunGpsPoint> GpsPoints { get; set; }
            = new List<RunGpsPoint>();
    }
}
