import { RecipeVersionStatus, type RecipeResponse } from './recipeService';

/**
 * Order-readiness signal (issue #388, finding R-1): the recipe browse payload
 * already carries `versions` + `currentVersionId`, so the released-version
 * badge is display-only — no extra request.
 *
 * Returns the released version number to badge, or `null` when the recipe
 * has no released version (the row then shows the no-released warning).
 * The current version wins when it is released; otherwise the highest
 * released version number is shown.
 */
export function releasedVersionNumber(
  item: Pick<RecipeResponse, 'versions' | 'currentVersionId'>
): number | null {
  const released = (item.versions ?? []).filter((v) => v.status === RecipeVersionStatus.Released);
  if (released.length === 0) return null;
  if (item.currentVersionId) {
    const current = released.find((v) => v.id === item.currentVersionId);
    if (current) return current.versionNumber;
  }
  return Math.max(...released.map((v) => v.versionNumber));
}
