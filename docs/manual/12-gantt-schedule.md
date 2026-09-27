# Gantt schedule (Harmonogram)

The Gantt view — named **Harmonogram** — is the time-phased schedule:
every Released or InProgress order explodes into operation segments,
grouped per Work Center on a timeline, with manual reschedule moves
overlaid. Use it to see machine load and to move work when the plan
changes.

Route `/schedule` — API `GET /api/schedule/gantt?from=…&to=…`,
reschedule `PUT /api/schedule/gantt/segments/{id}`.

Reading the schedule needs only a signed-in user; moving a segment
needs the `production.write` permission (`403` without it).

## Name note

Until recently the day/shift order board also carried scheduling
labels. The split is now fixed: the **dispatch board** at
`/schedule/dispatch` shows which orders are due per day and shift —
see [Dispatch board](08-dispatch-board.md) — while **Harmonogram** at
`/schedule` shows which machine runs which operation when. This
chapter covers Harmonogram only.

## Reading the schedule

Call the API with a date window (`from`, `to`) and an optional
`machineId` to focus one Work Center. The response contains operation
segments grouped per machine: each segment carries the order, the
operation, the planned window and whether it comes from the computed
schedule or from a manual override. The window is capped at 31 days
and 200 order rows — keep views to days or weeks.

Procedure:

1. Open `/schedule` and pick the window (and a machine to focus).
2. Scan each machine lane for overlaps and for work spilling past
   due dates.
3. Select a segment to see its order, operation and source; follow
   the order link to `/production/orders/{id}` for detail.

The schedule is a read-model computed from orders, routing times and
saved overrides: opening it never changes data.

## Rescheduling a segment

Drag a segment to a new window or Work Center (or edit its fields);
the view persists the move as a manual override pinned to the
operation of that order.

| Field | Required | Rules |
|-------|----------|-------|
| ProductionOrderId | Yes | The order the operation belongs to |
| PlannedStart / PlannedEnd | Yes | The new window; start must precede end |
| MachineId | Yes | The target Work Center |
| ConcurrencyToken | Yes* | The order's token from the last `GET`; a stale token returns `409` with the current token — reload the order, re-apply the move, resubmit |
| Force | Yes | `false` checks capacity, `true` bypasses the overlap check |
| Notes | No | Why the move was made |

API: `PUT /api/schedule/gantt/segments/{id}` (where `{id}` is the
operation node) → `200` with the saved segment.

## Leveling and conflicts

Without `force`, the backend level-checks the move against the
target Work Center's lane: if the new window overlaps an already
scheduled segment there, the move is rejected with `409` listing the
conflicting segment IDs. Resolve it by picking a free window, moving
the conflicting segment first, or — when the overlap is deliberate
(overtime, parallel-capable steps) — repeating the move with
`force: true`. A forced move persists anyway and the response names
the overridden segment IDs, so the bypass stays auditable.

## Tenant isolation

The schedule only ever computes the caller's tenant: other tenants'
orders, machines and overrides are invisible and never affect lane
load.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `production.write` (reschedule) | `403` |
| Missing order, window or machine; start after end | `400` naming the field |
| Stale order concurrency token | `409` with the current token |
| Overlapping move on the same Work Center without `force` | `409` listing the conflicting segment IDs |
| Unknown operation or order ID | `404` |

## Next steps

- See which orders are due per shift: [Dispatch board](08-dispatch-board.md).
- Report against a scheduled operation: [Confirmations](09-confirmations.md).
- Give the shift its queue: [Operator panel](14-operator-panel.md).
