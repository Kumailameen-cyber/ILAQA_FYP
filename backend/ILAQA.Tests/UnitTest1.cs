using ILAQA.Models;
using ILAQA.Services;
using Xunit;

namespace ILAQA.Tests
{
    public class TerritoryDetectionTests
    {
        [Fact]
        public void SquareRoute_ShouldCreateTerritory()
        {
            var service = new TerritoryDetectionService(
                new GpsDistanceService());

            var start = DateTime.UtcNow.AddMinutes(-10);

            var points = new List<RunGpsPoint>
            {
                new() { SequenceNo = 1, LegNo = 1,
                    Latitude = 24.8600, Longitude = 67.0100,
                    AccuracyMeters = 5, RecordedAt = start },

                new() { SequenceNo = 2, LegNo = 1,
                    Latitude = 24.8600, Longitude = 67.0103,
                    AccuracyMeters = 5, RecordedAt = start.AddSeconds(30) },

                new() { SequenceNo = 3, LegNo = 1,
                    Latitude = 24.8603, Longitude = 67.0103,
                    AccuracyMeters = 5, RecordedAt = start.AddSeconds(60) },

                new() { SequenceNo = 4, LegNo = 1,
                    Latitude = 24.8603, Longitude = 67.0100,
                    AccuracyMeters = 5, RecordedAt = start.AddSeconds(90) },

                new() { SequenceNo = 5, LegNo = 1,
                    Latitude = 24.8600, Longitude = 67.0100,
                    AccuracyMeters = 5, RecordedAt = start.AddSeconds(120) }
            };

            var polygon = service.CreateTerritoryPolygon(points);

            Assert.NotNull(polygon);
            Assert.True(service.CalculateTerritoryArea(polygon!) >= 100);
        }
        [Fact]
        public void StraightOutAndBack_ShouldNotCreateTerritory()
        {
            var service = new TerritoryDetectionService(
                new GpsDistanceService());

            var start = DateTime.UtcNow.AddMinutes(-10);

            var points = new List<RunGpsPoint>
    {
        new() { SequenceNo = 1, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start },

        new() { SequenceNo = 2, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0103,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(30) },

        new() { SequenceNo = 3, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0106,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(60) },

        new() { SequenceNo = 4, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0103,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(90) },

        new() { SequenceNo = 5, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(120) }
    };

            var polygon = service.CreateTerritoryPolygon(points);

            Assert.Null(polygon);
        }
        [Fact]
        public void SelfIntersectingRoute_ShouldNotCreateTerritory()
        {
            var service = new TerritoryDetectionService(
                new GpsDistanceService());

            var start = DateTime.UtcNow.AddMinutes(-10);

            var points = new List<RunGpsPoint>
    {
        new() { SequenceNo = 1, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start },

        new() { SequenceNo = 2, LegNo = 1,
            Latitude = 24.8603, Longitude = 67.0103,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(30) },

        new() { SequenceNo = 3, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0103,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(60) },

        new() { SequenceNo = 4, LegNo = 1,
            Latitude = 24.8603, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(90) },

        new() { SequenceNo = 5, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(120) }
    };

            var polygon = service.CreateTerritoryPolygon(points);

            Assert.Null(polygon);
        }
        [Fact]
        public void TinyLoop_ShouldNotCreateTerritory()
        {
            var service = new TerritoryDetectionService(
                new GpsDistanceService());

            var start = DateTime.UtcNow.AddMinutes(-10);

            var points = new List<RunGpsPoint>
    {
        new() { SequenceNo = 1, LegNo = 1,
            Latitude = 24.86000, Longitude = 67.01000,
            AccuracyMeters = 5, RecordedAt = start },

        new() { SequenceNo = 2, LegNo = 1,
            Latitude = 24.86000, Longitude = 67.01004,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(10) },

        new() { SequenceNo = 3, LegNo = 1,
            Latitude = 24.86004, Longitude = 67.01004,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(20) },

        new() { SequenceNo = 4, LegNo = 1,
            Latitude = 24.86004, Longitude = 67.01000,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(30) },

        new() { SequenceNo = 5, LegNo = 1,
            Latitude = 24.86000, Longitude = 67.01000,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(40) }
    };

            var polygon = service.CreateTerritoryPolygon(points);

            Assert.Null(polygon);
        }
        [Fact]
        public void GpsTeleportation_ShouldNotCreateTerritory()
        {
            var service = new TerritoryDetectionService(
                new GpsDistanceService());

            var start = DateTime.UtcNow.AddMinutes(-10);

            var points = new List<RunGpsPoint>
    {
        new() { SequenceNo = 1, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start },

        new() { SequenceNo = 2, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0110,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(1) },

        new() { SequenceNo = 3, LegNo = 1,
            Latitude = 24.8610, Longitude = 67.0110,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(2) },

        new() { SequenceNo = 4, LegNo = 1,
            Latitude = 24.8610, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(3) },

        new() { SequenceNo = 5, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(4) }
    };

            var polygon = service.CreateTerritoryPolygon(points);

            Assert.Null(polygon);
        }
        [Fact]
        public void OutsideKarachi_ShouldNotCreateTerritory()
        {
            var service = new TerritoryDetectionService(
                new GpsDistanceService());

            var start = DateTime.UtcNow.AddMinutes(-10);

            var points = new List<RunGpsPoint>
    {
        new() { SequenceNo = 1, LegNo = 1,
            Latitude = 31.5200, Longitude = 74.3500,
            AccuracyMeters = 5, RecordedAt = start },

        new() { SequenceNo = 2, LegNo = 1,
            Latitude = 31.5200, Longitude = 74.3503,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(30) },

        new() { SequenceNo = 3, LegNo = 1,
            Latitude = 31.5203, Longitude = 74.3503,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(60) },

        new() { SequenceNo = 4, LegNo = 1,
            Latitude = 31.5203, Longitude = 74.3500,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(90) },

        new() { SequenceNo = 5, LegNo = 1,
            Latitude = 31.5200, Longitude = 74.3500,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(120) }
    };

            Assert.Null(service.CreateTerritoryPolygon(points));
        }
        [Fact]
        public void MixedKarachiAndOutside_ShouldNotCreateTerritory()
        {
            var service = new TerritoryDetectionService(
                new GpsDistanceService());

            var start = DateTime.UtcNow.AddMinutes(-10);

            var points = new List<RunGpsPoint>
    {
        new() { SequenceNo = 1, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start },

        new() { SequenceNo = 2, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0103,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(30) },

        // Outside Karachi: Lahore
        new() { SequenceNo = 3, LegNo = 1,
            Latitude = 31.5200, Longitude = 74.3500,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(60) },

        new() { SequenceNo = 4, LegNo = 1,
            Latitude = 24.8603, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(90) },

        new() { SequenceNo = 5, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(120) }
    };

            Assert.Null(service.CreateTerritoryPolygon(points));
        }
        [Fact]
        public void DifferentRunningLegs_ShouldNotCreateTerritory()
        {
            var service = new TerritoryDetectionService(
                new GpsDistanceService());

            var start = DateTime.UtcNow.AddMinutes(-10);

            var points = new List<RunGpsPoint>
    {
        new() { SequenceNo = 1, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start },

        new() { SequenceNo = 2, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0103,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(30) },

        // Runner pauses here and later resumes.
        new() { SequenceNo = 3, LegNo = 2,
            Latitude = 24.8603, Longitude = 67.0103,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(120) },

        new() { SequenceNo = 4, LegNo = 2,
            Latitude = 24.8603, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(150) },

        new() { SequenceNo = 5, LegNo = 2,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(180) }
    };

            var polygon = service.CreateTerritoryPolygon(points);

            Assert.Null(polygon);
        }
        [Fact]
        public void PoorGpsAccuracy_ShouldNotCreateTerritory()
        {
            var service = new TerritoryDetectionService(
                new GpsDistanceService());

            var start = DateTime.UtcNow.AddMinutes(-10);

            var points = new List<RunGpsPoint>
    {
        new() { SequenceNo = 1, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start },

        new() { SequenceNo = 2, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0103,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(30) },

        // Poor GPS accuracy: 80 meters
        new() { SequenceNo = 3, LegNo = 1,
            Latitude = 24.8603, Longitude = 67.0103,
            AccuracyMeters = 80, RecordedAt = start.AddSeconds(60) },

        new() { SequenceNo = 4, LegNo = 1,
            Latitude = 24.8603, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(90) },

        new() { SequenceNo = 5, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(120) }
    };

            var polygon = service.CreateTerritoryPolygon(points);

            Assert.Null(polygon);
        }
        [Fact]
        public void MissingGpsAccuracy_ShouldNotCreateTerritory()
        {
            var service = new TerritoryDetectionService(
                new GpsDistanceService());

            var start = DateTime.UtcNow.AddMinutes(-10);

            var points = new List<RunGpsPoint>
    {
        new() { SequenceNo = 1, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start },

        new() { SequenceNo = 2, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0103,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(30) },

        // GPS accuracy is missing
        new() { SequenceNo = 3, LegNo = 1,
            Latitude = 24.8603, Longitude = 67.0103,
            AccuracyMeters = null, RecordedAt = start.AddSeconds(60) },

        new() { SequenceNo = 4, LegNo = 1,
            Latitude = 24.8603, Longitude = 67.0100,
            AccuracyMeters = 5, RecordedAt = start.AddSeconds(90) },

        new() { SequenceNo = 5, LegNo = 1,
            Latitude = 24.8600, Longitude = 67.0100,
            AccuracyMeters = null, RecordedAt = start.AddSeconds(120) }
    };

            var polygon = service.CreateTerritoryPolygon(points);

            Assert.Null(polygon);
        }
        // TEST 11: Reject timestamps going backwards
        [Fact]
        public void BackwardTimestamps_ShouldNotCreateTerritory()
        {
            var points = ValidSquare();
            points[2].RecordedAt = points[1].RecordedAt.AddSeconds(-1);

            Assert.Null(Service().CreateTerritoryPolygon(points));
        }

