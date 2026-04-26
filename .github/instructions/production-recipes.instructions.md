# Production – Recipes Module

> Load this file whenever you work on the **Production / Recipes** feature area.
> It covers the full stack: backend domain, application, API, frontend service and views.

---

## 1. Overview

Receptury (Recipes) define the manufacturing process for producing a product.
The module lives in:

| Layer | Project |
|-------|---------|
| Domain entities & repositories (interfaces) | `AsistOff.MES.Production.Core` (the Core project IS the domain layer) |
| MediatR handlers, DTOs, responses | `AsistOff.MES.Production.Application` |
| EF Core configs, repositories (implementations) | `AsistOff.MES.Production.Infrastructure` |
| ASP.NET Core controllers | `AsistOff.MES.Production.Api` |
| Frontend SPA | `AsistOff.MES.Web/src/` |

Module bootstrap: `AsistOff.MES.Production.Api/ProductionModule.cs`  
DI registration: `AsistOff.MES.Production.Infrastructure/DependencyInjection.cs`

---

## 2. Domain Model

### Recipe (`production.Recipes`)
Top-level entity. Represents a product's manufacturing definition.

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK |
| `TenantId` | `Guid` | Multi-tenant |
| `Code` | `string` (50) | Unique per tenant |
| `Name` | `string` (200) | |
| `Description` | `string?` (1000) | |
| `IsActive` | `bool` | |
| `PrimaryProductId` | `Guid?` | FK → `config.Products` (no EF nav) |
| `CurrentVersionId` | `Guid?` | Points to the active `Released` version |
| `Versions` | `ICollection<RecipeVersion>` | Nav collection |

### RecipeVersion (`production.RecipeVersions`)
A versioned snapshot of the recipe process.

| Property | Notes |
|----------|-------|
| `Id`, `TenantId` | |
| `RecipeId` | FK |
| `VersionNumber` | Auto-incremented per recipe |
| `Status` | `Draft = 1`, `Released = 2`, `Obsolete = 3` |
| `ReleasedAt`, `ValidFrom`, `ValidTo` | Nullable dates |
| `ChangeNotes` | `string?` |
| `CreatedAt`, `UpdatedAt` | Auditable |

### OperationNode (`production.OperationNodes`)
A manufacturing step inside a recipe version.

| Property | Notes |
|----------|-------|
| `Id`, `TenantId`, `RecipeVersionId` | |
| `Code`, `Name`, `Description` | |
| `OperationType` | Free-text tag (optional) |
| `SortIndex` | Display order |
| `SetupTimeMinutes` | `decimal?` |
| `RunTimeMode` | Enum: `PerUnitSeconds = 1`, `PerBatchMinutes = 2` |
| `RunTimePerUnitSeconds`, `RunTimePerBatchMinutes` | `decimal?` |
| `TeardownTimeMinutes`, `QueueTimeMinutes` | `decimal?` |
| `IsOptional`, `AllowParallelExecution` | `bool` |
| `ExpectedQuantity` | `decimal?` |

Related collections on `OperationNode`:
- `Dependencies` → `OperationDependency` (predecessor links)
- `BomItems` → `BomItem` (material inputs)
- `Outputs` → `OperationOutput` (products / by-products)
- `ResourceRequirements` → `ResourceRequirement` (operator skills)

### OperationDependency (`production.OperationDependencies`)
Links two operations as predecessor → successor.

| Property | Notes |
|----------|-------|
| `SuccessorOperationNodeId`, `PredecessorOperationNodeId` | FKs |
| `DependencyType` | `FinishToStart=1`, `StartToStart=2`, `FinishToFinish=3`, `StartToFinish=4` |
| `LagMinutes` | `decimal?` |

### BomItem (`production.BomItems`)
A material input required for an operation.

| Property | Notes |
|----------|-------|
| `OperationNodeId`, `ProductId` | FKs |
| `MeasureUnitId` | `Guid?` |
| `Quantity` | `decimal` |
| `QuantityType` | `PerUnit=1`, `PerBatch=2`, `Fixed=3` |
| `ScrapPercentage`, `IsOptional` | |
| `ConsumptionTiming` | `AtStart=1`, `Continuous=2`, `AtEnd=3` |

