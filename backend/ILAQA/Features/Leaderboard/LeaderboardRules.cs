using ILAQA.Models;
using ILAQA.Services;

namespace ILAQA.Features.Leaderboard;

// The ranking rules for the dashboard and Karachi leaderboards, as pure
// functions (no database), so they are easy to test and to explain.
public static class LeaderboardRules
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    // "area" (default), "distance" or "territories", any case.
    public static bool TryParseMetric(string? text, out LeaderboardMetric metric)
    {
        metric = LeaderboardMetric.Area;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var name = text.Trim();
        return name.All(char.IsLetter) &&
            Enum.TryParse(name, ignoreCase: true, out metric);
    }

    public static int ClampLimit(int? limit) =>
        Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

    public static double ValueOf(RunnerTotals totals, LeaderboardMetric metric) => metric switch
    {
        LeaderboardMetric.Distance => totals.DistanceMeters,
        LeaderboardMetric.Territories => totals.TerritoryCount,
        _ => totals.AreaSquareMeters
    };

    // Highest first. Equal values share a rank and the next rank is
    // skipped (1, 2, 2, 4). Runners with nothing for this metric are left out.
    public static List<(int Rank, RunnerTotals Totals)> Rank(
        IEnumerable<RunnerTotals> totals, LeaderboardMetric metric)
    {
        var ordered = totals
            .Where(t => ValueOf(t, metric) > 0)
            .OrderByDescending(t => ValueOf(t, metric))
            .ThenBy(t => t.UserId, StringComparer.Ordinal)
            .ToList();

        var ranked = new List<(int, RunnerTotals)>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            bool tiedWithPrevious = i > 0 &&
                ValueOf(ordered[i], metric) == ValueOf(ordered[i - 1], metric);
            int rank = tiedWithPrevious ? ranked[i - 1].Item1 : i + 1;
            ranked.Add((rank, ordered[i]));
        }
        return ranked;
    }

    public static LeaderboardResponse BuildBoard(
        IEnumerable<RunnerTotals> totals,
        IReadOnlyDictionary<string, ApplicationUser> users,
        LeaderboardMetric metric,
        string viewerUserId,
        int limit)
    {
        var ranked = Rank(totals, metric);

        LeaderboardEntry ToEntry((int Rank, RunnerTotals Totals) row)
        {
            var name = users.TryGetValue(row.Totals.UserId, out var user)
                ? TerritoryDetectionService.OwnerLabel(user, viewerUserId)
                : TerritoryDetectionService.AnonymousOwnerLabel;

            return new LeaderboardEntry(
                row.Rank,
                name,
                Math.Round(ValueOf(row.Totals, metric), 2),
                row.Totals.UserId == viewerUserId,
                TerritoryDetectionService.OwnerColor(row.Totals.UserId));
        }

        var mine = ranked.FindIndex(r => r.Totals.UserId == viewerUserId);

        return new LeaderboardResponse(
            metric.ToString().ToLowerInvariant(),
            ranked.Take(limit).Select(ToEntry).ToList(),
            mine >= 0 ? ToEntry(ranked[mine]) : null);
    }

    public static MyStatsResponse StatsFor(
        string userId, IEnumerable<RunnerTotals> allTotals, int shieldedCount, int vulnerableCount)
    {
        var all = allTotals.ToList();
        var mine = all.FirstOrDefault(t => t.UserId == userId)
            ?? new RunnerTotals(userId, 0, 0, 0, 0, 0);

        int? RankIn(LeaderboardMetric metric)
        {
            var row = Rank(all, metric).FirstOrDefault(r => r.Totals.UserId == userId);
            return row.Totals is null ? null : row.Rank;
        }

        return new MyStatsResponse(
            mine.TerritoryCount,
            Math.Round(mine.AreaSquareMeters, 2),
            shieldedCount,
            vulnerableCount,
            mine.CompletedRuns,
            Math.Round(mine.DistanceMeters, 2),
            mine.ActiveSeconds,
            RankIn(LeaderboardMetric.Area),
            RankIn(LeaderboardMetric.Distance),
            RankIn(LeaderboardMetric.Territories));
    }
}
