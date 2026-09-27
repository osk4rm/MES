# Scrap and Downtime with Reason codes

Scrap and Downtime are captured as separate events from confirmations:
a scrap event records process loss against a Work Center with a reason,
and a Downtime event records that a Work Center was scheduled to run
but did not, from start until close. Both draw their reasons from the
Reason code dictionary so losses can be grouped and compared.

Routes `/production/scrap` and `/production/downtime` — APIs
`/api/scrap-events` and `/api/downtime-events`. The dictionary lives
at `/configuration/reason-codes` — API `/api/reason-codes`.

Reads need only a signed-in user. Scrap and Downtime writes need the
`production.write` permission; managing the Reason code dictionary
needs the `configuration.write` permission (`403` without the right
one).

## Reason code dictionary

A Reason code is a controlled-vocabulary entry with a code, a name and
a category. Maintain it before capturing loss so operators pick from a
list instead of typing free text.

| Category | Use for |
|----------|---------|
| Downtime | Why the Work Center stopped (setup, breakdown, material shortage, other) |
| Scrap | Why output was rejected (tolerance over, surface defect, other) |
| Quality | Quality holds and inspections |
| Setup | Planned changeovers |
| Other | Anything that does not fit above |

Procedures: list (`GET /api/reason-codes`, paged), create
(`POST /api/reason-codes` → `201`), edit
(`PUT /api/reason-codes/{id}` → `204`), delete
(`DELETE /api/reason-codes/{id}` → `204`). Duplicate codes within the
tenant return `409`; a route/body ID mismatch returns `400`.

## Scrap events

| Field | Required | Rules |
|-------|----------|-------|
| MachineId | Yes | The Work Center where the loss happened |
| ReasonCodeId | Yes | A code from the dictionary, normally of the Scrap category |
| Quantity | Yes | Greater than zero |
| ReportedAt | Yes | Cannot be in the future |
| ReportedByOperatorId | No | The operator reporting the loss |
| ProductionOrderId | No | Link to a Released or InProgress order when the scrap belongs to one |
| Notes | No | Free text |

Procedures:

- **Report:** open `/production/scrap`, create with machine, reason,
  quantity and time, save. API: `POST /api/scrap-events` → `201`.
- **Edit / delete:** change any field
  (`PUT /api/scrap-events/{id}` → `204`) or remove the event
  (`DELETE /api/scrap-events/{id}` → `204`).

Linking to an order is optional, but a linked order must be Released
or InProgress (`400` otherwise); Planned, Completed and Closed orders
cannot take scrap links. Unknown order IDs return `404`.

## Downtime events

| Field | Required | Rules |
|-------|----------|-------|
| MachineId | Yes | The Work Center that stopped |
| ReasonCodeId | Yes | A code from the dictionary, normally of the Downtime category |
| StartedAt | Yes | Cannot be in the future |
| EndedAt | On close | Set when closing; cannot be earlier than the start |
| ReportedByOperatorId | No | The operator reporting the stop |
| ProductionOrderId | No | Link to a Released or InProgress order when the stop interrupts one |
| Notes | No | Free text |

Procedures:

- **Start:** open `/production/downtime`, start with machine, reason
  and start time, save. API: `POST /api/downtime-events` → `201`.
  A Work Center can only have one open Downtime event: starting a
  second one returns `409`.
- **Close:** close the open event with an end time (defaults to now).
  API: `POST /api/downtime-events/{id}/close` → `200` with the closed
  event. Closing an already-closed event returns `409`; an end time
  earlier than the start returns `400`.
- **Edit / delete:** change fields of an event
  (`PUT /api/downtime-events/{id}` → `204`) or remove it
  (`DELETE /api/downtime-events/{id}` → `204`).

The same order-link rule as scrap applies: only Released or
InProgress orders accept the link.

## Tenant isolation

Reason codes, scrap events and Downtime events are tenant-scoped.
Each tenant keeps its own dictionary; cross-tenant IDs resolve as
`404` and never leak labels into another tenant's loss reports.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without the required write permission | `403` |
| Missing machine, reason or time; non-positive quantity | `400` naming the field |
| Timestamp in the future | `400` |
| Link to a Planned, Completed or Closed order | `400` |
| Second open Downtime event on the same Work Center | `409` |
| Close of an already-closed Downtime event | `409` |
| End time earlier than start time | `400` |
| Duplicate Reason code within the tenant | `409` |
| Route ID differs from body ID on edit | `400` |
| Unknown ID | `404` |

## Next steps

- Signal the floor while the line is down: [Operator panel](14-operator-panel.md) (Andon actions).
- See Downtime in the shift record: [Shift handover](13-shift-handover.md).
- Report good output: [Confirmations](09-confirmations.md).
