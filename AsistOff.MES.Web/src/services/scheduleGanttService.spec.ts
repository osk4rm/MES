import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  assignGanttRows,
  clampMoveToWindow,
  dragDeltaToMs,
  isGanttBarOverdue,
  isGanttWindowValid,
  layoutGanttBar,
  rescheduleErrorKey,
  scheduleGanttService,
  type GanttBar,
  type GanttSchedule
} from './scheduleGanttService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn(),
    put: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);
const putMock = vi.mocked(http.put);

/**
 * ISO timestamp for a wall-clock time in the runner's local zone. The lane
 * window is day-granular in local time (`yyyy-MM-dd` inputs), so fixtures
 * built this way keep exact offsets from the window start in any TZ.
 */
function isoLocal(month: number, day: number, hour: number, minute = 0): string {
  return new Date(2026, month - 1, day, hour, minute, 0).toISOString();
}

function bar(overrides: Partial<GanttBar> = {}): GanttBar {
  return {
    productionOrderId: 'order-1',
    productionOrderCode: 'ORDER-1',
    operationNodeId: 'node-1',
    operationCode: 'OP-10',
    operationName: 'Cutting',
    machineId: 'machine-1',
    plannedStart: isoLocal(9, 22, 6),
    plannedEnd: isoLocal(9, 22, 14),
    isOverdue: false,
    isBlocked: false,
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
        bars: [bar()]
      }
    ]
  };
}

describe('scheduleGanttService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getSchedule queries the gantt endpoint with the from/to window', async () => {
    const expected = scheduleFixture();
    getMock.mockResolvedValue({ data: expected });

    const result = await scheduleGanttService.getSchedule({ from: '2026-09-21', to: '2026-09-27' });

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/schedule/gantt', {
      params: { from: '2026-09-21', to: '2026-09-27' }
    });
  });

  it('getSchedule forwards the optional machine filter', async () => {
    getMock.mockResolvedValue({ data: scheduleFixture() });

    await scheduleGanttService.getSchedule({ from: '2026-09-21', to: '2026-09-27', machineId: 'machine-1' });

    expect(getMock).toHaveBeenCalledWith('/api/schedule/gantt', {
      params: { from: '2026-09-21', to: '2026-09-27', machineId: 'machine-1' }
    });
  });

  it('rescheduleSegment PUTs the mapped move body to the segment endpoint', async () => {
    const expected = {
      id: 'segment-1',
      productionOrderId: 'order-1',
      operationNodeId: 'node-1',
      machineId: 'machine-1',
      plannedStart: '2026-09-23T06:00:00.000Z',
      plannedEnd: '2026-09-23T14:00:00.000Z',
      shiftCoverageWarning: false,
      conflictingSegmentIds: []
    };
    putMock.mockResolvedValue({ data: expected });

    const result = await scheduleGanttService.rescheduleSegment('node-1', {
      productionOrderId: 'order-1',
      plannedStart: '2026-09-23T06:00:00.000Z',
      plannedEnd: '2026-09-23T14:00:00.000Z',
      machineId: 'machine-1',
      concurrencyToken: 'tok-1'
    });

    expect(result).toEqual(expected);
    expect(putMock).toHaveBeenCalledOnce();
    expect(putMock).toHaveBeenCalledWith('/api/schedule/gantt/segments/node-1', {
      productionOrderId: 'order-1',
      plannedStart: '2026-09-23T06:00:00.000Z',
      plannedEnd: '2026-09-23T14:00:00.000Z',
      machineId: 'machine-1',
      concurrencyToken: 'tok-1',
      force: false,
      notes: null
    });
  });

  it('propagates API errors (e.g. 409 on overlap) to the caller', async () => {
    const failure = { response: { status: 409 } };
    putMock.mockRejectedValue(failure);

    await expect(
      scheduleGanttService.rescheduleSegment('node-1', {
        productionOrderId: 'order-1',
        plannedStart: '2026-09-23T06:00:00.000Z',
        plannedEnd: '2026-09-23T14:00:00.000Z',
        machineId: 'machine-1',
        concurrencyToken: 'tok-1'
      })
    ).rejects.toBe(failure);
  });
});

