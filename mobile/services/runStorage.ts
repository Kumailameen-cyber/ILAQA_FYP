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
  // When Finish was pressed and the running time, sent with /end.
  endedAt: string | null;
  activeSeconds: number | null;
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
      // Run timer, so a run survives tab switches and app restarts.
      if (!activeColumns.some((col) => col.name === 'active_ms_before')) {
        await db.execAsync(
          'ALTER TABLE active_run ADD COLUMN active_ms_before INTEGER NOT NULL DEFAULT 0;'
        );
      }
      if (!activeColumns.some((col) => col.name === 'segment_started_at')) {
        await db.execAsync(
          'ALTER TABLE active_run ADD COLUMN segment_started_at INTEGER;'
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

      // Finish time and active seconds sent with /end (offline-safe).
      const syncColumns = await db.getAllAsync<{ name: string }>(
        'PRAGMA table_info(run_sync)'
      );
      if (!syncColumns.some((col) => col.name === 'ended_at')) {
        await db.execAsync('ALTER TABLE run_sync ADD COLUMN ended_at TEXT;');
      }
      if (!syncColumns.some((col) => col.name === 'active_seconds')) {
        await db.execAsync(
          'ALTER TABLE run_sync ADD COLUMN active_seconds INTEGER;'
        );
      }
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

// Starts tracking a new run. Never replaces another unfinished run: that
// run would lose its Finish and stay "Running" on the server.
export async function setActiveRun(runId: string): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  const existing = await getActiveRunId();
  if (existing && existing !== runId) {
    throw new Error('Another run is still active.');
  }
  await db.runAsync(
    `INSERT INTO active_run
       (id, run_id, is_recording, current_leg, active_ms_before, segment_started_at)
     VALUES (1, ?, 0, 1, 0, NULL)
     ON CONFLICT(id) DO UPDATE SET
       run_id = excluded.run_id, is_recording = 0, current_leg = 1,
       active_ms_before = 0, segment_started_at = NULL`,
    [runId]
  );
}

export type ActiveRunState = {
  runId: string;
  isRecording: boolean;
  currentLeg: number;
  // Running time of finished segments, in milliseconds.
  activeMsBefore: number;
  // When the current running segment started (ms), null while paused.
  segmentStartedAt: number | null;
};

export async function getActiveRunState(): Promise<ActiveRunState | null> {
  await initializeRunStorage();
  const db = await getDatabase();
  const row = await db.getFirstAsync<{
    runId: string;
    isRecording: number;
    currentLeg: number;
    activeMsBefore: number;
    segmentStartedAt: number | null;
  }>(
    `SELECT run_id AS runId, is_recording AS isRecording,
       current_leg AS currentLeg, active_ms_before AS activeMsBefore,
       segment_started_at AS segmentStartedAt
     FROM active_run WHERE id = 1`
  );
  if (!row) return null;
  return {
    runId: row.runId,
    isRecording: row.isRecording === 1,
    currentLeg: row.currentLeg,
    activeMsBefore: row.activeMsBefore ?? 0,
    segmentStartedAt: row.segmentStartedAt ?? null,
  };
}

// Saves the run timer so it can be restored after a tab switch or restart.
export async function saveRunTiming(
  runId: string,
  activeMsBefore: number,
  segmentStartedAt: number | null
): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  await db.runAsync(
    `UPDATE active_run SET active_ms_before = ?, segment_started_at = ?
     WHERE run_id = ?`,
    [Math.max(0, Math.round(activeMsBefore)), segmentStartedAt, runId]
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
       finish_requested AS finishRequested, server_ended AS serverEnded,
       ended_at AS endedAt, active_seconds AS activeSeconds
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

// endedAt/activeSeconds are kept from the first Finish if it is repeated.
export async function markRunFinishRequested(
  runId: string,
  endedAt?: string,
  activeSeconds?: number
): Promise<void> {
  await initializeRunStorage();
  const db = await getDatabase();
  await db.runAsync(
    `UPDATE run_sync SET finish_requested = 1,
       ended_at = COALESCE(ended_at, ?),
       active_seconds = COALESCE(active_seconds, ?)
     WHERE run_id = ?`,
    [
      endedAt ?? null,
      activeSeconds === undefined ? null : Math.max(0, Math.floor(activeSeconds)),
      runId,
    ]
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
