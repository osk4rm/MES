import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import ShopfloorDensityToggle from './ShopfloorDensityToggle.vue';
import { ShopfloorDensity } from '../../composables/useShopfloorDisplay';

// F-14: shared shopfloor density affordance — one toggle used by every
// operator-facing view; comfortable (44 px minimum) is the default.
vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

function mountToggle() {
  return mount(ShopfloorDensityToggle, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  });
}

describe('ShopfloorDensityToggle (F-14)', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    localStorage.clear();
  });

  it('renders the shared density label with the comfortable default', () => {
    const wrapper = mountToggle();

    expect(wrapper.text()).toContain('shopfloor.density.label');
    expect(wrapper.text()).toContain('shopfloor.density.comfortable');
    expect(localStorage.getItem('shopfloor-density')).toBeNull();
  });

  it('toggles to compact and persists the choice', async () => {
    const wrapper = mountToggle();

    await wrapper.find('button').trigger('click');
    await flushPromises();

    expect(wrapper.text()).toContain('shopfloor.density.compact');
    expect(localStorage.getItem('shopfloor-density')).toBe(ShopfloorDensity.Compact);
  });

  it('restores the persisted compact density on mount', () => {
    localStorage.setItem('shopfloor-density', 'compact');

    const wrapper = mountToggle();

    expect(wrapper.text()).toContain('shopfloor.density.compact');
  });

  it('documents the 44 px minimum as the shopfloor default', () => {
    const text = toggleSources['./ShopfloorDensityToggle.vue'] ?? '';

    expect(text).toContain('44 px');
  });
});

// Raw Vue source via Vite (same ?raw pattern as the operator-panel tablet
// test: no node:fs so vue-tsc stays happy).
const toggleSources = import.meta.glob<string>('./ShopfloorDensityToggle.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});
