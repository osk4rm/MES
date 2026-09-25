import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import ReliabilityDashboardView from './ReliabilityDashboardView.vue';
import {
  formatMinutes,
  formatNullableMinutes,
  reliabilityService,
  type ReliabilityFleetRow,
  type ReliabilitySnapshot,
  type ReliabilityTrend
} from '../../services/reliabilityService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';

// Frontend slice (2/2): the backend snapshot/trend/fleet endpoints are covered
// by the (1/2) suites (handler + validator unit tests plus
// ReliabilityEndpointTests, ReliabilityTrendEndpointTests and
// ReliabilityFleetEndpointTests). These component tests prove the dashboard
// contract instead: KPI cards matching the snapshot, the per-bucket trend and
// fleet ranking tables, the null-MTBF/MTTR notice (never zeros),
// client-side rejection of reversed/overlong windows with prior data kept,
// the cross-tenant 404 feedback and query deep-linking via router.replace
// only (no full page reload). JWT attachment itself lives in the shared
// `http` interceptor; here we prove the cross-tenant consequence: a foreign
// work center id surfaces the not-found state instead of foreign data.

vi.mock('../../services/reliabilityService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/reliabilityService')>();
  return {
    ...actual,
    reliabilityService: {
      getSnapshot: vi.fn(),
      getTrend: vi.fn(),
      getFleet: vi.fn()
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
  useI18n: (): { t: (key: string, params?: Record<string, unknown>) => string } => ({
    t: (key: string): string => key
  })
}));

const snapshotMock = vi.mocked(reliabilityService.getSnapshot);
const trendMock = vi.mocked(reliabilityService.getTrend);
const fleetMock = vi.mocked(reliabilityService.getFleet);
const browseMachinesMock = vi.mocked(machineService.browse);

function machine(overrides: Partial<MachineResponse> = {}): MachineResponse {
  return {
    id: 'machine-1',
    code: 'WC-1',
    name: 'Work Center 1',
    description: null,
    departmentId: null,
    isActive: true,
    capacity: 1,
    efficiencyFactor: 1,
    ...overrides
  };
}

function snapshotFixture(overrides: Partial<ReliabilitySnapshot> = {}): ReliabilitySnapshot {
  return {
    machineId: 'machine-1',
    fromUtc: new Date('2026-09-24T06:00:00Z').toISOString(),
    toUtc: new Date('2026-09-24T14:00:00Z').toISOString(),
    failureCount: 1,
    repairCount: 1,
    windowMinutes: 480,
    uptimeMinutes: 420,
    totalDowntimeMinutes: 60,
    mtbfMinutes: 420,
    mttrMinutes: 60,
    avgRepairMinutes: 45,
    ...overrides
  };
}

function trendFixture(overrides: Partial<ReliabilityTrend> = {}): ReliabilityTrend {
  return {
    machineId: 'machine-1',
    fromUtc: new Date('2026-09-24T06:00:00Z').toISOString(),
    toUtc: new Date('2026-09-24T14:00:00Z').toISOString(),
    bucket: 'Day',
    buckets: [snapshotFixture()],
    ...overrides
  };
}

function fleetRowFixture(overrides: Partial<ReliabilityFleetRow> = {}): ReliabilityFleetRow {
  return {
    machineId: 'machine-1',
    machineCode: 'WC-1',
    machineName: 'Work Center 1',
    departmentId: null,
    failureCount: 1,
    repairCount: 1,
    windowMinutes: 480,
    uptimeMinutes: 420,
    totalDowntimeMinutes: 60,
    mtbfMinutes: 420,
    mttrMinutes: 60,
    avgRepairMinutes: 45,
    ...overrides
  };
}

function notFoundError(): unknown {
  return { response: { status: 404, data: { title: 'Not Found' } }, message: 'Request failed with status code 404' };
}

function seedDeepLink(): void {
  mockQuery.machineId = 'machine-1';
  mockQuery.from = new Date('2026-09-24T06:00:00Z').toISOString();
  mockQuery.to = new Date('2026-09-24T14:00:00Z').toISOString();
  mockQuery.preset = 'custom';
  mockQuery.bucket = 'Day';
}

function seedHappyPath(): void {
  browseMachinesMock.mockResolvedValue({ totalCount: 1, totalPages: 1, items: [machine()] });
  snapshotMock.mockResolvedValue(snapshotFixture());
  trendMock.mockResolvedValue(trendFixture());
  fleetMock.mockResolvedValue([fleetRowFixture()]);
}

