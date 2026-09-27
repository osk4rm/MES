# Dispatch board

The dispatch board answers the daily question "what runs, on which
shift, in what order". It is a read-only, shift-aware view over
Released and InProgress orders: day buckets with active shifts and
roster headcounts, plus the ordered order rows for the window.

Route `/schedule/dispatch` — API `GET /api/schedule/dispatch?from=…&to=…`.

Reads need only a signed-in user; there is nothing to edit here, so no
permission beyond sign-in applies.

## Name note: dispatch board is not the Harmonogram

The time-phased, per-Work-Center schedule with drag-to-reschedule is
the Gantt view at `/schedule`, named **Harmonogram** — see
[Gantt schedule](12-gantt-schedule.md). The dispatch board keeps its
day/shift shape: it shows *which orders are due*, not *which machine
runs which operation when*. When colleagues say "Harmonogram", they
mean the Gantt view.

## What the board shows

Call the API with a date window (`from`, `to` as dates). The response
contains:

- **Day buckets** for the window. Each day lists the active shifts
  with their time windows and the roster headcount (how many operators
  are assigned that shift, from the operator roster — see
  [Work Centers, operators, rosters and skills](04-master-data-resources.md)).
- **Order rows** covering Released and InProgress orders that are
  overdue, due inside the window, or have no due date. Rows are ordered
  next-up: priority first, then due date. Completed and Closed orders
  never appear.

## Uncovered-shift flags

A shift bucket with no rostered operators is flagged as uncovered:
the machines are available but nobody is assigned. Treat an uncovered
flag as a staffing task, not a scheduling task — assign operators to
the shift in the roster (route `/configuration/operators`, shift
assignment by day), then reload the board. Orders due on uncovered
days stay on the board; the flag only warns that nobody is scheduled
to run them.

## Procedure

1. Open `/schedule/dispatch` and pick the date window (a day or a
   week ahead is typical).
2. Scan the day buckets for uncovered shifts and fix staffing first.
3. Work the order rows top to bottom: the top row is the next-up
   order. Select a row to open the order detail at
   `/production/orders/{id}` and release it (if still Planned) or
   confirm against it (if Released or InProgress) — see
   [Production Orders](07-production-orders.md) and
   [Confirmations](09-confirmations.md).

The window is bounded server-side (the same cap family as the Gantt
view): keep windows to days or weeks, not months. Reads are computed
from existing tables; opening the board never changes data.

## Tenant isolation

The board only ever shows the caller's tenant: shifts, rosters and
orders from other tenants are invisible and never leak into counts.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Missing `from` or `to` | `400` naming the field |
| Window end before window start | `400` |

## Next steps

- Drill into machine timing: [Gantt schedule (Harmonogram)](12-gantt-schedule.md).
- Hand the shift its work list: [Operator panel](14-operator-panel.md).
- Carry context across shifts: [Shift handover](13-shift-handover.md).
