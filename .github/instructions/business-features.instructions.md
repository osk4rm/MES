---
applyTo: "**"
---

# Business Features – MES Domain Map for AI Agents

Load this file when a task asks about **business behavior**, **implemented MES features**, cross-module workflows, requirement analysis, or when adding a new feature that must fit the product model.

Use this document together with:

- `.github/instructions/architecture.instructions.md` for backend layering and CQRS rules.
- `.github/instructions/api.instructions.md` for controller and endpoint conventions.
- `.github/instructions/frontend.instructions.md` for Vue/UI conventions.
- `.github/instructions/production-recipes.instructions.md` for deeper Production / Recipes implementation details.
- `docs/glossary.md` for MES vocabulary.

## 1. Product Scope and Current Status

AsistOff MES is a multi-tenant Manufacturing Execution System focused on:

1. tenant onboarding and tenant-isolated SaaS operation,
2. user authentication and provisional tenant-admin/user authorization,
3. configuration master data for products, organizational resources, units and reusable production templates,
4. production recipe engineering with versioned routings, operations, BOM, outputs and resource requirements,
5. polymorphic attachments for business objects,
6. a Vue SPA for back-office configuration and recipe editing.

Implemented UI areas:

| Area | Status | Frontend routes |
|------|--------|-----------------|
| Login / registration | Implemented | `/login`, `/register` |
| Dashboard shell | Implemented shell | `/dashboard` |
| Configuration master data | Implemented CRUD views | `/configuration/*` |
| Production recipes | Implemented list/detail/editor | `/production/recipes`, `/production/recipes/:id` |
| Customer orders | Implemented order intake | `/production/customer-orders`, `/production/customer-orders/:id` |
| Production orders | Placeholder | `/production/orders` |
| Schedule | Placeholder | `/schedule` |
| Reports | Placeholder | `/reports` |
| Settings | Placeholder | `/settings` |

Do not describe placeholders as implemented business modules. The UI routes above use `ComingSoonView.vue`.

## 2. Multi-Tenancy Business Model

### Tenant

`Tenant` is the customer organization in the SaaS model.

Key implementation files:

- `AsistOff.MES.Multitenancy/Entity/Tenant.cs`
- `AsistOff.MES.Multitenancy/Entity/TenantSettings.cs`
- `AsistOff.MES.Multitenancy/Controllers/TenantsController.cs`
- `AsistOff.MES.Multitenancy/Requests/Commands/Create/CreateTenantCommandHandler.cs`
- `AsistOff.MES.Multitenancy/Requests/Commands/Create/CreateTenantCommandValidator.cs`

Business behavior:

- Public tenant registration is available through `POST /api/tenants`.
- `GET /api/tenants/{id}` returns tenant details.
- Tenant creation sets `IsActive = true`, stores optional JSON settings as `TenantSettings`, hashes the admin password and publishes `TenantCreatedEvent`.
- The tenant-created flow provisions the tenant admin user through the users module.
- Tenant creation validation requires:
  - `Name` not empty, max 200 characters,
  - valid `ContactEmail`,
  - `Password` not empty, at least 8 characters,
  - `ConfirmPassword` equal to `Password`.

### Tenant isolation

Tenant isolation is a core business invariant:

- Every tenant-scoped entity implements `ISaasy` and has `TenantId`.
- `DefaultContext` applies a global EF Core query filter to all `ISaasy` entities.
- `SaasyEntityInterceptor` assigns the current tenant on insert and prevents cross-tenant writes.
- `TenantValidationBehavior` requires each MediatR request to explicitly be tenant-scoped (`ITenantRequest`) or anonymous (`IAllowAnonymousRequest`).

Agent guidance:

- Never add manual cross-tenant reads as a shortcut.
- Anonymous requests are exceptional and must be explicit.
- When adding new business entities, assume tenant scoping unless there is a strong reason not to.

## 3. Users, Authentication and Authorization

Key files:

