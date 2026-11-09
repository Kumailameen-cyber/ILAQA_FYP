// Forgotten-run handling on the phone: auto-finish an idle run, and stop a
// run the server has already closed.

import { idleFinishDue } from './idleRule';
import {
  clearActiveRun,
  getActiveRunId,
  getActiveRunState,
  getRunPoints,
  markRunFinishRequested,
  setRunRecording,
} from './runStorage';

type AutoFinishedRun = {
  runId: string;
  endedAt: number;
  activeSeconds: number;
};

// Finishes the active run when the runner has not moved 20 m for 5 minutes
// while it was running (L3). Works like pressing Finish at the moment the
// runner stopped: recording stops and Finish is queued for upload.
// Returns the finished run, or null when the run should go on.
export async function finishIdleRunIfDue(
  stopTracking: () => Promise<void>,
  now: number = Date.now()
): Promise<AutoFinishedRun | null> {
  const state = await getActiveRunState();
  if (!state || !state.isRecording || state.segmentStartedAt === null) {
    return null;
  }

  const points = await getRunPoints(state.runId);
  const due = idleFinishDue(points, state.segmentStartedAt, now);
  if (!due) return null;

  const runningMs = Math.max(0, due.endedAt - state.segmentStartedAt);
  const activeSeconds = Math.floor((state.activeMsBefore + runningMs) / 1000);

  // Same order as the Finish button: stop recording, queue Finish, then
  // clear the active marker.
  await setRunRecording(state.runId, false);
  await stopTracking();
  await markRunFinishRequested(
    state.runId,
    new Date(due.endedAt).toISOString(),
    activeSeconds
  );
  await clearActiveRun();

  return { runId: state.runId, endedAt: due.endedAt, activeSeconds };
}

// The server had already finished this run (6 hours without GPS): stop
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
