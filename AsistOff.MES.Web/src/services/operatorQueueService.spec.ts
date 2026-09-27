import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  OPERATOR_QUEUE_DEFAULT_TAKE,
  groupSignalsByMachine,
  hasShiftCoverage,
  nextUpOrder,
  operatorQueueService,
  operatorQueueSignalMeta,
  type OperatorShiftQueue,
  type OperatorShiftQueuedOrder,
  type OperatorShiftQueueSignal
} from './operatorQueueService';
import { AndonSignalCategory } from './andonSignalService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);

function order(overrides: Partial<OperatorShiftQueuedOrder> = {}): OperatorShiftQueuedOrder {
  return {
    id: 'order-1',
    code: 'ORDER-1',
    productId: 'product-1',
    productCode: 'PROD-1',
    plannedQuantity: 100,
    producedQuantity: 20,
    scrappedQuantity: 2,
    remainingQuantity: 78,
    machineId: 'machine-1',
    machineCode: 'M-1',
    machineName: 'Line 1',
    priority: 5,
    dueDate: new Date('2026-09-24T00:00:00Z').toISOString(),
    status: 2,
    ...overrides
  };
}

function signal(overrides: Partial<OperatorShiftQueueSignal> = {}): OperatorShiftQueueSignal {
  return {
    id: 'signal-1',
    machineId: 'machine-1',
    machineCode: 'M-1',
    category: AndonSignalCategory.Downtime,
    severity: 'Downtime',
    raisedAt: new Date('2026-09-24T08:00:00Z').toISOString(),
    ...overrides
  };
}

function queue(overrides: Partial<OperatorShiftQueue> = {}): OperatorShiftQueue {
  return {
    operatorCode: 'OP-1',
    operatorId: 'operator-1',
    shift: {
      shiftId: 'shift-1',
      shiftCode: 'AM',
      shiftName: 'Morning',
      date: '2026-09-24',
      windowStartUtc: new Date('2026-09-24T06:00:00Z').toISOString(),
      windowEndUtc: new Date('2026-09-24T14:00:00Z').toISOString(),
      isOvernight: false
    },
    orders: [order()],
    activeSignals: [signal()],
    ...overrides
  };
}

describe('operatorQueueService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getQueue queries the operator-queue endpoint with the operator code and default take', async () => {
    const expected = queue();
    getMock.mockResolvedValue({ data: expected });

    const result = await operatorQueueService.getQueue('OP-1');

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/schedule/operator-queue', {
      params: { operatorCode: 'OP-1', take: OPERATOR_QUEUE_DEFAULT_TAKE }
    });
  });

  it('getQueue forwards an explicit take cap to the backend', async () => {
    getMock.mockResolvedValue({ data: queue({ orders: [] }) });

    await operatorQueueService.getQueue('OP-1', 10);

    expect(getMock).toHaveBeenCalledWith('/api/schedule/operator-queue', {
      params: { operatorCode: 'OP-1', take: 10 }
    });
  });

  it('propagates API errors (e.g. 404 for an unknown operator) to the caller', async () => {
    const failure = new Error('Request failed with status code 404');
    getMock.mockRejectedValue(failure);

    await expect(operatorQueueService.getQueue('NOPE')).rejects.toBe(failure);
  });
});

describe('nextUpOrder', () => {
  it('returns the head of the queue (backend next-up ordering contract)', () => {
    const first = order({ id: 'first' });
    const second = order({ id: 'second' });

    expect(nextUpOrder(queue({ orders: [first, second] }))).toEqual(first);
  });

  it('returns null for an empty queue or a missing queue', () => {
    expect(nextUpOrder(queue({ orders: [] }))).toBeNull();
    expect(nextUpOrder(null)).toBeNull();
  });
});

describe('hasShiftCoverage', () => {
  it('is true only when a covering shift assignment is present', () => {
    expect(hasShiftCoverage(queue())).toBe(true);
    expect(hasShiftCoverage(queue({ shift: null }))).toBe(false);
    expect(hasShiftCoverage(null)).toBe(false);
  });
});

describe('groupSignalsByMachine', () => {
  it('groups signals by Work Center keeping first-seen order', () => {
    const groups = groupSignalsByMachine([
      signal({ id: 's1', machineId: 'm1', machineCode: 'M-1' }),
      signal({ id: 's2', machineId: 'm2', machineCode: 'M-2' }),
      signal({ id: 's3', machineId: 'm1', machineCode: 'M-1' })
    ]);

    expect(groups.map((g) => g.machineId)).toEqual(['m1', 'm2']);
    expect(groups[0]?.signals.map((s) => s.id)).toEqual(['s1', 's3']);
    expect(groups[1]?.signals.map((s) => s.id)).toEqual(['s2']);
    expect(groups[0]?.machineCode).toBe('M-1');
  });

  it('returns an empty list when no signals are open', () => {
    expect(groupSignalsByMachine([])).toEqual([]);
  });
});

describe('operatorQueueSignalMeta', () => {
  it('maps every Andon category to a distinct color-plus-icon signal', () => {
    const icons = new Set([
      operatorQueueSignalMeta(AndonSignalCategory.Downtime).icon,
      operatorQueueSignalMeta(AndonSignalCategory.Quality).icon,
      operatorQueueSignalMeta(AndonSignalCategory.Material).icon,
      operatorQueueSignalMeta(AndonSignalCategory.Other).icon
    ]);

    expect(icons.size).toBe(4);
    expect(operatorQueueSignalMeta(AndonSignalCategory.Downtime).variant).toBe('danger');
  });

  it('fails safe to the abnormal treatment for unknown categories', () => {
    for (const unknown of [0, -1, 99]) {
      const meta = operatorQueueSignalMeta(unknown);
      expect(meta.variant).toBe('danger');
      expect(meta.icon.length).toBeGreaterThan(0);
    }
  });
});
