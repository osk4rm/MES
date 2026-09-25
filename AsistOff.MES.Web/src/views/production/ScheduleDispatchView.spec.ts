import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import ScheduleDispatchView from './ScheduleDispatchView.vue';
import { scheduleService, type DispatchBoard, type DispatchOrderRow } from '../../services/scheduleService';
import { useToastStore } from '../../stores/toastStore';

// Slice (2/2) is frontend-only: the dispatch endpoint itself (ordering
// contract, window validation, headcount aggregation, tenant isolation) is
// covered by the #189 backend suites. These component tests prove the board
// contract instead: day buckets with shift chips plus headcounts, order rows
// rendered in the backend order with overdue badges, empty days showing an
// empty state instead of an error, date-window reloads, illegal-window toast
// with prior data kept, and row navigation to the Production Order detail.
// JWT attachment lives in the shared `http` interceptor (the service
// delegates to it); tenant isolation is inherited from the endpoint.

vi.mock('../../services/scheduleService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/scheduleService')>();
  return {
    ...actual,
    scheduleService: {
      getDispatch: vi.fn()
    }
  };
});

const mockReplace = vi.fn();
const mockPush = vi.fn();
const mockQuery: Record<string, unknown> = {};

vi.mock('vue-router', () => ({
  useRoute: (): { query: Record<string, unknown> } => ({ query: mockQuery }),
  useRouter: (): { replace: (...args: unknown[]) => void; push: (...args: unknown[]) => void } => ({
    replace: mockReplace,
    push: mockPush
  })
}));

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

const getDispatchMock = vi.mocked(scheduleService.getDispatch);

function overdueRow(): DispatchOrderRow {
  return {
    id: 'order-ovd',
    code: 'OVD-1',
    productId: 'product-1',
    plannedQuantity: 100,
    producedQuantity: 10,
    scrappedQuantity: 1,
    remainingQuantity: 89,
    priority: 5,
    dueDate: new Date('2026-09-20T00:00:00Z').toISOString(),
    status: 2,
    isOverdue: true
  };
}

function dueRow(): DispatchOrderRow {
  return {
    id: 'order-soon',
    code: 'SOON-1',
    productId: 'product-1',
    plannedQuantity: 100,
    producedQuantity: 20,
    scrappedQuantity: 2,
    remainingQuantity: 78,
    priority: 9,
    dueDate: new Date('2026-09-24T00:00:00Z').toISOString(),
    status: 3,
    isOverdue: false
  };
}

function noDueRow(): DispatchOrderRow {
  return {
    ...dueRow(),
    id: 'order-nodue',
    code: 'NODUE-1',
    dueDate: null,
    priority: 0
  };
}

function boardFixture(): DispatchBoard {
  return {
    from: '2026-09-22',
    to: '2026-09-28',
    days: [
      {
        date: '2026-09-22',
        shifts: [
          {
            shiftId: 'shift-am',
            code: 'AM',
            name: 'Morning',
            startTime: '06:00:00',
            endTime: '14:00:00',
            isOvernight: false,
            headcount: 2
          },
          {
            shiftId: 'shift-ni',
            code: 'NI',
            name: 'Night',
            startTime: '22:00:00',
            endTime: '06:00:00',
            isOvernight: true,
            headcount: 0
          }
        ]
      },
      {
        date: '2026-09-23',
        shifts: []
      }
    ],
    // Backend ordering contract: overdue first, then due-date ascending
    // with nulls last. The board renders this order unchanged.
    orders: [overdueRow(), dueRow(), noDueRow()]
  };
}

function seedDeepLink(): void {
  mockQuery.from = '2026-09-22';
  mockQuery.to = '2026-09-28';
}

