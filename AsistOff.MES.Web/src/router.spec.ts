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
});
