import { describe, expect, it } from 'vitest';
import type { RouteLocationMatched } from 'vue-router';
import router from './router';
import i18n from './i18n';
import {
  buildNavigationMap,
  deepestTitleKey,
  documentTitleForTitleKey,
  trailKeysForPath
} from './navigationMap';
import { sitemap } from './sitemap';

// Navigation + information architecture audit (issue #337, slice 1/3):
// the published map matches the shipped side nav, every route resolves
// EN+PL labels plus a breadcrumb trail with no raw keys, document titles
// follow the active locale, and the touched auth strings are translated.
// Raw Vue/TS sources are scanned via Vite (no node:fs so vue-tsc stays
// happy without @types/node). Paths are relative to this file in src/.
type Messages = Record<string, unknown>;

function localeDict(locale: string): Messages {
  return i18n.global.getLocaleMessage(locale) as Messages;
}

function resolveKey(dict: Messages, path: string): unknown {
  let node: unknown = dict;
  for (const part of path.split('.')) {
    node = (node as Messages)?.[part];
  }
  return node;
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

function translateFor(locale: 'pl' | 'en'): (key: string) => string {
  return (key: string) => {
    const value = resolveKey(localeDict(locale), key);
    return typeof value === 'string' ? value : key;
  };
}

const helperSources = import.meta.glob<string>('./navigationMap.ts', {
  eager: true,
  query: '?raw',
  import: 'default'
});
const shellSources = import.meta.glob<string>('./components/layout/AppShell.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});
const routerSources = import.meta.glob<string>('./router.ts', {
  eager: true,
  query: '?raw',
  import: 'default'
});
const authViewSources = import.meta.glob<string>('./views/{LoginView,RegisterView}.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});

describe('navigation map (issue #337)', () => {
  it('publishes one row per sitemap route with no dead or duplicate paths', () => {
    const rows = buildNavigationMap();

    expect(rows.length).toBeGreaterThan(0);
    const paths = rows.map((row) => row.path);
    expect(new Set(paths).size).toBe(paths.length);
    for (const row of rows) {
      expect(row.labelKey.startsWith('nav.'), `${row.path} label key`).toBe(true);
      expect(row.trailKeys.length).toBeGreaterThan(0);
      expect(row.trailKeys[row.trailKeys.length - 1]).toBe(row.labelKey);
    }
  });

  it('keeps top-level groups ordered across production, schedule, reports, configuration and settings', () => {
    expect(sitemap.map((item) => item.label)).toEqual([
      'nav.dashboard',
      'nav.production',
      'nav.schedule',
      'nav.reports',
      'nav.configuration',
      'nav.settings'
    ]);
  });

  it('orders production children by workflow: plan, track, monitor', () => {
    const production = sitemap.find((item) => item.label === 'nav.production');
    expect(production?.children?.map((child) => child.label)).toEqual([
      'nav.productionOrders',
      'nav.productionRecipes',
      'nav.productionKanban',
      'nav.productionLots',
      'nav.spcCharacteristics',
      'nav.productionScrap',
      'nav.productionDowntime',
      'nav.productionAndon',
      'nav.productionTelemetry',
      'nav.productionOpcUaConnections',
      'nav.productionTelemetryDashboard'
    ]);
  });

  it('groups the Gantt view and the dispatch board under Schedule', () => {
    const schedule = sitemap.find((item) => item.label === 'nav.schedule');
    expect(schedule?.children?.map((child) => child.label)).toEqual(['nav.gantt', 'nav.dispatchBoard']);
    expect(trailKeysForPath('/schedule/dispatch')).toEqual(['nav.schedule', 'nav.dispatchBoard']);
  });

  it('resolves detail pages to their browse parent trail', () => {
    expect(trailKeysForPath('/production/orders/123')).toEqual(['nav.production', 'nav.productionOrders']);
    expect(trailKeysForPath('/production/recipes/abc')).toEqual(['nav.production', 'nav.productionRecipes']);
  });

  it('returns an empty trail for unknown paths', () => {
    expect(trailKeysForPath('/no-such-view-xyz')).toEqual([]);
  });

  it('resolves every map label and trail key in EN and PL with no raw keys', () => {
    const keys = new Set<string>();
    for (const row of buildNavigationMap()) {
      keys.add(row.labelKey);
      for (const key of row.trailKeys) keys.add(key);
    }
    expect(keys.size).toBeGreaterThan(0);

    const offenders: string[] = [];
    for (const key of keys) {
      for (const locale of ['pl', 'en'] as const) {
        const value = resolveKey(localeDict(locale), key);
        if (typeof value !== 'string' || value.trim() === '' || value === key) {
          offenders.push(`${locale}:${key}`);
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it('keeps the nav namespace in parity between EN and PL', () => {
    const plKeys = leafKeys(resolveKey(localeDict('pl'), 'nav'), '').sort();
    const enKeys = leafKeys(resolveKey(localeDict('en'), 'nav'), '').sort();

    expect(plKeys.length).toBeGreaterThan(0);
    expect(enKeys).toEqual(plKeys);
    expect(enKeys).toContain('gantt');
    expect(enKeys).toContain('dispatchBoard');
  });

  it('gives every titled route a translated document title and a breadcrumb trail', () => {
    for (const record of router.getRoutes()) {
      const titleKey = record.meta?.['titleKey'];
      if (typeof titleKey !== 'string') continue;
      for (const locale of ['pl', 'en'] as const) {
        const title = documentTitleForTitleKey(translateFor(locale), titleKey);
        expect(title.endsWith(' — AsistOff MES'), `${record.path} title (${locale})`).toBe(true);
        expect(title, `${record.path} raw key (${locale})`).not.toBe('AsistOff MES');
      }
      // Public auth routes render outside the shell (no side nav), and the
      // catch-all renders the shell fallback crumb — both covered by title.
      if (record.name === 'login' || record.name === 'register' || record.name === 'not-found') continue;
      expect(trailKeysForPath(record.path).length > 0, `${record.path} trail`).toBe(true);
    }
  });

  it('formats document titles with the product suffix and falls back safely', () => {
    const t = translateFor('en');

    expect(documentTitleForTitleKey(t, 'nav.dashboard')).toBe('Dashboard — AsistOff MES');
    expect(documentTitleForTitleKey(t, undefined)).toBe('AsistOff MES');
    expect(documentTitleForTitleKey(t, '')).toBe('AsistOff MES');
    expect(documentTitleForTitleKey(t, 'nav.noSuchKey')).toBe('AsistOff MES');
  });

  it('resolves the deepest titleKey from matched records', () => {
    const match = (titleKey?: string): RouteLocationMatched =>
      ({ meta: titleKey ? { titleKey } : {} }) as unknown as RouteLocationMatched;

    expect(deepestTitleKey([])).toBeUndefined();
    expect(deepestTitleKey([match()])).toBeUndefined();
    expect(deepestTitleKey([match('nav.production')])).toBe('nav.production');
    expect(deepestTitleKey([match('nav.production'), match('nav.productionOrders')])).toBe('nav.productionOrders');
  });

  it('notes the operator panel as planned, not shipped (issue #336)', () => {
    const operatorRoutes = router.getRoutes().filter((record) => record.path.startsWith('/operator'));

    expect(operatorRoutes).toEqual([]);
  });

  it('keeps the touched auth and schedule strings translated in both locales', () => {
    for (const key of [
      'auth.signInTitle',
      'auth.signUpTitle',
      'auth.loginSideKicker',
      'auth.loginSideTitle',
      'auth.loginSideText',
      'auth.registerSideKicker',
      'auth.registerSideTitle',
      'auth.registerSideText',
      'scheduleDispatch.title',
      'nav.gantt',
      'nav.dispatchBoard'
    ]) {
      for (const locale of ['pl', 'en'] as const) {
        const value = resolveKey(localeDict(locale), key);
        expect(typeof value === 'string' && (value as string).trim() !== '', `${locale}:${key}`).toBe(true);
      }
    }
    // The dispatch board dropped the shipping-flavoured PL label in favour
    // of the dispatch (dyspozytorska) wording used by the view subtitle.
    expect(resolveKey(localeDict('pl'), 'nav.dispatchBoard')).toBe('Tablica dyspozytorska');
    expect(resolveKey(localeDict('pl'), 'scheduleDispatch.title')).toBe('Tablica dyspozytorska');
  });

  it('leaves no hardcoded English marketing copy on the auth views', () => {
    expect(Object.keys(authViewSources).length).toBe(2);
    for (const [file, text] of Object.entries(authViewSources)) {
      expect(text, `${file} hardcoded kicker`).not.toContain('>Multi-tenant<');
      expect(text, `${file} hardcoded title`).not.toContain('Enterprise-grade shop-floor control.');
      expect(text, `${file} hardcoded title`).not.toContain('Isolated workspace for your organization.');
      expect(text, `${file} hardcoded text`).not.toContain('one tenant-isolated workspace');
      expect(text, `${file} hardcoded text`).not.toContain('strictly-isolated dataset');
    }
    expect(authViewSources['./views/LoginView.vue']).toContain('auth.loginSideTitle');
    expect(authViewSources['./views/RegisterView.vue']).toContain('auth.registerSideTitle');
  });

  it('wires shell breadcrumbs and document titles without TypeScript any', () => {
    const helper = Object.values(helperSources);
    expect(helper.length).toBe(1);
    expect(helper[0]).not.toMatch(/:\s*any\b/);

    const shell = shellSources['./components/layout/AppShell.vue'] ?? '';
    expect(shell).toContain('AppBreadcrumbs');
    expect(shell).toContain(':items="crumbs"');
    expect(shell).toContain('findActiveNavTrail');

    const routerText = routerSources['./router.ts'] ?? '';
    expect(routerText).toContain('afterEach');
    expect(routerText).toContain('document.title');
  });
});
