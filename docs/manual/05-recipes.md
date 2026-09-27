# Recipes and versions

This chapter covers recipe engineering at the header level: the Recipe
and its RecipeVersions with the release flow. The inside of a version —
operations, routing, BOM and resources — is covered in
[Routing, BOM and resources](06-recipes-routing-bom.md).

Reads need only a signed-in user; every create, edit, clone, release
and delete needs the `production.write` permission (`403` without it).

## Recipes

Route `/production/recipes` — API `/api/recipes`.
A recipe is the engineering definition of how to make one product. It
holds the header data; the engineering content lives in its versions.

| Field | Required | Rules |
|-------|----------|-------|
| Code | Yes | Non-empty, unique per tenant |
| Name | Yes | Non-empty |
| Description | No | Free text |
| IsActive | Yes | Inactive recipes stay in history but cannot start new versions or orders |
| PrimaryProductId | No | The product this recipe makes (see [Products](02-master-data-products.md)) |
| SyncId | No | External ERP key |

Procedures:

- **List:** open `/production/recipes`; use search, the active filter
  and sorting. API: `GET /api/recipes` (paged).
- **Create:** press create, fill code and name, optionally link the
  primary product, save. API: `POST /api/recipes` → `201` with the
  recipe.
- **Open:** select a row to open the detail view at
  `/production/recipes/{id}`, which shows the header, the version list
  and the selected version's operations, BOM and resources.
  API: `GET /api/recipes/{id}`.
- **Edit:** edit the header in the detail view, save.
  API: `PUT /api/recipes/{id}` → `204`. A route/body ID mismatch
  returns `400`.
- **Delete:** delete action in the detail view, confirm. API:
  `DELETE /api/recipes/{id}` → `204`. A recipe with versions or
  production orders cannot be deleted (`409`).

## Recipe versions

API `/api/recipe-versions`, managed from the recipe detail view.
A version freezes one engineering state of the recipe. Versions start
as Draft, are engineered freely, then released. Exactly one version per
recipe is current: releasing a version points the recipe at it.

Lifecycle:

```text
Draft  --release-->  Released  --(a newer release)-->  Obsolete
```

| Status | Meaning |
|--------|---------|
| Draft | Under engineering. Operations, dependencies, BOM, outputs and resources can change freely. Never usable by production orders. |
| Released | Frozen and usable. A recipe's current version is always Released. Releasing a version demotes the previously Released sibling to Obsolete. |
| Obsolete | Superseded. Read-only history; production orders already created from it keep working. |

Procedures:

- **Create a version:** in the recipe detail view, create a version
  with optional change notes and validity dates (`ValidFrom`,
  `ValidTo`). The version starts as an empty Draft.
  API: `POST /api/recipe-versions` with `RecipeId` → `201`.
- **Clone a version:** copy any existing version (any status) into a
  new Draft of the same recipe, deep-copying operations, dependencies,
  BOM items, outputs and resource requirements with fresh IDs, then
  edit the copy. This is the normal way to start the next revision.
  API: `POST /api/recipe-versions/clone` with `SourceVersionId` → `201`.
- **Edit metadata:** change notes and validity dates of a version.
  API: `PUT /api/recipe-versions/{id}/metadata` → `204`. Only Draft
  versions accept structural edits; metadata of Released versions is
  limited to notes and dates.
- **Release:** press release on a Draft version.
  API: `POST /api/recipe-versions/{id}/release` → `204`. On success
  the version becomes Released, the previously Released sibling (if
  any) becomes Obsolete, and the recipe's current version pointer
  moves to the new release.
- **Delete:** delete a version. API:
  `DELETE /api/recipe-versions/{id}` → `204`. Released and Obsolete
  versions, and versions referenced by production orders, cannot be
  deleted (`409`).
- **Read:** API `GET /api/recipe-versions/{id}` returns the full
  detail: header, status, validity, operations with dependencies, BOM
  items, outputs and resource requirements.

Release preconditions (all enforced, `400`/`409` with a message when
unmet):

1. The version must be in Draft status — only Draft versions can be
   released. Releasing a Released or Obsolete version returns `409`.
2. The version must contain at least one operation — an empty version
   cannot be released (`400`).

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `production.write` (writes) | `403` |
| Missing code or name (recipe) | `400` naming the field |
| Duplicate recipe code within the tenant | `409` |
| Release of a non-Draft version | `409` |
| Release of a version with no operations | `400` |
| Route ID differs from body ID on edit | `400` |
| Unknown ID | `404` |

## Next steps

- Fill the version with engineering content: [Routing, BOM and resources](06-recipes-routing-bom.md).
- Make sure referenced master data exists: [Products](02-master-data-products.md) and [Work Centers, operators, rosters and skills](04-master-data-resources.md).
