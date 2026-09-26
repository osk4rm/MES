/**
 * Committed smoke suite (issue #272): login with post-login redirect,
 * Released orders on the dispatch board, an operator Confirmation carrying
 * produced + consumed lots, and the resulting Lot tree.
 *
 * Data isolation: `beforeAll` mints a unique `SMK-XXXXXX` tag and seeds a
 * machine, recipe (+ operation, released version), Released order and two
 * lots through the API. Every assertion filters by those codes, so repeat
 * runs pass without manual reset. `afterAll` removes the Confirmation and
 * the lots (best-effort); the Released order stays as an audit trail.
 *
 * Required environment:
 * - `E2E_FRONTEND_URL` (default `http://localhost:5173`)
 * - `E2E_API_URL` (default `http://localhost:5243`)
 * - `E2E_EMAIL` / `E2E_PASSWORD` (default the seeded dev admin)
 *
 * Local single command: `pwsh -File scripts/e2e/smoke.ps1`.
 */
import { expect, request as newRequest, test, type APIRequestContext, type Page } from '@playwright/test';
import {
  buildSmokeTag,
  confirmationPayload,
  createLotPayload,
  extractOrderIdFromUrl,
  findSmokeOrder,
  isDispatchBoardShape,
  machineOptionLabel,
  smokeCodes,
  smokeCleanupPlan,
  SMOKE_EMAIL,
  SMOKE_PASSWORD,
  upstreamContainsLot,
  type DispatchOrderRow,
  type SmokeCodes,
  type TraceabilityResponse
} from '../support/smoke-data';

const FRONTEND = process.env['E2E_FRONTEND_URL'] ?? 'http://localhost:5173';
const API = process.env['E2E_API_URL'] ?? 'http://localhost:5243';
const EMAIL = process.env['E2E_EMAIL'] ?? SMOKE_EMAIL;
const PASSWORD = process.env['E2E_PASSWORD'] ?? SMOKE_PASSWORD;

let codes: SmokeCodes;
let api: APIRequestContext;
let machineId = '';
let orderId = '';
let producedLotId = '';
let consumedLotId = '';
let confirmationId = '';
const pageErrors: string[] = [];

