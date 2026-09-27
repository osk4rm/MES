# Domain model

Key entities per module with identifiers, tenant scope, and lifecycles, as
implemented by the `*.Core/Entities` classes and the
`*.Infrastructure/Configurations` EF mappings. Identifiers are `Guid` primary
keys unless noted; entities with a human code additionally carry a
`(TenantId, Code)` unique constraint. All tenant-scoped entities implement
`ISaasy`; isolation mechanics are described in
[Multitenancy](multitenancy.md).

## Multitenancy: tenant registry

| Entity | Identifier | Tenant scope | Lifecycle / notes |
|---|---|---|---|
| `Tenant` | `Id` (Guid) | shared (the registry itself) | Created by `CreateTenantCommand` (anonymous bootstrap); `IsActive=false` blocks sign-in. `ContactEmail`, `DisplayName`, `Settings` (jsonb). Anonymous lookup returns only the minimal public projection (id, name, isActive). |

## Users: identity and access

| Entity | Identifier | Tenant scope | Lifecycle / notes |
|---|---|---|---|
| `User` | `Id` (Guid), `Email` unique per tenant | tenant-scoped | Active account; `IsTenantAdmin` drives the interim permission set. Created with the tenant (first admin) or by user CRUD. |
| `Role` | `Id` (Guid), `Code` unique per tenant | tenant-scoped | Seeded `tenant_admin` / `user`; custom roles via role CRUD. |
| `Permission` | `Id` (Guid), `Code` from `RbacDefaults` | tenant-scoped | Fixed catalog (`users`, `users.read`, `configuration.write`, …); no string literals outside `RbacDefaults`. |
| `RolePermission` | composite (`RoleId`, `PermissionId`) | tenant-scoped | Assignment of catalog codes to roles. |
| `UserRole` | composite (`UserId`, `RoleId`) | tenant-scoped | Assignment of roles to users. |
| `RefreshToken` | `Id` (Guid), lookup by SHA-256 `TokenHash` | tenant-scoped | `active → rotated → revoked`; reuse of a rotated token revokes the whole family (`FamilyId`). Only the hash is persisted. |

## Configuration: master data and plant model

| Entity | Identifier | Tenant scope | Lifecycle / notes |
|---|---|---|---|
| `Product` | `Id`, `Code` unique per tenant | tenant-scoped | Master record; optional `Ean` (tenant-unique) and barcodes for shopfloor scan lookup (`GetProductByScanRequest`: Code → Ean → Barcode). |
| `ProductGroup` | `Id`, `Code` unique per tenant | tenant-scoped | Grouping dictionary for products. |
| `ProductPrice` | `Id` | tenant-scoped | Price rows per product. |
| `MeasureUnit` | `Id`, `Code` unique per tenant | tenant-scoped | Unit-of-measure dictionary (pieces, kg, m, l). |
| `ProductMeasureUnit` | composite (`ProductId`, `MeasureUnitId`) | tenant-scoped | Alternate units with conversion factors against the base unit. |
| `Warehouse` | `Id`, `Code` unique per tenant | tenant-scoped | Storage location; planning hint for RW issues. |
| `StockMovement` | `Id` | tenant-scoped | Persisted RW/PW ledger, written only by Production confirmations (see below). `MovementType` is `PW` (internal receipt, finished goods) or `RW` (internal issue, materials). Cascade-deleted with its confirmation; corrections are delete plus re-create. |
| `MaterialReservation` | `Id` | tenant-scoped | Soft allocation created on order release, relieved on RW confirmation. |
| `Department` | `Id`, `Code` unique per tenant | tenant-scoped | Organizational dictionary. |
| `Machine` | `Id`, `Code` unique per tenant | tenant-scoped | The Work Center: machine, bench, cell, or logical group. Capacity, efficiency factor, calendar. |
| `WorkCenterCalendar` | `Id` | tenant-scoped | Calendar header per Work Center. |
| `WorkCenterCalendarEntry` | `Id` | tenant-scoped | Time windows (shifts) during which the Work Center is planned to run. |
| `Shift` | `Id`, `Code` unique per tenant | tenant-scoped | Named working window (for example 06:00–14:00). |
| `Operator` | `Id`, `Code`/RFID unique per tenant | tenant-scoped | Shopfloor person; distinct from the back-office `User`. |
| `OperatorShiftAssignment` | `Id` | tenant-scoped | Roster: which operators work which shift. |
| `Skill` | `Id`, `Code` unique per tenant | tenant-scoped | Capability tag matched against operation requirements. |
| `ReasonCode` | `Id`, `Code` unique per tenant | tenant-scoped | Controlled vocabulary for scrap and downtime causes (for example tolerance, breakdown). |
| `MaintenancePlan` | `Id` | tenant-scoped | Time/meter-based preventive plan with due-state evaluation. |
| `MaintenanceWorkOrder` | `Id` | tenant-scoped | Corrective work order, including auto-raised rows from due plans. |

## Production: engineering (recipes)

