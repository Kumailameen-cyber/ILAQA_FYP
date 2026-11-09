// Run tracking for the Home screen: GPS permission and position, the live
// route line, Start / Pause / Resume / Finish, the run timer, restoring an
// unfinished run, auto-finishing a forgotten run, and uploading recorded GPS.
//
// Only the background task saves GPS points (services/backgroundLocationTask);
// the foreground watcher here only draws the live line on the map.

import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert } from 'react-native';
import MapView, { LatLng } from 'react-native-maps';
import * as Location from 'expo-location';
import * as Crypto from 'expo-crypto';

import {
  startBackgroundTracking,
  stopBackgroundTracking,
} from '../services/backgroundLocationTask';
import {
  setActiveRun,
  setRunRecording,
  clearActiveRun,
  getRunPoints,
  resumeRunRecording,
  registerRunForSync,
  markRunFinishRequested,
  getActiveRunState,
  saveRunTiming,
  ActiveRunState,
  getRunSyncRecord,
  getActiveRunId,
} from '../services/runStorage';
import { stopRunClosedByServer } from '../services/idleFinish';
import { syncRun, syncPendingRuns } from '../services/runSync';
import { calculateGpsDistance } from '../services/geo';

export type RunStatus = 'idle' | 'running' | 'paused';

const AUTO_FINISHED_MESSAGE =
  'Your run was finished automatically. Any land you closed still counts.';
const RUN_CLOSED_BY_SERVER_MESSAGE =
  'This run had no GPS for over 6 hours, so the server had already finished it. GPS recorded after that could not be added.';

// Zoom used when centering the map on the runner.
const MAP_DELTA = 0.003;

// Live route drawing limits (display only).
const LIVE_MAX_ACCURACY_METERS = 20;
const LIVE_MAX_SPEED_MPS = 8;

// Seconds -> "mm:ss" or "hh:mm:ss".
export function formatDuration(seconds: number): string {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const remainingSeconds = seconds % 60;

  const pad = (value: number) => String(value).padStart(2, '0');

  return hours > 0
    ? `${pad(hours)}:${pad(minutes)}:${pad(remainingSeconds)}`
    : `${pad(minutes)}:${pad(remainingSeconds)}`;
}

type Options = {
  runnerId: string;
  refreshTerritories: () => void;
  onLogout: () => void;
};

