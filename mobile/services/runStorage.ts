import * as SQLite from 'expo-sqlite';

const DATABASE_NAME = 'ilaqa_runs.db';

export type StoredGpsPoint = {
  id: number;
  runId: string;
  latitude: number;
  longitude: number;
  accuracy: number | null;
  timestamp: number;
  legNo: number;
};

export type ConfirmedTerritory = {
  territoryId: string;
  loopIndex?: number;
  areaSquareMeters: number;
  boundary: { latitude: number; longitude: number }[];
  // From GET /api/runs/territories (map view).
  colorHex?: string;
  ownerLabel?: string;
  isMine?: boolean;
  isShielded?: boolean;
};

export type RunSyncRecord = {
  runId: string;
  ownerUserId: string;
  startedAt: string;
  uploadedThroughId: number;
  finishRequested: number;
  serverEnded: number;
};

let databasePromise: Promise<SQLite.SQLiteDatabase> | null = null;
let initializationPromise: Promise<void> | null = null;

async function getDatabase(): Promise<SQLite.SQLiteDatabase> {
  if (!databasePromise)
    databasePromise = SQLite.openDatabaseAsync(DATABASE_NAME);
  return databasePromise;
}

export async function initializeRunStorage(): Promise<void> {
  if (!initializationPromise) {
    initializationPromise = (async () => {
      const db = await getDatabase();
      // Preserve the user's existing GPS and active-run tables.
      await db.execAsync(`
        CREATE TABLE IF NOT EXISTS gps_points (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          run_id TEXT NOT NULL,
          latitude REAL NOT NULL,
          longitude REAL NOT NULL,
          accuracy REAL,
          timestamp INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_gps_points_run_id
          ON gps_points (run_id, timestamp);
        CREATE TABLE IF NOT EXISTS active_run (
          id INTEGER PRIMARY KEY CHECK (id = 1),
          run_id TEXT NOT NULL,
          is_recording INTEGER NOT NULL DEFAULT 0
        );
      `);

      const activeColumns = await db.getAllAsync<{ name: string }>(
        'PRAGMA table_info(active_run)'
      );
      if (!activeColumns.some((col) => col.name === 'is_recording')) {
        await db.execAsync(
          'ALTER TABLE active_run ADD COLUMN is_recording INTEGER NOT NULL DEFAULT 0;'
        );
      }
      if (!activeColumns.some((col) => col.name === 'current_leg')) {
        await db.execAsync(
          'ALTER TABLE active_run ADD COLUMN current_leg INTEGER NOT NULL DEFAULT 1;'
        );
      }

      const gpsColumns = await db.getAllAsync<{ name: string }>(
        'PRAGMA table_info(gps_points)'
      );
      if (!gpsColumns.some((col) => col.name === 'leg_no')) {
        await db.execAsync(
          'ALTER TABLE gps_points ADD COLUMN leg_no INTEGER NOT NULL DEFAULT 1;'
        );
      }

      // New local sync queue: no columns are removed from existing tables.
      await db.execAsync(`
        CREATE TABLE IF NOT EXISTS run_sync (
          run_id TEXT PRIMARY KEY,
          owner_user_id TEXT NOT NULL,
          started_at TEXT NOT NULL,
          uploaded_through_id INTEGER NOT NULL DEFAULT 0,
          finish_requested INTEGER NOT NULL DEFAULT 0,
          server_ended INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS idx_run_sync_owner
          ON run_sync (owner_user_id, server_ended);
        CREATE TABLE IF NOT EXISTS confirmed_territories (
          territory_id TEXT PRIMARY KEY,
          run_id TEXT NOT NULL,
          owner_user_id TEXT NOT NULL,
          loop_index INTEGER,
          area_square_meters REAL NOT NULL,
          boundary_json TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_confirmed_territories_owner
          ON confirmed_territories (owner_user_id);
      `);
    })().catch((error) => {
      initializationPromise = null;
      throw error;
    });
  }
  return initializationPromise;
}

