import { describe, expect, it } from 'vitest';
import router from './router';
import { sitemap, type NavItem } from './sitemap';
import { findActiveNavTrail } from './utils/navigation';

function sitemapRoutes(items: NavItem[]): string[] {
  const routes: string[] = [];
  for (const item of items) {
    if (item.route) routes.push(item.route);
    for (const child of item.children ?? []) {
      if (child.route) routes.push(child.route);
    }
  }
  return routes;
}

// Guards the navigation frame (issue #314): sidebar and router stay in
// sync, unknown routes land on the guarded not-found view, and every
// titled route — including detail pages via their browse parent — maps to
// a highlighted nav entry.
describe('sitemap/router alignment', () => {
  it('resolves every sitemap route to a real route record', () => {
    for (const route of sitemapRoutes(sitemap)) {
      const resolved = router.resolve(route);
      expect(resolved.matched.length > 0, `sitemap route ${route} resolves`).toBe(true);
    }
  });

  it('covers every titled route with a nav entry (detail pages via parent)', () => {
    const known = new Set(sitemapRoutes(sitemap));
    for (const record of router.getRoutes()) {
      const titleKey = record.meta?.['titleKey'];
      if (typeof titleKey !== 'string') continue;
      if (record.name === 'login' || record.name === 'register' || record.name === 'not-found') continue;
      if (record.path.includes(':')) {
        // Detail pages carry the parent titleKey and highlight the parent.
        const probe = record.path.replace(/:[^/]+/g, 'probe-id');
        expect(findActiveNavTrail(sitemap, probe).length > 0, `${record.path} highlights a nav entry`).toBe(true);
        continue;
      }
      expect(known.has(record.path), `${record.path} has a nav entry`).toBe(true);
    }
  });

  it('sends unknown routes to the guarded not-found view, not the dashboard', () => {
    const resolved = router.resolve('/no-such-view-xyz');

    expect(resolved.name).toBe('not-found');
  });

  it('keeps the not-found route behind the authenticated shell', () => {
    const resolved = router.resolve('/no-such-view-xyz');
    const paths = resolved.matched.map((m) => String(m.path));

    // Rendered inside the AppShell layout record (path '/'), never public.
    expect(paths).toContain('/');
    expect(resolved.meta.public).not.toBe(true);
  });
});
