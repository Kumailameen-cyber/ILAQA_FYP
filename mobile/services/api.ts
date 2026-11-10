// HTTP helpers for the auth and profile screens: JSON POSTs to the public
// auth endpoints, signed-in requests, and reading error messages.

import * as SecureStore from 'expo-secure-store';
import { API_URL } from '../config/api';

export const TOO_MANY_REQUESTS_MESSAGE =
  'Too many requests. Please wait a minute before trying again.';

// POSTs a JSON body. `data` is the parsed reply, or null if it has no JSON.
export async function postJson(path: string, body: unknown) {
  const response = await fetch(`${API_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  const data = await response.json().catch(() => null);
  return { response, data };
}

// Sends a request with the saved access token.
export async function authFetch(
  path: string,
  init: RequestInit = {}
): Promise<Response> {
  const token = await SecureStore.getItemAsync('accessToken');
  if (!token) throw new Error('Please log in again.');

  return fetch(`${API_URL}${path}`, {
    ...init,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(init.headers as Record<string, string> | undefined),
    },
  });
}

// The server's error message from a failed response, or the fallback.
export async function readErrorMessage(
  response: Response,
  fallback: string
): Promise<string> {
  const data = await response.json().catch(() => null);
  return data?.message || fallback;
}

// Message to show for a caught error.
export function errorMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}
