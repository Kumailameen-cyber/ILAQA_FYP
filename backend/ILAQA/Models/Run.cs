
using System;

namespace ILAQA.Models
{
    // Values stored in Runs.ValidationStatus (ERD: varchar(20)).
    // The ERD does not list the values; these are the approved FYP-I ones.
    public static class RunValidationStatuses
    {
        // Run not finished yet.
        public const string Pending = "Pending";

        // Finished and at least one territory claim was approved.
        public const string Validated = "Validated";

        // Finished and processed, but no land was awarded. The workout
        // itself is kept (FR-17).
        public const string NoClaim = "NoClaim";

        // Territory processing failed; it is retried (UC-05/UC-07).
        public const string Failed = "Failed";
    }

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

        // Official cleaned walking/running distance, set at Finish.
        // GPS teleports and bad fixes are excluded; vehicle-speed runs
        // (VEHICLE_SPEED) get 0.
        public double TotalDistanceMeters { get; set; } = 0;

        // Running, Paused, Completed, or Invalidated.
        public string Status { get; set; } = "Running";
        // Outcome of territory processing, see RunValidationStatuses.
        public string ValidationStatus { get; set; } =
            RunValidationStatuses.Pending;

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
