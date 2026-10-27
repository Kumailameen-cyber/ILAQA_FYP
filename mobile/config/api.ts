
import Constants from 'expo-constants';

const expoHost =
  Constants.expoConfig?.hostUri ??
  Constants.expoGoConfig?.debuggerHost ??
  '';

const host = expoHost.split(':')[0];

export const API_URL = host
  ? `http://${host}:5246`
  : 'http://localhost:5246';