- `AsistOff.MES.Users.Core/Entities/User.cs`
- `AsistOff.MES.Users.Api/Controllers/AuthenticationController.cs`
- `AsistOff.MES.Users.Application/Features/Authentication/SignIn/SignInRequestHandler.cs`
- `AsistOff.MES.Web/src/stores/authStore.ts`
- `AsistOff.MES.Web/src/router.ts`

### User

`User` is a system account used to sign in to the back-office application.

Business fields include:

- `Email`
- hashed `Password`
- `FirstName`, `LastName`
- `IsTenantAdmin`
- `TenantId`

Do not confuse `User` with `Operator`. A `User` logs into the system; an `Operator` represents a shop-floor labor resource.

### Sign-in

Endpoint:

- `POST /api/auth/sign-in`

Business behavior:

- Finds the user by email for authentication.
- Verifies the password hash.
- Loads the user's tenant.
- Rejects sign-in when the tenant does not exist or is inactive.
- Returns a JWT containing tenant and permission claims.

Important JWT claims:

| Claim | Meaning |
|-------|---------|
| `tenant_id` | Current tenant GUID |
| `tenant_name` | Tenant technical name |
| `tenant_display_name` | Optional display name |
| `tenant_active` | `"true"` / `"false"` |
| `email` | User email |
| `permissions` | Permission strings |
| role | `tenant_admin` or `user` |

### Current permission model

Authorization is provisional and derived from `User.IsTenantAdmin`.

| Role | Permissions |
|------|-------------|
| Tenant admin | `users`, `users.read`, `users.write`, `configuration`, `configuration.read`, `configuration.write`, `tenant.admin` |
| User | `users.read`, `configuration.read` |

Known limitation: there are no role/permission tables yet. Do not invent a full RBAC model unless the task explicitly asks for it.

### Frontend auth flow

- Public routes: `/login`, `/register`.
- All application-shell routes require a token.
- `authStore` persists auth state in `localStorage`.
- The router redirects unauthenticated users to `/login`.

## 4. Configuration Master Data

The Configuration module stores reference data used by recipes and future production execution.

Key paths:

- Backend entities: `AsistOff.MES.Configuration.Core/Entities/`
- Backend controllers: `AsistOff.MES.Configuration.Api/Controllers/`
- Frontend views: `AsistOff.MES.Web/src/views/configuration/`
- Frontend services: `AsistOff.MES.Web/src/services/*Service.ts`

All implemented configuration areas follow a CRUD/browse pattern in backend and frontend.

### Products and product hierarchy

Entities:

| Entity | Purpose |
|--------|---------|
| `Product` | Material, semi-finished good, finished good or item used in recipes. |
| `ProductGroup` | Hierarchical grouping/category for products. |
| `ProductMeasureUnit` | Product-specific unit conversion relationship. |
| `ProductPrice` | Time-boxed price data with currency and optional quantity threshold. |

Business rules and semantics:

- `Product.Code` and `Product.Name` are the core identifiers.
- `Product.IsActive` controls availability in business lookups.
- `Product.ScanBy` defines whether scanning uses `Code` or `Ean`.
- `ProductGroup.ParentGroupId` enables a hierarchy.
- Product measure units enable conversions between base and alternate units.
- Product prices support validity windows (`ValidFrom`, `ValidTo`), currency and minimum quantity.

API endpoints:

- `GET/POST /api/products`, `GET/PUT/DELETE /api/products/{id}`
- `GET/POST /api/product-groups`, `GET/PUT/DELETE /api/product-groups/{id}`

Frontend routes:

- `/configuration/products`
- `/configuration/product-groups`

### Units and warehouses

Entities:

| Entity | Purpose |
|--------|---------|
| `MeasureUnit` | Unit of measure, type and conversion metadata. |
| `Warehouse` | Inventory location header. |

Business rules and semantics:

- Measure units can reference a base unit through `BaseUnitId`.
- `MeasureUnitType` classifies unit domains.
- Warehouses exist as simple inventory locations; detailed warehouse-bin/location logic is not implemented.

