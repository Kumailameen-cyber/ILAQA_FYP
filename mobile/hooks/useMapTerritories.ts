// Map territories: loads every runner's active land from the backend and
// keeps it fresh, so other players' claims appear on the map.

import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState } from 'react-native';
import { ConfirmedTerritory } from '../services/runStorage';
import { fetchSavedTerritories } from '../services/runSync';

const REFRESH_INTERVAL_MS = 10000;

export function useMapTerritories(runnerId: string) {
  const [savedTerritories, setSavedTerritories] =
    useState<ConfirmedTerritory[]>([]);

  // Points at the current effect's loader (a no-op after unmount).
  const refreshRef = useRef<() => void>(() => {});

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

    refreshRef.current = () => {
      void loadSavedTerritories();
    };
    void loadSavedTerritories();

    // Check every 10 s, and at once when the app comes back to the screen
    // (timers do not run while it is in the background).
    const interval = setInterval(() => {
      void loadSavedTerritories();
    }, REFRESH_INTERVAL_MS);
    const appState = AppState.addEventListener('change', (state) => {
      if (state === 'active') void loadSavedTerritories();
    });

    return () => {
      mounted = false;
      clearInterval(interval);
      appState.remove();
      refreshRef.current = () => {};
    };
  }, [runnerId]);

  // Reload now, e.g. after a sync claimed new land. Stable between renders.
  const refreshTerritories = useCallback(() => refreshRef.current(), []);

  return { savedTerritories, refreshTerritories };
}
