using ILAQA.Common;
using ILAQA.Data;
using ILAQA.Models;
using ILAQA.Services;
using Microsoft.EntityFrameworkCore;

namespace ILAQA.Features.Leaderboard;

// Business logic for the runner dashboard (FR-22) and the Karachi
// leaderboards (FR-23). Reads totals from the database and leaves the
// ranking to LeaderboardRules. Read-only: nothing is written.
public sealed class LeaderboardService(ApplicationDbContext db)
{
    public async Task<LeaderboardResponse> GetLeaderboardAsync(string viewerUserId, string? metricText, int? limit)
    {
        if (!LeaderboardRules.TryParseMetric(metricText, out var metric))
            throw ApiException.BadRequest("Unknown ranking. Use area, distance or territories.");

        var totals = await LoadTotalsAsync();
        var ranked = LeaderboardRules.Rank(totals, metric);

        // Only the owners who appear on the board, and only the columns the
        // label needs (never password data).
        var shownIds = ranked
            .Take(LeaderboardRules.ClampLimit(limit))
            .Select(r => r.Totals.UserId)
            .Append(viewerUserId)
            .ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => shownIds.Contains(u.Id))
            .Select(u => new ApplicationUser
            {
                Id = u.Id, UserName = u.UserName, DisplayName = u.DisplayName, IsProfilePublic = u.IsProfilePublic
            })
            .ToDictionaryAsync(u => u.Id);

        return LeaderboardRules.BuildBoard(totals, users, metric, viewerUserId, LeaderboardRules.ClampLimit(limit));
    }

    public async Task<MyStatsResponse> GetMyStatsAsync(string userId)
    {
        var totals = await LoadTotalsAsync();

        var myLand = await db.Territories.AsNoTracking()
            .Where(t => t.OwnerUserId == userId && (t.Status == "Active" || t.Status == "Vulnerable"))
            .Select(t => new Territory { Status = t.Status, ShieldExpiresAt = t.ShieldExpiresAt, UpdatedAt = t.UpdatedAt })
            .ToListAsync();

        var now = DateTime.UtcNow;
        int shielded = myLand.Count(t => TerritoryDetectionService.IsShielded(t, now));
        int vulnerable = myLand.Count - shielded;

        return LeaderboardRules.StatsFor(userId, totals, shielded, vulnerable);
    }

    // Every runner's totals: land owned now, and finished runs. Two grouped
    // queries; the database adds up the numbers.
    private async Task<List<RunnerTotals>> LoadTotalsAsync()
    {
        var land = await db.Territories.AsNoTracking()
            .Where(t => t.Status == "Active" || t.Status == "Vulnerable")
            .GroupBy(t => t.OwnerUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count(), Area = g.Sum(t => t.AreaSquareMeters) })
            .ToListAsync();

        var runs = await db.Runs.AsNoTracking()
            .Where(r => r.Status == "Completed")
            .GroupBy(r => r.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
                Distance = g.Sum(r => r.TotalDistanceMeters),
                Seconds = g.Sum(r => r.ActiveSeconds)
            })
            .ToListAsync();

        var landById = land.ToDictionary(l => l.UserId);
        var runsById = runs.ToDictionary(r => r.UserId);

        return landById.Keys.Union(runsById.Keys)
            .Select(id =>
            {
                landById.TryGetValue(id, out var l);
                runsById.TryGetValue(id, out var r);
                return new RunnerTotals(id, l?.Count ?? 0, l?.Area ?? 0, r?.Distance ?? 0, r?.Count ?? 0, r?.Seconds ?? 0);
            })
            .ToList();
    }
}
