using ILAQA.Models;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Overlay;
using NetTopologySuite.Operation.OverlayNG;
using NetTopologySuite.Operation.Polygonize;

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

            if (seconds < MinimumPointIntervalSeconds)
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
        // Allow modest GPS drift when a runner returns near the starting point.
        // This is a candidate threshold, not proof of an enclosed territory.
        private const double MaxClosureDistanceMeters = 20;
        private const double MinimumPointIntervalSeconds = 0.5;
        private const double MaxTrackingGapSeconds = 120;

        // Longest straight jump between two GPS points that is still joined.
        private const double MaxBridgeDistanceMeters = 150;

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

            // The candidate must be continuous and physically plausible.
            for (int i = 1; i < orderedPoints.Count; i++)
            {
                double seconds = (orderedPoints[i].RecordedAt -
                    orderedPoints[i - 1].RecordedAt).TotalSeconds;
                if (seconds > MaxTrackingGapSeconds ||
                    !IsMovementValid(orderedPoints[i - 1], orderedPoints[i]))
                    return false;
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

            // A self-intersecting route may contain several ambiguous regions.
            // Do not award an arbitrary region via Buffer(0): require a single,
            // valid polygon that faithfully follows the recorded route.
            if (!polygon.IsValid || polygon.IsEmpty)
                return null;

            double areaSquareMeters = CalculateTerritoryArea(polygon);

            if (!double.IsFinite(areaSquareMeters) ||
                areaSquareMeters < MinTerritoryAreaSquareMeters)
            {
                return null;
            }
            if (!IsWideEnough(polygon))
                return null;

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
        private const double MinHalfWidthMeters = 8; // territory must be at least 16 m wide

        private static bool IsWideEnough(Polygon polygon)
        {
            // Convert degrees to meters so we can shrink by a real distance.
            double mLat = 111320;
            double mLon = 111320 * Math.Cos(polygon.Centroid.Y * Math.PI / 180);

            // Include holes: a thin ring around other land is not wide.
            var factory = new GeometryFactory();
            LinearRing ToMeters(LineString ring) => factory.CreateLinearRing(
                ring.Coordinates
                    .Select(c => new Coordinate(c.X * mLon, c.Y * mLat))
                    .ToArray());

            var meters = factory.CreatePolygon(
                ToMeters(polygon.ExteriorRing),
                polygon.InteriorRings.Select(hole => ToMeters(hole)).ToArray());

            // If shrinking by 8 m leaves nothing, the shape is a thin strip.
            return !meters.Buffer(-MinHalfWidthMeters).IsEmpty;
        }
        // Sustained-speed rule for vehicles (walking, jogging and running only).
        // A 10 s rolling average above 5.5 m/s, kept for at least 3 s, counts
        // as vehicle time. More than 15 s of vehicle time flags the whole run.
        // Provisional values: calibrate with real recorded rides.
        private const double VehicleWindowSeconds = 10;
        private const double VehicleSpeedMetersPerSecond = 5.5;
        private const double MinVehicleStretchSeconds = 3;
        private const double MaxVehicleSecondsPerRun = 15;

        private const double MetersPerDegree = 111320;

        // Analyse a whole run and explain every territory decision.
        public TerritoryAnalysis AnalyzeRun(List<RunGpsPoint> points)
        {
            var analysis = new TerritoryAnalysis();
            if (points == null || points.Count < 4)
                return analysis;

            var ordered = UseSnappedPositions(
                points.OrderBy(p => p.SequenceNo).ToList());
            var sections = BuildSections(ordered);

            analysis.VehicleSpeedSeconds =
                sections.Sum(s => MeasureVehicleSpeedSeconds(s));
            analysis.VehicleSuspected =
                analysis.VehicleSpeedSeconds > MaxVehicleSecondsPerRun;

            // Vehicle runs earn no genuine distance.
            analysis.GenuineDistanceMeters = analysis.VehicleSuspected
                ? 0
                : sections.Sum(s => SectionDistanceMeters(s));

            foreach (var section in sections)
            {
                foreach (var candidate in FindEnclosedAreas(section))
                {
                    if (candidate.RejectionReason != null)
                    {
                        analysis.Rejected.Add(candidate);
                    }
                    else if (analysis.VehicleSuspected)
                    {
                        analysis.Rejected.Add(new TerritoryCandidate
                        {
                            Boundary = candidate.Boundary,
                            AreaSquareMeters = candidate.AreaSquareMeters,
                            StartSequenceNo = candidate.StartSequenceNo,
                            EndSequenceNo = candidate.EndSequenceNo,
                            RejectionReason =
                                TerritoryRejectionReasons.VehicleSpeed
                        });
                    }
                    else
                    {
                        analysis.Accepted.Add(candidate);
                    }
                }
            }

            return analysis;
        }

        // Find valid territories in a completed run.
        public List<Polygon> FindTerritories(List<RunGpsPoint> points)
        {
            return AnalyzeRun(points).Accepted
                .Select(candidate => candidate.Boundary)
                .ToList();
        }

        // Split a run into continuous, cleaned GPS sections.
        private List<List<RunGpsPoint>> BuildSections(
            List<RunGpsPoint> ordered)
        {
            var sections = new List<List<RunGpsPoint>>();
            var section = new List<RunGpsPoint>();

            foreach (var point in ordered)
            {
                // Skip isolated inaccurate readings without erasing good points.
                if (!IsGpsPointAccurate(point))
                    continue;

                if (section.Count > 0)
                {
                    var previous = section[^1];
                    double gapSeconds =
                        (point.RecordedAt - previous.RecordedAt).TotalSeconds;

                    // A new leg must never be connected to the previous leg.
                    if (point.LegNo != previous.LegNo)
                    {
                        sections.Add(section);
                        section = new List<RunGpsPoint>();
                    }
                    else if (gapSeconds < MinimumPointIntervalSeconds)
                    {
                        // Duplicate, out-of-order or near-simultaneous reading.
                        continue;
                    }
                    else if (!IsMovementValid(previous, point))
                    {
                        // Impossible jump: drop this single point.
                        System.Diagnostics.Debug.WriteLine(
                            $"ILAQA GPS REJECTED: Point {point.SequenceNo}, " +
                            $"Previous accepted point {previous.SequenceNo}");

                        continue;
                    }
                    else if (_gpsDistanceService.CalculateDistance(
                                 previous.Latitude, previous.Longitude,
                                 point.Latitude, point.Longitude) >
                             MaxBridgeDistanceMeters)
                    {
                        // Missing GPS over a long stretch (FR-11): do not
                        // invent a straight route. Stops without movement
                        // stay joined however long they last.
                        sections.Add(section);
                        section = new List<RunGpsPoint>();
                    }
                }

                section.Add(point);
            }

            sections.Add(section);
            return sections;
        }

        // Cleaned distance travelled within one section.
        private double SectionDistanceMeters(List<RunGpsPoint> section)
        {
            double meters = 0;

            for (int i = 1; i < section.Count; i++)
            {
                meters += _gpsDistanceService.CalculateDistance(
                    section[i - 1].Latitude, section[i - 1].Longitude,
                    section[i].Latitude, section[i].Longitude);
            }

            return meters;
        }

        // Seconds of one section spent at sustained vehicle speed.
        private double MeasureVehicleSpeedSeconds(List<RunGpsPoint> section)
        {
            double vehicleSeconds = 0;
            double stretchSeconds = 0;

            for (int i = 1; i < section.Count; i++)
            {
                // Average speed over the segments ending in the last 10 s.
                double windowMeters = 0;
                double windowSeconds = 0;

                for (int j = i; j >= 1; j--)
                {
                    double age = (section[i].RecordedAt -
                        section[j].RecordedAt).TotalSeconds;

                    if (age >= VehicleWindowSeconds)
                        break;

                    windowSeconds += (section[j].RecordedAt -
                        section[j - 1].RecordedAt).TotalSeconds;
                    windowMeters += _gpsDistanceService.CalculateDistance(
                        section[j - 1].Latitude, section[j - 1].Longitude,
                        section[j].Latitude, section[j].Longitude);
                }

                double segmentSeconds = (section[i].RecordedAt -
                    section[i - 1].RecordedAt).TotalSeconds;

                if (windowSeconds > 0 &&
                    windowMeters / windowSeconds > VehicleSpeedMetersPerSecond)
                {
                    stretchSeconds += segmentSeconds;
                }
                else
                {
                    if (stretchSeconds >= MinVehicleStretchSeconds)
                        vehicleSeconds += stretchSeconds;

                    stretchSeconds = 0;
                }
            }

            if (stretchSeconds >= MinVehicleStretchSeconds)
                vehicleSeconds += stretchSeconds;

            return vehicleSeconds;
        }

        // Find every genuinely enclosed area in one continuous section,
        // even when the route crosses itself. Work in local metres.
        private List<TerritoryCandidate> FindEnclosedAreas(
            List<RunGpsPoint> section)
        {
            var candidates = new List<TerritoryCandidate>();
            if (section.Count < 4)
                return candidates;

            double originLatitude = section[0].Latitude;
            double originLongitude = section[0].Longitude;
            double metersPerDegreeLongitude = MetersPerDegree *
                Math.Cos(originLatitude * Math.PI / 180);

            var coordinates = new List<Coordinate>();
            var sequenceNumbers = new List<int>();

            foreach (var point in section)
            {
                var coordinate = new Coordinate(
                    (point.Longitude - originLongitude) * metersPerDegreeLongitude,
                    (point.Latitude - originLatitude) * MetersPerDegree);

                // Stationary repeats add nothing to the shape.
                if (coordinates.Count > 0 &&
                    coordinates[^1].Equals2D(coordinate))
                    continue;

                coordinates.Add(coordinate);
                sequenceNumbers.Add(point.SequenceNo);
            }

            if (coordinates.Count < 3)
                return candidates;

            // Close the route only when it ends near its start (FR-13).
            double closureDistance = _gpsDistanceService.CalculateDistance(
                section[0].Latitude, section[0].Longitude,
                section[^1].Latitude, section[^1].Longitude);

            if (!coordinates[0].Equals2D(coordinates[^1]) &&
                closureDistance <= MaxClosureDistanceMeters)
            {
                coordinates.Add(coordinates[0].Copy());
            }

            var metricFactory = new GeometryFactory();
            var route = metricFactory.CreateLineString(coordinates.ToArray());

            // Split the route at every crossing, then collect each
            // enclosed face separately. Out-and-back tracks enclose nothing.
            var polygonizer = new Polygonizer();
            polygonizer.Add(OverlayNGRobust.Union(route));

            var geographicFactory = new GeometryFactory(
                new PrecisionModel(), 4326);

            foreach (var face in polygonizer.GetPolygons().OfType<Polygon>())
            {
                if (face.IsEmpty || !face.IsValid)
                    continue;

                double area = face.Area;
                string? reason = null;

                if (!double.IsFinite(area) ||
                    area < MinTerritoryAreaSquareMeters)
                {
                    reason = TerritoryRejectionReasons.TooSmall;
                }
                else if (face.Buffer(-MinHalfWidthMeters).IsEmpty)
                {
                    reason = TerritoryRejectionReasons.TooNarrow;
                }

                // GPS sequence range of the points forming this face.
                var faceSequences = new List<int>();
                for (int i = 0; i < sequenceNumbers.Count; i++)
                {
                    var vertex = metricFactory.CreatePoint(coordinates[i]);
                    if (face.ExteriorRing.IsWithinDistance(vertex, 0.01))
                        faceSequences.Add(sequenceNumbers[i]);
                }

                candidates.Add(new TerritoryCandidate
                {
                    Boundary = ToGeographic(face, geographicFactory,
                        originLatitude, originLongitude,
                        metersPerDegreeLongitude),
                    AreaSquareMeters = area,
                    StartSequenceNo = faceSequences.Count > 0
                        ? faceSequences.Min()
                        : section[0].SequenceNo,
                    EndSequenceNo = faceSequences.Count > 0
                        ? faceSequences.Max()
                        : section[^1].SequenceNo,
                    RejectionReason = reason
                });
            }

            return candidates;
        }

        // Convert a face from local metres back to longitude/latitude.
        private static Polygon ToGeographic(
            Polygon face,
            GeometryFactory factory,
            double originLatitude,
            double originLongitude,
            double metersPerDegreeLongitude)
        {
            LinearRing ToRing(LineString ring) => factory.CreateLinearRing(
                ring.Coordinates
                    .Select(c => new Coordinate(
                        originLongitude + c.X / metersPerDegreeLongitude,
                        originLatitude + c.Y / MetersPerDegree))
                    .ToArray());

            return factory.CreatePolygon(
                ToRing(face.ExteriorRing),
                face.InteriorRings.Select(hole => ToRing(hole)).ToArray());
        }

        // Smaller "too small" faces are GPS knots, not real claim attempts.
        public const double MinRecordedTooSmallAreaSquareMeters = 25;

        // Decide which candidates become new TerritoryClaims rows.
        // Ranges already submitted for this run are never planned again.
        public List<PlannedClaim> PlanNewClaims(
            TerritoryAnalysis analysis,
            IReadOnlyCollection<(int StartSequenceNo, int EndSequenceNo, double AreaSquareMeters)>
                alreadySubmitted)
        {
            // A claim is identified by its GPS range AND its area: two
            // different areas can share a range (e.g. both sides of the
            // closing line) and both must be recorded.
            var submitted = alreadySubmitted
                .Select(s => ClaimKey(
                    s.StartSequenceNo, s.EndSequenceNo, s.AreaSquareMeters))
                .ToHashSet();

            var approved = analysis.Accepted.Select(candidate =>
                new PlannedClaim
                {
                    Candidate = candidate,
                    Status = TerritoryClaimStatuses.Approved
                });

            var rejected = analysis.Rejected
                .Where(candidate =>
                    candidate.RejectionReason !=
                        TerritoryRejectionReasons.TooSmall ||
                    candidate.AreaSquareMeters >=
                        MinRecordedTooSmallAreaSquareMeters)
                .Select(candidate => new PlannedClaim
                {
                    Candidate = candidate,
                    Status = TerritoryClaimStatuses.Rejected,
                    DecisionReason = candidate.RejectionReason
                });

            return approved
                .Concat(rejected)
                .Where(claim => !submitted.Contains(ClaimKey(
                    claim.Candidate.StartSequenceNo,
                    claim.Candidate.EndSequenceNo,
                    claim.Candidate.AreaSquareMeters)))
                .OrderBy(claim => claim.Candidate.StartSequenceNo)
                .ThenBy(claim => claim.Candidate.EndSequenceNo)
                .ToList();
        }

        // A territory stays protected for 3 days after owner activity.
        public const int ShieldDays = 3;

        // Ignore overlaps smaller than this (shared edges, GPS noise).
        private const double MinTakeoverAreaSquareMeters = 1;

        // Apply shield and overlap rules to one approved candidate
        // (FR-18 to FR-20). "overlapping" holds every existing territory
        // that touches the proposed boundary.
        // - Another runner's shielded land is protected: only the free part
        //   is awarded.
        // - From vulnerable land, only the part the loop covers is taken.
        // - The claimant's own land is not counted again.
        public ClaimResolution ResolveClaim(
            Polygon proposed,
            string claimantUserId,
            IReadOnlyList<Territory> overlapping,
            DateTime now)
        {
            var result = new ClaimResolution();

            Geometry? protectedLand = null;
            Geometry? ownLand = null;
            var vulnerable = new List<Territory>();

            foreach (var territory in overlapping)
            {
                if (!territory.Boundary.Intersects(proposed))
                    continue;

                if (territory.OwnerUserId == claimantUserId)
                    ownLand = ownLand == null
                        ? territory.Boundary
                        : Overlay(ownLand, territory.Boundary, SpatialFunction.Union);
                else if (IsShielded(territory, now))
                    protectedLand = protectedLand == null
                        ? territory.Boundary
                        : Overlay(protectedLand, territory.Boundary, SpatialFunction.Union);
                else
                    vulnerable.Add(territory);
            }

            Geometry withoutProtected = protectedLand == null
                ? proposed
                : Overlay(proposed, protectedLand, SpatialFunction.Difference);

            Geometry free = ownLand == null
                ? withoutProtected
                : Overlay(withoutProtected, ownLand, SpatialFunction.Difference);

            var pieces = ValidPieces(free);

            if (pieces.Count == 0)
            {
                result.RejectionReason = ValidPieces(withoutProtected).Count == 0
                    ? TerritoryRejectionReasons.Shielded
                    : TerritoryRejectionReasons.AlreadyOwned;
                return result;
            }

            result.AwardedPieces.AddRange(pieces);
            result.AwardedAreaSquareMeters =
                pieces.Sum(piece => CalculateTerritoryArea(piece));

            Geometry awarded = pieces.Count == 1
                ? pieces[0]
                : proposed.Factory.CreateMultiPolygon(pieces.ToArray());

            foreach (var territory in vulnerable)
            {
                var taken = Overlay(
                    territory.Boundary, awarded, SpatialFunction.Intersection);

                if (taken.IsEmpty ||
                    AreaSquareMeters(taken) < MinTakeoverAreaSquareMeters)
                    continue;

                result.Takeovers.Add(new TerritoryTakeover
                {
                    Territory = territory,
                    TakenPart = taken,
                    RemainingPieces = ValidPieces(
                        Overlay(territory.Boundary, awarded, SpatialFunction.Difference))
                });
            }

            return result;
        }

        // Active and inside the 3-day shield. Older rows without a shield
        // date are protected for 3 days after their last update.
        public static bool IsShielded(Territory territory, DateTime now)
        {
            if (territory.Status != "Active")
                return false;

            var shieldExpiresAt = territory.ShieldExpiresAt ??
                territory.UpdatedAt.AddDays(ShieldDays);

            return shieldExpiresAt > now;
        }

        // ----- Shield refresh, map colour and owner privacy -----

        // Genuine movement needed in one run to renew the owner's shields.
        public const double ShieldRefreshDistanceMeters = 200;

        // A finished run with enough genuine walking/running (on claimed or
        // unclaimed ground) renews all of the runner's shields.
        public bool ShouldRefreshShield(TerritoryAnalysis analysis)
        {
            return !analysis.VehicleSuspected &&
                analysis.GenuineDistanceMeters >= ShieldRefreshDistanceMeters;
        }

        // A fixed map colour for each runner, derived from their user id.
        public static string OwnerColor(string ownerUserId)
        {
            byte[] hash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(ownerUserId ?? string.Empty));

            double hue = ((hash[0] << 8) | hash[1]) % 360;
            return HslToHex(hue, 0.70, 0.50);
        }

        private static string HslToHex(
            double hue, double saturation, double lightness)
        {
            double chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            double x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
            double m = lightness - chroma / 2;

            (double r, double g, double b) = hue switch
            {
                < 60 => (chroma, x, 0.0),
                < 120 => (x, chroma, 0.0),
                < 180 => (0.0, chroma, x),
                < 240 => (0.0, x, chroma),
                < 300 => (x, 0.0, chroma),
                _ => (chroma, 0.0, x)
            };

            int red = (int)Math.Round((r + m) * 255);
            int green = (int)Math.Round((g + m) * 255);
            int blue = (int)Math.Round((b + m) * 255);

            return $"#{red:X2}{green:X2}{blue:X2}";
        }

        public const string AnonymousOwnerLabel = "Anonymous profile";

        // Runners always see their own land at once. Other runners' land
        // appears only after the run that claimed it has finished, so no
        // one can follow a runner live (FR-21). Split leftovers have no run.
        public static bool IsVisibleOnMap(
            string ownerUserId, string? runStatus, string viewerUserId)
        {
            return ownerUserId == viewerUserId ||
                runStatus == null ||
                runStatus == "Completed";
        }

        // Range plus area rounded to whole m², stable across re-analysis.
        private static (int, int, double) ClaimKey(
            int start, int end, double areaSquareMeters)
        {
            return (start, end, Math.Round(areaSquareMeters));
        }

        // Robust overlay (OverlayNG): avoids topology errors on near-identical
        // edges, e.g. when a runner re-runs the boundary of existing land.
        private static Geometry Overlay(
            Geometry a, Geometry b, SpatialFunction operation)
        {
            return OverlayNGRobust.Overlay(a, b, operation);
        }

        // The name shown when a territory is tapped. Private runners appear
        // as "Anonymous profile" to everyone except themselves.
        public static string OwnerLabel(ApplicationUser owner, string viewerUserId)
        {
            bool canShowName =
                owner.IsProfilePublic || owner.Id == viewerUserId;

            if (!canShowName)
                return AnonymousOwnerLabel;

            return string.IsNullOrWhiteSpace(owner.DisplayName)
                ? owner.UserName ?? AnonymousOwnerLabel
                : owner.DisplayName;
        }

        // Active territories whose 3-day shield has ended (FR-19).
        public List<Territory> FindTerritoriesToMarkVulnerable(
            IEnumerable<Territory> territories, DateTime now)
        {
            return territories
                .Where(t => t.Status == "Active" && !IsShielded(t, now))
                .ToList();
        }

        // ----- Road snapping for poor GPS only (FR-12) -----

        // Points less accurate than this are sent to Google Roads.
        public const double SnapAccuracyThresholdMeters = 15;

        // Google Roads accepts at most 100 points per request.
        public const int MaxSnapPointsPerRequest = 100;

        // Stretches of poor GPS to send to Google Roads. Each run of poor
        // points goes with one good neighbour on each side (same leg), split
        // into requests of at most 100 points.
        public List<List<RunGpsPoint>> BuildSnapStretches(
            List<RunGpsPoint> ordered)
        {
            var stretches = new List<List<RunGpsPoint>>();
            int i = 0;

            while (i < ordered.Count)
            {
                if (!NeedsSnapping(ordered[i]))
                {
                    i++;
                    continue;
                }

                // Extend over neighbouring poor points in the same leg.
                int first = i;
                int last = i;
                while (last + 1 < ordered.Count &&
                       NeedsSnapping(ordered[last + 1]) &&
                       ordered[last + 1].LegNo == ordered[first].LegNo)
                {
                    last++;
                }

                // Add one good neighbour each side for road context.
                if (first > 0 &&
                    ordered[first - 1].LegNo == ordered[first].LegNo)
                    first--;

                if (last + 1 < ordered.Count &&
                    ordered[last + 1].LegNo == ordered[last].LegNo)
                    last++;

                var stretch = ordered.GetRange(first, last - first + 1);

                for (int start = 0; start < stretch.Count;
                     start += MaxSnapPointsPerRequest)
                {
                    stretches.Add(stretch.GetRange(start,
                        Math.Min(MaxSnapPointsPerRequest, stretch.Count - start)));
                }

                i = last + 1;
            }

            return stretches;
        }

        // Copy of the stretch with poor points moved onto the road, but only
        // when the snapped position lies inside the point's accuracy circle.
        // Good points are never moved.
        public List<RunGpsPoint> ApplySnappedPoints(
            List<RunGpsPoint> stretch,
            IReadOnlyList<SnappedRoadPoint> snapped)
        {
            var result = stretch.Select(p => new RunGpsPoint
            {
                Id = p.Id,
                RunId = p.RunId,
                SequenceNo = p.SequenceNo,
                LegNo = p.LegNo,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                AccuracyMeters = p.AccuracyMeters,
                RecordedAt = p.RecordedAt
            }).ToList();

            foreach (var road in snapped)
            {
                if (road.OriginalIndex < 0 || road.OriginalIndex >= result.Count)
                    continue;

                var point = result[road.OriginalIndex];
                if (!NeedsSnapping(point))
                    continue;

                double moved = _gpsDistanceService.CalculateDistance(
                    point.Latitude, point.Longitude,
                    road.Latitude, road.Longitude);

                if (moved <= point.AccuracyMeters!.Value)
                {
                    point.Latitude = road.Latitude;
                    point.Longitude = road.Longitude;
                }
            }

            return result;
        }

        // Cleaned route lines for the map: the same cleaning as territory
        // checks (bad accuracy, duplicates and GPS teleports removed, split
        // where GPS is missing), drawn at road-snapped positions.
        public List<List<RunGpsPoint>> BuildDisplayRoute(
            List<RunGpsPoint> points)
        {
            if (points == null || points.Count == 0)
                return new List<List<RunGpsPoint>>();

            var ordered = UseSnappedPositions(
                points.OrderBy(p => p.SequenceNo).ToList());

            return BuildSections(ordered)
                .Where(line => line.Count >= 2)
                .ToList();
        }

        // Copies of the points that use the stored road-snapped position
        // where one exists. Raw database rows are never changed.
        private static List<RunGpsPoint> UseSnappedPositions(
            List<RunGpsPoint> points)
        {
            return points.Select(p => new RunGpsPoint
            {
                Id = p.Id,
                RunId = p.RunId,
                SequenceNo = p.SequenceNo,
                LegNo = p.LegNo,
                Latitude = p.SnappedLatitude ?? p.Latitude,
                Longitude = p.SnappedLongitude ?? p.Longitude,
                AccuracyMeters = p.AccuracyMeters,
                RecordedAt = p.RecordedAt
            }).ToList();
        }

        // Poor but usable accuracy: worse than 15 m, not worse than 30 m.
        private static bool NeedsSnapping(RunGpsPoint point)
        {
            return point.AccuracyMeters.HasValue &&
                double.IsFinite(point.AccuracyMeters.Value) &&
                point.AccuracyMeters.Value > SnapAccuracyThresholdMeters &&
                point.AccuracyMeters.Value <= MaxAccuracyMeters;
        }

        // Polygons in a geometry that are big and wide enough to own.
        private List<Polygon> ValidPieces(Geometry geometry)
        {
            var pieces = new List<Polygon>();

            for (int i = 0; i < geometry.NumGeometries; i++)
            {
                if (geometry.GetGeometryN(i) is not Polygon piece ||
                    piece.IsEmpty)
                    continue;

                double area = CalculateTerritoryArea(piece);

                if (double.IsFinite(area) &&
                    area >= MinTerritoryAreaSquareMeters &&
                    IsWideEnough(piece))
                {
                    pieces.Add(piece);
                }
            }

            return pieces;
        }

        // Approximate area in m² of any longitude/latitude geometry.
        private static double AreaSquareMeters(Geometry geometry)
        {
            double latitude = geometry.Centroid.Y;
            return geometry.Area * MetersPerDegree *
                MetersPerDegree * Math.Cos(latitude * Math.PI / 180);
        }
    }

    // ----- Result types used by TerritoryDetectionService -----

    // Reason codes for rejected territory candidates (FR-17).
    public static class TerritoryRejectionReasons
    {
        public const string VehicleSpeed = "VEHICLE_SPEED";
        public const string TooSmall = "TOO_SMALL";
        public const string TooNarrow = "TOO_NARROW";
        public const string Shielded = "SHIELDED";
        public const string AlreadyOwned = "ALREADY_OWNED";
    }

    // Statuses stored in TerritoryClaims.Status.
    public static class TerritoryClaimStatuses
    {
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    // One enclosed area found in a run, accepted or rejected.
    public sealed class TerritoryCandidate
    {
        public Polygon Boundary { get; init; } = null!;

        public double AreaSquareMeters { get; init; }

        // GPS sequence range that forms this area.
        public int StartSequenceNo { get; init; }
        public int EndSequenceNo { get; init; }

        // Null when accepted.
        public string? RejectionReason { get; init; }
    }

    // Result of analysing one run.
    public sealed class TerritoryAnalysis
    {
        public List<TerritoryCandidate> Accepted { get; } = new();

        public List<TerritoryCandidate> Rejected { get; } = new();

        // True when the run moved at vehicle speed for too long.
        public bool VehicleSuspected { get; set; }

        // Seconds spent at sustained vehicle speed.
        public double VehicleSpeedSeconds { get; set; }

        // Cleaned walking/running distance (0 for vehicle runs).
        public double GenuineDistanceMeters { get; set; }
    }

    // One claim decision to save in TerritoryClaims.
    public sealed class PlannedClaim
    {
        public TerritoryCandidate Candidate { get; init; } = null!;

        public string Status { get; init; } = TerritoryClaimStatuses.Rejected;

        public string? DecisionReason { get; init; }
    }

    // Part of another runner's vulnerable territory taken by a claim.
    public sealed class TerritoryTakeover
    {
        public Territory Territory { get; init; } = null!;

        // The land that changes owner.
        public Geometry TakenPart { get; init; } = null!;

        // What the previous owner keeps (may be split into pieces).
        public List<Polygon> RemainingPieces { get; init; } = new();
    }

    // Outcome of applying shield and overlap rules to one claim.
    public sealed class ClaimResolution
    {
        // New land for the claimant, as separate valid pieces.
        public List<Polygon> AwardedPieces { get; } = new();

        public double AwardedAreaSquareMeters { get; set; }

        public List<TerritoryTakeover> Takeovers { get; } = new();

        // Set when nothing could be awarded.
        public string? RejectionReason { get; set; }
    }

    // One point returned by Google Roads snapToRoads.
    public sealed class SnappedRoadPoint
    {
        // Index of the point in the request that was snapped.
        public int OriginalIndex { get; init; }

        public double Latitude { get; init; }

        public double Longitude { get; init; }
    }
}
