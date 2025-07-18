import { defineStore } from 'pinia';

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: '' as string,
    user: null as any,
  }),
  actions: {
    setAuth(token: string, user: any) {
      this.token = token;
      this.user = user;
      localStorage.setItem('token', token);
      localStorage.setItem('user', JSON.stringify(user));
    },
    loadAuth() {
      this.token = localStorage.getItem('token') || '';
      const user = localStorage.getItem('user');
      this.user = user ? JSON.parse(user) : null;
    },
    clearAuth() {
      this.token = '';
      this.user = null;
      localStorage.removeItem('token');
      localStorage.removeItem('user');
    }
  }
});
