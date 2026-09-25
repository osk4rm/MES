import { describe, expect, it } from 'vitest';
import {
  buildConsumedLotLines,
  createConsumedLotRow,
  validateConfirmationLots,
  validateConsumedLotRow,
  validateProducedLot
} from './confirmationLots';

describe('validateProducedLot', () => {
  it('returns null when no consumed rows are present', () => {
    expect(validateProducedLot(null, 0)).toBeNull();
    expect(validateProducedLot('lot-produced', 0)).toBeNull();
  });

  it('requires a produced lot when consumed rows are present', () => {
    expect(validateProducedLot(null, 1)).toBe('producedLotRequired');
    expect(validateProducedLot('', 2)).toBe('producedLotRequired');
  });

  it('accepts a produced lot alongside consumed rows', () => {
    expect(validateProducedLot('lot-produced', 2)).toBeNull();
  });
});

describe('validateConsumedLotRow', () => {
  it('requires a lot selection', () => {
    expect(validateConsumedLotRow({ lotId: null, quantity: 5 }, 'lot-produced')).toBe('required');
    expect(validateConsumedLotRow({ lotId: '', quantity: 5 }, 'lot-produced')).toBe('required');
  });

  it('rejects null, zero and negative quantities', () => {
    expect(validateConsumedLotRow({ lotId: 'lot-a', quantity: null }, 'lot-produced')).toBe(
      'consumedQuantityPositive'
    );
    expect(validateConsumedLotRow({ lotId: 'lot-a', quantity: 0 }, 'lot-produced')).toBe(
      'consumedQuantityPositive'
    );
    expect(validateConsumedLotRow({ lotId: 'lot-a', quantity: -2.5 }, 'lot-produced')).toBe(
      'consumedQuantityPositive'
    );
  });

  it('rejects a self-link to the produced lot', () => {
    expect(validateConsumedLotRow({ lotId: 'lot-produced', quantity: 5 }, 'lot-produced')).toBe(
      'selfLinkNotAllowed'
    );
  });

  it('accepts a distinct lot with a positive quantity', () => {
    expect(validateConsumedLotRow({ lotId: 'lot-a', quantity: 5 }, 'lot-produced')).toBeNull();
    expect(validateConsumedLotRow({ lotId: 'lot-a', quantity: 0.001 }, 'lot-produced')).toBeNull();
  });

  it('checks quantity before self-link so missing quantities surface first', () => {
    expect(validateConsumedLotRow({ lotId: 'lot-produced', quantity: 0 }, 'lot-produced')).toBe(
      'consumedQuantityPositive'
    );
  });
});

describe('validateConfirmationLots', () => {
  it('passes a trace with a produced lot and two valid consumed rows', () => {
    const result = validateConfirmationLots('lot-produced', [
      { lotId: 'lot-a', quantity: 5 },
      { lotId: 'lot-b', quantity: 7 }
    ]);

    expect(result.producedLot).toBeNull();
    expect(result.rowErrors).toEqual([null, null]);
  });

  it('flags a missing produced lot and keeps per-row errors aligned', () => {
    const result = validateConfirmationLots(null, [
      { lotId: 'lot-a', quantity: 5 },
      { lotId: null, quantity: null }
    ]);

    expect(result.producedLot).toBe('producedLotRequired');
    expect(result.rowErrors).toEqual([null, 'required']);
  });

  it('passes when reporting without any trace reference', () => {
    const result = validateConfirmationLots(null, []);

    expect(result.producedLot).toBeNull();
    expect(result.rowErrors).toEqual([]);
  });
});

describe('consumed-row helpers', () => {
  it('creates an empty row for the editor', () => {
    expect(createConsumedLotRow()).toEqual({ lotId: null, quantity: null });
  });

  it('builds payload lines from validated rows', () => {
    expect(
      buildConsumedLotLines([
        { lotId: 'lot-a', quantity: 5 },
        { lotId: 'lot-b', quantity: 7 }
      ])
    ).toEqual([
      { lotId: 'lot-a', quantity: 5 },
      { lotId: 'lot-b', quantity: 7 }
    ]);
  });

  it('supports add/remove of editor rows', () => {
    const rows = [createConsumedLotRow(), createConsumedLotRow()];
    rows.splice(0, 1);

    expect(rows).toHaveLength(1);
    expect(validateConfirmationLots(null, rows).producedLot).toBe('producedLotRequired');
  });
});