function mountBoard(): VueWrapper {
  return mount(ScheduleDispatchView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

describe('ScheduleDispatchView', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    for (const key of Object.keys(mockQuery)) delete mockQuery[key];
    getDispatchMock.mockResolvedValue(boardFixture());
  });

  it('shows day buckets with shift chips plus headcounts and order rows from the API', async () => {
    seedDeepLink();

    const wrapper = mountBoard();
    await flushPromises();

    expect(getDispatchMock).toHaveBeenCalledWith({ from: '2026-09-22', to: '2026-09-28' });
    // The URL already matches so no redundant replace is pushed (sync guard).
    expect(mockReplace).not.toHaveBeenCalled();

    // Day buckets with shift chips, names and headcounts.
    expect(wrapper.text()).toContain('AM');
    expect(wrapper.text()).toContain('Morning');
    expect(wrapper.text()).toContain('NI');
    expect(wrapper.text()).toContain('Night');
    expect(wrapper.text()).toContain('scheduleDispatch.headcount');

    // Order rows from the API.
    expect(wrapper.text()).toContain('OVD-1');
    expect(wrapper.text()).toContain('SOON-1');
    expect(wrapper.text()).toContain('NODUE-1');
  });

  it('badges overdue orders and lists them before non-overdue rows', async () => {
    seedDeepLink();

    const wrapper = mountBoard();
    await flushPromises();

    expect(wrapper.text()).toContain('scheduleDispatch.overdue');
    expect(wrapper.text()).toContain('scheduleDispatch.onTime');

    const text = wrapper.text();
    expect(text.indexOf('OVD-1')).toBeLessThan(text.indexOf('SOON-1'));
    expect(text.indexOf('SOON-1')).toBeLessThan(text.indexOf('NODUE-1'));
  });

  it('shows an empty state for days without shifts instead of an error', async () => {
    seedDeepLink();

    const wrapper = mountBoard();
    await flushPromises();
    const toast = useToastStore();

    expect(wrapper.text()).toContain('scheduleDispatch.noShifts');
    expect(toast.toasts.filter((t) => t.variant === 'error')).toHaveLength(0);
  });

  it('shows an empty state for the order table when the window has no orders', async () => {
    seedDeepLink();
    getDispatchMock.mockResolvedValue({ ...boardFixture(), orders: [] });

    const wrapper = mountBoard();
    await flushPromises();

    expect(wrapper.text()).toContain('scheduleDispatch.ordersEmpty');
  });

  it('changing the date window reloads the board', async () => {
    seedDeepLink();

    const wrapper = mountBoard();
    await flushPromises();
    expect(getDispatchMock).toHaveBeenCalledTimes(1);

    // setValue on the native date input already fires the change event
    // through AppInput, so no explicit trigger('change') — that would
    // double-fire onWindowChange and fetch twice.
    const from = wrapper.findAll('input[type="date"]')[0];
    expect(from).toBeDefined();
    await from?.setValue('2026-09-23');
    await flushPromises();

    expect(getDispatchMock).toHaveBeenCalledTimes(2);
    expect(getDispatchMock).toHaveBeenLastCalledWith({ from: '2026-09-23', to: '2026-09-28' });
  });

  it('an illegal window shows the error toast and leaves prior data in place', async () => {
    seedDeepLink();

    const wrapper = mountBoard();
    await flushPromises();
    expect(wrapper.text()).toContain('OVD-1');
    expect(getDispatchMock).toHaveBeenCalledTimes(1);

    const from = wrapper.findAll('input[type="date"]')[0];
    expect(from).toBeDefined();
    await from?.setValue('2026-09-30');
    await flushPromises();

    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'error' && t.message === 'scheduleDispatch.invalidWindow')).toBe(true);
    expect(getDispatchMock).toHaveBeenCalledTimes(1);
    expect(wrapper.text()).toContain('OVD-1');
  });

  it('clicking an order row navigates to its Production Order detail view', async () => {
    seedDeepLink();

    const wrapper = mountBoard();
    await flushPromises();

    const rows = wrapper.findAll('tr.app-table__row');
    expect(rows.length).toBeGreaterThan(0);
    await rows[0]?.trigger('click');
    await flushPromises();

    expect(mockPush).toHaveBeenCalledWith({ name: 'production-order-detail', params: { id: 'order-ovd' } });
  });
});
