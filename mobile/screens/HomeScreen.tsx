
import React, { useEffect, useRef, useState } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
  Alert, Platform
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';
import { Ionicons } from '@expo/vector-icons';
import MapView, {
  Marker,
  Polyline,
  PROVIDER_GOOGLE,
  Region,
  Polygon,
  LatLng,
} from 'react-native-maps';
import * as Location from 'expo-location';
import * as Crypto from 'expo-crypto';

import { BACKGROUND_LOCATION_TASK } from '../services/backgroundLocationTask';
import {
  setActiveRun,
  setRunRecording,
  clearActiveRun,
  getRunPoints,
  resumeRunRecording,
  registerRunForSync,
  markRunFinishRequested,
  getConfirmedTerritories,
  ConfirmedTerritory,
  inspectSavedRuns,
  getActiveRunState,
  saveRunTiming,
  ActiveRunState,
  getRunSyncRecord,
} from '../services/runStorage';
import {
  syncRun,
  syncPendingRuns,
  fetchSavedTerritories,
  fetchSavedRoutes,
  SavedRunRoute,
} from '../services/runSync';


type HomeScreenProps = {
  runnerId: string;
  displayName: string;
  onLogout: () => void;
};

type RunStatus = 'idle' | 'running' | 'paused';

const KARACHI_REGION: Region = {
  latitude: 24.8607,
  longitude: 67.0011,
  latitudeDelta: 0.04,
  longitudeDelta: 0.04,
};

const MAP_DELTA = 0.003;

function calculateDistanceMeters(a: LatLng, b: LatLng): number {
  const earthRadius = 6371000;
  const toRadians = (value: number) => (value * Math.PI) / 180;

  const latitudeDifference = toRadians(b.latitude - a.latitude);
  const longitudeDifference = toRadians(b.longitude - a.longitude);

  const h =
    Math.sin(latitudeDifference / 2) ** 2 +
    Math.cos(toRadians(a.latitude)) *
      Math.cos(toRadians(b.latitude)) *
      Math.sin(longitudeDifference / 2) ** 2;

  return (
    2 *
    earthRadius *
    Math.atan2(Math.sqrt(h), Math.sqrt(Math.max(0, 1 - h)))
  );
}

function formatDuration(seconds: number): string {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const remainingSeconds = seconds % 60;

  const pad = (value: number) => String(value).padStart(2, '0');

  return hours > 0
    ? `${pad(hours)}:${pad(minutes)}:${pad(remainingSeconds)}`
    : `${pad(minutes)}:${pad(remainingSeconds)}`;
}


// Live route drawing limits (display only).
const LIVE_MAX_ACCURACY_METERS = 20;
const LIVE_MAX_SPEED_MPS = 8;

