import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import ComingSoonView from './ComingSoonView.vue';
import { useAuthStore } from '../stores/authStore';
import { Permissions } from '../models/authModels';

// Slice (2/3) F-17: the `/settings` overview stub links onward to the
// settings sections that exist (Roles), filtered by the same permission
// grants as the sidebar — a non-admin sees the generic stub with no
// dead-end link instead of a bounce.

const mockRoute = vi.hoisted(() => ({
  name: undefined as unknown,
  meta: {} as Record<string, unknown>
}));

vi.mock('vue-router', () => ({
  useRoute: () => mockRoute
}));

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

let pinia: Pinia;

function mountStub(): VueWrapper {
  return mount(ComingSoonView, {
    global: {
      plugins: [pinia],
      mocks: { $t: (key: string): string => key },
      stubs: {
        RouterLink: {
          template: '<a :href="to"><slot /></a>',
          props: ['to']
        }
      }
    }
  }) as unknown as VueWrapper;
}

describe('ComingSoonView', () => {
  beforeEach(() => {
    pinia = createPinia();
    setActivePinia(pinia);
    mockRoute.name = undefined;
    mockRoute.meta = {};
  });

  it('links onward to Roles on /settings for an admin user', async () => {
    mockRoute.name = 'settings';
    mockRoute.meta = { titleKey: 'nav.settings', icon: 'pi pi-cog' };
    useAuthStore().setAuth({ email: 'admin@dev.local' }, [Permissions.TenantAdmin]);

    const wrapper = mountStub();
    await flushPromises();

    expect(wrapper.text()).toContain('settings.indexHint');
    const link = wrapper.find('a[href="/settings/roles"]');
    expect(link.exists()).toBe(true);
    expect(link.text()).toContain('nav.roles');
  });

  it('shows the generic stub with no dead-end link on /settings for a non-admin user', async () => {
    mockRoute.name = 'settings';
    mockRoute.meta = { titleKey: 'nav.settings', icon: 'pi pi-cog' };
    useAuthStore().setAuth({ email: 'operator@shop.local' }, []);

    const wrapper = mountStub();
    await flushPromises();

    expect(wrapper.text()).toContain('stubs.title');
    expect(wrapper.find('a[href="/settings/roles"]').exists()).toBe(false);
  });

  it('keeps the generic stub for non-settings routes even for an admin', async () => {
    mockRoute.name = 'something-else';
    mockRoute.meta = {};
    useAuthStore().setAuth({ email: 'admin@dev.local' }, [Permissions.TenantAdmin]);

    const wrapper = mountStub();
    await flushPromises();

    expect(wrapper.text()).toContain('stubs.title');
    expect(wrapper.find('a[href="/settings/roles"]').exists()).toBe(false);
  });
});
