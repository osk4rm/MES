# AsistOff MES — End-User Manual

Slice 1 covers getting started, master data, and recipes engineering.
Slice 2 covers production execution: orders, dispatch,
confirmations, scrap and Downtime, lots and genealogy, the Gantt
Harmonogram, shift handover and the Operator panel. Slice 3 (this
slice) covers analytics, integration, and administration and
operations: OEE, SPC, Andon, reliability, OPC UA telemetry,
Kanban, maintenance, tenants and access, attachments and audit,
health and observability, deploy and backup.

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

### Analytics

- [OEE dashboard](15-oee-dashboard.md) — Quality, Availability and
  Performance factors, trend buckets, loss Pareto.
- [SPC](16-spc-quality.md) — characteristic dictionary,
  append-only measurements, control chart with Western Electric
  rules 1–4.
- [Andon signals](17-andon.md) — abnormal-condition signals,
  Active → Acknowledged → Resolved lifecycle, board work.
- [Reliability](18-reliability.md) — MTBF/MTTR snapshot, trend
  and fleet comparison.

### Integration

- [OPC UA telemetry](19-opcua-telemetry.md) — tag dictionary,
  readings log with CSV export, stale dashboard and simulator,
  connection registry with polling and shape-test.
- [Kanban pull](20-kanban.md) — loops, card registry, pull
  transitions with the WIP limit, board UI.
- [CMMS and preventive maintenance](21-cmms-maintenance.md) —
  corrective work orders with board UI, preventive plans with
  time/meter schedules, due state, evaluation auto-raise and
  manual raise.

### Administration and operations

- [Tenants, users and roles](22-admin-tenants-users-roles.md) —
  tenant signup with minimal projection, users and auth with JWT,
  BFF httpOnly cookies and refresh rotation, roles and
  permissions, back-office roles UI.
- [Attachments and audit trail](23-attachments-audit.md) —
  attachments with upload validation and safe download, actor
  plus append-only audit history.
- [Health, correlation and observability](24-observability-health.md) —
  health live/ready probes, correlation ID end-to-end,
  OpenTelemetry metrics plus traces.
- [Deploy, backup and operations](25-deploy-backup-ops.md) —
  production deploy plus backup safety, container and runtime
  hardening notes, demo data seed script usage.

## Conventions used in every chapter

- **Route** is the frontend path (for example `/configuration/products`).
- **API** is the backend endpoint (for example `GET /api/products`).
- **Permission** is the RBAC permission a write operation requires.
  All list and detail reads are open to any authenticated user; every
  create, edit and delete states its required permission.
- **Tenant isolation:** every company works inside its own Tenant. Data
  created by one tenant is invisible to every other tenant. There is no
  cross-tenant sharing in this manual.
- **Error codes** use the shared contract: `401` not signed in,
  `403` signed in but missing the required permission, `400` validation
  failure or route/body ID mismatch, `404` unknown ID, `409` uniqueness
  or state conflict.
