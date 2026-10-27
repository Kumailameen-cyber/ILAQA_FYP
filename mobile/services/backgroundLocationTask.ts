
import * as TaskManager from 'expo-task-manager';
import * as Location from 'expo-location';

import {
  initializeRunStorage,
  getActiveRunId,
  saveGpsPoint,
} from './runStorage';

export const BACKGROUND_LOCATION_TASK = 'ILAQA_BACKGROUND_LOCATION';

TaskManager.defineTask(
  BACKGROUND_LOCATION_TASK,
  async ({ data, error }) => {
    if (error) {
      console.error('ILAQA background GPS error:', error);
      return;
    }

    const locations = (
      data as { locations?: Location.LocationObject[] } | undefined
    )?.locations;

    if (!locations || locations.length === 0) {
      return;
    }

    try {
      // Make sure the SQLite tables exist.
      await initializeRunStorage();

      // Find the run currently being recorded.
      const runId = await getActiveRunId();

      if (!runId) {
        return;
      }

      for (const location of locations) {
        const {
          latitude,
          longitude,
          accuracy,
        } = location.coords;

        // Reject invalid coordinates.
        if (
          !Number.isFinite(latitude) ||
          !Number.isFinite(longitude) ||
          latitude < -90 ||
          latitude > 90 ||
          longitude < -180 ||
          longitude > 180
        ) {
          continue;
        }

        // Ignore inaccurate GPS readings.
        if (
          accuracy !== null &&
          (!Number.isFinite(accuracy) || accuracy > 40)
        ) {
          continue;
        }

        // saveGpsPoint checks is_recording before inserting.
        await saveGpsPoint(
          runId,
          latitude,
          longitude,
          accuracy,
          location.timestamp
        );
      }
    } catch (err) {
      console.error(
        'ILAQA: Unable to save background GPS points:',
        err
      );
    }
  }
);