describe('isGanttWindowValid', () => {
  it('accepts the current week window', () => {
    expect(isGanttWindowValid('2026-09-21', '2026-09-27')).toBe(true);
  });

  it('accepts a single-day window and the 30-day span cap edge', () => {
    expect(isGanttWindowValid('2026-09-21', '2026-09-21')).toBe(true);
    // The backend allows To - From + 1 <= 31 days, i.e. at most a 30-day span.
    expect(isGanttWindowValid('2026-09-01', '2026-10-01')).toBe(true);
    expect(isGanttWindowValid('2026-09-01', '2026-10-02')).toBe(false);
  });

  it('rejects reversed and empty windows', () => {
    expect(isGanttWindowValid('2026-09-27', '2026-09-21')).toBe(false);
    expect(isGanttWindowValid('', '2026-09-27')).toBe(false);
    expect(isGanttWindowValid('2026-09-21', '')).toBe(false);
  });
});

describe('layoutGanttBar', () => {
  // Window Mon 2026-09-21 00:00 .. Mon 2026-09-28 00:00 (7 x 24h = 168h).
  const from = '2026-09-21';
  const to = '2026-09-27';

  it('positions a mid-window bar by its share of the window', () => {
    // Tue 06:00 is 30h into the 168h window; the 8h bar spans 8/168.
    const layout = layoutGanttBar(bar(), from, to);

    expect(layout.leftPct).toBeCloseTo((30 / 168) * 100, 10);
    expect(layout.widthPct).toBeCloseTo((8 / 168) * 100, 10);
  });

  it('clamps bars sticking out of the window to the track', () => {
    const before = layoutGanttBar(
      bar({ plannedStart: isoLocal(9, 20, 12), plannedEnd: isoLocal(9, 21, 12) }),
      from,
      to
    );
    expect(before.leftPct).toBe(0);
    expect(before.widthPct).toBeCloseTo((12 / 168) * 100, 10);

    const after = layoutGanttBar(
      bar({ plannedStart: isoLocal(9, 27, 12), plannedEnd: isoLocal(9, 29, 12) }),
      from,
      to
    );
    expect(after.leftPct).toBeCloseTo((156 / 168) * 100, 10);
    expect(after.widthPct).toBeCloseTo((12 / 168) * 100, 10);
  });

  it('returns a zero bar for unparseable input instead of NaN styles', () => {
    expect(layoutGanttBar(bar({ plannedStart: 'not-a-date' }), from, to)).toEqual({ leftPct: 0, widthPct: 0 });
    expect(layoutGanttBar(bar(), 'not-a-date', to)).toEqual({ leftPct: 0, widthPct: 0 });
    expect(layoutGanttBar(bar({ plannedStart: isoLocal(9, 22, 14), plannedEnd: isoLocal(9, 22, 6) }), from, to)).toEqual({
      leftPct: 0,
      widthPct: 0
    });
  });
});

describe('dragDeltaToMs', () => {
  it('converts a lane drag into window milliseconds', () => {
    // 100px of a 700px lane inside a 7-day window is exactly one day.
    expect(dragDeltaToMs(100, 700, '2026-09-21', '2026-09-27')).toBeCloseTo(86400000, 0);
    expect(dragDeltaToMs(-350, 700, '2026-09-21', '2026-09-27')).toBeCloseTo(-3 * 86400000, 0);
  });

  it('returns zero for a zero-width lane or an invalid window', () => {
    expect(dragDeltaToMs(100, 0, '2026-09-21', '2026-09-27')).toBe(0);
    expect(dragDeltaToMs(100, 700, 'not-a-date', '2026-09-27')).toBe(0);
  });
});

