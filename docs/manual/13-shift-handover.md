# Shift handover

Shift handover carries context across shift boundaries for one Work
Center: what is running, what signalled for help, what was confirmed —
plus a written logbook entry from the outgoing shift. It has two
parts: a read-only context snapshot for any window, and an append-only
logbook of entries.

API `/api/shift-handovers` (no dedicated frontend view; call it from
API clients or the order and machine views that link to it).

Reading context and the logbook needs only a signed-in user; writing a
logbook entry needs the `production.write` permission (`403`
without it).

## Handover context (read-only)

`GET /api/shift-handovers/context?machineId=…&from=…&to=…` resolves
the window for one Work Center:

- the shift covering the window start (from the Work Center calendar;
  when no calendar entry covers it, the context reports no single
  shift and flags the gap as uncovered),
- the open (Released/InProgress) orders,
- the active Andon signals for that machine,
- the recent operator confirmations (paged via `confirmationPage`
  and `confirmationPageSize`).

Rules: `machineId` is optional — omit it for a tenant-wide context
with no single shift. The window may not exceed 24 hours (`400`
otherwise) and `from` must precede `to` (`400`).

Procedure:

1. At shift end, request the context for the Work Center and the
   shift window just worked.
2. Review open orders, active Andon signals and recent confirmations.
3. Write the logbook entry below, referencing what the context
   showed.

## Logbook entries (append-only)

| Field | Required | Rules |
|-------|----------|-------|
| MachineId | Yes | The Work Center the entry belongs to |
| From / To | Yes | The shift window in UTC; `from` before `to`, max 24 hours |
| Notes | Yes | Non-empty, max 2000 characters — what the next shift must know |

Procedure:

1. API: `POST /api/shift-handovers` → `201` with the entry. The
   backend stamps the resolved shift, the open-order count and the
   active-Andon count at creation time, so the entry stays meaningful
   even after orders complete and signals resolve.
2. Read back: `GET /api/shift-handovers` (paged logbook, newest
   boundary first, filterable by machine and by boundary start inside
   `from`/`to`) and `GET /api/shift-handovers/{id}` for one entry
   with its notes, machine, shift window, author and snapshot counts.

History is append-only: there are no update or delete endpoints. A
correction is a new entry. Writing a second entry for the same
(machine, window start) returns `409` — one entry per shift boundary.

Worked example: the 06:00–14:00 shift on Work Center `CELL-3` ends.

1. Request
   `GET /api/shift-handovers/context?machineId={cell3}&from=2026-09-27T06:00:00Z&to=2026-09-27T14:00:00Z`.
2. Note the context: two orders still InProgress, one active Andon
   signal for missing material, four confirmations in the window.
3. Post the entry: machine `CELL-3`, the same window, notes such as
   "Order MO-1041 half confirmed, material Andon still open, do not
   start MO-1042 before QA releases lot L-88".
4. The next shift reads the entry from `GET /api/shift-handovers`
   filtered by the machine before starting work.

## Tenant isolation

Handovers, orders, signals and confirmations are tenant-scoped: the
context and the logbook only ever cover the caller's tenant, and
foreign machine IDs resolve as `404`.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `production.write` (create entry) | `403` |
| Missing machine, window or notes; window over 24 hours; `from` after `to`; notes over 2000 characters | `400` naming the field |
| Unknown machine ID | `404` |
| Second entry for the same (machine, window start) | `409` |
| Unknown handover ID | `404` |

## Next steps

- See what to hand over: [Dispatch board](08-dispatch-board.md) and [Operator panel](14-operator-panel.md).
- Confirm remaining work before handing over: [Confirmations](09-confirmations.md).
- Record stops and loss for the record: [Scrap and Downtime](10-scrap-downtime.md).
