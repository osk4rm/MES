# Kanban pull

Kanban runs material replenishment as a pull: a fixed-quantity
container (card) circulates in a loop between a consumer and a
supplier, and a new supply is ordered only when a card is consumed.
The board at `/production/kanban` shows every loop with its cards;
the API underneath enforces the pull rules and the WIP limit.

Reads need only a signed-in user; every write below needs the
`production.write` permission (`403` without it).

## Loops

API `GET /api/kanban/loops` (paged browse),
`GET /api/kanban/loops/{id}`,
`POST /api/kanban/loops` (`201`),
`PUT /api/kanban/loops/{id}` (`204`, carries card quantity,
cards in circulation, active flag and notes directly),
`DELETE /api/kanban/loops/{id}` (`204`).

| Field | Required | Rules |
|-------|----------|-------|
| code | Yes | Unique per tenant; duplicates return `409` |
| product / warehouses | Yes | What flows, and from where to where |
| cardQuantity | Yes | Fixed quantity per container |
| cardsInCirculation | Yes | The WIP limit: at most this many cards may be Ordered at once |
| isActive | No | Inactive loops keep history but take no replenishments |
| notes | No | Free text |

Procedure:

1. Open `/production/kanban` and create the loop with its product,
   source and target warehouses, card quantity and cards in
   circulation.
2. Size cards in circulation from daily demand times lead time
   divided by card quantity — the loop holds that many containers,
   no more.
3. Deactivate instead of deleting a retired loop so card history
   stays readable.

## Card registry

API `GET /api/kanban/loops/{loopId}/cards` (paged browse),
`GET /api/kanban/loops/{loopId}/cards/{id}`,
`POST /api/kanban/loops/{loopId}/cards` (`201`, carries the card
number and notes),
`DELETE /api/kanban/loops/{loopId}/cards/{id}` (`204`).

Card numbers are unique per loop (for example `LOOPCODE-01`).
Reads pin the loop server-side: a card id read under the wrong
loop id returns `404`, and deleting under a mismatched loop id
returns `404` without touching the card.

Procedure:

1. Register one card per physical container circulating in the
   loop.
2. Remove lost or damaged containers with delete so the registry
   matches the floor.

## Pull transitions and the WIP limit

```text
Full  --consume-->  Empty  --order-->  Ordered  --replenish-->  Full
```

| Action | API | Guard |
|--------|-----|-------|
| Consume | `POST /api/kanban/cards/{id}/consume` (`200`) | Card must be Full, else `409` |
| Order | `POST /api/kanban/cards/{id}/order` (`200`) | Card must be Empty, else `409`; ordered cards must stay below the loop's cards-in-circulation limit, else `409` |
| Replenish | `POST /api/kanban/cards/{id}/replenish` (`200`) | Card must be Ordered, else `409`; the loop must be active, else `409` |

Procedure on the board:

1. The consumer empties a container: consume its card
   (Full → Empty).
2. The consumer signals demand: order the empty card
   (Empty → Ordered). When the loop already holds its limit of
   ordered cards, the call returns `409` — that is the WIP limit
   doing its job, not an error to retry: expedite an ordered card
   first.
3. The supplier fills the container: replenish the ordered card
   (Ordered → Full).

Worked example: loop `SUP-AXLE` circulates 4 cards of 50 axles.
The line consumes card `SUP-AXLE-02` (Full → Empty) and orders it
(Empty → Ordered). Two more cards are already Ordered, so the
loop holds 3 of its 4 allowed — the fourth order still succeeds,
the fifth returns `409` until a replenishment lands.

## Tenant isolation

Loops and cards are tenant-scoped: an id from another tenant
resolves as `404`, and the board never mixes tenants.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `production.write` (writes) | `403` |
| Missing code, quantity or card number | `400` |
| Card read or delete under the wrong loop id | `404` |
| Unknown loop or card id | `404` |
| Duplicate loop code, or duplicate card number in the loop | `409` |
| Wrong status for the transition, WIP limit reached, or replenish on an inactive loop | `409` |

## Next steps

- Signal a material shortage while a card is late: [Andon](17-andon.md).
- Confirm the output the replenished material enables: [Confirmations](09-confirmations.md).
- Trace which lots the cards carried: [Lots and genealogy](11-lots-genealogy.md).
- See stock effects of consumption: [Confirmations with RW/PW](09-confirmations.md).
