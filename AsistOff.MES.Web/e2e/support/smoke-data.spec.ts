import { describe, expect, it } from 'vitest';
import {
  buildSmokeTag,
  confirmationPayload,
  createLotPayload,
  extractOrderIdFromUrl,
  findSmokeOrder,
  isDispatchBoardShape,
  machineOptionLabel,
  smokeCodes,
  smokeCleanupPlan,
  upstreamContainsLot,
  type TraceabilityResponse
} from './smoke-data';

describe('buildSmokeTag', () => {
  it('mints a 6-character uppercase tag', () => {
    const tag = buildSmokeTag(1727222400000, 0.5);

    expect(tag).toMatch(/^[A-Z0-9]{6}$/);
  });

  it('differs across runs so data stays isolated', () => {
    const first = buildSmokeTag(1727222400000, 0.1);
    const second = buildSmokeTag(1727222401000, 0.9);

    expect(first).not.toBe(second);
  });
});

describe('smokeCodes', () => {
  it('derives every seeded code from the tag', () => {
    const codes = smokeCodes('AB12CD');

    expect(codes.orderCode).toBe('SMK-AB12CD-ORD');
    expect(codes.producedLotCode).toBe('SMK-AB12CD-PRD');
    expect(codes.consumedLotCode).toBe('SMK-AB12CD-CON');
    expect(codes.recipeCode).toBe('SMK-AB12CD-RCP');
    expect(codes.machineCode).toBe('SMK-AB12CD-MAC');
    expect(codes.machineName).toBe('Smoke Work Center');
  });

  it('sanitizes hostile tags so codes stay backend-valid', () => {
    const codes = smokeCodes('a/b\\c:d;e');

    expect(codes.orderCode).toMatch(/^SMK-[A-Z0-9]{1,6}-ORD$/);
  });

  it('falls back to a placeholder when the tag is empty', () => {
    expect(smokeCodes('').orderCode).toBe('SMK-XXXXXX-ORD');
  });
});

describe('machineOptionLabel', () => {
  it('matches the "<code> — <name>" dropdown format', () => {
    expect(machineOptionLabel(smokeCodes('AB12CD'))).toBe('SMK-AB12CD-MAC — Smoke Work Center');
  });
});

describe('createLotPayload', () => {
  it('builds a CreateLotRequest-shaped payload', () => {
    const payload = createLotPayload('SMK-AB12CD-PRD', 'product-1', 'unit-1', 50);

    expect(payload).toMatchObject({
      code: 'SMK-AB12CD-PRD',
      productId: 'product-1',
      measureUnitId: 'unit-1',
      quantity: 50
    });
  });
});

describe('confirmationPayload', () => {
  it('links the produced lot with one consumed lot', () => {
    const payload = confirmationPayload('order-1', 'machine-1', 'lot-prd', 'lot-con', 5);

    expect(payload).toMatchObject({
      productionOrderId: 'order-1',
      machineId: 'machine-1',
      producedLotId: 'lot-prd',
      goodQuantity: 10,
      consumedLots: [{ lotId: 'lot-con', quantity: 5 }]
    });
    expect(typeof payload['reportedAt']).toBe('string');
  });
});

describe('findSmokeOrder', () => {
  const rows = [
    { id: 'id-1', code: 'OTHER-1', status: 2, isOverdue: false },
    { id: 'id-2', code: 'SMK-AB12CD-ORD', status: 2, isOverdue: false }
  ];

  it('finds the seeded order by exact code', () => {
    expect(findSmokeOrder(rows, 'SMK-AB12CD-ORD')?.id).toBe('id-2');
  });

  it('returns null when the board has not picked it up yet', () => {
    expect(findSmokeOrder(rows, 'SMK-ZZZZZZ-ORD')).toBeNull();
    expect(findSmokeOrder([], 'SMK-AB12CD-ORD')).toBeNull();
  });

  it('does not substring-match neighbouring runs', () => {
    expect(findSmokeOrder(rows, 'SMK-AB12CD')).toBeNull();
  });
});

describe('upstreamContainsLot', () => {
  const trace: TraceabilityResponse = {
    rootLotId: 'lot-prd',
    rootLotCode: 'SMK-AB12CD-PRD',
    nodes: [{ lotId: 'lot-con', lotCode: 'SMK-AB12CD-CON', depth: 1 }],
    truncated: false
  };

  it('detects the consumed lot in the upstream trace', () => {
    expect(upstreamContainsLot(trace, 'SMK-AB12CD-CON')).toBe(true);
  });

  it('rejects traces without the consumed lot', () => {
    expect(upstreamContainsLot(trace, 'SMK-AB12CD-PRD')).toBe(false);
    expect(upstreamContainsLot({ ...trace, nodes: [] }, 'SMK-AB12CD-CON')).toBe(false);
  });
});

describe('extractOrderIdFromUrl', () => {
  it('extracts the order id from a detail URL', () => {
    expect(
      extractOrderIdFromUrl('http://localhost:5173/production/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6')
    ).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
  });

  it('tolerates trailing query strings and hashes', () => {
    expect(
      extractOrderIdFromUrl('/production/orders/3fa85f64-5717-4562-b3fc-2c963f66afa6?tab=details#top')
    ).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
  });

  it('returns null outside the order-detail route', () => {
    expect(extractOrderIdFromUrl('http://localhost:5173/schedule')).toBeNull();
    expect(extractOrderIdFromUrl('http://localhost:5173/production/orders/not-a-guid')).toBeNull();
    expect(extractOrderIdFromUrl('')).toBeNull();
  });
});

describe('isDispatchBoardShape', () => {
  it('accepts the board shape the smoke asserts on', () => {
    expect(isDispatchBoardShape({ orders: [] })).toBe(true);
  });

  it('rejects contract drift instead of timing out obscurely', () => {
    expect(isDispatchBoardShape(null)).toBe(false);
    expect(isDispatchBoardShape({})).toBe(false);
    expect(isDispatchBoardShape({ orders: 'nope' })).toBe(false);
  });
});

describe('smokeCleanupPlan', () => {
  it('deletes the confirmation before either lot', () => {
    const steps = smokeCleanupPlan({
      confirmationId: 'conf-1',
      producedLotId: 'lot-prd',
      consumedLotId: 'lot-con'
    });

    expect(steps.map((s) => s.path)).toEqual([
      '/api/production-confirmations/conf-1',
      '/api/lots/lot-prd',
      '/api/lots/lot-con'
    ]);
  });

  it('skips ids a half-finished run never created', () => {
    expect(
      smokeCleanupPlan({ confirmationId: '', producedLotId: 'lot-prd', consumedLotId: '' }).map((s) => s.path)
    ).toEqual(['/api/lots/lot-prd']);
    expect(smokeCleanupPlan({ confirmationId: '', producedLotId: '', consumedLotId: '' })).toEqual([]);
  });

  it('never touches the audit trail (order / machine / recipe)', () => {
    const steps = smokeCleanupPlan({
      confirmationId: 'conf-1',
      producedLotId: 'lot-prd',
      consumedLotId: 'lot-con'
    });
    const blob = JSON.stringify(steps);

    expect(blob).not.toContain('production-orders');
    expect(blob).not.toContain('machines');
    expect(blob).not.toContain('recipes');
  });

  it('keeps repeat runs disjoint via unique per-run codes', () => {
    const first = smokeCodes('AB12CD');
    const second = smokeCodes('EF34GH');

    expect(first.orderCode).not.toBe(second.orderCode);
    expect(first.producedLotCode).not.toBe(second.producedLotCode);
    expect(first.consumedLotCode).not.toBe(second.consumedLotCode);
  });
});
