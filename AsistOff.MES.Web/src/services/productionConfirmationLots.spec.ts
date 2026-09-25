import { describe, expect, it } from 'vitest';
import { validateConfirmationLots } from './productionConfirmationLots';

describe('validateConfirmationLots', () => {
  it('accepts an empty selection so confirmations without lots keep existing behavior', () => {
    expect(validateConfirmationLots(null, [])).toBeNull();
    expect(validateConfirmationLots(undefined, undefined)).toBeNull();
    expect(validateConfirmationLots('produced-1', [])).toBeNull();
  });

  it('requires a produced lot when consumed rows are present', () => {
    expect(
      validateConfirmationLots(null, [{ lotId: 'consumed-1', quantity: 5 }])
    ).toBe('producedLotRequired');
    expect(
      validateConfirmationLots('', [{ lotId: 'consumed-1', quantity: 5 }])
    ).toBe('producedLotRequired');
  });

  it('requires a lot id on every consumed row', () => {
    expect(
      validateConfirmationLots('produced-1', [{ lotId: '', quantity: 5 }])
    ).toBe('consumedLotRequired');
  });

  it('rejects zero or negative consumed quantities', () => {
    expect(
      validateConfirmationLots('produced-1', [{ lotId: 'consumed-1', quantity: 0 }])
    ).toBe('consumedQuantityPositive');
    expect(
      validateConfirmationLots('produced-1', [{ lotId: 'consumed-1', quantity: -2.5 }])
    ).toBe('consumedQuantityPositive');
  });

  it('rejects same-lot self links', () => {
    expect(
      validateConfirmationLots('lot-1', [{ lotId: 'lot-1', quantity: 5 }])
    ).toBe('lotsMustDiffer');
  });

  it('accepts a produced lot with two consumed lots carrying positive quantities', () => {
    expect(
      validateConfirmationLots('produced-1', [
        { lotId: 'consumed-a', quantity: 5 },
        { lotId: 'consumed-b', quantity: 7 }
      ])
    ).toBeNull();
  });
});
