import { describe, expect, it } from 'vitest';

// Guards the shared list error wiring (issue #314): every browse view must
// surface list-fetch failures with a working retry instead of a stuck loader
// or a silent empty table. These tests scan the Vue sources so a future view
// (or a refactor that drops a binding) fails in CI instead of in the browser.
//
// Standard list views own a `useCrudPage` table and bind its error/retry to
// the shared AppTable error row (`:error="table.error.value"` +
// `@retry="table.retry"`). Dashboard-style views render derived rows from a
// page-level fetch instead; those tables are covered by the page-level
// `<AppErrorState ... @retry="...">` banner.
//
// Raw Vue sources via Vite (no node:fs so vue-tsc stays happy without
// @types/node). Paths are relative to this file in src/views.
const viewSources = import.meta.glob<string>('./**/*.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});

function viewsUsing(pattern: RegExp): Array<[string, string]> {
  return Object.entries(viewSources).filter(([, text]) => pattern.test(text));
}

describe('list error/retry wiring (issue #314)', () => {
  it('scans a non-empty set of views', () => {
    expect(Object.keys(viewSources).length).toBeGreaterThan(0);
    expect(viewsUsing(/<AppTable/).length).toBeGreaterThan(0);
    expect(viewsUsing(/useCrudPage/).length).toBeGreaterThan(0);
  });

  it('binds every useCrudPage table to the shared error row with retry', () => {
    const offenders: string[] = [];
    for (const [file, text] of viewsUsing(/useCrudPage/)) {
      if (!text.includes(':error="table.error.value"') || !text.includes('@retry="table.retry"')) {
        offenders.push(file);
      }
    }
    expect(offenders).toEqual([]);
  });

  it('offers a retry on every view that renders an AppTable', () => {
    const offenders: string[] = [];
    for (const [file, text] of viewsUsing(/<AppTable/)) {
      if (!text.includes('@retry=')) {
        offenders.push(file);
      }
    }
    expect(offenders).toEqual([]);
  });

  it('surfaces errors on every view that renders an AppTable', () => {
    const offenders: string[] = [];
    for (const [file, text] of viewsUsing(/<AppTable/)) {
      if (!text.includes(':error=') && !text.includes('<AppErrorState')) {
        offenders.push(file);
      }
    }
    expect(offenders).toEqual([]);
  });

  it('binds error and retry on every shared AppDataState region', () => {
    const offenders: string[] = [];
    for (const [file, text] of viewsUsing(/<AppDataState/)) {
      if (!text.includes(':error=') || !text.includes('@retry=')) {
        offenders.push(file);
      }
    }
    expect(offenders).toEqual([]);
  });
});