API endpoints:

- `GET/POST /api/measure-units`, `GET/PUT/DELETE /api/measure-units/{id}`
- `GET/POST /api/warehouses`, `GET/PUT /api/warehouses/{id}`

Frontend routes:

- `/configuration/measure-units`
- `/configuration/warehouses`

### Organizational and shop-floor resources

Entities:

| Entity | Purpose |
|--------|---------|
| `Department` | Organizational unit / production area. |
| `Machine` | Physical or logical production equipment. |
| `Operator` | Shop-floor labor resource; may link to a system user. |
| `Skill` | Capability dictionary used by recipe resource requirements. |

Business rules and semantics:

- Machines can belong to departments.
- Operators can belong to departments and optionally link to a `User`.
- Operator hourly rate is stored on `Operator`.
- Skills are a dictionary; recipe `ResourceRequirement.RequiredCapability` stores a human-readable skill display string, not a skill ID.
- Scheduling and skill enforcement are not implemented yet.

API endpoints:

- `GET/POST /api/departments`, `GET/PUT/DELETE /api/departments/{id}`
- `GET/POST /api/machines`, `GET/PUT/DELETE /api/machines/{id}`
- `GET/POST /api/operators`, `GET/PUT/DELETE /api/operators/{id}`
- `GET/POST /api/skills`, `GET/PUT/DELETE /api/skills/{id}`

Frontend routes:

- `/configuration/departments`
- `/configuration/machines`
- `/configuration/operators`
- `/configuration/skills`

### Operation templates

`OperationTemplate` is managed as configuration but belongs to the production domain projects.

Purpose:

- reusable default operation definition,
- prefill operation code/name/type and timing fields when adding operations to recipe versions.

API endpoints:

- `GET/POST /api/operation-templates`
- `GET/PUT/DELETE /api/operation-templates/{id}`

Frontend route:

- `/configuration/operation-templates`

## 5. Production Recipes

Production recipes are the most complete MES business area in the current codebase.

Key paths:

- Domain: `AsistOff.MES.Production.Core/Entities/`
- Enums: `AsistOff.MES.Production.Core/Enums/`
- Application handlers: `AsistOff.MES.Production.Application/Features/`
- API: `AsistOff.MES.Production.Api/Controllers/`
- Frontend services:
  - `AsistOff.MES.Web/src/services/recipeService.ts`
  - `AsistOff.MES.Web/src/services/recipeVersionService.ts`
  - `AsistOff.MES.Web/src/services/operationTemplateService.ts`
- Frontend views/components:
  - `AsistOff.MES.Web/src/views/production/RecipesView.vue`
  - `AsistOff.MES.Web/src/views/production/RecipeDetailView.vue`
  - `AsistOff.MES.Web/src/components/production/RecipeVersionEditor.vue`

### Recipe

`Recipe` is the stable logical identity of a manufacturing definition.

Business fields:

- `Code`, `Name`, `Description`
- `IsActive`
- optional `PrimaryProductId`
- optional `CurrentVersionId`
- collection of `Versions`

Business semantics:

- Recipe content is versioned; operations/BOM/resources live on `RecipeVersion`.
- `PrimaryProductId` is informational and points to the main product when known.
- `CurrentVersionId` points to the currently released version.
- A recipe without a released version cannot be used by future production orders.

API endpoints:

- `GET /api/recipes` with filters such as code/name/isActive.
- `GET /api/recipes/{id}`
- `POST /api/recipes`
- `PUT /api/recipes/{id}`
- `DELETE /api/recipes/{id}`

Frontend routes:

- `/production/recipes`
- `/production/recipes/:id`

### Recipe version

`RecipeVersion` is a versioned snapshot of a recipe's process.

Statuses:

| Status | Value | Meaning |
|--------|-------|---------|
| `Draft` | 1 | Editable work-in-progress version. |
| `Released` | 2 | Approved active version. |
| `Obsolete` | 3 | Superseded historical version. |

