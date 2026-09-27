# Work Centers, operators, rosters and skills

This chapter covers the master data that describes *who and what does
the work*: Machine (the Work Center), Operator, the shift roster
(Shift plus OperatorShiftAssignment), and Skill. Routing resource
requirements and production scheduling reference all of them, so finish
this chapter before engineering recipes.

All views share the standard Configuration page pattern and the same
authorization: reads need only a signed-in user, every create, edit and
delete needs the `configuration.write` permission (`403` without it).

## Machines (Work Centers)

Route `/configuration/machines` — API `/api/machines`.
A machine is the system's Work Center: the resource where routing
operations happen. It optionally belongs to a department and carries
planning factors plus a work calendar.

| Field | Required | Rules |
|-------|----------|-------|
| Code | Yes | Non-empty, unique per tenant |
| Name | Yes | Non-empty |
| Description | No | Free text |
| IsActive | Yes | Inactive machines stay in history but are hidden from pickers and planning |
| DepartmentId | No | Owning department (see [Warehouses and departments](03-master-data-organisation.md)) |
| Capacity | No | Nominal capacity used by planning |
| EfficiencyFactor | No | Multiplier applied to planned times |
| SyncId | No | External ERP key |

Procedures: list (`GET /api/machines`, paged), create
(`POST /api/machines` → `201`), edit (`PUT /api/machines/{id}` →
`204`), delete (`DELETE /api/machines/{id}` → `204`). A machine still
referenced by a calendar entry, a resource requirement, telemetry tags
or history cannot be deleted (`409`). Route/body ID mismatches return
`400`, unknown IDs `404`, duplicate codes `409`.

### Work-center calendar and shifts

Route `/configuration/shifts` — APIs `/api/shifts` and the calendar
endpoints. Shifts are the dictionary of working time windows (name,
start/end, days) and each machine owns calendar entries built from
those shifts: planned working time, breaks and exceptions.

1. Create the shifts first (code, name, start and end time, active
   flag) in the Shifts view.
2. Open the machine, add calendar entries that bind a shift to a date
   range, and mark exceptions (holidays, maintenance windows) as
   non-working.
3. The dispatch board and scheduling read the calendar to decide
   whether a Work Center is staffed at a given time and to flag orders
   planned into uncovered shifts (slice 2).

## Operators

Route `/configuration/operators` — API `/api/operators`.
An Operator is a shopfloor worker identified by code or RFID tag. It is
master data, not a login account: do not confuse it with the User that
signs into the UI (see [Getting started](01-getting-started.md)).

| Field | Required | Rules |
|-------|----------|-------|
| Identifier | Yes | Code or RFID tag, unique per tenant — this is what the shopfloor scan matches |
| FirstName | Yes | Non-empty |
| LastName | Yes | Non-empty |
| RatePerHour | Yes | Labour rate used by costing |
| DepartmentId | No | Owning department |
| UserId | Yes | Linked login account for audit attribution |

Procedures: list (`GET /api/operators`, paged, searchable by
identifier and name), create (`POST /api/operators` → `201`), edit
(`PUT /api/operators/{id}` → `204`), delete
(`DELETE /api/operators/{id}` → `204`). Duplicate identifiers return
`409`; operators with roster entries or confirmation history cannot be
deleted (`409`).

### Shift roster (OperatorShiftAssignment)

The roster answers "which operators work which shift". It links an
operator to a shift for a date range and is maintained in the Operators
view (roster section) — API `/api/operator-shift-assignments`.

1. Pick the operator, then add an assignment: shift plus valid-from
   date, optionally valid-to.
2. Overlapping assignments for the same operator are rejected
   (`409`); correct the dates instead of double-booking.
3. Remove or end-date assignments when rosters change; history keeps
   expired rows for traceability.

## Skills

Route `/configuration/skills` — API `/api/skills`.
A skill is a named capability (for example "TIG welding", "forklift
licence"). Skills qualify operators and machines for operations that
require them.

| Field | Required | Rules |
|-------|----------|-------|
| Code | Yes | Non-empty, unique per tenant |
| Name | Yes | Non-empty |
| Description | No | Free text |
| IsActive | Yes | Inactive skills stay in history but are hidden from pickers |

Procedures: list (`GET /api/skills`, paged), create
(`POST /api/skills` → `201`), edit (`PUT /api/skills/{id}` → `204`),
delete (`DELETE /api/skills/{id}` → `204`). Duplicate codes return
`409`; a skill still required by a routing operation cannot be deleted
(`409`).

## Shared error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `configuration.write` (writes) | `403` |
| Missing required field | `400` naming the field |
| Duplicate code/identifier within the tenant | `409` |
| Route ID differs from body ID on edit | `400` |
| Unknown ID | `404` |

## Next steps

- Engineer how products are made on these resources: [Recipes and versions](05-recipes.md).
- Revisit what is made: [Products, groups, units and scan lookup](02-master-data-products.md).
