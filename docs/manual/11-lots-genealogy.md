# Lots and genealogy

A Lot is a batch of identical units tracked together — for example 500
pieces of a product from one run. Lots make traceability possible:
which material lots went into which finished-good lot, on which Work
Center, under which Production Order. The links between lots are
genealogy edges, and the upstream/downstream traces walk those edges.

Route `/production/lots` — APIs `/api/lots` (registry) and
`/api/lot-genealogy` (edges and traces).

Reads need only a signed-in user; every create, edit, status change
and edge write needs the `production.write` permission (`403`
without it).

## Lot registry

| Field | Required | Rules |
|-------|----------|-------|
| Code | Yes | Non-empty, max 50 characters, unique per tenant |
| ProductId | Yes | The product this lot holds |
| MeasureUnitId | Yes | The unit for the quantity |
| Quantity | Yes | Zero or more |
| SupplierLotNumber | No | External batch reference, max 100 characters |
| ProducedAt / ExpiryDate | No | Production and expiry timestamps |
| Notes | No | Free text, max 1000 characters |

New lots start as Available.

Lifecycle:

```text
Available  -->  Consumed | Scrapped | Expired | OnHold
```

`OnHold` quarantines a lot pending a decision; the other end states
are final for that lot's usable life.

Procedures:

- **List:** open `/production/lots`; use search, product and status
  filters and sorting. API: `GET /api/lots` (paged).
- **Create:** press create, fill code, product, unit and quantity,
  save. API: `POST /api/lots` → `201`. Duplicate codes within the
  tenant return `409`.
- **Open:** select a row for the detail; the API
  `GET /api/lots/{id}` returns one lot, and
  `GET /api/lots/by-code/{code}` resolves a lot by its code (handy
  for scan lookups).
- **Edit:** change fields in the detail view, save.
  API: `PUT /api/lots/{id}` → `204`. A route/body ID mismatch
  returns `400`.
- **Change status:** move the lot to Available, OnHold, Consumed,
  Scrapped or Expired. API: `POST /api/lots/{id}/status` → `204`.
- **Delete:** delete action, confirm. API:
  `DELETE /api/lots/{id}` → `204`.

## Genealogy edges

An edge records one consumption fact: this produced lot consumed this
quantity of that material lot, on this Work Center, under this order
(and optionally this confirmation), at this time. Most edges are
created automatically when a confirmation names consumed and produced
lots — see [Confirmations](09-confirmations.md). Record an edge
manually only for consumption that happened outside a confirmation
(rework, manual top-up).

| Field | Required | Rules |
|-------|----------|-------|
| ConsumedLotId / ProducedLotId | Yes | The two lots; they must differ |
| ProductionOrderId | Yes | The order the consumption belongs to |
| ProductionConfirmationId | No | The confirmation, when the edge explains one |
| MachineId | Yes | The Work Center |
| ReportedByOperatorId | No | The operator |
| ConsumedQuantity | Yes | Greater than zero |
| OccurredAt | Yes | Cannot be in the future |
| Notes | No | Free text |

Procedures:

- **Record:** API `POST /api/lot-genealogy` → `201`.
- **Browse / read:** API `GET /api/lot-genealogy` (paged) and
  `GET /api/lot-genealogy/{id}`.
- **Delete:** API `DELETE /api/lot-genealogy/{id}` → `204`.

## Upstream and downstream traces

From any lot, walk the tree in both directions:

- **Upstream — "what went into this lot":**
  `GET /api/lot-genealogy/upstream/{lotId}?maxDepth=…`. Starts at a
  finished-good lot and expands consumed lots level by level. Use it
  for a customer complaint: find every material batch inside the
  complained lot.
- **Downstream — "where did this lot go":**
  `GET /api/lot-genealogy/downstream/{lotId}?maxDepth=…`. Starts at a
  material lot and expands produced lots level by level. Use it for a
  supplier recall: find every finished-good lot that contains the
  suspect material.

`maxDepth` caps the expansion; omit it for the full tree. Unknown lot
IDs return `404`.

Worked example (recall): a supplier reports that raw-material lot
`RM-2026-118` is contaminated.

1. Open `/production/lots`, search `RM-2026-118`, open the lot.
2. Call `GET /api/lot-genealogy/downstream/{id}` with the lot's ID.
3. The response lists every finished-good lot that consumed it,
   with the orders, Work Centers and times — quarantine those lots
   (status `OnHold`) and notify the customers from the order records.

## Tenant isolation

Lots and edges are tenant-scoped: traces never cross into another
tenant's lots, and foreign IDs resolve as `404`.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `production.write` (writes) | `403` |
| Missing code, product or unit; code over 50 characters; notes over 1000 characters | `400` naming the field |
| Duplicate lot code within the tenant | `409` |
| Consumed and produced lot identical; non-positive consumed quantity | `400` |
| Occurred time in the future | `400` |
| Route ID differs from body ID on edit | `400` |
| Unknown lot, order or edge ID | `404` |

## Next steps

- Report the consumption that builds these edges: [Confirmations](09-confirmations.md).
- Quarantine suspect lots with reason: [Scrap and Downtime](10-scrap-downtime.md).
- Release the order that consumes the lots: [Production Orders](07-production-orders.md).
