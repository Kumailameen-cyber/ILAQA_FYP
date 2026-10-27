
import React, { useEffect, useRef, useState } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
  Alert,
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
  resumeRunRecording
} from '../services/runStorage';



import {
  detectTerritoriesByLeg,
  TerritoryCandidate,
} from '../services/territoryDetection';

type HomeScreenProps = {
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

export default function HomeScreen({
  displayName,
  onLogout,
}: HomeScreenProps) {
  const mapRef = useRef<MapView>(null);
  const gpsSubscription = useRef<Location.LocationSubscription | null>(null);
  const routeRef = useRef<LatLng[]>([]);
  const lastLivePointRef = useRef<LatLng | null>(null);
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
  const [route, setRoute] = useState<LatLng[]>([]);
  
const [territoryCandidates, setTerritoryCandidates] =
  useState<TerritoryCandidate[]>([]);

  const [distanceMeters, setDistanceMeters] = useState(0);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const [startingRun, setStartingRun] = useState(false);

  const updateRunStatus = (status: RunStatus) => {
    statusRef.current = status;
    setRunStatus(status);
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
        distanceInterval: 3,
        timeInterval: 1000,
      },
      (position) => {
        if (statusRef.current !== 'running') return;

        const coords = position.coords;
        setLocation(coords);

        // Basic GPS accuracy filtering.
        if (
          coords.accuracy !== null &&
          coords.accuracy > 40
        ) {
          return;
        }

        const point: LatLng = {
          latitude: coords.latitude,
          longitude: coords.longitude,
        };

        const previousRoute = routeRef.current;
        const lastPoint = lastLivePointRef.current;

        if (lastPoint) {
          const segmentDistance = calculateDistanceMeters(lastPoint, point);

          // Ignore tiny GPS movements and extreme jumps.
          if (segmentDistance < 2 || segmentDistance > 100) {
            return;
          }

          setDistanceMeters((previous) => previous + segmentDistance);
        }

        lastLivePointRef.current = point;
        const nextRoute = [...previousRoute, point];
        routeRef.current = nextRoute;
        setRoute(nextRoute);
        const nextSegment = [...activeSegmentRef.current, point];
        activeSegmentRef.current = nextSegment;
        setRouteSegments((segments) => [...segments.slice(0, -1), nextSegment]);

        centerMap(point.latitude, point.longitude);
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
      distanceInterval: 3,
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
 
const startRun = async () => {
  if (startingRun || statusRef.current !== 'idle') return;

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
    setRoute([]);
    setTerritoryCandidates([]);
    setDistanceMeters(0);
    
    elapsedBeforeSegmentRef.current = 0;
    segmentStartedAtRef.current = Date.now();
    setElapsedSeconds(0);
    setLocation(initialLocation.coords);

    updateRunStatus('running');
    await startGpsWatcher();

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

    try {
      await clearActiveRun();
      activeRunIdRef.current = null;
    } catch (cleanupError) {
      console.error('Unable to clear active run:', cleanupError);
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
          const started = segmentStartedAtRef.current;
          if (started !== null) elapsedBeforeSegmentRef.current += Date.now() - started;
          segmentStartedAtRef.current = null;
          const totalTime = formatDuration(Math.floor(elapsedBeforeSegmentRef.current / 1000));
          const totalKm = (distanceMeters / 1000).toFixed(2);
          try {
            const runId = await stopAndKeepSavedPoints();
            const savedPoints = runId ? await getRunPoints(runId) : [];
            await clearActiveRun();
            activeRunIdRef.current = null;
            routeRef.current = [];
            lastLivePointRef.current = null;
            activeSegmentRef.current = [];
            setRoute([]);
            setRouteSegments([]);
            setDistanceMeters(0);
            setElapsedSeconds(0);
            elapsedBeforeSegmentRef.current = 0;
            updateRunStatus('idle');
            Alert.alert('Run Completed',
              `Live distance: ${totalKm} km\nTime: ${totalTime}\nGPS points stored: ${savedPoints.length}\n\nRoute points remain in SQLite. Run summary/history sync will be added later.`);
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


const checkSavedTerritories = async () => {
  try {
    const runId = activeRunIdRef.current;

    if (!runId) {
      Alert.alert('No Active Run', 'Start a run first.');
      return;
    }

    // Read GPS points already saved in SQLite.
    const savedPoints = await getRunPoints(runId);

    // Include timestamps and accuracy for GPS validation.
    const gpsPoints = savedPoints.map((point) => ({
      latitude: point.latitude,
      longitude: point.longitude,
      legNo: point.legNo,
      timestamp: point.timestamp,
      accuracy: point.accuracy,
    }));

    // Detect territories using validated GPS movement.
    const candidates = detectTerritoriesByLeg(gpsPoints);

    // Show detected territory previews on the map.
    setTerritoryCandidates(candidates);

    console.log('ILAQA TERRITORY TEST:', {
      gpsPoints: savedPoints.length,
      territoriesFound: candidates.length,
      areas: candidates.map(
        (candidate) => candidate.areaSquareMeters
      ),
    });

    Alert.alert(
      'Territory Detection',
      `GPS points: ${savedPoints.length}\n` +
      `Territories detected: ${candidates.length}`
    );
  } catch (error) {
    console.error('Territory detection failed:', error);
    Alert.alert('Error', 'Unable to detect territories.');
  }
};


const testSavedLegs = async () => {
  try {
    const runId = activeRunIdRef.current;

    if (!runId) {
      Alert.alert(
        'No Active Run',
        'Start a new run before testing GPS legs.'
      );
      return;
    }

    const points = await getRunPoints(runId);

    const legCounts: Record<number, number> = {};

    for (const point of points) {
      legCounts[point.legNo] =
        (legCounts[point.legNo] ?? 0) + 1;
    }

    console.log('ILAQA LEG TEST:', {
      runId,
      totalPoints: points.length,
      legCounts,
    });

    const summary = Object.entries(legCounts)
      .sort(([a], [b]) => Number(a) - Number(b))
      .map(
        ([leg, count]) =>
          `Leg ${leg}: ${count} GPS points`
      )
      .join('\n');

    Alert.alert(
      'GPS Leg Test',
      `Total points: ${points.length}\n\n${
        summary || 'No GPS points recorded yet.'
      }`
    );
  } catch (error) {
    console.error('Leg test failed:', error);
    Alert.alert('Error', 'Unable to read GPS points.');
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
              await stopAndKeepSavedPoints();
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
          <Text style={styles.logo}>ILAQA®</Text>
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
          provider={PROVIDER_GOOGLE}
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
              />
            ) : null
          )}
{territoryCandidates.map((territory, index) => (
  <Polygon
    key={`territory-preview-${index}`}
    coordinates={territory.boundary}
    fillColor="rgba(198, 255, 53, 0.25)"
    strokeColor="#C6FF35"
    strokeWidth={3}
    tappable
    onPress={() => {
      Alert.alert(
        'Territory Preview',
        `Area: ${territory.areaSquareMeters.toFixed(2)} m²\n\n` +
        'This territory has not been approved by the backend yet.'
      );
    }}
  />
))}

        </MapView>

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
        
<TouchableOpacity
  onPress={testSavedLegs}
  style={{
    backgroundColor: '#333733',
    padding: 12,
    borderRadius: 10,
    marginBottom: 10,
    alignItems: 'center',
  }}
>
  <Text style={{ color: '#C6FF35', fontWeight: 'bold' }}>
    TEST GPS LEGS
  </Text>
</TouchableOpacity>

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
              (loading || startingRun) && styles.disabledButton,
            ]}
            onPress={startRun}
            disabled={loading || startingRun}
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
