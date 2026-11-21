using ILAQA.Features.Leaderboard;
using ILAQA.Features.Runs;
using ILAQA.Models;
using ILAQA.Services;

namespace ILAQA.Features.Public;

// The rules for what the public website sees, as pure functions (no
// database), so they are easy to test and to explain.
public static class PublicRules
{
    // No viewer is signed in, so a private runner is always "Anonymous profile".
    public static PublicTerritoryDto ToTerritoryDto(Territory territory, ApplicationUser owner, DateTime now) =>
        new(
            Math.Round(territory.AreaSquareMeters, 2),
            TerritoryDetectionService.OwnerColor(owner.Id),
            TerritoryDetectionService.OwnerLabel(owner, viewerUserId: ""),
            TerritoryDetectionService.IsShielded(territory, now),
            RunTerritoryDto.ToLatLngs(territory.Boundary))
        {
            Holes = RunTerritoryDto.HolesOf(territory.Boundary)
        };

    public const int TopCount = 10;

    // Per-IP request limit for the public endpoints, and how long the server
    // keeps an answer so the website cannot hammer the database.
    public const string RateLimitPolicy = "public";
    public const int DefaultRequestsPerMinute = 60;
    public const int CacheSeconds = 30;
    public const string CorsPolicy = "GuestWebsite";

    // City totals plus the top runners by area. Takes each runner's land
    // totals (Active + Vulnerable) and reuses the leaderboard ranking with no
    // viewer, so private runners are "Anonymous profile".
    public static PublicSummaryResponse BuildSummary(
        IEnumerable<RunnerTotals> landTotals, IReadOnlyDictionary<string, ApplicationUser> users)
    {
        var withLand = landTotals.Where(t => t.TerritoryCount > 0).ToList();

        var board = LeaderboardRules.BuildBoard(
            withLand, users, LeaderboardMetric.Area, viewerUserId: "", limit: TopCount);

        return new PublicSummaryResponse(
            withLand.Count,
            withLand.Sum(t => t.TerritoryCount),
            Math.Round(withLand.Sum(t => t.AreaSquareMeters), 2),
            board.Entries.Select(e => new PublicRankEntry(e.Rank, e.Name, e.Value, e.ColorHex)).ToList());
    }

    // The guest website origins from config, as scheme + host + port only.
    // Anything that is not a plain http(s) address (including "*") is dropped,
    // so a config mistake can never turn into "allow every origin".
    public static string[] CleanOrigins(IEnumerable<string?>? configured) =>
        (configured ?? [])
            .Select(text => Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var uri) ? uri : null)
            .Where(uri => uri is { Scheme: "http" or "https" } && !uri.Host.Contains('*'))
            .Select(uri => uri!.GetLeftPart(UriPartial.Authority))
            .Distinct()
            .ToArray();
}
