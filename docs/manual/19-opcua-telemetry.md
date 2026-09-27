# OPC UA telemetry

Telemetry connects the shopfloor to the system: tag values flow
from machines (or the local simulator) into an append-only reading
log, and the dashboards show what each Work Center reports right
now — including which tags went stale.

Three surfaces work together:

| Surface | Route | Purpose |
|---------|-------|---------|
| Tag dictionary | `/production/telemetry` | Which tags exist per Work Center |
| Connection registry | `/production/opcua-connections` | Which OPC UA servers are polled |
| Live dashboard | `/production/telemetry-dashboard` | Latest values plus stale flags |

Reads and the connection test need only a signed-in user; every
dictionary and connection write needs the `production.write`
permission (`403` without it).

## Tag dictionary

API `GET /api/telemetry-tags` (paged browse),
`GET /api/telemetry-tags/{id}`,
`POST /api/telemetry-tags` (`201`),
`PUT /api/telemetry-tags/{id}` (`204`),
`POST /api/telemetry-tags/{id}/toggle` (`200`, enable/disable),
`DELETE /api/telemetry-tags/{id}` (`204`). A route/body ID
mismatch on update returns `400`.

Each tag belongs to one Work Center and carries its node address,
data type, polling setup and enabled flag. Tags are tenant-scoped:
an id from another tenant resolves as `404`.

Procedure:

1. Open `/production/telemetry` and create one tag per machine
   signal you need (temperature, pressure, counter).
2. Keep the tag enabled only while something polls it — disabled
   tags take no readings and render as "never" on the dashboard.
3. Use toggle for a temporary sensor outage instead of deleting:
   deletion removes the dictionary row while its readings stay.

## Readings log

API `GET /api/telemetry-readings` (browse),
`GET /api/telemetry-readings/trend?tagId=…&take=…` (last-N
readings, oldest first, take clamped to 1–200, for sparklines),
`GET /api/telemetry-readings/{id}`,
`POST /api/telemetry-readings` (`201` submit). There are
intentionally no update or delete endpoints: machine readings are
immutable once recorded.

Browsing and the trend need only a signed-in user; submitting a
reading needs `production.write`.

CSV export: send `Accept: text/csv` on the browse call to download
the same filters as a file (at most 5000 data rows). Larger
history needs narrower filters, not retries.

## Stale dashboard and the simulator

`GET /api/telemetry-tags/status` reports every tag of your tenant
with its latest reading plus a stale flag: a tag is stale when no
reading arrived within twice the simulator poll interval. The
dashboard at `/production/telemetry-dashboard` renders each tag as
a card — recent, stale, or never — with text badges, never color
alone.

The telemetry simulator writes synthetic values on the local stack
so the OEE and trend views have data to show. It is enabled in
Development and disabled in Production: a stale fleet on a local
stack means the simulator is off, while a stale tag in Production
means its OPC UA source stopped delivering.

Procedure:

1. Open `/production/telemetry-dashboard` at shift start.
2. A stale card means "no fresh data", not "bad value": check the
   connection below before trusting the last value.
3. A never card means the tag was never polled: enable it and
   check its connection mapping.

## Connection registry and polling

API `GET /api/opcua-connections` (paged browse),
`GET /api/opcua-connections/status`,
`GET /api/opcua-connections/{id}`,
`POST /api/opcua-connections` (`201`),
`PUT /api/opcua-connections/{id}` (`204`),
`POST /api/opcua-connections/{id}/toggle` (`200`,
enable/disable polling),
`POST /api/opcua-connections/{id}/test` (`200`, validates the
connection shape only — it never writes to the server),
`DELETE /api/opcua-connections/{id}` (`204`).

Status, reads and the connection test need only a signed-in user;
creating, editing, toggling and deleting need `production.write`.

Sensitive projection: the raw `LastError` provider message on the
browse, get and status reads is returned only to callers holding
`production.write`; callers with the read-only role receive `null`
(the status view shows the empty marker) while liveness flags,
tag counts and totals stay visible to everyone.

Procedure:

1. Open `/production/opcua-connections` and register the server
   with its endpoint, security settings and poll interval.
2. Run the connection test: it checks the shape of the
   configuration, so a passing test with stale tags means the
   mapping (node addresses) is wrong, not the server.
3. Toggle polling on; the tag statuses turn from never to recent
   within two poll intervals.
4. Toggle polling off for planned PLC maintenance instead of
   deleting the connection, so history and mapping survive.

## Tenant isolation

Tags, readings and connections are tenant-scoped: ids from another
tenant resolve as `404`, the status call lists only your tenant's
tags, and tenants never see each other's machine data.

## Error cases

| Situation | Result |
|-----------|--------|
| Not signed in | `401` |
| Signed in without `production.write` (writes, submits) | `403` |
| Missing tag, machine or reading value | `400` |
| Route/body ID mismatch on update | `400` |
| Trend take outside 1–200 | `400` (clamped or rejected per the endpoint contract) |
| Unknown tag, reading or connection id | `404` |
| CSV export over 5000 rows | Narrow the filters; the export is capped |

## Next steps

- Turn a stale temperature into shopfloor action: [Andon](17-andon.md).
- Watch quality drift behind a sensor value: [SPC](16-spc-quality.md).
- See the output cost of a sensor outage: [OEE dashboard](15-oee-dashboard.md).
- Prove the numbers end to end: [Health, correlation and observability](24-observability-health.md).