function randomId(): string {
  // ProductId / MeasureUnitId are free-form references on orders and lots
  // (no FK existence check in the handlers — endpoint tests seed them with
  // Guid.NewGuid() the same way), so a random GUID is a valid seed value.
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = Math.floor(Math.random() * 16);
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

async function postJson(path: string, body: unknown): Promise<{ status: number; json: <T>() => Promise<T> }> {
  const response = await api.post(`${API}${path}`, { data: body });
  const status = response.status();
  if (status >= 400) {
    const text = await response.text().catch(() => '');
    throw new Error(`POST ${path} failed with ${status}: ${text.slice(0, 500)}`);
  }
  return { status, json: <T,>() => response.json() as Promise<T> };
}

async function seedSmokeRun(): Promise<void> {
  codes = smokeCodes(buildSmokeTag());

  const signIn = await api.post(`${API}/api/auth/sign-in`, { data: { email: EMAIL, password: PASSWORD } });
  if (signIn.status() !== 200) {
    throw new Error(`sign-in failed with ${signIn.status()}: ${(await signIn.text().catch(() => '')).slice(0, 300)}`);
  }

  const machine = await postJson('/api/machines', {
    code: codes.machineCode,
    name: codes.machineName,
    description: null,
    isActive: true
  });
  machineId = (await machine.json<{ id: string }>()).id;

  const recipe = await postJson('/api/recipes', {
    code: codes.recipeCode,
    name: `Smoke recipe ${codes.tag}`,
    description: null,
    isActive: true,
    primaryProductId: null,
    syncId: null
  });
  const recipeBody = await recipe.json<{ id: string; versions: Array<{ id: string }> }>();
  const versionId = recipeBody.versions[0]!.id;

  await postJson('/api/operations', {
    versionId,
    code: `${codes.tag}-OP`,
    name: 'Smoke operation',
    description: null,
    operationType: null,
    sortIndex: 0,
    setupTimeMinutes: null,
    runTimeMode: 1,
    runTimePerUnitSeconds: 10,
    runTimePerBatchMinutes: null,
    teardownTimeMinutes: null,
    queueTimeMinutes: null,
    isOptional: false,
    allowParallelExecution: false,
    expectedQuantity: null
  });

  const releaseVersion = await api.post(`${API}/api/recipe-versions/${versionId}/release`);
  if (releaseVersion.status() !== 204) {
    throw new Error(`release recipe version failed with ${releaseVersion.status()}`);
  }

  const order = await postJson('/api/production-orders', {
    code: codes.orderCode,
    productId: randomId(),
    recipeId: recipeBody.id,
    recipeVersionId: versionId,
    plannedQuantity: 100,
    measureUnitId: null,
    priority: 0,
    dueDate: null,
    notes: `smoke ${codes.tag}`,
    syncId: null
  });
  const createdOrder = await order.json<{ id: string }>();

  const releaseOrder = await api.post(`${API}/api/production-orders/${createdOrder.id}/release`);
  if (releaseOrder.status() !== 200) {
    throw new Error(`release order failed with ${releaseOrder.status()}`);
  }
  orderId = (await releaseOrder.json() as { id: string }).id;

  const produced = await postJson(
    '/api/lots',
    createLotPayload(codes.producedLotCode, randomId(), randomId(), 50)
  );
  producedLotId = (await produced.json<{ id: string }>()).id;

  const consumed = await postJson(
    '/api/lots',
    createLotPayload(codes.consumedLotCode, randomId(), randomId(), 50)
  );
  consumedLotId = (await consumed.json<{ id: string }>()).id;
}

async function cleanupSmokeRun(): Promise<void> {
  // Best-effort: a failed DELETE must not fail the suite, but it must not be
  // silent either — warn so a leaking run shows up in the reporter output.
  // Order and audit-trail scope come from smokeCleanupPlan (covered by
  // support/smoke-data.spec.ts): confirmation first, then lots; the Released
  // order, machine and recipe stay behind as an audit trail.
  const warn = (what: string) => (error: unknown): null => {
    // eslint-disable-next-line no-console
    console.warn(`smoke cleanup: DELETE ${what} failed: ${String(error)}`);
    return null;
  };
  const attempts = smokeCleanupPlan({ confirmationId, producedLotId, consumedLotId }).map((step) =>
    api.delete(`${API}${step.path}`).catch(warn(step.label))
  );
  // The Released order, machine and recipe stay behind as an audit trail;
  // unique per-run codes keep repeat runs green without manual reset.
  await Promise.all(attempts);
}

async function signIn(page: Page): Promise<void> {
  await page.getByTestId('login-email').locator('input').fill(EMAIL);
  await page.getByTestId('login-password').locator('input').fill(PASSWORD);
  await page.getByTestId('login-submit').click();
}

/**
 * Every test runs in a fresh browser context, so tests after the redirect
 * check sign in directly before driving their page.
 */
async function signInAndGoto(page: Page, path: string): Promise<void> {
  await page.goto(`${FRONTEND}/login`);
  await expect(page.getByTestId('login-form')).toBeVisible();
  await signIn(page);
  await expect(page).toHaveURL(/\/dashboard/);
  await page.goto(`${FRONTEND}${path}`);
}

test.describe.serial('login to lots smoke', () => {
  test.beforeAll(async () => {
    api = await newRequest.newContext();
    await seedSmokeRun();
  });

  test.afterAll(async () => {
    if (api) {
      await cleanupSmokeRun().catch((error: unknown) => {
        // eslint-disable-next-line no-console
        console.warn(`smoke cleanup: unexpected failure: ${String(error)}`);
      });
      await api.dispose().catch((error: unknown) => {
        // eslint-disable-next-line no-console
        console.warn(`smoke cleanup: api.dispose failed: ${String(error)}`);
      });
    }
  });

  test.beforeEach(async ({ page }) => {
    page.on('pageerror', (error) => {
      pageErrors.push(String(error));
    });
  });

  test('login honours the post-login redirect', async ({ page }) => {
    // Deep link while anonymous must bounce to login and remember the target.
    await page.goto(`${FRONTEND}/production/lots`);

    await expect(page).toHaveURL(/\/login\?redirect=/);
    await expect(page.getByTestId('login-form')).toBeVisible();

    await signIn(page);

    // Post-login redirect lands back on the requested Lots page.
    await expect(page).toHaveURL(/\/production\/lots/);
    await expect(page.getByTestId('lots-page')).toBeVisible();
  });

  test('dispatch board lists the seeded Released order', async ({ page }) => {
    await signInAndGoto(page, '/schedule');

    await expect(page.getByTestId('dispatch-board')).toBeVisible();
    const table = page.getByTestId('dispatch-orders-table');
    await expect(table).toContainText(codes.orderCode, { timeout: 30_000 });

    // The board payload itself carries the seeded row (backend ordering
    // contract is covered by endpoint tests; here we prove visibility).
    const board = await api.get(
      `${API}/api/schedule/dispatch?from=2000-01-01&to=2100-01-01`
    );
    expect(board.status()).toBe(200);
    const boardJson: unknown = await board.json();
    expect(isDispatchBoardShape(boardJson)).toBe(true);
    if (isDispatchBoardShape(boardJson)) {
      const rows = boardJson.orders as DispatchOrderRow[];
      expect(findSmokeOrder(rows, codes.orderCode)).not.toBeNull();
    }

    // Opening the row lands on the order detail page.
    await table.getByText(codes.orderCode).click();
    await expect(page).toHaveURL(new RegExp(`/production/orders/${orderId}`));
    await expect(page.getByTestId('order-detail')).toBeVisible();
    // The detail URL carries the seeded order id (guards a wrong-row redirect).
    expect(extractOrderIdFromUrl(page.url())).toBe(orderId);
  });

  test('confirmation posts produced plus consumed lots', async ({ page }) => {
    await signInAndGoto(page, `/production/orders/${orderId}`);

    await expect(page.getByTestId('order-detail')).toBeVisible();
    await page.getByTestId('report-confirmation').click();
    const modal = page.getByTestId('confirmation-modal');
    await expect(modal).toBeVisible();

    // The seeded machine is offered in the Work Center dropdown under the
    // same `${code} — ${name}` label the detail view builds its options with.
    const machineSelect = page.getByTestId('confirmation-machine-select');
    await expect(machineSelect).toContainText(machineOptionLabel(codes));
    await machineSelect.selectOption(machineId);
    await page.getByTestId('confirmation-good-qty').locator('input').fill('10');
    await page.getByTestId('confirmation-produced-lot').selectOption(producedLotId);

    await page.getByTestId('add-consumed-lot').click();
    await page.getByTestId('consumed-lot-select-0').selectOption(consumedLotId);
    await page.getByTestId('consumed-lot-qty-0').locator('input').fill('5');

    const created = page.waitForResponse(
      (response) =>
        response.url().includes('/api/production-confirmations') &&
        response.request().method() === 'POST' &&
        response.status() === 201
    );
    await page.getByTestId('confirmation-save').click();
    const response = await created;
    confirmationId = ((await response.json()) as { id: string }).id;
    expect(confirmationId).toBeTruthy();

    // The UI posts the same contract the API documents: order, machine,
    // quantities and the produced/consumed lot linkage.
    const sent = response.request().postDataJSON() as Record<string, unknown>;
    const expected = confirmationPayload(orderId, machineId, producedLotId, consumedLotId, 5);
    expect(sent['productionOrderId']).toBe(expected['productionOrderId']);
    expect(sent['machineId']).toBe(expected['machineId']);
    expect(sent['goodQuantity']).toBe(expected['goodQuantity']);
    expect(sent['producedLotId']).toBe(expected['producedLotId']);
    expect(sent['consumedLots']).toEqual(expected['consumedLots']);

    // The modal closes and the confirmation is persisted with its trace.
    await expect(modal).toBeHidden();
    const stored = await api.get(`${API}/api/production-confirmations/${confirmationId}`);
    expect(stored.status()).toBe(200);
    const storedBody = await stored.json() as { goodQuantity: number };
    expect(storedBody.goodQuantity).toBe(10);
  });

  test('lot tree shows the consumed lot behind the produced lot', async ({ page }) => {
    await signInAndGoto(page, `/production/lots?lotId=${producedLotId}&tab=genealogy`);

    await expect(page.getByTestId('lot-detail-modal')).toBeVisible();
    const upstream = page.getByTestId('lot-genealogy-upstream');
    await expect(upstream).toContainText(codes.consumedLotCode, { timeout: 30_000 });

    const traceResponse = await api.get(`${API}/api/lot-genealogy/upstream/${producedLotId}?maxDepth=5`);
    expect(traceResponse.status()).toBe(200);
    const trace = (await traceResponse.json()) as TraceabilityResponse;
    expect(upstreamContainsLot(trace, codes.consumedLotCode)).toBe(true);

    // No uncaught client errors across the whole login-to-lots journey.
    expect(pageErrors).toEqual([]);
  });
});
