import * as SecureStore from 'expo-secure-store';
import { API_URL } from '../config/api';
import {
  cacheConfirmedTerritories,
  ConfirmedTerritory,
  getPendingRunIds,
  getRunSyncRecord,
  getUnuploadedRunPoints,
  markPointsUploaded,
  markServerRunEnded,
} from './runStorage';

export type SyncResult = {
  newlyClaimedCount: number;
  confirmedTerritories: ConfirmedTerritory[];
  serverEnded: boolean;
};

let refreshInFlight: Promise<string> | null = null;
const syncing = new Map<string, Promise<SyncResult>>();

async function renewAccessToken(): Promise<string> {
  if (!refreshInFlight) {
    refreshInFlight = (async () => {
      const refreshToken = await SecureStore.getItemAsync('refreshToken');
      if (!refreshToken) throw new Error('Session expired. Please log in.');
      const response = await fetch(`${API_URL}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
      if (!response.ok) {
        throw new Error(`Unable to refresh session (HTTP ${response.status}).`);
      }
      const data = await response.json();
      if (!data.accessToken || !data.refreshToken) {
        throw new Error('Invalid refresh response.');
      }
      await SecureStore.setItemAsync('accessToken', data.accessToken);
      await SecureStore.setItemAsync('refreshToken', data.refreshToken);
      return data.accessToken as string;
    })().finally(() => { refreshInFlight = null; });
  }
  return refreshInFlight;
}

async function postJson(path: string, body?: unknown): Promise<any> {
  let token = await SecureStore.getItemAsync('accessToken');
  if (!token) token = await renewAccessToken();

  const send = (authToken: string) => fetch(`${API_URL}${path}`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${authToken}`,
      'Content-Type': 'application/json',
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });

  let response = await send(token);
  if (response.status === 401) {
    token = await renewAccessToken();
    response = await send(token);
  }
  if (!response.ok) {
    const detail = await response.text().catch(() => '');
    throw new Error(`Run sync failed (HTTP ${response.status}): ${detail.slice(0, 180)}`);
  }
  return response.json();
}

async function syncOnce(runId: string): Promise<SyncResult> {
  const initial = await getRunSyncRecord(runId);
  if (!initial) throw new Error('Run is missing from the local sync queue.');
  if (initial.serverEnded) {
    return { newlyClaimedCount: 0, confirmedTerritories: [], serverEnded: true };
  }

  // Idempotent: retrying the same RunId + StartedAt is safe.
  await postJson('/api/runs/start', {
    runId,
    startedAt: initial.startedAt,
  });

  let lastUploadedId = initial.uploadedThroughId;
  let totalNewClaims = 0;
  let confirmedTerritories: ConfirmedTerritory[] = [];

  // Keep each request below the ASP.NET endpoint's 500-point limit.
  for (;;) {
    const batch = await getUnuploadedRunPoints(runId, lastUploadedId, 100);
    if (batch.length === 0) break;

    const response = await postJson(`/api/runs/${runId}/points`, batch.map((p) => ({
      sequenceNo: p.id,
      legNo: p.legNo,
      latitude: p.latitude,
      longitude: p.longitude,
      accuracyMeters: p.accuracy,
      recordedAt: new Date(p.timestamp).toISOString(),
    })));

    // Advance the cursor ONLY after the server has acknowledged this batch.
    lastUploadedId = batch[batch.length - 1].id;
    await markPointsUploaded(runId, lastUploadedId);
    totalNewClaims += Number(response.newlyClaimedCount ?? 0);
    if (Array.isArray(response.confirmedTerritories)) {
      confirmedTerritories = response.confirmedTerritories;
      await cacheConfirmedTerritories(initial.ownerUserId, runId, confirmedTerritories);
    }
  }

  // Read state again: Finish may have been pressed during an upload.
  const latest = await getRunSyncRecord(runId);
  if (latest?.finishRequested) {
    const response = await postJson(`/api/runs/${runId}/end`);
    if (Array.isArray(response.detectedTerritories)) {
      confirmedTerritories = response.detectedTerritories;
      await cacheConfirmedTerritories(initial.ownerUserId, runId, confirmedTerritories);
    }
    await markServerRunEnded(runId);
    return { newlyClaimedCount: totalNewClaims,
      confirmedTerritories, serverEnded: true };
  }

  return { newlyClaimedCount: totalNewClaims,
    confirmedTerritories, serverEnded: false };
}

// One sync at a time per run in the main JS runtime.
export async function syncRun(runId: string): Promise<SyncResult> {
  const existing = syncing.get(runId);
  if (existing) {
    const result = await existing;
    // If Finish happened during that request, make sure /end is sent too.
    const state = await getRunSyncRecord(runId);
    if (state?.finishRequested && !state.serverEnded) {
      return syncRun(runId);
    }
    return result;
  }
  const work = syncOnce(runId).finally(() => { syncing.delete(runId); });
  syncing.set(runId, work);
  return work;
}

// Used after app restart/reconnection to retry unsent runs for this user.
export async function syncPendingRuns(ownerUserId: string): Promise<void> {
  const pending = await getPendingRunIds(ownerUserId);
  for (const runId of pending) {
    try {
      await syncRun(runId);
    } catch (error) {
      console.log('ILAQA sync pending:', runId, String(error));
      // Leave the queue untouched, retry later.
    }
  }
}
export async function fetchSavedTerritories(): Promise<ConfirmedTerritory[]> {
  let token = await SecureStore.getItemAsync('accessToken');

  if (!token) {
    token = await renewAccessToken();
  }

  const send = (authToken: string) =>
    fetch(`${API_URL}/api/runs/territories`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${authToken}`,
      },
    });

  let response = await send(token);

  // Automatically refresh an expired access token.
  if (response.status === 401) {
    token = await renewAccessToken();
    response = await send(token);
  }

  if (!response.ok) {
    throw new Error(
      `Unable to fetch saved territories (HTTP ${response.status})`
    );
  }

  const territories = await response.json();

  if (!Array.isArray(territories)) {
    throw new Error('Invalid territories response.');
  }

  return territories as ConfirmedTerritory[];
}
export type SavedRunRoute = {
  runId: string;
  startedAt: string;
  legs: {
    legNo: number;
    coordinates: {
      latitude: number;
      longitude: number;
    }[];
  }[];
};

export async function fetchSavedRoutes(): Promise<SavedRunRoute[]> {
  let token = await SecureStore.getItemAsync('accessToken');

  if (!token) {
    token = await renewAccessToken();
  }

  const send = (authToken: string) =>
    fetch(`${API_URL}/api/runs/routes`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${authToken}`,
      },
    });

  let response = await send(token);

  if (response.status === 401) {
    token = await renewAccessToken();
    response = await send(token);
  }

  if (!response.ok) {
    throw new Error(`Unable to fetch routes: HTTP ${response.status}`);
  }

  const routes = await response.json();

  if (!Array.isArray(routes)) {
    throw new Error('Invalid routes response');
  }

  return routes as SavedRunRoute[];
}