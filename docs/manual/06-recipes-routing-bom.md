# Routing, BOM and resources

This chapter covers the inside of a recipe version: OperationNode
routing with OperationDependency edges, OperationTemplate reuse,
OperationOutput, BomItem quantities and ResourceRequirement assignments
against a Work Center. Read [Recipes and versions](05-recipes.md)
first — everything here lives under a Draft version and is managed
from the recipe detail view (`/production/recipes/{id}`).

All operations in this chapter need the `production.write` permission
(`403` without it); reads need only a signed-in user.

## Operation templates (reuse)

Route `/configuration/operation-templates` — API
`/api/operation-templates`.
A template is a reusable operation master: code, name, type and default
times. Engineers copy a template's values when adding an operation
instead of retyping them, so repeating steps stay consistent.

| Field | Required | Rules |
|-------|----------|-------|
| Code | Yes | Non-empty, unique per tenant |
| Name | Yes | Non-empty |
| Description | No | Free text |
| OperationType | No | Free classification label |
| IsActive | Yes | Inactive templates are hidden from pickers |
| SetupTimeMinutes | No | Setup time in minutes |
| RunTimeMode | Yes | Per-unit or per-batch timing |
| RunTimePerUnitSeconds | Depends on mode | Required in per-unit mode |
| RunTimePerBatchMinutes | Depends on mode | Required in per-batch mode |
| TeardownTimeMinutes | No | Teardown time in minutes |
| QueueTimeMinutes | No | Planned queue time in minutes |

Procedures: list (`GET /api/operation-templates`, paged), create
(`POST /api/operation-templates` → `201`), edit
(`PUT /api/operation-templates/{id}` → `204`), delete
(`DELETE /api/operation-templates/{id}` → `204`). Duplicate codes
return `409`.

## Operations (routing nodes)

Managed in the recipe detail view, version section. Each operation is
one routing step with its own code, name, times and execution flags.

| Field | Required | Rules |
|-------|----------|-------|
| VersionId | Yes | Owning Draft version |
| Code | Yes | Non-empty, unique within the version |
| Name | Yes | Non-empty |
| Description | No | Free text |
| OperationType | No | Free classification label |
| SortIndex | No | Display order; the reorder endpoint rewrites the whole order |
| SetupTimeMinutes / TeardownTimeMinutes / QueueTimeMinutes | No | Non-negative minutes |
| RunTimeMode | Yes | Per-unit or per-batch timing |
| RunTimePerUnitSeconds / RunTimePerBatchMinutes | Depends on mode | The matching one is required |
| IsOptional | Yes | Optional steps may be skipped in execution |
| AllowParallelExecution | Yes | Whether the step may overlap others |
| ExpectedQuantity | No | Planned output quantity of the step |

Procedures:

- **Add:** add an operation to the Draft version, optionally seeding
  times from a template. API: `POST /api/operations` → `200` with the
  operation node.
- **Edit:** change any field. API:
  `PUT /api/operations/{id}` → `204`.
- **Delete:** remove the operation with its dependencies, BOM items,
  outputs and resources. API: `DELETE /api/operations/{id}` → `204`.
- **Reorder:** drag operations into sequence; the view saves the whole
  order at once. API:
  `POST /api/recipe-versions/{versionId}/operations/reorder` → `204`.

## Dependencies (routing order)

Dependencies turn the operation list into a routing graph: each edge
says "this operation waits for a predecessor", with a type and an
optional lag.

- **Set:** edit an operation's predecessors in the detail view; saving
  replaces the full inbound edge set of that operation.
  API: `PUT /api/operations/{id}/dependencies` → `204`.
- Each entry carries the predecessor operation ID, the dependency type
  and an optional lag in minutes.
- The backend rejects any set that would close a cycle (`400`/`409`
  with a message naming the loop) — a routing must stay acyclic.
- An operation with no dependencies starts immediately; an operation
  with predecessors waits until they complete (plus lag).

Keep routings linear where possible and reserve parallel branches for
genuinely independent steps — every branch complicates scheduling and
confirmation later.

## BOM items (materials consumed)

Each operation lists the materials it consumes. A BOM item points at a
product (see [Products](02-master-data-products.md)) with a quantity,
a quantity type and consumption timing.