### OperationOutput (`production.OperationOutputs`)
A product / by-product produced by an operation.

| Property | Notes |
|----------|-------|
| `OperationNodeId`, `ProductId` | FKs |
| `Quantity`, `QuantityType` | |
| `OutputType` | `Product=1`, `ByProduct=2`, `Waste=3`, `Sample=4` |

### ResourceRequirement (`production.ResourceRequirements`)
Operator / machine requirements for an operation.

| Property | Notes |
|----------|-------|
| `OperationNodeId` | FK |
| `PreferredDepartmentId`, `PreferredMachineId` | `Guid?` |
| `RequiredCapability` | Free-text skill name (shown from skill dictionary) |
| `RequiredOperatorCount` | `int` |
| `RequiredRole` | `string?` |

### OperationTemplate (`production.OperationTemplates`)
Reusable template for operations. Managed in Configuration → Operation Templates.  
When added to a recipe, the form prefills from the template's defaults.

Same timing fields as `OperationNode`. Has `Code`, `Name`, `Description`, `IsActive`.

---

## 3. Skills (`config.Skills`)

Managed in Configuration → Skills (`AsistOff.MES.Configuration.*`).  
The skills are used as a lookup when adding `ResourceRequirement` rows.
In the current implementation `RequiredCapability` stores the skill display string
(`"CODE — Name"`), not the skill ID (to keep the field human-readable in orders/shop-floor views).

Entity: `AsistOff.MES.Configuration.Core/Entities/Skill.cs`  
Repository interface: `AsistOff.MES.Configuration.Core/Repositories/ISkillsRepository.cs`  
CRUD handlers: `AsistOff.MES.Configuration.Application/Features/Skills/`  
Controller: `AsistOff.MES.Configuration.Api/Controllers/SkillsController.cs` → `GET/POST/PUT/DELETE /api/skills`

---

## 4. API Endpoints

### Recipes  `/api/recipes`
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/recipes` | Paged list (filters: `code`, `name`, `isActive`) |
| GET | `/api/recipes/{id}` | Single recipe with versions |
| POST | `/api/recipes` | Create recipe (optionally with `primaryProductId`) |
| PUT | `/api/recipes/{id}` | Update |
| DELETE | `/api/recipes/{id}` | Delete (cascade to versions) |

### Recipe Versions  `/api/recipe-versions`
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/recipe-versions/{id}` | Full version detail with all operations |
| POST | `/api/recipe-versions` | Create blank draft for a recipe |
| POST | `/api/recipe-versions/clone` | Clone a released version into a new draft |
| POST | `/api/recipe-versions/{id}/release` | Release a draft |
| DELETE | `/api/recipe-versions/{id}` | Delete draft version |

### Operations (OperationNode CRUD)  `/api/operations`
| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/operations` | Add operation to version |
| PUT | `/api/operations/{id}` | Update operation |
| DELETE | `/api/operations/{id}` | Delete operation |
| POST | `/api/operations/{id}/dependencies` | Add dependency |
| DELETE | `/api/operations/{id}/dependencies/{depId}` | Remove dependency |
| POST | `/api/operations/{id}/bom` | Add BOM item |
| DELETE | `/api/operations/bom/{id}` | Remove BOM item |
| POST | `/api/operations/{id}/outputs` | Add output |
| DELETE | `/api/operations/outputs/{id}` | Remove output |
| POST | `/api/operations/{id}/resources` | Add resource requirement |
| DELETE | `/api/operations/resources/{id}` | Remove resource requirement |

### Operation Templates  `/api/operation-templates`
Standard CRUD: `GET`, `GET/{id}`, `POST`, `PUT/{id}`, `DELETE/{id}`

### Skills  `/api/skills`
Standard CRUD: `GET`, `GET/{id}`, `POST`, `PUT/{id}`, `DELETE/{id}`

---

## 5. Application Layer Patterns

All request types implement `ITenantRequest<TResponse>` or `ITenantRequest` (for commands).  
Handlers are in `Features/{Entity}/{Verb}/` subfolders.

Typical file structure for a feature:
```
Features/Recipes/
  Browse/BrowseRecipesRequest.cs
  Browse/BrowseRecipesRequestHandler.cs
  Create/CreateRecipeRequest.cs
  Create/CreateRecipeRequestHandler.cs
  Delete/DeleteRecipeRequest.cs
  Delete/DeleteRecipeRequestHandler.cs
  Get/GetRecipeRequest.cs
  Get/GetRecipeRequestHandler.cs
  Update/UpdateRecipeRequest.cs
  Update/UpdateRecipeRequestHandler.cs
  Common/OperationNodeResponse.cs   ← shared response DTOs
