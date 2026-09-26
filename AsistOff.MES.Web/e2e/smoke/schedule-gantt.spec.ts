/**
 * Harmonogram Gantt click-through (issue #306, slice 3/3).
 *
 * Covers the issue test plan that the verifier flagged as missing:
 * sign in as admin, open Harmonogram (`/schedule`), assert per-Work Center
 * bars render for seeded fixture data, drag one bar and assert the
 * reschedule PUT returns 200 with persistence after reload, force a stale
 * token conflict and assert 409, then open the renamed dispatch board
 * (`/schedule/dispatch`) and assert rows render with unchanged data.
 *
 * Seeding mirrors `GanttScheduleEndpointTests` (machine + recipe with one
 * resourced operation, released version, Released order with a due date
 * inside the current week) so the computed Gantt read-model always yields
 * exactly one bar for our order code. Every code derives from a unique
 * `GNT-XXXXXX` tag, so repeat runs pass without manual reset. The Released
 * order, machine and recipe stay behind as an audit trail (same convention
 * as the login-to-lots smoke).
 *
 * Required environment (same as the smoke suite):
 * - `E2E_FRONTEND_URL` (default `http://localhost:5173`)
 * - `E2E_API_URL` (default `http://localhost:5243`)
 * - `E2E_EMAIL` / `E2E_PASSWORD` (default the seeded dev admin)
 *
 * Run via `pwsh -File scripts/e2e/smoke.ps1` (picks up `smoke/*.spec.ts`)
 * or `npx playwright test e2e/smoke/schedule-gantt.spec.ts`.
 * The stack is NOT started here.
 */
import { expect, request as newRequest, test, type APIRequestContext, type Page } from '@playwright/test';
import { SMOKE_EMAIL, SMOKE_PASSWORD } from '../support/smoke-data';

const FRONTEND = process.env['E2E_FRONTEND_URL'] ?? 'http://localhost:5173';
const API = process.env['E2E_API_URL'] ?? 'http://localhost:5243';
const EMAIL = process.env['E2E_EMAIL'] ?? SMOKE_EMAIL;
const PASSWORD = process.env['E2E_PASSWORD'] ?? SMOKE_PASSWORD;

interface GanttBarDto {
  productionOrderId: string;
  productionOrderCode: string;
  operationNodeId: string;
  operationCode: string;
  machineId: string | null;
  plannedStart: string;
  plannedEnd: string;
}

interface GanttGroupDto {
  machineId: string | null;
  machineCode: string | null;
  machineName: string | null;
  bars: GanttBarDto[];
}

interface GanttScheduleDto {
  from: string;
  to: string;
  groups: GanttGroupDto[];
}

function buildTag(): string {
  const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let tag = '';
  let n = Math.floor(Date.now() % 999983) * 997 + Math.floor(Math.random() * 997);
  for (let i = 0; i < 6; i += 1) {
    tag = alphabet[n % alphabet.length] + tag;
    n = Math.floor(n / alphabet.length);
  }
  return tag;
}

