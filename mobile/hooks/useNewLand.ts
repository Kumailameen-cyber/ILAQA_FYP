// Live-claim feedback for the Home map: when a refresh shows a territory of
// the runner's that was not there before, return its ids (the map highlights
// them) and a message "Territory claimed · +N m²" for a few seconds.

import { useEffect, useRef, useState } from 'react';
import { ConfirmedTerritory } from '../services/runStorage';
import { findNewOwnLand } from '../services/newLand';

const SHOW_FOR_MS = 4000;

// 1234.6 -> "1,235"
function formatArea(squareMeters: number): string {
  return String(Math.round(squareMeters)).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

export function useNewLand(
  territories: ConfirmedTerritory[],
  loaded: boolean
) {
  const knownIdsRef = useRef<Set<string> | null>(null);
  const [highlightIds, setHighlightIds] = useState<string[]>([]);
  const [message, setMessage] = useState<string | null>(null);
  // Kept outside the effect: the map refreshes every 10 s, and a refresh
  // must not cancel the timer that hides the message.
  const hideTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(
    () => () => {
      if (hideTimerRef.current) clearTimeout(hideTimerRef.current);
    },
    []
  );

  useEffect(() => {
    // Not loaded yet (or another runner logged in): start fresh, so land the
    // runner already owned is never announced as new.
    if (!loaded) {
      knownIdsRef.current = null;
      return;
    }

    const result = findNewOwnLand(knownIdsRef.current, territories);
    knownIdsRef.current = result.knownIds;
    if (result.newIds.length === 0) return;

    setHighlightIds(result.newIds);
    setMessage(`Territory claimed · +${formatArea(result.newAreaSquareMeters)} m²`);

    if (hideTimerRef.current) clearTimeout(hideTimerRef.current);
    hideTimerRef.current = setTimeout(() => {
      setHighlightIds([]);
      setMessage(null);
    }, SHOW_FOR_MS);
  }, [territories, loaded]);

  return { highlightIds, message };
}
