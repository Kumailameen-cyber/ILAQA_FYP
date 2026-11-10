// Run sync: uploads a run's GPS points from SQLite to the backend (start,
// points, end), retries offline, and loads the map territories.

import * as SecureStore from 'expo-secure-store';
import { API_URL } from '../config/api';
import { fetchWithTimeout } from './http';
import { saveTokens } from './session';
import {
  cacheConfirmedTerritories,
  ConfirmedTerritory,
  getPendingRunIds,
  getRunSyncRecord,
  getUnuploadedRunPoints,
  markPointsUploaded,
  markRunFinishRequested,
  markServerRunEnded,
} from './runStorage';

type SyncResult = {
  newlyClaimedCount: number;
  serverEnded: boolean;
  // True only for the sync that completed /end.
  endedNow: boolean;
};

const NOTHING_SYNCED: SyncResult = {
  newlyClaimedCount: 0,
  serverEnded: false,
  endedNow: false,
};

// The server no longer accepts GPS for this run: it was already finished
// (for example by the server after 6 hours without GPS). Matches the
// backend's answer to an upload for a run that is not Running or Paused.
const RUN_CLOSED_TEXT = 'cannot be uploaded to this run';
class RunClosedOnServerError extends Error {}

// The server limits GPS uploads per user (HTTP 429 + Retry-After). Points
// stay in SQLite and the upload cursor only moves after an accepted batch,
// so waiting loses nothing.
class SyncRateLimitedError extends Error {}
let uploadsPausedUntil = 0;

// Points per upload request (server maximum is 500).
const UPLOAD_BATCH_SIZE = 250;

let refreshInFlight: Promise<string> | null = null;
const syncing = new Map<string, Promise<SyncResult>>();
const lastSyncStartedAt = new Map<string, number>();

