import { execSync } from 'node:child_process';
import { copyFileSync, mkdirSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// Pins the AC3/AC4 contract (issue #272) without needing Docker/DB.
// The assertions run against the *effective* workflow: the live
// `.github/workflows/ci.yml` once it defines the `e2e-smoke` job, otherwise
// the versioned patch (`scripts/e2e/e2e-smoke-ci.patch`) applied onto the
// current ci.yml in a scratch dir. The patch exists because the automation
// token historically could not push `.github/workflows/*` itself (GitHub
// refuses with "refusing to allow a GitHub App to create or update workflow
// ... without 'workflows' permission"); a maintainer lands it with:
//   git apply scripts/e2e/e2e-smoke-ci.patch
// Once landed, the patch file must be deleted so the two definitions of the
// same job cannot drift apart (pinned by the shipping-mode test below).
const webDir = join(__dirname, '..', '..');
const repoRoot = join(webDir, '..');
const liveWorkflowPath = '.github/workflows/ci.yml';
const workflowPatchPath = 'scripts/e2e/e2e-smoke-ci.patch';

function readRepo(relativePath: string): string {
  return readFileSync(join(repoRoot, relativePath), 'utf8');
}

function isLanded(): boolean {
  return readRepo(liveWorkflowPath).includes('e2e-smoke:');
}

function effectiveWorkflow(): string {
  const live = readRepo(liveWorkflowPath);
  if (live.includes('e2e-smoke:')) return live;
  const scratch = join(tmpdir(), `e2e-smoke-ci-${process.pid}`);
  const targetDir = join(scratch, '.github', 'workflows');
  try {
    mkdirSync(targetDir, { recursive: true });
    copyFileSync(join(repoRoot, liveWorkflowPath), join(targetDir, 'ci.yml'));
    // `git apply` works outside a repo; run it with the scratch tree as cwd
    // so the patch lands on the copied ci.yml.
    execSync(`git apply "${join(repoRoot, workflowPatchPath)}"`, {
      cwd: scratch,
      stdio: 'pipe',
    });
    return readFileSync(join(targetDir, 'ci.yml'), 'utf8');
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
}

describe('e2e-smoke CI contract', () => {
  it('ships the workflow change live, or as an apply-ready patch fallback', () => {
    if (isLanded()) {
      // Landed: the versioned patch is superseded and must be gone, so the
      // live job is the single source of truth.
      expect(() => readRepo(workflowPatchPath)).toThrow();
      return;
    }
    const patch = readRepo(workflowPatchPath);

    expect(patch).toContain('diff --git a/.github/workflows/ci.yml');
    // Paths-filter: the job must trigger on Web / Production / runner / workflow changes.
    expect(patch).toContain('e2e:');
    expect(patch).toContain('AsistOff.MES.Web/**');
    expect(patch).toContain('AsistOff.MES.Production.*/**');
    expect(patch).toContain('scripts/e2e/**');
  });

  it('applies cleanly onto the current ci.yml base while the patch fallback ships', () => {
    if (isLanded()) return;
    expect(() =>
      execSync(
        `git apply --check --unsafe-paths "${join(repoRoot, workflowPatchPath)}"`,
        { cwd: repoRoot, stdio: 'pipe' },
      ),
    ).not.toThrow();
  });

  it('defines the e2e-smoke job with Postgres, stack start, smoke run and artifact upload in the effective workflow', () => {
    const workflow = effectiveWorkflow();

    expect(workflow).toContain('e2e-smoke:');
    expect(workflow).toContain('needs.changes.outputs.e2e');
    expect(workflow).toContain('postgres:16-alpine');
    expect(workflow).toContain('app.ps1');
    expect(workflow).toContain('npx playwright test');
    expect(workflow).toContain('actions/upload-artifact@');
    expect(workflow).toContain('playwright-report/');
    expect(workflow).toContain('test-results/');
    expect(workflow).toContain('retention-days: 7');
    // AC4: failure bundle must include the stack logs under a stable
    // artifact name and upload even when the smoke run itself fails.
    expect(workflow).toContain('e2e-smoke-report');
    expect(workflow).toContain('e2e-*.log');
    expect(workflow).toContain('if: always()');
  });

  it('wires the stack endpoints the smoke suite actually dials in the effective workflow', () => {
    const workflow = effectiveWorkflow();

    // The Playwright suite talks to the stack via E2E_FRONTEND_URL (:5173)
    // and E2E_API_URL (:5243), and the backend needs the service Postgres
    // (:5432) through the same connection-string key app.ps1 uses. A job
    // with wrong ports/keys would start green and fail every check.
    expect(workflow).toContain('needs: changes');
    expect(workflow).toContain('5432:5432');
    expect(workflow).toContain('pg_isready');
    expect(workflow).toContain('postgres__connectionString');
    // Pin the full value, not just the key: a wrong password/database here
    // would still start Postgres green and fail every API call in the suite.
    expect(workflow).toContain(
      'postgres__connectionString: Host=localhost;Port=5432;Database=mes;Username=admin;Password=root',
    );
    expect(workflow).toContain('E2E_FRONTEND_URL: http://localhost:5173');
    expect(workflow).toContain('E2E_API_URL: http://localhost:5243');
  });

  it('probes Postgres health with the job credentials in the effective workflow', () => {
    const workflow = effectiveWorkflow();

    // The service creates role `admin` / db `mes`, so a bare `pg_isready`
    // (which probes role/db `postgres`) can report unhealthy forever and
    // stall the job before the stack even starts. The probe must target
    // the created role and database.
    expect(workflow).toContain('pg_isready -U admin -d mes');
  });

  it('gates the job on the e2e filter, self-triggers on workflow edits, and always stops the stack', () => {
    const workflow = effectiveWorkflow();

    // The job must run exactly when the e2e filter fires — a `!=` typo
    // would silently invert the gate and never run the smoke suite.
    expect(workflow).toContain("if: needs.changes.outputs.e2e == 'true'");
    // Edits to the workflow itself must re-trigger the smoke job, otherwise
    // a broken job definition lands without ever being exercised.
    expect(workflow).toContain('.github/workflows/ci.yml');
    // Both the stack-stop and the artifact-upload steps must run even when
    // the smoke run fails: one `always()` (upload only) would leak the
    // stack, and the other way round would lose the failure bundle.
    const alwaysCount = workflow.split('if: always()').length - 1;
    expect(alwaysCount).toBeGreaterThanOrEqual(2);
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
