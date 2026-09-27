# SPC (statistical process control)

Statistical Process Control watches a measured characteristic —
shaft diameter, fill weight, defect rate — and tells you when the
process drifts, before the drift becomes scrap. The flow has two
halves: a characteristic dictionary that defines the limits, and an
append-only measurement log with a control-chart evaluation.

Route `/production/spc-characteristics`. Reads need only a signed-in
user; every write below needs the `production.write` permission
(`403` without it).

## Characteristic dictionary

API `GET /api/spc-characteristics` (paged browse),
`GET /api/spc-characteristics/{id}`,
`POST /api/spc-characteristics` (`201`),
`PUT /api/spc-characteristics/{id}` (`204`),
`DELETE /api/spc-characteristics/{id}` (`204`). A route/body ID
mismatch on update returns `400`.

| Field | Required | Rules |
|-------|----------|-------|
| code | Yes | Unique per tenant; duplicates return `409` |
| name | Yes | Human-readable label |
| description | No | Free text |
| productId | No | Loose reference to the measured product; existence is not validated |
| machineId | No | Loose reference to the measuring Work Center; existence is not validated |
| chartType | No | `XbarR` (default), `XbarS`, `XmR`, `PChart` or `CChart` |
| nominalValue | No | Target value in `unit` |
| lowerSpecLimit / upperSpecLimit | No | Specification limits: what the customer accepts |
| lowerControlLimit / upperControlLimit | No | Control limits: what the process is capable of |
| sampleSize | No | Subgroup size, default 5 |
| unit | No | For example `mm`, `g`, `%` |
| isActive | No | Inactive characteristics stay readable but take no new measurements |

Specification limits and control limits answer different questions:
a point can be inside specification (acceptable to the customer)
while violating a control rule (the process has shifted and needs
attention). Record both whenever the routing knows them.

Procedure:

1. Create the characteristic with its code, chart type, both limit
   pairs, sample size and unit.
2. Deactivate (`isActive: false`) instead of deleting when a
   characteristic is retired, so history stays readable.

## Recording measurements

API `POST /api/spc-measurements` (`201`),
`GET /api/spc-measurements` (paged browse),
`GET /api/spc-measurements/{id}`,
`GET /api/spc-measurements/chart`. There are intentionally no
update or delete endpoints: measurements are immutable once
recorded, so a correction is a new measurement, never an edit.

Each record carries the characteristic id, the measured value, the
measurement time and the sample context. Recording needs
`production.write`; browsing the log and the chart needs only a
signed-in user.

## Control chart and out-of-control evaluation

`GET /api/spc-measurements/chart?characteristicId=…` returns one
ordered point per measurement with:

| Field | Meaning |
|-------|---------|
| IsOutOfControl | Rule 1: the point lies beyond the control limits |
| IsOutOfSpec | The point lies outside the specification limits |
| ViolatedRules | Which Western Electric rules (1–4) this point completes |
| OutOfControlCount | Total rule-1 points in the window |
| Rule2ViolationCount / Rule3ViolationCount / Rule4ViolationCount | Totals per rule |

The Western Electric rules 1–4, evaluated in measurement order:

```text
Rule 1:  one point beyond the control limits (LCL/UCL)
Rule 2:  2 of the last 3 points beyond 2-sigma, same side
Rule 3:  4 of the last 5 points beyond 1-sigma, same side
Rule 4:  the last 8 points on one side of the centre line
```

Rules 2–4 flag the point completing each window, so a single
drifting run lights up progressively: first rule 4, then rule 3,
then rule 2, then rule 1 as points cross the limits.

Procedure:

1. Open the characteristic and its chart for the shift window.
2. Rule 1 or out-of-spec: stop and contain — quarantine the lot
   and record scrap per [Scrap and Downtime](10-scrap-downtime.md).
3. Rules 2–4 only: schedule a process check (tool wear, temperature,
   material batch) before the drift reaches the limits.
4. When the check finds an assignable cause, fix it and keep
   measuring: the chart proves the fix held.

## Tenant isolation

Characteristics and measurements are tenant-scoped: an id from
another tenant resolves as `404`, and charts never mix tenants.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `production.write` (writes) | `403` |
| Missing code, value or characteristic | `400` |
| Route/body ID mismatch on update | `400` |
| Unknown characteristic or measurement id | `404` |
| Duplicate characteristic code | `409` |

## Next steps

- Confirm what the shift produced while the drift lasted: [Confirmations](09-confirmations.md).
- Attribute the scrap the drift caused: [Scrap and Downtime](10-scrap-downtime.md).
- See the headline effect on good output: [OEE dashboard](15-oee-dashboard.md).
- Raise the maintenance the check calls for: [CMMS and preventive maintenance](21-cmms-maintenance.md).