async function renewAccessToken(): Promise<string> {
  if (!refreshInFlight) {
    refreshInFlight = (async () => {
      const refreshToken = await SecureStore.getItemAsync('refreshToken');
      if (!refreshToken) throw new Error('Session expired. Please log in.');
      const response = await fetchWithTimeout(`${API_URL}/api/auth/refresh`, {
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
      await saveTokens(data.accessToken, data.refreshToken);
      return data.accessToken as string;
    })().finally(() => {
      refreshInFlight = null;
    });
  }
  return refreshInFlight;
}

// Sends a request with the access token; renews an expired token once.
async function fetchAuthorized(
  path: string,
  init: { method: string; headers?: Record<string, string>; body?: string }
): Promise<Response> {
  let token = await SecureStore.getItemAsync('accessToken');
  if (!token) token = await renewAccessToken();

  const send = (authToken: string) =>
    fetchWithTimeout(`${API_URL}${path}`, {
      ...init,
      headers: { Authorization: `Bearer ${authToken}`, ...init.headers },
    });

  let response = await send(token);
  if (response.status === 401) {
    token = await renewAccessToken();
    response = await send(token);
  }
  return response;
}

async function postAuthorized(path: string, body?: unknown): Promise<any> {
  const response = await fetchAuthorized(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  if (response.status === 429) {
    const seconds = Number(response.headers.get('Retry-After'));
    const waitSeconds = Number.isFinite(seconds) && seconds > 0 ? seconds : 30;
    uploadsPausedUntil = Date.now() + waitSeconds * 1000;
    throw new SyncRateLimitedError(
      `Server busy: GPS kept on phone, retrying in ${waitSeconds} s.`
    );
  }
  if (!response.ok) {
    const detail = await response.text().catch(() => '');
    if (response.status === 409 && detail.includes(RUN_CLOSED_TEXT)) {
      throw new RunClosedOnServerError(detail.slice(0, 180));
    }
    throw new Error(
      `Run sync failed (HTTP ${response.status}): ${detail.slice(0, 180)}`
    );
  }
  return response.json();
}

async function syncOnce(runId: string): Promise<SyncResult> {
  const initial = await getRunSyncRecord(runId);
  if (!initial) throw new Error('Run is missing from the local sync queue.');
  if (initial.serverEnded) {
    return { ...NOTHING_SYNCED, serverEnded: true };
  }

  // If points were already uploaded, the server has created this run.
  // Otherwise, safely retry /start.
  if (initial.uploadedThroughId === 0) {
    await postAuthorized('/api/runs/start', {
      runId,
      startedAt: initial.startedAt,
    });
  }

  let lastUploadedId = initial.uploadedThroughId;
  let totalNewClaims = 0;

  // Keep each request below the ASP.NET endpoint's 500-point limit.
  for (;;) {
    const batch = await getUnuploadedRunPoints(
      runId,
      lastUploadedId,
      UPLOAD_BATCH_SIZE
    );
    if (batch.length === 0) break;
    // Only point uploads are limited: a pending Finish still goes out.
    if (Date.now() < uploadsPausedUntil) {
      throw new SyncRateLimitedError('Waiting before the next GPS upload.');
    }

    let response: any;
    try {
      response = await postAuthorized(
        `/api/runs/${runId}/points`,
        batch.map((p) => ({
          sequenceNo: p.id,
          legNo: p.legNo,
          latitude: p.latitude,
          longitude: p.longitude,
          accuracyMeters: p.accuracy,
          recordedAt: new Date(p.timestamp).toISOString(),
        }))
      );
    } catch (error) {
      if (error instanceof RunClosedOnServerError) {
        return acceptServerFinish(runId, initial.ownerUserId);
      }
      throw error;
    }

    // Advance the cursor ONLY after the server has acknowledged this batch.
    lastUploadedId = batch[batch.length - 1].id;
    await markPointsUploaded(runId, lastUploadedId);
    totalNewClaims += Number(response.newlyClaimedCount ?? 0);
    if (Array.isArray(response.confirmedTerritories)) {
      await cacheConfirmedTerritories(
        initial.ownerUserId,
        runId,
        response.confirmedTerritories
      );
    }
  }

  // Read state again: Finish may have been pressed during an upload.
  const latest = await getRunSyncRecord(runId);
  if (latest?.finishRequested) {
    // When Finish was pressed and the phone's running time; the server
    // checks both against the GPS (works for an offline Finish too).
    const hasTiming = latest.endedAt !== null || latest.activeSeconds !== null;
    const response = await postAuthorized(
      `/api/runs/${runId}/end`,
      hasTiming
        ? {
            endedAt: latest.endedAt ?? undefined,
            activeSeconds: latest.activeSeconds ?? undefined,
          }
        : undefined
    );
    if (Array.isArray(response.detectedTerritories)) {
      await cacheConfirmedTerritories(
        initial.ownerUserId,
        runId,
        response.detectedTerritories
      );
    }
    await markServerRunEnded(runId);
    return { newlyClaimedCount: totalNewClaims, serverEnded: true, endedNow: true };
  }

  return { ...NOTHING_SYNCED, newlyClaimedCount: totalNewClaims };
}

// The server finished this run before the phone did. Confirm with /end
// (answers "already completed" and the run's land), then stop retrying it:
// GPS recorded after the server's finish cannot be added any more.
async function acceptServerFinish(
  runId: string,
  ownerUserId: string
): Promise<SyncResult> {
  const response = await postAuthorized(`/api/runs/${runId}/end`);
  if (Array.isArray(response.detectedTerritories)) {
    await cacheConfirmedTerritories(ownerUserId, runId, response.detectedTerritories);
  }
  await markRunFinishRequested(runId);
  await markServerRunEnded(runId);
  return { newlyClaimedCount: 0, serverEnded: true, endedNow: true };
}

// One sync at a time per run in the main JS runtime. minIntervalMs skips a
// sync that comes too soon after the previous one (the background GPS task
// fires on every fix, every 1-3 s while running).
export async function syncRun(
  runId: string,
  options: { minIntervalMs?: number } = {}
): Promise<SyncResult> {
  const now = Date.now();
  const last = lastSyncStartedAt.get(runId) ?? 0;
  if (options.minIntervalMs && now - last < options.minIntervalMs) {
    return NOTHING_SYNCED;
  }
  lastSyncStartedAt.set(runId, now);

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
  const work = syncOnce(runId).finally(() => {
    syncing.delete(runId);
  });
  syncing.set(runId, work);
  return work;
}

// Used after app restart/reconnection to retry unsent runs for this user.
// Returns how many runs finished syncing now (their routes changed).
export async function syncPendingRuns(ownerUserId: string): Promise<number> {
  const pending = await getPendingRunIds(ownerUserId);
  let endedNow = 0;
  for (const runId of pending) {
    try {
      const result = await syncRun(runId);
      if (result.endedNow) endedNow++;
    } catch (error) {
      console.log('ILAQA sync pending:', runId, String(error));
      // Leave the queue untouched, retry later.
      if (error instanceof SyncRateLimitedError) break;
    }
  }
  return endedNow;
}

// Every runner's active territories, for the map.
export async function fetchSavedTerritories(): Promise<ConfirmedTerritory[]> {
  const response = await fetchAuthorized('/api/runs/territories', {
    method: 'GET',
  });

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
