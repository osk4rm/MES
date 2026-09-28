import http from './http';

/**
 * One scheduled operation segment on the Gantt chart. Mirrors
 * `GanttBarResponse` from `GET /api/schedule/gantt`: which Production Order
 * and Operation runs on the Work Center, when, whether it ends after the
 * order due date, and whether the parent order is on hold (`isBlocked` bars
 * render as blocked, not schedulable). Timestamps arrive as ISO strings.
 */
export interface GanttBar {
  productionOrderId: string;
  productionOrderCode: string;
  operationNodeId: string;
  operationCode: string;
  operationName: string;
  machineId: string | null;
  plannedStart: string;
  plannedEnd: string;
  isOverdue: boolean;
  isBlocked: boolean;
}

/**
 * One Work Center lane of the Gantt chart with its operation bars. Mirrors
 * `GanttMachineGroupResponse`. A null `machineId` lane holds computed bars
 * whose operation has no preferred Work Center yet.
 */
export interface GanttMachineGroup {
  machineId: string | null;
  machineCode: string | null;
  machineName: string | null;
  bars: GanttBar[];
}

/** Time-phased Gantt schedule over a caller-supplied date window. */
export interface GanttSchedule {
  /** ISO `yyyy-MM-dd`. */
  from: string;
  /** ISO `yyyy-MM-dd`. */
  to: string;
  groups: GanttMachineGroup[];
}

/** Date window for the Gantt schedule; bounds are ISO `yyyy-MM-dd`. */
export interface GetGanttScheduleQuery {
  from: string;
  to: string;
  machineId?: string;
}

/**
 * Manual placement of one Gantt segment (drag or resize). The backend pins
 * the operation (`operationNodeId` route id) of the given Production Order to
 * the new window and Work Center; `concurrencyToken` is the parent order
 * `xmin` token from the last order read (stale token is a 409, overlap
 * without `force` is a 409 listing the conflicting segments).
 */
export interface RescheduleGanttSegmentCommand {
  productionOrderId: string;
  /** ISO timestamp. */
  plannedStart: string;
  /** ISO timestamp. */
  plannedEnd: string;
  machineId: string;
  concurrencyToken: string;
  force?: boolean;
  notes?: string | null;
}

/** The persisted manual placement after a reschedule move. */
export interface RescheduleGanttSegmentResult {
  id: string;
  productionOrderId: string;
  operationNodeId: string;
  machineId: string;
  plannedStart: string;
  plannedEnd: string;
  /** Advisory: the new window spans dates with zero active shifts or zero roster headcount. */
  shiftCoverageWarning: boolean;
  conflictingSegmentIds: string[];
}

const BASE = '/api/schedule/gantt';

export const scheduleGanttService = {
  /** Time-phased Gantt schedule: per-Work Center lanes with operation bars. */
  async getSchedule(query: GetGanttScheduleQuery): Promise<GanttSchedule> {
    const { data } = await http.get<GanttSchedule>(BASE, { params: { ...query } });
    return data;
  },
  /** Pin one operation segment to a new window / Work Center (drag or resize). */
  async rescheduleSegment(
    operationNodeId: string,
    cmd: RescheduleGanttSegmentCommand
  ): Promise<RescheduleGanttSegmentResult> {
    const { data } = await http.put<RescheduleGanttSegmentResult>(
      `${BASE}/segments/${encodeURIComponent(operationNodeId)}`,
      {
        productionOrderId: cmd.productionOrderId,
        plannedStart: cmd.plannedStart,
        plannedEnd: cmd.plannedEnd,
        machineId: cmd.machineId,
        concurrencyToken: cmd.concurrencyToken,
        force: cmd.force ?? false,
        notes: cmd.notes ?? null
      }
    );
    return data;
  }
};

/** Bar geometry inside its lane track, as percentages of the track width. */
export interface GanttBarLayout {
  leftPct: number;
  widthPct: number;
}

const DAY_MS = 86400000;

function startOfWindowDay(value: string): number {
  return new Date(`${value}T00:00:00`).getTime();
}

/**
 * True when `from..to` is a usable Gantt window: ISO day strings, ordered,
 * and inside the backend 31-day cap (`To - From + 1 <= 31`, i.e. at most a
 * 30-day span — the same contract as the dispatch board).
 */
export function isGanttWindowValid(from: string, to: string): boolean {
  if (!from || !to || from > to) return false;
  const fromMs = startOfWindowDay(from);
  const toMs = startOfWindowDay(to);
  if (!Number.isFinite(fromMs) || !Number.isFinite(toMs)) return false;
  const spanDays = Math.round((toMs - fromMs) / DAY_MS);
  return spanDays >= 0 && spanDays <= 30;
}

/**
 * Position of a bar inside the `from..to` window lane, in percentages of the
 * track width. The window covers whole days (`from 00:00` inclusive to
 * `to 24:00` exclusive); bars sticking out are clamped to the track, and
 * unparseable input yields a zero-width bar at the track start instead of
 * `NaN` styles.
 */
