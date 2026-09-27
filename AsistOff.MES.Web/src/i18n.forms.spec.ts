import { describe, expect, it } from 'vitest';
import i18n from './i18n';

// Guards the slice 2/3 i18n gap sweep (issue #325): every validation string
// rendered next to a form field — plus the shared-component aria strings the
// slice touches — must resolve in both EN and PL. These tests scan the Vue
// sources so a future form that references a missing key fails in CI.
//
// Out of scope by design: date/number/plural formatting. Views format via
// `Intl.NumberFormat(undefined, …)` / `toLocaleDateString()` (browser
// locale, aligned by construction) and the app uses no `$tc`/`$d`/`$n`
// plural or datetime helpers.
type Messages = Record<string, unknown>;

function localeDict(locale: string): Messages {
  return i18n.global.getLocaleMessage(locale) as Messages;
}

function section(dict: Messages, path: string): Messages {
  const parts = path.split('.');
  let node: unknown = dict;
  for (const part of parts) {
    node = (node as Messages)?.[part];
  }
  return (node ?? {}) as Messages;
}

function leafKeys(node: unknown, prefix: string): string[] {
  if (typeof node === 'string') return [prefix];
  if (node && typeof node === 'object') {
    return Object.entries(node as Messages).flatMap(([key, value]) =>
      leafKeys(value, prefix ? `${prefix}.${key}` : key)
    );
  }
  return [];
}

// Raw Vue sources via Vite (no node:fs so vue-tsc stays happy without
// @types/node). Paths are relative to this file in src/.
const viewSources = import.meta.glob<string>('./views/**/*.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});
const uiSources = import.meta.glob<string>('./components/ui/*.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});

function referencedValidationKeys(): string[] {
  const found = new Set<string>();
  for (const text of Object.values(viewSources)) {
    for (const match of text.matchAll(/validation\.([A-Za-z0-9_]+)/g)) {
      if (match[1]) found.add(match[1]);
    }
  }
  return [...found].sort();
}

describe('form i18n gaps (issue #325)', () => {
  it('keeps the validation namespace in parity between EN and PL', () => {
    const plKeys = leafKeys(section(localeDict('pl'), 'validation'), '').sort();
    const enKeys = leafKeys(section(localeDict('en'), 'validation'), '').sort();

    expect(plKeys.length).toBeGreaterThan(0);
    expect(enKeys).toEqual(plKeys);
  });

  it('keeps the shared pagination aria labels in parity between EN and PL', () => {
    const plKeys = leafKeys(section(localeDict('pl'), 'common.pagination'), '').sort();
    const enKeys = leafKeys(section(localeDict('en'), 'common.pagination'), '').sort();

    expect(enKeys).toEqual(plKeys);
    for (const key of ['firstPage', 'previousPage', 'nextPage', 'lastPage']) {
      expect(enKeys).toContain(key);
    }
  });

  it('resolves every validation key referenced by views in both locales', () => {
    const keys = referencedValidationKeys();
    expect(keys.length).toBeGreaterThan(0);

    const offenders: string[] = [];
    for (const key of keys) {
      for (const locale of ['pl', 'en']) {
        const value = section(localeDict(locale), 'validation')[key];
        if (typeof value !== 'string' || value.trim() === '') offenders.push(`${locale}:${key}`);
      }
    }
    expect(offenders).toEqual([]);
  });

  it('resolves the shared aria strings touched by the slice in both locales', () => {
    for (const key of ['common.clear', 'common.close', 'common.dismiss', 'common.notifications', 'common.breadcrumb', 'common.loading']) {
      for (const locale of ['pl', 'en']) {
        const value = section(localeDict(locale), key.split('.')[0])[key.split('.')[1] as string];
        expect(typeof value === 'string' && (value as string).trim() !== '', `${locale}:${key}`).toBe(true);
      }
    }
  });

  it('leaves no hardcoded aria-label text in shared UI components', () => {
    // Dynamic bindings (`:aria-label="…"`) carry caller-provided translated
    // labels, so only literal `aria-label="…"` attributes are offenders.
    const offenders: string[] = [];
    for (const [file, text] of Object.entries(uiSources)) {
      for (const match of text.matchAll(/(?<!:)aria-label="([^"$]+)"/g)) {
        offenders.push(`${file}: aria-label="${match[1]}"`);
      }
    }
    expect(offenders).toEqual([]);
  });
});
