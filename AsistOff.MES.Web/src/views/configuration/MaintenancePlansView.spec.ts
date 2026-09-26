import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import MaintenancePlansView from './MaintenancePlansView.vue';
import {
  MaintenancePlanTriggerType,
  maintenancePlanService,
  type MaintenancePlanResponse
} from '../../services/maintenancePlanService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';

// Verifier gap for #299 / PR #303 (TESTS_INSUFFICIENT): the backend unit +
// endpoint suites proved the due-state API contract (overdue-first ordering,
// badge flags, completion rollover), but the plans-view criterion ("overdue
// filter + due-within-7-days highlight") had no committed coverage —
// `maintenancePlanService.spec.ts` only asserts request URLs/params. These
// component tests close the automated gap by driving the real view with a
// mocked service: each due state renders its badge, the 7-day highlight
// boundary holds, the overdue-only checkbox narrows browse via `dueBefore`,
// and the raise action posts + reloads.

vi.mock('../../services/maintenancePlanService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/maintenancePlanService')>();
  return {
    ...actual,
    maintenancePlanService: {
      ...actual.maintenancePlanService,
      browse: vi.fn(),
      due: vi.fn(),
      get: vi.fn(),
      create: vi.fn(),
      evaluateDue: vi.fn(),
      raiseNow: vi.fn()
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

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

const browseMock = vi.mocked(maintenancePlanService.browse);
const raiseNowMock = vi.mocked(maintenancePlanService.raiseNow);
const browseMachinesMock = vi.mocked(machineService.browse);

function plan(overrides: Partial<MaintenancePlanResponse> = {}): MaintenancePlanResponse {
  return {
    id: 'plan-1',
    code: 'PM-1',
    name: 'Monthly greasing',
    description: null,
    machineId: 'machine-1',
    machineCode: 'MC-1',
    triggerType: MaintenancePlanTriggerType.Time,
    intervalDays: 30,
    meterIntervalValue: null,
    nextDueAt: new Date('2026-10-26T00:00:00Z').toISOString(),
    lastCompletedAt: null,
    isActive: true,
    isOverdue: false,
    dueInDays: 30,
    ...overrides
  };
}

function machine(overrides: Partial<MachineResponse> = {}): MachineResponse {
  return {
    id: 'machine-1',
    code: 'MC-1',
    name: 'Lathe 1',
    description: null,
    departmentId: null,
    isActive: true,
    capacity: 1,
    efficiencyFactor: 1,
    ...overrides
  };
}

function page<T>(items: T[]): { totalCount: number; totalPages: number; items: T[] } {
  return { totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, items };
}

function mountPlans(): VueWrapper {
  return mount(MaintenancePlansView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

describe('MaintenancePlansView due state', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    browseMachinesMock.mockResolvedValue(page([machine()]));
    browseMock.mockResolvedValue(page([plan()]));
    raiseNowMock.mockResolvedValue({ id: 'order-1' } as never);
  });

  it('renders one badge per due state: overdue, due soon, scheduled, no schedule', async () => {
    browseMock.mockResolvedValue(page([
      plan({ id: 'plan-overdue', code: 'PM-OVER', isOverdue: true, dueInDays: -4 }),
      plan({ id: 'plan-soon', code: 'PM-SOON', isOverdue: false, dueInDays: 3 }),
      plan({ id: 'plan-later', code: 'PM-LATER', isOverdue: false, dueInDays: 30 }),
      plan({ id: 'plan-none', code: 'PM-NONE', isOverdue: false, dueInDays: null, nextDueAt: null })
    ]));

    const wrapper = mountPlans();
    await flushPromises();

    expect(browseMock).toHaveBeenCalledOnce();
    expect(wrapper.text()).toContain('PM-OVER');
    expect(wrapper.text()).toContain('maintenancePlans.states.overdue');
    expect(wrapper.text()).toContain('PM-SOON');
    expect(wrapper.text()).toContain('maintenancePlans.states.dueSoon');
    expect(wrapper.text()).toContain('PM-LATER');
    expect(wrapper.text()).toContain('maintenancePlans.states.scheduled');
    expect(wrapper.text()).toContain('PM-NONE');
    expect(wrapper.text()).toContain('maintenancePlans.states.noSchedule');
  });

  it('holds the 7-day due-soon boundary (7 days highlights, 8 does not)', async () => {
    browseMock.mockResolvedValue(page([
      plan({ id: 'plan-edge', code: 'PM-EDGE', isOverdue: false, dueInDays: 7 }),
      plan({ id: 'plan-outside', code: 'PM-OUT', isOverdue: false, dueInDays: 8 })
    ]));

    const wrapper = mountPlans();
    await flushPromises();

    const text = wrapper.text();
    expect(text).toContain('maintenancePlans.states.dueSoon');
    expect(text).toContain('maintenancePlans.states.scheduled');
    // Each badge appears exactly once: the edge row is due soon, the outer
    // row stays scheduled.
    expect(text.split('maintenancePlans.states.dueSoon')).toHaveLength(2);
    expect(text.split('maintenancePlans.states.scheduled')).toHaveLength(2);
  });

  it('overdue-only narrows browse via dueBefore and unchecking drops it', async () => {
    const wrapper = mountPlans();
    await flushPromises();
    expect(browseMock).toHaveBeenCalledTimes(1);

    const checkbox = wrapper.find('input[type="checkbox"]');
    expect(checkbox.exists()).toBe(true);

    await checkbox.setValue(true);
    await flushPromises();

    const overdueCalls = browseMock.mock.calls;
    expect(overdueCalls.length).toBeGreaterThan(1);
    const byOverdue = overdueCalls[overdueCalls.length - 1]?.[0] as Record<string, unknown>;
    expect(typeof byOverdue['dueBefore']).toBe('string');
    const dueBefore = Date.parse(byOverdue['dueBefore'] as string);
    expect(dueBefore).toBeGreaterThan(Date.now() - 60_000);
    expect(dueBefore).toBeLessThanOrEqual(Date.now() + 60_000);

    await checkbox.setValue(false);
    await flushPromises();

    const clearedCalls = browseMock.mock.calls;
    const cleared = clearedCalls[clearedCalls.length - 1]?.[0] as Record<string, unknown>;
    // The view clears the filter to undefined; the service's buildPagedParams
    // then drops it from the HTTP query (covered by the service spec).
    expect(cleared['dueBefore']).toBeUndefined();
  });

  it('raise posts a manual order for the plan and reloads the list', async () => {
    const wrapper = mountPlans();
    await flushPromises();
    const callsBefore = browseMock.mock.calls.length;

    const raiseBtn = wrapper.findAll('button').find((b) => b.attributes('aria-label') === 'maintenancePlans.raiseNow');
    expect(raiseBtn).toBeDefined();
    await raiseBtn?.trigger('click');
    await flushPromises();

    expect(raiseNowMock).toHaveBeenCalledWith('plan-1');
    expect(browseMock.mock.calls.length).toBeGreaterThan(callsBefore);
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'toasts.created')).toBe(true);
  });

  it('shows no raise action for inactive plans', async () => {
    browseMock.mockResolvedValue(page([plan({ isActive: false })]));

    const wrapper = mountPlans();
    await flushPromises();

    const raiseBtn = wrapper.findAll('button').find((b) => b.attributes('aria-label') === 'maintenancePlans.raiseNow');
    expect(raiseBtn).toBeUndefined();
  });
});
