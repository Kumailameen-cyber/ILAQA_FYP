// ILAQA - mobile/services/territoryDetection.ts
// Finds valid closed loops. A GPS gap or pause separates the route into sections.
// This file creates PREVIEWS only. The ASP.NET backend must approve real claims.

export type GpsCoordinate = {
  latitude: number;
  longitude: number;
};

export type TerritoryCandidate = {
  startIndex: number;
  endIndex: number;
  boundary: GpsCoordinate[];
  areaSquareMeters: number;
};

export type LegGpsPoint = GpsCoordinate & {
  legNo: number;
  timestamp?: number; // milliseconds since epoch
  accuracy?: number | null;
};

const EARTH_RADIUS = 6371000;
const CLOSE_ENOUGH_METERS = 15;
const MIN_AREA_SQUARE_METERS = 100;
const MIN_LOOP_POINTS = 4;
const MAX_ACCURACY_METERS = 40; // Existing mobile rule; backend currently uses 30.
const MAX_SPEED_METERS_PER_SECOND = 8;
const MAX_GAP_SECONDS = 120; // NEW: split routes when GPS is missing for > 2 minutes.

const radians = (degrees: number) => degrees * Math.PI / 180;

function samePoint(a: GpsCoordinate, b: GpsCoordinate): boolean {
  return a.latitude === b.latitude && a.longitude === b.longitude;
}

// 1. Calculate distance between two GPS coordinates (Haversine).
export function calculateGpsDistance(a: GpsCoordinate, b: GpsCoordinate): number {
  const lat = radians(b.latitude - a.latitude);
  const lon = radians(b.longitude - a.longitude);
  const h = Math.sin(lat / 2) ** 2 +
    Math.cos(radians(a.latitude)) * Math.cos(radians(b.latitude)) *
    Math.sin(lon / 2) ** 2;
  return 2 * EARTH_RADIUS * Math.atan2(Math.sqrt(h), Math.sqrt(Math.max(0, 1 - h)));
}

// 2. Estimate polygon area in square meters (shoelace formula).
export function calculatePolygonArea(points: GpsCoordinate[]): number {
  if (points.length < 3) return 0;

  const averageLatitude = points.reduce((sum, p) => sum + p.latitude, 0) / points.length;
  const scale = Math.cos(radians(averageLatitude));
  const projected = points.map(p => ({
    x: EARTH_RADIUS * radians(p.longitude) * scale,
    y: EARTH_RADIUS * radians(p.latitude),
  }));

  let twiceArea = 0;
  for (let i = 0; i < projected.length; i++) {
    const a = projected[i];
    const b = projected[(i + 1) % projected.length];
    twiceArea += a.x * b.y - b.x * a.y;
  }
  return Math.abs(twiceArea) / 2;
}

// 3. Check whether two non-adjacent route edges cross.
function edgesCross(a: GpsCoordinate, b: GpsCoordinate,
                    c: GpsCoordinate, d: GpsCoordinate): boolean {
  const turn = (p: GpsCoordinate, q: GpsCoordinate, r: GpsCoordinate) =>
    (q.longitude - p.longitude) * (r.latitude - p.latitude) -
    (q.latitude - p.latitude) * (r.longitude - p.longitude);

  const close = 1e-12;
  const onEdge = (p: GpsCoordinate, q: GpsCoordinate, r: GpsCoordinate) =>
    Math.abs(turn(p, q, r)) <= close &&
    r.longitude >= Math.min(p.longitude, q.longitude) - close &&
    r.longitude <= Math.max(p.longitude, q.longitude) + close &&
    r.latitude >= Math.min(p.latitude, q.latitude) - close &&
    r.latitude <= Math.max(p.latitude, q.latitude) + close;

  const t1 = turn(a, b, c), t2 = turn(a, b, d);
  const t3 = turn(c, d, a), t4 = turn(c, d, b);
  const crosses = ((t1 > close && t2 < -close) || (t1 < -close && t2 > close)) &&
                  ((t3 > close && t4 < -close) || (t3 < -close && t4 > close));
  return crosses || onEdge(a, b, c) || onEdge(a, b, d) ||
    onEdge(c, d, a) || onEdge(c, d, b);
}

export function hasSelfIntersection(points: GpsCoordinate[]): boolean {
  const vertices = [...points];
  if (vertices.length > 1 && samePoint(vertices[0], vertices[vertices.length - 1])) {
    vertices.pop(); // Do not count the repeated closing point twice.
  }
  if (vertices.length < 3) return true;

  for (let i = 0; i < vertices.length; i++) {
    const nextI = (i + 1) % vertices.length;
    for (let j = i + 1; j < vertices.length; j++) {
      const nextJ = (j + 1) % vertices.length;
      if (i === j || nextI === j || nextJ === i) continue; // Neighbor edges share a corner.
      if (edgesCross(vertices[i], vertices[nextI], vertices[j], vertices[nextJ])) {
        return true;
      }
    }
  }
  return false;
}

