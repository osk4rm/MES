import { describe, expect, it } from 'vitest';
import {
  findActiveNavTrail,
  isNavGroupActive,
  isNavRouteActive,
  normalizeNavPath
} from './navigation';
import type { NavItem } from '../sitemap';

// Unit tests for the sidebar active-state helpers (issue #314): detail
// pages highlight their browse parent, sibling prefixes never cross-match,
// and every resolvable path maps to a visible nav trail.
describe('normalizeNavPath', () => {
  it('keeps the root slash intact', () => {
    expect(normalizeNavPath('/')).toBe('/');
  });

  it('strips trailing slashes, query strings and hashes', () => {
    expect(normalizeNavPath('/production/orders/')).toBe('/production/orders');
    expect(normalizeNavPath('/production/orders?page=2')).toBe('/production/orders');
    expect(normalizeNavPath('/schedule#today')).toBe('/schedule');
  });
});

describe('isNavRouteActive', () => {
  it('matches the exact route', () => {
    expect(isNavRouteActive('/dashboard', '/dashboard')).toBe(true);
  });

  it('matches nested detail pages under their browse parent', () => {
    expect(isNavRouteActive('/production/orders/123', '/production/orders')).toBe(true);
    expect(isNavRouteActive('/production/recipes/abc', '/production/recipes')).toBe(true);
  });

  it('does not cross-match sibling prefixes', () => {
    expect(isNavRouteActive('/production/telemetry-dashboard', '/production/telemetry')).toBe(false);
    expect(isNavRouteActive('/production/telemetry', '/production/telemetry-dashboard')).toBe(false);
  });

  it('does not match unrelated routes', () => {
    expect(isNavRouteActive('/reports/oee', '/production/orders')).toBe(false);
    expect(isNavRouteActive('/dashboard', '/schedule')).toBe(false);
  });
});

const items: NavItem[] = [
  { label: 'nav.dashboard', icon: 'pi pi-chart-pie', route: '/dashboard' },
  {
    label: 'nav.production',
    icon: 'pi pi-cog',
    route: '/production',
    children: [
      { label: 'nav.productionOrders', icon: 'pi pi-list', route: '/production/orders' },
      { label: 'nav.productionTelemetry', icon: 'pi pi-wave-pulse', route: '/production/telemetry' },
      { label: 'nav.productionTelemetryDashboard', icon: 'pi pi-chart-line', route: '/production/telemetry-dashboard' }
    ]
  },
  {
    label: 'nav.settings',
    icon: 'pi pi-cog',
    route: '/settings',
    children: [{ label: 'nav.roles', icon: 'pi pi-lock', route: '/settings/roles' }]
  }
];

describe('isNavGroupActive', () => {
  it('is active when a child is active', () => {
    const group = items[1] as NavItem;
    expect(isNavGroupActive('/production/orders', group)).toBe(true);
    expect(isNavGroupActive('/dashboard', group)).toBe(false);
  });

  it('is active on the group overview route with no child match', () => {
    const group = items[2] as NavItem;
    expect(isNavGroupActive('/settings', group)).toBe(true);
    expect(isNavGroupActive('/settings/roles', group)).toBe(true);
  });
});

describe('findActiveNavTrail', () => {
  it('resolves a top-level entry to a single-item trail', () => {
    const trail = findActiveNavTrail(items, '/dashboard');

    expect(trail.map((i) => i.label)).toEqual(['nav.dashboard']);
  });

  it('resolves a child entry to its group trail', () => {
    const trail = findActiveNavTrail(items, '/production/orders');

    expect(trail.map((i) => i.label)).toEqual(['nav.production', 'nav.productionOrders']);
  });

  it('resolves a detail page to its browse parent trail', () => {
    const trail = findActiveNavTrail(items, '/production/orders/123');

    expect(trail.map((i) => i.label)).toEqual(['nav.production', 'nav.productionOrders']);
  });

  it('prefers the longest matching sibling route', () => {
    const trail = findActiveNavTrail(items, '/production/telemetry-dashboard');

    expect(trail.map((i) => i.label)).toEqual(['nav.production', 'nav.productionTelemetryDashboard']);
  });

  it('resolves the settings overview stub to its group', () => {
    const trail = findActiveNavTrail(items, '/settings');

    expect(trail.map((i) => i.label)).toEqual(['nav.settings']);
  });

  it('returns an empty trail for unknown paths', () => {
    expect(findActiveNavTrail(items, '/nope/nothing')).toEqual([]);
  });
});
