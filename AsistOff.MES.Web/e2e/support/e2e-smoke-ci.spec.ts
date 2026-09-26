import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// Pins the AC3/AC4 contract (issue #272) without needing Docker/DB:
// the versioned workflow patch must stay in sync with the failure-artifact
// config the committed suite relies on. A maintainer with `workflows`
// permission lands it via `git apply scripts/e2e/e2e-smoke-ci.patch`.
const webDir = join(__dirname, '..', '..');
const repoRoot = join(webDir, '..');

function readRepo(relativePath: string): string {
  return readFileSync(join(repoRoot, relativePath), 'utf8');
}

describe('e2e-smoke CI contract', () => {
  it('ships the workflow change as an apply-ready patch', () => {
    const patch = readRepo('scripts/e2e/e2e-smoke-ci.patch');

    expect(patch).toContain('diff --git a/.github/workflows/ci.yml');
    // Paths-filter: the job must trigger on Web / Production / runner / workflow changes.
    expect(patch).toContain('e2e:');
    expect(patch).toContain('AsistOff.MES.Web/**');
    expect(patch).toContain('AsistOff.MES.Production.*/**');
    expect(patch).toContain('scripts/e2e/**');
  });

  it('defines the e2e-smoke job with Postgres, stack start, smoke run and artifact upload', () => {
    const patch = readRepo('scripts/e2e/e2e-smoke-ci.patch');

    expect(patch).toContain('e2e-smoke:');
    expect(patch).toContain('postgres:16-alpine');
    expect(patch).toContain('app.ps1');
    expect(patch).toContain('npx playwright test');
    expect(patch).toContain('actions/upload-artifact@');
    expect(patch).toContain('playwright-report/');
    expect(patch).toContain('test-results/');
    expect(patch).toContain('retention-days: 7');
  });

  it('keeps failure artifacts enabled in the Playwright config', () => {
    const config = readRepo('AsistOff.MES.Web/playwright.config.ts');

    expect(config).toContain("trace: 'retain-on-failure'");
    expect(config).toContain("screenshot: 'only-on-failure'");
    expect(config).toContain('playwright-report');
  });

  it('documents the local artifact paths in the single-command runner', () => {
    const runner = readRepo('scripts/e2e/smoke.ps1');

    expect(runner).toContain('app.ps1');
    expect(runner).toContain('playwright test');
    expect(runner).toContain('playwright-report');
    expect(runner).toContain('test-results');
  });
});
