namespace ILAQA.Features.Leaderboard;

// What a Karachi ranking is ordered by (FR-23).
public enum LeaderboardMetric
{
    Area,        // m² of land owned now (Active + Vulnerable)
    Distance,    // cleaned walking/running distance of finished runs
    Territories  // number of territories owned now
}

// One runner's totals, read from the database.
public sealed record RunnerTotals(
    string UserId, int TerritoryCount, double AreaSquareMeters,
    double DistanceMeters, int CompletedRuns, int ActiveSeconds);

// ---------- Responses ----------

// One row of the ranking. Private runners are "Anonymous profile" to
// everyone except themselves; no ids, GPS or routes are sent.
public sealed record LeaderboardEntry(int Rank, string Name, double Value, bool IsMine, string ColorHex);

// GET /api/leaderboard: the top rows, plus the viewer's own row (null when
// they have nothing to rank yet).
public sealed record LeaderboardResponse(string Metric, List<LeaderboardEntry> Entries, LeaderboardEntry? Me);

// GET /api/stats/me: the runner dashboard (FR-22). Ranks are null until
// the runner has something to rank.
public sealed record MyStatsResponse(
    int TerritoryCount, double TotalAreaSquareMeters, int ShieldedCount, int VulnerableCount,
    int CompletedRuns, double TotalDistanceMeters, int TotalActiveSeconds,
    int? AreaRank, int? DistanceRank, int? TerritoryRank);
