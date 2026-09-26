import axios, { AxiosError, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios';
import { currentApiBaseUrl } from './apiBaseUrl';
import { CORRELATION_ID_HEADER, generateCorrelationId } from './correlation';
import { useAuthStore } from '../stores/authStore';

/**
 * Frontend resilience (issue #273): shopfloor tablets run on flaky plant
 * WiFi, so the shared axios instance aborts stalled calls, retries an
 * idempotent GET exactly once, and lets views cancel in-flight work on
 * unmount or route change. POST/PUT/PATCH/DELETE never auto-retry.
 */
export const DEFAULT_REQUEST_TIMEOUT_MS = 15000;

const RETRYABLE_STATUS_CODES = new Set([502, 503, 504]);

/** Minimal init bag forwarded from composables to axios (no `any`). */
export interface HttpRequestInit {
  signal?: AbortSignal;
}

/** Extra resilience marker carried on the axios config across the retry. */
interface ResilienceRequestState {
  __resilienceRetried?: boolean;
}

type ResilientConfig = InternalAxiosRequestConfig & ResilienceRequestState;

/**
 * Delay before the single retry. Mutable (not a const) so Vitest can set it
 * to zero and avoid real timers; production keeps the default backoff.
 */
export const httpResilienceOptions = {
  retryDelayMs: 250
};

export function resolveRequestTimeout(env: { [key: string]: unknown }): number {
  const raw = env['VITE_API_TIMEOUT_MS'];
  const parsed = typeof raw === 'string' ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_REQUEST_TIMEOUT_MS;
}

const http = axios.create({
  // Runtime-resolved (issue #271): the web container renders /config.js from
  // the API_BASE_URL env at startup; that wins over the build-time
  // VITE_API_BASE_URL, then the documented default.
  baseURL: currentApiBaseUrl(),
  // Abort stalled calls instead of spinning forever on a dead terminal.
  timeout: resolveRequestTimeout(import.meta.env),
  // Cookie transport (issue #242): the session lives in httpOnly
  // mes_access/mes_refresh cookies, so every API call must carry
  // cookies even cross-origin (Vite :5173 -> API :5080). No bearer token
  // is ever injected — JavaScript never sees a usable token.
  withCredentials: true
});

export const loginPath = '/login';
const authApiPrefix = '/api/auth/';

export function ensureCorrelationId(
  headers: { get?: (name: string) => unknown; set?: (name: string, value: string) => void; [key: string]: unknown },
  generate: () => string = generateCorrelationId
): string {
  const read = (name: string): string | null => {
    if (headers && typeof headers.get === 'function') {
      const current = headers.get(name) as unknown;
      if (typeof current === 'string' && current.trim() !== '') return current;
    }
    const direct = headers[name] as unknown;
    if (typeof direct === 'string' && direct.trim() !== '') return direct;
    const lowered = headers[name.toLowerCase()] as unknown;
    if (typeof lowered === 'string' && lowered.trim() !== '') return lowered;
    return null;
  };
  const existing = read(CORRELATION_ID_HEADER);
  // Preserve a caller-supplied value (including retries, which re-run this
  // interceptor with the header already set) instead of overwriting it.
  if (existing) return existing;
  const next = generate();
  if (headers && typeof headers.set === 'function') {
    headers.set(CORRELATION_ID_HEADER, next);
  } else {
    headers[CORRELATION_ID_HEADER] = next;
  }
  return next;
}

/**
 * Auth-endpoint calls (sign-in / refresh / sign-out) manage the session
 * themselves: a 401 there means bad credentials or an expired refresh
 * cookie, and the caller (LoginView, router guard) decides what happens
 * next. The global expired-session redirect below must not fire for them.
 */
export function isAuthEndpoint(url: string | undefined): boolean {
  return typeof url === 'string' && url.includes(authApiPrefix);
}

/**
 * Builds the login redirect for an expired session, preserving the page
 * the caller was on as the `?redirect` param so sign-in can land back on
 * it. Returns null on the public auth pages (they handle 401 themselves)
 * so no redirect loop is possible. Never carries tokens — only the path.
 */
export function buildLoginRedirectUrl(pathname: string, search: string): string | null {
  const onAuthPage = pathname === '/' || pathname.startsWith('/login') || pathname.startsWith('/register');
  if (onAuthPage) return null;
  return `${loginPath}?redirect=${encodeURIComponent(`${pathname}${search}`)}`;
}

http.interceptors.request.use((config) => {
  ensureCorrelationId(config.headers);
  return config;
});

/** True for user- or router-initiated aborts, which must never be retried. */
export function isCanceledError(err: unknown): boolean {
  if (axios.isCancel(err)) return true;
  const code = (err as { code?: unknown }).code;
  return code === 'ERR_CANCELED';
}

/**
 * True when a failed call may be retried exactly once: idempotent GET only,
 * never retried before, and the failure is a network error, a timeout, or a
 * retryable gateway status. Aborts and mutations always return false.
 */
export function isRetryableHttpError(err: unknown): boolean {
  const ax = err as AxiosError | undefined;
  const config = ax?.config as ResilientConfig | undefined;
  if (!config) return false;
  if (config.method?.toLowerCase() !== 'get') return false;
  if (config.__resilienceRetried) return false;
  if (isCanceledError(err)) return false;
  const status = ax?.response?.status;
  if (status !== undefined) return RETRYABLE_STATUS_CODES.has(status);
  // No response: connection refused / DNS / offline (or a timeout, which
  // axios also surfaces without a response under ECONNABORTED).
  return true;
}

function delay(ms: number): Promise<void> {
  return new Promise<void>((resolve) => {
    setTimeout(resolve, ms);
  });
}

/**
 * Re-dispatches a retryable GET once. The same config object is reused, so
 * the request interceptor preserves the original X-Correlation-ID instead
 * of minting a new one, and the per-attempt axios timeout applies again.
 */
async function retryOnce(error: AxiosError): Promise<AxiosResponse | null> {
  if (!isRetryableHttpError(error)) return null;
  const config = error.config as ResilientConfig;
  config.__resilienceRetried = true;
  await delay(httpResilienceOptions.retryDelayMs);
  return http.request(config);
}

// Controllers created via createRequestController (used by useCrudPage) are
// tracked so a route change can abort the previous page's in-flight calls.
// Untracked direct service calls are unaffected, so views that toast on
// failure cannot emit stray toasts after navigation.
const trackedControllers = new Set<AbortController>();

export function createRequestController(): AbortController {
  const controller = new AbortController();
  trackedControllers.add(controller);
  controller.signal.addEventListener('abort', () => {
    trackedControllers.delete(controller);
  }, { once: true });
  return controller;
}

/** Aborts every tracked in-flight request (called on route change). */
export function abortPendingRequests(): void {
  for (const controller of Array.from(trackedControllers)) {
    try {
      controller.abort();
    } catch { /* ignore - abort must never throw */ }
  }
  trackedControllers.clear();
}

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const retried = await retryOnce(error);
    if (retried) return retried;
    if (error.response?.status === 401 && !isAuthEndpoint(error.config?.url)) {
      const target = buildLoginRedirectUrl(window.location.pathname, window.location.search);
      if (target) {
        // Session expired mid-use: drop the in-memory marker (cookies are
        // already unusable) and bounce to login with the return path.
        try {
          useAuthStore().clearAuth();
        } catch { /* ignore - pinia may be unavailable in tests */ }
        window.location.assign(target);
      }
    }
    return Promise.reject(error);
  }
);

export function extractErrorMessage(err: unknown, fallback: string): string {
  const ax = err as AxiosError<any>;
  const data = ax?.response?.data;
  if (typeof data === 'string') return data;
  if (data && typeof data === 'object') {
    if (typeof data.detail === 'string') return data.detail;
    if (typeof data.title === 'string') return data.title;
    if (typeof data.message === 'string') return data.message;
    if (data.errors && typeof data.errors === 'object') {
      const firstKey = Object.keys(data.errors)[0];
      const firstArr = firstKey ? data.errors[firstKey] : null;
      if (Array.isArray(firstArr) && firstArr.length) return String(firstArr[0]);
    }
  }
  if (ax?.message) return ax.message;
  return fallback;
}

export default http;
