using ILAQA.Models;
using NetTopologySuite.Geometries;

namespace ILAQA.Services
{
    public class TerritoryDetectionService
    {
        private readonly GpsDistanceService _gpsDistanceService;

        public TerritoryDetectionService(
            GpsDistanceService gpsDistanceService)
        {
            _gpsDistanceService = gpsDistanceService;
        }
        private const double MaxAccuracyMeters = 30;

        // Approximate Karachi geographic boundaries
        private const double MinLatitude = 24.70;
        private const double MaxLatitude = 25.50;
        private const double MinLongitude = 66.50;
        private const double MaxLongitude = 67.60;

        public bool IsGpsPointAccurate(RunGpsPoint point)
        {
            return point.AccuracyMeters.HasValue
                && double.IsFinite(point.AccuracyMeters.Value)
                && point.AccuracyMeters.Value >= 0
                && point.AccuracyMeters.Value <= MaxAccuracyMeters

                // Karachi latitude
                && double.IsFinite(point.Latitude)
                && point.Latitude >= MinLatitude
                && point.Latitude <= MaxLatitude

                // Karachi longitude
                && double.IsFinite(point.Longitude)
                && point.Longitude >= MinLongitude
                && point.Longitude <= MaxLongitude;
        }
        private const double MaxSpeedMetersPerSecond = 8;

        public bool IsMovementValid(
            RunGpsPoint previous,
            RunGpsPoint current)
        {
            // Do not connect points across pause/resume.
            if (previous.LegNo != current.LegNo)
                return false;

            // Both GPS points must be accurate and inside Karachi.
            if (!IsGpsPointAccurate(previous) ||
                !IsGpsPointAccurate(current))
                return false;

            // Calculate elapsed time.
            double seconds =
                (current.RecordedAt - previous.RecordedAt).TotalSeconds;

            if (seconds <= 0)
                return false;

            // Calculate distance using your existing service.
            double meters = _gpsDistanceService.CalculateDistance(
                previous.Latitude,
                previous.Longitude,
                current.Latitude,
                current.Longitude);

            // Calculate speed in meters per second.
            double speed = meters / seconds;

            return double.IsFinite(speed)
                && speed <= MaxSpeedMetersPerSecond;
        }
        private const double MaxClosureDistanceMeters = 15;

        public bool IsLoopClosed(List<RunGpsPoint> points)
        {
            if (points == null || points.Count < 4)
                return false;

            var orderedPoints = points
                .OrderBy(p => p.SequenceNo)
                .ToList();

            var first = orderedPoints.First();
            var last = orderedPoints.Last();

            // A loop must stay within one running leg.
            if (orderedPoints.Any(p => p.LegNo != first.LegNo))
                return false;

            // Check GPS accuracy and Karachi boundaries.
            if (orderedPoints.Any(p => !IsGpsPointAccurate(p)))
                return false;

            // Reject impossible movement between consecutive points.
            for (int i = 1; i < orderedPoints.Count; i++)
            {
                if (!IsMovementValid(
                    orderedPoints[i - 1],
                    orderedPoints[i]))
                {
                    return false;
                }
            }

            // Measure distance between start and end.
            double closureDistance =
                _gpsDistanceService.CalculateDistance(
                    first.Latitude,
                    first.Longitude,
                    last.Latitude,
                    last.Longitude);

            return closureDistance <= MaxClosureDistanceMeters;
        }
        public Polygon? CreateTerritoryPolygon(List<RunGpsPoint> points)
        {
            // First validate the GPS loop.
            if (!IsLoopClosed(points))
                return null;

            var orderedPoints = points
                .OrderBy(p => p.SequenceNo)
                .ToList();

            // NetTopologySuite uses X = Longitude, Y = Latitude.
            var coordinates = orderedPoints
                .Select(p => new Coordinate(p.Longitude, p.Latitude))
                .ToList();

            // Close the polygon only if it is not already closed.
            if (!coordinates[0].Equals2D(coordinates[^1]))
            {
                coordinates.Add(coordinates[0].Copy());
            }
            var factory = new GeometryFactory(
                new PrecisionModel(),
                4326);

            var polygon = factory.CreatePolygon(
                coordinates.ToArray());

            // Reject invalid polygons and zero-area routes.
            if (!polygon.IsValid || polygon.IsEmpty)
                return null;

            double areaSquareMeters = CalculateTerritoryArea(polygon);

            if (!double.IsFinite(areaSquareMeters) ||
                areaSquareMeters < MinTerritoryAreaSquareMeters)
            {
                return null;
            }

            return polygon;
        }
        private const double MinTerritoryAreaSquareMeters = 100;

        public double CalculateTerritoryArea(Polygon polygon)
        {
            // Approximate conversion from GPS degrees to meters.
            double latitude = polygon.Centroid.Y;

            double metersPerDegreeLatitude = 111320;

            double metersPerDegreeLongitude =
                111320 * Math.Cos(latitude * Math.PI / 180);

            double areaSquareMeters =
                polygon.Area *
                metersPerDegreeLatitude *
                metersPerDegreeLongitude;

            return Math.Abs(areaSquareMeters);
        }
        // Find every valid territory in a completed run.
        public List<Polygon> FindTerritories(List<RunGpsPoint> points)
        {
            var territories = new List<Polygon>();

            if (points == null || points.Count < 4)
                return territories;

            // Put GPS points in the correct order.
            var ordered = points.OrderBy(p => p.SequenceNo).ToList();

            // One continuous section of the run.
            var section = new List<RunGpsPoint>();

            foreach (var point in ordered)
            {
                // A bad GPS point breaks the route section.
                if (!IsGpsPointAccurate(point))
                {
                    FindLoopsInSection(section, territories);
                    section.Clear();
                    continue;
                }

                if (section.Count > 0)
                {
                    var previous = section[section.Count - 1];

                    double gapSeconds =
                        (point.RecordedAt - previous.RecordedAt).TotalSeconds;

                    // Break the route after a pause, GPS gap,
                    // or invalid movement.
                    if (point.LegNo != previous.LegNo ||
                        gapSeconds > 120 ||
                        !IsMovementValid(previous, point))
                    {
                        // Save valid loops found before the gap.
                        FindLoopsInSection(section, territories);

                        // Start a fresh GPS section.
                        section.Clear();
                    }
                }

                section.Add(point);
            }

            // Check the final section too.
            FindLoopsInSection(section, territories);

            return territories;
        }


        // Find valid loops inside one continuous GPS section.
        private void FindLoopsInSection(
            List<RunGpsPoint> section,
            List<Polygon> territories)
        {
            int start = 0;

            while (start + 3 < section.Count)
            {
                bool found = false;

                // Try progressively longer paths.
                for (int end = start + 3; end < section.Count; end++)
                {
                    var loop = section.GetRange(
                        start,
                        end - start + 1);

                    // Reuse our existing polygon validation.
                    var polygon = CreateTerritoryPolygon(loop);

                    if (polygon == null)
                        continue;

                    // Save this valid territory.
                    territories.Add(polygon);

                    // Continue from the closing point.
                    start = end;
                    found = true;
                    break;
                }

                // Try the next starting point if no loop was found.
                if (!found)
                    start++;
            }
        }
    }
}