// 4. Find one valid loop that ends at the LAST supplied GPS point.
export function detectTerritoryCandidate(points: GpsCoordinate[]): TerritoryCandidate | null {
  if (points.length < MIN_LOOP_POINTS) return null;
  const endIndex = points.length - 1;

  for (let startIndex = 0; startIndex <= points.length - MIN_LOOP_POINTS; startIndex++) {
    if (calculateGpsDistance(points[startIndex], points[endIndex]) > CLOSE_ENOUGH_METERS) {
      continue;
    }

    const loop = points.slice(startIndex);
    const boundary = samePoint(loop[0], loop[loop.length - 1])
      ? loop
      : [...loop, loop[0]];

    if (hasSelfIntersection(boundary)) continue;
    const area = calculatePolygonArea(loop);
    if (!Number.isFinite(area) || area < MIN_AREA_SQUARE_METERS) continue;

    return { startIndex, endIndex, boundary, areaSquareMeters: area };
  }
  return null;
}

// 5. A continuous GPS section may contain more than one valid loop.
export function detectMultipleTerritories(points: GpsCoordinate[]): TerritoryCandidate[] {
  const result: TerritoryCandidate[] = [];
  let start = 0;

  while (start <= points.length - MIN_LOOP_POINTS) {
    let found: TerritoryCandidate | null = null;

    for (let end = start + MIN_LOOP_POINTS - 1; end < points.length; end++) {
      const loop = detectTerritoryCandidate(points.slice(start, end + 1));
      if (!loop) continue;

      found = {
        ...loop,
        startIndex: start + loop.startIndex,
        endIndex: start + loop.endIndex,
      };
      break;
    }

    if (found) {
      result.push(found);
      start = found.endIndex; // Allow next loop to share the closing point.
    } else {
      start++; // Earlier points did not form a loop; look later in the route.
    }
  }
  return result;
}

// 6. Reject an unreliable GPS point.
function isUsablePoint(p: LegGpsPoint): boolean {
  return p.timestamp != null && Number.isFinite(p.timestamp) &&
    Number.isFinite(p.latitude) && p.latitude >= -90 && p.latitude <= 90 &&
    Number.isFinite(p.longitude) && p.longitude >= -180 && p.longitude <= 180 &&
    (p.accuracy == null || (Number.isFinite(p.accuracy) &&
      p.accuracy >= 0 && p.accuracy <= MAX_ACCURACY_METERS));
}

// 7. Reject impossible movements, pause boundaries, and long GPS gaps.
export function isGpsMovementValid(previous: LegGpsPoint, current: LegGpsPoint): boolean {
  if (previous.legNo !== current.legNo) return false;
  if (!isUsablePoint(previous) || !isUsablePoint(current)) return false;

  const seconds = (current.timestamp! - previous.timestamp!) / 1000;
  if (seconds <= 0 || seconds > MAX_GAP_SECONDS) return false;

  const speed = calculateGpsDistance(previous, current) / seconds;
  return Number.isFinite(speed) && speed <= MAX_SPEED_METERS_PER_SECOND;
}

// 8. Detect each valid loop BEFORE and AFTER a pause/GPS gap independently.
export function detectTerritoriesByLeg(points: LegGpsPoint[]): TerritoryCandidate[] {
  const territories: TerritoryCandidate[] = [];
  type IndexedPoint = { point: LegGpsPoint; originalIndex: number };

  // Divide points into consecutive legs (not simply all points with the same number).
  const legs: IndexedPoint[][] = [];
  points.forEach((point, originalIndex) => {
    const lastLeg = legs[legs.length - 1];
    if (!lastLeg || lastLeg[0].point.legNo !== point.legNo) legs.push([]);
    legs[legs.length - 1].push({ point, originalIndex });
  });

  // Save any completed loops in a continuous GPS section.
  function saveLoops(section: IndexedPoint[]): void {
    if (section.length < MIN_LOOP_POINTS) return;
    const coordinates = section.map(entry => ({
      latitude: entry.point.latitude,
      longitude: entry.point.longitude,
    }));

    for (const loop of detectMultipleTerritories(coordinates)) {
      territories.push({
        ...loop,
        startIndex: section[loop.startIndex].originalIndex,
        endIndex: section[loop.endIndex].originalIndex,
      });
    }
  }

  for (const leg of legs) {
    // Compatibility with old simulated tests only; NOT safe for real claims.
    if (leg.every(entry => entry.point.timestamp == null)) {
      saveLoops(leg);
      continue;
    }

    let section: IndexedPoint[] = [];
    for (let i = 0; i < leg.length; i++) {
      const current = leg[i];
      const previous = section[section.length - 1]?.point;
      const next = leg[i + 1]?.point;

      if (!isUsablePoint(current.point)) {
        // Skip one bad reading if the points around it reconnect safely.
        if (previous && next && isUsablePoint(next) &&
            isGpsMovementValid(previous, next)) continue;
        saveLoops(section);
        section = [];
        continue;
      }

      if (!previous || isGpsMovementValid(previous, current.point)) {
        section.push(current);
        continue;
      }

      // One isolated jump? Ignore it when the next point reconnects safely.
      if (next && isUsablePoint(next) && isGpsMovementValid(previous, next)) continue;

      // Pause/gap/bad movement: keep earlier loops, then start a fresh section.
      saveLoops(section);
      section = [current];
    }
    saveLoops(section); // Include the final part of the leg.
  }
  return territories;
}
