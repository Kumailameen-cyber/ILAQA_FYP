// Background GPS: the task that records run points while the app is in the
// background, and starting/stopping Android's location service for it.

import * as TaskManager from 'expo-task-manager';
import * as Location from 'expo-location';
import {
  initializeRunStorage,
  getActiveRunId,
  saveGpsPoint,
} from './runStorage';
import { syncRun } from './runSync';
import { finishIdleRunIfDue, stopRunClosedByServer } from './idleFinish';

const BACKGROUND_LOCATION_TASK = 'ILAQA_BACKGROUND_LOCATION';

// Asks for background permission and starts the location service.
export async function startBackgroundTracking(): Promise<void> {
  const foregroundPermission = await Location.getForegroundPermissionsAsync();

  if (foregroundPermission.status !== 'granted') {
    throw new Error('Foreground location permission is required.');
  }

  const backgroundPermission = await Location.requestBackgroundPermissionsAsync();

  if (backgroundPermission.status !== 'granted') {
    throw new Error(
      'Background location permission is required to track runs while the app is minimized.'
    );
  }

  if (await Location.hasStartedLocationUpdatesAsync(BACKGROUND_LOCATION_TASK)) {
    return;
  }

  await Location.startLocationUpdatesAsync(BACKGROUND_LOCATION_TASK, {
    accuracy: Location.Accuracy.High,
    distanceInterval: 5, // FR-06: record a point every 5 m
    timeInterval: 1000,
    pausesUpdatesAutomatically: false,
    foregroundService: {
      notificationTitle: 'ILAQA Run Tracking',
      notificationBody: 'Your running route is being recorded.',
      notificationColor: '#C6FF35',
    },
  });
}

export async function stopBackgroundTracking(): Promise<void> {
  if (await Location.hasStartedLocationUpdatesAsync(BACKGROUND_LOCATION_TASK)) {
    await Location.stopLocationUpdatesAsync(BACKGROUND_LOCATION_TASK);
  }
}

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
    // Finish a forgotten run: no 20 m of movement for 5 minutes (L3).
    // The screen notices the run is gone and shows that it finished.
    try {
      await finishIdleRunIfDue(stopBackgroundTracking);
    } catch (idleError) {
      console.log('ILAQA idle check failed:', String(idleError));
    }
    // Best-effort background upload, at most every 5 s (this task fires on
    // every GPS fix). Android can delay/suspend tasks; the Home screen timer
    // retries and SQLite retains unsent points.
    try {
      const result = await syncRun(runId, { minIntervalMs: 5000 });
      // The server had already closed this run: stop recording into it.
      if (result.serverEnded) {
        await stopRunClosedByServer(runId, stopBackgroundTracking);
      }
    } catch (syncError) {
      console.log('ILAQA background sync queued:', String(syncError));
    }
  } catch (err) {
    console.error('ILAQA: Unable to save background GPS points:', err);
  }
});
