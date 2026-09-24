import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import OeeDashboardView from './OeeDashboardView.vue';
import { oeeService, type OeeLosses, type OeeSnapshot, type OeeTrend } from '../../services/oeeService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';

// Slice (3/3) is frontend-only: the backend snapshot/trend/losses endpoints
// are covered by slices (1/3) and (2/3) suites. These component tests prove
// the dashboard contract instead: factor cards matching the (1/3) snapshot,
// the per-bucket trend table, pareto lists with reason code names, the
// null-factor notice (never zeros), illegal-input toast with prior data kept,
// the cross-tenant 404 feedback and query deep-linking via router.replace
// only (no full page reload). JWT attachment itself lives in the shared
// `http` interceptor; here we prove the cross-tenant consequence: a foreign
// work center id surfaces the not-found state instead of foreign data.

vi.mock('../../services/oeeService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/oeeService')>();
  return {
    ...actual,
    oeeService: {
      getSnapshot: vi.fn(),
      getTrend: vi.fn(),
      getLosses: vi.fn()
    }
  };
});

vi.mock('../../services/machineService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/machineService')>();
  return {
    ...actual,
    machineService: {
      ...actual.machineService,
      browse: vi.fn()
    }
  };
});

const mockReplace = vi.fn();
const mockQuery: Record<string, unknown> = {};

vi.mock('vue-router', () => ({
  useRoute: (): { query: Record<string, unknown> } => ({ query: mockQuery }),
  useRouter: (): { replace: (...args: unknown[]) => void } => ({ replace: mockReplace })
}));

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

const snapshotMock = vi.mocked(oeeService.getSnapshot);
const trendMock = vi.mocked(oeeService.getTrend);
const lossesMock = vi.mocked(oeeService.getLosses);
const browseMachinesMock = vi.mocked(machineService.browse);

function machine(overrides: Partial<MachineResponse> = {}): MachineResponse {
  return {
    id: 'machine-1',
    code: 'WC-1',
    name: 'Work Center 1',
    description: null,
    departmentId: null,
    isActive: true,
    ...overrides
  };
}

function snapshotFixture(overrides: Partial<OeeSnapshot> = {}): OeeSnapshot {
  return {
    machineId: 'machine-1',
    fromUtc: new Date('2026-09-24T06:00:00Z').toISOString(),
    toUtc: new Date('2026-09-24T14:00:00Z').toISOString(),
    idealCycleTimeSeconds: 60,
    availability: 0.875,
    performance: 0.9,
    quality: 0.95,
    oee: 0.7481,
    availabilityComputed: true,
    performanceComputed: true,
    qualityComputed: true,
    plannedProductionTimeMinutes: 480,
    runTimeMinutes: 420,
    downtimeMinutes: 60,
    totalCount: 100,
    goodCount: 95,
    scrapCount: 5,
    ...overrides
  };
}

function trendFixture(): OeeTrend {
  const first = snapshotFixture();
  const second = snapshotFixture({
    fromUtc: new Date('2026-09-24T10:00:00Z').toISOString(),
    toUtc: new Date('2026-09-24T14:00:00Z').toISOString(),
    availability: 0.5,
    performance: 0.5,
    quality: 0.5,
    oee: 0.125
  });
  return {
    machineId: 'machine-1',
    fromUtc: first.fromUtc,
    toUtc: first.toUtc,
    idealCycleTimeSeconds: 60,
    bucket: 'Day',
    buckets: [first, second]
  };
}

function lossesFixture(): OeeLosses {
  return {
    machineId: 'machine-1',
    fromUtc: new Date('2026-09-24T06:00:00Z').toISOString(),
    toUtc: new Date('2026-09-24T14:00:00Z').toISOString(),
    totalDowntimeMinutes: 90,
    downtimePareto: [
      { reasonCodeId: 'reason-1', code: 'DT-1', displayName: 'Breakdown', minutes: 60, share: 60 / 90 },
      { reasonCodeId: 'reason-2', code: 'DT-2', displayName: 'Setup', minutes: 30, share: 30 / 90 }
    ],
    totalScrapQuantity: 15,
    scrapPareto: [
      { reasonCodeId: 'reason-3', code: 'SCR-1', displayName: 'Tolerance over', quantity: 10, share: 10 / 15 },
      { reasonCodeId: 'reason-4', code: 'SCR-2', displayName: 'Scratch', quantity: 5, share: 5 / 15 }
    ]
  };
}

function notFoundError(): unknown {
  return { response: { status: 404, data: { title: 'Not Found' } }, message: 'Request failed with status code 404' };
}

function seedDeepLink(): void {
  mockQuery.machineId = 'machine-1';
  mockQuery.from = new Date('2026-09-24T06:00:00Z').toISOString();
  mockQuery.to = new Date('2026-09-24T14:00:00Z').toISOString();
  mockQuery.ideal = '60';
  mockQuery.bucket = 'Day';
}

function seedHappyPath(): void {
  browseMachinesMock.mockResolvedValue({ totalCount: 1, totalPages: 1, items: [machine()] });
  snapshotMock.mockResolvedValue(snapshotFixture());
  trendMock.mockResolvedValue(trendFixture());
  lossesMock.mockResolvedValue(lossesFixture());
}