export function layoutGanttBar(bar: GanttBar, windowFrom: string, windowTo: string): GanttBarLayout {
  const zero: GanttBarLayout = { leftPct: 0, widthPct: 0 };
  const windowStartMs = startOfWindowDay(windowFrom);
  const windowEndMs = startOfWindowDay(windowTo) + DAY_MS;
  const barStartMs = new Date(bar.plannedStart).getTime();
  const barEndMs = new Date(bar.plannedEnd).getTime();
  if (
    !Number.isFinite(windowStartMs) ||
    !Number.isFinite(windowEndMs) ||
    !Number.isFinite(barStartMs) ||
    !Number.isFinite(barEndMs) ||
    windowEndMs <= windowStartMs ||
    barEndMs <= barStartMs
  ) {
    return zero;
  }
  const spanMs = windowEndMs - windowStartMs;
  const leftPct = ((Math.max(barStartMs, windowStartMs) - windowStartMs) / spanMs) * 100;
  const widthPct = ((Math.min(barEndMs, windowEndMs) - Math.max(barStartMs, windowStartMs)) / spanMs) * 100;
  return { leftPct: Math.max(0, leftPct), widthPct: Math.max(0, widthPct) };
}

/**
 * Milliseconds a pointer drag of `deltaPx` over a `trackPx`-wide lane
 * represents inside the `from..to` window. Pure so the view stays thin and
 * the conversion is unit-testable without layout.
 */
export function dragDeltaToMs(deltaPx: number, trackPx: number, windowFrom: string, windowTo: string): number {
  const windowStartMs = startOfWindowDay(windowFrom);
  const windowEndMs = startOfWindowDay(windowTo) + DAY_MS;
  if (!Number.isFinite(windowStartMs) || !Number.isFinite(windowEndMs) || windowEndMs <= windowStartMs || trackPx <= 0) {
    return 0;
  }
  return (deltaPx / trackPx) * (windowEndMs - windowStartMs);
}

/**
 * Shift a `[startMs, endMs)` window by `deltaMs`, clamped so the whole
 * window stays inside `windowFrom..windowTo` (whole days). Used while
 * dragging a bar so the preview never leaves the lane track.
 */
export function clampMoveToWindow(
  startMs: number,
  endMs: number,
  deltaMs: number,
  windowFrom: string,
  windowTo: string
): { startMs: number; endMs: number } {
  const windowStartMs = startOfWindowDay(windowFrom);
  const windowEndMs = startOfWindowDay(windowTo) + DAY_MS;
  const durationMs = endMs - startMs;
  if (!Number.isFinite(startMs) || !Number.isFinite(durationMs) || durationMs <= 0 || !Number.isFinite(windowStartMs) || windowEndMs <= windowStartMs) {
    return { startMs, endMs };
  }
  const minDelta = windowStartMs - startMs;
  const maxDelta = windowEndMs - endMs;
  const clamped = Math.min(Math.max(deltaMs, minDelta), maxDelta);
  return { startMs: startMs + clamped, endMs: endMs + clamped };
}

/** True when the bar missed its order due date (mirrors the backend flag). */
export function isGanttBarOverdue(bar: GanttBar): boolean {
  return bar.isOverdue;
}

/**
 * First-fit sub-row assignment for the bars of one lane, parallel to the
 * input order: a bar lands on the first row whose previous bar already
 * ended, so overlapping bars stack instead of painting over each other.
 * Bars with unparseable timestamps each take the next free row. Pure so
 * lane layout is unit-testable without rendering.
 */
export function assignGanttRows(bars: GanttBar[]): number[] {
  const rowEnds: number[] = [];
  return bars.map((bar) => {
    const startMs = new Date(bar.plannedStart).getTime();
    const endMs = new Date(bar.plannedEnd).getTime();
    if (!Number.isFinite(startMs) || !Number.isFinite(endMs) || endMs <= startMs) {
      // Unparseable timestamps block the row so they never paint over a
      // valid sibling (and never share a row with each other).
      rowEnds.push(Number.POSITIVE_INFINITY);
      return rowEnds.length - 1;
    }
    let row = rowEnds.findIndex((rowEnd) => startMs >= rowEnd);
    if (row === -1) {
      row = rowEnds.length;
      rowEnds.push(endMs);
    } else {
      rowEnds[row] = endMs;
    }
    return row;
  });
}

function errorStatus(err: unknown): number | undefined {
  if (typeof err !== 'object' || err === null) return undefined;
  const response = (err as { response?: unknown }).response;
  if (typeof response !== 'object' || response === null) return undefined;
  const status = (response as { status?: unknown }).status;
  return typeof status === 'number' ? status : undefined;
}

/**
 * i18n key for a failed reschedule move: 409 (stale order token or an
 * overlapping segment without force) maps to the conflict toast with retry
 * guidance, 404 to the missing-target toast, 403 to access-denied, anything
 * else to the generic save failure. The caller toasts `t(key)` and reverts
 * the bar by reloading the schedule.
 */
export function rescheduleErrorKey(err: unknown): string {
  switch (errorStatus(err)) {
    case 409:
      return 'scheduleGantt.conflict';
    case 404:
      return 'scheduleGantt.moveTargetMissing';
    case 403:
      return 'errors.accessDenied';
    default:
      return 'errors.saveFailed';
  }
}
