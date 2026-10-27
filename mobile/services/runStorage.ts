
import * as SQLite from 'expo-sqlite';

const DATABASE_NAME = 'ilaqa_runs.db';

// Structure of a GPS point saved on the phone.
export type StoredGpsPoint = {
  id: number;
  runId: string;
  latitude: number;
  longitude: number;
  accuracy: number | null;
  timestamp: number;
  legNo: number;
};

// Reuse the same database connection.
let databasePromise: Promise<SQLite.SQLiteDatabase> | null = null;
let initializationPromise: Promise<void> | null = null;

async function getDatabase(): Promise<SQLite.SQLiteDatabase> {
  if (!databasePromise) {
    databasePromise = SQLite.openDatabaseAsync(DATABASE_NAME);
  }

  return databasePromise;
}

// Create the tables needed for GPS tracking.
export async function initializeRunStorage(): Promise<void> {
  if (!initializationPromise) {
    initializationPromise = (async () => {
      const db = await getDatabase();

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

      // Support databases created before is_recording existed.
      const columns = await db.getAllAsync<{ name: string }>(
        'PRAGMA table_info(active_run)'
      );

      const hasRecordingColumn = columns.some(
        (column) => column.name === 'is_recording'
      );

      if (!hasRecordingColumn) {
        await db.execAsync(`
          ALTER TABLE active_run
          ADD COLUMN is_recording INTEGER NOT NULL DEFAULT 0;
        `);
      }
const hasLegColumn = columns.some(
  (column) => column.name === 'current_leg'
);

if (!hasLegColumn) {
  await db.execAsync(`
    ALTER TABLE active_run
    ADD COLUMN current_leg INTEGER NOT NULL DEFAULT 1;
  `);
}

const gpsColumns = await db.getAllAsync<{ name: string }>(
  'PRAGMA table_info(gps_points)'
);

const hasGpsLegColumn = gpsColumns.some(
  (column) => column.name === 'leg_no'
);

if (!hasGpsLegColumn) {
  await db.execAsync(`
    ALTER TABLE gps_points
    ADD COLUMN leg_no INTEGER NOT NULL DEFAULT 1;
  `);
}

    })().catch((error) => {
      initializationPromise = null;
      throw error;
    });
  }

  return initializationPromise;
}

// Save a GPS point only when the correct run is recording.
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
   SELECT ?, ?, ?, ?, ?, current_leg
   FROM active_run
   WHERE run_id = ?
     AND is_recording = 1`,
  [
    runId,
    latitude,
    longitude,
    accuracy,
    timestamp,
    runId,
  ]
);
}
export async function resumeRunRecording(
  runId: string
): Promise<void> {
  await initializeRunStorage();

  const db = await getDatabase();

  const result = await db.runAsync(
    `UPDATE active_run
     SET current_leg = current_leg + 1,
         is_recording = 1
     WHERE run_id = ?
       AND is_recording = 0`,
    [runId]
  );

  if (result.changes !== 1) {
    throw new Error(
      'Unable to resume run: no matching paused run.'
    );
  }
}



// Get all GPS points belonging to one run.
export async function getRunPoints(
  runId: string
): Promise<StoredGpsPoint[]> {
  await initializeRunStorage();

  const db = await getDatabase();

  
return db.getAllAsync<StoredGpsPoint>(
  `SELECT
     id,
     run_id AS runId,
     leg_no AS legNo,
     latitude,
     longitude,
     accuracy,
     timestamp
   FROM gps_points
   WHERE run_id = ?
   ORDER BY timestamp ASC, id ASC`,
  [runId]
);

}

// Create a new active run.
// Recording starts disabled until GPS starts successfully.
export async function setActiveRun(
  runId: string
): Promise<void> {
  await initializeRunStorage();

  const db = await getDatabase();

  await db.runAsync(
    `INSERT INTO active_run (id, run_id, is_recording)
     VALUES (1, ?, 0)
     ON CONFLICT(id)
     DO UPDATE SET
       run_id = excluded.run_id,
       is_recording = 0`,
    [runId]
  );
}

// Find the currently active run.
export async function getActiveRunId(): Promise<string | null> {
  await initializeRunStorage();

  const db = await getDatabase();

  const result = await db.getFirstAsync<{ run_id: string }>(
    'SELECT run_id FROM active_run WHERE id = 1'
  );

  return result?.run_id ?? null;
}

// Enable or disable recording without losing the run ID.
export async function setRunRecording(
  runId: string,
  isRecording: boolean
): Promise<void> {
  await initializeRunStorage();

  const db = await getDatabase();

  await db.runAsync(
    `UPDATE active_run
     SET is_recording = ?
     WHERE run_id = ?`,
    [isRecording ? 1 : 0, runId]
  );
}

// Remove the active-run marker.
// Previously saved GPS points remain in gps_points.
export async function clearActiveRun(): Promise<void> {
  await initializeRunStorage();

  const db = await getDatabase();

  await db.runAsync(
    'DELETE FROM active_run WHERE id = 1'
  );
}