| Field | Required | Rules |
|-------|----------|-------|
| OperationId | Yes | Owning operation |
| ProductId | Yes | Consumed material |
| MeasureUnitId | No | Unit for the quantity; defaults to the product's base unit |
| Quantity | Yes | Positive amount |
| QuantityType | Yes | Per-unit or per-batch scaling |
| ScrapPercentage | No | Expected scrap allowance added to the gross need |
| IsOptional | Yes | Optional materials may be skipped |
| PreferredWarehouseId | No | Warehouse to issue from (see [Warehouses](03-master-data-organisation.md)) |
| ConsumptionTiming | Yes | When the material is consumed (start or end of the operation) |
| Notes | No | Free text |
| SortIndex | No | Display order |

Procedures:

- **Add:** API `POST /api/operations/{id}/bom-items` → `200` with the
  new item ID. Route/body ID mismatch returns `400`.
- **Edit:** API `PUT /api/bom-items/{id}` → `204`.
- **Remove:** API `DELETE /api/bom-items/{id}` → `204`.

Scale quantities per unit of the operation's expected output so that
order quantities multiply cleanly; use the scrap percentage for known
process loss instead of inflating the base quantity.

## Operation outputs (what the step produces)

Outputs mirror BOM items on the production side: each operation declares
the products it yields, with quantity, type and preferred receipt
warehouse.

| Field | Required | Rules |
|-------|----------|-------|
| OperationId | Yes | Owning operation |
| ProductId | Yes | Produced product |
| MeasureUnitId | No | Unit for the quantity; defaults to the product's base unit |
| Quantity | Yes | Positive amount |
| QuantityType | Yes | Per-unit or per-batch scaling |
| OutputType | Yes | Main product, co-product or by-product handling |
| PreferredWarehouseId | No | Warehouse to receive into |
| Notes | No | Free text |
| SortIndex | No | Display order |

Procedures:

- **Add:** API `POST /api/operations/{id}/outputs` → `200` with the
  new output ID.
- **Edit:** API `PUT /api/outputs/{id}` → `204`.
- **Remove:** API `DELETE /api/outputs/{id}` → `204`.

The final operation's main output should be the recipe's primary
product so orders and genealogy resolve correctly.

## Resource requirements (Work Center assignment)

Each operation declares what it needs to run: a preferred department
and machine (the Work Center, see
[Work Centers](04-master-data-resources.md)), a required capability, a
headcount and notes.

| Field | Required | Rules |
|-------|----------|-------|
| OperationId | Yes | Owning operation |
| PreferredDepartmentId | No | Preferred department |
| PreferredMachineId | No | Preferred Work Center — the machine that should run this step |
| RequiredCapability | No | Capability or skill code the resource must have |
| RequiredOperatorCount | Yes | Number of operators, zero when unattended |
| RequiredRole | No | Role the operators must hold |
| Notes | No | Free text |

Procedures:

- **Add:** pick the preferred machine (and department where relevant),
  set the headcount, save. API:
  `POST /api/operations/{id}/resources` → `200` with the new
  requirement ID.
- **Edit:** API `PUT /api/resources/{id}` → `204`.
- **Remove:** API `DELETE /api/resources/{id}` → `204`.

Always set a preferred machine for machine-paced steps; leave it empty
only for purely manual steps, and keep the required skill in step with
the [Skills](04-master-data-resources.md) dictionary.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `production.write` (writes) | `403` |
| Missing required field or negative quantity/time | `400` naming the field |
| Dependency set that creates a cycle | `400`/`409` describing the loop |
| Route ID differs from body ID | `400` |
| Unknown operation, item or output ID | `404` |
| Structural edit of a Released or Obsolete version | `409` — clone the version first (see [Recipes and versions](05-recipes.md)) |

## Checklist before release

1. The version has at least one operation (release precondition).
2. Dependencies form an acyclic graph in the intended run order.
3. Every operation has its BOM items with positive quantities.
4. The final operation outputs the recipe's primary product.
5. Machine-paced operations name a preferred Work Center.

Release the version as described in [Recipes and versions](05-recipes.md).
