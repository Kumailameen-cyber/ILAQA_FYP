using ILAQA.Data;
using ILAQA.Features.Leaderboard;
using ILAQA.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ILAQA.Features.Public;

// The data behind the public guest website (FR-24). Read-only. Answers are
// the same for every visitor, so they are kept for a short time and shared.
public sealed class PublicService(ApplicationDbContext db, IMemoryCache cache)
{
    private static readonly TimeSpan KeepFor = TimeSpan.FromSeconds(PublicRules.CacheSeconds);

    public async Task<List<PublicTerritoryDto>> GetTerritoriesAsync() =>
        (await cache.GetOrCreateAsync("public:territories", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = KeepFor;
            return await LoadTerritoriesAsync();
        }))!;

    public async Task<PublicSummaryResponse> GetSummaryAsync() =>
        (await cache.GetOrCreateAsync("public:summary", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = KeepFor;
            return await LoadSummaryAsync();
        }))!;

    // Every Active or Vulnerable territory, with only the owner columns the
    // label needs (never password data).
    private async Task<List<PublicTerritoryDto>> LoadTerritoriesAsync()
    {
        var rows = await (
                from t in db.Territories.AsNoTracking()
                where t.Status == "Active" || t.Status == "Vulnerable"
                join u in db.Users on t.OwnerUserId equals u.Id
                orderby t.CreatedAt descending
                select new
                {
                    Territory = t,
                    Owner = new ApplicationUser
                    {
                        Id = u.Id, UserName = u.UserName, DisplayName = u.DisplayName, IsProfilePublic = u.IsProfilePublic
                    }
                })
            .ToListAsync();

        var now = DateTime.UtcNow;
        return rows.Select(r => PublicRules.ToTerritoryDto(r.Territory, r.Owner, now)).ToList();
    }

    private async Task<PublicSummaryResponse> LoadSummaryAsync()
    {
        var totals = await db.Territories.AsNoTracking()
            .Where(t => t.Status == "Active" || t.Status == "Vulnerable")
            .GroupBy(t => t.OwnerUserId)
            .Select(g => new RunnerTotals(g.Key, g.Count(), g.Sum(t => t.AreaSquareMeters), 0, 0, 0))
            .ToListAsync();

        var topIds = LeaderboardRules.Rank(totals, LeaderboardMetric.Area)
            .Take(PublicRules.TopCount)
            .Select(r => r.Totals.UserId)
            .ToList();

        var users = await db.Users.AsNoTracking()
            .Where(u => topIds.Contains(u.Id))
            .Select(u => new ApplicationUser
            {
                Id = u.Id, UserName = u.UserName, DisplayName = u.DisplayName, IsProfilePublic = u.IsProfilePublic
            })
            .ToDictionaryAsync(u => u.Id);

        return PublicRules.BuildSummary(totals, users);
    }
}
