import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import type { RouteLocationNormalized } from 'vue-router';
import router, { hasRoutePermission, requiredPermissionFor } from './router';
import { useAuthStore } from './stores/authStore';
import { useToastStore } from './stores/toastStore';
import { restoreSession } from './services/authService';
import { createRequestController } from './services/http';
import { Permissions } from './models/authModels';

vi.mock('./services/authService', () => ({
  signIn: vi.fn(),
  signOut: vi.fn(),
  refreshSession: vi.fn(),
  restoreSession: vi.fn(),
  extractSessionPermissions: vi.fn(() => [])
}));

const restoreMock = vi.mocked(restoreSession);

function syntheticRoute(permission: string | undefined): RouteLocationNormalized {
  return {
    matched: [{ meta: permission ? { permission } : {} }]
  } as unknown as RouteLocationNormalized;
}

// Covers the route permission guard of issue #273: routes carrying
// meta.permission deny users without the grant by redirecting to the
// dashboard with a toast, and direct URL entry is blocked the same way.
describe('router permission guard', () => {
  beforeEach(async () => {
    setActivePinia(createPinia());
    restoreMock.mockReset();
    restoreMock.mockResolvedValue({ ok: false, permissions: [] });
    useAuthStore().clearAuth();
    await router.replace('/login');
  });

  it('marks the roles route with the tenant admin requirement', () => {
    const resolved = router.resolve('/settings/roles');

    expect(requiredPermissionFor(resolved)).toBe(Permissions.TenantAdmin);
  });

  it('denies direct URL entry without the grant with a redirect plus toast', async () => {
    useAuthStore().setAuth({ email: 'reader@example.com' }, ['production.read']);

    await router.push('/settings/roles');

    expect(router.currentRoute.value.name).toBe('dashboard');
    const toasts = useToastStore().toasts;
    expect(toasts).toHaveLength(1);
    expect(toasts[0]?.variant).toBe('error');
  });

  it('lets users holding the grant open the guarded route silently', async () => {
    useAuthStore().setAuth({ email: 'admin@example.com' }, [Permissions.TenantAdmin]);

    await router.push('/settings/roles');

    expect(router.currentRoute.value.name).toBe('roles');
    expect(useToastStore().toasts).toHaveLength(0);
  });

  it('restores the session then denies when the grants lack the permission', async () => {
    restoreMock.mockResolvedValue({
      ok: true,
      permissions: ['production.read'],
      email: 'restored@example.com'
    });

    await router.push('/settings/roles');

    expect(useAuthStore().isAuthenticated).toBe(true);
    expect(useAuthStore().user?.email).toBe('restored@example.com');
    expect(router.currentRoute.value.name).toBe('dashboard');
    expect(useToastStore().toasts).toHaveLength(1);
  });

  it('keeps unguarded routes open without any permission', async () => {
    useAuthStore().setAuth({ email: 'reader@example.com' }, []);

    await router.push('/production/orders');

    expect(router.currentRoute.value.name).toBe('production-orders');
    expect(useToastStore().toasts).toHaveLength(0);
  });

  it('evaluates production write, configuration write and tenant admin grants', () => {
    for (const code of [Permissions.ProductionWrite, Permissions.ConfigurationWrite, Permissions.TenantAdmin]) {
      expect(hasRoutePermission(syntheticRoute(code), [])).toBe(false);
      expect(hasRoutePermission(syntheticRoute(code), ['production.read'])).toBe(false);
      expect(hasRoutePermission(syntheticRoute(code), [code])).toBe(true);
    }
    expect(hasRoutePermission(syntheticRoute(undefined), [])).toBe(true);
  });

  it('aborts tracked in-flight requests when navigating away', async () => {
    useAuthStore().setAuth({ email: 'reader@example.com' }, []);
    const inflight = createRequestController();

    await router.push('/dashboard');

    expect(inflight.signal.aborted).toBe(true);
    expect(router.currentRoute.value.name).toBe('dashboard');
  });
});
