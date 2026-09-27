# OEE dashboard

Overall Equipment Effectiveness (OEE) answers one question per
Work Center: of the time the shift plan gave us, how much became good
product? The dashboard at `/reports/oee` reads computed summaries —
nothing is stored; every number is derived at read time from
confirmations, the Work Center calendar and closed Downtime events.

OEE is the product of three 0–1 factors:

```text
OEE = Availability x Performance x Quality
```

| Factor | Meaning | Source |
|--------|---------|--------|
| Quality | Good units over total confirmed units | Confirmations on the Work Center in the window; Scrap-event rows never double-count |
| Availability | Run time over planned production time | Planned minutes from the calendar shifts overlapping the window, minus closed-Downtime overlap |
| Performance | Actual output rate over the theoretical maximum | Confirmed quantities against the ideal cycle time |

Reading any OEE endpoint needs only a signed-in user; there is no
write operation and no permission beyond authentication.

## Opening the dashboard

1. Open `/reports/oee` and pick a Work Center (Machine) plus a UTC
   time window (`fromUtc` must be before `toUtc`).
2. The summary call is `GET /api/oee?machineId=…&fromUtc=…&toUtc=…`.
   The ideal cycle time is resolved automatically as the minimum
   positive run time per unit across the recipe versions of the
   confirmed Production Orders in the window.
3. For an explicit ideal cycle time (for example a nameplate rate),
   use `GET /api/oee/snapshot` with `idealCycleTimeSeconds`.

Null rules (a missing factor is shown as "no data", never as zero):

- No confirmed units in the window: Quality is null.
- No planned calendar time in the window: Availability is null
  (a fully stopped shift is 0, not null: run time zero is computed).
- No resolvable ideal cycle time: Performance is null.

## Availability details

- Planned time is the overlap of the Work Center calendar shifts
  with the window — unplanned nights and weekends do not dilute the
  score.
- Only closed-Downtime overlap counts; still-open events are
  ignored, so an ongoing breakdown does not move the number until it
  is closed — see [Scrap and Downtime](10-scrap-downtime.md).
- A stop overlapping mostly unplanned time is clamped so run time
  never goes negative.

## Performance details

The summary resolves the ideal cycle time from your own confirmed
orders, so the rate always matches what the routing promises. Use
the snapshot variant when you want to pin a contractual rate and
compare shifts against it.

## Trend buckets

`GET /api/oee/trend` partitions the window into calendar-aligned
buckets and returns one snapshot per bucket:

| Parameter | Required | Rules |
|-----------|----------|-------|
| machineId | Yes | Unknown ids return `404` |
| fromUtc / toUtc | Yes | `fromUtc` before `toUtc`; windows longer than 93 days return `400` |
| idealCycleTimeSeconds | Yes | Explicit rate for every bucket |
| bucket | No | `Day` (UTC midnights) or `Week` (Monday 00:00 UTC); anything else returns `400` |

Buckets without planned time carry null factors, so a weekend with
no shifts renders as a gap, not as a zero.

Procedure:

1. Pick the same Work Center and a window of up to 93 days.
2. Choose `Day` for shift-level follow-up or `Week` for the
   management review.
3. Read dips against the loss Pareto below: a Quality dip points at
   scrap, an Availability dip at Downtime.

## Loss Pareto

`GET /api/oee/losses?machineId=…&fromUtc=…&toUtc=…` breaks the two
losses down by Reason code for Pareto analysis:

- Downtime minutes per Reason code (closed events only).
- Scrap quantities per Reason code (from confirmations).

Worked example: `CELL-3` shows OEE 0.61 with Availability 0.72,
Performance 0.95 and Quality 0.89. The losses call attributes most
Downtime minutes to the breakdown Reason code and most scrap to the
tolerance Reason code — the shift knows to fix the feeder first and
review the tolerance second.

## Tenant isolation

Work Centers, calendars, confirmations and Downtime events are
tenant-scoped: a machine id from another tenant resolves as `404`,
and summaries never mix tenants.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Missing machine or window | `400` |
| `fromUtc` not before `toUtc` | `400` |
| Unknown bucket, or window over 93 days (trend) | `400` |
| Unknown machine id | `404` |

## Next steps

- Capture the losses the Pareto shows: [Scrap and Downtime](10-scrap-downtime.md).
- Check whether the stops were failures: [Reliability](18-reliability.md).
- Watch the process drift behind a Quality dip: [SPC](16-spc-quality.md).
- See the stops as they happen: [Andon](17-andon.md).
