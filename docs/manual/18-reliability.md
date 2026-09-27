# Reliability (MTBF/MTTR)

The reliability views answer whether a Work Center keeps failing
and how fast maintenance restores it. Two numbers per Work Center
over a UTC window:

| KPI | Meaning | Source |
|-----|---------|--------|
| MTBF (Mean Time Between Failures) | Average operating time between failures | Closed Downtime-event overlap (failures) |
| MTTR (Mean Time To Repair) | Average time to restore the Work Center after a failure | Done maintenance work orders (repairs) |

Route `/reports/reliability`. All three endpoints are read-only and
need only a signed-in user; nothing is stored and no permission is
required.

## Snapshot

`GET /api/reliability/snapshot?machineId=…&fromUtc=…&toUtc=…`
returns the per-Work Center MTBF/MTTR snapshot for the window.

Procedure:

1. Open `/reports/reliability` and pick a Work Center plus a UTC
   window.
2. Read MTBF first: a falling MTBF across windows means the cell
   fails more often, even when each repair is quick.
3. Read MTTR second: a rising MTTR means repairs take longer —
   missing spares, missing skills, or an aging asset.
4. Only closed Downtime events count as failures and only Done
   work orders count as repairs, so finish the paperwork (close
   the event, complete the order) before trusting the numbers —
   see [Scrap and Downtime](10-scrap-downtime.md) and
   [CMMS and preventive maintenance](21-cmms-maintenance.md).

## Trend

`GET /api/reliability/trend` takes the same window plus a `bucket`
(`Day` with UTC-midnight breaks, or `Week` with Monday 00:00 UTC
breaks; anything else returns `400`) and returns one snapshot per
bucket.

Procedure:

1. Pick a window covering several weeks of production.
2. Choose `Week` for the maintenance review: a staircase of
   falling MTBF buckets is the signal to schedule preventive
   work before the failure becomes a breakdown.
3. Drill a bad bucket back into the snapshot and the Downtime
   list to name the failing component.

## Fleet comparison

`GET /api/reliability/fleet?fromUtc=…&toUtc=…&departmentId=…`
compares MTBF/MTTR across the active Work Centers, worst first,
optionally filtered to one department.

Procedure:

1. Open the fleet view for the last month without picking a
   machine.
2. The top rows are the maintenance backlog ordered by pain:
   start preventive plans there.
3. Filter by department when one cost center owns the budget.

## Tenant isolation

Downtime events and work orders are tenant-scoped: a machine id
from another tenant resolves as `404`, and fleet rows never mix
tenants.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Missing machine or window | `400` |
| Unknown bucket (trend) | `400` |
| Unknown machine id | `404` |

## Next steps

- Record the failures the KPIs count: [Scrap and Downtime](10-scrap-downtime.md).
- Raise the repairs the KPIs count: [CMMS and preventive maintenance](21-cmms-maintenance.md).
- See the output cost of the failures: [OEE dashboard](15-oee-dashboard.md).
- Catch the failure while it happens: [Andon](17-andon.md).
