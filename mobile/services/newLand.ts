// Which of the runner's territories are new since the last map refresh.
// Used to say "Territory claimed · +N m²" and to briefly highlight the new
// land when a live claim appears. Pure logic: no React, no network.

export type LandSummary = {
  territoryId: string;
  areaSquareMeters: number;
  isMine?: boolean;
};

export type NewLandResult = {
  // Every territory id on the map now (pass back in next time).
  knownIds: Set<string>;
  // The runner's own territories that were not on the map last time.
  newIds: string[];
  newAreaSquareMeters: number;
};

// knownIds = null means this is the first load: remember everything and
// announce nothing (land the runner already owned is not "new").
export function findNewOwnLand(
  knownIds: Set<string> | null,
  territories: LandSummary[]
): NewLandResult {
  const nowIds = new Set(territories.map((t) => t.territoryId));

  if (knownIds === null) {
    return { knownIds: nowIds, newIds: [], newAreaSquareMeters: 0 };
  }

  const fresh = territories.filter(
    (t) => t.isMine === true && !knownIds.has(t.territoryId)
  );

  return {
    knownIds: nowIds,
    newIds: fresh.map((t) => t.territoryId),
    newAreaSquareMeters: fresh.reduce((sum, t) => sum + t.areaSquareMeters, 0),
  };
}
