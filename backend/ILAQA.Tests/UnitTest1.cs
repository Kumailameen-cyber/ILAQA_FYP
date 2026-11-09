using ILAQA.Models;
using ILAQA.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Security.Claims;
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

        // TEST 33: Duplicate GPS timestamps should not destroy a valid loop.
        [Fact]
        public void DuplicateGpsTimestamp_ShouldStillFindTerritory()
        {
            var points = ValidSquare();

            var duplicate = new RunGpsPoint
            {
                SequenceNo = 3,
                LegNo = 1,
                Latitude = points[1].Latitude,
                Longitude = points[1].Longitude,
                AccuracyMeters = 5,
                RecordedAt = points[1].RecordedAt.AddMilliseconds(5)
            };

            for (int i = 2; i < points.Count; i++)
                points[i].SequenceNo++;

            points.Insert(2, duplicate);

            Assert.Single(Service().FindTerritories(points));
        }

        // TEST 34: One unrealistic GPS jump should be ignored.
        [Fact]
        public void GpsJump_ShouldStillFindTerritory()
        {
            var points = ValidSquare();

            var jump = new RunGpsPoint
            {
                SequenceNo = 3,
                LegNo = 1,
                Latitude = 24.8650,
                Longitude = 67.0150,
                AccuracyMeters = 5,
                RecordedAt = points[1].RecordedAt.AddSeconds(1)
            };

            for (int i = 2; i < points.Count; i++)
                points[i].SequenceNo++;

            points.Insert(2, jump);

            Assert.Single(Service().FindTerritories(points));
        }

        // TEST 35: One inaccurate GPS reading should be ignored.
        [Fact]
        public void InaccurateGpsPoint_ShouldStillFindTerritory()
        {
            var points = ValidSquare();

            var inaccurate = new RunGpsPoint
            {
                SequenceNo = 3,
                LegNo = 1,
                Latitude = 24.8650,
                Longitude = 67.0150,
                AccuracyMeters = 80,
                RecordedAt = points[1].RecordedAt.AddSeconds(1)
            };

            for (int i = 2; i < points.Count; i++)
                points[i].SequenceNo++;

            points.Insert(2, inaccurate);

            Assert.Single(Service().FindTerritories(points));
        }

        [Fact]
        public void AlFarooqGround_ShouldCreateTerritory()
        {
            var service = new TerritoryDetectionService(
                new GpsDistanceService());

            var start = DateTime.UtcNow.AddMinutes(-20);

            // GPS points around Al Farooq Cricket Ground,
            // Nazimabad No. 2, Karachi.
            var coordinates = new (double Lat, double Lon)[]
            {
        (24.906475, 67.033082),
        (24.906756, 67.033012),
        (24.907110, 67.032718),
        (24.906771, 67.031706),
        (24.906052, 67.031606),
        (24.905925, 67.032805),
        (24.906475, 67.033082)
            };

            var points = coordinates
                .Select((coordinate, index) => new RunGpsPoint
                {
                    SequenceNo = index + 1,
                    LegNo = 1,
                    Latitude = coordinate.Lat,
                    Longitude = coordinate.Lon,
                    AccuracyMeters = 5,
                    RecordedAt = start.AddSeconds(index * 60)
                })
                .ToList();

            // Verify the GPS route closes successfully.
            Assert.True(service.IsLoopClosed(points));

            // Verify that a valid polygon is created.
            var polygon = service.CreateTerritoryPolygon(points);

            Assert.NotNull(polygon);
            Assert.True(polygon!.IsValid);

            // Verify territory size.
            double area = service.CalculateTerritoryArea(polygon);

            Assert.True(area >= 100);

            // Verify the complete detection pipeline.
            var territories = service.FindTerritories(points);

            Assert.Single(territories);
        }

        // =====================================================================
        // Feature 1: crossing routes and vehicle speed.
        // Synthetic routes sampled once per second, in metres from a Karachi
        // origin (see Route helper at the end of this class).
        // =====================================================================

        private const double WalkSpeed = 1.5;   // m/s
        private const double JogSpeed = 3.5;    // m/s
        private const double BikeSpeed = 7.0;   // m/s, below the old 8 m/s cap

        // A 60 m square walk with a small GPS knot that crosses itself.
        [Fact]
        public void LoopWithSmallGpsKnot_ShouldStillClaimTerritory()
        {
            var points = new Route()
                .To(60, 0, WalkSpeed)
                .To(60, 24, WalkSpeed)
                .To(66, 36, WalkSpeed)   // knot starts
                .To(54, 36, WalkSpeed)
                .To(62, 26, WalkSpeed)   // crosses back over itself
                .To(60, 60, WalkSpeed)
                .To(0, 60, WalkSpeed)
                .To(0, 0, WalkSpeed)
                .Build();

            var service = Service();
            var territory = Assert.Single(service.FindTerritories(points));

            Assert.InRange(service.CalculateTerritoryArea(territory), 3200, 3800);
        }

        // A figure-8: two genuine enclosed lobes joined at one crossing.
        [Fact]
        public void FigureEight_ShouldClaimBothLobes()
        {
            var points = new Route()
                .To(60, 60, WalkSpeed)
                .To(60, 120, WalkSpeed)
                .To(0, 120, WalkSpeed)
                .To(0, 60, WalkSpeed)
                .To(60, 0, WalkSpeed)    // crosses the first diagonal
                .To(0, 0, WalkSpeed)
                .Build();

            Assert.Equal(2, Service().FindTerritories(points).Count);
        }

        // Two sides of a 120 m square ridden at bike speed (about 34 s).
        [Fact]
        public void LoopWithSustainedBikeSpeed_ShouldNotClaimTerritory()
        {
            Assert.Empty(Service().FindTerritories(BikeLoop()));
        }

        [Fact]
        public void LoopWithSustainedBikeSpeed_ShouldGiveVehicleReason()
        {
            var analysis = Service().AnalyzeRun(BikeLoop());

            Assert.True(analysis.VehicleSuspected);
            Assert.Empty(analysis.Accepted);
            Assert.Contains(analysis.Rejected, candidate =>
                candidate.RejectionReason ==
                TerritoryRejectionReasons.VehicleSpeed);
        }

        // Guard: a normal jogging loop must still be claimed.
        [Fact]
        public void JoggingLoop_ShouldClaimTerritory()
        {
            var points = new Route()
                .To(120, 0, JogSpeed)
                .To(120, 120, JogSpeed)
                .To(0, 120, JogSpeed)
                .To(0, 0, JogSpeed)
                .Build();

            Assert.Single(Service().FindTerritories(points));
        }

        // Guard: a runner's short 10-second sprint is not a vehicle.
        [Fact]
        public void WalkingLoopWithShortSprint_ShouldClaimTerritory()
        {
            var points = new Route()
                .To(50, 0, WalkSpeed)
                .To(120, 0, BikeSpeed)   // 70 m in 10 s
                .To(120, 120, WalkSpeed)
                .To(0, 120, WalkSpeed)
                .To(0, 0, WalkSpeed)
                .Build();

            Assert.Single(Service().FindTerritories(points));
        }

        // =====================================================================
        // Feature 1: saving claim decisions (TerritoryClaims, FR-17).
        // Each meaningful candidate is planned exactly once, with a reason.
        // =====================================================================

        private static readonly (int, int, double)[] NothingSubmitted =
            Array.Empty<(int, int, double)>();

        [Fact]
        public void AcceptedCandidate_ShouldBePlannedAsApproved()
        {
            var analysis = new TerritoryAnalysis();
            analysis.Accepted.Add(Candidate(1, 40, 3600, null));

            var claim = Assert.Single(
                Service().PlanNewClaims(analysis, NothingSubmitted));

            Assert.Equal(TerritoryClaimStatuses.Approved, claim.Status);
            Assert.Null(claim.DecisionReason);
            Assert.Equal(1, claim.Candidate.StartSequenceNo);
            Assert.Equal(40, claim.Candidate.EndSequenceNo);
        }

        [Fact]
        public void AlreadySubmittedRange_ShouldNotBePlannedAgain()
        {
            var analysis = new TerritoryAnalysis();
            analysis.Accepted.Add(Candidate(1, 40, 3600, null));

            Assert.Empty(Service().PlanNewClaims(analysis, new[] { (1, 40, 3600.0) }));
        }

        // A later upload adds a second loop: only the new loop is planned.
        [Fact]
        public void LaterUploadWithNewLoop_ShouldPlanOnlyTheNewLoop()
        {
            var analysis = new TerritoryAnalysis();
            analysis.Accepted.Add(Candidate(1, 40, 3600, null));
            analysis.Accepted.Add(Candidate(55, 90, 2500, null));

            var claim = Assert.Single(
                Service().PlanNewClaims(analysis, new[] { (1, 40, 3600.0) }));

            Assert.Equal(55, claim.Candidate.StartSequenceNo);
            Assert.Equal(90, claim.Candidate.EndSequenceNo);
        }

        [Fact]
        public void VehicleSpeedCandidate_ShouldBePlannedAsRejectedWithReason()
        {
            var analysis = new TerritoryAnalysis { VehicleSuspected = true };
            analysis.Rejected.Add(Candidate(1, 40, 7500,
                TerritoryRejectionReasons.VehicleSpeed));

            var claim = Assert.Single(
                Service().PlanNewClaims(analysis, NothingSubmitted));

            Assert.Equal(TerritoryClaimStatuses.Rejected, claim.Status);
            Assert.Equal(TerritoryRejectionReasons.VehicleSpeed,
                claim.DecisionReason);
        }

        // Tiny GPS knots (a few m²) are noise, not claim attempts.
        [Fact]
        public void TinyGpsKnot_ShouldNotBeRecorded()
        {
            var analysis = new TerritoryAnalysis();
            analysis.Rejected.Add(Candidate(20, 24, 3,
                TerritoryRejectionReasons.TooSmall));

            Assert.Empty(Service().PlanNewClaims(analysis, NothingSubmitted));
        }

        // A real but undersized loop still gets a "too small" reason.
        [Fact]
        public void SmallButRealLoop_ShouldBeRecordedAsTooSmall()
        {
            var analysis = new TerritoryAnalysis();
            analysis.Rejected.Add(Candidate(1, 30, 80,
                TerritoryRejectionReasons.TooSmall));

            var claim = Assert.Single(
                Service().PlanNewClaims(analysis, NothingSubmitted));

            Assert.Equal(TerritoryRejectionReasons.TooSmall,
                claim.DecisionReason);
        }

        [Fact]
        public void PlannedClaims_ShouldBeInRouteOrder()
        {
            var analysis = new TerritoryAnalysis();
            analysis.Accepted.Add(Candidate(55, 90, 2500, null));
            analysis.Rejected.Add(Candidate(1, 40, 300,
                TerritoryRejectionReasons.TooNarrow));

            var claims = Service().PlanNewClaims(analysis, NothingSubmitted);

            Assert.Equal(new[] { 1, 55 },
                claims.Select(c => c.Candidate.StartSequenceNo).ToArray());
        }

        // ----- Feature 1 helpers -----

        private static List<RunGpsPoint> BikeLoop()
        {
            return new Route()
                .To(120, 0, WalkSpeed)
                .To(120, 120, BikeSpeed)
                .To(0, 120, BikeSpeed)
                .To(0, 0, WalkSpeed)
                .Build();
        }

        private static TerritoryCandidate Candidate(
            int start, int end, double area, string? reason)
        {
            var factory = new GeometryFactory(new PrecisionModel(), 4326);
            var boundary = factory.CreatePolygon(new[]
            {
                new Coordinate(67.0100, 24.8600),
                new Coordinate(67.0106, 24.8600),
                new Coordinate(67.0106, 24.8606),
                new Coordinate(67.0100, 24.8600)
            });

            return new TerritoryCandidate
            {
                Boundary = boundary,
                AreaSquareMeters = area,
                StartSequenceNo = start,
                EndSequenceNo = end,
                RejectionReason = reason
            };
        }

        // Builds a GPS route from waypoints in metres (x = east, y = north),
        // with one point per second at the given speed.
        private sealed class Route
        {
            private const double OriginLatitude = 24.8600;
            private const double OriginLongitude = 67.0100;
            private const double MetersPerDegree = 111320;

            private static readonly DateTime StartTime =
                new(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);

            private readonly List<RunGpsPoint> _points = new();
            private double _x;
            private double _y;
            private DateTime _time = StartTime;

            public Route()
            {
                Add();
            }

            public Route To(double x, double y, double speed)
            {
                double distance = Math.Sqrt(
                    Math.Pow(x - _x, 2) + Math.Pow(y - _y, 2));
                int steps = Math.Max(1, (int)Math.Ceiling(distance / speed));
                double stepSeconds = distance / speed / steps;
                double startX = _x;
                double startY = _y;

                for (int i = 1; i <= steps; i++)
                {
                    double fraction = (double)i / steps;
                    _x = startX + (x - startX) * fraction;
                    _y = startY + (y - startY) * fraction;
                    _time = _time.AddSeconds(stepSeconds);
                    Add();
                }

                return this;
            }

            // Stand still: no GPS points for this many seconds.
            public Route Wait(double seconds)
            {
                _time = _time.AddSeconds(seconds);
                return this;
            }

            // Missing GPS: the next point appears after a straight jump.
            public Route JumpTo(double x, double y, double seconds)
            {
                _x = x;
                _y = y;
                _time = _time.AddSeconds(seconds);
                Add();
                return this;
            }

            public List<RunGpsPoint> Build() => _points;

            private void Add()
            {
                double metersPerDegreeLongitude = MetersPerDegree *
                    Math.Cos(OriginLatitude * Math.PI / 180);

                _points.Add(new RunGpsPoint
                {
                    SequenceNo = _points.Count + 1,
                    LegNo = 1,
                    Latitude = OriginLatitude + _y / MetersPerDegree,
                    Longitude = OriginLongitude + _x / metersPerDegreeLongitude,
                    AccuracyMeters = 5,
                    RecordedAt = _time
                });
            }
        }

        // =====================================================================
        // Feature 1: shield and overlap rules (FR-18 to FR-20).
        // Shielded land of another runner is protected (only the free part is
        // awarded). From vulnerable land, only the covered part is taken.
        // Squares are in metres from the same Karachi origin as Route.
        // =====================================================================

        private const string Me = "runner-me";
        private const string Rival = "runner-rival";

        private static readonly DateTime Now =
            new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void ClaimOnFreeLand_ShouldAwardWholeArea()
        {
            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory>(), Now);

            Assert.Null(result.RejectionReason);
            Assert.Single(result.AwardedPieces);
            Assert.InRange(result.AwardedAreaSquareMeters, 9900, 10100);
            Assert.Empty(result.Takeovers);
        }

        [Fact]
        public void ClaimOverShieldedLand_ShouldAwardOnlyFreePart()
        {
            var shielded = Owned(Rival, Rect(-50, 0, 50, 100), "Active",
                Now.AddDays(2));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { shielded }, Now);

            Assert.Null(result.RejectionReason);
            Assert.InRange(result.AwardedAreaSquareMeters, 4900, 5100);
            Assert.Empty(result.Takeovers);
        }

        [Fact]
        public void ClaimInsideShieldedLand_ShouldBeRejectedAsShielded()
        {
            var shielded = Owned(Rival, Rect(-50, -50, 150, 150), "Active",
                Now.AddDays(2));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { shielded }, Now);

            Assert.Equal(TerritoryRejectionReasons.Shielded,
                result.RejectionReason);
            Assert.Empty(result.AwardedPieces);
            Assert.Empty(result.Takeovers);
        }

        [Fact]
        public void ClaimOverVulnerableLand_ShouldTakeOnlyCoveredPart()
        {
            var vulnerable = Owned(Rival, Rect(50, 0, 150, 100), "Vulnerable",
                Now.AddDays(-1));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { vulnerable }, Now);

            Assert.Null(result.RejectionReason);
            Assert.InRange(result.AwardedAreaSquareMeters, 9900, 10100);

            var takeover = Assert.Single(result.Takeovers);
            Assert.InRange(AreaOf(takeover.TakenPart), 4900, 5100);
            var remaining = Assert.Single(takeover.RemainingPieces);
            Assert.InRange(AreaOf(remaining), 4900, 5100);
        }

        // The scheduler may not have run yet: an expired shield is vulnerable.
        [Fact]
        public void ExpiredShield_ShouldCountAsVulnerable()
        {
            var expired = Owned(Rival, Rect(50, 0, 150, 100), "Active",
                Now.AddHours(-1));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { expired }, Now);

            Assert.Single(result.Takeovers);
            Assert.InRange(result.AwardedAreaSquareMeters, 9900, 10100);
        }

        [Fact]
        public void ClaimCoveringWholeVulnerableTerritory_ShouldLeaveNothing()
        {
            var vulnerable = Owned(Rival, Rect(20, 20, 80, 80), "Vulnerable",
                Now.AddDays(-1));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { vulnerable }, Now);

            var takeover = Assert.Single(result.Takeovers);
            Assert.Empty(takeover.RemainingPieces);
            Assert.InRange(result.AwardedAreaSquareMeters, 9900, 10100);
        }

        // A loop through the middle of vulnerable land splits what is left.
        [Fact]
        public void ClaimThroughMiddleOfVulnerableTerritory_ShouldSplitIt()
        {
            var vulnerable = Owned(Rival, Rect(-50, 30, 150, 70), "Vulnerable",
                Now.AddDays(-1));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { vulnerable }, Now);

            var takeover = Assert.Single(result.Takeovers);
            Assert.Equal(2, takeover.RemainingPieces.Count);
            Assert.All(takeover.RemainingPieces, piece =>
                Assert.InRange(AreaOf(piece), 1900, 2100));
        }

        [Fact]
        public void ClaimOverOwnLand_ShouldAwardOnlyNewPart()
        {
            var mine = Owned(Me, Rect(-50, 0, 50, 100), "Active",
                Now.AddDays(2));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { mine }, Now);

            Assert.Null(result.RejectionReason);
            Assert.InRange(result.AwardedAreaSquareMeters, 4900, 5100);
            Assert.Empty(result.Takeovers);
        }

        [Fact]
        public void ClaimInsideOwnLand_ShouldBeRejectedAsAlreadyOwned()
        {
            var mine = Owned(Me, Rect(-50, -50, 150, 150), "Active",
                Now.AddDays(2));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { mine }, Now);

            Assert.Equal(TerritoryRejectionReasons.AlreadyOwned,
                result.RejectionReason);
            Assert.Empty(result.AwardedPieces);
        }

        // ----- Live takeover guard (fix #1) -----
        // Taking a rival's land during a run would change their territory on
        // everyone's map and reveal the runner's route. Such a claim waits
        // until Finish. A claim that takes no one's land still applies live.

        [Fact]
        public void ClaimOverVulnerableLandDuringRun_ShouldBePostponed()
        {
            var vulnerable = Owned(Rival, Rect(50, 0, 150, 100), "Vulnerable",
                Now.AddDays(-1));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { vulnerable }, Now);

            Assert.True(TerritoryDetectionService.ShouldPostponeClaim(
                result, runIsFinishing: false));
        }

        [Fact]
        public void ClaimOverVulnerableLandAtFinish_ShouldBeApplied()
        {
            var vulnerable = Owned(Rival, Rect(50, 0, 150, 100), "Vulnerable",
                Now.AddDays(-1));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { vulnerable }, Now);

            Assert.False(TerritoryDetectionService.ShouldPostponeClaim(
                result, runIsFinishing: true));
        }

        [Fact]
        public void ClaimOnFreeLandDuringRun_ShouldApplyLive()
        {
            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory>(), Now);

            Assert.False(TerritoryDetectionService.ShouldPostponeClaim(
                result, runIsFinishing: false));
        }

        // Shielded rival land is never taken, so the free part applies live.
        [Fact]
        public void ClaimOverShieldedLandDuringRun_ShouldApplyFreePartLive()
        {
            var shielded = Owned(Rival, Rect(-50, 0, 50, 100), "Active",
                Now.AddDays(2));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { shielded }, Now);

            Assert.False(TerritoryDetectionService.ShouldPostponeClaim(
                result, runIsFinishing: false));
        }

        // ----- Shield and overlap helpers -----

        private const double OriginLatitude = 24.8600;
        private const double OriginLongitude = 67.0100;
        private const double MetersPerDegree = 111320;

        private static double MetersPerDegreeLongitude =>
            MetersPerDegree * Math.Cos(OriginLatitude * Math.PI / 180);

        // Rectangle from (x0, y0) to (x1, y1) metres east/north of the origin.
        private static Polygon Rect(double x0, double y0, double x1, double y1)
        {
            Coordinate At(double x, double y) => new(
                OriginLongitude + x / MetersPerDegreeLongitude,
                OriginLatitude + y / MetersPerDegree);

            var factory = new GeometryFactory(new PrecisionModel(), 4326);
            return factory.CreatePolygon(new[]
            {
                At(x0, y0), At(x1, y0), At(x1, y1), At(x0, y1), At(x0, y0)
            });
        }

        private static Territory Owned(
            string owner, Polygon boundary, string status, DateTime shieldExpiresAt)
        {
            return new Territory
            {
                OwnerUserId = owner,
                Boundary = boundary,
                Status = status,
                ShieldExpiresAt = shieldExpiresAt
            };
        }

        private static double AreaOf(Geometry geometry)
        {
            return geometry.Area * MetersPerDegree * MetersPerDegreeLongitude;
        }

        // =====================================================================
        // Feature 1: shield refresh, map colours and private profiles.
        // A run with at least 200 m of genuine movement (anywhere) renews the
        // owner's shields. Each runner has their own territory colour.
        // Private runners' territories show "Anonymous profile".
        // =====================================================================

        [Fact]
        public void WalkingRunOver200m_ShouldRefreshShield()
        {
            var service = Service();
            var analysis = service.AnalyzeRun(new Route()
                .To(60, 0, WalkSpeed)
                .To(60, 60, WalkSpeed)
                .To(0, 60, WalkSpeed)
                .To(0, 0, WalkSpeed)
                .Build());

            Assert.InRange(analysis.GenuineDistanceMeters, 230, 250);
            Assert.True(service.ShouldRefreshShield(analysis));
        }

        // Unclaimed ground counts too: an open 250 m walk with no loop.
        [Fact]
        public void OpenWalkOver200m_ShouldRefreshShield()
        {
            var service = Service();
            var analysis = service.AnalyzeRun(new Route()
                .To(250, 0, WalkSpeed)
                .Build());

            Assert.Empty(analysis.Accepted);
            Assert.True(service.ShouldRefreshShield(analysis));
        }

        [Fact]
        public void WalkUnder200m_ShouldNotRefreshShield()
        {
            var service = Service();
            var analysis = service.AnalyzeRun(new Route()
                .To(150, 0, WalkSpeed)
                .Build());

            Assert.False(service.ShouldRefreshShield(analysis));
        }

        [Fact]
        public void BikeRun_ShouldNotRefreshShield()
        {
            var service = Service();
            var analysis = service.AnalyzeRun(BikeLoop());

            Assert.Equal(0, analysis.GenuineDistanceMeters);
            Assert.False(service.ShouldRefreshShield(analysis));
        }

        [Fact]
        public void OwnerColor_ShouldBeAHexColour()
        {
            Assert.Matches("^#[0-9A-F]{6}$",
                TerritoryDetectionService.OwnerColor("runner-a"));
        }

        [Fact]
        public void SameOwner_ShouldAlwaysGetTheSameColour()
        {
            Assert.Equal(
                TerritoryDetectionService.OwnerColor("runner-a"),
                TerritoryDetectionService.OwnerColor("runner-a"));
        }

        [Fact]
        public void DifferentOwners_ShouldGetDifferentColours()
        {
            var colours = new[]
            {
                "3f2b1c9e-0a11-4c55-9d1e-1a2b3c4d5e01",
                "7c9d2e4f-1b22-4d66-8e2f-2b3c4d5e6f02",
                "a1b2c3d4-2c33-4e77-9f30-3c4d5e6f7a03",
                "d4e5f6a7-3d44-4f88-a041-4d5e6f7a8b04",
                "09f8e7d6-4e55-4099-b152-5e6f7a8b9c05"
            }.Select(TerritoryDetectionService.OwnerColor).ToList();

            Assert.Equal(colours.Count, colours.Distinct().Count());
        }

        [Fact]
        public void PrivateOwner_ShouldBeShownAsAnonymous()
        {
            var owner = new ApplicationUser
            {
                Id = "owner", DisplayName = "Kumail", IsProfilePublic = false
            };

            Assert.Equal(TerritoryDetectionService.AnonymousOwnerLabel,
                TerritoryDetectionService.OwnerLabel(owner, "someone-else"));
        }

        [Fact]
        public void PublicOwner_ShouldShowDisplayName()
        {
            var owner = new ApplicationUser
            {
                Id = "owner", DisplayName = "Kumail", IsProfilePublic = true
            };

            Assert.Equal("Kumail",
                TerritoryDetectionService.OwnerLabel(owner, "someone-else"));
        }

        // Runners always see their own name on their own land.
        [Fact]
        public void PrivateOwnerViewingOwnTerritory_ShouldSeeOwnName()
        {
            var owner = new ApplicationUser
            {
                Id = "owner", DisplayName = "Kumail", IsProfilePublic = false
            };

            Assert.Equal("Kumail",
                TerritoryDetectionService.OwnerLabel(owner, "owner"));
        }

        // =====================================================================
        // Feature 1: inactivity job (FR-19). Active territories whose 3-day
        // shield has ended become Vulnerable.
        // =====================================================================

        [Fact]
        public void ActiveTerritoryWithEndedShield_ShouldBecomeVulnerable()
        {
            var expired = Owned(Rival, Rect(0, 0, 100, 100), "Active",
                Now.AddMinutes(-1));

            var result = Service().FindTerritoriesToMarkVulnerable(
                new[] { expired }, Now);

            Assert.Same(expired, Assert.Single(result));
        }

        [Fact]
        public void ActiveTerritoryInsideShield_ShouldStayActive()
        {
            var shielded = Owned(Rival, Rect(0, 0, 100, 100), "Active",
                Now.AddDays(1));

            Assert.Empty(Service().FindTerritoriesToMarkVulnerable(
                new[] { shielded }, Now));
        }

        [Fact]
        public void AlreadyVulnerableTerritory_ShouldNotBeMarkedAgain()
        {
            var vulnerable = Owned(Rival, Rect(0, 0, 100, 100), "Vulnerable",
                Now.AddDays(-2));

            Assert.Empty(Service().FindTerritoriesToMarkVulnerable(
                new[] { vulnerable }, Now));
        }

        [Fact]
        public void TakenOverTerritory_ShouldBeIgnored()
        {
            var takenOver = Owned(Rival, Rect(0, 0, 100, 100), "TakenOver",
                Now.AddDays(-2));

            Assert.Empty(Service().FindTerritoriesToMarkVulnerable(
                new[] { takenOver }, Now));
        }

        // Territories saved before shields existed have no shield date:
        // they count as shielded for 3 days after their last update.
        [Fact]
        public void OldTerritoryWithoutShieldDate_ShouldBecomeVulnerableAfter3Days()
        {
            var legacy = new Territory
            {
                OwnerUserId = Rival,
                Boundary = Rect(0, 0, 100, 100),
                Status = "Active",
                ShieldExpiresAt = null,
                UpdatedAt = Now.AddDays(-4)
            };

            Assert.Single(Service().FindTerritoriesToMarkVulnerable(
                new[] { legacy }, Now));
        }

        // =====================================================================
        // Feature 1: GPS gaps (FR-11). A stop without movement keeps the route
        // joined however long it lasts; a straight jump of more than 150 m
        // with no points in between is missing data and breaks the route.
        // =====================================================================

        // Like a namaz stop: 5 minutes standing still at a corner.
        [Fact]
        public void LongStopWithoutMoving_ShouldStillClaimLoop()
        {
            var points = new Route()
                .To(60, 0, WalkSpeed)
                .To(60, 60, WalkSpeed)
                .Wait(300)
                .To(0, 60, WalkSpeed)
                .To(0, 0, WalkSpeed)
                .Build();

            Assert.Single(Service().FindTerritories(points));
        }

        // A whole 200 m side has no GPS points (jump in 40 s, 5 m/s).
        [Fact]
        public void MissingGpsOver150m_ShouldNotBeBridged()
        {
            var points = new Route()
                .To(200, 0, WalkSpeed)
                .JumpTo(200, 200, 40)
                .To(0, 200, WalkSpeed)
                .To(0, 0, WalkSpeed)
                .Build();

            Assert.Empty(Service().FindTerritories(points));
        }

        // Guard: a short 40 m dropout is still bridged.
        // FR-11: GPS lost for 10 minutes while the runner moved 100 m. The
        // straight line across the gap was never run, so it must not close
        // a territory (it did before: any jump up to 150 m was joined).
        [Fact]
        public void LongGpsGapWithMovement_ShouldNotInventTheMissingSide()
        {
            var points = new Route()
                .To(100, 0, WalkSpeed)
                .To(100, 100, WalkSpeed)
                .JumpTo(0, 100, 600)
                .To(0, 0, WalkSpeed)
                .Build();

            Assert.Empty(Service().FindTerritories(points));
        }

        // Standing still for 10 minutes: no fixes arrive (5 m distance
        // filter) and the next fix wobbles a few metres. Still one loop.
        [Fact]
        public void LongStopWithGpsWobble_ShouldStillClaimLoop()
        {
            var points = new Route()
                .To(60, 0, WalkSpeed)
                .To(60, 60, WalkSpeed)
                .JumpTo(63, 64, 600)
                .To(0, 60, WalkSpeed)
                .To(0, 0, WalkSpeed)
                .Build();

            Assert.Single(Service().FindTerritories(points));
        }

        [Fact]
        public void ShortGpsDropout_ShouldStillClaimLoop()
        {
            var points = new Route()
                .To(60, 0, WalkSpeed)
                .JumpTo(60, 40, 20)
                .To(60, 60, WalkSpeed)
                .To(0, 60, WalkSpeed)
                .To(0, 0, WalkSpeed)
                .Build();

            Assert.Single(Service().FindTerritories(points));
        }

        // =====================================================================
        // Feature 1: road snapping for poor GPS only (FR-12). Points with
        // accuracy worse than 15 m (and not worse than 30 m) are sent to
        // Google Roads with one good neighbour each side. A snapped point is
        // used only if it lies inside that point's accuracy circle.
        // =====================================================================

        [Fact]
        public void AccurateRun_ShouldNeedNoSnapping()
        {
            Assert.Empty(Service().BuildSnapStretches(Line(10)));
        }

        [Fact]
        public void OneBadPoint_ShouldBeSentWithItsNeighbours()
        {
            var points = Line(10);
            points[4].AccuracyMeters = 20;   // sequence 5

            var stretch = Assert.Single(Service().BuildSnapStretches(points));

            Assert.Equal(new[] { 4, 5, 6 },
                stretch.Select(p => p.SequenceNo).ToArray());
        }

        [Fact]
        public void NeighbouringBadPoints_ShouldShareOneStretch()
        {
            var points = Line(10);
            points[4].AccuracyMeters = 20;   // sequence 5
            points[5].AccuracyMeters = 25;   // sequence 6

            var stretch = Assert.Single(Service().BuildSnapStretches(points));

            Assert.Equal(new[] { 4, 5, 6, 7 },
                stretch.Select(p => p.SequenceNo).ToArray());
        }

        // Points worse than 30 m are dropped anyway, so never sent.
        [Fact]
        public void VeryInaccuratePoint_ShouldNotBeSnapped()
        {
            var points = Line(10);
            points[4].AccuracyMeters = 40;

            Assert.Empty(Service().BuildSnapStretches(points));
        }

        [Fact]
        public void LongBadStretch_ShouldBeSplitIntoRequestsOf100()
        {
            var points = Line(250);
            foreach (var point in points)
                point.AccuracyMeters = 20;

            var stretches = Service().BuildSnapStretches(points);

            Assert.All(stretches, stretch =>
                Assert.InRange(stretch.Count, 1,
                    TerritoryDetectionService.MaxSnapPointsPerRequest));
            Assert.Equal(250, stretches
                .SelectMany(stretch => stretch)
                .Select(p => p.SequenceNo)
                .Distinct()
                .Count());
        }

        [Fact]
        public void SnappedPointInsideAccuracyCircle_ShouldReplaceBadPoint()
        {
            var stretch = Line(3);
            stretch[1].AccuracyMeters = 20;

            var result = Service().ApplySnappedPoints(stretch, new[]
            {
                Snapped(1, stretch[1], northMeters: 10)
            });

            Assert.Equal(stretch[1].Latitude + 10 / MetersPerDegree,
                result[1].Latitude, 9);
        }

        [Fact]
        public void SnappedPointOutsideAccuracyCircle_ShouldBeIgnored()
        {
            var stretch = Line(3);
            stretch[1].AccuracyMeters = 20;
            double original = stretch[1].Latitude;

            var result = Service().ApplySnappedPoints(stretch, new[]
            {
                Snapped(1, stretch[1], northMeters: 40)
            });

            Assert.Equal(original, result[1].Latitude);
        }

        // Good points keep their real (possibly off-road) position.
        [Fact]
        public void GoodPoint_ShouldNeverBeMovedBySnapping()
        {
            var stretch = Line(3);
            stretch[1].AccuracyMeters = 20;
            double original = stretch[0].Latitude;

            var result = Service().ApplySnappedPoints(stretch, new[]
            {
                Snapped(0, stretch[0], northMeters: 3)
            });

            Assert.Equal(original, result[0].Latitude);
        }

        // ----- Snapping helpers -----

        // Points 10 m apart going east, 5 m accuracy, 5 s apart.
        private static List<RunGpsPoint> Line(int count)
        {
            var route = new Route();
            for (int i = 1; i < count; i++)
                route.JumpTo(i * 10, 0, 5);
            return route.Build();
        }

        private static SnappedRoadPoint Snapped(
            int originalIndex, RunGpsPoint point, double northMeters)
        {
            return new SnappedRoadPoint
            {
                OriginalIndex = originalIndex,
                Latitude = point.Latitude + northMeters / MetersPerDegree,
                Longitude = point.Longitude
            };
        }

        // =====================================================================
        // Feature 1: clean route on the map and stored road-snapped positions.
        // The map draws the cleaned path (bad points and GPS teleports
        // removed, snapped positions used). Territory checks also use the
        // snapped position when one is stored.
        // =====================================================================

        [Fact]
        public void DisplayRoute_ShouldDropGpsTeleport()
        {
            var points = Line(10);
            points[4].Latitude += 700 / MetersPerDegree;   // 700 m jump

            var line = Assert.Single(Service().BuildDisplayRoute(points));

            Assert.Equal(9, line.Count);
            Assert.DoesNotContain(line, p => p.SequenceNo == 5);
        }

        [Fact]
        public void DisplayRoute_ShouldDropInaccuratePoint()
        {
            var points = Line(10);
            points[4].AccuracyMeters = 40;

            var line = Assert.Single(Service().BuildDisplayRoute(points));

            Assert.DoesNotContain(line, p => p.SequenceNo == 5);
        }

        [Fact]
        public void DisplayRoute_ShouldUseSnappedPosition()
        {
            var points = Line(10);
            points[4].AccuracyMeters = 20;
            SnapTo(points[4], 40, 5);

            var line = Assert.Single(Service().BuildDisplayRoute(points));
            var shown = line.Single(p => p.SequenceNo == 5);

            Assert.Equal(points[4].SnappedLatitude, shown.Latitude);
            Assert.Equal(points[4].SnappedLongitude, shown.Longitude);
        }

        [Fact]
        public void DisplayRoute_ShouldSplitWhereGpsIsMissing()
        {
            var points = new Route()
                .To(50, 0, WalkSpeed)
                .JumpTo(250, 0, 100)                       // 200 m, no points
                .To(300, 0, WalkSpeed)
                .Build();

            Assert.Equal(2, Service().BuildDisplayRoute(points).Count);
        }

        // The raw end is 25 m from the start (not closed), but its snapped
        // position is 8 m away, so the loop closes and is claimed.
        [Fact]
        public void SnappedEndPoint_ShouldBeUsedForTerritory()
        {
            var points = new Route()
                .To(60, 0, WalkSpeed)
                .To(60, 60, WalkSpeed)
                .To(0, 60, WalkSpeed)
                .To(0, 30, WalkSpeed)
                .JumpTo(0, 25, 10)                         // last fix, 20 m accuracy
                .Build();

            points[^1].AccuracyMeters = 20;
            SnapTo(points[^1], 0, 8);

            Assert.Single(Service().FindTerritories(points));
        }

        // Store a snapped position x metres east, y metres north of origin.
        private static void SnapTo(RunGpsPoint point, double x, double y)
        {
            point.SnappedLatitude = OriginLatitude + y / MetersPerDegree;
            point.SnappedLongitude =
                OriginLongitude + x / MetersPerDegreeLongitude;
        }

        // =====================================================================
        // Feature 1: real-world situations (review fixes).
        // =====================================================================

        // Two different areas can share a GPS range (e.g. both sides of the
        // closing line). Each must be recorded; neither may block the run.
        [Fact]
        public void SameRangeDifferentAreas_ShouldBothBePlanned()
        {
            var analysis = new TerritoryAnalysis();
            analysis.Accepted.Add(Candidate(1, 40, 3600, null));
            analysis.Accepted.Add(Candidate(1, 40, 2500, null));

            Assert.Equal(2,
                Service().PlanNewClaims(analysis, NothingSubmitted).Count);
        }

        [Fact]
        public void AlreadySubmittedRangeWithOtherArea_ShouldStillBePlanned()
        {
            var analysis = new TerritoryAnalysis();
            analysis.Accepted.Add(Candidate(1, 40, 2500, null));

            var claim = Assert.Single(Service().PlanNewClaims(
                analysis, new[] { (1, 40, 3600.0) }));

            Assert.Equal(2500, claim.Candidate.AreaSquareMeters);
        }

        // A loop just around your own land leaves only a thin 5 m ring.
        [Fact]
        public void LoopAroundOwnLand_ThinRing_ShouldNotBeAwarded()
        {
            var mine = Owned(Me, Rect(5, 5, 95, 95), "Active", Now.AddDays(2));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { mine }, Now);

            Assert.Equal(TerritoryRejectionReasons.AlreadyOwned,
                result.RejectionReason);
            Assert.Empty(result.AwardedPieces);
        }

        // A loop just around a rival's shielded land must not wrap it.
        [Fact]
        public void LoopAroundShieldedLand_ThinRing_ShouldBeRejected()
        {
            var shielded = Owned(Rival, Rect(5, 5, 95, 95), "Active",
                Now.AddDays(2));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { shielded }, Now);

            Assert.Equal(TerritoryRejectionReasons.Shielded,
                result.RejectionReason);
            Assert.Empty(result.AwardedPieces);
        }

        // Real GPS drifts a few metres. A 100 m block walked with ±4 m drift
        // is still one territory of about the right size.
        [Fact]
        public void WalkingLoopWithGpsDrift_ShouldClaimOneTerritory()
        {
            var points = WithGpsDrift(new Route()
                .To(100, 0, WalkSpeed)
                .To(100, 100, WalkSpeed)
                .To(0, 100, WalkSpeed)
                .To(0, 0, WalkSpeed)
                .Build(), driftMeters: 4, phase: 0);

            var service = Service();
            var territory = Assert.Single(service.FindTerritories(points));

            Assert.InRange(service.CalculateTerritoryArea(territory), 8500, 11500);
        }

        // Two laps of the same block with different drift on each lap must
        // never claim more than the block itself.
        [Fact]
        public void TwoLapsOfSameBlock_ShouldNotClaimMoreThanTheBlock()
        {
            var route = new Route();
            for (int lap = 0; lap < 2; lap++)
            {
                route.To(100, 0, WalkSpeed)
                    .To(100, 100, WalkSpeed)
                    .To(0, 100, WalkSpeed)
                    .To(0, 0, WalkSpeed);
            }

            var service = Service();
            var territories = service.FindTerritories(
                WithGpsDrift(route.Build(), driftMeters: 4, phase: 1.3));

            Assert.NotEmpty(territories);
            Assert.InRange(
                territories.Sum(t => service.CalculateTerritoryArea(t)),
                8000, 11500);
        }

        // ----- Map visibility (privacy, FR-21) -----

        [Fact]
        public void OwnTerritoryDuringRun_ShouldBeVisibleToOwner()
        {
            Assert.True(TerritoryDetectionService.IsVisibleOnMap(
                Me, "Running", Me));
        }

        [Fact]
        public void OtherRunnersTerritoryDuringTheirRun_ShouldBeHidden()
        {
            Assert.False(TerritoryDetectionService.IsVisibleOnMap(
                Rival, "Running", Me));
        }

        [Fact]
        public void OtherRunnersTerritoryAfterTheirRun_ShouldBeVisible()
        {
            Assert.True(TerritoryDetectionService.IsVisibleOnMap(
                Rival, "Completed", Me));
        }

        // Split leftovers have no run of their own.
        [Fact]
        public void TerritoryWithoutRun_ShouldBeVisible()
        {
            Assert.True(TerritoryDetectionService.IsVisibleOnMap(
                Rival, null, Me));
        }

        // ----- Run validation status at Finish (UC-02, UC-06) -----

        [Fact]
        public void FinishedRunWithApprovedClaim_ShouldBeValidated()
        {
            Assert.Equal(RunValidationStatuses.Validated,
                TerritoryDetectionService.DecideValidationStatus(
                    processingSucceeded: true, approvedClaimCount: 1));
        }

        [Fact]
        public void FinishedRunWithoutApprovedClaim_ShouldBeNoClaim()
        {
            Assert.Equal(RunValidationStatuses.NoClaim,
                TerritoryDetectionService.DecideValidationStatus(
                    processingSucceeded: true, approvedClaimCount: 0));
        }

        // Land won during live uploads does not hide a failed Finish.
        [Fact]
        public void FailedTerritoryProcessing_ShouldBeFailedEvenWithEarlierClaims()
        {
            Assert.Equal(RunValidationStatuses.Failed,
                TerritoryDetectionService.DecideValidationStatus(
                    processingSucceeded: false, approvedClaimCount: 2));
        }

        // The ERD stores ValidationStatus as varchar(20).
        [Fact]
        public void ValidationStatusValues_ShouldFitTheErdColumn()
        {
            Assert.All(new[]
            {
                RunValidationStatuses.Pending, RunValidationStatuses.Validated,
                RunValidationStatuses.NoClaim, RunValidationStatuses.Failed
            }, value => Assert.InRange(value.Length, 1, 20));
        }

        // ----- Recovery of failed territory processing (UC-05/UC-07) -----

        private static readonly DateTime RunEnd =
            new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void FailedRunInsideRetryWindow_ShouldBeRetried()
        {
            Assert.True(TerritoryDetectionService.IsAutoRetryDue(
                "Completed", RunValidationStatuses.Failed, RunEnd,
                RunEnd.AddDays(2), retryWindowDays: 3));
        }

        [Fact]
        public void FailedRunOlderThanRetryWindow_ShouldNotBeRetriedAutomatically()
        {
            Assert.False(TerritoryDetectionService.IsAutoRetryDue(
                "Completed", RunValidationStatuses.Failed, RunEnd,
                RunEnd.AddDays(4), retryWindowDays: 3));
        }

        [Fact]
        public void ProcessedRun_ShouldNeverBeRetried()
        {
            Assert.False(TerritoryDetectionService.IsAutoRetryDue(
                "Completed", RunValidationStatuses.Validated, RunEnd,
                RunEnd.AddHours(1), retryWindowDays: 3));
            Assert.False(TerritoryDetectionService.IsAutoRetryDue(
                "Running", RunValidationStatuses.Pending, RunEnd,
                RunEnd.AddHours(1), retryWindowDays: 3));
        }

        // ----- Abandoned runs (L3): no GPS for 6 hours -----

        private static readonly DateTime AbandonStart =
            new(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void RunWithNoGpsForSixHours_IsAbandoned()
        {
            var lastFix = AbandonStart.AddMinutes(40);
            Assert.True(TerritoryDetectionService.IsRunAbandoned(
                "Running", AbandonStart, lastFix, lastFix.AddHours(6),
                TimeSpan.FromHours(6)));
            Assert.True(TerritoryDetectionService.IsRunAbandoned(
                "Paused", AbandonStart, lastFix, lastFix.AddHours(7),
                TimeSpan.FromHours(6)));
        }

        [Fact]
        public void RunWithRecentGps_IsNotAbandoned()
        {
            var lastFix = AbandonStart.AddMinutes(40);
            Assert.False(TerritoryDetectionService.IsRunAbandoned(
                "Running", AbandonStart, lastFix, lastFix.AddHours(5.9),
                TimeSpan.FromHours(6)));
        }

        [Fact]
        public void RunWithoutAnyGps_CountsFromItsStart()
        {
            Assert.False(TerritoryDetectionService.IsRunAbandoned(
                "Running", AbandonStart, null, AbandonStart.AddHours(5),
                TimeSpan.FromHours(6)));
            Assert.True(TerritoryDetectionService.IsRunAbandoned(
                "Running", AbandonStart, null, AbandonStart.AddHours(6),
                TimeSpan.FromHours(6)));
        }

        [Fact]
        public void FinishedRun_IsNeverAbandoned()
        {
            Assert.False(TerritoryDetectionService.IsRunAbandoned(
                "Completed", AbandonStart, AbandonStart, AbandonStart.AddDays(2),
                TimeSpan.FromHours(6)));
        }

        // The abandoned run's running time stops at its last GPS fix, not
        // at the moment the server notices it six hours later.
        [Fact]
        public void AbandonedRunningRun_CountsTimeUntilLastFixOnly()
        {
            var resumed = AbandonStart.AddMinutes(10);
            Assert.Equal(600 + 1800, TerritoryDetectionService.ActiveSecondsAt(
                "Running", 600, resumed, resumed.AddMinutes(30)));
        }

        [Fact]
        public void AbandonedPausedRun_KeepsItsSavedTime()
        {
            Assert.Equal(900, TerritoryDetectionService.ActiveSecondsAt(
                "Paused", 900, null, AbandonStart.AddHours(1)));
            // Resumed after the last fix: nothing was recorded after that.
            Assert.Equal(900, TerritoryDetectionService.ActiveSecondsAt(
                "Running", 900, AbandonStart.AddHours(2), AbandonStart.AddHours(1)));
        }

        // A retry renews shields from the run's own end, not from the
        // moment the retry happens.
        [Fact]
        public void RetriedShieldRenewal_ShouldCountFromRunEnd()
        {
            var expiry = TerritoryDetectionService.RenewedShieldExpiry(
                existing: null, activityTime: RunEnd, now: RunEnd.AddDays(1));

            Assert.Equal(RunEnd.AddDays(3), expiry);
        }

        [Fact]
        public void ShieldRenewal_ShouldNeverShortenALongerShield()
        {
            var longer = RunEnd.AddDays(5);

            Assert.Equal(longer, TerritoryDetectionService.RenewedShieldExpiry(
                existing: longer, activityTime: RunEnd, now: RunEnd.AddHours(1)));
        }

        // Activity more than 3 days ago no longer protects anything.
        [Fact]
        public void ShieldRenewalFromAnOldRun_ShouldNotRenew()
        {
            Assert.Null(TerritoryDetectionService.RenewedShieldExpiry(
                existing: null, activityTime: RunEnd, now: RunEnd.AddDays(4)));
        }

        // ----- Performance (NFR-08: validation within 5 seconds) -----

        // A 5,000-point walk round a street grid with smooth GPS wobble
        // (thousands of tiny faces). Measured 8 Nov: ~2 s before indexing
        // the route points, ~0.45 s after (Release build).
        private static List<RunGpsPoint> LongGridWalk(int count, double wobble)
        {
            var random = new Random(7);
            var points = new List<RunGpsPoint>();
            double x = 0, y = 0, dx = 0, dy = 0, legMeters = 0;
            int direction = 0;
            var time = new DateTime(2026, 11, 6, 6, 0, 0, DateTimeKind.Utc);

            for (int i = 0; i < count; i++)
            {
                legMeters += 5;
                if (legMeters >= 200)
                {
                    legMeters = 0;
                    direction = (direction + (random.Next(4) == 0 ? 3 : 1)) % 4;
                }
                x += direction == 0 ? 5 : direction == 2 ? -5 : 0;
                y += direction == 1 ? 5 : direction == 3 ? -5 : 0;
                dx = dx * 0.8 + (random.NextDouble() - 0.5) * wobble;
                dy = dy * 0.8 + (random.NextDouble() - 0.5) * wobble;
                time = time.AddSeconds(2);

                points.Add(new RunGpsPoint
                {
                    SequenceNo = i + 1,
                    LegNo = 1,
                    Latitude = OriginLatitude + (y + dy) / MetersPerDegree,
                    Longitude = OriginLongitude + (x + dx) / MetersPerDegreeLongitude,
                    AccuracyMeters = 5 + random.NextDouble() * 10,
                    RecordedAt = time
                });
            }

            return points;
        }

        [Fact]
        public void FiveThousandPointRun_ShouldBeAnalysedWellInsideFiveSeconds()
        {
            var points = LongGridWalk(5000, wobble: 3);
            var service = Service();
            service.AnalyzeRun(LongGridWalk(200, wobble: 3));

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var analysis = service.AnalyzeRun(points);
            watch.Stop();

            Assert.NotEmpty(analysis.Accepted);
            Assert.True(watch.ElapsedMilliseconds < 2500,
                $"Analysis took {watch.ElapsedMilliseconds} ms.");
        }

        // ----- Profile visibility default (Muneeb's decision, 8 Nov) -----

        // New accounts are public: other runners see their name on land.
        // Runners can still switch to private in their profile.
        [Fact]
        public void NewAccount_ShouldBePublicByDefault()
        {
            Assert.True(new ApplicationUser().IsProfilePublic);
        }

        // ----- Per-user GPS upload limit (NFR-05) -----

        private static System.Threading.RateLimiting.RateLimiter UploadLimiterFor(
            string? userId, int perMinute)
        {
            var context = new DefaultHttpContext();
            if (userId != null)
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "test"));

            var partition = GpsUploadRateLimit.Partition(context, perMinute);
            return partition.Factory(partition.PartitionKey);
        }

        [Fact]
        public void GpsUploads_ShouldBeLimitedPerUserPerMinute()
        {
            using var limiter = UploadLimiterFor(Me, perMinute: 20);

            for (int i = 0; i < 20; i++)
                Assert.True(limiter.AttemptAcquire().IsAcquired);

            Assert.False(limiter.AttemptAcquire().IsAcquired);
        }

        [Fact]
        public void GpsUploadLimit_ShouldBeSeparateForEachUser()
        {
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, Me) }, "test"));
            var other = new DefaultHttpContext();
            other.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, Rival) }, "test"));

            Assert.NotEqual(
                GpsUploadRateLimit.Partition(context, 20).PartitionKey,
                GpsUploadRateLimit.Partition(other, 20).PartitionKey);
        }

        // ----- Finish timing: endedAt and active time (UC-02) -----

        private static readonly DateTime Started =
            new(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);

        // Two legs: 08:00-08:10 and 08:20-08:30 (a 10 minute pause).
        private static List<RunGpsPoint> TwoLegs() => new()
        {
            Fix(1, 1, Started),
            Fix(2, 1, Started.AddMinutes(10)),
            Fix(3, 2, Started.AddMinutes(20)),
            Fix(4, 2, Started.AddMinutes(30))
        };

        private static RunGpsPoint Fix(int sequence, int leg, DateTime at) => new()
        {
            SequenceNo = sequence, LegNo = leg, RecordedAt = at,
            Latitude = OriginLatitude, Longitude = OriginLongitude,
            AccuracyMeters = 5
        };

        // Finish pressed offline at 08:31, synced at 11:00.
        [Fact]
        public void OfflineFinish_ShouldUseThePhonesEndTimeAndActiveTime()
        {
            var timing = TerritoryDetectionService.ResolveFinishTiming(
                Started, serverNow: Started.AddHours(3), serverActiveSeconds: 10800,
                clientEndedAt: Started.AddMinutes(31), clientActiveSeconds: 1230,
                TwoLegs());

            Assert.Equal(Started.AddMinutes(31), timing.EndedAt);
            Assert.Equal(1230, timing.ActiveSeconds);
        }

        // Old app without a body: the server time stays, but the 10 minute
        // pause seen between the legs is not counted as running.
        [Fact]
        public void FinishWithoutClientTiming_ShouldNotCountThePauseBetweenLegs()
        {
            var timing = TerritoryDetectionService.ResolveFinishTiming(
                Started, serverNow: Started.AddMinutes(31), serverActiveSeconds: 1860,
                clientEndedAt: null, clientActiveSeconds: null, TwoLegs());

            Assert.Equal(Started.AddMinutes(31), timing.EndedAt);
            Assert.Equal(1860 - 600 + 60, timing.ActiveSeconds);
        }

        [Fact]
        public void ActiveTimeLongerThanTheRun_ShouldBeCapped()
        {
            var timing = TerritoryDetectionService.ResolveFinishTiming(
                Started, Started.AddMinutes(40), 0,
                Started.AddMinutes(31), clientActiveSeconds: 99999, TwoLegs());

            Assert.Equal(1860 - 600 + 60, timing.ActiveSeconds);
        }

        // 20 minutes of recorded running cannot be reported as 5 minutes.
        [Fact]
        public void ActiveTimeShorterThanRecordedRunning_ShouldBeRaised()
        {
            var timing = TerritoryDetectionService.ResolveFinishTiming(
                Started, Started.AddMinutes(40), 0,
                Started.AddMinutes(31), clientActiveSeconds: 300, TwoLegs());

            Assert.Equal(1200, timing.ActiveSeconds);
        }

        [Fact]
        public void EndTimeBeforeTheLastGpsPoint_ShouldMoveToThatPoint()
        {
            var timing = TerritoryDetectionService.ResolveFinishTiming(
                Started, Started.AddMinutes(40), 0,
                Started.AddMinutes(25), null, TwoLegs());

            Assert.Equal(Started.AddMinutes(30), timing.EndedAt);
        }

        [Fact]
        public void EndTimeInTheFutureOrNotUtc_ShouldFallBackToServerTime()
        {
            var now = Started.AddMinutes(40);

            Assert.Equal(now, TerritoryDetectionService.ResolveFinishTiming(
                Started, now, 0, now.AddMinutes(10), null, TwoLegs()).EndedAt);
            Assert.Equal(now, TerritoryDetectionService.ResolveFinishTiming(
                Started, now, 0, DateTime.SpecifyKind(
                    Started.AddMinutes(31), DateTimeKind.Local), null, TwoLegs()).EndedAt);
            Assert.Equal(now, TerritoryDetectionService.ResolveFinishTiming(
                Started, now, 0, Started.AddMinutes(-5), null,
                new List<RunGpsPoint>()).EndedAt);
        }

        // ----- Merging adjacent land of one owner (UC-07 alternate) -----

        [Fact]
        public void AdjacentOwnLand_SharingAnEdge_ShouldMergeIntoOnePolygon()
        {
            var merged = Service().MergedBoundary(
                Rect(0, 0, 100, 100), Rect(100, 0, 200, 100));

            Assert.NotNull(merged);
            Assert.True(merged!.IsValid);
            Assert.InRange(AreaOf(merged), 19800, 20200);
        }

        [Fact]
        public void LandTouchingOnlyAtACorner_ShouldNotMerge()
        {
            Assert.Null(Service().MergedBoundary(
                Rect(0, 0, 100, 100), Rect(100, 100, 200, 200)));
        }

        // A shared edge shorter than the minimum territory width (16 m) is
        // not a meaningful border.
        [Fact]
        public void LandSharingOnlyAShortEdge_ShouldNotMerge()
        {
            Assert.Null(Service().MergedBoundary(
                Rect(0, 0, 100, 100), Rect(100, 90, 200, 190)));
        }

        [Fact]
        public void LandSharingALongEnoughEdge_ShouldMerge()
        {
            Assert.NotNull(Service().MergedBoundary(
                Rect(0, 0, 100, 100), Rect(100, 80, 200, 180)));
        }

        [Fact]
        public void SeparateLand_ShouldNotMerge()
        {
            Assert.Null(Service().MergedBoundary(
                Rect(0, 0, 100, 100), Rect(130, 0, 230, 100)));
        }

        // Merging never changes protection: only land with the same owner,
        // status and shield end can merge.
        [Fact]
        public void OnlyLandWithTheSameOwnerStatusAndShield_ShouldMerge()
        {
            var shield = Now.AddDays(3);
            var a = Owned(Me, Rect(0, 0, 100, 100), "Active", shield);

            Assert.True(TerritoryDetectionService.CanMerge(
                a, Owned(Me, Rect(100, 0, 200, 100), "Active", shield)));
            Assert.False(TerritoryDetectionService.CanMerge(
                a, Owned(Me, Rect(100, 0, 200, 100), "Active", shield.AddDays(-1))));
            Assert.False(TerritoryDetectionService.CanMerge(
                a, Owned(Me, Rect(100, 0, 200, 100), "Vulnerable", shield)));
            Assert.False(TerritoryDetectionService.CanMerge(
                a, Owned(Rival, Rect(100, 0, 200, 100), "Active", shield)));
        }

        // ----- Final polygon checks before saving (SRS 7.3) -----

        private static Polygon Ring(params (double x, double y)[] corners)
        {
            Coordinate At(double x, double y) => new(
                OriginLongitude + x / MetersPerDegreeLongitude,
                OriginLatitude + y / MetersPerDegree);

            var factory = new GeometryFactory(new PrecisionModel(), 4326);
            return factory.CreatePolygon(corners
                .Append(corners[0])
                .Select(c => At(c.x, c.y))
                .ToArray());
        }

        [Fact]
        public void ValidPiece_ShouldBeStoredUnchanged()
        {
            var square = Rect(0, 0, 100, 100);

            var piece = Assert.Single(Service().StorablePieces(square));

            Assert.True(piece.EqualsExact(square));
        }

        [Fact]
        public void EmptyGeometry_ShouldGiveNoPieces()
        {
            var empty = new GeometryFactory(new PrecisionModel(), 4326)
                .CreatePolygon();

            Assert.Empty(Service().StorablePieces(empty));
        }

        // A self-touching "bow tie" is invalid; its two real lobes are kept
        // rather than silently dropping the land.
        [Fact]
        public void InvalidBowTie_ShouldBeRepairedIntoValidPieces()
        {
            var bowTie = Ring((0, 0), (100, 100), (100, 0), (0, 100));
            Assert.False(bowTie.IsValid);

            var pieces = Service().StorablePieces(bowTie);

            Assert.Equal(2, pieces.Count);
            Assert.All(pieces, piece =>
            {
                Assert.True(piece.IsValid);
                Assert.Equal(4326, piece.SRID);
                Assert.InRange(AreaOf(piece), 2400, 2600);
            });
        }

        // Overlaps can leave slivers; only real land survives.
        [Fact]
        public void FragmentedGeometry_ShouldKeepOnlyOwnablePieces()
        {
            var factory = new GeometryFactory(new PrecisionModel(), 4326);
            var fragments = factory.CreateMultiPolygon(new[]
            {
                Rect(0, 0, 100, 100),
                Rect(200, 0, 300, 3)
            });

            var piece = Assert.Single(Service().StorablePieces(fragments));

            Assert.InRange(AreaOf(piece), 9900, 10100);
        }

        // Every awarded and remaining piece is valid and in SRID 4326.
        [Fact]
        public void TakeoverPieces_ShouldAllBeStorable()
        {
            var vulnerable = Owned(Rival, Rect(-50, 30, 150, 70), "Vulnerable",
                Now.AddDays(-1));

            var result = Service().ResolveClaim(
                Rect(0, 0, 100, 100), Me, new List<Territory> { vulnerable }, Now);

            var all = result.AwardedPieces
                .Concat(result.Takeovers.SelectMany(t => t.RemainingPieces));
            Assert.All(all, piece =>
            {
                Assert.True(piece.IsValid);
                Assert.False(piece.IsEmpty);
                Assert.Equal(4326, piece.SRID);
            });
        }

        // ----- Official run distance (Runs.TotalDistanceMeters) -----

        // One impossible 60 m spike in a 300 m walk is not counted.
        [Fact]
        public void GpsTeleport_ShouldNotAddToRunDistance()
        {
            var points = new Route()
                .To(150, 0, 2.5)
                .JumpTo(150, 60, 1)
                .JumpTo(150, 0, 1)
                .To(300, 0, 2.5)
                .Build();

            var analysis = Service().AnalyzeRun(points);

            Assert.InRange(analysis.GenuineDistanceMeters, 295, 305);
        }

        // ----- Google Roads failures (UC-05 exception, FR-12) -----

        // Answers every request with the same status and body, counting calls.
        private sealed class FakeRoads : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _body;
            public int Calls;

            public FakeRoads(HttpStatusCode status, string body = "{}")
            {
                _status = status;
                _body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(new HttpResponseMessage(_status)
                {
                    Content = new StringContent(_body)
                });
            }
        }

        private static GoogleRoadsService Roads(
            HttpMessageHandler handler, int cooldownSeconds = 0)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GoogleRoads:ApiKey"] = "test-key",
                    ["GoogleRoads:FailureCooldownSeconds"] =
                        cooldownSeconds.ToString()
                })
                .Build();

            return new GoogleRoadsService(new HttpClient(handler),
                configuration, NullLogger<GoogleRoadsService>.Instance);
        }

        [Fact]
        public async Task RoadsServerError_ShouldBeReportedAsFailureNotAsNoSnap()
        {
            var result = await Roads(new FakeRoads(HttpStatusCode.InternalServerError))
                .SnapAsync(Line(3));

            Assert.Null(result);
        }

        // A request Google rejects for good (e.g. 400 for one bad stretch)
        // must not be retried forever nor pause snapping for everyone.
        [Fact]
        public async Task RoadsPermanentRejection_ShouldCountAsCheckedWithoutPausing()
        {
            var rejected = await Roads(new FakeRoads(HttpStatusCode.BadRequest), 60)
                .SnapAsync(Line(3));

            Assert.NotNull(rejected);
            Assert.Empty(rejected!);

            var healthy = new FakeRoads(HttpStatusCode.OK, "{}");
            await Roads(healthy, cooldownSeconds: 60).SnapAsync(Line(3));
            Assert.Equal(1, healthy.Calls);
        }

        [Fact]
        public async Task RoadsAnswerWithoutSnappedPoints_ShouldBeASuccessWithNoPoints()
        {
            var result = await Roads(new FakeRoads(HttpStatusCode.OK, "{}"))
                .SnapAsync(Line(3));

            Assert.NotNull(result);
            Assert.Empty(result!);
        }

        // During an outage Google is not called again for every upload.
        [Fact]
        public async Task RoadsFailure_ShouldPauseFurtherCallsDuringCooldown()
        {
            var failing = new FakeRoads(HttpStatusCode.ServiceUnavailable);
            await Roads(failing, cooldownSeconds: 60).SnapAsync(Line(3));

            var healthy = new FakeRoads(HttpStatusCode.OK, "{}");
            var second = await Roads(healthy, cooldownSeconds: 60).SnapAsync(Line(3));

            Assert.Null(second);
            Assert.Equal(0, healthy.Calls);

            // A successful call clears the pause for everyone.
            await Roads(new FakeRoads(HttpStatusCode.OK, "{}"), 0).SnapAsync(Line(3));
            var after = new FakeRoads(HttpStatusCode.OK, "{}");
            await Roads(after, cooldownSeconds: 60).SnapAsync(Line(3));
            Assert.Equal(1, after.Calls);
        }

        // Snapping moves points only inside their accuracy circle, so an
        // out-and-back street pulled onto one road line still encloses
        // nothing.
        [Fact]
        public void SnappedOutAndBack_ShouldStillNotBecomeTerritory()
        {
            var points = new Route()
                .To(150, 0, 2.5)
                .To(150, 12, 2.5)
                .To(0, 12, 2.5)
                .Build();
            foreach (var point in points)
                point.AccuracyMeters = 25;

            var snapped = Service().ApplySnappedPoints(points, points
                .Select((p, i) => Snapped(i, p, northMeters: 0))
                .Select((s, i) => new SnappedRoadPoint
                {
                    OriginalIndex = i,
                    Latitude = points[0].Latitude,
                    Longitude = s.Longitude
                })
                .ToList());

            Assert.Empty(Service().FindTerritories(snapped));
        }

        // Smooth, realistic GPS drift (not random jumps): each point is
        // shifted by up to driftMeters, changing slowly over time.
        private static List<RunGpsPoint> WithGpsDrift(
            List<RunGpsPoint> points, double driftMeters, double phase)
        {
            for (int i = 0; i < points.Count; i++)
            {
                double east = driftMeters * Math.Sin(i / 7.0 + phase);
                double north = driftMeters * Math.Cos(i / 11.0 + phase);

                points[i].Latitude += north / MetersPerDegree;
                points[i].Longitude += east / MetersPerDegreeLongitude;
            }

            return points;
        }
    }
}
