// Stops a run on the phone when the server has already finished it.
// (Resting does not finish a run: only Finish, or the server's
// abandoned-run safety net, ends it.)

import {
  clearActiveRun,
  getActiveRunId,
  setRunRecording,
} from './runStorage';

// The server had already finished this run (no GPS for many hours): stop
// recording into it on the phone. Returns true when the phone's active run
// was stopped.
export async function stopRunClosedByServer(
  runId: string,
  stopTracking: () => Promise<void>
): Promise<boolean> {
  if ((await getActiveRunId()) !== runId) return false;
  await setRunRecording(runId, false);
  await stopTracking();
  await clearActiveRun();
  return true;
}
