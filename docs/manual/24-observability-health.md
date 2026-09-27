# Health, correlation and observability

When something looks wrong — a slow page, a failed save, a stale
dashboard — this chapter tells you where the system proves it is
alive, how to join one error to its logs, and which metrics the
operations team watches. All three probes and the metrics
endpoint are anonymous infrastructure surface: they carry no user
or tenant.

## Health probes

| Probe | Checks | Meaning |
|-------|--------|---------|
| `GET /health/live` | Dependency-free self check | `200` whenever the process serves, even when PostgreSQL is down — orchestrators use it for restarts |
| `GET /health/ready` | PostgreSQL reachability only | `200` when the database answers, else `503` — orchestrators use it for traffic gating |
| `GET /health` | Same as ready | Historical single endpoint kept as a readiness alias; the compose `api` health check polls it |

Success bodies are `{"status":"Healthy"}` JSON; failures render a
problem body that never serializes per-check exceptions or
descriptions, so probes leak no host or user detail. Probes are
rate-limit-free.

Procedure:

1. The app does not load at all: open `/health/live`. A `200`
   means the process serves and the fault is downstream (usually
   the database or the reverse proxy).
2. Logins fail but the shell loads: open `/health/ready`. A
   `503` means PostgreSQL is unreachable — check the database
   container, not the app.
3. After any restart or restore, wait for `/health/ready` to
   return `200` before letting traffic back — the restore runbook
   in [Deploy, backup and operations](25-deploy-backup-ops.md)
   gates on exactly this.

## Correlation IDs

Every request carries exactly one correlation ID under the
`X-Correlation-ID` header:

- An incoming value that parses as a GUID is echoed verbatim;
  anything else (missing, malformed) is replaced with a freshly
  generated GUID, so downstream always sees a well-formed ID.
- The ID is echoed on every response (including error
  responses), becomes the server trace id, is pushed into the log
  context (`CorrelationId` on every request log), and is attached
  to the active trace span.
- Every error envelope carries the effective ID as its `traceId`
  extension; the frontend mints the ID when absent, keeps it
  across its single idempotent retry, and the error screen shows
  it for copy-paste into a report.

Procedure for reporting a failure:

1. Reproduce the error and copy the correlation ID from the
   error screen (or the `X-Correlation-ID` response header).
2. Send that single value to support: it joins the
   customer-visible error to its trace and its server logs.
3. Searching logs by anything else (user name, approximate time)
   is the slow path — the correlation ID is the fast one.

## OpenTelemetry metrics and traces

Telemetry is configured under `Observability:` without ever
throwing on missing values — an unconfigured system simply
exports nothing while request correlation still works.

| Setting | Default | Effect |
|---------|---------|--------|
| `ServiceName` / `ServiceVersion` | `AsistOff.MES` / `1.0.0` | Resource attributes on every span and metric |
| `OtlpEndpoint` | empty (export disabled) | Valid absolute URI exports traces and metrics via OTLP |
| `SamplingRatio` | `1.0` | Parent-based trace sampling, clamped to 0–1 |
| `PrometheusEnabled` | `false` | `true` maps `GET /metrics` for Prometheus scraping; `false` returns `404` |

Tracing covers ASP.NET Core (responses enriched with the tenant
and correlation IDs), outgoing HTTP calls and EF Core queries.
Request logging stays on Serilog: one record per request
(method, path, status, elapsed) enriched with host, scheme,
client IP, user agent, correlation ID, tenant ID and user name,
sunk to the console and to Seq.

Business meters (meter `AsistOff.MES`, recorded on the success
path only so failed requests never inflate production KPIs):

| Instrument | Labels |
|------------|--------|
| `mes_confirmations_total` (counter) | Work Center, tenant |
| `mes_scrap_total` (counter) | Work Center, Reason code, tenant |
| `mes_downtime_events_total` (counter) | Work Center, Reason code, tenant |
| `mes_oee_snapshot_duration_seconds` (histogram) | Work Center, tenant |
| `mes_confirmation_duration_seconds` (histogram) | Work Center, tenant |

The OEE snapshot histogram backs latency SLOs on the most
expensive computed query in the system — alert on its
`_count`/`_sum`/`_bucket` series. Cardinality is guarded at the
source: missing Reason codes become `unknown`, overlong or
off-grammar codes become `other`; lot codes, serials, operator
identities and tokens are never label values.

## Tenant isolation

Probes and metrics intentionally cross tenant boundaries in the
other direction: they expose no tenant data at all. Tenant
identity appears only as an opaque tag on traces and metric
series, never in a log message.

## Error cases

| Situation | Result |
|-----------|--------|
| Database down (`/health/ready`, `/health`) | `503` with a sanitized problem body |
| Metrics disabled (`/metrics`) | `404` |
| Any API error | Problem body with the `traceId` extension carrying the correlation ID |

## Next steps

- Restarting and restoring around these probes: [Deploy, backup and operations](25-deploy-backup-ops.md).
- Populating the dashboards these meters watch: [OEE dashboard](15-oee-dashboard.md).
- The demo data behind a fresh local stack: the seed script in [Deploy, backup and operations](25-deploy-backup-ops.md).
- Session and permission failures by code: [Tenants, users and roles](22-admin-tenants-users-roles.md).
