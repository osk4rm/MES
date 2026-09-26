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
    authenticated: false as boolean,
    // Permission grants (issue #273) materialised from the sign-in/refresh
    // response-body claims (never from a token — JavaScript sees none).
    // In-memory only, like the rest of the session. Exact-match semantics
    // mirror the backend AuthorizationBehavior: no parent-code implication.
    permissions: [] as string[]
  }),
  getters: {
    isAuthenticated: (state) => state.authenticated,
    hasPermission: (state) => (code: string): boolean => state.permissions.includes(code)
  },
  actions: {
    setAuth(user: AuthUser | null, permissions?: string[]) {
      this.user = user;
      this.authenticated = true;
      if (permissions !== undefined) this.setPermissions(permissions);
    },
    setPermissions(codes: string[]) {
      this.permissions = [...new Set(codes.filter((c) => typeof c === 'string' && c.length > 0))];
    },
    clearAuth() {
      this.user = null;
      this.authenticated = false;
      this.permissions = [];
    }
  }
});