        // TEST 12: Reject duplicate timestamps
        [Fact]
        public void DuplicateTimestamps_ShouldNotCreateTerritory()
        {
            var points = ValidSquare();
            points[2].RecordedAt = points[1].RecordedAt;

            Assert.Null(Service().CreateTerritoryPolygon(points));
        }

        // TEST 13: Reject unrealistic speed on one segment
        [Fact]
        public void ExcessiveSpeed_ShouldNotCreateTerritory()
        {
            var points = ValidSquare();
            points[1].RecordedAt = points[0].RecordedAt.AddSeconds(1);

            Assert.Null(Service().CreateTerritoryPolygon(points));
        }

        // TEST 14: Accept a route below the maximum speed
        [Fact]
        public void NormalRunningSpeed_ShouldCreateTerritory()
        {
            var points = ValidSquare();

            Assert.NotNull(Service().CreateTerritoryPolygon(points));
        }

        // TEST 15: Reject an open route
        [Fact]
        public void OpenRoute_ShouldNotCreateTerritory()
        {
            var points = ValidSquare();
            points.RemoveAt(points.Count - 1);

            Assert.Null(Service().CreateTerritoryPolygon(points));
        }

        // TEST 16: Reject a route with fewer than four points
        [Fact]
        public void TooFewPoints_ShouldNotCreateTerritory()
        {
            var points = ValidSquare().Take(3).ToList();

            Assert.Null(Service().CreateTerritoryPolygon(points));
        }