| Entity | Identifier | Tenant scope | Lifecycle / notes |
|---|---|---|---|
| `Recipe` | `Id`, `Code` unique per tenant | tenant-scoped | Header for one producible product. |
| `RecipeVersion` | `Id`, `Version` per recipe | tenant-scoped | Draft → Released. Only a released version can be referenced by a Production Order. |
| `OperationNode` | `Id` | tenant-scoped | One routing step (cutting, welding, packaging) on a Work Center with setup and run time per unit. |
| `OperationDependency` | composite (predecessor, successor) | tenant-scoped | Routing graph edges (`FinishToStart` and related PDM types). |
| `OperationTemplate` | `Id`, `Code` unique per tenant | tenant-scoped | Reusable operation blueprint stamped into routings. |
| `OperationOutput` | `Id` | tenant-scoped | Declared output of an operation step. |
| `BomItem` | `Id` | tenant-scoped | One BOM line: component product, quantity type (`PerUnit` / `PerBatch` / `PerOperationRun`), consumption timing, warehouse hint. |
| `ResourceRequirement` | `Id` | tenant-scoped | Work Center / skill requirement of an operation step. |

## Production: execution

| Entity | Identifier | Tenant scope | Lifecycle / notes |
|---|---|---|---|
| `ProductionOrder` | `Id`, `Code` unique per tenant | tenant-scoped | **Planned → Released → InProgress → Completed → Closed** (`ProductionOrderStatus`). Planned and produced/scrapped totals derive from confirmations. Carries the `xmin` optimistic-concurrency token: concurrent lifecycle writes return 409 and the client retries. |
| `ProductionConfirmation` | `Id` | tenant-scoped | Operator report of work done: good quantity, scrap quantity, time, machine, operator. Posting runs the atomic fan-out: confirmation row + PW receipt line + per-BOM-item RW issue lines + Genealogy edges + order totals, in one transaction. |
| `Lot` | `Id`, `Code` unique per tenant | tenant-scoped | Batch registry (`Available`, `OnHold`, `Consumed`, `Scrapped`, `Expired`). Produced lots (finished goods) and consumed lots (components) are the same table. |
| `LotGenealogyEdge` | `Id` | tenant-scoped | One consumed-lot → produced-lot link per consumed lot entry on a confirmation, under an order and machine. Same-lot self links rejected; corrections are delete plus re-record; upstream/downstream traceability queries walk these edges. |
| `ScrapEvent` | `Id` | tenant-scoped | Scrap quantity with a `ReasonCode` link. |
| `DowntimeEvent` | `Id` | tenant-scoped | Downtime window with a `ReasonCode` link; open while `EndedAt` is null, closed once set. |
| `ScheduledOperation` | `Id` | tenant-scoped | Time-phased schedule rows behind the dispatch board and Gantt views. |
| `ShiftHandover` | `Id` | tenant-scoped | Persisted logbook entries with context snapshot and notes. |
| `AndonSignal` | `Id` | tenant-scoped | `Active → Acknowledged → Resolved`, categorized Downtime / Quality / Material / Other. |
| `KanbanLoop` | `Id`, `Code` unique per tenant | tenant-scoped | Pull-loop dictionary with WIP limits. |
| `KanbanCard` | `Id` | tenant-scoped | Card registry (`Full` / `Empty` / `Ordered`) transitioned within its loop. |
| `SpcCharacteristic` | `Id` | tenant-scoped | Measured characteristic dictionary. |
| `SpcMeasurement` | `Id` | tenant-scoped | Single measurement with out-of-control evaluation (Western Electric rules). |
| `MachineTelemetryTag` | `Id` | tenant-scoped | OPC UA / simulator tag dictionary per Work Center. |
| `TelemetryReading` | `Id` | tenant-scoped | Timestamped tag values feeding status, trend, and OEE inputs. |
| `OpcUaConnection` | `Id` | tenant-scoped | Connection registry (endpoint, security policy) with polled status; the connection test validates shape only. |

## Attachments

| Entity | Identifier | Tenant scope | Lifecycle / notes |
|---|---|---|---|
| `Attachment` | `Id` | tenant-scoped | Polymorphic file row keyed by owner (module + entity id). Upload → stored; list/download gated by `attachments.read` plus owner-module scope; delete gated by write permission. |

## Lifecycle diagrams

Production Order (the central lifecycle):

```text
Planned ---> Released ---> InProgress ---> Completed ---> Closed
   |              |               |              |
   | reserve      | first         | last good    | terminal,
   | materials    | confirmation  | quantity     | read-only
   |              | starts work   | completes it |
```

Confirmation fan-out (one transaction, delete plus re-create on correction):

```text
ProductionConfirmation
  +-- StockMovement PW (finished-goods receipt, order product)
  +-- StockMovement RW x N (one material issue per released BOM item)
  +-- LotGenealogyEdge x M (one edge per consumed lot entry)
  +-- ProductionOrder totals update (produced / scrapped quantities)
  +-- MaterialReservation relief (RW portion)
```

Lot Genealogy (traceability walks these edges in both directions):

```text
consumed Lot(s) --[edge: qty, order, machine, confirmation]--> produced Lot
upstream trace: produced lot -> all contributing lots + orders + machines
downstream trace: consumed lot -> all finished lots it went into
```
