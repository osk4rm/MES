import axios, { AxiosError } from 'axios';
import { CORRELATION_ID_HEADER, generateCorrelationId } from './correlation';
import { useAuthStore } from '../stores/authStore';

const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
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

http.interceptors.response.use(
  (response) => response,
  (error: AxiosError<any>) => {
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
