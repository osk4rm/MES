import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import router from './router';
import { useAuthStore } from './stores/authStore';
import { refreshSession } from './services/authService';

vi.mock('./services/authService', () => ({
  signIn: vi.fn(),
  signOut: vi.fn(),
  refreshSession: vi.fn()
}));

const refreshMock = vi.mocked(refreshSession);

// Guards the reliability dashboard (AC5): `reports/reliability` carries no
// `meta.public`, so the global default-deny `beforeEach` must bounce
// unauthenticated users to `login` instead of rendering the dashboard.
// Cookie session (issue #242): the in-memory state is gone after a reload,
// so the guard re-proves the httpOnly cookies via refreshSession() before
// bouncing.
describe('router auth guard', () => {
  beforeEach(async () => {
    setActivePinia(createPinia());
    refreshMock.mockReset();
    refreshMock.mockResolvedValue(false);
    useAuthStore().clearAuth();
    // Start from a public route so each navigation triggers the guard.
    await router.replace('/login');
  });

  it('keeps the reliability route non-public (default-deny)', () => {
    const resolved = router.resolve('/reports/reliability');
    expect(resolved.meta.public).not.toBe(true);
  });

  it('redirects unauthenticated users away from reports/reliability to login', async () => {
    await router.push('/reports/reliability');

    expect(refreshMock).toHaveBeenCalled();
    expect(router.currentRoute.value.name).toBe('login');
    expect(router.currentRoute.value.query['redirect']).toBe('/reports/reliability');
  });

  it('restores a cookie session on reload instead of bouncing to login', async () => {
    refreshMock.mockResolvedValue(true);

    await router.push('/reports/reliability');

    expect(router.currentRoute.value.name).toBe('reports-reliability');
    expect(useAuthStore().isAuthenticated).toBe(true);
    // User display info stays unknown until the next sign-in.
    expect(useAuthStore().user).toBeNull();
  });

  it('lets authenticated users open reports/reliability', async () => {
    useAuthStore().setAuth({ email: 'supervisor@example.com' });

    await router.push('/reports/reliability');

    expect(refreshMock).not.toHaveBeenCalled();
    expect(router.currentRoute.value.name).toBe('reports-reliability');
  });

  // Guards the Roles view (issue #210 AC4): `settings/roles` carries no
  // `meta.public`, so unauthenticated users bounce to `login` while
  // authenticated admins land on the `roles` route (which restores the
  // selected role from localStorage on mount and re-fetches after each
  // mutation — full click-through lives in the e2e stage).
  it('keeps the roles route non-public (default-deny)', () => {
    const resolved = router.resolve('/settings/roles');
    expect(resolved.name).toBe('roles');
    expect(resolved.meta.public).not.toBe(true);
  });

  it('redirects unauthenticated users away from settings/roles to login', async () => {
    await router.push('/settings/roles');

    expect(refreshMock).toHaveBeenCalled();
    expect(router.currentRoute.value.name).toBe('login');
    expect(router.currentRoute.value.query['redirect']).toBe('/settings/roles');
  });

  it('lets authenticated users open settings/roles', async () => {
    useAuthStore().setAuth({ email: 'admin@example.com' });

    await router.push('/settings/roles');

    expect(router.currentRoute.value.name).toBe('roles');
  });
});
