# MES Feature Tracker

Canonical, persistent map of what AsistOff MES can do, what is proposed, and
what is missing. It exists so agents do **not** rescan the whole repository on
every run.

## Contract (agents, read this)

- **mes-researcher** — reads this file **first** and is its only **appender**: it
  adds capabilities that are missing entirely as new `gap` rows, and never
  modifies, reorders, or deletes existing rows. It does not create GitHub
  issues.
- **mes-analyst** — reads this file **first**. Each run it either adopts the
  oldest unlabeled open issue, or takes the first `gap` row whose dependencies
  are `done`, and turns it into an `ai:implement`-labeled issue. It does not
  edit this file.
- **mes-tracker** — the single writer for statuses and work items. Reconciles
  this file with GitHub (issues / PRs) and the codebase, and publishes the
  update via a PR on branch `ai/tracker-sync`.
- **mes-implementer / mes-reviewer** — do **not** edit this file; keep PRs
  minimal. The tracker is reconciled out of band.
- Work items live in GitHub; this file stores only their identifiers.

## Status legend

| Status | Meaning |
|---|---|
| `done` | Implemented and merged. |
| `partial` | Exists but incomplete versus the glossary definition. |
| `proposed` | An open GitHub issue exists. |
| `in-progress` | An open PR implements it. |
| `gap` | Missing; no work item yet. |

## Dependencies

A row that needs another capability first names it in the `Notes` column,
prefixed with `depends on` (e.g. `depends on #80`, `depends on Lot / Serial`).
`mes-analyst` will not pick a `gap` row until every named dependency is `done`.

## Platform / cross-cutting

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| Multi-tenancy (tenant isolation) | Tenant | Multitenancy | done | — | ADR-0002; `ISaasy` + global query filter |
| Authentication / JWT | — | Users, Auth | partial | — | interim permission resolution; RBAC pending (ADR-0003) |
| Polymorphic attachments | — | Attachments | done | — | `Attachment` |

## Configuration (master data)

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| Products | — | Configuration | done | — | `Product`, `ProductGroup`, `ProductPrice` |
| Units of measure | Unit of Measure | Configuration | done | — | `MeasureUnit`, `ProductMeasureUnit` |
| Warehouses | RW / PW (future) | Configuration | done | — | `Warehouse`; stock movements not implemented |
| Departments | — | Configuration | done | — | `Department` |
| Machines / resources | Work Center | Configuration | partial | — | `Machine`; no calendar / capacity / efficiency |
| Operators | Operator | Configuration | done | — | `Operator` (code / RFID) |
| Skills | — | Configuration | done | — | `Skill` |

## Production engineering

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| Recipes | BOM / Receptura | Production | done | — | `Recipe` |
| Recipe versions + release | — | Production | done | — | `RecipeVersion`; release flow |
| Operations / routing | Operation / Routing | Production | done | — | `OperationNode`, `OperationDependency` |
| Operation templates | — | Production | done | — | `OperationTemplate` |
| Operation outputs | — | Production | done | — | `OperationOutput` |
| BOM items | BOM | Production | done | — | `BomItem` |
| Resource requirements | Work Center | Production | done | — | `ResourceRequirement` |

## Production execution

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| Production Order | Production Order | Production | proposed | #80 | header + `Planned -> Released` only |
| Operator confirmations (RW / PW) | Confirmation | Production | gap | — | depends on #80 |
| Scrap capture | Quality | Production | gap | — | |
| Downtime capture | Downtime | Production | gap | — | |
| Reason codes | Reason code | Configuration | proposed | #81 | dictionary only; no event capture |
| Work-center calendar / shifts | Shift | Configuration | gap | — | `Machine` has no calendar |
| Lot / Serial tracking | Lot / Serial | Production | gap | — | |
| Genealogy / traceability | Genealogy | Production | gap | — | depends on lot/serial + confirmations |

## Analytics / integration

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| OEE | OEE | — | gap | — | needs execution + downtime data |
| Andon | Andon | — | gap | — | |
| SPC | SPC | — | gap | — | |
| CMMS | CMMS | — | gap | — | |
| OPC UA / SCADA telemetry | OPC UA | — | gap | — | |
| Kanban | Kanban | — | gap | — | |

_Last reconciled: 2026-09-23 — initial seed from repository scan._
