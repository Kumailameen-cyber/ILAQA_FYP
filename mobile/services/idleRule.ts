// Auto-finish rule for a forgotten run (L3): while a run is running (not
// paused), it finishes by itself once the runner has stayed within 20 m
// for 5 minutes. The run ends at the moment the runner stopped, so the
// idle minutes do not count as running time.

export const IDLE_FINISH_MS = 5 * 60 * 1000;
export const IDLE_RADIUS_METERS = 20;

export type IdleFix = {
  latitude: number;
  longitude: number;
  timestamp: number; // ms
};

function distanceMeters(a: IdleFix, b: IdleFix): number {
  const R = 6371000;
  const toRad = (d: number) => (d * Math.PI) / 180;
  const dLat = toRad(b.latitude - a.latitude);
  const dLon = toRad(b.longitude - a.longitude);
  const h =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRad(a.latitude)) * Math.cos(toRad(b.latitude)) *
      Math.sin(dLon / 2) ** 2;
  return 2 * R * Math.asin(Math.min(1, Math.sqrt(h)));
}

// When the runner stopped moving in the current running segment: the
// first of the latest fixes that all lie within 20 m of the newest fix.
// With no fixes since Start/Resume, the segment start itself.
export function stillSince(points: IdleFix[], segmentStartedAt: number): number {
  const leg = points.filter((p) => p.timestamp >= segmentStartedAt);
  if (leg.length === 0) return segmentStartedAt;

  const newest = leg[leg.length - 1];
  let since = newest.timestamp;
  for (let i = leg.length - 1; i >= 0; i--) {
    if (distanceMeters(leg[i], newest) > IDLE_RADIUS_METERS) break;
    since = leg[i].timestamp;
  }
  return Math.max(since, segmentStartedAt);
}

// The end time to finish the run with, or null while it should go on.
export function idleFinishDue(
  points: IdleFix[],
  segmentStartedAt: number,
  now: number
): { endedAt: number } | null {
  const since = stillSince(points, segmentStartedAt);
  return now - since >= IDLE_FINISH_MS ? { endedAt: since } : null;
}
