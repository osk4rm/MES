import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import DashboardView from './DashboardView.vue';
import { productionOrderService, ProductionOrderStatus } from '../services/productionOrderService';
import { machineService } from '../services/machineService';
import { operatorService } from '../services/operatorService';
import { warehouseService } from '../services/warehouseService';
import i18n from '../i18n';

// Issue #405: strona startowa (DashboardView, domyslne / -> /dashboard)
// pokazuje pelnowymiarowe logo organizacji na calej szerokosci kontentu.
// Hero jest addytywne — istniejace KPI zyja dalej (patrz DashboardView.spec).

vi.mock('../services/productionOrderService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../services/productionOrderService')>();
  return { ...actual, productionOrderService: { browse: vi.fn() } };
});

vi.mock('../services/machineService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../services/machineService')>();
  return { ...actual, machineService: { browse: vi.fn() } };
});

vi.mock('../services/operatorService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../services/operatorService')>();
  return { ...actual, operatorService: { browse: vi.fn() } };
});

vi.mock('../services/warehouseService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../services/warehouseService')>();
  return { ...actual, warehouseService: { browse: vi.fn() } };
});

vi.mock('vue-i18n', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-i18n')>();
  return {
    ...actual,
    useI18n: (): { t: (key: string) => string } => ({
      t: (key: string): string => key
    })
  };
});

// Raw Vue source via Vite (no node:fs so vue-tsc stays happy without
// @types/node). Paths are relative to this file in src/views.
const viewSources = import.meta.glob<string>('./DashboardView.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});

const DASHBOARD_SOURCE: string = viewSources['./DashboardView.vue'] ?? '';

const ordersMock = vi.mocked(productionOrderService.browse);
const machinesMock = vi.mocked(machineService.browse);
const operatorsMock = vi.mocked(operatorService.browse);
const warehousesMock = vi.mocked(warehouseService.browse);

function countPage(totalCount: number): { items: never[]; totalCount: number; totalPages: number } {
  return { items: [], totalCount, totalPages: totalCount > 0 ? 1 : 0 };
}

function mountDashboard(): VueWrapper {
  return mount(DashboardView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

describe('DashboardView organization logo (issue #405)', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    ordersMock.mockImplementation(async (req) => {
      if (req.status === ProductionOrderStatus.Released) return countPage(1);
      if (req.status === ProductionOrderStatus.InProgress) return countPage(0);
      return countPage(0);
    });
    machinesMock.mockResolvedValue(countPage(1));
    operatorsMock.mockResolvedValue(countPage(1));
    warehousesMock.mockResolvedValue(countPage(1));
  });

  it('renders the organization logo hero above the KPI cards', async () => {
    const wrapper = mountDashboard();
    await flushPromises();

    const hero = wrapper.find('section.dashboard__hero');
    expect(hero.exists()).toBe(true);
    expect(hero.attributes('aria-label')).toBe('dashboard.organizationHeroTitle');

    const logo = wrapper.find('[data-testid="organization-logo"]');
    expect(logo.exists()).toBe(true);
    // Vite in testach inline'uje male SVG do data URI, wiec kontrakt
    // pliku (src="/organization-logo.svg") pinuje asercja zrodlowa ponizej;
    // tu wystarczy, ze src niesie dane SVG.
    expect(logo.attributes('src')).toMatch(/svg/);
    // Alt nigdy nie moze byc pusty — czytniki ekranu musza uslyszec logo.
    expect(logo.attributes('alt')).toBe('dashboard.organizationLogoAlt');

    const caption = wrapper.find('.dashboard__hero-caption');
    expect(caption.exists()).toBe(true);
    expect(caption.text()).toContain('dashboard.organizationHeroText');

    // Hero nie zastepuje KPI — ladowanie pulpitu dziala jak wczesniej.
    expect(wrapper.text()).toContain('dashboard.activeOrders');
  });

  it('shows the hero even when the tenant has no data', async () => {
    ordersMock.mockResolvedValue(countPage(0));
    machinesMock.mockResolvedValue(countPage(0));
    operatorsMock.mockResolvedValue(countPage(0));
    warehousesMock.mockResolvedValue(countPage(0));

    const emptyWrapper = mountDashboard();
    await flushPromises();
    expect(emptyWrapper.find('[data-testid="organization-logo"]').exists()).toBe(true);
    expect(emptyWrapper.text()).toContain('dashboard.emptyTitle');
  });

  it('resolves hero strings in both PL and EN locales', () => {
    for (const locale of ['pl', 'en'] as const) {
      const dict = i18n.global.getLocaleMessage(locale) as Record<string, unknown>;
      const dashboard = (dict['dashboard'] ?? {}) as Record<string, unknown>;
      for (const key of ['organizationHeroTitle', 'organizationLogoAlt', 'organizationHeroText'] as const) {
        const value = dashboard[key];
        expect(typeof value).toBe('string');
        expect((value as string).length).toBeGreaterThan(0);
      }
    }
    const pl = (i18n.global.getLocaleMessage('pl') as Record<string, Record<string, unknown>>)['dashboard'] as Record<string, string>;
    const en = (i18n.global.getLocaleMessage('en') as Record<string, Record<string, unknown>>)['dashboard'] as Record<string, string>;
    expect(pl['organizationLogoAlt']).not.toBe(en['organizationLogoAlt']);
  });

  it('styles the logo full-width via token classes (no inline hex, no inline style)', async () => {
    const wrapper = mountDashboard();
    await flushPromises();

    const logo = wrapper.find('[data-testid="organization-logo"]');
    expect(logo.classes()).toContain('dashboard__hero-logo');
    expect(logo.attributes('style')).toBeUndefined();

    expect(DASHBOARD_SOURCE).toContain('.dashboard__hero-logo');
    // Pelna szerokosc kontentu + wypelnienie cover (asset 1600x400 skaluje sie).
    expect(DASHBOARD_SOURCE).toContain('width: 100%');
    expect(DASHBOARD_SOURCE).toContain('object-fit: cover');
    const styleBlock = DASHBOARD_SOURCE.slice(DASHBOARD_SOURCE.indexOf('<style'));
    expect(styleBlock).not.toMatch(/#[0-9a-fA-F]{3,8}/);
  });

  it('references the scalable vector logo asset', async () => {
    const wrapper = mountDashboard();
    await flushPromises();

    const logo = wrapper.find('[data-testid="organization-logo"]');
    expect(logo.attributes('src')).toMatch(/svg/);
    // Zrodlo widoku musi wskazywac ten sam asset — podmiana pliku w public/
    // podmienia logo bez zmian kodu.
    expect(DASHBOARD_SOURCE).toContain('/organization-logo.svg');
    expect(DASHBOARD_SOURCE).toContain('data-testid="organization-logo"');
  });
});
