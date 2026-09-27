import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import AppDataState from '../components/ui/AppDataState.vue';
import AppTable, { type TableColumn } from '../components/ui/AppTable.vue';
import router from '../router';
import i18n from '../i18n';
import { sitemap } from '../sitemap';
import { findActiveNavTrail } from '../utils/navigation';

// Shell-consistency verification (issue #392, slice 1/3): navigation labels
// and order match in PL and EN, every shell view renders a consistent page
// header, every list view shows a loading indicator plus a dedicated empty
// state with a recovery action, and failed requests surface a consistent
// error state with retry instead of a blank view or a stuck spinner.
// Behavioural mounts prove the shared loading/empty/error branches (with
// retry); source scans pin every view to the shared wiring; i18n assertions
// prove PL+EN nav-key and title-key resolution with no raw keys.
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

// Raw Vue sources via Vite (no node:fs so vue-tsc stays happy without
// @types/node). Paths are relative to this file in src/views.
const viewSources = import.meta.glob<string>('./**/*.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});

// Public auth views render the standalone public layout by design (no shell
// chrome, no AppPageHeader) — see findings F-09 and the per-view checklist.
const publicLayoutExempt = new Set(['./LoginView.vue', './RegisterView.vue']);

// Views without fetch-driven list/state regions: the public layout views,
// the intentional ComingSoon stub, the guarded not-found view, and the
// static dashboard landing (F-01 tracks wiring it to real endpoints).
const stateExempt = new Set([
  './LoginView.vue',
  './RegisterView.vue',
  './ComingSoonView.vue',
  './NotFoundView.vue',
  './DashboardView.vue'
]);

function shellViewFiles(): string[] {
  return Object.keys(viewSources).filter((file) => !publicLayoutExempt.has(file));
}

function stateViewFiles(): string[] {
  return Object.keys(viewSources).filter((file) => !stateExempt.has(file));
}

describe('ux shell consistency (issue #392)', () => {
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

  it('renders a consistent page header on every shell view', () => {
    const files = shellViewFiles();
    expect(files.length).toBeGreaterThan(0);

    const offenders: string[] = [];
    for (const file of files) {
      const text = viewSources[file] ?? '';
      if (!text.includes('<AppPageHeader')) offenders.push(`${file} renders AppPageHeader`);
    }
    expect(offenders).toEqual([]);
  });

  it('binds loading, error and retry on every list/state view', () => {
    const files = stateViewFiles();
    expect(files.length).toBeGreaterThan(0);

    const offenders: string[] = [];
    for (const file of files) {
      const text = viewSources[file] ?? '';
      const hasLoading = text.includes(':loading=') || text.includes('<AppSpinner') || text.includes('<AppLoadingState');
      const hasError =
        text.includes(':error=') || text.includes('<AppErrorState') || text.includes('<AppDataState');
      const hasRetry = text.includes('@retry=');
      if (!hasLoading || !hasError || !hasRetry) {
        offenders.push(`${file} binds loading/error/retry`);
      }
      if (!text.includes('extractErrorMessage')) {
        offenders.push(`${file} toasts via extractErrorMessage`);
      }
      if (!text.includes('$t(')) {
        offenders.push(`${file} localises user-visible strings via $t`);
      }
    }
    expect(offenders).toEqual([]);
  });

  it('leaves no hardcoded English state copy in the shell views', () => {
    const denylist = ['>Loading<', '>Loading…<', '>No data<', '>Retry<', '>Something went wrong<'];
    const offenders: string[] = [];
    for (const file of shellViewFiles()) {
      const text = viewSources[file] ?? '';
      for (const marker of denylist) {
        if (text.includes(marker)) offenders.push(`${file} contains ${marker}`);
      }
    }
    expect(offenders).toEqual([]);
  });

  it('keeps navigation labels and order locale-independent with PL and EN coverage', () => {
    const keys: string[] = [];
    const flatten = (label: string): void => {
      keys.push(label);
    };
    for (const item of sitemap) {
      flatten(item.label);
      for (const child of item.children ?? []) flatten(child.label);
    }
    expect(keys.length).toBeGreaterThan(0);

    // One sitemap source drives both locales, so order cannot drift; every
    // label must resolve to a translated string in PL and EN.
    const offenders: string[] = [];
    for (const key of keys) {
      for (const locale of ['pl', 'en'] as const) {
        const value = translated(locale, key);
        if (value.trim() === '' || value === key) offenders.push(`${locale}:${key}`);
      }
    }
    expect(offenders).toEqual([]);
  });

  it('resolves every titled route to a translated title and a nav trail in PL and EN', () => {
    const routes = router.getRoutes().filter((r) => typeof r.meta['titleKey'] === 'string');
    expect(routes.length).toBeGreaterThan(0);

    const offenders: string[] = [];
    for (const route of routes) {
      const titleKey = route.meta['titleKey'] as string;
      for (const locale of ['pl', 'en'] as const) {
        const title = translated(locale, titleKey);
        if (title.trim() === '' || title === titleKey) offenders.push(`${locale}:${titleKey}`);
      }
      if (route.meta.public !== true) {
        const trail = findActiveNavTrail(sitemap, route.path);
        // Detail routes share their browse parent's key and highlight the
        // parent; redirects and the guarded not-found route carry no trail.
        const isRedirect = router.resolve(route.path).matched.length === 0 || route.redirect !== undefined;
        if (!isRedirect && route.path !== '/:pathMatch(.*)*' && trail.length === 0) {
          offenders.push(`${route.path} nav trail`);
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it('sends unknown routes to the guarded not-found view, not the dashboard', () => {
    const resolved = router.resolve('/no-such-view-xyz');

    expect(resolved.name).toBe('not-found');
    expect(resolved.meta.public).not.toBe(true);
  });
});
