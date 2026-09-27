# Module boundaries

One row per deployable module: what it owns, which projects implement it,
how it plugs into the Gateway, and which controllers it serves. Layer rules
are defined in [Architecture](architecture.md); tenant and auth rules in
[Multitenancy](multitenancy.md) and [Authentication and RBAC](auth-rbac.md).

## Modules at a glance

| Module | Responsibility | Projects |
|---|---|---|
| Gateway | Host process only: `Program.cs` pipeline, `ModuleLoader`, Swagger, health probes, no MediatR requests of its own | `AsistOff.MES.Gateway` |
| Multitenancy | Tenant registry and ambient tenant context: `Tenant` CRUD/provisioning, `TenantValidationBehavior`, `ITenantRequest` / `IAllowAnonymousRequest` markers | `AsistOff.MES.Multitenancy`, `AsistOff.MES.Multitenancy.Contracts` |
| Users | Identity and access: sign-in/out, refresh rotation, users, roles, permissions | `AsistOff.MES.Users.Core`, `AsistOff.MES.Users.Application`, `AsistOff.MES.Users.Infrastructure`, `AsistOff.MES.Users.Api` |
| Configuration | Master data and plant model: products, units, warehouses and stock, departments, machines (Work Centers), operators and rosters, skills, shifts and calendars, reason codes, maintenance | `AsistOff.MES.Configuration.Core`, `AsistOff.MES.Configuration.Application`, `AsistOff.MES.Configuration.Infrastructure`, `AsistOff.MES.Configuration.Api` |
| Production | Engineering and execution: recipes, versions, routing, BOM, Production Orders, Confirmations, lots and Genealogy, scrap/downtime, OEE, SPC, Kanban, telemetry, Andon, maintenance execution views, scheduling | `AsistOff.MES.Production.Core`, `AsistOff.MES.Production.Application`, `AsistOff.MES.Production.Infrastructure`, `AsistOff.MES.Production.Api` |
| Attachments | Polymorphic file attachments linked to any module entity | `AsistOff.MES.Attachments.Core`, `AsistOff.MES.Attachments.Application`, `AsistOff.MES.Attachments.Infrastructure`, `AsistOff.MES.Attachments.Api` |
| Shared | Cross-cutting infrastructure, no business entities: `DefaultContext`, interceptors, pipeline behaviors, auth, outbox, health, correlation, observability | `AsistOff.MES.Shared.Abstractions`, `AsistOff.MES.Shared.Infrastructure` |
| Web | Vue 3 SPA operator and back-office UI (slice 2) | `AsistOff.MES.Web` |

## Gateway

Owns the host and nothing else: `Program.cs` (full pipeline, see
[Architecture](architecture.md)), `ModuleLoader` (discovers `IModule`
implementations from `AsistOff.MES.*.dll`), `DependencyInjection`
(`AddPresentation`: controllers, Swagger with Bearer scheme,
`CustomProblemDetailsFactory`), plus `Controllers`, `Errors`,
`Protection`, and health wiring. It defines no entities and no MediatR
requests; the errors controller is the only controller.

## Multitenancy

- **Owns:** the `Tenant` entity (shared, not tenant-scoped), tenant
  provisioning (`CreateTenantCommand`), single-tenant lookup
  (`GetTenantQuery`), the ambient tenant context (`TenantContext`
  implementing `ITenantContext` / `ICurrentTenantAccessor`), and
  `TenantValidationBehavior`.
- **Markers:** `ITenantRequest` / `ITenantRequest<TResponse>` and
  `IAllowAnonymousRequest` live in `Multitenancy.Contracts/Interfaces` so
  every module can reference them without a module-to-module dependency.
- **Controller:** `TenantsController`.
- **Boundary:** no other module reads `Tenant` rows directly for
  authorization; they read the ambient tenant id and the JWT claims issued
  at sign-in.

## Users

- **Owns:** `User` (tenant-scoped), `Role`, `Permission`, `RolePermission`,
  `UserRole`, `RefreshToken` (tenant-scoped, hash-persisted), sign-in /
  refresh / sign-out, user CRUD, role CRUD and assignment, permission browse.
- **Controllers:** `AuthenticationController`, `RolesController`,
  `PermissionsController`.
- **Boundary:** the sole writer of auth claims and the sole reader of
  credentials. Other modules consume `permissions` claims and
  `ICurrentPermissionsAccessor`; they never query user tables. The two
  pre-auth repository methods (`UsersRepository.GetForAuthenticationAsync`,
  `RefreshTokensRepository.GetByHashIgnoringQueryFiltersAsync`) are the only
  justified `IgnoreQueryFilters` bypasses outside anonymous tenant lookup
  (see [Multitenancy](multitenancy.md)).