describe('clampMoveToWindow', () => {
  const from = '2026-09-21';
  const to = '2026-09-27';
  const start = new Date('2026-09-22T06:00:00.000Z').getTime();
  const end = new Date('2026-09-22T14:00:00.000Z').getTime();

  it('shifts the window by the drag delta preserving duration', () => {
    const moved = clampMoveToWindow(start, end, 86400000, from, to);

    expect(moved.startMs).toBe(start + 86400000);
    expect(moved.endMs).toBe(end + 86400000);
  });

  it('clamps the move so the bar never leaves the lane', () => {
    const windowStart = new Date('2026-09-21T00:00:00').getTime();
    const windowEnd = new Date('2026-09-28T00:00:00').getTime();

    const early = clampMoveToWindow(start, end, -10 * 86400000, from, to);
    expect(early.startMs).toBe(windowStart);
    expect(early.endMs).toBe(windowStart + (end - start));

    const late = clampMoveToWindow(start, end, 10 * 86400000, from, to);
    expect(late.endMs).toBe(windowEnd);
    expect(late.startMs).toBe(windowEnd - (end - start));
  });
});

describe('isGanttBarOverdue', () => {
  it('mirrors the backend isOverdue flag', () => {
    expect(isGanttBarOverdue(bar({ isOverdue: true }))).toBe(true);
    expect(isGanttBarOverdue(bar({ isOverdue: false }))).toBe(false);
  });
});

describe('assignGanttRows', () => {
  it('keeps sequential bars on the first row', () => {
    const rows = assignGanttRows([
      bar({ plannedStart: isoLocal(9, 22, 6), plannedEnd: isoLocal(9, 22, 8) }),
      bar({ plannedStart: isoLocal(9, 22, 8), plannedEnd: isoLocal(9, 22, 10) }),
      bar({ plannedStart: isoLocal(9, 22, 12), plannedEnd: isoLocal(9, 22, 14) })
    ]);

    expect(rows).toEqual([0, 0, 0]);
  });

  it('stacks overlapping bars and reuses freed rows', () => {
    const rows = assignGanttRows([
      bar({ plannedStart: isoLocal(9, 22, 6), plannedEnd: isoLocal(9, 22, 14) }),
      bar({ plannedStart: isoLocal(9, 22, 8), plannedEnd: isoLocal(9, 22, 10) }),
      bar({ plannedStart: isoLocal(9, 22, 9), plannedEnd: isoLocal(9, 22, 11) }),
      bar({ plannedStart: isoLocal(9, 22, 15), plannedEnd: isoLocal(9, 22, 16) })
    ]);

    expect(rows).toEqual([0, 1, 2, 0]);
  });

  it('parks unparseable bars on their own rows', () => {
    const rows = assignGanttRows([bar(), bar({ plannedStart: 'not-a-date' }), bar({ plannedStart: 'also-bad' })]);

    expect(rows[0]).toBe(0);
    expect(rows[1]).toBe(1);
    expect(rows[2]).toBe(2);
  });
});

describe('rescheduleErrorKey', () => {
  it('maps 409 to the conflict toast with retry guidance', () => {
    expect(rescheduleErrorKey({ response: { status: 409 } })).toBe('scheduleGantt.conflict');
  });

  it('maps 404 to the missing-target toast and 403 to access-denied', () => {
    expect(rescheduleErrorKey({ response: { status: 404 } })).toBe('scheduleGantt.moveTargetMissing');
    expect(rescheduleErrorKey({ response: { status: 403 } })).toBe('errors.accessDenied');
  });

  it('maps anything else to the generic save failure', () => {
    expect(rescheduleErrorKey({ response: { status: 500 } })).toBe('errors.saveFailed');
    expect(rescheduleErrorKey({ response: {} })).toBe('errors.saveFailed');
    expect(rescheduleErrorKey(new Error('boom'))).toBe('errors.saveFailed');
    expect(rescheduleErrorKey(null)).toBe('errors.saveFailed');
  });
});
