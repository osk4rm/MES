import type { RouteLocationMatched } from 'vue-router';
import { findActiveNavTrail } from './utils/navigation';
import { sitemap, type NavItem } from './sitemap';

/**
 * Published navigation map (issue #337, slice 1/3).
 *
 * One row per sitemap route: the path, the i18n label key, and the
 * breadcrumb trail (also i18n keys, group-first). `AppShell` renders the
 * trail and the router sets `document.title` from the same source, so the
 * side nav, breadcrumbs and document titles can never drift apart. The
 * Operator panel route (issue #336) ships as a Production child so its
 * trail resolves to [nav.production, nav.operatorPanel].
 */
export interface NavigationMapRow {
  path: string;
  labelKey: string;
  trailKeys: string[];
}

/** Flattens the sitemap into map rows: group overviews plus every leaf. */
export function buildNavigationMap(items: NavItem[] = sitemap): NavigationMapRow[] {
  const rows: NavigationMapRow[] = [];
  const seen = new Set<string>();
  const pushRow = (row: NavigationMapRow): void => {
    // A group overview must never shadow a leaf on the same path (issue
    // #337 review): the leaf trail is canonical, so the first row wins and
    // later duplicates are skipped to keep the map 1:1 with paths.
    if (seen.has(row.path)) return;
    seen.add(row.path);
    rows.push(row);
  };
  for (const item of items) {
    const childPaths = new Set(
      (item.children ?? []).filter((child) => child.route !== undefined).map((child) => child.route as string)
    );
    if (item.children !== undefined && item.children.length > 0) {
      // Skip the group overview row when a child owns the same path, so the
      // leaf trail (e.g. [nav.schedule, nav.gantt]) stays canonical.
      if (item.route !== undefined && !childPaths.has(item.route)) {
        pushRow({ path: item.route, labelKey: item.label, trailKeys: [item.label] });
      }
      for (const child of item.children) {
        if (child.route !== undefined) {
          pushRow({ path: child.route, labelKey: child.label, trailKeys: [item.label, child.label] });
        }
      }
    } else if (item.route !== undefined) {
      pushRow({ path: item.route, labelKey: item.label, trailKeys: [item.label] });
    }
  }
  return rows;
}

/** Breadcrumb label keys for a path; `[]` when nothing matches. */
export function trailKeysForPath(path: string, items: NavItem[] = sitemap): string[] {
  return findActiveNavTrail(items, path).map((item) => item.label);
}

/** Deepest `meta.titleKey` wins, mirroring the permission helper in router. */
export function deepestTitleKey(matches: readonly RouteLocationMatched[]): string | undefined {
  for (let i = matches.length - 1; i >= 0; i -= 1) {
    const key = matches[i]?.meta?.['titleKey'];
    if (typeof key === 'string' && key.length > 0) return key;
  }
  return undefined;
}

/** `"<translated title> — AsistOff MES"`, falling back to the product name. */
export function documentTitleForTitleKey(
  translate: (key: string) => string,
  titleKey: string | undefined
): string {
  if (!titleKey) return 'AsistOff MES';
  const translated = translate(titleKey);
  if (!translated || translated === titleKey) return 'AsistOff MES';
  return `${translated} — AsistOff MES`;
}
