import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useAuthStore } from './authStore';

const TOKEN_KEYS = ['token', 'user', 'mes_auth_user', 'mes_auth_flag'];

// Cookie session (issue #242): the JWTs live in httpOnly cookies, so the
// store keeps only a non-sensitive session marker plus user display info
// in memory. Nothing auth-related may reach localStorage/sessionStorage.
describe('authStore cookie session', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    localStorage.clear();
    sessionStorage.clear();
  });

  it('starts unauthenticated with no stored session', () => {
    const store = useAuthStore();

    expect(store.isAuthenticated).toBe(false);
    expect(store.user).toBeNull();
  });

  it('marks sign-in as authenticated keeping only display info', () => {
    const store = useAuthStore();

    store.setAuth({ email: 'admin@dev.local' });

    expect(store.isAuthenticated).toBe(true);
    expect(store.user?.email).toBe('admin@dev.local');
  });

  it('marks a restored cookie session as authenticated without user info', () => {
    const store = useAuthStore();

    store.setAuth(null);

    expect(store.isAuthenticated).toBe(true);
    expect(store.user).toBeNull();
  });

  it('never writes auth state to web storage', () => {
    const store = useAuthStore();

    store.setAuth({ email: 'admin@dev.local' });
    store.setAuth(null);
    store.clearAuth();

    for (const key of TOKEN_KEYS) {
      expect(localStorage.getItem(key)).toBeNull();
      expect(sessionStorage.getItem(key)).toBeNull();
    }
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('clears the session on sign-out', () => {
    const store = useAuthStore();
    store.setAuth({ email: 'admin@dev.local' });

    store.clearAuth();

    expect(store.isAuthenticated).toBe(false);
    expect(store.user).toBeNull();
  });
});
