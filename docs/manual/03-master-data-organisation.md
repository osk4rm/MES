# Warehouses and departments

This chapter covers the master data that describes *where* stock sits
and *who owns* resources: Warehouse and Department. Both are small
dictionaries, but stock movements, BOM preferences and machine
assignments all reference them, so create them before resources and
recipes.

Both views share the standard Configuration page pattern (searchable,
sortable, paginated table with refresh, create, row edit, row delete).
All reads need only a signed-in user; every create, edit and delete
needs the `configuration.write` permission (`403` without it).

## Warehouses

Route `/configuration/warehouses` — API `/api/warehouses`.
A warehouse is a storage location. Stock-on-hand rows are kept per
product and warehouse, and the Warehouses view shows a stock card per
warehouse.

| Field | Required | Rules |
|-------|----------|-------|
| Name | Yes | Non-empty, unique per tenant |
| SyncId | No | External ERP key |

Procedures:

- **List:** open the view; use search and sorting.
  API: `GET /api/warehouses` (paged).
- **Create:** press create, enter the name, save.
  API: `POST /api/warehouses` → `201` with the warehouse.
- **Edit:** row action edit, rename, save.
  API: `PUT /api/warehouses/{id}` → `204`. A route/body ID mismatch
  returns `400`.
- **Delete:** row action delete, confirm. API:
  `DELETE /api/warehouses/{id}` → `204`. A warehouse that still holds
  stock, open movements, reservations, or is set as a preferred
  warehouse on a BOM item or operation output cannot be deleted
  (`409`).

Warehouse-related reads available once warehouses exist:

- **Stock on hand** (`GET /api/stock-on-hand`): quantity per product
  and warehouse, shown on the Warehouses view stock card.
- **Stock movements** (`GET /api/stock-movements`): the RW (internal
  issue) and PW (internal receipt) history behind the on-hand figures.
  Movements are created automatically by production confirmations
  (slice 2) — never hand-edited here.

## Departments

Route `/configuration/departments` — API `/api/departments`.
A department groups machines and operators organisationally (for
example "Assembly", "Packaging"). Machines and operators optionally
point at a department; resource requirements on routing operations can
prefer one.

| Field | Required | Rules |
|-------|----------|-------|
| Code | Yes | Non-empty, unique per tenant |
| Name | Yes | Non-empty |

Procedures:

- **List:** open the view; use search and sorting.
  API: `GET /api/departments` (paged).
- **Create:** press create, fill code and name, save.
  API: `POST /api/departments` → `201` with the department.
- **Edit:** row action edit, change code or name, save.
  API: `PUT /api/departments/{id}` → `204`. A route/body ID mismatch
  returns `400`.
- **Delete:** row action delete, confirm. API:
  `DELETE /api/departments/{id}` → `204`. A department still assigned
  to machines or operators cannot be deleted (`409`).

## Shared error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `configuration.write` (writes) | `403` |
| Empty name (warehouse) or empty code/name (department) | `400` naming the field |
| Duplicate warehouse name or department code within the tenant | `409` |
| Route ID differs from body ID on edit | `400` |
| Unknown ID | `404` |

## Next steps

- Describe machines and people: [Work Centers, operators, rosters and skills](04-master-data-resources.md).
- Describe what you make: [Products, groups, units and scan lookup](02-master-data-products.md).
