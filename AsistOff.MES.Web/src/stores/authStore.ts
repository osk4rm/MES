import { defineStore } from 'pinia';

export interface AuthUser {
  email?: string;
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: '' as string,
    user: null as AuthUser | null
  }),
  getters: {
    isAuthenticated: (state) => !!state.token
  },
  actions: {
    setAuth(token: string, user: AuthUser) {
      this.token = token;
      this.user = user;
      try {
        localStorage.setItem('token', token);
        localStorage.setItem('user', JSON.stringify(user));
      } catch { /* ignore */ }
    },
    loadAuth() {
      try {
        this.token = localStorage.getItem('token') || '';
        const user = localStorage.getItem('user');
        this.user = user ? JSON.parse(user) as AuthUser : null;
      } catch {
        this.token = '';
        this.user = null;
      }
    },
    clearAuth() {
      this.token = '';
      this.user = null;
      try {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
      } catch { /* ignore */ }
    }
  }
});
