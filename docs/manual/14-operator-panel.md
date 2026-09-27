# Operator panel (shift queue)

The Operator panel is the shopfloor work list: an operator identifies
by code, and the system answers with the current-shift assignment,
the queued Released/InProgress orders in next-up order, and the open
Andon signals for the queued Work Centers. From there the operator
claims work and reports confirmations, scrap, Downtime and Andon
signals through the same endpoints the back office uses.

API `GET /api/schedule/operator-queue?operatorCode=…&take=…` for the
queue; all actions reuse the documented production endpoints.

Reading the queue needs only a signed-in user; every action (confirm,
scrap, Downtime, Andon signal, release, complete, close) needs the
`production.write` permission (`403` without it).

## Opening the queue

| Parameter | Required | Rules |
|-----------|----------|-------|
| operatorCode | Yes | The operator's code; unknown codes return `404` |
| take | No | Caps the queue rows; values are clamped to 200 |

The response contains:

- the operator context and the roster assignment covering now (shift
  and Work Centers); an operator with no covering assignment gets an
  empty queue with the operator context, not an error,
- the queued orders overlapping the shift window, ordered next-up
  (priority, then due date) with remaining quantities,
- the open Andon signals for the queued Work Centers, each with
  category, severity text and status — never color alone.

Procedure:

1. Enter the operator code to load
   `GET /api/schedule/operator-queue?operatorCode={code}`.
2. If the queue is empty, check the roster first: either the operator
   has no assignment covering now, or no Released/InProgress order
   overlaps the shift window.
3. Take the top row as the next-up task.

## Shift actions from the queue

Every action below is the standard endpoint; the queue only decides
*what* to act on.

| Action | Endpoint | Notes |
|--------|----------|-------|
| Claim / start | First `POST /api/production-confirmations` for the order | There is no separate start action: the first confirmation flips the order Released → InProgress automatically |
| Confirm output | `POST /api/production-confirmations` | Good/scrap quantities with produced and consumed lots — see [Confirmations](09-confirmations.md) |
| Report scrap | `POST /api/scrap-events` | Machine, Scrap-category Reason code, quantity — see [Scrap and Downtime](10-scrap-downtime.md) |
| Capture Downtime | `POST /api/downtime-events` then `POST /api/downtime-events/{id}/close` | One open event per Work Center — see [Scrap and Downtime](10-scrap-downtime.md) |
| Raise Andon | `POST /api/andon-signals` | Category Downtime, Quality, Material or Other, optional Reason code; acknowledge with `POST /api/andon-signals/{id}/acknowledge`, resolve with `POST /api/andon-signals/{id}/resolve` |
| Finish the order | `POST /api/production-orders/{id}/complete`, then `…/close` | Completion needs confirmed good quantity — see [Production Orders](07-production-orders.md) |

Andon signal lifecycle for reference:

```text
Active  --acknowledge-->  Acknowledged  --resolve-->  Resolved
```

Raising needs the machine, a category and the raised time; resolving
accepts an optional resolved time (defaults to now). Updating
(`PUT /api/andon-signals/{id}`) and deleting
(`DELETE /api/andon-signals/{id}`) exist for corrections; a route/body
ID mismatch returns `400`.

Worked example: operator `OP-014` starts the shift.

1. Load `GET /api/schedule/operator-queue?operatorCode=OP-014`:
   assignment covers 06:00–14:00 on `CELL-3`, top row is order
   `MO-1041` (Released), one open Material Andon on `CELL-3`.
2. Claim: report the first confirmation for `MO-1041` on `CELL-3`
   with `OP-014` as the reporting operator — the order flips to
   InProgress.
3. Mid-shift the feeder jams: capture Downtime on `CELL-3` with the
   breakdown Reason code, raise a Material Andon, clear the jam,
   close the Downtime event, then confirm the next quantity.
4. At shift end the queue shows `MO-1041` further down (remaining
   quantity reduced) — the next shift picks it up from the same
   queue.

## Tenant isolation

The queue, orders, signals and roster are tenant-scoped: an operator
code from another tenant resolves as `404`, and the queue never
mixes tenants.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `production.write` (actions) | `403` |
| Missing operator code | `400` |
| Unknown operator code | `404` |
| Action validation (quantities, times, lots, reasons) | Same `400`/`404`/`409` contract as the linked chapter for that endpoint |

## Next steps

- Full confirmation rules: [Confirmations](09-confirmations.md).
- Loss capture rules: [Scrap and Downtime](10-scrap-downtime.md).
- Carry context to the next shift: [Shift handover](13-shift-handover.md).
- Trace what the shift produced: [Lots and genealogy](11-lots-genealogy.md).
