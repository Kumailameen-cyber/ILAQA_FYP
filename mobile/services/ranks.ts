// RANKS tab data: the runner dashboard (GET /api/stats/me, FR-22) and the
// Karachi leaderboard (GET /api/leaderboard, FR-23), plus how their values
// are written on screen. Formatting is pure, so it is tested with Node.

export type RankMetric = 'area' | 'distance' | 'territories';

export type MyStats = {
  territoryCount: number;
  totalAreaSquareMeters: number;
  shieldedCount: number;
  vulnerableCount: number;
  completedRuns: number;
  totalDistanceMeters: number;
  totalActiveSeconds: number;
  areaRank: number | null;
  distanceRank: number | null;
  territoryRank: number | null;
};

export type LeaderboardEntry = {
  rank: number;
  name: string;
  value: number;
  isMine: boolean;
  colorHex: string;
};

export type Leaderboard = {
  metric: RankMetric;
  entries: LeaderboardEntry[];
  me: LeaderboardEntry | null;
};

// 1234.6 -> "1,235"
function withCommas(value: number): string {
  return String(Math.round(value)).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

// The server never sends these, but the screen shows a dash rather than "NaN".
function isShowable(value: number): boolean {
  return Number.isFinite(value) && value >= 0;
}

export function formatRankValue(metric: RankMetric, value: number): string {
  if (!isShowable(value)) return '—';
  if (metric === 'territories') return String(Math.round(value));
  if (metric === 'distance') {
    return value < 1000 ? `${Math.round(value)} m` : `${(value / 1000).toFixed(2)} km`;
  }
  return value < 1_000_000
    ? `${withCommas(value)} m²`
    : `${(value / 1_000_000).toFixed(2)} km²`;
}

export function formatActiveTime(seconds: number): string {
  const totalMinutes = Math.floor(seconds / 60);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return hours > 0 ? `${hours} h ${minutes} min` : `${minutes} min`;
}

export function formatRank(rank: number | null | undefined): string {
  return rank == null || !isShowable(rank) ? '—' : `#${rank}`;
}