Business fields:

- `VersionNumber`
- `Status`
- `ReleasedAt`
- `ValidFrom`, `ValidTo`
- `ChangeNotes`
- operations collection

API endpoints:

- `GET /api/recipe-versions/{id}`
- `POST /api/recipe-versions`
- `POST /api/recipe-versions/clone`
- `PUT /api/recipe-versions/{id}/metadata`
- `POST /api/recipe-versions/{id}/release`
- `DELETE /api/recipe-versions/{id}`

Business rules:

- New versions are drafts.
- Clone creates a new draft from a source version, copying operations, dependencies, BOM items, outputs and resource requirements with new IDs.
- Only a draft can be released.
- A version must contain at least one operation before release.
- Releasing a version demotes any previous released sibling version to obsolete.
- Release updates `Recipe.CurrentVersionId`.
- Non-draft versions should be treated as immutable by business logic and UI.

### Operations and routing graph

`OperationNode` represents a manufacturing step in a recipe version.

Business fields:

- `Code`, `Name`, `Description`
- `OperationType`
- `SortIndex`
- timing fields:
  - `SetupTimeMinutes`
  - `RunTimeMode`
  - `RunTimePerUnitSeconds`
  - `RunTimePerBatchMinutes`
  - `TeardownTimeMinutes`
  - `QueueTimeMinutes`
- `IsOptional`
- `AllowParallelExecution`
- `ExpectedQuantity`

Runtime modes:

| Mode | Value | Meaning |
|------|-------|---------|
| `PerUnitSeconds` | 1 | Runtime scales per produced unit. |
| `PerBatchMinutes` | 2 | Runtime is batch-based. |

API endpoints:

- `POST /api/operations`
- `PUT /api/operations/{id}`
- `DELETE /api/operations/{id}`
- `POST /api/recipe-versions/{versionId}/operations/reorder`

Business semantics:

- Operations form a routing for a recipe version.
- `SortIndex` controls display/order.
- Dependencies create a directed process graph.
- Optional operations can be skipped by future execution logic.
- Parallel execution is a hint for future scheduling.

### Operation dependencies

`OperationDependency` links a predecessor operation to a successor operation.

Dependency types:

| Type | Value | Meaning |
|------|-------|---------|
| `FinishToStart` | 1 | Successor starts after predecessor finishes. |
| `StartToStart` | 2 | Successor may start when predecessor starts. |
| `FinishToFinish` | 3 | Successor finishes with/after predecessor finish. |
| `StartToFinish` | 4 | Successor finishes with/after predecessor start. |

Endpoint:

- `PUT /api/operations/{id}/dependencies`

Business semantics:

- The endpoint sets the dependency collection for one operation.
- `LagMinutes` allows offset between linked operations.
- Keep dependencies within the same recipe version.

### BOM items

`BomItem` is a material input consumed by an operation.

Business fields:

- `ProductId`
- optional `MeasureUnitId`
- `Quantity`
- `QuantityType`
- `ScrapPercentage`
- `IsOptional`
- optional `PreferredWarehouseId`
- `ConsumptionTiming`
- `Notes`
- `SortIndex`

Quantity types:

| Type | Value | Meaning |
|------|-------|---------|
| `PerUnit` | 1 | Quantity per final/product unit. |
| `PerBatch` | 2 | Quantity per batch. |
| `PerOperationRun` | 3 | Fixed per operation execution. |

Consumption timing:

| Timing | Value | Meaning |
|--------|-------|---------|
| `AtStart` | 1 | Consume/reserve at operation start. |
| `Continuous` | 2 | Consume gradually during execution. |
| `AtEnd` | 3 | Consume at operation completion. |

API endpoints:

- `POST /api/operations/{id}/bom-items`
- `PUT /api/bom-items/{id}`
- `DELETE /api/bom-items/{id}`

Business semantics:

- BOM is operation-level, not only recipe-level.
- Preferred warehouse is a planning hint; inventory reservation/execution is not implemented.

