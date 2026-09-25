import type { ConsumedLotLine } from './productionConfirmationService';

/** One editable row of the consumed-lots editor in the confirmation modal. */
export interface ConsumedLotFormRow {
  lotId: string | null;
  quantity: number | null;
}

/** Error codes for a consumed-lot row; the caller maps them to i18n strings. */
export type ConsumedLotRowErrorCode = 'required' | 'consumedQuantityPositive' | 'selfLinkNotAllowed';

export function createConsumedLotRow(): ConsumedLotFormRow {
  return { lotId: null, quantity: null };
}

/**
 * Mirrors the backend rule: consumed rows require a produced lot.
 * Returns the i18n key suffix when invalid, otherwise null.
 */
export function validateProducedLot(
  producedLotId: string | null,
  consumedCount: number
): 'producedLotRequired' | null {
  if (consumedCount > 0 && !producedLotId) {
    return 'producedLotRequired';
  }
  return null;
}

/**
 * Mirrors the backend 400 rules for a single consumed row:
 * lot required → quantity > 0 → must differ from the produced lot.
 */
export function validateConsumedLotRow(
  row: ConsumedLotFormRow,
  producedLotId: string | null
): ConsumedLotRowErrorCode | null {
  if (!row.lotId) {
    return 'required';
  }
  if (row.quantity === null || row.quantity === undefined || row.quantity <= 0) {
    return 'consumedQuantityPositive';
  }
  if (producedLotId && row.lotId === producedLotId) {
    return 'selfLinkNotAllowed';
  }
  return null;
}

/** Validates the whole editor; rowErrors aligns 1:1 with the input rows. */
export function validateConfirmationLots(
  producedLotId: string | null,
  rows: ConsumedLotFormRow[]
): { producedLot: 'producedLotRequired' | null; rowErrors: (ConsumedLotRowErrorCode | null)[] } {
  return {
    producedLot: validateProducedLot(producedLotId, rows.length),
    rowErrors: rows.map((row) => validateConsumedLotRow(row, producedLotId))
  };
}

/** Builds the service payload lines; callers must validate first. */
export function buildConsumedLotLines(rows: ConsumedLotFormRow[]): ConsumedLotLine[] {
  return rows.map((r) => ({ lotId: r.lotId as string, quantity: r.quantity as number }));
}
