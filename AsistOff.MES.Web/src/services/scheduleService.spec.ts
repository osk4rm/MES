import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  currentWeekWindow,
  isDispatchRowOverdue,
  scheduleService,
  toDateOnlyString,
  type DispatchBoard,
  type DispatchOrderRow
} from './scheduleService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);

function row(overrides: Partial<DispatchOrderRow> = {}): DispatchOrderRow {
  return {
    id: 'order-1',
    code: 'ORDER-1',
    productId: 'product-1',
    plannedQuantity: 100,
    producedQuantity: 20,
    scrappedQuantity: 2,
    remainingQuantity: 78,
    priority: 5,
    dueDate: new Date('2026-09-24T00:00:00Z').toISOString(),
    status: 2,
    isOverdue: false,
    ...overrides
  };
}

function board(overrides: Partial<DispatchBoard> = {}): DispatchBoard {
  return {
    from: '2026-09-22',
    to: '2026-09-28',
    days: [
      {
        date: '2026-09-22',
        shifts: [
          {
            shiftId: 'shift-1',
            code: 'AM',
            name: 'Morning',
            startTime: '06:00:00',
            endTime: '14:00:00',
            isOvernight: false,
            headcount: 2
          }
        ]
      }
    ],
    orders: [row()],
    ...overrides
  };
}

describe('scheduleService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getDispatch queries the dispatch endpoint with the from/to window', async () => {
    const expected = board();
    getMock.mockResolvedValue({ data: expected });

    const result = await scheduleService.getDispatch({ from: '2026-09-22', to: '2026-09-28' });

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/schedule/dispatch', {
      params: { from: '2026-09-22', to: '2026-09-28' }
    });
  });

  it('propagates API errors (e.g. 400 for an illegal window) to the caller', async () => {
    const failure = new Error('Request failed with status code 400');
    getMock.mockRejectedValue(failure);

    await expect(scheduleService.getDispatch({ from: '2026-09-28', to: '2026-09-22' })).rejects.toBe(failure);
  });
});

describe('toDateOnlyString', () => {
  it('formats local calendar fields as yyyy-MM-dd', () => {
    expect(toDateOnlyString(new Date(2026, 8, 22))).toBe('2026-09-22');
    expect(toDateOnlyString(new Date(2026, 0, 5))).toBe('2026-01-05');
  });
});

describe('currentWeekWindow', () => {
  it('returns the Monday-to-Sunday week containing the given date', () => {
    // Thursday 2026-09-24 -> week Mon 21st .. Sun 27th.
    expect(currentWeekWindow(new Date(2026, 8, 24))).toEqual({ from: '2026-09-21', to: '2026-09-27' });
  });

  it('keeps a Sunday inside its own week and a Monday at the week start', () => {
    // Sunday 2026-09-27 still belongs to the Mon 21st .. Sun 27th week.
    expect(currentWeekWindow(new Date(2026, 8, 27))).toEqual({ from: '2026-09-21', to: '2026-09-27' });
    // Monday 2026-09-21 starts that same week.
    expect(currentWeekWindow(new Date(2026, 8, 21))).toEqual({ from: '2026-09-21', to: '2026-09-27' });
    // Monday 2026-09-28 starts the next week (Mon 28th .. Sun Oct 4th).
    expect(currentWeekWindow(new Date(2026, 8, 28))).toEqual({ from: '2026-09-28', to: '2026-10-04' });
  });

  it('never exceeds the 31-day backend cap', () => {
    const window = currentWeekWindow(new Date(2026, 8, 24));
    const span = new Date(`${window.to}T00:00:00`).getTime() - new Date(`${window.from}T00:00:00`).getTime();
    expect(span).toBe(6 * 86400000);
  });
});

describe('isDispatchRowOverdue', () => {
  it('mirrors the backend isOverdue flag', () => {
    expect(isDispatchRowOverdue(row({ isOverdue: true }))).toBe(true);
    expect(isDispatchRowOverdue(row({ isOverdue: false }))).toBe(false);
  });
});
