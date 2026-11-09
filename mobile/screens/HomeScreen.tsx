// Home tab: the run-tracking screen. Puts together the header (logout), the
// map (RunMap) and the run controls (RunPanel). The run logic lives in
// hooks/useRunTracking and the map territories in hooks/useMapTerritories.

import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';
import { Ionicons } from '@expo/vector-icons';

import RunMap from '../components/RunMap';
import RunPanel from '../components/RunPanel';
import { useMapTerritories } from '../hooks/useMapTerritories';
import { useRunTracking } from '../hooks/useRunTracking';

type HomeScreenProps = {
  runnerId: string;
  displayName: string;
  onLogout: () => void;
};

export default function HomeScreen({
  runnerId,
  displayName,
  onLogout,
}: HomeScreenProps) {
  const { savedTerritories, refreshTerritories } = useMapTerritories(runnerId);
  const run = useRunTracking({ runnerId, refreshTerritories, onLogout });

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
          onPress={run.confirmLogout}
          accessibilityRole="button"
          accessibilityLabel="Log out"
        >
          <Ionicons name="log-out-outline" size={24} color="#C6FF35" />
        </TouchableOpacity>
      </View>

      <RunMap
        mapRef={run.mapRef}
        location={run.location}
        routeSegments={run.routeSegments}
        territories={savedTerritories}
        loading={run.loading}
        onRecenter={run.recenterMap}
      />

      <RunPanel
        runStatus={run.runStatus}
        hasLocation={run.location !== null}
        locationError={run.locationError}
        syncMessage={run.syncMessage}
        distanceMeters={run.distanceMeters}
        elapsedSeconds={run.elapsedSeconds}
        loading={run.loading}
        startingRun={run.startingRun}
        restoringRun={run.restoringRun}
        onStart={run.startRun}
        onPause={run.pauseRun}
        onResume={run.resumeRun}
        onFinish={run.finishRun}
      />
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
});