function mountDashboard(): VueWrapper {
  return mount(ReliabilityDashboardView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

describe('ReliabilityDashboardView', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    for (const key of Object.keys(mockQuery)) delete mockQuery[key];
    seedHappyPath();
  });

  it('shows KPI cards matching the API snapshot response', async () => {
    seedDeepLink();

    const wrapper = mountDashboard();
    await flushPromises();

    expect(snapshotMock).toHaveBeenCalledWith(
      expect.objectContaining({ machineId: 'machine-1' })
    );
    // Per-card title -> value mapping: a swapped MTBF/MTTR binding or a
    // wrong count binding fails instead of passing on titles alone. The
    // seeded fixture is failureCount 1 / repairCount 1 / MTBF 420 min /
    // MTTR 60 min / avgRepair 45 min over a 480 min window.
    const cards = wrapper.findAll('.reliability-card');
    expect(cards).toHaveLength(8);
    const byTitle = new Map(
      cards.map((card) => [
        card.find('.reliability-card__title').text(),
        card.find('.reliability-card__value').text()
      ])
    );
    expect(byTitle.get('reliabilityDashboard.cards.failures')).toBe('1');
    expect(byTitle.get('reliabilityDashboard.cards.repairs')).toBe('1');
    expect(byTitle.get('reliabilityDashboard.cards.mtbf')).toBe(formatNullableMinutes(420));
    expect(byTitle.get('reliabilityDashboard.cards.mttr')).toBe(formatNullableMinutes(60));
    expect(byTitle.get('reliabilityDashboard.cards.avgRepair')).toBe(formatNullableMinutes(45));
    expect(byTitle.get('reliabilityDashboard.cards.window')).toBe(formatMinutes(480));
    expect(byTitle.get('reliabilityDashboard.cards.uptime')).toBe(formatMinutes(420));
    expect(byTitle.get('reliabilityDashboard.cards.downtime')).toBe(formatMinutes(60));
    // Fully computed window: no em-dash placeholders in the KPI values
    // (the picker label itself uses an em dash by design, so scope this to
    // the cards rather than the whole view text).
    expect([...byTitle.values()].join(' ')).not.toContain('—');
    // URL already matches so no redundant replace is pushed (sync guard).
    expect(mockReplace).not.toHaveBeenCalled();
  });

  it('requests only active machines for the picker', async () => {
    seedDeepLink();

    const wrapper = mountDashboard();
    await flushPromises();
    expect(wrapper.exists()).toBe(true);

    expect(browseMachinesMock).toHaveBeenCalledWith(
      expect.objectContaining({ isActive: true })
    );
  });

  it('shows null MTBF/MTTR states, not zeros or an error, for a window with zero failures', async () => {
    seedDeepLink();
    snapshotMock.mockResolvedValue(
      snapshotFixture({
        failureCount: 0,
        repairCount: 0,
        totalDowntimeMinutes: 0,
        uptimeMinutes: 480,
        mtbfMinutes: null,
        mttrMinutes: null,
        avgRepairMinutes: null
      })
    );

    const wrapper = mountDashboard();
    await flushPromises();
    const toast = useToastStore();

    expect(wrapper.text()).toContain('reliabilityDashboard.nullMtbfTitle');
    expect(wrapper.text()).toContain('reliabilityDashboard.nullMtbfHint');
    expect(wrapper.text()).toContain('—');
    expect(toast.toasts.filter((t) => t.variant === 'error')).toHaveLength(0);
  });

  it('reversed dates are rejected client side before any request, keeping prior data', async () => {
    seedDeepLink();

    const wrapper = mountDashboard();
    await flushPromises();
    expect(snapshotMock).toHaveBeenCalledTimes(1);

    const inputs = wrapper.findAll('input[type="datetime-local"]');
    expect(inputs).toHaveLength(2);
    // From after To: the view must refuse it up front instead of clearing panels.
    await inputs[0]?.setValue('2026-09-24T15:00');
    await inputs[1]?.setValue('2026-09-24T06:00');
    await flushPromises();

    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'error' && t.message === 'reliabilityDashboard.invalidInput')).toBe(true);
    expect(snapshotMock).toHaveBeenCalledTimes(1);
  });

  it('windows over 93 days are rejected client side before any request', async () => {
    seedDeepLink();

    const wrapper = mountDashboard();
    await flushPromises();
    expect(snapshotMock).toHaveBeenCalledTimes(1);

    const inputs = wrapper.findAll('input[type="datetime-local"]');
    await inputs[0]?.setValue('2026-06-01T00:00');
    await inputs[1]?.setValue('2026-09-24T00:01');
    await flushPromises();

    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'error' && t.message === 'reliabilityDashboard.invalidInput')).toBe(true);
    expect(snapshotMock).toHaveBeenCalledTimes(1);
  });

  it('switching the work center reloads the snapshot and re-syncs the URL', async () => {
    browseMachinesMock.mockResolvedValue({
      totalCount: 2,
      totalPages: 1,
      items: [machine(), machine({ id: 'machine-b', code: 'WC-B', name: 'Work Center B' })]
    });

    const wrapper = mountDashboard();
    await flushPromises();
    expect(wrapper.text()).toContain('reliabilityDashboard.noMachine');

    const select = wrapper.find('select');
    expect(select.exists()).toBe(true);
    await select.setValue('machine-b');
    await flushPromises();

    expect(snapshotMock).toHaveBeenCalledWith(expect.objectContaining({ machineId: 'machine-b' }));
    expect(mockReplace).toHaveBeenCalledWith({ query: expect.objectContaining({ machineId: 'machine-b' }) });
  });

  it('shows the not-found feedback for a cross-tenant work center link (foreign id)', async () => {
    seedDeepLink();
    snapshotMock.mockRejectedValue(notFoundError());

    const wrapper = mountDashboard();
    await flushPromises();

    // The deep-linked id belongs to another tenant: the API hides it with
    // 404, so no foreign KPIs are rendered and the not-found feedback is
    // shown instead.
    expect(wrapper.text()).toContain('reliabilityDashboard.notFound');
    expect(wrapper.text()).toContain('reliabilityDashboard.notFoundHint');
    expect(wrapper.text()).not.toContain('reliabilityDashboard.cards.mtbf');
  });

  it('loads trend and fleet panels alongside the snapshot over the shared window', async () => {
    seedDeepLink();

    const wrapper = mountDashboard();
    await flushPromises();
    expect(wrapper.exists()).toBe(true);

    const snapshotQuery = snapshotMock.mock.calls[0]?.[0] as { fromUtc: string; toUtc: string };
    // The trend shares the snapshot window plus the selected bucket; the
    // fleet shares the window (all active tenant work centers, no department).
    expect(trendMock).toHaveBeenCalledWith({
      machineId: 'machine-1',
      fromUtc: snapshotQuery.fromUtc,
      toUtc: snapshotQuery.toUtc,
      bucket: 'Day'
    });
    expect(fleetMock).toHaveBeenCalledWith({
      fromUtc: snapshotQuery.fromUtc,
      toUtc: snapshotQuery.toUtc
    });
    expect(wrapper.text()).toContain('reliabilityDashboard.trendTitle');
    expect(wrapper.text()).toContain('reliabilityDashboard.fleetTitle');
  });

  it('renders null MTBF/MTTR as an em dash in the trend and fleet tables, never as zeros', async () => {
    seedDeepLink();
    trendMock.mockResolvedValue(
      trendFixture({
        buckets: [
          snapshotFixture({ failureCount: 0, mtbfMinutes: null, mttrMinutes: null, avgRepairMinutes: null })
        ]
      })
    );
    fleetMock.mockResolvedValue([
      fleetRowFixture({ failureCount: 0, mtbfMinutes: null, mttrMinutes: null, avgRepairMinutes: null })
    ]);

    const wrapper = mountDashboard();
    await flushPromises();

    const cells = wrapper.findAll('.app-table__td').map((td) => td.text());
    expect(cells.filter((c) => c === '—').length).toBeGreaterThan(0);
  });

  it('renders the fleet ranking in API order (MTBF ascending, nulls last)', async () => {
    seedDeepLink();
    const worst = fleetRowFixture({
      machineId: 'worst',
      machineCode: 'WC-W',
      machineName: 'Worst',
      failureCount: 2,
      mtbfMinutes: 180,
      mttrMinutes: 60
    });
    const best = fleetRowFixture({
      machineId: 'best',
      machineCode: 'WC-B',
      machineName: 'Best',
      failureCount: 1,
      mtbfMinutes: 420,
      mttrMinutes: 60
    });
    const clean = fleetRowFixture({
      machineId: 'clean',
      machineCode: 'WC-C',
      machineName: 'Clean',
      failureCount: 0,
      mtbfMinutes: null,
      mttrMinutes: null
    });
    fleetMock.mockResolvedValue([worst, best, clean]);

    const wrapper = mountDashboard();
    await flushPromises();

    const text = wrapper.text();
    expect(text.indexOf('WC-W')).toBeLessThan(text.indexOf('WC-B'));
    expect(text.indexOf('WC-B')).toBeLessThan(text.indexOf('WC-C'));
  });

  it('switching the bucket reloads the trend and re-syncs the URL', async () => {
    seedDeepLink();

    const wrapper = mountDashboard();
    await flushPromises();
    expect(trendMock).toHaveBeenCalledWith(expect.objectContaining({ bucket: 'Day' }));

    const selects = wrapper.findAll('select');
    expect(selects).toHaveLength(3);
    await selects[2]?.setValue('Week');
    await flushPromises();

    expect(trendMock).toHaveBeenCalledWith(expect.objectContaining({ bucket: 'Week' }));
    expect(mockReplace).toHaveBeenCalledWith({ query: expect.objectContaining({ bucket: 'Week' }) });
  });

  it('restores the trend bucket from the URL on reload', async () => {
    seedDeepLink();
    mockQuery.bucket = 'Week';

    const wrapper = mountDashboard();
    await flushPromises();
    expect(wrapper.exists()).toBe(true);

    expect(trendMock).toHaveBeenCalledWith(expect.objectContaining({ bucket: 'Week' }));
    // URL already matches so no redundant replace is pushed (sync guard).
    expect(mockReplace).not.toHaveBeenCalled();
  });
});
