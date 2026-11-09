// GPS maths shared by the live map, the idle (forgotten run) rule and
// territory detection.

export const EARTH_RADIUS_METERS = 6371000;

export type GpsCoordinate = {
  latitude: number;
  longitude: number;
};

// Distance between two GPS coordinates in meters (Haversine formula).
export function calculateGpsDistance(a: GpsCoordinate, b: GpsCoordinate): number {
  const radians = (degrees: number) => (degrees * Math.PI) / 180;
  const lat = radians(b.latitude - a.latitude);
  const lon = radians(b.longitude - a.longitude);
  const h =
    Math.sin(lat / 2) ** 2 +
    Math.cos(radians(a.latitude)) * Math.cos(radians(b.latitude)) *
      Math.sin(lon / 2) ** 2;
  return 2 * EARTH_RADIUS_METERS * Math.atan2(Math.sqrt(h), Math.sqrt(Math.max(0, 1 - h)));
}