## Configuration

- **Owns:** all master data in `Configuration.Core/Entities`:
  `Product`, `ProductGroup`, `ProductPrice`, `MeasureUnit`,
  `ProductMeasureUnit`, `Warehouse`, `StockMovement`, `MaterialReservation`,
  `Department`, `Machine` (the Work Center, with capacity and efficiency
  factor), `WorkCenterCalendar`, `WorkCenterCalendarEntry`, `Shift`,
  `Operator`, `OperatorShiftAssignment`, `Skill`, `ReasonCode`,
  `MaintenancePlan`, `MaintenanceWorkOrder`. See
  [Domain model](domain-model.md) for the entity table.
- **Controllers:** `ProductsController`, `ProductGroupsController`,
  `MeasureUnitsController`, `WarehousesController`, `StockMovementsController`,
  `StockOnHandController`, `MaterialReservationsController`,
  `DepartmentsController`, `MachinesController`, `OperatorsController`,
  `OperatorShiftAssignmentsController`, `ShiftsController`, `SkillsController`,
  `ReasonCodesController`, `MaintenancePlansController`,
  `MaintenanceWorkOrdersController`.
- **Boundary:** Configuration never references Production entities.
  Production reads Configuration rows (products, BOM warehouses, machines)
  through its own queries against the shared database; the RW/PW ledger
  (`StockMovement`) is written by Production confirmations and read back as
  Configuration data.

## Production

- **Owns:** engineering and execution in `Production.Core/Entities`:
  `Recipe`, `RecipeVersion`, `OperationNode`, `OperationDependency`,
  `OperationTemplate`, `OperationOutput`, `BomItem`, `ResourceRequirement`,
  `ProductionOrder`, `ProductionConfirmation`, `Lot`, `LotGenealogyEdge`,
  `ScrapEvent`, `DowntimeEvent`, `AndonSignal`, `KanbanLoop`, `KanbanCard`,
  `SpcCharacteristic`, `SpcMeasurement`, `MachineTelemetryTag`,
  `TelemetryReading`, `OpcUaConnection`, `ScheduledOperation`,
  `ShiftHandover`. See [Domain model](domain-model.md) for lifecycles.
- **Controllers:** `RecipesController`, `RecipeVersionsController`,
  `OperationsController`, `OperationTemplatesController`,
  `ProductionOrdersController`, `ProductionConfirmationsController`,
  `LotsController`, `LotGenealogyController`, `ScrapEventsController`,
  `DowntimeEventsController`, `AndonSignalsController`, `KanbanController`,
  `SpcCharacteristicsController`, `SpcMeasurementsController`,
  `TelemetryReadingsController`, `MachineTelemetryTagsController`,
  `OpcUaConnectionsController`, `OeeController`, `ReliabilityController`,
  `ScheduleController` (dispatch board, Gantt, operator shift queue),
  `ShiftHandoversController`, `AuditEventsController`.
- **Boundary:** Production owns the only cross-entity transaction in the
  system — the atomic confirmation fan-out (confirmation + RW/PW movements
  + Genealogy edges + order totals in one transaction). It consumes
  Configuration master data but never writes it, except for the
  confirmation-posted `StockMovement` ledger lines defined by the shared
  `MovementCalculator` contract.

## Attachments

- **Owns:** the polymorphic `Attachment` entity plus upload/download/list/
  delete with MIME allowlist, sniffed-type verification, and safe download
  disposition.
- **Controller:** `AttachmentsController`.
- **Boundary:** attachments link to any module entity by owner key and verify
  ownership through `EfAttachmentOwnerVerifier`; they never embed
  module-specific tables. List and download require `attachments.read` plus
  the owner-module scope permission and are therefore not on the anonymous
  or read allowlists.

## What lives where (quick router)

| I need to change… | I work in… |
|---|---|
| Tenant signup, tenant lookup | Multitenancy |
| Login, JWT, roles, permissions | Users |
| Products, warehouses, machines, operators, shifts, reason codes | Configuration |
| Recipes, orders, confirmations, lots, OEE, SPC, Kanban, telemetry | Production |
| File upload/download | Attachments |
| Query filter, interceptors, pipeline behaviors, health, correlation | Shared |
| HTTP pipeline order, CORS, rate limiting, boot gate | Gateway `Program.cs` |
