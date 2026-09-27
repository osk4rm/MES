import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import AppDataState from '../components/ui/AppDataState.vue';
import AppTable, { type TableColumn } from '../components/ui/AppTable.vue';
import router from '../router';
import i18n from '../i18n';
import { sitemap } from '../sitemap';
import { findActiveNavTrail } from '../utils/navigation';

// Slice-1 re-verification (issue #389): navigation/IA drift plus the
// standardized empty/loading/error states on Products, Production Orders,
// Dispatch board, Lots, Andon and the OEE dashboard. Behavioural mounts
// prove the shared loading/empty/error branches (with retry); source scans
// pin the six views to the shared wiring; i18n assertions prove PL+EN
// nav-key resolution with no raw keys.
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

function translated(locale: 'pl' | 'en', key: string): string {
  const value = resolveKey(localeDict(locale), key);
  return typeof value === 'string' ? value : key;
}

const columns: TableColumn[] = [
  { key: 'code', label: 'Code' },
  { key: 'name', label: 'Name' }
];

const viewSources = import.meta.glob<string>('./{configuration/ProductsView,production/ProductionOrdersView,production/ScheduleDispatchView,production/LotsView,production/AndonView,production/OeeDashboardView}.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});

const slice1Views: Array<{ file: string; path: string; titleKey: string }> = [
  { file: './configuration/ProductsView.vue', path: '/configuration/products', titleKey: 'nav.products' },
  { file: './production/ProductionOrdersView.vue', path: '/production/orders', titleKey: 'nav.productionOrders' },
  { file: './production/ScheduleDispatchView.vue', path: '/schedule/dispatch', titleKey: 'nav.dispatchBoard' },
  { file: './production/LotsView.vue', path: '/production/lots', titleKey: 'nav.productionLots' },
  { file: './production/AndonView.vue', path: '/production/andon', titleKey: 'nav.productionAndon' },
  { file: './production/OeeDashboardView.vue', path: '/reports/oee', titleKey: 'nav.oeeDashboard' }
];

describe('ux slice 1 re-verification (issue #389)', () => {
  it('proves the shared loading/empty/error branches with retry (AppDataState)', async () => {
    const loading = mount(AppDataState, {
      props: { loading: true, error: null, empty: true },
      global: { plugins: [i18n] }
    });
    expect(loading.find('.app-loading-state').exists()).toBe(true);

    const empty = mount(AppDataState, {
      props: { loading: false, error: null, empty: true, emptyTitle: 'Nothing here' },
      global: { plugins: [i18n] }
    });
    expect(empty.find('.app-empty-state').exists()).toBe(true);
    expect(empty.text()).toContain('Nothing here');

    const failed = mount(AppDataState, {
      props: { loading: false, error: 'Timed out', empty: true },
      global: { plugins: [i18n] }
    });
    const error = failed.find('.app-error-state');
    expect(error.exists()).toBe(true);
    expect(error.text()).toContain('Timed out');
    await error.find('button').trigger('click');
    expect(failed.emitted('retry')).toHaveLength(1);

    // Error wins over loading and content.
    const precedence = mount(AppDataState, {
      props: { loading: true, error: 'Boom', empty: false },
      slots: { default: '<p class="payload">rows</p>' },
      global: { plugins: [i18n] }
    });
    expect(precedence.find('.app-error-state').exists()).toBe(true);
    expect(precedence.find('.payload').exists()).toBe(false);
  });

  it('proves the table loading/empty/error branches with retry (AppTable)', async () => {
    const loading = mount(AppTable, {
      props: { items: [], columns, loading: true, loadingLabel: 'Hold on…' },
      global: { plugins: [i18n] }
    });
    expect(loading.text()).toContain('Hold on…');

    const empty = mount(AppTable, {
      props: { items: [], columns, loading: false, emptyLabel: 'Zero rows' },
      global: { plugins: [i18n] }
    });
    expect(empty.text()).toContain('Zero rows');

    const failed = mount(AppTable, {
      props: { items: [], columns, loading: false, error: 'Request failed' },
      global: { plugins: [i18n] }
    });
    const error = failed.find('.app-error-state');
    expect(error.exists()).toBe(true);
    await error.find('button').trigger('click');
    expect(failed.emitted('retry')).toHaveLength(1);
  });

  it('wires every slice-1 view to the shared page header and state contract', () => {
    expect(Object.keys(viewSources)).toHaveLength(6);

    const offenders: string[] = [];
    for (const view of slice1Views) {
      const text = viewSources[view.file];
      if (text === undefined) {
        offenders.push(`${view.file} missing from slice-1 scan`);
        continue;
      }
      if (!text.includes('<AppPageHeader')) offenders.push(`${view.file} renders AppPageHeader`);
      // Loading + error + retry: AppTable bindings on list views, AppDataState
      // bindings on board/dashboard views — either shape satisfies the slice.
      const hasTableWiring =
        text.includes(':loading="table.loading.value"') &&
        text.includes(':error="table.error.value"') &&
        text.includes('@retry="table.retry"');
      const hasRegionWiring = text.includes('<AppDataState') && text.includes(':error=') && text.includes('@retry=');
      if (!hasTableWiring && !hasRegionWiring) offenders.push(`${view.file} binds loading/error/retry`);
      if (!text.includes('extractErrorMessage')) offenders.push(`${view.file} toasts via extractErrorMessage`);
      if (!text.includes('$t(')) offenders.push(`${view.file} localises user-visible strings via $t`);
    }
    expect(offenders).toEqual([]);
  });

  it('leaves no hardcoded English state copy in the slice-1 views', () => {
    const denylist = ['>Loading<', '>Loading…<', '>No data<', '>Retry<', '>Something went wrong<'];
    const offenders: string[] = [];
    for (const view of slice1Views) {
      const text = viewSources[view.file] ?? '';
      for (const marker of denylist) {
        if (text.includes(marker)) offenders.push(`${view.file} contains ${marker}`);
      }
    }
    expect(offenders).toEqual([]);
  });

  it('resolves every slice-1 route to a translated title and a nav trail in PL and EN', () => {
    for (const view of slice1Views) {
      const resolved = router.resolve(view.path);
      expect(resolved.matched.length > 0, `${view.path} resolves`).toBe(true);

      const titleKey = resolved.meta['titleKey'];
      expect(titleKey, `${view.path} titleKey`).toBe(view.titleKey);

      const trail = findActiveNavTrail(sitemap, view.path);
      expect(trail.length > 0, `${view.path} nav trail`).toBe(true);

      for (const locale of ['pl', 'en'] as const) {
        const title = translated(locale, view.titleKey);
        expect(title.trim() !== '' && title !== view.titleKey, `${locale}:${view.titleKey}`).toBe(true);
        for (const item of trail) {
          const label = translated(locale, item.label);
          expect(label.trim() !== '' && label !== item.label, `${locale}:${item.label}`).toBe(true);
        }
      }
    }
  });

  it('sends unknown routes to the guarded not-found view, not the dashboard', () => {
    const resolved = router.resolve('/no-such-view-xyz');

    expect(resolved.name).toBe('not-found');
    expect(resolved.meta.public).not.toBe(true);
  });

  it('keeps every sitemap nav key translated in PL and EN', () => {
    const keys = new Set<string>();
    for (const item of sitemap) {
      keys.add(item.label);
      for (const child of item.children ?? []) keys.add(child.label);
    }
    expect(keys.size).toBeGreaterThan(0);

    const offenders: string[] = [];
    for (const key of keys) {
      for (const locale of ['pl', 'en'] as const) {
        const value = translated(locale, key);
        if (value.trim() === '' || value === key) offenders.push(`${locale}:${key}`);
      }
    }
    expect(offenders).toEqual([]);
  });
});
