// ILAQA "Volt Night" Google Map style (dark): real Google streets, blocks and
// labels recoloured to match the app. Used by components/RunMap.tsx.
// Colours: ink blocks, grey streets, lighter main roads, forest parks.
// Only styling — no data, no network calls, no battery cost.

import type { MapStyleElement } from 'react-native-maps';

export const ILAQA_MAP_STYLE: MapStyleElement[] = [
  // Base land and city blocks
  { elementType: 'geometry', stylers: [{ color: '#121615' }] },
  { featureType: 'landscape.man_made', elementType: 'geometry', stylers: [{ color: '#171C1A' }] },
  { featureType: 'landscape.natural', elementType: 'geometry', stylers: [{ color: '#141917' }] },

  // Labels: soft grey with a dark halo so they read on any block
  { elementType: 'labels.text.fill', stylers: [{ color: '#8F9891' }] },
  { elementType: 'labels.text.stroke', stylers: [{ color: '#121615' }, { weight: 3 }] },
  { elementType: 'labels.icon', stylers: [{ visibility: 'off' }] },
  { featureType: 'administrative.locality', elementType: 'labels.text.fill', stylers: [{ color: '#B8C0BA' }] },
  { featureType: 'administrative.neighborhood', elementType: 'labels.text.fill', stylers: [{ color: '#6B7570' }] },
  { featureType: 'administrative', elementType: 'geometry.stroke', stylers: [{ color: '#2A322E' }] },

  // Places: hide shops and businesses (clutter), keep parks and hospitals
  { featureType: 'poi', stylers: [{ visibility: 'off' }] },
  { featureType: 'poi.park', stylers: [{ visibility: 'on' }] },
  { featureType: 'poi.park', elementType: 'geometry', stylers: [{ color: '#123020' }] },
  { featureType: 'poi.park', elementType: 'labels.text.fill', stylers: [{ color: '#5E8A6E' }] },
  { featureType: 'poi.medical', stylers: [{ visibility: 'on' }] },
  { featureType: 'poi.medical', elementType: 'geometry', stylers: [{ color: '#1B2321' }] },
  { featureType: 'poi.sports_complex', stylers: [{ visibility: 'on' }] },
  { featureType: 'poi.sports_complex', elementType: 'geometry', stylers: [{ color: '#123020' }] },

  // Roads: local streets grey, main roads lighter, highways lightest
  { featureType: 'road', elementType: 'geometry.fill', stylers: [{ color: '#262D2A' }] },
  { featureType: 'road', elementType: 'geometry.stroke', stylers: [{ color: '#151917' }] },
  { featureType: 'road', elementType: 'labels.text.fill', stylers: [{ color: '#8F9891' }] },
  { featureType: 'road.arterial', elementType: 'geometry.fill', stylers: [{ color: '#3A433F' }] },
  { featureType: 'road.highway', elementType: 'geometry.fill', stylers: [{ color: '#4A5550' }] },
  { featureType: 'road.highway', elementType: 'geometry.stroke', stylers: [{ color: '#151917' }] },
  { featureType: 'road.highway', elementType: 'labels.text.fill', stylers: [{ color: '#B8C0BA' }] },

  // Transit lines off (not useful for runners)
  { featureType: 'transit', stylers: [{ visibility: 'off' }] },

  // Sea and lakes
  { featureType: 'water', elementType: 'geometry', stylers: [{ color: '#0B1A21' }] },
  { featureType: 'water', elementType: 'labels.text.fill', stylers: [{ color: '#4C6470' }] },
];
