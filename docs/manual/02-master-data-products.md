# Products, groups, units and scan lookup

This chapter covers the master data that describes *what* you make:
Product, ProductGroup, MeasureUnit with ProductMeasureUnit, and the
EAN/GTIN scan lookup. Set these up before warehouses, resources and
recipes — everything else references products.

All four views live under Configuration and share the same page pattern:
searchable, sortable, paginated table with refresh, create, row edit and
row delete. All reads need only a signed-in user; every create, edit
and delete needs the `configuration.write` permission (`403` without it).

## Product groups

Route `/configuration/products`, groups section — API `/api/product-groups`.
Groups classify products and may nest via an optional parent.

| Field | Required | Rules |
|-------|----------|-------|
| Code | Yes | Non-empty, unique per tenant |
| Name | Yes | Non-empty |
| Description | No | Free text |
| IsActive | Yes | Inactive groups stay in history but are hidden from pickers |
| ParentId | No | Optional parent group for a hierarchy; empty for top-level groups |
| SyncId | No | External ERP key |

Procedures:

- **List:** open the view; use search, the active/inactive filter and
  column sorting. API: `GET /api/product-groups` (paged).
- **Create:** press create, fill code and name, optionally pick a
  parent, save. API: `POST /api/product-groups` → `201` with the group.
- **Edit:** row action edit, change any field, save.
  API: `PUT /api/product-groups/{id}` → `204`. A route/body ID
  mismatch returns `400`.
- **Delete:** row action delete, confirm. API:
  `DELETE /api/product-groups/{id}` → `204`. Deleting a group that
  still has products or child groups assigned returns `409`.

## Measure units and product units

Route `/configuration/measure-units` — API `/api/measure-units`.
A MeasureUnit (pieces, kilograms, meters) can stand alone or convert
into a base unit. Each product additionally links the units it is
measured in through ProductMeasureUnit (base unit plus alternates with
conversion factors).

| Field | Required | Rules |
|-------|----------|-------|
| Name | Yes | Non-empty, for example "Piece" |
| Symbol | Yes | Short mark, for example "pc", "kg" |
| Type | Yes | Unit kind (base or derived handling) |
| ConversionFactor | Only for derived units | Factor against the base unit, for example 1000 g per kg |
| BaseUnitId | Only for derived units | The base unit this unit converts into |
| IsActive | Yes | Inactive units stay in history but are hidden from pickers |
| Description | No | Free text |
| SyncId | No | External ERP key |

Procedures: list, create, edit and delete work exactly like product
groups (API `/api/measure-units`, `201` on create, `204` on edit and
delete). A unit still referenced by products or BOM items cannot be
deleted (`409`). Assign a product's units on the product form by picking
its base unit and, where needed, alternate units with their factors.

## Products

Route `/configuration/products` — API `/api/products`.
The product is the central record: recipes, BOM items, stock and orders
all point at it.

| Field | Required | Rules |
|-------|----------|-------|
| Code | Yes | Non-empty, unique per tenant |
| Name | Yes | Non-empty |
| Description | No | Free text |
| Ean | No | When present: 8, 12, 13 or 14 digits with a valid GTIN check digit; unique per tenant |
| Barcode | No | Free text, for non-GTIN symbologies |
| ScanBy | Yes | Which identifier the shopfloor scan uses: code, EAN or barcode |
| IsActive | Yes | Inactive products stay in history but are hidden from pickers |
| ProductGroupId | No | Owning group |
| SyncId | No | External ERP key |

Procedures:

- **List:** open the view; filter by name, code, EAN, barcode, group,
  active flag or scan mode, and sort by name, code, active or scan
  mode. API: `GET /api/products` (paged, max 100 rows per page).
- **Create:** press create, fill code and name plus the optional data,
  save. API: `POST /api/products` → `201` with the product.
- **Edit:** row action edit, change any field, save.
  API: `PUT /api/products/{id}` → `204`.
- **Delete:** row action delete, confirm. API:
  `DELETE /api/products/{id}` → `204`. A product still referenced by
  a recipe, BOM item, stock row or order returns `409`.

Error cases:

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `configuration.write` (writes) | `403` |
| EAN with wrong length or bad check digit | `400` naming the Ean field |
| Duplicate EAN within the tenant | `409` |
| Duplicate code within the tenant | `409` |
| Route ID differs from body ID on edit | `400` |
| Unknown product ID | `404` |

## EAN/GTIN scan lookup

The shopfloor scan box resolves a scanned value to a product without
knowing its internal ID first.

- Type or scan any value into a product scan field (or call
  `GET /api/products/by-scan?value=…`).
- The backend tries the product's configured `ScanBy` identifier —
  code, then EAN, then barcode — normalises the EAN and returns the
  matching product.
- No match returns `404`; an empty value returns `400`.
- Because EANs are tenant-unique, a scan can never return another
  tenant's product; tenant isolation is enforced by the shared query
  filter, not by caller parameters.

Set the `ScanBy` field per product to the symbology actually printed on
its label so the scan box checks the right identifier first.

## Next steps

- Store products somewhere: [Warehouses and departments](03-master-data-organisation.md).
- Describe who and what does the work: [Work Centers, operators, rosters and skills](04-master-data-resources.md).
- Define how a product is made: [Recipes and versions](05-recipes.md).
