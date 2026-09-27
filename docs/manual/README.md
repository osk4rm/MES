# AsistOff MES — End-User Manual (Slices 1–2 of 3)

Slice 1 covers getting started, master data, and recipes engineering.
Slice 2 (this slice) covers production execution: orders, dispatch,
confirmations, scrap and Downtime, lots and genealogy, the Gantt
Harmonogram, shift handover and the Operator panel. Slice 3
(analytics, integration, administration and operations) follows
separately.

## How to use this manual

Read the chapters in order the first time: sign up and log in, set up
master data, then engineer recipes. Afterwards use any chapter as a
standalone reference — every procedure lists its route, its API endpoint,
the permission it needs, and its error cases.

## Chapters

### Getting started

- [Getting started](01-getting-started.md) — tenant signup, login with
  BFF cookies, roles and permissions overview, navigation map.

### Master data

- [Products, groups, units and scan lookup](02-master-data-products.md) —
  Product, ProductGroup, MeasureUnit and ProductMeasureUnit, EAN/GTIN
  scan lookup.
- [Warehouses and departments](03-master-data-organisation.md) —
  Warehouse, Department.
- [Work Centers, operators, rosters and skills](04-master-data-resources.md) —
  Machine as a Work Center with calendar and shifts, Operator with the
  shift roster, Skill.

### Recipes engineering

- [Recipes and versions](05-recipes.md) — Recipe, RecipeVersion
  creation and the release flow.
- [Routing, BOM and resources](06-recipes-routing-bom.md) —
  OperationNode and OperationDependency routing, OperationTemplate,
  OperationOutput, BomItem, ResourceRequirement.

### Production execution

- [Production Orders](07-production-orders.md) — order lifecycle
  Planned → Released → InProgress → Completed → Closed, priorities,
  due dates, reservations on release.
- [Dispatch board](08-dispatch-board.md) — shift-aware
  Released-orders board with uncovered-shift flags (and the
  Harmonogram name note).
- [Confirmations with RW/PW](09-confirmations.md) — operator
  confirmations with produced/consumed lots, RW/PW preview and stock
  effect, complete/close.
- [Scrap and Downtime with Reason codes](10-scrap-downtime.md) —
  scrap capture, Downtime start/close, Reason code dictionary.
- [Lots and genealogy](11-lots-genealogy.md) — Lot registry, status
  lifecycle, genealogy edges, upstream/downstream traces.
- [Gantt schedule (Harmonogram)](12-gantt-schedule.md) — time-phased
  schedule read-model, reschedule moves, leveling conflicts.
- [Shift handover](13-shift-handover.md) — handover context snapshot
  and the append-only logbook.
- [Operator panel (shift queue)](14-operator-panel.md) — operator
  shift queue with claim/confirm/scrap/downtime/Andon actions.

## Conventions used in every chapter

- **Route** is the frontend path (for example `/configuration/products`).
- **API** is the backend endpoint (for example `GET /api/products`).
- **Permission** is the RBAC permission a write operation requires.
  All list and detail reads are open to any authenticated user; every
  create, edit and delete states its required permission.
- **Tenant isolation:** every company works inside its own Tenant. Data
  created by one tenant is invisible to every other tenant. There is no
  cross-tenant sharing in this slice.
- **Error codes** use the shared contract: `401` not signed in,
  `403` signed in but missing the required permission, `400` validation
  failure or route/body ID mismatch, `404` unknown ID, `409` uniqueness
  or state conflict.