export function useRunTracking({
  runnerId,
  refreshTerritories,
  onLogout,
}: Options) {
  const mapRef = useRef<MapView>(null);
  const gpsSubscription = useRef<Location.LocationSubscription | null>(null);

  // Live line: last drawn point and its GPS time, and the current leg.
  const lastLivePointRef = useRef<LatLng | null>(null);
  const lastLiveTimeRef = useRef(0);
  const activeSegmentRef = useRef<LatLng[]>([]);

  // Wall-clock timer: running time of earlier legs + start of this leg.
  const elapsedBeforeSegmentRef = useRef(0);
  const segmentStartedAtRef = useRef<number | null>(null);

  // Refs read inside GPS callbacks and timers, which outlive one render.
  const busyRef = useRef(false);
  const statusRef = useRef<RunStatus>('idle');
  const activeRunIdRef = useRef<string | null>(null);

  const [location, setLocation] =
    useState<Location.LocationObjectCoords | null>(null);
  const [loading, setLoading] = useState(true);
  const [locationError, setLocationError] = useState('');
  const [runStatus, setRunStatus] = useState<RunStatus>('idle');
  const [routeSegments, setRouteSegments] = useState<LatLng[][]>([]);
  const [syncMessage, setSyncMessage] = useState('');
  const [distanceMeters, setDistanceMeters] = useState(0);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  // True while Start, Pause, Resume, Finish or Log Out is in progress.
  const [startingRun, setStartingRun] = useState(false);
  // True until an unfinished run in SQLite has been restored.
  const [restoringRun, setRestoringRun] = useState(true);

  const updateRunStatus = useCallback((status: RunStatus) => {
    statusRef.current = status;
    setRunStatus(status);
  }, []);

  const stopGpsWatcher = useCallback(() => {
    gpsSubscription.current?.remove();
    gpsSubscription.current = null;
  }, []);

  // Glide the camera to the runner, keeping the user's zoom and rotation.
  const followRunner = useCallback((point: LatLng) => {
    mapRef.current?.animateCamera({ center: point }, { duration: 900 });
  }, []);

  const centerMap = useCallback((latitude: number, longitude: number) => {
    mapRef.current?.animateToRegion(
      { latitude, longitude, latitudeDelta: MAP_DELTA, longitudeDelta: MAP_DELTA },
      700
    );
  }, []);

  // Adds the current leg's running time to the total and stops the leg clock.
  const closeTimerSegment = () => {
    const started = segmentStartedAtRef.current;
    if (started !== null) {
      elapsedBeforeSegmentRef.current += Date.now() - started;
    }
    segmentStartedAtRef.current = null;
  };

  // Removes a finished run from the screen.
  const clearRunFromScreen = useCallback(() => {
    activeRunIdRef.current = null;
    lastLivePointRef.current = null;
    activeSegmentRef.current = [];
    segmentStartedAtRef.current = null;
    elapsedBeforeSegmentRef.current = 0;
    setRouteSegments([]);
    setDistanceMeters(0);
    setElapsedSeconds(0);
    updateRunStatus('idle');
  }, [updateRunStatus]);

  // Sends a finished run's Finish now (or later, if offline) and shows its land.
  const sendFinish = useCallback(
    (runId: string) => {
      void syncRun(runId)
        .then(() => {
          // Takeovers and merges are applied at Finish.
          refreshTerritories();
          setSyncMessage('');
        })
        .catch((error) => {
          console.log('Finish queued for retry:', error);
          setSyncMessage('Run saved locally. Finish sync pending.');
        });
    },
    [refreshTerritories]
  );

  // The run finished by itself (idle, or closed by the server).
  const showRunEndedAutomatically = useCallback(
    (runId: string, message: string) => {
      stopGpsWatcher();
      clearRunFromScreen();
      Alert.alert('Run Finished', message);
      sendFinish(runId);
    },
    [stopGpsWatcher, clearRunFromScreen, sendFinish]
  );

  // First GPS position when the screen opens.
  useEffect(() => {
    let active = true;

    const loadLocation = async () => {
      try {
        const permission = await Location.requestForegroundPermissionsAsync();

        if (!active) return;

        if (permission.status !== 'granted') {
          setLocationError(
            'Location permission denied. Enable location access in your phone settings.'
          );
          return;
        }

        const currentLocation = await Location.getCurrentPositionAsync({
          accuracy: Location.Accuracy.High,
        });

        if (!active) return;

        setLocation(currentLocation.coords);
        centerMap(
          currentLocation.coords.latitude,
          currentLocation.coords.longitude
        );
      } catch {
        if (active) {
          setLocationError(
            'Unable to get your GPS location. Check location services.'
          );
        }
      } finally {
        if (active) setLoading(false);
      }
    };

    loadLocation();

    return () => {
      active = false;
      stopGpsWatcher();
    };
  }, [centerMap, stopGpsWatcher]);

  // Upload the current run's GPS and retry unsent runs every 5 seconds.
  // SQLite keeps the GPS while the phone is offline.
  useEffect(() => {
    let mounted = true;
    let busy = false;

    const syncAndRefresh = async () => {
      if (busy) return;
      busy = true;
      try {
        // Upload the current run first, then any pending finished runs.
        const current = activeRunIdRef.current;
        let endedNow = 0;
        let newLand = false;

        if (current) {
          const result = await syncRun(current);
          if (result.endedNow) endedNow++;
          if (result.newlyClaimedCount > 0) newLand = true;

          // The server had already finished this run (6 hours without
          // GPS): stop recording into it on the phone.
          if (
            result.serverEnded &&
            !busyRef.current &&
            activeRunIdRef.current === current &&
            (await stopRunClosedByServer(current, stopBackgroundTracking))
          ) {
            if (mounted) {
              showRunEndedAutomatically(current, RUN_CLOSED_BY_SERVER_MESSAGE);
            }
          }
        }

        endedNow += await syncPendingRuns(runnerId);

        // Show new or changed land at once instead of at the next check.
        if (mounted && (newLand || endedNow > 0)) {
          refreshTerritories();
        }

        if (mounted) setSyncMessage('');
      } catch (error) {
        console.log('ILAQA: live territory sync pending:', error);
        if (mounted) setSyncMessage('GPS saved locally. Server sync pending.');
      } finally {
        busy = false;
      }
    };

    void syncAndRefresh();
    const timer = setInterval(() => {
      void syncAndRefresh();
    }, 5000);

    return () => {
      mounted = false;
      clearInterval(timer);
    };
  }, [runnerId, refreshTerritories, showRunEndedAutomatically]);

  // Wall-clock timing keeps working when the phone is locked.
  useEffect(() => {
    if (runStatus !== 'running') return;

    const updateTimer = () => {
      const started = segmentStartedAtRef.current;
      if (started !== null) {
        setElapsedSeconds(
          Math.floor(
            (elapsedBeforeSegmentRef.current + Date.now() - started) / 1000
          )
        );
      }
    };

    updateTimer();
    const timer = setInterval(updateTimer, 1000);
    return () => clearInterval(timer);
  }, [runStatus]);

  // Resting never finishes a run. Every 15 s, check whether the background
  // task stopped this run because the server had already finished it
  // (abandoned-run safety net), and tell the runner.
  useEffect(() => {
    if (runStatus !== 'running') return;

    let checking = false;

    const check = async () => {
      const runId = activeRunIdRef.current;
      if (
        checking ||
        busyRef.current ||
        statusRef.current !== 'running' ||
        !runId
      ) {
        return;
      }
      checking = true;
      try {
        if (
          statusRef.current === 'running' &&
          activeRunIdRef.current === runId &&
          !busyRef.current &&
          (await getActiveRunId()) !== runId
        ) {
          // The background task stopped it (the server had finished it).
          showRunEndedAutomatically(runId, AUTO_FINISHED_MESSAGE);
        }
      } catch (error) {
        console.log('ILAQA: run state check failed:', error);
      } finally {
        checking = false;
      }
    };

    void check();
    const timer = setInterval(() => {
      void check();
    }, 15000);
    return () => clearInterval(timer);
  }, [runStatus, showRunEndedAutomatically]);

  // Watches GPS while running, to draw the live line and follow the runner.
  const startGpsWatcher = async () => {
    stopGpsWatcher();

    const subscription = await Location.watchPositionAsync(
      {
        accuracy: Location.Accuracy.High,
        distanceInterval: 5, // FR-06: record a point every 5 m
        timeInterval: 1000,
      },
      (position) => {
        if (statusRef.current !== 'running') return;

        const coords = position.coords;
        setLocation(coords);

        // Live line only (the server still receives every raw GPS point):
        // draw good fixes, ignore wobble while standing still and
        // impossible jumps, and soften zig-zags.
        const accuracy = coords.accuracy ?? LIVE_MAX_ACCURACY_METERS;
        if (accuracy > LIVE_MAX_ACCURACY_METERS) return;

        const raw: LatLng = {
          latitude: coords.latitude,
          longitude: coords.longitude,
        };
        let point = raw;

        const lastPoint = lastLivePointRef.current;

        if (lastPoint) {
          const rawDistance = calculateGpsDistance(lastPoint, raw);
          const seconds = Math.max(
            0.5,
            (position.timestamp - lastLiveTimeRef.current) / 1000
          );

          // Standing still: movement smaller than the fix's own error.
          if (rawDistance < Math.max(3, accuracy * 0.5)) return;

          // Faster than any runner: a GPS jump, not real movement.
          if (rawDistance / seconds > LIVE_MAX_SPEED_MPS) return;

          // Move most of the way to the new fix; good fixes count more.
          const weight = accuracy <= 8 ? 0.7 : 0.5;
          point = {
            latitude:
              lastPoint.latitude + (raw.latitude - lastPoint.latitude) * weight,
            longitude:
              lastPoint.longitude + (raw.longitude - lastPoint.longitude) * weight,
          };

          setDistanceMeters(
            (previous) => previous + calculateGpsDistance(lastPoint, point)
          );
        }

        lastLivePointRef.current = point;
        lastLiveTimeRef.current = position.timestamp;

        const nextSegment = [...activeSegmentRef.current, point];
        activeSegmentRef.current = nextSegment;
        setRouteSegments((segments) => [...segments.slice(0, -1), nextSegment]);

        followRunner(point);
      }
    );

    if (statusRef.current === 'running') {
      gpsSubscription.current = subscription;
    } else {
      subscription.remove();
    }
  };

  // Bring back a run that is still active in SQLite: after switching tabs
  // (this screen unmounts), or after Android restarted the app. Background
  // GPS kept recording meanwhile; the timer and route continue from storage.
  const restoreActiveRun = async (state: ActiveRunState) => {
    activeRunIdRef.current = state.runId;
    elapsedBeforeSegmentRef.current = state.activeMsBefore;

    // Redraw the route recorded so far, one line per leg.
    const points = await getRunPoints(state.runId);
    const segments: LatLng[][] = [];
    let lastLeg = -1;
    let meters = 0;
    for (const stored of points) {
      const point = { latitude: stored.latitude, longitude: stored.longitude };
      if (stored.legNo !== lastLeg) {
        segments.push([]);
        lastLeg = stored.legNo;
      } else {
        const current = segments[segments.length - 1];
        meters += calculateGpsDistance(current[current.length - 1], point);
      }
      segments[segments.length - 1].push(point);
    }
    lastLivePointRef.current = null;
    activeSegmentRef.current = [];
    setDistanceMeters(meters);

    if (state.isRecording) {
      const segmentStart = state.segmentStartedAt ?? Date.now();
      segmentStartedAtRef.current = segmentStart;
      if (state.segmentStartedAt === null) {
        await saveRunTiming(state.runId, state.activeMsBefore, segmentStart);
      }
      setRouteSegments([...segments, []]);
      updateRunStatus('running');
      try {
        await startBackgroundTracking();
      } catch (error) {
        console.error('Unable to restart background GPS:', error);
        setSyncMessage('Background GPS needs attention. Check location permission.');
      }
      await startGpsWatcher();
    } else {
      segmentStartedAtRef.current = null;
      setRouteSegments(segments);
      setElapsedSeconds(Math.floor(state.activeMsBefore / 1000));
      updateRunStatus('paused');
    }
  };

  // A run left on this phone by another account (for example after an
  // expired session signed that runner out) belongs to that account: finish
  // it for its owner, so it syncs when they sign in again, instead of
  // recording this runner's GPS into it.
  const belongsToAnotherAccount = async (state: ActiveRunState) => {
    const record = await getRunSyncRecord(state.runId);
    return !record || record.ownerUserId !== runnerId;
  };

  const finishForOtherAccount = async (state: ActiveRunState) => {
    const points = await getRunPoints(state.runId);
    const lastFix =
      points.length > 0 ? points[points.length - 1].timestamp : Date.now();
    const runningMs =
      state.isRecording && state.segmentStartedAt !== null
        ? Math.max(0, lastFix - state.segmentStartedAt)
        : 0;
    await setRunRecording(state.runId, false);
    await stopBackgroundTracking();
    await markRunFinishRequested(
      state.runId,
      new Date(lastFix).toISOString(),
      Math.floor((state.activeMsBefore + runningMs) / 1000)
    );
    await clearActiveRun();
  };

  // Restore an unfinished run once, when the screen opens (runnerId does
  // not change while this screen is shown).
  useEffect(() => {
    let active = true;
    (async () => {
      try {
        let state = await getActiveRunState();
        if (state && (await belongsToAnotherAccount(state))) {
          await finishForOtherAccount(state);
          state = null;
        }
        if (active && state && statusRef.current === 'idle') {
          await restoreActiveRun(state);
          // The screen closed while restoring: stop the live map watcher.
          if (!active) stopGpsWatcher();
        }
      } catch (error) {
        console.error('Unable to restore the active run:', error);
      } finally {
        if (active) setRestoringRun(false);
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  const startRun = async () => {
    if (startingRun || statusRef.current !== 'idle') return;

    // Never start a second run over an unfinished one: continue it instead
    // (or finish it for its owner if another account left it here).
    const unfinished = await getActiveRunState().catch(() => null);
    if (unfinished) {
      if (await belongsToAnotherAccount(unfinished)) {
        await finishForOtherAccount(unfinished);
      } else {
        await restoreActiveRun(unfinished);
        return;
      }
    }

    setStartingRun(true);
    let backgroundStarted = false;

    try {
      const permission = await Location.requestForegroundPermissionsAsync();

      if (permission.status !== 'granted') {
        Alert.alert(
          'Location Required',
          'Please enable location permission to start running.'
        );
        return;
      }

      const enabled = await Location.hasServicesEnabledAsync();

      if (!enabled) {
        Alert.alert(
          'GPS Disabled',
          'Please enable location services on your phone.'
        );
        return;
      }

      // Ask for background location before creating the run.
      const backgroundPermission =
        await Location.requestBackgroundPermissionsAsync();

      if (backgroundPermission.status !== 'granted') {
        Alert.alert(
          'Background Location Required',
          'Please allow background location to record runs while your phone is locked.'
        );
        return;
      }

      const initialLocation = await Location.getCurrentPositionAsync({
        accuracy: Location.Accuracy.High,
      });

      const runId = Crypto.randomUUID();
      // Keep the original UTC start time so /start can be safely retried.
      // GPS points carry GPS fix times, so never start after the first fix:
      // a phone clock running fast would otherwise make the server reject
      // every batch as "before run start" and the run could never sync.
      const startedAtMs = Math.min(Date.now(), initialLocation.timestamp);
      await registerRunForSync(runId, runnerId, new Date(startedAtMs).toISOString());

      await setActiveRun(runId);
      activeRunIdRef.current = runId;

      // Start Android's background location service.
      await startBackgroundTracking();
      backgroundStarted = true;

      // Allow the background task to save GPS points.
      await setRunRecording(runId, true);

      stopGpsWatcher();

      lastLivePointRef.current = null;
      activeSegmentRef.current = [];
      setRouteSegments([[]]);
      setDistanceMeters(0);

      elapsedBeforeSegmentRef.current = 0;
      segmentStartedAtRef.current = Date.now();
      await saveRunTiming(runId, 0, segmentStartedAtRef.current);
      setElapsedSeconds(0);
      setLocation(initialLocation.coords);

      updateRunStatus('running');
      await startGpsWatcher();

      // Starting the server run is part of syncRun; it retries offline.
      void syncRun(runId)
        .then(() => setSyncMessage(''))
        .catch((error) => {
          console.log('Run will sync when online:', error);
          setSyncMessage('GPS saved locally. Server sync pending.');
        });

      centerMap(initialLocation.coords.latitude, initialLocation.coords.longitude);
    } catch (error) {
      console.error('Unable to start run:', error);

      updateRunStatus('idle');
      stopGpsWatcher();

      if (activeRunIdRef.current) {
        try {
          await markRunFinishRequested(activeRunIdRef.current);
          await setRunRecording(activeRunIdRef.current, false);
        } catch (cleanupError) {
          console.error('Unable to disable run recording:', cleanupError);
        }
      }

      if (backgroundStarted) {
        try {
          await stopBackgroundTracking();
        } catch (cleanupError) {
          console.error('Unable to stop background GPS:', cleanupError);
        }
      }

      // Only clear the run this attempt created, never another active run.
      if (activeRunIdRef.current) {
        try {
          await clearActiveRun();
          activeRunIdRef.current = null;
        } catch (cleanupError) {
          console.error('Unable to clear active run:', cleanupError);
        }
      }

      Alert.alert('GPS Error', 'Unable to start GPS tracking. Please try again.');
    } finally {
      setStartingRun(false);
    }
  };

  const pauseRun = async () => {
    if (statusRef.current !== 'running' || busyRef.current) return;
    busyRef.current = true;
    setStartingRun(true);

    // Freeze the UI immediately, then disable SQLite recording.
    closeTimerSegment();
    setElapsedSeconds(Math.floor(elapsedBeforeSegmentRef.current / 1000));
    updateRunStatus('paused');
    stopGpsWatcher();
    lastLivePointRef.current = null;

    try {
      const runId = activeRunIdRef.current;
      if (runId) await setRunRecording(runId, false);
      if (runId) await saveRunTiming(runId, elapsedBeforeSegmentRef.current, null);
      await stopBackgroundTracking();
    } catch (error) {
      console.error('Unable to fully pause GPS:', error);
      Alert.alert(
        'GPS Warning',
        'Recording was disabled where possible. Check tracking before resuming.'
      );
    } finally {
      busyRef.current = false;
      setStartingRun(false);
    }
  };

  const resumeRun = async () => {
    if (statusRef.current !== 'paused' || busyRef.current) return;
    const runId = activeRunIdRef.current;
    if (!runId) {
      Alert.alert('Run Error', 'No active run was found.');
      return;
    }
    busyRef.current = true;
    setStartingRun(true);

    try {
      await startBackgroundTracking();
      await resumeRunRecording(runId);
      lastLivePointRef.current = null; // No distance across the pause.
      activeSegmentRef.current = [];
      setRouteSegments((segments) => [...segments, []]);
      segmentStartedAtRef.current = Date.now();
      await saveRunTiming(
        runId,
        elapsedBeforeSegmentRef.current,
        segmentStartedAtRef.current
      );
      updateRunStatus('running');
      await startGpsWatcher();
    } catch (error) {
      console.error('Unable to resume GPS:', error);
      closeTimerSegment();
      updateRunStatus('paused');
      stopGpsWatcher();
      try { await setRunRecording(runId, false); } catch (e) { console.error(e); }
      try { await saveRunTiming(runId, elapsedBeforeSegmentRef.current, null); } catch (e) { console.error(e); }
      try { await stopBackgroundTracking(); } catch (e) { console.error(e); }
      Alert.alert('GPS Error', 'Unable to resume GPS tracking.');
    } finally {
      busyRef.current = false;
      setStartingRun(false);
    }
  };

  // Stops recording but keeps the saved GPS points; returns the run id.
  const stopAndKeepSavedPoints = async () => {
    updateRunStatus('paused');
    stopGpsWatcher();
    const runId = activeRunIdRef.current;
    if (runId) await setRunRecording(runId, false);
    await stopBackgroundTracking();
    return runId;
  };

  const finishRun = () => {
    if (busyRef.current) return;
    Alert.alert('Finish Run?', 'Stop GPS tracking and keep the recorded route on this phone?', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Finish',
        onPress: async () => {
          if (busyRef.current) return;
          busyRef.current = true;
          setStartingRun(true);

          const finishedAt = new Date().toISOString();
          closeTimerSegment();
          const activeSeconds = Math.floor(elapsedBeforeSegmentRef.current / 1000);
          const totalTime = formatDuration(activeSeconds);
          const totalKm = (distanceMeters / 1000).toFixed(2);

          try {
            const runId = await stopAndKeepSavedPoints();
            const savedPoints = runId ? await getRunPoints(runId) : [];
            // Queue Finish *before* clearing the active marker. The end time
            // and running time go with /end, even if it is sent later.
            if (runId) await markRunFinishRequested(runId, finishedAt, activeSeconds);
            await clearActiveRun();
            clearRunFromScreen();
            Alert.alert('Run Saved',
              `Live distance: ${totalKm} km\nTime: ${totalTime}\nGPS points stored: ${savedPoints.length}\n\nGPS points are stored on your phone. Territory claims will sync with the backend when connected.`);
            if (runId) sendFinish(runId);
          } catch (error) {
            console.error('Unable to finish run safely:', error);
            Alert.alert('Finish Error', 'Could not fully stop tracking. Your run has not been cleared. Try Finish again.');
          } finally {
            busyRef.current = false;
            setStartingRun(false);
          }
        },
      },
    ]);
  };

  // Passed to the memoized map, so it keeps the same identity between renders.
  const recenterMap = useCallback(() => {
    if (location) {
      centerMap(location.latitude, location.longitude);
    }
  }, [location, centerMap]);

  // Log out stops the current run first (its GPS stays on the phone).
  const confirmLogout = () => {
    if (busyRef.current) return;
    Alert.alert(
      'Log Out?',
      statusRef.current === 'idle'
        ? 'Log out of ILAQA?'
        : 'Your current run will stop. Existing GPS points will remain on this phone.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Log Out',
          style: 'destructive',
          onPress: async () => {
            if (busyRef.current) return;
            busyRef.current = true;
            setStartingRun(true);
            try {
              const endedAt = new Date().toISOString();
              const segmentStart = segmentStartedAtRef.current;
              const activeMs =
                elapsedBeforeSegmentRef.current +
                (segmentStart !== null ? Date.now() - segmentStart : 0);
              const endingRunId = await stopAndKeepSavedPoints();
              if (endingRunId) {
                await markRunFinishRequested(
                  endingRunId,
                  endedAt,
                  Math.floor(activeMs / 1000)
                );
              }
              await clearActiveRun();
              activeRunIdRef.current = null;
              onLogout();
            } catch (error) {
              console.error('Unable to stop tracking for logout:', error);
              Alert.alert('Logout Blocked', 'Unable to confirm GPS has stopped. Please try again.');
            } finally {
              busyRef.current = false;
              setStartingRun(false);
            }
          },
        },
      ]
    );
  };

  return {
    mapRef,
    location,
    loading,
    locationError,
    runStatus,
    routeSegments,
    syncMessage,
    distanceMeters,
    elapsedSeconds,
    startingRun,
    restoringRun,
    startRun,
    pauseRun,
    resumeRun,
    finishRun,
    recenterMap,
    confirmLogout,
  };
}