// Only the registered background GPS task writes points; the foreground
// watcher is only for the live map. This avoids double recording.
export async function saveGpsPoint(
  runId: string,
  latitude: number,
  longitude: number,
  accuracy: number | null,
  timestamp: number
): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  await db.runAsync(
    `INSERT INTO gps_points
       (run_id, latitude, longitude, accuracy, timestamp, leg_no)
     SELECT ?, ?, ?, ?, ?, current_leg FROM active_run
     WHERE run_id = ? AND is_recording = 1
       AND NOT EXISTS (
         SELECT 1 FROM gps_points
         WHERE run_id = ? AND latitude = ? AND longitude = ? AND timestamp = ?
       )`,
    [
      runId,
      latitude,
      longitude,
      accuracy,
      timestamp,
      runId,
      runId,
      latitude,
      longitude,
      timestamp,
    ]
  );
}

export async function resumeRunRecording(runId: string): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  const result = await db.runAsync(
    `UPDATE active_run SET current_leg = current_leg + 1,
       is_recording = 1 WHERE run_id = ? AND is_recording = 0`,
    [runId]
  );
  if (result.changes !== 1) {
    throw new Error('Unable to resume run: no matching paused run.');
  }
}

export async function getRunPoints(runId: string): Promise<StoredGpsPoint[]> {
  await initializeRunStorage();
  const db = await getDatabase();
  return db.getAllAsync<StoredGpsPoint>(
    `SELECT id, run_id AS runId, leg_no AS legNo,
       latitude, longitude, accuracy, timestamp
     FROM gps_points WHERE run_id = ? ORDER BY timestamp ASC, id ASC`,
    [runId]
  );
}

export async function setActiveRun(runId: string): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  await db.runAsync(
    `INSERT INTO active_run (id, run_id, is_recording, current_leg)
     VALUES (1, ?, 0, 1)
     ON CONFLICT(id) DO UPDATE SET
       run_id = excluded.run_id, is_recording = 0, current_leg = 1`,
    [runId]
  );
}

export async function getActiveRunId(): Promise<string | null> {
  await initializeRunStorage();
  const db = await getDatabase();
  const row = await db.getFirstAsync<{ run_id: string }>(
    'SELECT run_id FROM active_run WHERE id = 1'
  );
  return row?.run_id ?? null;
}

export async function setRunRecording(
  runId: string,
  isRecording: boolean
): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  await db.runAsync('UPDATE active_run SET is_recording = ? WHERE run_id = ?', [
    isRecording ? 1 : 0,
    runId,
  ]);
}

export async function clearActiveRun(): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  await db.runAsync('DELETE FROM active_run WHERE id = 1');
}

// ==== Offline-safe upload queue (new) ====

export async function registerRunForSync(
  runId: string,
  ownerUserId: string,
  startedAt: string
): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  await db.runAsync(
    `INSERT OR IGNORE INTO run_sync
       (run_id, owner_user_id, started_at)
     VALUES (?, ?, ?)`,
    [runId, ownerUserId, startedAt]
  );
}

export async function getRunSyncRecord(
  runId: string
): Promise<RunSyncRecord | null> {
  await initializeRunStorage();
  const db = await getDatabase();
  const row = await db.getFirstAsync<RunSyncRecord>(
    `SELECT run_id AS runId, owner_user_id AS ownerUserId,
       started_at AS startedAt, uploaded_through_id AS uploadedThroughId,
       finish_requested AS finishRequested, server_ended AS serverEnded
     FROM run_sync WHERE run_id = ?`,
    [runId]
  );
  return row ?? null;
}

export async function getPendingRunIds(ownerUserId: string): Promise<string[]> {
  await initializeRunStorage();
  const db = await getDatabase();
  const rows = await db.getAllAsync<{ run_id: string }>(
    `SELECT run_id FROM run_sync
     WHERE owner_user_id = ? AND server_ended = 0`,
    [ownerUserId]
  );
  return rows.map((row) => row.run_id);
}

