# Andon signals

An Andon signal is the shopfloor alarm bell: an abnormal condition —
a breakdown, a quality suspicion, a material shortage — raised where
it happens so supervisors and maintenance see it immediately. The
back-office view is `/production/andon`; the same signals surface in
the [Operator panel](14-operator-panel.md) queue for the affected
Work Centers.

Reads need only a signed-in user; every write below needs the
`production.write` permission (`403` without it).

## Signal lifecycle

```text
Active  --acknowledge-->  Acknowledged  --resolve-->  Resolved
```

| Status | Meaning |
|--------|---------|
| Active | Raised, nobody owns it yet |
| Acknowledged | Someone accepted ownership; work on the cause is expected |
| Resolved | The cause is cleared |

API `GET /api/andon-signals` (paged browse),
`GET /api/andon-signals/{id}`,
`POST /api/andon-signals` (`201` raise),
`POST /api/andon-signals/{id}/acknowledge` (`200`),
`POST /api/andon-signals/{id}/resolve` (`200`),
`PUT /api/andon-signals/{id}` (corrections),
`DELETE /api/andon-signals/{id}` (corrections). A route/body ID
mismatch on update returns `400`.

## Raising a signal

| Field | Required | Rules |
|-------|----------|-------|
| machineId | Yes | The Work Center with the abnormal condition; unknown ids return `404` |
| category | Yes | `Downtime`, `Quality`, `Material` or `Other` |
| raisedAt | Yes | When the condition started |
| reasonCodeId | No | A Reason code from the dictionary — see [Scrap and Downtime](10-scrap-downtime.md) |
| description | No | What the operator observes |

Procedure:

1. Raise with the machine, a category and the raised time; add the
   Reason code when the cause is already known.
2. Acknowledge when someone takes ownership — the signal stays
   visible but stops paging for a new owner.
3. Resolve when the cause is cleared; the resolved time defaults to
   now when omitted.

Signals render with category and severity text — never color alone —
so the board stays readable for color-blind operators.

## Working the board

1. Open `/production/andon` at shift start: every Active signal is
   an unresolved abnormal condition on the floor.
2. Filter by Work Center to see what blocks one cell, or by
   category to see all material shortages at once.
3. Acknowledge what you take on; resolve what you cleared.
4. A Downtime-category signal normally pairs with a Downtime
   event (`POST /api/downtime-events`, then close) so the stop
   also counts against Availability — see
   [Scrap and Downtime](10-scrap-downtime.md).
5. A Quality-category signal pairs with an SPC check — see
   [SPC](16-spc-quality.md); a Material-category signal pairs with
   a Kanban pull — see [Kanban](20-kanban.md).

Worked example: the feeder on `CELL-3` jams. The operator raises a
Material Andon, captures a Downtime event with the breakdown Reason
code, clears the jam, closes the Downtime event, resolves the Andon
and confirms the next quantity. The board, the OEE losses and the
reliability snapshot all agree on what happened.

## Tenant isolation

Signals are tenant-scoped: a signal id from another tenant resolves
as `404`, and the board never mixes tenants.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `production.write` (writes) | `403` |
| Missing machine, category or raised time | `400` |
| Route/body ID mismatch on update | `400` |
| Unknown signal or machine id | `404` |
| Acknowledging or resolving twice (state conflict) | `409` |

## Next steps

- Count the stop the signal describes: [Scrap and Downtime](10-scrap-downtime.md).
- Report output around the stop: [Operator panel](14-operator-panel.md).
- Hand the open signals to the next shift: [Shift handover](13-shift-handover.md).
- Turn a breakdown signal into lasting prevention: [CMMS and preventive maintenance](21-cmms-maintenance.md).
