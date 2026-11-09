using System.Text.Json;
using ILAQA.Common;
using ILAQA.Features.Auth;
using ILAQA.Features.Runner;
using ILAQA.Features.Runs;

namespace ILAQA.Tests;

// Backward compatibility: each response record must produce the same JSON
// as the anonymous object the old controllers returned (copied below), so
// the mobile app keeps working. Uses ASP.NET Core's web JSON settings.
public class ApiContractTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly DateTime When = new(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Id = Guid.Parse("3f2b8c1e-0000-4000-8000-000000000001");

    private static void AssertSameJson(object oldShape, object newShape) =>
        Assert.Equal(JsonSerializer.Serialize(oldShape, Web), JsonSerializer.Serialize(newShape, Web));

    // Same JSON keys and values, ignoring property order.
    private static void AssertSameFields(object oldShape, object newShape)
    {
        static SortedDictionary<string, string> Fields(object o) =>
            new(JsonSerializer.SerializeToElement(o, Web).EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetRawText()));

        Assert.Equal(Fields(oldShape), Fields(newShape));
    }

    [Fact]
    public void Register() => AssertSameJson(
        new { message = "m", requiresEmailVerification = true, email = "a@b.com" },
        new RegisterResponse("m", true, "a@b.com"));

    [Fact]
    public void Login() => AssertSameJson(
        new { message = "m", accessToken = "a", refreshToken = "r", tokenType = "Bearer", expiresIn = 900,
              user = new { id = "u", username = "n", displayName = "d" } },
        new LoginResponse("m", "a", "r", "Bearer", 900, new UserSummary("u", "n", "d")));

    [Fact]
    public void Refresh() => AssertSameJson(
        new { message = "m", accessToken = "a", refreshToken = "r", tokenType = "Bearer", expiresIn = 900 },
        new TokenResponse("m", "a", "r", "Bearer", 900));

    [Fact]
    public void VerifyResetOtp() => AssertSameJson(
        new { message = "m", verified = true, resetToken = "t", expiresIn = 900 },
        new ResetOtpVerifiedResponse("m", true, "t", 900));

    [Fact]
    public void MessageOnly() => AssertSameJson(new { message = "m" }, new MessageResponse("m"));

    [Fact]
    public void ErrorWithPasswordRules() => AssertSameJson(
        new { message = "m", errors = new[] { "e" } },
        new ErrorResponse("m") { Errors = ["e"] });

    [Fact]
    public void LoginNeedsVerification_WithAndWithoutEmail()
    {
        AssertSameJson(new { message = "m", requiresEmailVerification = true, email = "a@b.com" },
            new ErrorResponse("m") { RequiresEmailVerification = true, Email = "a@b.com" });
        AssertSameJson(new { message = "m", requiresEmailVerification = true },
            new ErrorResponse("m") { RequiresEmailVerification = true });
    }

    [Fact]
    public void RegistrationExpired() => AssertSameJson(
        new { message = "m", registrationExpired = true },
        new ErrorResponse("m") { RegistrationExpired = true });

    [Fact]
    public void Profile() => AssertSameJson(
        new { id = "u", username = "n", email = "e", displayName = "d", bio = (string?)null,
              profilePictureUrl = "p", isProfilePublic = true, createdAt = When },
        new ProfileResponse("u", "n", "e", "d", null, "p", true, When));

    [Fact]
    public void ProfileUpdated() => AssertSameJson(
        new { message = "m", displayName = "d", bio = "b", isProfilePublic = false },
        new ProfileUpdatedResponse("m", "d", "b", false));

    [Fact]
    public void ProfilePictureRemoved_KeepsNullUrl() => AssertSameJson(
        new { message = "m", profilePictureUrl = (string?)null },
        new ProfilePictureResponse("m", null));

    [Fact]
    public void StartRun() => AssertSameJson(
        new { Id, StartedAt = When, Status = "Running", AlreadyExists = false },
        new StartRunResponse(Id, When, "Running", false));

    [Fact]
    public void PauseRun_HasNoActiveStartedAt() => AssertSameFields(
        new { Id, Status = "Paused", ActiveSeconds = 5 },
        new RunStateResponse(Id, "Paused", 5, null));

    [Fact]
    public void ResumeRun() => AssertSameFields(
        new { Id, Status = "Running", ActiveStartedAt = (DateTime?)When, ActiveSeconds = 5 },
        new RunStateResponse(Id, "Running", 5, When));

    [Fact]
    public void Distance() => AssertSameJson(
        new { RunId = Id, TotalGpsPoints = 3, AcceptedSegments = 2, RejectedSegments = 0,
              CalculatedDistanceMeters = 12.5, CalculatedDistanceKm = 0.013 },
        new RunDistanceResponse(Id, 3, 2, 0, 12.5, 0.013));

    [Fact]
    public void ClaimDecisionFields() => AssertSameJson(
        new { StartSequenceNo = 1, EndSequenceNo = 9, Status = "Rejected", DecisionReason = (string?)"TOO_SMALL",
              AreaSquareMeters = 10.5, TerritoryId = (Guid?)null },
        new ClaimDecision(1, 9, "Rejected", "TOO_SMALL", 10.5, null));

    [Fact]
    public void UploadedTerritory_HasSameFieldsAsBefore() => AssertSameJson(
        new { TerritoryId = Id, LoopIndex = 1, AreaSquareMeters = 2.5,
              Boundary = new[] { new { Latitude = 24.86, Longitude = 67.0 } } },
        new RunTerritoryDto(Id, 1, 2.5, [new LatLng(24.86, 67.0)]));

    [Fact]
    public void MapTerritory() => AssertSameJson(
        new { TerritoryId = Id, AreaSquareMeters = 2.5, ColorHex = "#123456", OwnerLabel = "Anonymous profile",
              IsMine = false, IsShielded = true, Boundary = new[] { new { Latitude = 24.86, Longitude = 67.0 } } },
        new MapTerritoryDto(Id, 2.5, "#123456", "Anonymous profile", false, true, [new LatLng(24.86, 67.0)]));

    [Fact]
    public void Route() => AssertSameJson(
        new { RunId = Id, StartedAt = When,
              Legs = new[] { new { LegNo = 1, Coordinates = new[] { new { Latitude = 24.86, Longitude = 67.0 } } } } },
        new RouteDto(Id, When, [new RouteLegDto(1, [new LatLng(24.86, 67.0)])]));

    [Fact]
    public void Boundary_UsesYAsLatitudeAndXAsLongitude()
    {
        var square = new NetTopologySuite.Geometries.GeometryFactory().CreatePolygon(
        [
            new(67.0, 24.86), new(67.1, 24.86), new(67.1, 24.96), new(67.0, 24.86)
        ]);

        var first = RunTerritoryDto.ToLatLngs(square)[0];

        Assert.Equal(24.86, first.Latitude);
        Assert.Equal(67.0, first.Longitude);
    }
}
