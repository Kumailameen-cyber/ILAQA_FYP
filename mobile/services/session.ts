// Login session: the saved tokens, restoring the session when the app
// opens, and logging out on the server.

import * as SecureStore from 'expo-secure-store';
import { authFetch, postJson } from './api';

export type Runner = {
  id: string;
  username: string;
  displayName: string;
};

export async function saveTokens(
  accessToken: string,
  refreshToken: string
): Promise<void> {
  await SecureStore.setItemAsync('accessToken', accessToken);
  await SecureStore.setItemAsync('refreshToken', refreshToken);
}

export async function clearTokens(): Promise<void> {
  await SecureStore.deleteItemAsync('accessToken');
  await SecureStore.deleteItemAsync('refreshToken');
}

// Returns the signed-in runner, or null when there is no valid session.
// A temporary network or server error keeps the saved tokens.
export async function restoreSession(): Promise<Runner | null> {
  const refreshToken = await SecureStore.getItemAsync('refreshToken');
  if (!refreshToken) return null;

  // Verify the saved session with the backend.
  // A saved token alone does not prove the session is valid.
  const requestRefresh = () =>
    postJson('/api/auth/refresh', { refreshToken });

  let { response, data } = await requestRefresh();

  if (response.status === 429 || response.status >= 500) {
    console.log('Temporary refresh failure. Retrying once...');
    await new Promise<void>((resolve) => {
      setTimeout(resolve, 5000);
    });
    ({ response, data } = await requestRefresh());
  }

  if (response.status === 429) {
    console.log('Session refresh rate-limited. Saved tokens preserved.');
    return null;
  }

  if (!response.ok) {
    if (response.status === 400 || response.status === 401) {
      await clearTokens();
    } else {
      console.log('Temporary session refresh failure. Saved tokens preserved.');
    }
    return null;
  }

  if (!data?.accessToken || !data?.refreshToken) return null;

  await saveTokens(data.accessToken, data.refreshToken);

  // Fetch the current runner's profile using the new access token.
  const profileResponse = await authFetch('/api/runner/me');
  if (!profileResponse.ok) return null;

  const profile = await profileResponse.json();

  return {
    id: String(profile.id ?? profile.userId ?? ''),
    username: String(profile.username ?? ''),
    displayName: String(profile.displayName ?? profile.username ?? 'Runner'),
  };
}

// Ends the session on the server. Throws if the server refused.
export async function logoutOnServer(): Promise<void> {
  const refreshToken = await SecureStore.getItemAsync('refreshToken');
  if (!refreshToken) return;

  const { response, data } = await postJson('/api/auth/logout', { refreshToken });

  // If the session is already invalid (401), local logout is safe.
  if (!response.ok && response.status !== 401) {
    throw new Error(data?.message || 'Server logout failed.');
  }
}
