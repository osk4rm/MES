import { execSync } from 'node:child_process';
import { copyFileSync, mkdirSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// Pins the AC3/AC4 contract (issue #272) without needing Docker/DB.
// The e2e-smoke workflow ships as a versioned patch because the automation
// token cannot push `.github/workflows/*` itself (GitHub refuses with
// "refusing to allow a GitHub App to create or update workflow ... without
// 'workflows' permission"); a maintainer lands it with:
//   git apply scripts/e2e/e2e-smoke-ci.patch
// These tests prove the patch is landing-ready: it applies cleanly onto the
// current ci.yml, and the patched workflow actually defines the e2e-smoke
// job with failure-artifact upload.
const webDir = join(__dirname, '..', '..');
const repoRoot = join(webDir, '..');

function readRepo(relativePath: string): string {
  return readFileSync(join(repoRoot, relativePath), 'utf8');
}

function patchedWorkflow(): string {
  const scratch = join(tmpdir(), `e2e-smoke-ci-${process.pid}`);
  const targetDir = join(scratch, '.github', 'workflows');
  try {
    mkdirSync(targetDir, { recursive: true });
    copyFileSync(
      join(repoRoot, '.github/workflows/ci.yml'),
      join(targetDir, 'ci.yml'),
    );
    // `git apply` works outside a repo; run it with the scratch tree as cwd
    // so the patch lands on the copied ci.yml.
    execSync(
      `git apply "${join(repoRoot, 'scripts/e2e/e2e-smoke-ci.patch')}"`,
      { cwd: scratch, stdio: 'pipe' },
    );
    return readFileSync(join(targetDir, 'ci.yml'), 'utf8');
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
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

  it('applies cleanly onto the current ci.yml base', () => {
    expect(() =>
      execSync(
        `git apply --check --unsafe-paths "${join(repoRoot, 'scripts/e2e/e2e-smoke-ci.patch')}"`,
        { cwd: repoRoot, stdio: 'pipe' },
      ),
    ).not.toThrow();
  });

  it('defines the e2e-smoke job with Postgres, stack start, smoke run and artifact upload once applied', () => {
    const workflow = patchedWorkflow();

    expect(workflow).toContain('e2e-smoke:');
    expect(workflow).toContain('needs.changes.outputs.e2e');
    expect(workflow).toContain('postgres:16-alpine');
    expect(workflow).toContain('app.ps1');
    expect(workflow).toContain('npx playwright test');
    expect(workflow).toContain('actions/upload-artifact@');
    expect(workflow).toContain('playwright-report/');
    expect(workflow).toContain('test-results/');
    expect(workflow).toContain('retention-days: 7');
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
