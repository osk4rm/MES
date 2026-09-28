import http from './http';
import { AndonSignalCategory } from './andonSignalService';
import type { StatusSignalMeta } from '../composables/useShopfloorDisplay';

/**
 * Operator shift queue read-model (issue #336, slice 2/2). Mirrors
 * `OperatorShiftQueueResponse` from `GET /api/schedule/operator-queue`
 * (slice 1, #335): the roster assignment covering now, the Released and
 * InProgress Production Orders overlapping the shift window ordered next-up
 * (priority, then due date), and the open Andon signals for the queued Work
 * Centers. Read-only — the panel advances work through the existing
 * Production Order lifecycle and confirmation endpoints.
 */
export interface OperatorShiftContext {
  shiftId: string;
  shiftCode: string;
  shiftName: string;
  /** ISO `yyyy-MM-dd`. */
  date: string;
  windowStartUtc: string;
  windowEndUtc: string;
  isOvernight: boolean;
}

export interface OperatorShiftQueuedOrder {
  id: string;
  code: string;
  productId: string;
  productCode: string | null;
  plannedQuantity: number;
  producedQuantity: number;
  scrappedQuantity: number;
  remainingQuantity: number;
  machineId: string | null;
  machineCode: string | null;
  machineName: string | null;
  priority: number;
  dueDate: string | null;
  /** ProductionOrderStatus value. */
  status: number;
  /**
   * True when the order's recipe operations require at least one skill and
   * zero operators assigned to the shift hold every required skill
   * (issue #397). Optional for tolerance of older payloads.
   */
  noQualifiedOperator?: boolean;
}

export interface OperatorShiftQueueSignal {
  id: string;
  machineId: string;
  machineCode: string | null;
  category: AndonSignalCategory;
  /** Category name (the signal table carries no severity column). */
  severity: string;
  raisedAt: string;
}

export interface OperatorShiftQueue {
  operatorCode: string;
  operatorId: string;
  shift: OperatorShiftContext | null;
  orders: OperatorShiftQueuedOrder[];
  activeSignals: OperatorShiftQueueSignal[];
}

const BASE = '/api/schedule/operator-queue';

/** Backend clamps `take` to 200 rows; the panel stays well inside the cap. */
export const OPERATOR_QUEUE_DEFAULT_TAKE = 50;

export const operatorQueueService = {
  /**
   * Read-only current-shift queue for one operator code. An operator with
   * no covering assignment gets HTTP 200 with an empty queue plus shift
   * context; an unknown code yields 404.
   */
  async getQueue(operatorCode: string, take: number = OPERATOR_QUEUE_DEFAULT_TAKE): Promise<OperatorShiftQueue> {
    const { data } = await http.get<OperatorShiftQueue>(BASE, { params: { operatorCode, take } });
    return data;
  }
};

/**
 * Next-up item: rows arrive in the backend ordering contract (priority,
 * then due date), so the head of the list is the next-up task — the panel
 * renders the queue as returned and never re-sorts.
 */
export function nextUpOrder(queue: OperatorShiftQueue | null): OperatorShiftQueuedOrder | null {
  if (!queue || queue.orders.length === 0) return null;
  return queue.orders[0] ?? null;
}

/** True when the operator has a roster assignment covering now. */
export function hasShiftCoverage(queue: OperatorShiftQueue | null): boolean {
  return queue?.shift !== null && queue?.shift !== undefined;
}

export interface MachineSignalGroup {
  machineId: string;
  machineCode: string | null;
  signals: OperatorShiftQueueSignal[];
}

/**
 * Groups open Andon signals by Work Center so the panel renders one
 * machine card per affected Work Center with icon-plus-text badges.
 * Groups keep first-seen order; signals inside a group keep queue order.
 */
export function groupSignalsByMachine(signals: OperatorShiftQueueSignal[]): MachineSignalGroup[] {
  const groups = new Map<string, MachineSignalGroup>();
  for (const signal of signals) {
    const existing = groups.get(signal.machineId);
    if (existing) {
      existing.signals.push(signal);
    } else {
      groups.set(signal.machineId, { machineId: signal.machineId, machineCode: signal.machineCode, signals: [signal] });
    }
  }
  return [...groups.values()];
}

/**
 * Queue-signal severity: color (variant) + icon + text. The signal table
 * carries no severity column (`severity` is the category name), so the
 * mapping keys off the Andon category. Unknown categories fail safe to the
 * abnormal treatment so a new category can never render as calm. Views must
 * always render the icon plus the category/severity text — never color
 * alone.
 */
export function operatorQueueSignalMeta(category: number): StatusSignalMeta {
  if (category === AndonSignalCategory.Quality) return { variant: 'warning', icon: 'pi pi-exclamation-circle' };
  if (category === AndonSignalCategory.Material) return { variant: 'info', icon: 'pi pi-box' };
  if (category === AndonSignalCategory.Other) return { variant: 'primary', icon: 'pi pi-flag' };
  return { variant: 'danger', icon: 'pi pi-exclamation-triangle' };
}
