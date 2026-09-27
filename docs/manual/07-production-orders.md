# Production Orders

This chapter covers the Production Order lifecycle: creating a Planned
order, releasing it (which reserves materials), confirming output against
it, then completing and closing it. Read
[Recipes and versions](05-recipes.md) first — every order points at one
Released recipe version.

Route `/production/orders`, detail `/production/orders/{id}` — API
`/api/production-orders`.

Reads need only a signed-in user; every create, edit, delete and
transition needs the `production.write` permission (`403` without it).

## Lifecycle

```text
Planned  --release-->  Released  --(first confirmation)-->  InProgress
   --complete-->  Completed  --close-->  Closed
```

| Status | Meaning |
|--------|---------|
| Planned | Draft order. The only status that can be edited or deleted. Never usable for confirmations. |
| Released | Committed to the shopfloor. Material reservations are created from the recipe BOM. Confirmations can be reported. |
| InProgress | Work has started: the first confirmation flips a Released order here automatically. Completing requires at least one confirmation with good quantity. |
| Completed | Work is done. No more confirmations can be reported. Ready to close. |
| Closed | Archived. Closing settles any remaining material reservations. |

Delete is only possible while Planned. There is no way back from any
transition: a Released order cannot return to Planned, and a Closed order
is final.

## Fields

| Field | Required | Rules |
|-------|----------|-------|
| Code | Yes | Non-empty, unique per tenant |
| ProductId | Yes | The finished product being made (see [Products](02-master-data-products.md)) |
| RecipeId / RecipeVersionId | Yes | The recipe and version to build from; the version must be Released before the order itself can be released |
| PlannedQuantity | Yes | Greater than zero, in the order's unit |
| MeasureUnitId | No | Unit for the quantity; defaults to the product's base unit |
| Priority | Yes | Higher numbers dispatch first on the [dispatch board](08-dispatch-board.md) and in the [Operator panel queue](14-operator-panel.md) |
| DueDate | No | Planned completion date; drives overdue flags and ordering |
| Notes / SyncId | No | Free text; SyncId is the external ERP key |

## Procedures

- **List:** open `/production/orders`; use search, status filter and
  sorting. API: `GET /api/production-orders` (paged).
- **Create:** press create, fill code, product, recipe and version,
  planned quantity, priority and due date, save.
  API: `POST /api/production-orders` → `201` with the order.
- **Open:** select a row to open `/production/orders/{id}`, which shows
  the header, totals, the RW/PW movement preview, confirmations and the
  audit history (`GET /api/production-orders/{id}/history`, newest
  first, 50 rows).
- **Edit:** edit a Planned order in the detail view, save.
  API: `PUT /api/production-orders/{id}` → `200`. Only Planned orders
  can be edited (`409` otherwise); a route/body ID mismatch returns
  `400`. The body must carry the `concurrencyToken` from the last
  `GET`; a stale token returns `409` with the current token — reload
  the order, re-apply the change, resubmit.
- **Delete:** delete a Planned order, confirm.
  API: `DELETE /api/production-orders/{id}` → `204`. Non-Planned
  orders cannot be deleted (`409`).
- **Release:** press release on a Planned order.
  API: `POST /api/production-orders/{id}/release?concurrencyToken=…`
  → `200` with the Released order. Release creates one material
  reservation row per distinct (product, warehouse) pair from the
  recipe BOM, scaled by the order quantity; orders whose recipe has no
  BOM items reserve nothing. The referenced recipe version must be
  Released (`400` otherwise), and only Planned orders can be released
  (`409` otherwise).
- **Complete:** press complete on an InProgress order.
  API: `POST /api/production-orders/{id}/complete?concurrencyToken=…`
  → `200`. Only InProgress orders with at least one confirmation
  carrying good quantity can be completed (`409` otherwise).
- **Close:** press close on a Completed order.
  API: `POST /api/production-orders/{id}/close?concurrencyToken=…`
  → `200`. Closing settles open material reservations so they stop
  reducing stock availability.

The concurrency token guard works the same on release, complete and
close: when the token is supplied and stale, the transition is rejected
with `409` carrying the current token instead of silently transitioning.
Reload the order and resubmit.

## Tenant isolation

Orders, reservations, confirmations and movements are tenant-scoped:
one tenant never sees another tenant's orders, and IDs from another
tenant resolve as `404`.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `production.write` (writes) | `403` |
| Missing code, product, recipe or version; planned quantity zero or negative | `400` naming the field |
| Duplicate order code within the tenant | `409` |
| Edit or delete of a non-Planned order | `409` |
| Release of a non-Planned order | `409` |
| Release against an unreleased recipe version | `400` |
| Complete of an order with no good quantity confirmed | `409` |
| Confirmations against a Completed or Closed order | `409` |
| Stale concurrency token on update or transition | `409` with the current token |
| Route ID differs from body ID on edit | `400` |
| Unknown ID | `404` |

## Next steps

- See what is ready to run and when: [Dispatch board](08-dispatch-board.md).
- Report output against the order: [Confirmations with RW/PW](09-confirmations.md).
- Plan machine time: [Gantt schedule (Harmonogram)](12-gantt-schedule.md).
