// Network requests with a time limit. React Native's fetch has no timeout:
// one request that never answers (e.g. while the phone switches between
// Wi-Fi and mobile data) kept the map refresh and the GPS upload loop
// waiting forever, until the app was reopened.

export const REQUEST_TIMEOUT_MS = 20000;

export async function fetchWithTimeout(
  url: string,
  init: RequestInit = {},
  timeoutMs: number = REQUEST_TIMEOUT_MS,
  fetchImpl: (url: string, init: RequestInit) => Promise<Response> = fetch
): Promise<Response> {
  const controller = new AbortController();
  let timer: ReturnType<typeof setTimeout> | undefined;
  const timeout = new Promise<never>((_, reject) => {
    timer = setTimeout(() => {
      controller.abort();
      reject(new Error(`Request timed out after ${timeoutMs / 1000} s.`));
    }, timeoutMs);
  });
  try {
    // Also stop waiting if the request ignores the abort signal.
    return await Promise.race([
      fetchImpl(url, { ...init, signal: controller.signal }),
      timeout,
    ]);
  } finally {
    clearTimeout(timer);
  }
}
