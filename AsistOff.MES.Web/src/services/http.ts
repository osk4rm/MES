import axios, { AxiosError } from 'axios';

const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL
});

http.interceptors.request.use((config) => {
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