function mountDashboard(): VueWrapper {
  return mount(OeeDashboardView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

describe('OeeDashboardView', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    for (const key of Object.keys(mockQuery)) delete mockQuery[key];
    seedHappyPath();
  });

  it('shows factor cards matching the snapshot, the trend table and pareto lists with reason names', async () => {
    seedDeepLink();

    const wrapper = mountDashboard();
    await flushPromises();

    // Factor cards mirror the (1/3) snapshot factors as percentages.
    expect(wrapper.text()).toContain('87.5%');
    expect(wrapper.text()).toContain('90.0%');
    expect(wrapper.text()).toContain('95.0%');
    expect(wrapper.text()).toContain('74.8%');

    // One table for the per-bucket trend plus one per pareto list.
    const tables = wrapper.findAll('table.app-table');
    expect(tables).toHaveLength(3);
    expect(tables[0]?.text()).toContain('50.0%');
    expect(wrapper.text()).toContain('Breakdown');
    expect(wrapper.text()).toContain('Setup');
    expect(wrapper.text()).toContain('Tolerance over');
    expect(wrapper.text()).toContain('66.7%');

    // All three panels were refreshed for the deep-linked work center and
    // window; the URL state is synced via router.replace (no full reload).
    expect(snapshotMock).toHaveBeenCalledWith(
      expect.objectContaining({ machineId: 'machine-1', idealCycleTimeSeconds: 60 })
    );
    expect(trendMock).toHaveBeenCalledWith(expect.objectContaining({ machineId: 'machine-1', bucket: 'Day' }));
    expect(lossesMock).toHaveBeenCalledWith(expect.objectContaining({ machineId: 'machine-1' }));
    expect(mockReplace).toHaveBeenCalledWith({ query: expect.objectContaining({ machineId: 'machine-1' }) });
  });

  it('shows the null-factor notice, not zeros or an error, for a window with no planned time', async () => {
    seedDeepLink();
    snapshotMock.mockResolvedValue(
      snapshotFixture({
        availability: null,
        performance: null,
        quality: null,
        oee: null,
        availabilityComputed: false,
        performanceComputed: false,
        qualityComputed: false,
        plannedProductionTimeMinutes: 0,
        runTimeMinutes: 0,
        downtimeMinutes: 0
      })
    );

    const wrapper = mountDashboard();
    await flushPromises();
    const toast = useToastStore();

    expect(wrapper.text()).toContain('oeeDashboard.nullFactorsTitle');
    expect(wrapper.text()).toContain('oeeDashboard.nullFactorsHint');
    expect(toast.toasts.filter((t) => t.variant === 'error')).toHaveLength(0);
  });

  it('illegal input shows the error toast and leaves prior data in place', async () => {
    seedDeepLink();

    const wrapper = mountDashboard();
    await flushPromises();
    expect(wrapper.text()).toContain('87.5%');
    expect(snapshotMock).toHaveBeenCalledTimes(1);

    // Zero ideal cycle time is rejected by the backend: the view must refuse
    // it up front instead of clearing the panels.
    const ideal = wrapper.find('input[type="number"]');
    expect(ideal.exists()).toBe(true);
    await ideal.setValue('0');
    await ideal.trigger('blur');
    await flushPromises();

    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'error' && t.message === 'oeeDashboard.invalidInput')).toBe(true);
    expect(snapshotMock).toHaveBeenCalledTimes(1);
    expect(trendMock).toHaveBeenCalledTimes(1);
    expect(lossesMock).toHaveBeenCalledTimes(1);
    expect(wrapper.text()).toContain('87.5%');
  });

  it('changing the work center, window or ideal cycle time refreshes all panels', async () => {
    seedDeepLink();

    const wrapper = mountDashboard();
    await flushPromises();
    expect(snapshotMock).toHaveBeenCalledTimes(1);

    // Changing the window refreshes every panel without a page reload.
    const from = wrapper.findAll('input[type="datetime-local"]')[0];
    expect(from).toBeDefined();
    await from?.setValue('2026-09-24T07:00');
    await from?.trigger('change');
    await flushPromises();

    expect(snapshotMock).toHaveBeenCalledTimes(2);
    expect(trendMock).toHaveBeenCalledTimes(2);
    expect(lossesMock).toHaveBeenCalledTimes(2);
    const secondCall = snapshotMock.mock.calls[1]?.[0] as { fromUtc: string };
    expect(secondCall.fromUtc).not.toBe((snapshotMock.mock.calls[0]?.[0] as { fromUtc: string }).fromUtc);
  });

  it('switching the work center reloads the panels and re-syncs the URL', async () => {
    browseMachinesMock.mockResolvedValue({
      totalCount: 2,
      totalPages: 1,
      items: [machine(), machine({ id: 'machine-b', code: 'WC-B', name: 'Work Center B' })]
    });

    const wrapper = mountDashboard();
    await flushPromises();
    expect(wrapper.text()).toContain('oeeDashboard.noMachine');

    const select = wrapper.find('select');
    expect(select.exists()).toBe(true);
    await select.setValue('machine-b');
    await flushPromises();

    expect(snapshotMock).toHaveBeenCalledWith(expect.objectContaining({ machineId: 'machine-b' }));
    expect(mockReplace).toHaveBeenCalledWith({ query: expect.objectContaining({ machineId: 'machine-b' }) });
    expect(wrapper.text()).toContain('87.5%');
  });

  it('shows the not-found feedback for a cross-tenant work center link (foreign id)', async () => {
    seedDeepLink();
    snapshotMock.mockRejectedValue(notFoundError());
    trendMock.mockRejectedValue(notFoundError());
    lossesMock.mockRejectedValue(notFoundError());

    const wrapper = mountDashboard();
    await flushPromises();

    // The deep-linked id belongs to another tenant: the API hides it with
    // 404, so no foreign factors are rendered and the not-found feedback is
    // shown instead.
    expect(wrapper.text()).toContain('oeeDashboard.notFound');
    expect(wrapper.text()).toContain('oeeDashboard.notFoundHint');
    expect(wrapper.findAll('table.app-table')).toHaveLength(0);
    expect(wrapper.text()).not.toContain('87.5%');
  });
});