// "#RRGGBB" -> "rgba(r, g, b, alpha)" for translucent territory fills.
function withAlpha(hex: string, alpha: number): string {
  const value = parseInt(hex.replace('#', ''), 16);
  if (!Number.isFinite(value)) return `rgba(198, 255, 53, ${alpha})`;
  const r = (value >> 16) & 255;
  const g = (value >> 8) & 255;
  const b = value & 255;
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

export default function HomeScreen({
  runnerId,
  displayName,
  onLogout,
}: HomeScreenProps) {
  const mapRef = useRef<MapView>(null);
  const gpsSubscription = useRef<Location.LocationSubscription | null>(null);
  const routeRef = useRef<LatLng[]>([]);
  const lastLivePointRef = useRef<LatLng | null>(null);
  const lastLiveTimeRef = useRef(0);
  const [routeSegments, setRouteSegments] = useState<LatLng[][]>([]);
  const activeSegmentRef = useRef<LatLng[]>([]);
  const elapsedBeforeSegmentRef = useRef(0);
  const segmentStartedAtRef = useRef<number | null>(null);
  const busyRef = useRef(false);
  const statusRef = useRef<RunStatus>('idle');
  const activeRunIdRef = useRef<string | null>(null);
  const [location, setLocation] =
    useState<Location.LocationObjectCoords | null>(null);
  const [loading, setLoading] = useState(true);
  const [locationError, setLocationError] = useState('');
  const [runStatus, setRunStatus] = useState<RunStatus>('idle');
  
  
  const [savedRoutes, setSavedRoutes] =
    useState<SavedRunRoute[]>([]);
  const [savedTerritories, setSavedTerritories] =
    useState<ConfirmedTerritory[]>([]);

  // Load confirmed territory boundaries from the backend.
 
// Fetch all active territories from the backend.
// Refresh every 15 seconds so other players' claims appear.
useEffect(() => {
  let mounted = true;
  let fetching = false;

  const loadSavedTerritories = async () => {
    if (fetching) return;
    fetching = true;

    try {
      const territories = await fetchSavedTerritories();

      if (mounted) {
        setSavedTerritories(territories);
        console.log('ILAQA MAP TERRITORIES:', territories.length);
      }
    } catch (error) {
      console.log('Territory refresh failed:', error);
    } finally {
      fetching = false;
    }
  };

  void loadSavedTerritories();

  const interval = setInterval(() => {
    void loadSavedTerritories();
  }, 15000);

  return () => {
    mounted = false;
    clearInterval(interval);
  };
}, [runnerId]);


 // Load saved routes on startup and whenever a run finishes syncing.
const loadRoutes = async () => {
  try {
    const routes = await fetchSavedRoutes();
    setSavedRoutes(routes);
    console.log('ILAQA SAVED ROUTES:', routes.length);
  } catch (error) {
    console.error('Unable to load saved routes:', error);
  }
};

useEffect(() => {
  void loadRoutes();
}, [runnerId]);

    

  const [syncMessage, setSyncMessage] = useState('');

  const [distanceMeters, setDistanceMeters] = useState(0);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const [startingRun, setStartingRun] = useState(false);
  // True until an unfinished run in SQLite has been restored.
  const [restoringRun, setRestoringRun] = useState(true);

  const updateRunStatus = (status: RunStatus) => {
    statusRef.current = status;
    setRunStatus(status);
  };

  // Glide the camera to the runner, keeping the user's zoom and rotation.
  const followRunner = (point: LatLng) => {
    mapRef.current?.animateCamera({ center: point }, { duration: 900 });
  };

  const centerMap = (latitude: number, longitude: number) => {
    mapRef.current?.animateToRegion(
      {
        latitude,
        longitude,
        latitudeDelta: MAP_DELTA,
        longitudeDelta: MAP_DELTA,
      },
      700
    );
  };

  useEffect(() => {
    let active = true;

    const loadLocation = async () => {
      try {
        const permission =
          await Location.requestForegroundPermissionsAsync();

        if (!active) return;

        if (permission.status !== 'granted') {
          setLocationError(
            'Location permission denied. Enable location access in your phone settings.'
          );
          return;
        }

        const currentLocation =
          await Location.getCurrentPositionAsync({
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
      gpsSubscription.current?.remove();
      gpsSubscription.current = null;
    };
  }, []);

  // Recover backend-confirmed claims and retry failed GPS uploads every 5 seconds.
  // SQLite remains the source of GPS recordings when the internet is offline.
  useEffect(() => {
    let mounted = true;
    let busy = false;
    const syncAndRefresh = async () => {
      if (busy) return;
      busy = true;
      try {
        // Upload current run first, and then any pending finished runs.
        const current = activeRunIdRef.current;
        let endedNow = 0;

        if (current) {
          const result = await syncRun(current);
          if (result.endedNow) endedNow++;
        }

        endedNow += await syncPendingRuns(runnerId);

        // Saved routes change only when a run finished syncing; they are
        // also loaded when the screen opens. (No more reload every 5 s.)
        if (mounted && endedNow > 0) {
          await loadRoutes();
        }
        
        if (mounted) {
          
          setSyncMessage('');
        }
      } catch (error) {
        console.log('ILAQA: live territory sync pending:', error);
        if (mounted) setSyncMessage('GPS saved locally. Server sync pending.');
      } finally {
        busy = false;
      }
    };
    void syncAndRefresh();
    const timer = setInterval(() => { void syncAndRefresh(); }, 5000);
    return () => { mounted = false; clearInterval(timer); };
  }, [runnerId]);

  // Wall-clock timing keeps working when the phone is locked.
  useEffect(() => {
    if (runStatus !== 'running') return;
    const updateTimer = () => {
      const started = segmentStartedAtRef.current;
      if (started !== null) {
        setElapsedSeconds(
          Math.floor((elapsedBeforeSegmentRef.current + Date.now() - started) / 1000)
        );
      }
    };
    updateTimer();
    const timer = setInterval(updateTimer, 1000);
    return () => clearInterval(timer);
  }, [runStatus]);

  const startGpsWatcher = async () => {
    gpsSubscription.current?.remove();
    gpsSubscription.current = null;

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

        const previousRoute = routeRef.current;
        const lastPoint = lastLivePointRef.current;

        if (lastPoint) {
          const rawDistance = calculateDistanceMeters(lastPoint, raw);
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
            (previous) => previous + calculateDistanceMeters(lastPoint, point)
          );
        }

        lastLivePointRef.current = point;
        lastLiveTimeRef.current = position.timestamp;

        const nextRoute = [...previousRoute, point];
        routeRef.current = nextRoute;
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


 


const startBackgroundTracking = async () => {
  const foregroundPermission =
    await Location.getForegroundPermissionsAsync();

  if (foregroundPermission.status !== 'granted') {
    throw new Error('Foreground location permission is required.');
  }

  const backgroundPermission =
    await Location.requestBackgroundPermissionsAsync();

  if (backgroundPermission.status !== 'granted') {
    throw new Error(
      'Background location permission is required to track runs while the app is minimized.'
    );
  }

  const alreadyStarted =
    await Location.hasStartedLocationUpdatesAsync(
      BACKGROUND_LOCATION_TASK
    );

  if (alreadyStarted) return;

  await Location.startLocationUpdatesAsync(
    BACKGROUND_LOCATION_TASK,
    {
      accuracy: Location.Accuracy.High,
      distanceInterval: 5, // FR-06: record a point every 5 m
      timeInterval: 1000,
      pausesUpdatesAutomatically: false,
      foregroundService: {
        notificationTitle: 'ILAQA Run Tracking',
        notificationBody: 'Your running route is being recorded.',
        notificationColor: '#C6FF35',
      },
    }
  );
};
const stopBackgroundTracking = async () => {
  const started =
    await Location.hasStartedLocationUpdatesAsync(
      BACKGROUND_LOCATION_TASK
    );

  if (started) {
    await Location.stopLocationUpdatesAsync(
      BACKGROUND_LOCATION_TASK
    );
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
      meters += calculateDistanceMeters(current[current.length - 1], point);
    }
    segments[segments.length - 1].push(point);
  }
  routeRef.current = segments.flat();
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
  const lastFix = points.length > 0 ? points[points.length - 1].timestamp : Date.now();
  const runningMs = state.isRecording && state.segmentStartedAt !== null
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
        if (!active) {
          gpsSubscription.current?.remove();
          gpsSubscription.current = null;
        }
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
    const permission =
      await Location.requestForegroundPermissionsAsync();

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

    const initialLocation =
      await Location.getCurrentPositionAsync({
        accuracy: Location.Accuracy.High,
      });

    // Create a unique ID for this run.
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

    gpsSubscription.current?.remove();
    gpsSubscription.current = null;

    routeRef.current = [];
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

    centerMap(
      initialLocation.coords.latitude,
      initialLocation.coords.longitude
    );
  } catch (error) {
    console.error('Unable to start run:', error);

    updateRunStatus('idle');
    gpsSubscription.current?.remove();
    gpsSubscription.current = null;

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

    Alert.alert(
      'GPS Error',
      'Unable to start GPS tracking. Please try again.'
    );
  } finally {
    setStartingRun(false);
  }
};


  const pauseRun = async () => {
    if (statusRef.current !== 'running' || busyRef.current) return;
    busyRef.current = true;
    setStartingRun(true);
    // Freeze the UI immediately, then disable SQLite recording.
    const now = Date.now();
    const started = segmentStartedAtRef.current;
    if (started !== null) elapsedBeforeSegmentRef.current += now - started;
    segmentStartedAtRef.current = null;
    setElapsedSeconds(Math.floor(elapsedBeforeSegmentRef.current / 1000));
    updateRunStatus('paused');
    gpsSubscription.current?.remove();
    gpsSubscription.current = null;
    lastLivePointRef.current = null;
    try {
      const runId = activeRunIdRef.current;
      if (runId) await setRunRecording(runId, false);
      if (runId) await saveRunTiming(runId, elapsedBeforeSegmentRef.current, null);
      await stopBackgroundTracking();
    } catch (error) {
      console.error('Unable to fully pause GPS:', error);
      Alert.alert('GPS Warning', 'Recording was disabled where possible. Check tracking before resuming.');
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
      await saveRunTiming(runId, elapsedBeforeSegmentRef.current, segmentStartedAtRef.current);
      updateRunStatus('running');
      await startGpsWatcher();
    } catch (error) {
      console.error('Unable to resume GPS:', error);
      const started = segmentStartedAtRef.current;
      if (started !== null) elapsedBeforeSegmentRef.current += Date.now() - started;
      segmentStartedAtRef.current = null;
      updateRunStatus('paused');
      gpsSubscription.current?.remove();
      gpsSubscription.current = null;
      try { await setRunRecording(runId, false); } catch (e) { console.error(e); }
      try { await saveRunTiming(runId, elapsedBeforeSegmentRef.current, null); } catch (e) { console.error(e); }
      try { await stopBackgroundTracking(); } catch (e) { console.error(e); }
      Alert.alert('GPS Error', 'Unable to resume GPS tracking.');
    } finally {
      busyRef.current = false;
      setStartingRun(false);
    }
  };

  const stopAndKeepSavedPoints = async () => {
    updateRunStatus('paused');
    gpsSubscription.current?.remove();
    gpsSubscription.current = null;
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
          const started = segmentStartedAtRef.current;
          if (started !== null) elapsedBeforeSegmentRef.current += Date.now() - started;
          segmentStartedAtRef.current = null;
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
            activeRunIdRef.current = null;
            routeRef.current = [];
            lastLivePointRef.current = null;
            activeSegmentRef.current = [];
            setRouteSegments([]);
            setDistanceMeters(0);
            setElapsedSeconds(0);
            elapsedBeforeSegmentRef.current = 0;
            updateRunStatus('idle');
            Alert.alert('Run Saved',
              `Live distance: ${totalKm} km\nTime: ${totalTime}\nGPS points stored: ${savedPoints.length}\n\nGPS points are stored on your phone. Territory claims will sync with the backend when connected.`);
            if (runId) {
  void syncRun(runId)
    .then(async () => {
      // Fetch the finished route from the backend.
      await loadRoutes();

      setSyncMessage('');
    })
    .catch((error) => {
      console.log('Finish queued for retry:', error);
      setSyncMessage('Run saved locally. Finish sync pending.');
    });
}
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
const inspectOldGps = async () => {
  try {
    const runs = await inspectSavedRuns();
    console.log('ILAQA OLD GPS RUNS:', JSON.stringify(runs));
    Alert.alert(
      'Saved GPS Runs',
      runs.length
        ? runs.map(r =>
            `${r.runId.slice(0, 8)}: ${r.pointCount} points, sync=${r.hasSyncRecord}`
          ).join('\n')
        : 'No GPS points stored'
    );
  } catch (error) {
    Alert.alert('Inspection Error', String(error));
  }
};

  const recenterMap = () => {
    if (location) {
      centerMap(location.latitude, location.longitude);
    }
  };

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
              const activeMs = elapsedBeforeSegmentRef.current +
                (segmentStart !== null ? Date.now() - segmentStart : 0);
              const endingRunId = await stopAndKeepSavedPoints();
              if (endingRunId) {
                await markRunFinishRequested(
                  endingRunId, endedAt, Math.floor(activeMs / 1000));
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

  return (
    <SafeAreaView style={styles.container}>
      <StatusBar style="light" />

      <View style={styles.header}>
        <View>
          <Text style={styles.logo}>ILAQA® </Text>
          <Text style={styles.greeting}>
            WELCOME, {displayName.toUpperCase()}
          </Text>
        </View>

        <TouchableOpacity
          style={styles.logoutIconButton}
          onPress={confirmLogout}
          accessibilityRole="button"
          accessibilityLabel="Log out"
        >
          <Ionicons
            name="log-out-outline"
            size={24}
            color="#C6FF35"
          />
        </TouchableOpacity>
      </View>

      <View style={styles.mapContainer}>
        <MapView
          ref={mapRef}
          style={styles.map}
          provider={Platform.OS === 'android' ? PROVIDER_GOOGLE : undefined}
          initialRegion={KARACHI_REGION}
          showsUserLocation={location !== null}
          showsMyLocationButton={false}
          showsCompass
          rotateEnabled
        >
          {location && (
            <Marker
              coordinate={{
                latitude: location.latitude,
                longitude: location.longitude,
              }}
              title="Your Location"
              pinColor="#C6FF35"
            />
          )}

          {routeSegments.map((segment, index) =>
            segment.length >= 2 ? (
              <Polyline
                key={index}
                coordinates={segment}
                strokeColor="#C6FF35"
                strokeWidth={5}
                lineCap="round"
                lineJoin="round"
              />
            ) : null
          )}
          {savedTerritories.map((territory) => {
  // Each runner has their own colour; fall back to ILAQA lime.
  const color = territory.colorHex ?? '#C6FF35';
  return (
    <Polygon
      key={`saved-${territory.territoryId}`}
      coordinates={territory.boundary}
      fillColor={withAlpha(color, 0.35)}
      strokeColor={color}
      strokeWidth={3}
      tappable
      onPress={() =>
        Alert.alert(
          territory.isMine ? 'Your Territory' : 'Territory',
          `Owner: ${territory.ownerLabel ?? 'Anonymous profile'}\n` +
            `Area: ${territory.areaSquareMeters.toFixed(2)} m²\n` +
            `Status: ${territory.isShielded === false ? 'Vulnerable' : 'Shielded'}`
        )
      }
    />
  );
})}


          {/* Historical GPS routes from Supabase; one polyline per leg. */}
          {savedRoutes.map((run) =>
            run.legs.map((leg) =>
              leg.coordinates.length >= 2 ? (
                <Polyline
                  key={`saved-route-${run.runId}-${leg.legNo}`}
                  coordinates={leg.coordinates}
                  strokeColor="#38BDF8"
                  strokeWidth={3}
                />
              ) : null
            )
          )}

        </MapView>
<TouchableOpacity
  onPress={inspectOldGps}
  style={{
    position: 'absolute',
    top: 16,
    right: 16,
    backgroundColor: '#0C0D0C',
    padding: 12,
    borderRadius: 10,
  }}
>
  <Text style={{ color: '#C6FF35' }}>CHECK OLD GPS</Text>
</TouchableOpacity>
        <TouchableOpacity
          style={styles.recenterButton}
          onPress={recenterMap}
          disabled={!location}
        >
          <Ionicons
            name="locate"
            size={24}
            color="#C6FF35"
          />
        </TouchableOpacity>

        {loading && (
          <View style={styles.loadingOverlay}>
            <ActivityIndicator size="large" color="#C6FF35" />
            <Text style={styles.loadingText}>
              FINDING YOUR LOCATION...
            </Text>
          </View>
        )}
      </View>

      <View style={styles.runPanel}>

        {!!syncMessage && (
          <Text style={{ color: '#FFBE55', fontSize: 11, marginBottom: 8 }}>
            {syncMessage}
          </Text>
        )}
        <View style={styles.statsRow}>
          <View style={styles.stat}>
            <Text style={styles.statValue}>
              {(distanceMeters / 1000).toFixed(2)}
            </Text>
            <Text style={styles.statLabel}>DISTANCE (KM)</Text>
          </View>

          <View style={styles.stat}>
            <Text style={styles.statValue}>
              {formatDuration(elapsedSeconds)}
            </Text>
            <Text style={styles.statLabel}>TIME</Text>
          </View>
        </View>

        <View style={styles.statusRow}>
          <View
            style={[
              styles.statusDot,
              {
                backgroundColor:
                  runStatus === 'running'
                    ? '#C6FF35'
                    : runStatus === 'paused'
                      ? '#FFBE55'
                      : location
                        ? '#C6FF35'
                        : '#777777',
              },
            ]}
          />

          <Text style={styles.statusText}>
            {runStatus === 'running'
              ? 'RUN IN PROGRESS'
              : runStatus === 'paused'
                ? 'RUN PAUSED'
                : location
                  ? 'GPS READY'
                  : 'GPS UNAVAILABLE'}
          </Text>
        </View>

        {locationError ? (
          <Text style={styles.errorText}>{locationError}</Text>
        ) : null}

        {runStatus === 'idle' ? (
          <TouchableOpacity
            style={[
              styles.primaryButton,
              (loading || startingRun || restoringRun) && styles.disabledButton,
            ]}
            onPress={startRun}
            disabled={loading || startingRun || restoringRun}
          >
            <Ionicons name="play" size={20} color="#0C0D0C" />
            <Text style={styles.primaryButtonText}>
              {startingRun ? 'STARTING...' : 'START RUN'}
            </Text>
          </TouchableOpacity>
        ) : (
          <View style={styles.buttonRow}>
            <TouchableOpacity
              style={styles.primaryButton}
              onPress={
                runStatus === 'running' ? pauseRun : resumeRun
              }
              disabled={startingRun}
            >
              <Ionicons
                name={runStatus === 'running' ? 'pause' : 'play'}
                size={19}
                color="#0C0D0C"
              />
              <Text style={styles.primaryButtonText}>
                {runStatus === 'running' ? 'PAUSE' : 'RESUME'}
              </Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={styles.finishButton}
              onPress={finishRun}
              disabled={startingRun}
            >
              <Ionicons
                name="stop"
                size={19}
                color="#FFFFFF"
              />
              <Text style={styles.finishButtonText}>FINISH</Text>
            </TouchableOpacity>
          </View>
        )}
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0C0D0C',
  },
  header: {
    paddingHorizontal: 20,
    paddingTop: 12,
    paddingBottom: 16,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  logo: {
    color: '#C6FF35',
    fontSize: 25,
    fontWeight: '900',
    letterSpacing: 2,
  },
  greeting: {
    color: '#A0A0A0',
    fontSize: 11,
    fontWeight: '700',
    marginTop: 6,
  },
  logoutIconButton: {
    width: 44,
    height: 44,
    borderRadius: 12,
    backgroundColor: '#1C1E1C',
    borderWidth: 1,
    borderColor: '#303330',
    alignItems: 'center',
    justifyContent: 'center',
  },
mapContainer: {
  flex: 1,
  minHeight: 0,
  maxHeight: '65%',
  marginHorizontal: 12,
  borderRadius: 16,
  overflow: 'hidden',
  backgroundColor: '#1B1D1B',
},
  map: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
  },
  recenterButton: {
    position: 'absolute',
    right: 16,
    bottom: 18,
    backgroundColor: '#0C0D0C',
    width: 48,
    height: 48,
    borderRadius: 24,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: '#C6FF35',
  },
  loadingOverlay: {
    position: 'absolute',
    top: 16,
    left: 16,
    right: 16,
    alignItems: 'center',
    backgroundColor: '#0C0D0C',
    borderRadius: 12,
    padding: 16,
  },
  loadingText: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '800',
    marginTop: 10,
  },
 runPanel: {
  paddingHorizontal: 18,
  paddingTop: 10,
  paddingBottom: 8,
  flexShrink: 0,
},
  statsRow: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    marginBottom: 14,
  },
  stat: {
    alignItems: 'center',
    flex: 1,
  },
  statValue: {
    color: '#FFFFFF',
    fontSize: 26,
    fontWeight: '900',
  },
  statLabel: {
    color: '#999E99',
    fontSize: 10,
    fontWeight: '800',
    letterSpacing: 1,
    marginTop: 4,
  },
  statusRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 14,
  },
  statusDot: {
    width: 9,
    height: 9,
    borderRadius: 5,
    marginRight: 9,
  },
  statusText: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '800',
    letterSpacing: 1,
  },
  errorText: {
    color: '#FF7777',
    fontSize: 12,
    marginBottom: 10,
  },
 primaryButton: {
  backgroundColor: '#C6FF35',
  borderRadius: 12,
  height: 52,
  minHeight: 52,
  alignItems: 'center',
  justifyContent: 'center',
  flexDirection: 'row',
  gap: 8,
  flexGrow: 1,
},
  primaryButtonText: {
    color: '#0C0D0C',
    fontWeight: '900',
    fontSize: 14,
    letterSpacing: 1,
  },
  buttonRow: {
    flexDirection: 'row',
    gap: 10,
  },
  finishButton: {
    backgroundColor: '#333733',
    borderRadius: 12,
    height: 52,
    alignItems: 'center',
    justifyContent: 'center',
    flexDirection: 'row',
    gap: 8,
    flex: 1,
  },
  finishButtonText: {
    color: '#FFFFFF',
    fontWeight: '900',
    fontSize: 14,
    letterSpacing: 1,
  },
  disabledButton: {
    opacity: 0.5,
  },
});
