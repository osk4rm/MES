import { describe, expect, it } from 'vitest';
import i18n from './i18n';

// Guards the slice 3/3 shopfloor ergonomics work (issue #331): every new
// user-facing string on the operator-facing views resolves in both EN and
// PL, and each touched view is wired to the shared touch/density/severity
// presentation (no color-only Andon signaling, 44px touch scope, density
// option). These tests scan the Vue sources so a future shopfloor change
// that drops the wiring or adds an untranslated string fails in CI.
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

function resolveKey(dict: Messages, path: string): unknown {
  let node: unknown = dict;
  for (const part of path.split('.')) {
    node = (node as Messages)?.[part];
  }
  return node;
}

// Raw Vue sources via Vite (no node:fs so vue-tsc stays happy without
// @types/node). Paths are relative to this file in src/.
const viewSources = import.meta.glob<string>('./views/**/*.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});

const helperSources = import.meta.glob<string>('./composables/useShopfloorDisplay.ts', {
  eager: true,
  query: '?raw',
  import: 'default'
});

// Operator-facing views in scope for slice 3/3: Confirmation entry,
// dispatch board, Andon signals, lot lookup, downtime and scrap reporting.
const TOUCHED_VIEWS = [
  './views/production/AndonView.vue',
  './views/production/ScheduleDispatchView.vue',
  './views/production/ProductionOrderDetailView.vue',
  './views/production/LotsView.vue',
  './views/production/DowntimeView.vue',
  './views/production/ScrapView.vue'
];

const BADGED_VIEWS = TOUCHED_VIEWS.filter((v) => v !== './views/production/ScrapView.vue');

function referencedShopfloorKeys(): string[] {
  const found = new Set<string>();
  for (const [path, text] of Object.entries(viewSources)) {
    if (!TOUCHED_VIEWS.includes(path)) continue;
    for (const match of text.matchAll(/shopfloor\.([A-Za-z0-9_.]+)/g)) {
      if (match[1]) found.add(match[1]);
    }
  }
  return [...found].sort();
}

describe('shopfloor i18n and touch wiring (issue #331)', () => {
  it('keeps the shopfloor namespace in parity between EN and PL', () => {
    const plKeys = leafKeys(section(localeDict('pl'), 'shopfloor'), '').sort();
    const enKeys = leafKeys(section(localeDict('en'), 'shopfloor'), '').sort();

    expect(plKeys.length).toBeGreaterThan(0);
    expect(enKeys).toEqual(plKeys);
    expect(enKeys).toEqual(['density.comfortable', 'density.compact', 'density.label', 'density.toggleHint']);
  });

  it('resolves every shopfloor key referenced by touched views in both locales', () => {
    const keys = referencedShopfloorKeys();

    expect(keys.length).toBeGreaterThan(0);
    for (const key of keys) {
      for (const locale of ['pl', 'en']) {
        const value = resolveKey(localeDict(locale), `shopfloor.${key}`);
        expect(typeof value).toBe('string');
        expect((value as string).length).toBeGreaterThan(0);
      }
    }
  });

  it('resolves the Andon/downtime/lot/dispatch status labels in both locales', () => {
    const keys = [
      'andon.statuses.1',
      'andon.statuses.2',
      'andon.statuses.3',
      'andon.categories.1',
      'downtime.status.open',
      'downtime.status.closed',
      'lots.statuses.1',
      'scheduleDispatch.overdue',
      'scheduleDispatch.onTime',
      'scheduleDispatch.uncovered'
    ];
    for (const key of keys) {
      for (const locale of ['pl', 'en']) {
        const value = resolveKey(localeDict(locale), key);
        expect(typeof value, `${key} (${locale})`).toBe('string');
        expect((value as string).length, `${key} (${locale})`).toBeGreaterThan(0);
      }
    }
  });

  it('wires every touched view to the shared touch scope and density option', () => {
    for (const path of TOUCHED_VIEWS) {
      const text = viewSources[path];
      expect(text, path).toBeDefined();
      expect(text, `${path} density state`).toContain('useShopfloorDensity');
      expect(text, `${path} touch scope`).toContain('viewClass');
      expect(text, `${path} density toggle`).toContain('shopfloor.density.label');
      expect(text, `${path} density options`).toContain('shopfloor.density.compact');
    }
  });

  it('passes icons to status badges so signals never rely on color alone', () => {
    for (const path of BADGED_VIEWS) {
      const text = viewSources[path];
      expect(text, `${path} badge icon`).toContain(':icon=');
    }
    const andon = viewSources['./views/production/AndonView.vue'] ?? '';
    expect(andon).toContain('andonSeverityMeta');
    expect(andon).toContain('board-legend');
  });

  it('adds no hardcoded density strings to touched views', () => {
    for (const path of TOUCHED_VIEWS) {
      const text = viewSources[path] ?? '';
      expect(text, `${path} hardcoded PL`).not.toContain('Gęstość');
      expect(text, `${path} hardcoded PL`).not.toContain('Dotykowa');
      expect(text, `${path} hardcoded PL`).not.toContain('Zwarta');
    }
  });

  it('uses no TypeScript any in the new presentation helper', () => {
    const values = Object.values(helperSources);
    expect(values.length).toBe(1);
    expect(values[0]).not.toMatch(/:\s*any\b/);
  });
});
