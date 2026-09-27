# AsistOff MES — End-User Manual (Slice 1 of 3)

Slice 1 covers getting started, master data, and recipes engineering.
It is the first of three slices; slices 2 (production execution) and 3
(analytics, integration, administration and operations) follow separately.

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
