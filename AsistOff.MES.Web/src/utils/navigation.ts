import type { NavItem } from '../sitemap';

/**
 * Navigation active-state helpers (issue #314).
 *
 * The sidebar must highlight the entry for the current page, including
 * detail pages (`/production/orders/:id` highlights `/production/orders`)
 * and the `/settings` overview stub (highlights the Settings group).
 * Matching is segment-aware on purpose: a naive `startsWith` would mark
 * `/production/telemetry` active while on `/production/telemetry-dashboard`.
 */

/** Strips query/hash and trailing slashes (keeps the root `/`). */
export function normalizeNavPath(path: string): string {
  const withoutSuffix = path.split('?')[0]?.split('#')[0] ?? '/';
  if (withoutSuffix.length > 1) return withoutSuffix.replace(/\/+$/, '');
  return withoutSuffix || '/';
}

/**
 * True when `currentPath` is exactly `itemRoute` or lives underneath it
 * on a segment boundary (`/a/b` is under `/a` but not under `/a-b`).
 */
export function isNavRouteActive(currentPath: string, itemRoute: string): boolean {
  const current = normalizeNavPath(currentPath);
  const item = normalizeNavPath(itemRoute);
  if (current === item) return true;
  return current.startsWith(item.endsWith('/') ? item : `${item}/`);
}

/** A group is active when its own overview route or any child is active. */
export function isNavGroupActive(currentPath: string, group: NavItem): boolean {
  if (group.route && isNavRouteActive(currentPath, group.route)) return true;
  return (group.children ?? []).some(
    (child) => child.route !== undefined && isNavRouteActive(currentPath, child.route)
  );
}

/**
 * The visible trail for the current path: `[group, leaf]` for a child
 * entry, `[leaf]` for a top-level entry, `[]` when nothing matches.
 * The deepest (longest) matching route wins, so detail pages resolve to
 * their browse parent.
 */
export function findActiveNavTrail(items: NavItem[], currentPath: string): NavItem[] {
  let best: NavItem[] = [];
  let bestLength = -1;

  const consider = (trail: NavItem[], route: string): void => {
    if (!isNavRouteActive(currentPath, route)) return;
    if (route.length > bestLength) {
      bestLength = route.length;
      best = trail;
    }
  };

  for (const item of items) {
    // A group overview route (e.g. `/settings`) with no child match still
    // highlights the group itself.
    if (item.route) consider([item], item.route);
    for (const child of item.children ?? []) {
      if (child.route) consider([item, child], child.route);
    }
  }

  return best;
}
