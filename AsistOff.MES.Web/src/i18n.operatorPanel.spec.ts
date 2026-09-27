import { describe, expect, it } from 'vitest';
import i18n from './i18n';

// Guards the operator panel slice (issue #336): every user-facing string
// on the panel resolves in both EN and PL, the view stays wired to the
// shared touch/density/severity presentation (no color-only Andon signals,
// 44px touch scope), and only App components drive interaction (no raw
// controls, no v-html, no TypeScript any). These tests scan the Vue source
// so a future panel change that drops the wiring or adds an untranslated
// string fails in CI.
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

const serviceSources = import.meta.glob<string>('./services/operatorQueueService.ts', {
  eager: true,
  query: '?raw',
  import: 'default'
});

const PANEL_VIEW = './views/production/OperatorPanelView.vue';

const PANEL_KEYS = [
  'title',
  'subtitle',
  'operatorCode',
  'operatorCodePlaceholder',
  'load',
  'enterCode',
  'shiftTitle',
  'shiftCode',
  'shiftName',
  'shiftWindow',
  'overnight',
  'offShift',
  'openSignals',
  'queueTitle',
  'queueEmpty',
  'nextUp',
  'claim',
  'priority',
  'product',
  'remaining',
  'workCenter',
  'reportScrap',
  'reportDowntime'
];

function referencedTKeys(): string[] {
  const text = viewSources[PANEL_VIEW] ?? '';
  const found = new Set<string>();
  for (const match of text.matchAll(/\$t\('([^']+)'\)/g)) {
    if (match[1]) found.add(match[1]);
  }
  return [...found].sort();
}

describe('operator panel i18n and touch wiring (issue #336)', () => {
  it('keeps the operatorPanel namespace in parity between EN and PL', () => {
    const plKeys = leafKeys(section(localeDict('pl'), 'operatorPanel'), '').sort();
    const enKeys = leafKeys(section(localeDict('en'), 'operatorPanel'), '').sort();

    expect(plKeys.length).toBeGreaterThan(0);
    expect(enKeys).toEqual(plKeys);
    expect(enKeys).toEqual([...PANEL_KEYS].sort());
  });

  it('resolves the operator panel nav entry in both locales', () => {
    for (const locale of ['pl', 'en']) {
      const value = resolveKey(localeDict(locale), 'nav.operatorPanel');
      expect(typeof value, `nav.operatorPanel (${locale})`).toBe('string');
      expect((value as string).length, `nav.operatorPanel (${locale})`).toBeGreaterThan(0);
    }
  });

  it('resolves every key referenced by the panel view in both locales', () => {
    const keys = referencedTKeys();

    expect(keys.length).toBeGreaterThan(0);
    for (const key of keys) {
      for (const locale of ['pl', 'en']) {
        const value = resolveKey(localeDict(locale), key);
        expect(typeof value, `${key} (${locale})`).toBe('string');
        expect((value as string).length, `${key} (${locale})`).toBeGreaterThan(0);
      }
    }
  });

  it('wires the panel to the shared touch scope and density option', () => {
    const text = viewSources[PANEL_VIEW] ?? '';

    expect(text).toContain('useShopfloorDensity');
    expect(text).toContain('viewClass');
    expect(text).toContain('shopfloor.density.label');
    expect(text).toContain('shopfloor.density.compact');
  });

  it('passes icons to status badges so signals never rely on color alone', () => {
    const text = viewSources[PANEL_VIEW] ?? '';

    expect(text).toContain(':icon=');
    expect(text).toContain('operatorQueueSignalMeta');
    expect(text).toContain('productionOrderStatusMeta');
  });

  it('drives interaction through App components only', () => {
    const text = viewSources[PANEL_VIEW] ?? '';

    expect(text).not.toContain('v-html');
    expect(text).not.toMatch(/<button[\s>]/);
    expect(text).not.toMatch(/<input[\s>]/);
    expect(text).not.toMatch(/<select[\s>]/);
    expect(text).not.toMatch(/<textarea[\s>]/);
    for (const component of ['AppButton', 'AppInput', 'AppNumberInput', 'AppTextarea', 'AppSelect', 'AppModal', 'AppBadge', 'AppCard']) {
      expect(text, component).toContain(component);
    }
  });

  it('uses no TypeScript any in the new panel service', () => {
    const values = Object.values(serviceSources);
    expect(values.length).toBe(1);
    expect(values[0]).not.toMatch(/:\s*any\b/);
  });
});