function toDateOnly(d: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** Current week Monday..Sunday (same math as `currentWeekWindow`). */
function weekWindow(now: Date = new Date()): { from: string; to: string } {
  const day = (now.getDay() + 6) % 7;
  const monday = new Date(now);
  monday.setDate(now.getDate() - day);
  const sunday = new Date(monday);
  sunday.setDate(monday.getDate() + 6);
  return { from: toDateOnly(monday), to: toDateOnly(sunday) };
}

function randomId(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = Math.floor(Math.random() * 16);
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

let tag = '';
let orderCode = '';
let machineCode = '';
let machineId = '';
let orderId = '';
let windowFrom = '';
let windowTo = '';
let api: APIRequestContext;
const pageErrors: string[] = [];

async function postJson(path: string, body: unknown): Promise<{ status: number; json: <T>() => Promise<T> }> {
  const response = await api.post(`${API}${path}`, { data: body });
  const status = response.status();
  if (status >= 400) {
    const text = await response.text().catch(() => '');
    throw new Error(`POST ${path} failed with ${status}: ${text.slice(0, 500)}`);
  }
  return { status, json: <T,>() => response.json() as Promise<T> };
}

async function seedGanttRun(): Promise<void> {
  tag = buildTag();
  orderCode = `GNT-${tag}-ORD`;
  machineCode = `GNT-${tag}-WC`;

  const window = weekWindow();
  windowFrom = window.from;
  windowTo = window.to;
  // Wednesday noon UTC of the current week: the single 60-minute op lands
  // its bar at [due-60min, due], safely inside the Mon..Sun window.
  const monday = new Date(`${windowFrom}T00:00:00`);
  const dueDate = new Date(monday);
  dueDate.setDate(monday.getDate() + 2);
  dueDate.setHours(12, 0, 0, 0);

  const signIn = await api.post(`${API}/api/auth/sign-in`, { data: { email: EMAIL, password: PASSWORD } });
  if (signIn.status() !== 200) {
    throw new Error(`sign-in failed with ${signIn.status()}: ${(await signIn.text().catch(() => '')).slice(0, 300)}`);
  }

  const machine = await postJson('/api/machines', {
    code: machineCode,
    name: `Gantt Work Center ${tag}`,
    description: null,
    isActive: true
  });
  machineId = (await machine.json<{ id: string }>()).id;

  const recipe = await postJson('/api/recipes', {
    code: `GNT-${tag}-RCP`,
    name: `Gantt recipe ${tag}`,
    description: null,
    isActive: true,
    primaryProductId: null,
    syncId: null
  });
  const recipeBody = await recipe.json<{ id: string; versions: Array<{ id: string }> }>();
  const versionId = recipeBody.versions[0]?.id;
  if (!versionId) throw new Error('recipe seed returned no version');

  const operation = await postJson('/api/operations', {
    versionId,
    code: `GNT-${tag}-OP`,
    name: 'Gantt operation',
    description: null,
    operationType: null,
    sortIndex: 0,
    setupTimeMinutes: null,
    runTimeMode: 1,
    runTimePerUnitSeconds: 60,
    runTimePerBatchMinutes: null,
    teardownTimeMinutes: null,
    queueTimeMinutes: null,
    isOptional: false,
    allowParallelExecution: false,
    expectedQuantity: null
  });
  const operationId = (await operation.json<{ id: string }>()).id;

  await postJson(`/api/operations/${operationId}/resources`, {
    operationId,
    preferredDepartmentId: null,
    preferredMachineId: machineId,
    requiredCapability: null,
    requiredOperatorCount: 1,
    requiredRole: null,
    notes: null
  });

  const releaseVersion = await api.post(`${API}/api/recipe-versions/${versionId}/release`);
  if (releaseVersion.status() !== 204) {
    throw new Error(`release recipe version failed with ${releaseVersion.status()}`);
  }

  const order = await postJson('/api/production-orders', {
    code: orderCode,
    productId: randomId(),
    recipeId: recipeBody.id,
    recipeVersionId: versionId,
    plannedQuantity: 60,
    measureUnitId: null,
    priority: 0,
    dueDate: dueDate.toISOString(),
    notes: `gantt e2e ${tag}`,
    syncId: null
  });
  const createdOrder = await order.json<{ id: string }>();

  const releaseOrder = await api.post(`${API}/api/production-orders/${createdOrder.id}/release`);
  if (releaseOrder.status() !== 200) {
    throw new Error(`release order failed with ${releaseOrder.status()}`);
  }
  orderId = ((await releaseOrder.json()) as { id: string }).id;
}

async function ganttBars(): Promise<GanttBarDto[]> {
  const response = await api.get(`${API}/api/schedule/gantt?from=${windowFrom}&to=${windowTo}`);
  expect(response.status()).toBe(200);
  const schedule = (await response.json()) as GanttScheduleDto;
  return schedule.groups.flatMap((g) => g.bars).filter((b) => b.productionOrderCode === orderCode);
}

async function signInAndGoto(page: Page, path: string): Promise<void> {
  await page.goto(`${FRONTEND}/login`);
  await expect(page.getByTestId('login-form')).toBeVisible();
  await page.getByTestId('login-email').locator('input').fill(EMAIL);
  await page.getByTestId('login-password').locator('input').fill(PASSWORD);
  await page.getByTestId('login-submit').click();
  await expect(page).toHaveURL(/\/dashboard/);
  await page.goto(`${FRONTEND}${path}`);
}

test.describe.serial('harmonogram gantt click-through', () => {
  test.beforeAll(async () => {
    api = await newRequest.newContext();
    await seedGanttRun();
  });

  test.afterAll(async () => {
    // Audit trail (same convention as the login-to-lots smoke): the
    // Released order, machine and recipe stay behind; unique per-run codes
    // keep repeat runs green without manual reset.
    await api.dispose().catch(() => undefined);
  });

  test.beforeEach(async ({ page }) => {
    page.on('pageerror', (error) => {
      pageErrors.push(String(error));
    });
  });

  test('harmonogram renders Gantt bars grouped by Work Center', async ({ page }) => {
    await signInAndGoto(page, `/schedule?from=${windowFrom}&to=${windowTo}`);

    await expect(page.getByTestId('gantt-board')).toBeVisible();

    const bars = await ganttBars();
    expect(bars.length).toBeGreaterThanOrEqual(1);
    const nodeId = bars[0]?.operationNodeId;
    if (!nodeId) throw new Error('gantt seed produced no bars');

    // Lane header carries the seeded Work Center; the bar carries the order.
    await expect(page.getByTestId('gantt-board')).toContainText(machineCode);
    const bar = page.getByTestId(`gantt-bar-${nodeId}`);
    await expect(bar).toBeVisible({ timeout: 30_000 });
    await expect(bar).toContainText(orderCode);
  });

  test('dragging a bar calls the reschedule API and persists after reload', async ({ page }) => {
    await signInAndGoto(page, `/schedule?from=${windowFrom}&to=${windowTo}`);

    const bars = await ganttBars();
    const target = bars[0];
    if (!target) throw new Error('gantt seed produced no bars for drag');
    const bar = page.getByTestId(`gantt-bar-${target.operationNodeId}`);
    await expect(bar).toBeVisible({ timeout: 30_000 });

    const box = await bar.boundingBox();
    if (!box) throw new Error('gantt bar has no bounding box for drag');
    const startX = box.x + Math.min(40, box.width / 2);
    const y = box.y + box.height / 2;

    const put = page.waitForResponse(
      (response) =>
        response.url().includes(`/api/schedule/gantt/segments/${target.operationNodeId}`) &&
        response.request().method() === 'PUT',
      { timeout: 30_000 }
    );
    await page.mouse.move(startX, y);
    await page.mouse.down();
    await page.mouse.move(startX + 80, y, { steps: 12 });
    await page.mouse.up();
    const putResponse = await put;
    expect(putResponse.status()).toBe(200);

    // The bar persists on the moved slot after a full reload.
    const afterDrag = await ganttBars();
    const movedStart = afterDrag.find((b) => b.operationNodeId === target.operationNodeId)?.plannedStart;
    expect(movedStart).toBeTruthy();
    expect(new Date(movedStart ?? '').getTime()).toBeGreaterThan(new Date(target.plannedStart).getTime());

    await page.reload();
    await expect(page.getByTestId('gantt-board')).toBeVisible();
    await expect(page.getByTestId(`gantt-bar-${target.operationNodeId}`)).toBeVisible({ timeout: 30_000 });
  });

  test('stale order token conflicts with 409 and the dispatch board stays reachable', async ({ page }) => {
    // Conflict path: replaying the reschedule with a bogus concurrency
    // token must be rejected so the UI can toast and revert (the toast +
    // revert itself is covered by `ScheduleGanttView.spec.ts`).
    const bars = await ganttBars();
    const target = bars[0];
    if (!target) throw new Error('gantt seed produced no bars for conflict check');
    const order = await api.get(`${API}/api/production-orders/${orderId}`);
    expect(order.status()).toBe(200);

    const conflict = await api.put(
      `${API}/api/schedule/gantt/segments/${target.operationNodeId}`,
      {
        data: {
          productionOrderId: orderId,
          plannedStart: target.plannedStart,
          plannedEnd: target.plannedEnd,
          machineId,
          concurrencyToken: 'stale-token-for-e2e',
          force: false,
          notes: null
        }
      }
    );
    expect(conflict.status()).toBe(409);

    // Old dispatch board under its new name, with unchanged data: the
    // seeded Released order is still listed.
    await signInAndGoto(page, '/schedule/dispatch');
    await expect(page.getByTestId('dispatch-board')).toBeVisible();
    await expect(page.getByTestId('dispatch-orders-table')).toContainText(orderCode, { timeout: 30_000 });

    const board = await api.get(`${API}/api/schedule/dispatch?from=2000-01-01&to=2100-01-01`);
    expect(board.status()).toBe(200);
    const boardJson = (await board.json()) as { orders: Array<{ code: string }> };
    expect(boardJson.orders.some((row) => row.code === orderCode)).toBe(true);

    expect(pageErrors).toEqual([]);
  });
});
