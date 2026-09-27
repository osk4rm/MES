# Health and observability

How operators know the system is alive, ready, and behaving: probes,
correlation IDs, OpenTelemetry wiring, and business meters. The pipeline
position of this middleware is defined in [Architecture](architecture.md);
the error envelope that carries the correlation ID is defined in
[Endpoint catalog](endpoints.md). Tenant tagging of traces is possible only
because of the ambient tenant described in [Multitenancy](multitenancy.md).

## Health probes: live versus ready

Health checks are registered once (`AddMesHealthChecks` in Shared
Infrastructure) and mapped as anonymous, rate-limit-free routes:

| Probe | Checks | Meaning |
|---|---|---|
| `GET /health/live` | `self` (dependency-free liveness) | 200 whenever the process serves, even when PostgreSQL is down. Orchestrators use this for restarts. |
| `GET /health/ready` | PostgreSQL reachability only | 200 when the database answers, else 503. orchestrators use this for traffic gating. |
| `GET /health` | Same as ready | Historical single endpoint, kept as a readiness alias. The compose `api` health check polls it. |

The response writer emits `{"status":"Healthy"}` as JSON on success and an
RFC 7807 problem body on failure; per-check exceptions and descriptions are
never serialized, so probes leak no host or user detail. Probes carry no user
or tenant — they are the only anonymous GET surface outside the tenant
provisioning and session endpoints.

## Correlation IDs

Every request carries exactly one correlation ID under the
`X-Correlation-ID` header (`CorrelationIds.HeaderName`), resolved by
`CorrelationIdMiddleware`, which runs first in the pipeline:

- An incoming value that parses as a GUID is echoed verbatim; anything else
  (missing, malformed) is replaced with a freshly generated GUID, so
  downstream systems always see a well-formed ID.
- The ID becomes `HttpContext.TraceIdentifier`, is echoed on the response
  (both eagerly and via `OnStarting`, so error responses carry it too), is
  pushed into the Serilog log context (`CorrelationId` property on every
  request log), and is attached to the active trace span plus W3C baggage.
- `TenantTraceContextMiddleware` runs next and stashes the ambient tenant ID
  for the OpenTelemetry enricher (the tenant is opaque tag data, never a log
  message).

Every error envelope carries the effective ID as its `traceId` extension, so
support can join a customer-visible error to its trace and its server logs
with a single value. The frontend preserves the contract from the other side:
the axios layer mints the ID when absent (`services/correlation.ts`), keeps
the caller's value across its single idempotent retry, and the error
boundary fallback screen shows the ID for copy-paste into a report. (See
[Frontend](frontend.md) for the client half.)

## OpenTelemetry: traces and metrics

`AddMesObservability` (Shared Infrastructure, `Observability:` section)
wires OpenTelemetry without ever throwing on missing or malformed
configuration — an unconfigured system simply exports nothing while W3C
propagation still works.

| Setting (`Observability:`) | Default | Effect |
|---|---|---|
| `ServiceName` / `ServiceVersion` | `AsistOff.MES` / `1.0.0` | Resource attributes on every span and metric. |
| `OtlpEndpoint` | empty (export disabled) | When set to a valid absolute URI (for example `Observability__OtlpEndpoint=http://otel-collector:4317`), traces and metrics export via OTLP. |
| `SamplingRatio` | `1.0` | Parent-based trace-ID-ratio sampler; out-of-range values are clamped to `[0,1]`. |
| `PrometheusEnabled` | `false` | When true, `UseMesObservability` maps `GET /metrics` to the Prometheus scraping endpoint; when false the path returns 404. |

Tracing instruments ASP.NET Core (exceptions recorded; responses enriched
with the tenant ID and correlation ID), outgoing `HttpClient` calls, and EF
Core queries. Metrics instrument ASP.NET Core, `HttpClient`, and the
application meter `AsistOff.MES` (below). Request logging stays on Serilog:
one record per request (method, path, status, elapsed) enriched with host,
scheme, client IP, user agent, correlation ID, tenant ID, and user name,
sunk to the console and to Seq (the Seq server URL is overridable per
environment; compose points it at the `seq` service).

## Business meters and OEE latency

`MesMeters` (Shared Abstractions, meter `AsistOff.MES`) records
manufacturing counters and latency histograms on the success path only, so
failed requests never inflate production KPIs:

| Instrument | Labels |
|---|---|
| `mes_confirmations_total` (counter) | `work_center_id`, `tenant_id` |
| `mes_scrap_total` (counter) | `work_center_id`, `reason_code`, `tenant_id` |
| `mes_downtime_events_total` (counter) | `work_center_id`, `reason_code`, `tenant_id` |
| `mes_oee_snapshot_duration_seconds` (histogram, seconds) | `work_center_id`, `tenant_id` |
| `mes_confirmation_duration_seconds` (histogram, seconds) | `work_center_id`, `tenant_id` |

The OEE snapshot histogram backs latency SLOs on the most expensive computed
query in the system: alert on its `_count`/`_sum`/`_bucket` series in PromQL.
Cardinality is guarded at the source: reason codes that are missing become
`unknown` and codes that are overlong or outside the controlled-vocabulary
grammar become `other` — lot codes, serials, operator identities, and tokens
are never used as label values.
