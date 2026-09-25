import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import router from './router';
import { useAuthStore } from './stores/authStore';

// Guards the reliability dashboard (AC5): `reports/reliability` carries no
// `meta.public`, so the global default-deny `beforeEach` must bounce
// unauthenticated users to `login` instead of rendering the dashboard.
describe('router auth guard', () => {
  beforeEach(async () => {
    setActivePinia(createPinia());
    localStorage.clear();
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

    expect(router.currentRoute.value.name).toBe('login');
    expect(router.currentRoute.value.query['redirect']).toBe('/reports/reliability');
  });

  it('lets authenticated users open reports/reliability', async () => {
    useAuthStore().setAuth('test-token', { email: 'supervisor@example.com' });

    await router.push('/reports/reliability');

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

    expect(router.currentRoute.value.name).toBe('login');
    expect(router.currentRoute.value.query['redirect']).toBe('/settings/roles');
  });

  it('lets authenticated users open settings/roles', async () => {
    useAuthStore().setAuth('test-token', { email: 'admin@example.com' });

    await router.push('/settings/roles');

    expect(router.currentRoute.value.name).toBe('roles');
  });
});
