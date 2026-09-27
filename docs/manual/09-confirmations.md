# Confirmations with RW/PW

A confirmation is an operator's report of work performed against a
Released or InProgress Production Order: how much good output was
produced, how much was scrapped, which material lots were consumed and
which finished-good lot was produced. Every confirmation moves stock:
materials leave the warehouse (RW — *Rozchód Wewnętrzny*, internal
issue) and finished goods arrive (PW — *Przyjęcie Wewnętrzne*,
internal receipt).

Confirmations are reported from the order detail view
(`/production/orders/{id}`) — API `/api/production-confirmations`.

Reads need only a signed-in user; reporting and deleting a
confirmation needs the `production.write` permission (`403`
without it).

## Reporting a confirmation

| Field | Required | Rules |
|-------|----------|-------|
| ProductionOrderId | Yes | The order; must be Released or InProgress |
| MachineId | Yes | The Work Center the work ran on |
| ReportedByOperatorId | No | The operator who did the work (see [master data](04-master-data-resources.md)) |
| ReportedAt | Yes | When the work happened; cannot be in the future and cannot be before the order was released |
| GoodQuantity | Yes* | Zero or more; at least one of good or scrap must be greater than zero |
| ScrapQuantity | Yes* | Zero or more; at least one of good or scrap must be greater than zero |
| ProducedLotId | Depends | Required when consumed lots are listed; must differ from every consumed lot |
| ConsumedLots | No | List of (lot, quantity) pairs, each quantity greater than zero |
| Notes | No | Free text, max 1000 characters |

Procedure:

1. Open the Released order at `/production/orders/{id}`.
2. Report the confirmation with machine, time, good and scrap
   quantities, optionally naming the produced lot and the consumed
   lots with their quantities.
   API: `POST /api/production-confirmations` → `201`.
3. The first confirmation flips the order from Released to InProgress
   automatically — no separate "start" action exists.
4. Check the totals on the order: produced, scrapped and confirmation
   count accumulate across confirmations and gate completion (an order
   with no good quantity cannot be completed — see
   [Production Orders](07-production-orders.md)).

Unknown or cross-tenant lot IDs fail with `404` and leave no orphan
confirmation behind; consumed and produced lots must differ (`400`).

## RW/PW stock effect

Each confirmation fans out atomically: the confirmation row, all
stock movement lines, all genealogy edges and the Released →
InProgress flip commit in one database transaction — either all of
them persist or none do.

- **RW (internal issue):** consumed quantities leave the warehouse
  named by the BOM item's preferred warehouse (falling back to the
  order default). Relief also settles the matching material
  reservation created at release, so reserved stock becomes free as
  it is consumed.
- **PW (internal receipt):** the good quantity arrives into the
  preferred receipt warehouse from the operation output definition.

To see the effect before or after reporting:

- Per confirmation (read-only preview):
  `GET /api/production-confirmations/{id}/movements`.
- Per order (read-only aggregate over all its confirmations):
  `GET /api/production-orders/{id}/movements`.

Both previews list movement lines with warehouse, product, quantity
and direction; they never change data.

## Deleting a confirmation

API: `DELETE /api/production-confirmations/{id}` → `204`. Deleting
removes the confirmation row; stock movements already posted stay in
the warehouse ledger, so prefer correcting with a new offsetting
confirmation rather than deleting history.

## Tenant isolation

Confirmations, movements, lots and reservations are tenant-scoped.
Cross-tenant lot or order IDs are hidden by the tenant filter and
resolve as `404`.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `production.write` (writes) | `403` |
| Order Planned, Completed or Closed | `409` for Completed/Closed, `400` for Planned |
| Missing machine or time; both quantities zero; notes over 1000 characters | `400` naming the field |
| Reported time in the future or before the order release | `400` |
| Consumed lots listed without a produced lot | `400` |
| Consumed and produced lot identical | `400` |
| Unknown order or lot ID | `404` |

## Next steps

- Record process loss separately: [Scrap and Downtime](10-scrap-downtime.md).
- Trace what went into what: [Lots and genealogy](11-lots-genealogy.md).
- Finish the order: [Production Orders](07-production-orders.md).
