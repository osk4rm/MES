import { defineStore } from 'pinia';

export interface AuthUser {
  email?: string;
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    // Cookie session (issue #242): the JWTs live in httpOnly
    // mes_access/mes_refresh cookies that JavaScript cannot read, so the
    // store keeps only a non-sensitive session marker plus user display
    // info in memory. Nothing auth-related is persisted to localStorage
    // or sessionStorage — a reload re-proves the session against
    // POST /api/auth/refresh via refreshSession() (see authService).
    user: null as AuthUser | null,
    authenticated: false as boolean
  }),
  getters: {
    isAuthenticated: (state) => state.authenticated
  },
  actions: {
    setAuth(user: AuthUser | null) {
      this.user = user;
      this.authenticated = true;
    },
    clearAuth() {
      this.user = null;
      this.authenticated = false;
    }
  }
});
