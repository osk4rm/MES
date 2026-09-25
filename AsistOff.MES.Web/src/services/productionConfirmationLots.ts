import type { ConsumedLotInput } from './productionConfirmationService';

/**
 * Suffix of the `productionConfirmations.*` i18n key describing the first
 * violated lot rule, mirroring the backend rules on
 * CreateProductionConfirmation so the confirmation modal can reject invalid
 * lot references inline before posting: consumed rows require a produced
 * lot, every row needs a lot id with a quantity greater than zero, and
 * same-lot self links are rejected. Returns null when the selection is
 * valid (including the no-lots case, which keeps existing behavior and
 * posts no genealogy edges).
 */
export type ConfirmationLotsError =
  | 'producedLotRequired'
  | 'consumedLotRequired'
  | 'consumedQuantityPositive'
  | 'lotsMustDiffer';

export function validateConfirmationLots(
  producedLotId: string | null | undefined,
  consumedLots: ConsumedLotInput[] | null | undefined
): ConfirmationLotsError | null {
  const rows = consumedLots ?? [];
  if (rows.length === 0) return null;
  if (!producedLotId) return 'producedLotRequired';
  for (const row of rows) {
    if (!row.lotId) return 'consumedLotRequired';
    if (!(row.quantity > 0)) return 'consumedQuantityPositive';
    if (row.lotId === producedLotId) return 'lotsMustDiffer';
  }
  return null;
}