        // TEST 17: Reject an invalid latitude
        [Fact]
        public void InvalidLatitude_ShouldNotCreateTerritory()
        {
            var points = ValidSquare();
            points[2].Latitude = 95;

            Assert.Null(Service().CreateTerritoryPolygon(points));
        }

        // TEST 18: Reject an invalid longitude
        [Fact]
        public void InvalidLongitude_ShouldNotCreateTerritory()
        {
            var points = ValidSquare();
            points[2].Longitude = 200;

            Assert.Null(Service().CreateTerritoryPolygon(points));
        }

        // TEST 19: Reject NaN GPS coordinates
        [Fact]
        public void NaNCoordinates_ShouldNotCreateTerritory()
        {
            var points = ValidSquare();
            points[2].Latitude = double.NaN;

            Assert.Null(Service().CreateTerritoryPolygon(points));
        }

        // TEST 20: Reject negative GPS accuracy
        [Fact]
        public void NegativeAccuracy_ShouldNotCreateTerritory()
        {
            var points = ValidSquare();
            points[2].AccuracyMeters = -5;

            Assert.Null(Service().CreateTerritoryPolygon(points));
        }

        // TEST 21: Accept GPS accuracy at the allowed limit
        [Fact]
        public void AccuracyExactly30Meters_ShouldCreateTerritory()
        {
            var points = ValidSquare();
            points[2].AccuracyMeters = 30;

            Assert.NotNull(Service().CreateTerritoryPolygon(points));
        }

