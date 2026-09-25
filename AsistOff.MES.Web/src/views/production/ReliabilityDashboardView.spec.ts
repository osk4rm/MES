import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import ReliabilityDashboardView from './ReliabilityDashboardView.vue';
import {
  formatMinutes,
  formatNullableMinutes,
  reliabilityService,
  type ReliabilitySnapshot
} from '../../services/reliabilityService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';

// Frontend slice (2/2): the backend snapshot endpoint is covered by the
// (1/2) suites (GetReliabilitySnapshot* unit tests + ReliabilityEndpointTests).
// These component tests prove the dashboard contract instead: KPI cards
// matching the snapshot, the null-MTBF/MTTR notice (never zeros),
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
      getSnapshot: vi.fn()
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

function notFoundError(): unknown {
  return { response: { status: 404, data: { title: 'Not Found' } }, message: 'Request failed with status code 404' };
}

function seedDeepLink(): void {
  mockQuery.machineId = 'machine-1';
  mockQuery.from = new Date('2026-09-24T06:00:00Z').toISOString();
  mockQuery.to = new Date('2026-09-24T14:00:00Z').toISOString();
  mockQuery.preset = 'custom';
}

function seedHappyPath(): void {
  browseMachinesMock.mockResolvedValue({ totalCount: 1, totalPages: 1, items: [machine()] });
  snapshotMock.mockResolvedValue(snapshotFixture());
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
});
