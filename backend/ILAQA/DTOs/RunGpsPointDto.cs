
using System;

namespace ILAQA.DTOs
{
    public class RunGpsPointDto
    {
        public int SequenceNo { get; set; }

        public int LegNo { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double? AccuracyMeters { get; set; }

        public DateTime RecordedAt { get; set; }
    }
}
