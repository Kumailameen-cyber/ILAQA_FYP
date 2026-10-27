
using System;

namespace ILAQA.Models
{
    public class RunGpsPoint
    {
        // Unique database ID for each GPS point.
        public long Id { get; set; }

        // Which run does this GPS point belong to?
        public Guid RunId { get; set; }

        // Order of GPS points: 1, 2, 3, 4...
        public int SequenceNo { get; set; }

        // Running section number.
        // Increases after each Resume.
        public int LegNo { get; set; } = 1;

        // GPS coordinates.
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // GPS accuracy in meters.
        public double? AccuracyMeters { get; set; }

        // When the phone recorded this point (UTC).
        public DateTime RecordedAt { get; set; }

        // Navigation property.
        public Run Run { get; set; } = null!;
    }
}
