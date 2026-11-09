import * as TaskManager from 'expo-task-manager';
import * as Location from 'expo-location';
import {
  initializeRunStorage,
  getActiveRunId,
  saveGpsPoint,
} from './runStorage';
import { syncRun } from './runSync';
import { finishIdleRunIfDue, stopRunClosedByServer } from './idleFinish';

export const BACKGROUND_LOCATION_TASK = 'ILAQA_BACKGROUND_LOCATION';

const stopBackgroundUpdates = async () => {
  if (await Location.hasStartedLocationUpdatesAsync(BACKGROUND_LOCATION_TASK)) {
    await Location.stopLocationUpdatesAsync(BACKGROUND_LOCATION_TASK);
  }
};

TaskManager.defineTask(BACKGROUND_LOCATION_TASK, async ({ data, error }) => {
  if (error) {
    console.error('ILAQA background GPS error:', error);
    return;
  }
  const locations = (
    data as { locations?: Location.LocationObject[] } | undefined
  )?.locations;
  if (!locations?.length) return;

  try {
    await initializeRunStorage();
    const runId = await getActiveRunId();
    if (!runId) return;
    for (const item of locations) {
      const { latitude, longitude, accuracy } = item.coords;
      if (
        !Number.isFinite(latitude) ||
        !Number.isFinite(longitude) ||
        latitude < -90 ||
        latitude > 90 ||
        longitude < -180 ||
        longitude > 180
      )
        continue;
      // Match the backend's 30-meter accuracy threshold.
      if (
        accuracy == null ||
        !Number.isFinite(accuracy) ||
        accuracy > 30 ||
        accuracy < 0
      )
        continue;
      await saveGpsPoint(runId, latitude, longitude, accuracy, item.timestamp);
    }
    // Best-effort background upload, at most every 5 s (this task fires on
    // every GPS fix). Android can delay/suspend tasks; the HomeScreen timer
    // retries and SQLite retains unsent points.
    // Finish a forgotten run: no 20 m of movement for 5 minutes (L3).
    // The screen notices the run is gone and shows that it finished.
    try {
      await finishIdleRunIfDue(stopBackgroundUpdates);
    } catch (idleError) {
      console.log('ILAQA idle check failed:', String(idleError));
    }
    try {
      const result = await syncRun(runId, { minIntervalMs: 5000 });
      // The server had already closed this run: stop recording into it.
      if (result.serverEnded) {
        await stopRunClosedByServer(runId, stopBackgroundUpdates);
      }
    } catch (syncError) {
      console.log('ILAQA background sync queued:', String(syncError));
    }
  } catch (err) {
    console.error('ILAQA: Unable to save background GPS points:', err);
  }
});