### Operation outputs

`OperationOutput` defines what an operation produces.

Business fields:

- `ProductId`
- optional `MeasureUnitId`
- `Quantity`
- `QuantityType`
- `OutputType`
- optional `PreferredWarehouseId`
- `Notes`
- `SortIndex`

Output types:

| Type | Value | Meaning |
|------|-------|---------|
| `MainProduct` | 1 | Main intended output. |
| `Coproduct` | 2 | Co-product of comparable business value. |
| `Byproduct` | 3 | Secondary by-product. |
| `Scrap` | 4 | Scrap/waste output. |
| `Intermediate` | 5 | Intermediate product for later operations. |

API endpoints:

- `POST /api/operations/{id}/outputs`
- `PUT /api/outputs/{id}`
- `DELETE /api/outputs/{id}`

### Resource requirements

`ResourceRequirement` captures preferred labor/machine requirements for an operation.

Business fields:

- optional `PreferredDepartmentId`
- optional `PreferredMachineId`
- `RequiredCapability`
- `RequiredOperatorCount`
- optional `RequiredRole`
- `Notes`

API endpoints:

- `POST /api/operations/{id}/resources`
- `PUT /api/resources/{id}`
- `DELETE /api/resources/{id}`

Business semantics:

- Resource requirements are planning hints for future scheduling/execution.
- They do not assign actual machines or operators.
- Required capability is currently human-readable text selected from the skill dictionary.

### Recipe editor UX

Typical implemented flow:

1. User opens `/production/recipes`.
2. User creates or selects a recipe.
3. Detail view loads versions and selects a version.
4. If there is a released version, creating a new version clones it into a draft; otherwise a blank draft is created.
5. In the editor, the user manages operations, dependencies, BOM, outputs, resource requirements and attachments.
6. When the draft is complete, the user releases it.
7. Release makes the version immutable and sets it as the recipe's current version.


## 5.1 Customer Orders

Customer orders represent demand from customers. They can be imported from ERP/external systems or created manually in MES. Customers can also be maintained manually in Configuration → Customers.

Key paths:

- Domain: `AsistOff.MES.CustomerOrders.Core/Entities/`
- Application handlers: `AsistOff.MES.CustomerOrders.Application/Features/`
- API: `AsistOff.MES.CustomerOrders.Api/Controllers/`
- Frontend service: `AsistOff.MES.Web/src/services/customerOrderService.ts`
- Frontend views:
  - `AsistOff.MES.Web/src/views/customer-orders/CustomerOrdersView.vue`
  - `AsistOff.MES.Web/src/views/customer-orders/CustomerOrderDetailView.vue`
  - `AsistOff.MES.Web/src/views/configuration/CustomersView.vue`

Business behavior:

- `CustomerOrder` stores order header data, source ERP identifiers and a customer snapshot.
- `CustomerOrderLine` stores product demand, quantity, delivery date and a product/unit snapshot.
- One line can be released to production multiple times. Partial releases are allowed.
- Production release currently stores the selected released recipe/current version and quantity as a planning link; the production order module is still a placeholder.
- Planned due date defaults from the order line delivery date, then order delivery date, and can be manually changed by the user.
- MES owns local customer-order statuses independently from ERP statuses; ERP status mapping is not implemented yet.

API endpoints:

- `GET/POST /api/customers`, `GET/PUT/DELETE /api/customers/{id}`
- `GET/POST /api/customer-orders`, `GET/PUT/DELETE /api/customer-orders/{id}`
- `POST /api/customer-orders/{orderId}/lines`
- `PUT/DELETE /api/customer-orders/lines/{id}`
- `POST /api/customer-orders/lines/{id}/production-releases`

## 6. Attachments

Attachments provide polymorphic file support for business objects.

Key paths:

- `AsistOff.MES.Attachments.Core/Entities/Attachment.cs`
- `AsistOff.MES.Attachments.Api/Controllers/AttachmentsController.cs`
- `AsistOff.MES.Web/src/services/attachmentService.ts`
- `AsistOff.MES.Web/src/components/production/AttachmentsPanel.vue`

