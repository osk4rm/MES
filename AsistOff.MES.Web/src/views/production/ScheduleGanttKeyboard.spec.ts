import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import ScheduleGanttView from './ScheduleGanttView.vue';
import { scheduleGanttService, type GanttBar, type GanttSchedule } from '../../services/scheduleGanttService';
import { productionOrderService } from '../../services/productionOrderService';
import type { ProductionOrderResponse } from '../../services/productionOrderService';

// Slice (3/3) F-07/F-14: Gantt bars are keyboard-focusable and movable by
// arrow keys (Enter commits, Esc cancels); touch-scroll starting on a bar
// scrolls instead of dragging (touch-action scoped to the drag handle); the
// view exposes the shared density control with touch-sized defaults.

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
    ...overrides
  };
}

function scheduleFixture(): GanttSchedule {
  return {
    from: '2026-09-21',
    to: '2026-09-27',
    groups: [
      {
        machineId: 'machine-1',
        machineCode: 'WC-1',
        machineName: 'Work Center 1',
        bars: [ganttBar()]
      }
    ]
  };
}

function mountGantt(): VueWrapper {
  return mount(ScheduleGanttView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

function stylePct(style: string | undefined, prop: 'left' | 'width'): number {
  const match = (style ?? '').match(new RegExp(`${prop}: ([\\d.]+)%`));
  const raw: string | undefined = match === null ? undefined : match[1];
  if (raw === undefined) return Number.NaN;
  return Number.parseFloat(raw);
}

describe('ScheduleGanttView keyboard path (F-07)', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    localStorage.clear();
    for (const key of Object.keys(mockQuery)) delete mockQuery[key];
    mockQuery.from = '2026-09-21';
    mockQuery.to = '2026-09-27';
    getScheduleMock.mockResolvedValue(scheduleFixture());
    orderGetMock.mockResolvedValue({
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
    } as ProductionOrderResponse);
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
  });

  it('bars are keyboard-focusable with a button role and label', async () => {
    const wrapper = mountGantt();
    await flushPromises();

    const bar = wrapper.find('[data-testid="gantt-bar-node-a"]');
    expect(bar.exists()).toBe(true);
    expect(bar.attributes('tabindex')).toBe('0');
    expect(bar.attributes('role')).toBe('button');
    expect(bar.attributes('aria-label')).toBeTruthy();
  });

  it('arrow keys preview a day move and Enter commits it', async () => {
    const wrapper = mountGantt();
    await flushPromises();

    const bar = wrapper.find('[data-testid="gantt-bar-node-a"]');
    const leftBefore = stylePct(bar.attributes('style'), 'left');

    await bar.trigger('keydown', { key: 'ArrowRight' });
    await flushPromises();

    // Preview shifts the bar by one day of the 7-day lane.
    const leftPreview = stylePct(wrapper.find('[data-testid="gantt-bar-node-a"]').attributes('style'), 'left');
    expect(leftPreview).toBeGreaterThan(leftBefore);
    expect(wrapper.find('[data-testid="gantt-bar-node-a"]').classes()).toContain('gantt-bar--preview');
    expect(rescheduleMock).not.toHaveBeenCalled();

    await wrapper.find('[data-testid="gantt-bar-node-a"]').trigger('keydown', { key: 'Enter' });
    await vi.waitFor(() => {
      expect(rescheduleMock).toHaveBeenCalledTimes(1);
    });

    const [, cmd] = rescheduleMock.mock.calls[0] as [string, { plannedStart: string; plannedEnd: string }];
    const expectedStart = new Date(2026, 8, 22, 6, 0, 0).getTime() + 86400000;
    expect(Math.abs(new Date(cmd.plannedStart).getTime() - expectedStart)).toBeLessThan(5);
  });

  it('Esc cancels the keyboard preview without committing', async () => {
    const wrapper = mountGantt();
    await flushPromises();

    const bar = wrapper.find('[data-testid="gantt-bar-node-a"]');
    const leftBefore = stylePct(bar.attributes('style'), 'left');

    await bar.trigger('keydown', { key: 'ArrowRight' });
    await flushPromises();
    expect(wrapper.find('[data-testid="gantt-bar-node-a"]').classes()).toContain('gantt-bar--preview');

    await wrapper.find('[data-testid="gantt-bar-node-a"]').trigger('keydown', { key: 'Escape' });
    await flushPromises();

    expect(rescheduleMock).not.toHaveBeenCalled();
    const reverted = wrapper.find('[data-testid="gantt-bar-node-a"]');
    expect(reverted.classes()).not.toContain('gantt-bar--preview');
    expect(stylePct(reverted.attributes('style'), 'left')).toBeCloseTo(leftBefore, 3);
  });

  it('exposes the shared density control with touch-sized defaults (F-14)', async () => {
    const wrapper = mountGantt();
    await flushPromises();

    expect(wrapper.text()).toContain('shopfloor.density.label');
    expect(wrapper.text()).toContain('shopfloor.density.comfortable');
    expect(wrapper.classes()).toContain('shopfloor-view');
  });

  it('scopes touch-action none to the drag handle so bar touch-scroll pans', () => {
    const text = ganttSources['./ScheduleGanttView.vue'] ?? '';

    // Bars pan vertically (scroll) while the explicit handle captures drags.
    expect(text).toMatch(/\.gantt-bar\s*\{[^}]*touch-action:\s*pan-y/);
    expect(text).toMatch(/\.gantt-bar__handle\s*\{[^}]*touch-action:\s*none/);
    expect(text).toContain('gantt-bar__handle');
    expect(text).toContain('scheduleGantt.keyboardHint');
  });
});

const ganttSources = import.meta.glob<string>('./ScheduleGanttView.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});
