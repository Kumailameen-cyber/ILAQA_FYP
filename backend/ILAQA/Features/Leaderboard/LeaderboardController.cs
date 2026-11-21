using ILAQA.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ILAQA.Features.Leaderboard;

// /api/stats/me and /api/leaderboard — the runner dashboard (FR-22) and
// the Karachi rankings (FR-23). Thin controller: each endpoint only calls
// LeaderboardService.
[ApiController]
[Route("api")]
[Authorize]
public sealed class LeaderboardController(LeaderboardService leaderboard) : ControllerBase
{
    // The signed-in runner's land, runs, distance and ranks.
    [HttpGet("stats/me")]
    public async Task<MyStatsResponse> GetMyStats() =>
        await leaderboard.GetMyStatsAsync(User.GetRequiredUserId());

    // Karachi ranking: ?metric=area|distance|territories (default area),
    // ?limit=1..100 (default 50). Includes the viewer's own row.
    [HttpGet("leaderboard")]
    public async Task<LeaderboardResponse> GetLeaderboard([FromQuery] string? metric, [FromQuery] int? limit) =>
        await leaderboard.GetLeaderboardAsync(User.GetRequiredUserId(), metric, limit);
}