Business fields:

- `OwnerType`
- `OwnerId`
- `FileName`
- `ContentType`
- `SizeBytes`
- `StorageKey`
- optional `Description`
- optional `UploadedByUserId`
- audit timestamps

API endpoints:

- `GET /api/attachments?ownerType={type}&ownerId={id}`
- `POST /api/attachments` as `multipart/form-data`
- `GET /api/attachments/{id}/download`
- `DELETE /api/attachments/{id}`

Business rules:

- Attachments are tenant-scoped.
- Binary content is stored through `IFileStorage`; the database stores metadata and `StorageKey`.
- Upload request size limit is 100 MB.
- `OwnerType` + `OwnerId` make the module reusable for recipes and future entities.

Known limitations:

- No virus scanning.
- No attachment versioning.
- No deletion audit trail beyond standard logs.

## 7. Frontend Business Navigation

Main route map:

| Route | Business meaning |
|-------|------------------|
| `/dashboard` | Authenticated landing page. |
| `/production/customer-orders` | Customer order list and creation. |
| `/production/customer-orders/:id` | Customer order detail, lines and production release requests. |
| `/production/recipes` | Recipe list and creation. |
| `/production/recipes/:id` | Recipe detail, versions and editor. |
| `/production/orders` | Placeholder for production orders. |
| `/schedule` | Placeholder for planning/scheduling. |
| `/reports` | Placeholder for analytics/reports. |
| `/configuration/products` | Product master data. |
| `/configuration/product-groups` | Product group hierarchy. |
| `/configuration/measure-units` | Units of measure. |
| `/configuration/warehouses` | Warehouses. |
| `/configuration/departments` | Departments. |
| `/configuration/machines` | Machines. |
| `/configuration/operators` | Operators. |
| `/configuration/skills` | Skill dictionary. |
| `/configuration/operation-templates` | Reusable operation templates. |
| `/settings` | Placeholder for settings. |

Services use typed wrappers under `AsistOff.MES.Web/src/services/` and the shared Axios instance in `http.ts`.

Most CRUD views use:

- `useCrudPage` for paging/filtering/sorting state,
- `AppPageHeader`, `AppFilterBar`, `AppTable`, `AppPagination`,
- `AppModal` for create/edit,
- `AppConfirmDialog` for deletes,
- `toastStore` for user feedback.

## 8. Business Features Not Implemented Yet

Do not assume these exist unless a task asks to implement them:

| Feature | Current state |
|---------|---------------|
| Production orders | UI placeholder only. |
| Detailed production scheduling / APS | Not implemented. |
| Shop-floor execution, confirmations, downtime | Not implemented. |
| OEE/KPI reporting | Not implemented. |
| Lot/serial traceability and genealogy | Not implemented. |
| Warehouse movements and reservations | Not implemented beyond master data hints. |
| Supplier/vendor management | Not implemented. |
| Full RBAC roles/permissions | Not implemented; current model is `IsTenantAdmin` based. |
| Soft deletes | Not implemented as a general pattern. |
| External ERP sync | `SyncId` fields exist in several entities, but no sync flow is implemented. |

## 9. Agent Guidance for Business Work

When changing business behavior:

1. Identify the business module first: Multitenancy, Users/Auth, Configuration, Production/Recipes, Attachments, or Frontend only.
2. Preserve tenant isolation; every new tenant-scoped entity/request must follow the established tenant model.
3. Keep `User` and `Operator` separate in naming and logic.
4. Treat released/obsolete recipe versions as immutable.
5. For recipe changes, update the full stack consistently: domain entity, EF configuration/migration if needed, request/response DTOs, handlers, controller, frontend service, view/component, and i18n keys.
6. If adding production execution features, do not overload recipes. Recipes define the plan; production orders/execution should instantiate and track actual work.
7. Document any new business term in `docs/glossary.md`.
