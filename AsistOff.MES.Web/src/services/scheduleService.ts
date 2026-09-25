import http from './http';

/**
 * One active shift inside a dispatch day bucket. Mirrors
 * `DispatchShiftResponse` from `GET /api/schedule/dispatch`: code, name,
 * start/end times (ISO `HH:mm:ss`), the overnight flag and the roster
 * headcount for the bucket date.
 */
export interface DispatchShift {
  shiftId: string;
  code: string;
  name: string;
  startTime: string;
  endTime: string;
  isOvernight: boolean;
  headcount: number;
}

/** One calendar day of the requested window with its active shifts. */
export interface DispatchDay {
  /** ISO `yyyy-MM-dd`. */
  date: string;
  shifts: DispatchShift[];
}

/**
 * A Released or InProgress Production Order that is overdue, due inside the
 * window or has no due date. Mirrors `DispatchOrderRowResponse`: read-time
 * confirmation totals plus the `isOverdue` flag. Rows arrive in the backend
 * ordering contract (overdue first, then due date ascending with nulls last,
 * then priority, then code) — the board renders them as returned.
 */
export interface DispatchOrderRow {
  id: string;
  code: string;
  productId: string;
  plannedQuantity: number;
  producedQuantity: number;
  scrappedQuantity: number;
  remainingQuantity: number;
  priority: number;
  dueDate: string | null;
  status: number;
  isOverdue: boolean;
}

/** Shift-aware dispatch board over a caller-supplied date window. */
export interface DispatchBoard {
  /** ISO `yyyy-MM-dd`. */
  from: string;
  /** ISO `yyyy-MM-dd`. */
  to: string;
  days: DispatchDay[];
  orders: DispatchOrderRow[];
}

/** Date window for the dispatch board; both bounds are ISO `yyyy-MM-dd`. */
export interface GetDispatchBoardQuery {
  from: string;
  to: string;
}

const BASE = '/api/schedule/dispatch';

export const scheduleService = {
  /** Read-only dispatch board: day buckets with shifts/headcounts plus ordered Released/InProgress order rows. */
  async getDispatch(query: GetDispatchBoardQuery): Promise<DispatchBoard> {
    const { data } = await http.get<DispatchBoard>(BASE, { params: { ...query } });
    return data;
  }
};

/**
 * Formats a date as an ISO `yyyy-MM-dd` day string using local calendar
 * fields (no UTC shift), suitable for the dispatch `from`/`to` params and
 * native `type="date"` inputs.
 */
export function toDateOnlyString(value: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
}

/**
 * Default board window: the current week, Monday to Sunday. A window longer
 * than 31 days is rejected by the backend with 400, so callers stay inside
 * the week by construction.
 */
export function currentWeekWindow(now: Date = new Date()): GetDispatchBoardQuery {
  const day = (now.getDay() + 6) % 7;
  const monday = new Date(now);
  monday.setDate(now.getDate() - day);
  const sunday = new Date(monday);
  sunday.setDate(monday.getDate() + 6);
  return { from: toDateOnlyString(monday), to: toDateOnlyString(sunday) };
}

/** True when the row missed its due date before the window start. */
export function isDispatchRowOverdue(row: DispatchOrderRow): boolean {
  return row.isOverdue;
}