export async function getUnuploadedRunPoints(
  runId: string,
  uploadedThroughId: number,
  limit = 100
): Promise<StoredGpsPoint[]> {
  await initializeRunStorage();
  const db = await getDatabase();
  return db.getAllAsync<StoredGpsPoint>(
    `SELECT id, run_id AS runId, leg_no AS legNo,
       latitude, longitude, accuracy, timestamp
     FROM gps_points WHERE run_id = ? AND id > ?
     ORDER BY id ASC LIMIT ?`,
    [runId, uploadedThroughId, limit]
  );
}

export async function markPointsUploaded(
  runId: string,
  throughId: number
): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  await db.runAsync(
    `UPDATE run_sync
     SET uploaded_through_id = MAX(uploaded_through_id, ?)
     WHERE run_id = ?`,
    [throughId, runId]
  );
}

export async function markRunFinishRequested(runId: string): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  await db.runAsync(
    'UPDATE run_sync SET finish_requested = 1 WHERE run_id = ?',
    [runId]
  );
}

export async function markServerRunEnded(runId: string): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  await db.runAsync('UPDATE run_sync SET server_ended = 1 WHERE run_id = ?', [
    runId,
  ]);
}

export async function cacheConfirmedTerritories(
  ownerUserId: string,
  runId: string,
  territories: ConfirmedTerritory[]
): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  for (const territory of territories) {
    if (!territory.territoryId || !Array.isArray(territory.boundary)) continue;
    await db.runAsync(
      `INSERT INTO confirmed_territories
         (territory_id, run_id, owner_user_id, loop_index,
          area_square_meters, boundary_json)
       VALUES (?, ?, ?, ?, ?, ?)
       ON CONFLICT(territory_id) DO UPDATE SET
         loop_index = excluded.loop_index,
         area_square_meters = excluded.area_square_meters,
         boundary_json = excluded.boundary_json`,
      [
        territory.territoryId,
        runId,
        ownerUserId,
        territory.loopIndex ?? null,
        territory.areaSquareMeters,
        JSON.stringify(territory.boundary),
      ]
    );
  }
}

export async function getConfirmedTerritories(
  ownerUserId: string
): Promise<ConfirmedTerritory[]> {
  await initializeRunStorage();
  const db = await getDatabase();
  const rows = await db.getAllAsync<{
    territoryId: string;
    loopIndex: number | null;
    areaSquareMeters: number;
    boundaryJson: string;
  }>(
    `SELECT territory_id AS territoryId, loop_index AS loopIndex,
       area_square_meters AS areaSquareMeters,
       boundary_json AS boundaryJson
     FROM confirmed_territories WHERE owner_user_id = ?`,
    [ownerUserId]
  );
  return rows.flatMap((row) => {
    try {
      const boundary = JSON.parse(row.boundaryJson);
      if (!Array.isArray(boundary) || boundary.length < 4) return [];
      return [
        {
          territoryId: row.territoryId,
          loopIndex: row.loopIndex ?? undefined,
          areaSquareMeters: row.areaSquareMeters,
          boundary,
        },
      ];
    } catch {
      return [];
    }
  });
}

export async function inspectSavedRuns() {
  await initializeRunStorage();
  const db = await getDatabase();

  return db.getAllAsync<{
    runId: string;
    pointCount: number;
    firstTimestamp: number;
    lastTimestamp: number;
    hasSyncRecord: number;
  }>(`
    SELECT
      p.run_id AS runId,
      COUNT(*) AS pointCount,
      MIN(p.timestamp) AS firstTimestamp,
      MAX(p.timestamp) AS lastTimestamp,
      CASE WHEN s.run_id IS NULL THEN 0 ELSE 1 END AS hasSyncRecord
    FROM gps_points p
    LEFT JOIN run_sync s ON s.run_id = p.run_id
    GROUP BY p.run_id
    ORDER BY lastTimestamp DESC
  `);
}
