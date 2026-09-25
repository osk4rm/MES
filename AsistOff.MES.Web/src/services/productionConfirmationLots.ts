import type { ConsumedLotInput } from './productionConfirmationService';

export type ConfirmationLotsError =
  | 'producedRequired'
  | 'lotRequired'
  | 'quantityPositive'
  | 'selfLink';

/**
 * Mirrors the backend lot rules on CreateProductionConfirmation so the
 * confirmation modal can reject invalid lot references inline before
 * posting: consumed rows require a produced lot, every row needs a lot id
 * with a quantity greater than zero, and same-lot self links are rejected.
 * Returns the first violated rule, or null when the selection is valid.
 */
export function validateConfirmationLots(
  producedLotId: string | null | undefined,
  consumedLots: ConsumedLotInput[] | null | undefined
): ConfirmationLotsError | null {
  const rows = consumedLots ?? [];
  if (rows.length === 0) return null;
  if (!producedLotId) return 'producedRequired';
  for (const row of rows) {
    if (!row.lotId) return 'lotRequired';
    if (!(row.quantity > 0)) return 'quantityPositive';
    if (row.lotId === producedLotId) return 'selfLink';
  }
  return null;
}
