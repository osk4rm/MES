import { beforeEach, describe, expect, it } from 'vitest';
import { mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { createMemoryHistory, createRouter, type Router } from 'vue-router';
import AppSideNav from './AppSideNav.vue';
import type { NavItem } from '../../sitemap';
import { useAuthStore } from '../../stores/authStore';
import { Permissions } from '../../models/authModels';

// Slice (2/3) F-02 + F-10: permission-gated leaves (Settings → Roles with
// tenant.admin) stay hidden for users who may not open them — the router
// guard remains the defence, the sidebar stops promising the dead end — and
// the nav chrome (nav element label, collapse toggle) resolves via i18n.

const items: NavItem[] = [
  { label: 'nav.dashboard', icon: 'pi pi-chart-pie', route: '/dashboard' },
  {
    label: 'nav.settings',
    icon: 'pi pi-cog',
    route: '/settings',
    children: [{ label: 'nav.roles', icon: 'pi pi-lock', route: '/settings/roles' }]
  }
];

let router: Router;
let pinia: Pinia;

async function mountNav(collapsed = false): Promise<VueWrapper> {
  await router.push('/dashboard');
  return mount(AppSideNav, {
    props: { items, collapsed },
    global: {
      plugins: [router, pinia],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

describe('AppSideNav', () => {
  beforeEach(async () => {
    pinia = createPinia();
    setActivePinia(pinia);
    router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/dashboard', name: 'dashboard', component: { template: '<div />' } },
        { path: '/settings', name: 'settings', component: { template: '<div />' } },
        {
          path: '/settings/roles',
          name: 'roles',
          component: { template: '<div />' },
          meta: { permission: Permissions.TenantAdmin }
        }
      ]
    });
    await router.push('/dashboard');
  });

  it('hides the permission-gated Roles leaf and its group for a non-admin user', async () => {
    useAuthStore().setAuth({ email: 'operator@shop.local' }, []);

    const wrapper = await mountNav();

    expect(wrapper.text()).not.toContain('nav.roles');
    expect(wrapper.find('[href="/settings/roles"]').exists()).toBe(false);
    // The Settings group has no other visible child, so it hides as well.
    expect(wrapper.find('[href="/settings"]').exists()).toBe(false);
  });

  it('shows the Roles leaf for an admin user', async () => {
    useAuthStore().setAuth({ email: 'admin@dev.local' }, [Permissions.TenantAdmin]);

    const wrapper = await mountNav();

    // The Settings group starts collapsed on /dashboard (only the active
    // group auto-opens), so expand it like a user would before asserting
    // the permission-gated leaf is reachable.
    await wrapper.find('button.app-sidenav__link--group').trigger('click');

    expect(wrapper.text()).toContain('nav.roles');
    expect(wrapper.find('[href="/settings/roles"]').exists()).toBe(true);
  });

  it('labels the nav landmark and the collapse toggle via i18n keys', async () => {
    useAuthStore().setAuth({ email: 'admin@dev.local' }, [Permissions.TenantAdmin]);

    const expanded = await mountNav(false);
    expect(expanded.find('nav.app-sidenav__nav').attributes('aria-label')).toBe('nav.main');
    expect(expanded.find('button.app-sidenav__toggle').attributes('aria-label')).toBe('sidenav.collapse');

    const collapsed = await mountNav(true);
    expect(collapsed.find('button.app-sidenav__toggle').attributes('aria-label')).toBe('sidenav.expand');
  });
});
