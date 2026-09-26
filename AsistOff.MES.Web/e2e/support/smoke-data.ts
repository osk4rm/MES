/**
 * Pure helpers for the login-to-lots Playwright smoke suite (issue #272).
 *
 * This module is intentionally free of Playwright imports so the same
 * parsing/validation logic the smoke run relies on is covered by Vitest
 * (`smoke-data.spec.ts`). Anything that touches the network lives in the
 * spec file (`../smoke/login-to-lots.spec.ts`) and in `scripts/e2e/smoke.ps1`.
 *
 * Test-data isolation: every run mints a unique `SMK-XXXXXX` tag and
 * derives all seeded codes from it, so repeat runs never collide and no
 * manual reset is needed. Cleanup is best-effort (confirmation + lots are
 * removed; the Released order is left as an audit trail).
 */

/** Prefix shared by every code minted for a smoke run. */
export const SMOKE_PREFIX = 'SMK';

/** Credentials of the seeded dev tenant (see docs/e2e-local-setup.md). */
export const SMOKE_EMAIL = 'admin@dev.local';
export const SMOKE_PASSWORD = 'Passw0rd!';

export interface SmokeCodes {
  tag: string;
  orderCode: string;
  producedLotCode: string;
  consumedLotCode: string;
  recipeCode: string;
  machineCode: string;
  machineName: string;
}

export interface DispatchOrderRow {
  id: string;
  code: string;
  status: number;
  isOverdue: boolean;
}

export interface TraceabilityNode {
  lotId: string;
  lotCode: string;
  depth: number;
}

export interface TraceabilityResponse {
  rootLotId: string;
  rootLotCode: string;
  nodes: TraceabilityNode[];
  truncated: boolean;
}

/**
 * Mints a 6-character uppercase tag from the current time plus randomness.
 * Optional parameters exist so tests can pin the output deterministically.
 */
export function buildSmokeTag(now: number = Date.now(), random: number = Math.random()): string {
  const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  const timePart = Math.floor(now % Math.pow(alphabet.length, 4));
  let tag = '';
  let n = timePart * 997 + Math.floor(random * 997);
  for (let i = 0; i < 6; i++) {
    tag = alphabet[n % alphabet.length] + tag;
    n = Math.floor(n / alphabet.length);
  }
  return tag;
}

/** Derives every seeded code for a run from its tag. */
export function smokeCodes(tag: string): SmokeCodes {
  const clean = tag.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 6) || 'XXXXXX';
  return {
    tag: clean,
    orderCode: `${SMOKE_PREFIX}-${clean}-ORD`,
    producedLotCode: `${SMOKE_PREFIX}-${clean}-PRD`,
    consumedLotCode: `${SMOKE_PREFIX}-${clean}-CON`,
    recipeCode: `${SMOKE_PREFIX}-${clean}-RCP`,
    machineCode: `${SMOKE_PREFIX}-${clean}-MAC`,
    machineName: 'Smoke Work Center'
  };
}

/** Label of the seeded machine inside the confirmation Work Center dropdown. */
export function machineOptionLabel(codes: SmokeCodes): string {
  return `${codes.machineCode} — ${codes.machineName}`;
}

/** Payload for `POST /api/lots` matching `CreateLotRequest`. */
export function createLotPayload(
  code: string,
  productId: string,
  measureUnitId: string,
  quantity: number
): Record<string, unknown> {
  return {
    code,
    productId,
    measureUnitId,
    quantity,
    supplierLotNumber: null,
    producedAt: null,
    expiryDate: null,
    notes: `smoke ${code}`
  };
}

/** Payload for `POST /api/production-confirmations` with a lot trace. */
export function confirmationPayload(
  productionOrderId: string,
  machineId: string,
  producedLotId: string,
  consumedLotId: string,
  consumedQuantity: number
): Record<string, unknown> {
  return {
    productionOrderId,
    machineId,
    reportedByOperatorId: null,
    reportedAt: new Date().toISOString(),
    goodQuantity: 10,
    scrapQuantity: 0,
    notes: 'smoke confirmation',
    producedLotId,
    consumedLots: [{ lotId: consumedLotId, quantity: consumedQuantity }]
  };
}

/**
 * Finds the seeded order among dispatch rows. Returns null when the board
 * has not picked it up yet so the caller can reload and retry.
 */
export function findSmokeOrder(rows: DispatchOrderRow[], orderCode: string): DispatchOrderRow | null {
  return rows.find((row) => row.code === orderCode) ?? null;
}

/** True when the upstream trace links the produced lot to the consumed lot. */
export function upstreamContainsLot(trace: TraceabilityResponse, consumedLotCode: string): boolean {
  return trace.nodes.some((node) => node.lotCode === consumedLotCode);
}

/**
 * Extracts the production order id from a detail URL
 * (`/production/orders/<id>`). Returns null for anything else so a bad
 * redirect never produces a bogus API call.
 */
export function extractOrderIdFromUrl(url: string): string | null {
  const match = /\/production\/orders\/([0-9a-fA-F-]{36})(?:[/?#]|$)/.exec(url);
  return match ? match[1]! : null;
}

/**
 * Type guard for the dispatch board shape the smoke asserts on. Keeps a
 * backend contract drift from surfacing as an obscure Playwright timeout.
 */
export function isDispatchBoardShape(value: unknown): value is { orders: DispatchOrderRow[] } {
  if (typeof value !== 'object' || value === null) return false;
  const orders = (value as { orders?: unknown }).orders;
  return Array.isArray(orders);
}
