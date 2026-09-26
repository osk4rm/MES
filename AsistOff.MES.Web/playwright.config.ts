import { defineConfig } from '@playwright/test';

/**
 * Committed Playwright smoke suite (issue #272): login -> dispatch board ->
 * operator Confirmation with produced + consumed lots -> Lot tree.
 *
 * The stack is NOT started here. Locally run `pwsh -File scripts/e2e/smoke.ps1`
 * (starts backend + frontend, seeds via the API, runs this suite, cleans up).
 * In CI the (pending) `e2e-smoke` job will start Postgres + the stack
 * before invoking `npx playwright test`.
 */
export default defineConfig({
  testDir: './e2e',
  // Only the smoke journey runs under Playwright. `e2e/support/*.spec.ts`
  // holds Vitest coverage for the pure smoke helpers (run via `npm run test`).
  testMatch: 'smoke/*.spec.ts',
  // The login-to-lots flow is one serial journey sharing a seeded run tag.
  fullyParallel: false,
  workers: 1,
  timeout: 90_000,
  expect: {
    timeout: 15_000
  },
  retries: process.env['CI'] ? 1 : 0,
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }]
  ],
  use: {
    baseURL: process.env['E2E_FRONTEND_URL'] ?? 'http://localhost:5173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  }
});