```

---

## 6. Frontend

### Services
| File | API coverage |
|------|-------------|
| `src/services/recipeService.ts` | `/api/recipes` |
| `src/services/recipeVersionService.ts` | `/api/recipe-versions`, `/api/operations` (+ child CRUD) |
| `src/services/skillService.ts` | `/api/skills` |
| `src/services/operationTemplateService.ts` | `/api/operation-templates` |

### Views
| Route | Component |
|-------|-----------|
| `/production/recipes` | `src/views/production/RecipesView.vue` |
| `/production/recipes/:id` | `src/views/production/RecipeDetailView.vue` |
| `/configuration/skills` | `src/views/configuration/SkillsView.vue` |
| `/configuration/operation-templates` | `src/views/configuration/OperationTemplatesView.vue` |

### Key Components
- `src/components/production/RecipeVersionEditor.vue` — main editor for a recipe version (operations, BOM, outputs, resources, attachments, dependencies)
- `src/components/ui/AppAutocomplete.vue` — reusable autocomplete/search-select component

### UX Flows

**Creating a recipe:**
1. Open `/production/recipes` → click "New recipe"
2. Fill `Code`, `Name`, select `Primary Product` via autocomplete (optional)
3. After save → navigate to `/production/recipes/:id` automatically

**Adding a version:**
- If a `Released` version exists → `Clone` is used
- Otherwise a blank `Draft` is created

**Adding operations:**
1. Select a version (must be `Draft`)
2. Click "Add operation"
3. Optionally pick from `Operation Templates` autocomplete to prefill defaults
4. Fill/override `Code`, `Name`, timing fields → Save

**BOM & Outputs:**
- Product is selected via `AppAutocomplete` bound to the full product list (code — name)

**Resource Requirements:**
- Skill is selected via `AppAutocomplete` from the skill dictionary (`/api/skills`)
- The `RequiredCapability` stored is the skill's `"CODE — Name"` display string

---

## 7. Known Patterns & Pitfalls

- `CreatedAtAction` must use the string `"Get"` — **not** `nameof(GetAsync)` — to generate correct `Location` headers (ASP.NET Core MVC strips the `Async` suffix from action names).
- `reloadAll()` in `RecipeDetailView.vue` preserves `selectedVersionId` explicitly before refreshing data (to avoid resetting to v1).
- `PagedResponse<T>` in `AsistOff.MES.Shared.Abstractions` is **abstract**. Concrete typed subclasses (`PagedXxxResponse`) must be created in each Application project.
- EF migrations are in `AsistOff.MES.Shared.Infrastructure/Migrations/` targeting `DefaultContext`.

---

## 8. Adding New Features

Typical checklist for adding a new field to a recipe entity:

1. Update entity class in `.Core/Entities/`
2. Update EF configuration in `.Infrastructure/Configurations/`
3. Add migration: `dotnet ef migrations add <Name> --project AsistOff.MES.Shared.Infrastructure --startup-project AsistOff.MES.Gateway`
4. Update request/response DTOs
5. Update handler(s)
6. Update controller (if new endpoint)
7. Update frontend service type
8. Update Vue component/view
9. Add i18n keys to `src/i18n.ts` (both `pl` and `en` sections)
