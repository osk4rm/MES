import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useAuthStore } from './authStore';

// Cookie transport (issue #241, E2E follow-up): the session lives in
// httpOnly cookies and the sign-in body carries empty token strings,
// so the store must treat an empty-token setAuth as authenticated.
describe('authStore cookie session', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    localStorage.clear();
  });

  it('marks an empty-token sign-in as authenticated', () => {
    const store = useAuthStore();

    store.setAuth('', { email: 'admin@dev.local' });

    expect(store.isAuthenticated).toBe(true);
    expect(store.user?.email).toBe('admin@dev.local');
  });

  it('restores a cookie session from storage on reload', () => {
    const store = useAuthStore();
    store.setAuth('', { email: 'admin@dev.local' });

    const reloaded = useAuthStore();
    reloaded.loadAuth();

    expect(reloaded.isAuthenticated).toBe(true);
    expect(reloaded.user?.email).toBe('admin@dev.local');
  });

  it('keeps legacy bearer sessions working', () => {
    const store = useAuthStore();
    store.setAuth('legacy-token', { email: 'admin@dev.local' });

    expect(store.isAuthenticated).toBe(true);

    const reloaded = useAuthStore();
    reloaded.loadAuth();

    expect(reloaded.isAuthenticated).toBe(true);
    expect(reloaded.token).toBe('legacy-token');
  });

  it('clears the session', () => {
    const store = useAuthStore();
    store.setAuth('', { email: 'admin@dev.local' });
    store.clearAuth();

    expect(store.isAuthenticated).toBe(false);
    expect(store.user).toBeNull();
  });
});
