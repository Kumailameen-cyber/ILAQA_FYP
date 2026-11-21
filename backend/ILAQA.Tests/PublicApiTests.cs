using System.Text.Json;
using ILAQA.Features.Leaderboard;
using ILAQA.Features.Public;
using System.Reflection;
using ILAQA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using NetTopologySuite.Geometries;

namespace ILAQA.Tests;

// FR-24 (public guest map), FR-21 and NFR-07: what the unauthenticated
// website may receive. The rules are pure functions, tested without a
// database.
public class PublicApiTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly DateTime Now = new(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);
    private static readonly GeometryFactory Factory = new();

    private static Polygon Square() => Factory.CreatePolygon(
        [new(67.00, 24.86), new(67.01, 24.86), new(67.01, 24.87), new(67.00, 24.87), new(67.00, 24.86)]);

    private static Polygon SquareWithHole()
    {
        var outer = Factory.CreateLinearRing(
            [new(67.00, 24.86), new(67.01, 24.86), new(67.01, 24.87), new(67.00, 24.87), new(67.00, 24.86)]);
        var hole = Factory.CreateLinearRing(
            [new(67.004, 24.864), new(67.006, 24.864), new(67.006, 24.866), new(67.004, 24.866), new(67.004, 24.864)]);
        return Factory.CreatePolygon(outer, [hole]);
    }

    private static Territory Land(Polygon? boundary = null, string status = "Active", DateTime? shieldEnds = null) => new()
    {
        Id = Guid.NewGuid(), RunId = Guid.NewGuid(), LoopIndex = 1, OwnerUserId = "owner-secret-id",
        Boundary = boundary ?? Square(), AreaSquareMeters = 1234.5678, Status = status,
        ShieldExpiresAt = shieldEnds, CreatedAt = Now, UpdatedAt = Now
    };

    private static ApplicationUser Owner(bool isPublic = true) =>
        new() { Id = "owner-secret-id", UserName = "ali_runner", DisplayName = "Ali", IsProfilePublic = isPublic };

    // ---------- Slice 1: one territory for the public map ----------

    [Fact]
    public void Territory_ShowsPublicOwnerName_ColourAreaAndBoundary()
    {
        var dto = PublicRules.ToTerritoryDto(Land(), Owner(), Now);

        Assert.Equal("Ali", dto.OwnerLabel);
        Assert.Equal(1234.57, dto.AreaSquareMeters);
        Assert.Matches("^#[0-9A-F]{6}$", dto.ColorHex);
        Assert.Equal(5, dto.Boundary.Count);
        Assert.Equal(24.86, dto.Boundary[0].Latitude);
        Assert.Equal(67.00, dto.Boundary[0].Longitude);
        Assert.Null(dto.Holes);
    }

    [Fact]
    public void PrivateOwner_IsShownAsAnonymousProfile()
    {
        var dto = PublicRules.ToTerritoryDto(Land(), Owner(isPublic: false), Now);

        Assert.Equal("Anonymous profile", dto.OwnerLabel);
    }

    [Fact]
    public void Territory_IsShielded_OnlyWhileActiveAndShieldHasNotEnded()
    {
        var shielded = PublicRules.ToTerritoryDto(Land(shieldEnds: Now.AddDays(1)), Owner(), Now);
        var expired = PublicRules.ToTerritoryDto(Land(shieldEnds: Now.AddMinutes(-1)), Owner(), Now);
        var vulnerable = PublicRules.ToTerritoryDto(Land(status: "Vulnerable", shieldEnds: Now.AddDays(1)), Owner(), Now);

        Assert.True(shielded.IsShielded);
        Assert.False(expired.IsShielded);
        Assert.False(vulnerable.IsShielded);
    }

    [Fact]
    public void TerritoryWithHole_SendsOuterEdgeAsBoundary_AndTheHoleSeparately()
    {
        var dto = PublicRules.ToTerritoryDto(Land(SquareWithHole()), Owner(), Now);

        Assert.Equal(5, dto.Boundary.Count);
        var hole = Assert.Single(dto.Holes!);
        Assert.Equal(24.864, hole[0].Latitude);
    }

    // ---------- Contract: no ids, no GPS or route fields (FR-21, NFR-07) ----------

    // Every JSON property name in the answer, at any depth.
    private static List<string> PropertyNames(object response)
    {
        var names = new List<string>();
        void Walk(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
                foreach (var p in e.EnumerateObject()) { names.Add(p.Name); Walk(p.Value); }
            else if (e.ValueKind == JsonValueKind.Array)
                foreach (var item in e.EnumerateArray()) Walk(item);
        }
        Walk(JsonSerializer.SerializeToElement(response, Web));
        return names;
    }

    private static readonly string[] ForbiddenNames =
    [
        "id", "userid", "ownerUserId", "runid", "territoryid", "claimid", "username", "email",
        "accuracymeters", "sequenceno", "legno", "recordedat", "points", "route", "routes", "legs",
        "ismine", "snappedlatitude", "snappedlongitude"
    ];

    private static void AssertNoPrivateFields(object response)
    {
        var names = PropertyNames(response);
        Assert.NotEmpty(names);
        foreach (var name in names)
        {
            Assert.DoesNotContain(name.ToLowerInvariant(), ForbiddenNames.Select(f => f.ToLowerInvariant()));
            Assert.False(name.EndsWith("Id", StringComparison.Ordinal), $"'{name}' looks like an id");
        }
    }

    [Fact]
    public void TerritoryJson_HasNoIdsAndNoGpsFields()
    {
        var dto = PublicRules.ToTerritoryDto(Land(SquareWithHole(), shieldEnds: Now.AddDays(1)), Owner(), Now);

        AssertNoPrivateFields(dto);
        var json = JsonSerializer.Serialize(dto, Web);
        Assert.DoesNotContain("owner-secret-id", json);
        Assert.DoesNotContain("ali_runner", json);
    }

    // ---------- Slice 2: city summary ----------

    private static RunnerTotals Land(string id, int territories, double area) => new(id, territories, area, 0, 0, 0);

    private static readonly Dictionary<string, ApplicationUser> Users = new()
    {
        ["a"] = new() { Id = "a", UserName = "a_user", DisplayName = "Ali", IsProfilePublic = true },
        ["b"] = new() { Id = "b", UserName = "b_user", DisplayName = "Bilal", IsProfilePublic = false },
    };

    [Fact]
    public void Summary_AddsUpRunnersTerritoriesAndArea()
    {
        var summary = PublicRules.BuildSummary(
            [Land("a", 2, 500.123), Land("b", 3, 900.456), Land("c", 0, 0)], Users);

        Assert.Equal(2, summary.RunnersWithLand);
        Assert.Equal(5, summary.TerritoryCount);
        Assert.Equal(1400.58, summary.TotalAreaSquareMeters);
    }

    [Fact]
    public void Summary_ListsTheTopTenByArea_WithPrivateRunnersAnonymous()
    {
        var many = Enumerable.Range(1, 12).Select(i => Land($"r{i}", 1, i * 100)).ToList();
        many.Add(Land("b", 1, 5000));

        var summary = PublicRules.BuildSummary(many, Users);

        Assert.Equal(10, summary.TopByArea.Count);
        Assert.Equal(new[] { 1, 2, 3 }, summary.TopByArea.Take(3).Select(r => r.Rank));
        Assert.Equal("Anonymous profile", summary.TopByArea[0].Name);
        Assert.Equal(5000, summary.TopByArea[0].Value);
        Assert.Equal(1200, summary.TopByArea[1].Value);
    }

    [Fact]
    public void Summary_OfAnEmptyCity_IsZeroAndEmpty()
    {
        var summary = PublicRules.BuildSummary([], Users);

        Assert.Equal(0, summary.RunnersWithLand);
        Assert.Equal(0, summary.TerritoryCount);
        Assert.Equal(0, summary.TotalAreaSquareMeters);
        Assert.Empty(summary.TopByArea);
    }

    [Fact]
    public void SummaryJson_HasNoIdsAndNoGpsFields()
    {
        var summary = PublicRules.BuildSummary([Land("a", 2, 500), Land("b", 1, 100)], Users);

        AssertNoPrivateFields(summary);
        var json = JsonSerializer.Serialize(summary, Web);
        Assert.DoesNotContain("a_user", json);
        Assert.DoesNotContain("\"a\"", json);
    }

    // ---------- Slice 3: CORS origins from config (Cors:GuestWebsiteOrigins) ----------

    [Fact]
    public void Origins_AreKeptAsSchemeHostAndPort_WithoutPathOrTrailingSlash()
    {
        var cleaned = PublicRules.CleanOrigins(
            ["https://ilaqa.example.com/", "http://localhost:3000", "HTTPS://Other.Example.com/some/page"]);

        Assert.Equal(
            new[] { "https://ilaqa.example.com", "http://localhost:3000", "https://other.example.com" }, cleaned);
    }

    [Fact]
    public void Origins_ThatWouldAllowEveryone_OrAreNotWebAddresses_AreDropped()
    {
        var cleaned = PublicRules.CleanOrigins(
            ["*", "", "   ", null, "ilaqa.example.com", "ftp://ilaqa.example.com", "javascript:alert(1)", "https://*.example.com"]);

        Assert.Empty(cleaned);
    }

    [Fact]
    public void Origins_AreListedOnce_AndNothingConfiguredMeansNone()
    {
        Assert.Single(PublicRules.CleanOrigins(["https://a.example.com", "https://a.example.com/"]));
        Assert.Empty(PublicRules.CleanOrigins(null));
    }

    // ---------- Slice 4: the controller's shape ----------

    [Fact]
    public void PublicController_IsAnonymous_RateLimited_ReadOnly_AndHasOnlyTheTwoRoutes()
    {
        var type = typeof(PublicController);

        Assert.NotNull(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(PublicRules.RateLimitPolicy, type.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);

        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var routes = actions
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .Select(a => (Verbs: string.Join(",", a.HttpMethods), a.Template))
            .OrderBy(r => r.Template)
            .ToList();

        Assert.Equal(
            new[] { ("GET", "territories"), ("GET", "summary") }.OrderBy(r => r.Item2),
            routes.Select(r => (r.Verbs, r.Template!)).OrderBy(r => r.Item2));
        Assert.Equal("api/public", type.GetCustomAttribute<RouteAttribute>()!.Template);
    }

    // guest-website/app/page.js reads exactly these field names.
    [Fact]
    public void PublicJson_UsesTheFieldNamesTheWebsiteReads()
    {
        var territory = JsonSerializer.SerializeToElement(
            PublicRules.ToTerritoryDto(Land(SquareWithHole()), Owner(), Now), Web);
        var summary = JsonSerializer.SerializeToElement(
            PublicRules.BuildSummary([Land("a", 1, 100)], Users), Web);

        Assert.Equal(
            new[] { "areaSquareMeters", "boundary", "colorHex", "holes", "isShielded", "ownerLabel" },
            territory.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(
            new[] { "latitude", "longitude" },
            territory.GetProperty("boundary")[0].EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(
            new[] { "runnersWithLand", "territoryCount", "topByArea", "totalAreaSquareMeters" }.Order(),
            summary.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(
            new[] { "colorHex", "name", "rank", "value" },
            summary.GetProperty("topByArea")[0].EnumerateObject().Select(p => p.Name).Order());
    }
}
