import axios, { AxiosError } from 'axios';
import { CORRELATION_ID_HEADER, generateCorrelationId } from './correlation';

const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL
});

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

http.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  ensureCorrelationId(config.headers);
  return config;
});

http.interceptors.response.use(
  (response) => response,
  (error: AxiosError<any>) => {
    if (error.response?.status === 401) {
      const path = window.location.pathname;
      const onAuthPage = path === '/' || path.startsWith('/login') || path.startsWith('/register');
      if (!onAuthPage) {
        try {
          localStorage.removeItem('token');
          localStorage.removeItem('user');
        } catch { /* ignore */ }
        window.location.assign('/login');
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
