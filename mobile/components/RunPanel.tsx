// Home run panel: distance and time, GPS / run status, and the Start,
// Pause / Resume and Finish buttons.

import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { formatDuration, RunStatus } from '../hooks/useRunTracking';

type RunPanelProps = {
  runStatus: RunStatus;
  hasLocation: boolean;
  locationError: string;
  syncMessage: string;
  distanceMeters: number;
  elapsedSeconds: number;
  // Start stays disabled while any of these is true.
  loading: boolean;
  startingRun: boolean;
  restoringRun: boolean;
  onStart: () => void;
  onPause: () => void;
  onResume: () => void;
  onFinish: () => void;
};

export default function RunPanel({
  runStatus,
  hasLocation,
  locationError,
  syncMessage,
  distanceMeters,
  elapsedSeconds,
  loading,
  startingRun,
  restoringRun,
  onStart,
  onPause,
  onResume,
  onFinish,
}: RunPanelProps) {
  const statusColor =
    runStatus === 'running'
      ? '#C6FF35'
      : runStatus === 'paused'
        ? '#FFBE55'
        : hasLocation
          ? '#C6FF35'
          : '#777777';

  const statusText =
    runStatus === 'running'
      ? 'RUN IN PROGRESS'
      : runStatus === 'paused'
        ? 'RUN PAUSED'
        : hasLocation
          ? 'GPS READY'
          : 'GPS UNAVAILABLE';

  const startDisabled = loading || startingRun || restoringRun;

  return (
    <View style={styles.runPanel}>
      {!!syncMessage && <Text style={styles.syncText}>{syncMessage}</Text>}

      <View style={styles.statsRow}>
        <View style={styles.stat}>
          <Text style={styles.statValue}>
            {(distanceMeters / 1000).toFixed(2)}
          </Text>
          <Text style={styles.statLabel}>DISTANCE (KM)</Text>
        </View>

        <View style={styles.stat}>
          <Text style={styles.statValue}>{formatDuration(elapsedSeconds)}</Text>
          <Text style={styles.statLabel}>TIME</Text>
        </View>
      </View>

      <View style={styles.statusRow}>
        <View style={[styles.statusDot, { backgroundColor: statusColor }]} />
        <Text style={styles.statusText}>{statusText}</Text>
      </View>

      {locationError ? (
        <Text style={styles.errorText}>{locationError}</Text>
      ) : null}

      {runStatus === 'idle' ? (
        <TouchableOpacity
          style={[styles.primaryButton, startDisabled && styles.disabledButton]}
          onPress={onStart}
          disabled={startDisabled}
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
            onPress={runStatus === 'running' ? onPause : onResume}
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
            onPress={onFinish}
            disabled={startingRun}
          >
            <Ionicons name="stop" size={19} color="#FFFFFF" />
            <Text style={styles.finishButtonText}>FINISH</Text>
          </TouchableOpacity>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  runPanel: {
    paddingHorizontal: 18,
    paddingTop: 10,
    paddingBottom: 8,
    flexShrink: 0,
  },
  syncText: {
    color: '#FFBE55',
    fontSize: 11,
    marginBottom: 8,
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
