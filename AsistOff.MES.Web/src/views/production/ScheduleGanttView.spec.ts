import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import ScheduleGanttView from './ScheduleGanttView.vue';
import { scheduleGanttService } from '../../services/scheduleGanttService';
import type { GanttBar, GanttSchedule } from '../../services/scheduleGanttService';
import { productionOrderService } from '../../services/productionOrderService';
import type { ProductionOrderResponse } from '../../services/productionOrderService';
import { useToastStore } from '../../stores/toastStore';

// Slice (3/3) is frontend-only: the Gantt read-model and the reschedule
// write path (window caps, timing math, overlap 409s, tenant isolation) are
// covered by the #304/#305 backend suites. These component tests prove the
// Harmonogram contract instead: lanes grouped per Work Center with bars on
// the right dates, overdue highlighting, read-only unassigned lanes, drag to
// reschedule and resize via PUT with the order xmin token, 409 conflict toast
// with revert, and click-through to the Production Order detail. JWT
// attachment lives in the shared `http` interceptor (both services delegate
// to it); tenant isolation is inherited from the endpoints.

vi.mock('../../services/scheduleGanttService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/scheduleGanttService')>();
  return {
    ...actual,
    scheduleGanttService: {
      getSchedule: vi.fn(),
      rescheduleSegment: vi.fn()
    }
  };
});

vi.mock('../../services/productionOrderService', () => ({
  productionOrderService: {
    get: vi.fn()
  }
}));

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

const getScheduleMock = vi.mocked(scheduleGanttService.getSchedule);
const rescheduleMock = vi.mocked(scheduleGanttService.rescheduleSegment);
const orderGetMock = vi.mocked(productionOrderService.get);

interface MoveCmd {
  productionOrderId: string;
  plannedStart: string;
  plannedEnd: string;
  machineId: string;
  concurrencyToken: string;
}
/**
 * ISO timestamp for a September 2026 wall-clock time in the runner's local
 * zone (see the service spec: the lane window is day-granular local time, so
 * local-built fixtures keep exact date assertions TZ-independent).
 */
function isoLocal(day: number, hour: number, minute = 0): string {
  return new Date(2026, 8, day, hour, minute, 0).toISOString();
}

function ganttBar(overrides: Partial<GanttBar> = {}): GanttBar {
  return {
    productionOrderId: 'order-1',
    productionOrderCode: 'ORDER-1',
    operationNodeId: 'node-a',
    operationCode: 'OP-A',
    operationName: 'Cutting',
    machineId: 'machine-1',
    plannedStart: isoLocal(22, 6),
    plannedEnd: isoLocal(22, 14),
    isOverdue: false,
    isBlocked: false,
    ...overrides
  };
}

function scheduleFixture(): GanttSchedule {  return {
    from: '2026-09-21',
    to: '2026-09-27',
    groups: [
      {
        machineId: 'machine-1',
        machineCode: 'WC-1',
        machineName: 'Work Center 1',
        bars: [
          ganttBar(),
          ganttBar({
            productionOrderId: 'order-2',
            productionOrderCode: 'ORDER-2',
            operationNodeId: 'node-b',
            operationCode: 'OP-B',
            plannedStart: isoLocal(22, 8),
            plannedEnd: isoLocal(22, 10),
            isOverdue: true
          })
        ]
      },
      {
        machineId: null,
        machineCode: null,
        machineName: null,
        bars: [
          ganttBar({
            productionOrderId: 'order-3',
            productionOrderCode: 'ORDER-3',
            operationNodeId: 'node-c',
            operationCode: 'OP-C',
            machineId: null,
            plannedStart: isoLocal(23, 6),
            plannedEnd: isoLocal(23, 8)
          })
        ]
      }
    ]
  };
}

function orderResponse(): ProductionOrderResponse {
  return {
    id: 'order-1',
    code: 'ORDER-1',
    productId: 'product-1',
    recipeId: 'recipe-1',
    recipeVersionId: 'version-1',
    plannedQuantity: 100,
    priority: 5,
    status: 2,
    createdAt: '2026-09-20T00:00:00.000Z',
    producedQuantity: 0,
    scrappedQuantity: 0,
    remainingQuantity: 100,
    confirmationsCount: 0,
    concurrencyToken: 'tok-1'
  };
}