        // TEST 22: Reject GPS accuracy above the allowed limit
        [Fact]
        public void AccuracyAbove30Meters_ShouldNotCreateTerritory()
        {
            var points = ValidSquare();
            points[2].AccuracyMeters = 30.1;

            Assert.Null(Service().CreateTerritoryPolygon(points));
        }

        // TEST 23: Sort GPS points using SequenceNo
        [Fact]
        public void UnorderedGpsPoints_ShouldStillCreateTerritory()
        {
            var points = ValidSquare();
            points.Reverse();

            Assert.NotNull(Service().CreateTerritoryPolygon(points));
        }

        // TEST 24: Reject a loop whose endpoint is too far away
        [Fact]
        public void EndpointBeyondClosureLimit_ShouldNotCreateTerritory()
        {
            var points = ValidSquare();

            points[4].Latitude = 24.8602;

            Assert.Null(Service().CreateTerritoryPolygon(points));
        }

        // TEST 25: Accept a valid square and verify its geometry
        [Fact]
        public void ValidPolygon_ShouldHaveCorrectGeometry()
        {
            var polygon = Service().CreateTerritoryPolygon(ValidSquare());

            Assert.NotNull(polygon);
            Assert.True(polygon!.IsValid);
            Assert.Equal(4326, polygon.SRID);
            Assert.True(Service().CalculateTerritoryArea(polygon) >= 100);
        }


        // TEST 26: Two separate loops in one uninterrupted run.
        [Fact]
        public void TwoValidLoopsInOneLeg_ShouldFindTwoTerritories()
        {
            var first = ValidSquare();
            var second = MakeSquare(0.0006, 1, 6,
                first.Last().RecordedAt.AddSeconds(30));

            first.AddRange(second);

            Assert.Equal(2, Service().FindTerritories(first).Count);
        }

        // TEST 27: A pause must not join two different GPS legs.
        [Fact]
        public void TwoValidLoopsWithPause_ShouldFindTwoTerritories()
        {
            var first = ValidSquare();
            var second = MakeSquare(0.0010, 2, 6,
                first.Last().RecordedAt.AddMinutes(5));

            first.AddRange(second);

            Assert.Equal(2, Service().FindTerritories(first).Count);
        }

        // TEST 28: An invalid first leg must not cancel a valid second leg.
        [Fact]
        public void InvalidFirstLeg_ValidSecondLeg_ShouldFindOneTerritory()
        {
            var first = MakeOutAndBack(1, 1, DateTime.UtcNow.AddMinutes(-20));
            var second = MakeSquare(0.0010, 2, 6,
                first.Last().RecordedAt.AddMinutes(5));

            first.AddRange(second);

            Assert.Single(Service().FindTerritories(first));
        }

        // TEST 29: A GPS gap cannot erase an earlier valid territory.
        [Fact]
        public void ValidLoop_Gap_InvalidLoop_ShouldKeepFirstTerritory()
        {
            var first = ValidSquare();
            var second = MakeOutAndBack(1, 6,
                first.Last().RecordedAt.AddMinutes(5));

            first.AddRange(second);

            Assert.Single(Service().FindTerritories(first));
        }

