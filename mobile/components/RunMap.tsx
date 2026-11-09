// Home map: the runner's position, the current run's route line, every
// runner's territories, the recenter button and the "finding location" banner.

import { memo, RefObject } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
  Alert,
  Platform,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import MapView, {
  Marker,
  Polyline,
  PROVIDER_GOOGLE,
  Region,
  Polygon,
  LatLng,
} from 'react-native-maps';
import { ConfirmedTerritory } from '../services/runStorage';

const KARACHI_REGION: Region = {
  latitude: 24.8607,
  longitude: 67.0011,
  latitudeDelta: 0.04,
  longitudeDelta: 0.04,
};

// "#RRGGBB" -> "rgba(r, g, b, alpha)" for translucent territory fills.
function withAlpha(hex: string, alpha: number): string {
  const value = parseInt(hex.replace('#', ''), 16);
  if (!Number.isFinite(value)) return `rgba(198, 255, 53, ${alpha})`;
  const r = (value >> 16) & 255;
  const g = (value >> 8) & 255;
  const b = value & 255;
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

function showTerritoryDetails(territory: ConfirmedTerritory) {
  Alert.alert(
    territory.isMine ? 'Your Territory' : 'Territory',
    `Owner: ${territory.ownerLabel ?? 'Anonymous profile'}\n` +
      `Area: ${territory.areaSquareMeters.toFixed(2)} m²\n` +
      `Status: ${territory.isShielded === false ? 'Vulnerable' : 'Shielded'}`
  );
}

type RunMapProps = {
  mapRef: RefObject<MapView | null>;
  location: { latitude: number; longitude: number } | null;
  routeSegments: LatLng[][];
  territories: ConfirmedTerritory[];
  loading: boolean;
  onRecenter: () => void;
};

// memo: the run timer re-renders the Home screen every second; the map only
// needs to redraw when its own data changes.
function RunMap({
  mapRef,
  location,
  routeSegments,
  territories,
  loading,
  onRecenter,
}: RunMapProps) {
  return (
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

        {/* The current run only: one line per leg (Start/Resume). */}
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

        {territories.map((territory) => {
          // Each runner has their own colour; fall back to ILAQA lime.
          const color = territory.colorHex ?? '#C6FF35';
          return (
            <Polygon
              // The key changes with the shape, so a merged or reduced territory
              // is redrawn (Android maps do not always redraw changed points).
              key={`saved-${territory.territoryId}-${territory.boundary.length}-${territory.areaSquareMeters}`}
              coordinates={territory.boundary}
              fillColor={withAlpha(color, 0.35)}
              strokeColor={color}
              strokeWidth={3}
              tappable
              onPress={() => showTerritoryDetails(territory)}
            />
          );
        })}
      </MapView>

      <TouchableOpacity
        style={styles.recenterButton}
        onPress={onRecenter}
        disabled={!location}
      >
        <Ionicons name="locate" size={24} color="#C6FF35" />
      </TouchableOpacity>

      {loading && (
        <View style={styles.loadingOverlay}>
          <ActivityIndicator size="large" color="#C6FF35" />
          <Text style={styles.loadingText}>FINDING YOUR LOCATION...</Text>
        </View>
      )}
    </View>
  );
}

export default memo(RunMap);

const styles = StyleSheet.create({
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
});