function seedDeepLink(): void {
  mockQuery.from = '2026-09-21';
  mockQuery.to = '2026-09-27';
}

/** Fixture with bar A moved one day forward (what the reload returns after a drop). */
function movedScheduleFixture(): GanttSchedule {
  const next = scheduleFixture();
  const lane = next.groups[0];
  if (lane !== undefined) {
    const rest = lane.bars.slice(1);
    lane.bars = [
      ganttBar({ plannedStart: isoLocal(23, 6), plannedEnd: isoLocal(23, 14) }),
      ...rest
    ];
  }
  return next;
}

function mountGantt(): VueWrapper {
  return mount(ScheduleGanttView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

/** Percentage number for a `left:`/`width:` entry of an inline style string. */
function stylePct(style: string | undefined, prop: 'left' | 'width'): number {
  const match = (style ?? '').match(new RegExp(`${prop}: ([\\d.]+)%`));
  const raw: string | undefined = match === null ? undefined : match[1];
  if (raw === undefined) return Number.NaN;
  return Number.parseFloat(raw);
}

function pointerEvent(type: 'pointerdown' | 'pointermove' | 'pointerup', clientX: number): Event {
  return Object.assign(new Event(type, { bubbles: true, cancelable: true }), {
    button: 0,
    clientX,
    pointerId: 1
  });
}

function lastMove(): { nodeId: string; cmd: MoveCmd } {
  const calls = rescheduleMock.mock.calls;
  const last = calls[calls.length - 1] as [string, MoveCmd] | undefined;
  if (last === undefined) throw new Error('rescheduleSegment was not called');
  return { nodeId: last[0], cmd: last[1] };
}

describe('ScheduleGanttView', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    for (const key of Object.keys(mockQuery)) delete mockQuery[key];
    getScheduleMock.mockResolvedValue(scheduleFixture());
    orderGetMock.mockResolvedValue(orderResponse());
    rescheduleMock.mockResolvedValue({
      id: 'segment-1',
      productionOrderId: 'order-1',
      operationNodeId: 'node-a',
      machineId: 'machine-1',
      plannedStart: '2026-09-23T06:00:00.000Z',
      plannedEnd: '2026-09-23T14:00:00.000Z',
      shiftCoverageWarning: false,
      conflictingSegmentIds: []
    });
    // Lanes are 700px wide in tests so a 100px drag is exactly one day.
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue(new DOMRect(0, 0, 700, 40));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders lanes grouped per Work Center with bars on the right dates', async () => {
    seedDeepLink();

    const wrapper = mountGantt();
    await flushPromises();

    expect(getScheduleMock).toHaveBeenCalledWith({ from: '2026-09-21', to: '2026-09-27' });
    expect(mockReplace).not.toHaveBeenCalled();

    // Lane headers: the assigned Work Center plus the unassigned lane.
    expect(wrapper.text()).toContain('WC-1');
    expect(wrapper.text()).toContain('Work Center 1');
    expect(wrapper.text()).toContain('scheduleGantt.unassignedLane');

    // Bars carry the order/operation codes.
    expect(wrapper.text()).toContain('ORDER-1');
    expect(wrapper.text()).toContain('ORDER-2');
    expect(wrapper.text()).toContain('ORDER-3');

    // Bar A (Tue 06:00, 8h) starts 30h into the 168h week lane.
    const barA = wrapper.find('[data-testid="gantt-bar-node-a"]');
    expect(barA.exists()).toBe(true);
    expect(stylePct(barA.attributes('style'), 'left')).toBeCloseTo((30 / 168) * 100, 1);
    expect(stylePct(barA.attributes('style'), 'width')).toBeCloseTo((8 / 168) * 100, 1);
  });

  it('highlights overdue bars and leaves on-time bars plain', async () => {
    seedDeepLink();

    const wrapper = mountGantt();
    await flushPromises();

    const barA = wrapper.find('[data-testid="gantt-bar-node-a"]');
    const barB = wrapper.find('[data-testid="gantt-bar-node-b"]');
    expect(barA.attributes('data-overdue')).toBe('false');
    expect(barB.attributes('data-overdue')).toBe('true');
    expect(barB.classes()).toContain('gantt-bar--overdue');
    expect(barA.classes()).not.toContain('gantt-bar--overdue');

    // Exactly one overdue badge (the legend uses different keys).
    expect(wrapper.text().split('scheduleGantt.overdue').length - 1).toBe(1);
  });

  it('stacks overlapping bars on separate rows and locks the unassigned lane', async () => {
    seedDeepLink();

    const wrapper = mountGantt();
    await flushPromises();

    // Bars A and B overlap, so B lands on the next sub-row.
    const topA = wrapper.find('[data-testid="gantt-bar-node-a"]').attributes('style') ?? '';
    const topB = wrapper.find('[data-testid="gantt-bar-node-b"]').attributes('style') ?? '';
    const topOf = (style: string): number => {
      const match = style.match(/top: ([\d.]+)px/);
      const raw: string | undefined = match === null ? undefined : match[1];
      return raw === undefined ? Number.NaN : Number.parseFloat(raw);
    };
    expect(topOf(topB)).toBeGreaterThan(topOf(topA));

    // Only the assigned lane offers resize handles; the unassigned bar has none.
    expect(wrapper.findAll('.gantt-bar__resize')).toHaveLength(2);
  });

  it('dragging a bar reschedules it with a fresh order token and lands on the new slot', async () => {
    seedDeepLink();

    const wrapper = mountGantt();
    await flushPromises();
    // The reload after the drop returns the moved schedule.
    getScheduleMock.mockResolvedValueOnce(movedScheduleFixture());

    const barEl = wrapper.find('[data-testid="gantt-bar-node-a"]').element;
    barEl.dispatchEvent(pointerEvent('pointerdown', 100));
    window.dispatchEvent(pointerEvent('pointermove', 200));
    window.dispatchEvent(pointerEvent('pointerup', 200));

    await vi.waitFor(() => {
      expect(rescheduleMock).toHaveBeenCalledTimes(1);
    });
    await flushPromises();
    await flushPromises();

    // 100px of the 700px lane is one day; the order is re-read for the token.
    expect(orderGetMock).toHaveBeenCalledWith('order-1');
    const move = lastMove();
    expect(move.nodeId).toBe('node-a');
    expect(move.cmd.productionOrderId).toBe('order-1');
    expect(move.cmd.machineId).toBe('machine-1');
    expect(move.cmd.concurrencyToken).toBe('tok-1');
    const expectedStart = new Date(2026, 8, 22, 6, 0, 0).getTime() + 86400000;
    expect(Math.abs(new Date(move.cmd.plannedStart).getTime() - expectedStart)).toBeLessThan(5);
    const movedHours = new Date(move.cmd.plannedEnd).getTime() - new Date(move.cmd.plannedStart).getTime();
    expect(Math.abs(movedHours - 8 * 3600000)).toBeLessThan(5);

    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success' && t.message === 'scheduleGantt.moved')).toBe(true);

    // The schedule reloads and the bar lands on the persisted slot.
    expect(getScheduleMock).toHaveBeenCalledTimes(2);
    const landed = wrapper.find('[data-testid="gantt-bar-node-a"]');
    expect(stylePct(landed.attributes('style'), 'left')).toBeCloseTo((54 / 168) * 100, 1);
  });

  it('resizing a bar keeps its start and extends its duration via PUT', async () => {
    seedDeepLink();

    const wrapper = mountGantt();
    await flushPromises();

    const handle = wrapper.find('[data-testid="gantt-bar-node-a"] .gantt-bar__resize');
    expect(handle.exists()).toBe(true);
    handle.element.dispatchEvent(pointerEvent('pointerdown', 600));
    window.dispatchEvent(pointerEvent('pointermove', 640));
    window.dispatchEvent(pointerEvent('pointerup', 640));

    await vi.waitFor(() => {
      expect(rescheduleMock).toHaveBeenCalledTimes(1);
    });
    await flushPromises();
    await flushPromises();

    const move = lastMove();
    expect(new Date(move.cmd.plannedStart).getTime()).toBe(new Date(2026, 8, 22, 6, 0, 0).getTime());
    const expectedEnd = new Date(2026, 8, 22, 14, 0, 0).getTime() + (40 / 700) * 7 * 86400000;
    expect(Math.abs(new Date(move.cmd.plannedEnd).getTime() - expectedEnd)).toBeLessThan(5);
  });

  it('a 409 conflict shows the retry toast and reverts the bar', async () => {
    seedDeepLink();
    rescheduleMock.mockRejectedValueOnce({ response: { status: 409 } });

    const wrapper = mountGantt();
    await flushPromises();

    const barEl = wrapper.find('[data-testid="gantt-bar-node-a"]').element;
    barEl.dispatchEvent(pointerEvent('pointerdown', 100));
    window.dispatchEvent(pointerEvent('pointermove', 200));
    window.dispatchEvent(pointerEvent('pointerup', 200));

    await vi.waitFor(() => {
      expect(rescheduleMock).toHaveBeenCalledTimes(1);
    });
    await flushPromises();
    await flushPromises();

    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'error' && t.message === 'scheduleGantt.conflict')).toBe(true);

    // Revert: the persisted schedule reloads and the bar snaps back.
    expect(getScheduleMock).toHaveBeenCalledTimes(2);
    const reverted = wrapper.find('[data-testid="gantt-bar-node-a"]');
    expect(stylePct(reverted.attributes('style'), 'left')).toBeCloseTo((30 / 168) * 100, 1);
  });

  it('clicking a bar without dragging opens its Production Order', async () => {
    seedDeepLink();

    const wrapper = mountGantt();
    await flushPromises();

    const barEl = wrapper.find('[data-testid="gantt-bar-node-a"]').element;
    barEl.dispatchEvent(pointerEvent('pointerdown', 100));
    window.dispatchEvent(pointerEvent('pointerup', 100));
    await flushPromises();

    expect(rescheduleMock).not.toHaveBeenCalled();
    expect(mockPush).toHaveBeenCalledWith({ name: 'production-order-detail', params: { id: 'order-1' } });
  });

  it('an illegal window shows the error toast and keeps prior data', async () => {
    seedDeepLink();

    const wrapper = mountGantt();
    await flushPromises();
    expect(wrapper.text()).toContain('ORDER-1');
    expect(getScheduleMock).toHaveBeenCalledTimes(1);

    const from = wrapper.findAll('input[type="date"]')[0];
    expect(from).toBeDefined();
    await from?.setValue('2026-09-30');
    await flushPromises();

    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'error' && t.message === 'scheduleGantt.invalidWindow')).toBe(true);
    expect(getScheduleMock).toHaveBeenCalledTimes(1);
    expect(wrapper.text()).toContain('ORDER-1');
  });

  it('shows an error with retry when the schedule fetch fails instead of a stale lane', async () => {
    seedDeepLink();
    getScheduleMock.mockRejectedValueOnce(new Error('offline'));

    const wrapper = mountGantt();
    await flushPromises();

    // A failed window fetch reads as an error, never as an empty schedule.
    expect(wrapper.text()).toContain('offline');
    expect(wrapper.text()).not.toContain('scheduleGantt.empty');
    expect(wrapper.find('[data-testid="gantt-bar-node-a"]').exists()).toBe(false);

    const retry = wrapper.findAll('button').find((b) => b.text().includes('common.retry'));
    expect(retry).toBeDefined();
    await retry?.trigger('click');
    await flushPromises();

    expect(getScheduleMock).toHaveBeenCalledTimes(2);
    expect(wrapper.text()).toContain('ORDER-1');
    expect(wrapper.text()).not.toContain('offline');
  });

  it('shows an empty state when the window has no bars', async () => {
    seedDeepLink();
    getScheduleMock.mockResolvedValue({ from: '2026-09-21', to: '2026-09-27', groups: [] });

    const wrapper = mountGantt();
    await flushPromises();

    expect(wrapper.text()).toContain('scheduleGantt.empty');
  });
});