        // TEST 30: An open route before a gap cannot claim territory.
        [Fact]
        public void UnfinishedRoute_Gap_ValidLoop_ShouldFindOneTerritory()
        {
            var first = ValidSquare().Take(3).ToList();
            var second = MakeSquare(0.0010, 1, 4,
                first.Last().RecordedAt.AddMinutes(5));

            first.AddRange(second);

            Assert.Single(Service().FindTerritories(first));
        }

        // TEST 31: Going straight out and back never creates territory.
        [Fact]
        public void StraightOutAndBack_ShouldFindNoTerritories()
        {
            var points = MakeOutAndBack(1, 1,
                DateTime.UtcNow.AddMinutes(-10));

            Assert.Empty(Service().FindTerritories(points));
        }

        // TEST 32: Poor GPS must not erase complete loops on either side.
        [Fact]
        public void BadGpsBetweenLoops_ShouldKeepBothTerritories()
        {
            var first = ValidSquare();
            var badPoint = new RunGpsPoint
            {
                SequenceNo = 6,
                LegNo = 1,
                Latitude = 24.8600,
                Longitude = 67.0105,
                AccuracyMeters = 80,
                RecordedAt = first.Last().RecordedAt.AddSeconds(30)
            };
            var second = MakeSquare(0.0010, 1, 7,
                badPoint.RecordedAt.AddSeconds(30));

            first.Add(badPoint);
            first.AddRange(second);

            Assert.Equal(2, Service().FindTerritories(first).Count);
        }

        // HELPER METHODS
        // Add these only once inside TerritoryDetectionTests.

        // Reuse the existing valid square with a different position/time/leg.
        private static List<RunGpsPoint> MakeSquare(
            double longitudeShift, int leg, int firstSequence, DateTime start)
        {
            var points = ValidSquare();

            for (int i = 0; i < points.Count; i++)
            {
                points[i].Longitude += longitudeShift;
                points[i].LegNo = leg;
                points[i].SequenceNo = firstSequence + i;
                points[i].RecordedAt = start.AddSeconds(i * 30);
            }

            return points;
        }

        // A runner travels east and returns on the same straight line.
        private static List<RunGpsPoint> MakeOutAndBack(
            int leg, int firstSequence, DateTime start)
        {
            var points = MakeSquare(0, leg, firstSequence, start);
            double[] longitudeSteps = { 0, 0.0003, 0.0006, 0.0003, 0 };

            for (int i = 0; i < points.Count; i++)
            {
                points[i].Latitude = 24.8600;
                points[i].Longitude = 67.0100 + longitudeSteps[i];
            }

            return points;
        }

        private static TerritoryDetectionService Service()
        {
            return new TerritoryDetectionService(
                new GpsDistanceService());
        }

        private static List<RunGpsPoint> ValidSquare()
        {
            var start = DateTime.UtcNow.AddMinutes(-10);

            return new List<RunGpsPoint>
    {
        new()
        {
            SequenceNo = 1,
            LegNo = 1,
            Latitude = 24.8600,
            Longitude = 67.0100,
            AccuracyMeters = 5,
            RecordedAt = start
        },
        new()
        {
            SequenceNo = 2,
            LegNo = 1,
            Latitude = 24.8600,
            Longitude = 67.0103,
            AccuracyMeters = 5,
            RecordedAt = start.AddSeconds(30)
        },
        new()
        {
            SequenceNo = 3,
            LegNo = 1,
            Latitude = 24.8603,
            Longitude = 67.0103,
            AccuracyMeters = 5,
            RecordedAt = start.AddSeconds(60)
        },
        new()
        {
            SequenceNo = 4,
            LegNo = 1,
            Latitude = 24.8603,
            Longitude = 67.0100,
            AccuracyMeters = 5,
            RecordedAt = start.AddSeconds(90)
        },
        new()
        {
            SequenceNo = 5,
            LegNo = 1,
            Latitude = 24.8600,
            Longitude = 67.0100,
            AccuracyMeters = 5,
            RecordedAt = start.AddSeconds(120)
        }
    };
        }
    }
}