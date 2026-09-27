# Deploy, backup and operations

This chapter is for the person who keeps the system running:
how it is shipped, how it starts, how it is backed up, how its
containers are hardened, and how to fill a fresh stack with demo
data. End users stop here; operators continue top to bottom.

## How it is shipped

`docker-compose.yml` is the production base. It publishes no host
ports; port publishing lives in the local override and in the
hosting layer.

| Service | Role |
|---------|------|
| `postgres` | Single shared database with a data volume and a readiness check; CPU and memory limited |
| `seq` | Optional log sink for the request logs |
| `api` | Single-server mode: serves traffic and applies migrations on boot by default; waits for healthy `postgres`; health-checked against `/health` |
| `web` | Static SPA behind nginx; the API origin is baked at build with a runtime override |
| `migrate` | Gated migration job: applies migrations (optionally seeds), then exits without serving |
| `api-prod` | Strict production server: never migrates itself and starts only after `migrate` succeeds |
| `backup` | Nightly database-dump loop into a backups volume |

The local override publishes the database, Seq, API (Development,
migrate plus seed on boot) and web ports for a one-command local
stack.

## Starting, migrating and configuring

Boot order: logging, layered configuration (files, then secrets,
then environment, then command line — so `__`-separated
environment overrides win), secret validation, module wiring,
middleware pipeline, controllers.

| Area | Development | Production |
|------|-------------|------------|
| Boot flags | Migrate plus seed for a one-command stack | Strict server never migrates; the gated job migrates first — traffic never serves an unmigrated schema |
| Secrets | Dev defaults; the validator is a no-op | Missing or weak `POSTGRES_PASSWORD` (minimum 16 characters, common values rejected) or a short JWT signing key (under 32 bytes) refuses to boot |
| Swagger | Served | Not mapped; the path falls through to `404` |
| CORS | Any origin reflected with credentials (dev only) | At least one explicit origin required; credentials allowed |
| Telemetry simulator | Enabled (synthetic values for local dashboards) | Disabled |
| Auth escape hatch | An explicit disable flag exists outside Production as a dev and test aid | Full validation, error details suppressed |

Rate limiting is partitioned: only tenant self-registration and
credential sign-in run under per-IP fixed windows; all other
traffic bypasses the limiter.

## Backup and restore

Backups are logical database dumps; there is no point-in-time
recovery.

| Mechanism | Behavior |
|-----------|----------|
| Containerized nightly backup | Dumps custom-format files (`mes_YYYYMMDD-HHMMSS.dump`) into the backups volume on its interval and prunes to the newest 7 |
| Host cron backup (`scripts/backup-db.sh`, `scripts/restore-db.sh`) | Host-side compressed dumps into the backup directory (14-day retention); restore stops the app and web first, replays with stop-on-error, then verifies readiness |

Restore procedure:

1. Stop the app and web services so no traffic writes mid-restore.
2. Replay the newest dump.
3. Re-check `/health/ready` until it returns `200` before
   returning traffic.

Blob storage is the database itself (attachments persist as
rows), so a database dump is a full system backup — files
included. File-level volume copies are not part of the procedure.

## Container and runtime hardening

- Non-root runtime: the API image runs as the `app` user and the
  web image as `nginx`; file ownership is set at build time.
- Pinned image digests: rebuilds cannot silently float to a new
  base image.
- CPU and memory limits on every long-lived service with
  automatic restarts (except the one-shot migration job).
- Secret exclusion: environment files, git metadata and build
  outputs never enter either image; the web runtime config
  carries only the public API origin, never credentials.
- Request-size alignment: nginx accepts up to 12 MiB so that the
  application's own 11 MiB attachment edge limit is the binding
  rejection (`400`), not the proxy.

## Demo data seed script

`scripts/seed/seed-demo-data.ps1` fills a running stack with a
report-ready dataset — master data, recipes with routing and BOM,
orders across the whole lifecycle, lots, confirmations with
RW/PW, genealogy, losses, Andon, SPC, telemetry, Kanban and
maintenance — so every screen and report in this manual is
populated. It drives the public HTTP API (no direct database
access).

| Parameter | Default | Meaning |
|-----------|---------|---------|
| `BaseUrl` | `http://localhost:8080` | Gateway URL (compose); use the dev-profile URL for `dotnet run` |
| `Email` / `Password` | `admin@dev.local` / `Passw0rd!` | Seed-run login |
| `Tag` | clock-derived | Per-run code prefix (`SD<tag>`), so repeat runs accumulate instead of colliding |
| `Orders` | `24` | Approximate finished-goods order count |
| `Days` | `10` | Backfill window for Downtime, scrap, SPC and telemetry history |
| `NewTenant` | off | Self-register a fresh tenant so every report starts clean |
| `DryRun` | off | Print the plan without writing anything |

Time model: the API refuses confirmations reported before the
order's release, so production output lands "now" (dense enough
for the OEE and loss panels), while Downtime, scrap, SPC and
telemetry timestamps are backdated across `Days` so the
reliability, trend, SPC and telemetry charts have history.

Procedure:

```powershell
pwsh -File scripts/seed/seed-demo-data.ps1 -NewTenant
pwsh -File scripts/seed/seed-demo-data.ps1 -BaseUrl http://localhost:5243 -Orders 40 -Days 14
```

1. Start the local stack first — the script needs a running API.
2. Run with `-NewTenant` for a clean demo tenant, or against the
   dev login to accumulate prefixed data.
3. Open `/reports/oee` and `/reports/reliability`: the charts
   render immediately, which is the fastest proof the seed
   worked.

## Error cases

| Situation | Result |
|-----------|--------|
| Weak or missing production secrets | Process refuses to boot |
| Traffic against an unmigrated schema | Prevented by the gated migration job ordering |
| Restore without readiness re-check | Blocked by procedure: gate on `/health/ready` before traffic returns |
| Seed against a stopped stack | Connection failure; start the stack first |

## Next steps

- Liveness and readiness signals used above: [Health, correlation and observability](24-observability-health.md).
- Filing access for the new tenant: [Tenants, users and roles](22-admin-tenants-users-roles.md).
- From the empty seeded stack back to chapter one: [Getting started](01-getting-started.md).
