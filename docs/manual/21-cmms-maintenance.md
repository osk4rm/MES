# CMMS and preventive maintenance

The lightweight CMMS tracks two things: corrective work orders for
breakdowns that already happened, and preventive plans that raise
work orders before the next breakdown. Corrective orders live at
`/configuration/maintenance`; plans live at
`/configuration/maintenance-plans`.

Reads and the due list need only a signed-in user; every write —
orders, plans, evaluation and manual raising — needs the
`configuration.write` permission (`403` without it).

## Corrective work orders

API `GET /api/maintenance-work-orders` (paged browse),
`GET /api/maintenance-work-orders/{id}`,
`POST /api/maintenance-work-orders` (`201`),
`POST /api/maintenance-work-orders/{id}/start` (`200`),
`POST /api/maintenance-work-orders/{id}/complete` (`200`, carries
optional resolution notes),
`POST /api/maintenance-work-orders/{id}/cancel` (`200`). There are
no update or delete endpoints: an order moves forward through its
lifecycle or is cancelled, never edited away.

Lifecycle and priorities:

```text
Open  --start-->  InProgress  --complete-->  Done
  \--cancel-->  Cancelled   (from Open or InProgress)
```

| Priority | Meaning |
|----------|---------|
| Low / Medium | Plan into the next maintenance window |
| High / Critical | Stop-the-line candidates; schedule immediately |

Procedure:

1. Create the order with its code, title, Work Center, priority
   and report time; link the plan when the order comes from one
   (evaluation fills this automatically).
2. Start the order when work begins (Open → InProgress).
3. Complete with resolution notes when the cell is restored
   (→ Done). Only Done orders count as repairs in
   [Reliability](18-reliability.md) — complete the paperwork the
   same shift.
4. Cancel orders raised by mistake or superseded by another order.

## Preventive plans

API `GET /api/maintenance-plans` (paged browse),
`GET /api/maintenance-plans/due` (currently due plans),
`GET /api/maintenance-plans/{id}`,
`POST /api/maintenance-plans` (`201`),
`PUT /api/maintenance-plans/{id}` (`204`),
`DELETE /api/maintenance-plans/{id}` (`204`). A route/body ID
mismatch on update returns `400`.

| Field | Required | Rules |
|-------|----------|-------|
| code | Yes | Unique per tenant; duplicates return `409` |
| name | Yes | Human-readable label |
| machineId | Yes | The owning Work Center; unknown ids return `404` |
| triggerType | Yes | `Time` or `Meter` |
| intervalDays | For Time plans | Recurrence in days; the plan is due at `nextDueAt` |
| meterIntervalValue | For Meter plans | Recurrence in meter units against the supplied reading |
| nextDueAt | For Time plans | Next due time; past values mean overdue |
| lastCompletedAt | No | Set when a raised order completes |
| isActive | No | Inactive plans are skipped by evaluation |
| description | No | Free text |

Due state at a glance:

| Trigger | Due when | Not due when |
|---------|----------|--------------|
| Time | `nextDueAt` is now or in the past | `nextDueAt` is in the future |
| Meter | Current reading reached the interval since the last completion | Reading still below the threshold |
| Either | — | The plan is inactive |

## Evaluation, auto-raise and manual raise

`POST /api/maintenance-plans/evaluate-due` evaluates every due
preventive plan and raises one Open work order per due plan. Time
plans use `nextDueAt`; Meter plans use the supplied meter reading,
sent either in the body or as the `currentMeterReading` /
`meterReading` query value. `POST /api/maintenance-plans/{id}/raise-now`
raises one Open order from a single plan.

Both are idempotent: plans with a live Open or InProgress order
are skipped (evaluation) or return the existing live order
(raise-now) instead of duplicating it.

Procedure:

1. Open `/configuration/maintenance-plans` and check the due
   list (`GET /api/maintenance-plans/due`).
2. Run evaluation with the current meter readings for Meter
   plans; each due plan gains one Open order linked back to it.
3. Work the orders on `/configuration/maintenance`: start,
   complete with notes, or cancel.
4. For an urgent single plan, use raise-now instead of a full
   evaluation run.

Worked example: plan `PM-CELL3-30D` (Time, every 30 days) passes
its `nextDueAt`. Evaluation raises order `WO-118` linked to the
plan; the technician starts it, replaces the feeder belt,
completes it with notes — and the next MTBF bucket on
[Reliability](18-reliability.md) shows the effect.

## Tenant isolation

Orders and plans are tenant-scoped: ids from another tenant
resolve as `404`, evaluation only sees your tenant's plans, and
raised orders always belong to your tenant.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `configuration.write` (writes, evaluation, raise) | `403` |
| Missing code, machine, title or trigger data | `400` |
| Route/body ID mismatch on plan update | `400` |
| Unknown order, plan or machine id | `404` |
| Duplicate plan code or order code | `409` |
| Starting, completing or cancelling from the wrong status | `409` |

## Next steps

- Count the failures prevention avoids: [Scrap and Downtime](10-scrap-downtime.md).
- Watch MTBF recover after the work: [Reliability](18-reliability.md).
- Raise breakdowns from the floor instead: [Andon](17-andon.md).
- Prove the repair against the process: [SPC](16-spc-quality.md).
