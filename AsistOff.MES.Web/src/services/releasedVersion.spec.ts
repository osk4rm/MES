import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import { releasedVersionNumber } from './releasedVersion';
import { RecipeVersionStatus, type RecipeResponse } from './recipeService';

function recipe(
  versions: RecipeResponse['versions'],
  currentVersionId: RecipeResponse['currentVersionId'] = null
): RecipeResponse {
  return {
    id: 'r1',
    code: 'R-1',
    name: 'Recipe 1',
    isActive: true,
    currentVersionId,
    versions
  };
}

function summary(id: string, versionNumber: number, status: RecipeVersionStatus) {
  return { id, versionNumber, status };
}

describe('releasedVersionNumber', () => {
  it('returns null when the browse payload carries no versions', () => {
    expect(releasedVersionNumber(recipe(undefined))).toBeNull();
    expect(releasedVersionNumber(recipe([]))).toBeNull();
  });

  it('returns null when no version is released (drafts only)', () => {
    const item = recipe([summary('v1', 1, RecipeVersionStatus.Draft)]);
    expect(releasedVersionNumber(item)).toBeNull();
  });

  it('ignores obsolete versions when looking for the badge', () => {
    const item = recipe([
      summary('v2', 2, RecipeVersionStatus.Released),
      summary('v5', 5, RecipeVersionStatus.Obsolete)
    ]);
    expect(releasedVersionNumber(item)).toBe(2);
  });

  it('prefers the current version when it is released', () => {
    const item = recipe(
      [
        summary('v1', 1, RecipeVersionStatus.Released),
        summary('v2', 2, RecipeVersionStatus.Released),
        summary('v3', 3, RecipeVersionStatus.Released)
      ],
      'v2'
    );
    expect(releasedVersionNumber(item)).toBe(2);
  });

  it('falls back to the highest released version when current is unset', () => {
    const item = recipe([
      summary('v1', 1, RecipeVersionStatus.Released),
      summary('v3', 3, RecipeVersionStatus.Released),
      summary('v4', 4, RecipeVersionStatus.Draft)
    ]);
    expect(releasedVersionNumber(item)).toBe(3);
  });

  it('falls back to the highest released version when current is a draft', () => {
    const item = recipe(
      [
        summary('v1', 1, RecipeVersionStatus.Released),
        summary('v2', 2, RecipeVersionStatus.Draft)
      ],
      'v2'
    );
    expect(releasedVersionNumber(item)).toBe(1);
  });

  it('falls back to the highest released version when current is unknown', () => {
    const item = recipe([summary('v1', 1, RecipeVersionStatus.Released)], 'missing-id');
    expect(releasedVersionNumber(item)).toBe(1);
  });
});

describe('released badge locales (issue #388 AC1)', () => {
  function recipeStrings(locale: 'pl' | 'en'): Record<string, unknown> {
    const root = i18n.global.getLocaleMessage(locale) as { recipes?: Record<string, unknown> };
    return root.recipes ?? {};
  }

  it.each(['pl', 'en'] as const)('defines badge and warning strings in %s', (locale) => {
    const strings = recipeStrings(locale);
    expect(typeof strings['releasedVersion']).toBe('string');
    expect(typeof strings['noReleasedVersion']).toBe('string');
    expect(typeof strings['releasedColumn']).toBe('string');
    expect((strings['releasedVersion'] as string).length).toBeGreaterThan(0);
    expect((strings['noReleasedVersion'] as string).length).toBeGreaterThan(0);
  });

  it.each(['pl', 'en'] as const)('badge string in %s interpolates the version number', (locale) => {
    const strings = recipeStrings(locale);
    // The row renders `$t('recipes.releasedVersion', { version: n })`.
    expect(strings['releasedVersion'] as string).toContain('{version}');
  });
});
