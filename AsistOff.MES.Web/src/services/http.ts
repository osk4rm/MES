import axios, { AxiosError } from 'axios';

const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  // Cookie transport (issue #241): the session lives in httpOnly
  // mes_access/mes_refresh cookies, so every API call must carry
  // cookies even cross-origin (Vite :5173 -> API :5080).
  withCredentials: true
});

http.interceptors.request.use((config) => {
  // Header fallback during transition: when a legacy bearer token is
  // still stored (e.g. older session), keep sending it. Cookie-only
  // sessions send no Authorization header; the API falls back to the
  // mes_access cookie (see Extensions.OnMessageReceived).
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
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
          localStorage.removeItem('mes_auth_user');
          localStorage.removeItem('mes_auth_flag');
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
