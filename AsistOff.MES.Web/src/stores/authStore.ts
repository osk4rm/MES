import { defineStore } from 'pinia';

export interface AuthUser {
  email?: string;
}

const STORAGE_USER_KEY = 'mes_auth_user';
const STORAGE_FLAG_KEY = 'mes_auth_flag';

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: '' as string,
    user: null as AuthUser | null,
    // Cookie-transport session flag (issue #241): the JWTs live in
    // httpOnly cookies that JavaScript cannot read, so an empty body
    // token still means an authenticated session.
    authenticated: false as boolean
  }),
  getters: {
    isAuthenticated: (state) => state.authenticated || !!state.token
  },
  actions: {
    setAuth(token: string, user: AuthUser) {
      this.token = token;
      this.user = user;
      this.authenticated = true;
      try {
        if (token) {
          localStorage.setItem('token', token);
        } else {
          localStorage.removeItem('token');
        }
        localStorage.removeItem('user');
        localStorage.setItem(STORAGE_USER_KEY, JSON.stringify(user));
        localStorage.setItem(STORAGE_FLAG_KEY, '1');
      } catch { /* ignore */ }
    },
    loadAuth() {
      try {
        this.token = localStorage.getItem('token') || '';
        const stored = localStorage.getItem(STORAGE_USER_KEY) ?? localStorage.getItem('user');
        this.user = stored ? JSON.parse(stored) as AuthUser : null;
        const hasFlag = localStorage.getItem(STORAGE_FLAG_KEY) === '1';
        this.authenticated = (hasFlag && this.user !== null) || this.token !== '';
      } catch {
        this.token = '';
        this.user = null;
        this.authenticated = false;
      }
    },
    clearAuth() {
      this.token = '';
      this.user = null;
      this.authenticated = false;
      try {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        localStorage.removeItem(STORAGE_USER_KEY);
        localStorage.removeItem(STORAGE_FLAG_KEY);
      } catch { /* ignore */ }
    }
  }
});
