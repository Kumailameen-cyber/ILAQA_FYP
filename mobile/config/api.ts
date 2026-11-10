// Backend address: EXPO_PUBLIC_API_URL from .env, otherwise port 5246 on the
// computer running Expo.

import Constants from 'expo-constants';

// Optional custom backend address from .env
const customApiUrl = process.env.EXPO_PUBLIC_API_URL;

// Get the computer running Expo.
const expoHost =
  Constants.expoConfig?.hostUri ??
  Constants.expoGoConfig?.debuggerHost ??
  '';

// Extract its IP address.
const host = expoHost.split(':')[0];

// Use custom URL if provided.
// Otherwise, use the existing automatic IP detection.
export const API_URL = customApiUrl
  ? customApiUrl.replace(/\/+$/, '')
  : host
    ? `http://${host}:5246`
    : 'http://localhost:5246';
console.log('ILAQA API URL:', API_URL);