# Deployment and operations

How the system is shipped, started, migrated, and backed up. The host
pipeline order is defined in [Architecture](architecture.md); the runtime
signals it emits are defined in [Observability](observability.md). The
operator-oriented companion documents are
[../deployment.md](../deployment.md) (hosting flow and environment table) and
[../production-runbook.md](../production-runbook.md) (production boot path,
raw EF commands, restore steps).

## Compose topology

`docker-compose.yml` is the production base. It publishes no host ports; port
publishing lives in `docker-compose.override.yml` (local development only)
and in the hosting layer (Coolify/Traefik in production).

| Service | Image (pinned by digest) | Role |
|---|---|---|
| `postgres` | `postgres:16-alpine@sha256:7218...` | Single shared database; `postgres_data` volume; `pg_isready` health check; 1 CPU / 1 GiB limit. |
| `seq` | `datalust/seq:2024.3@sha256:a9d2...` | Optional log sink for Serilog; `seq_data` volume; 1 CPU / 1 GiB limit. |
| `api` | Built from the root `Dockerfile` | Single-server mode: serves traffic and applies migrations on boot (`Boot__ApplyMigrations` defaults to `true`, `Boot__RunSeeders` to `false`); waits for healthy `postgres`; `curl` health check against `/health`; 2 CPU / 2 GiB limit. |
| `web` | Built from `AsistOff.MES.Web/Dockerfile` | Static SPA behind nginx; API base URL baked via `VITE_API_BASE_URL` with runtime override (see [Frontend](frontend.md)); 0.5 CPU / 512 MiB limit. |
| `migrate` | Same build as `api`, profiles `migrate,production` | Gated migration job: runs `dotnet AsistOff.MES.Gateway.dll --migrate-only` (applies migrations, optionally seeds, then exits without serving); requires `AUTH_ISSUER_SIGNING_KEY`; `restart: no`. |
| `api-prod` | Same build as `api`, profile `production` | Strict production server: both `Boot__*` flags pinned to `false` and starts only after `migrate` completes successfully. |
| `backup` | Same postgres image, profiles `production,backup` | Nightly `pg_dump` loop (`docker/backup/backup.sh`, custom format into the `pg_backups` volume, default interval 86400 s, keeps the newest 7 dumps). |

`docker-compose.override.yml` is applied automatically only for local runs:
it publishes `postgres` on 5432, `seq` on `${SEQ_PORT:-5341}`, `api` on 8080
in Development with migrate plus seed on boot, and `web` on
`${HTTP_PORT:-3000}`. `docker-compose.swarm.yml` defines only the local
agent-swarm helper (dashboard plus dispatcher) and is never part of a
deployment.

## Boot sequence and the gated migration job

`AsistOff.MES.Gateway/Program.cs` boots in this order: Serilog bootstrap
logger; configuration layering (`appsettings.*`, then User Secrets, then
environment variables, then command line, so `postgres__connectionString` and
similar `__`-separated overrides win); `ContainerSecretsValidator`
(Production refuses to boot with a missing or weak `POSTGRES_PASSWORD`
— minimum 16 characters, common values rejected — or an
`auth:IssuerSigningKey` decoding to fewer than 32 bytes); module discovery
and MediatR registration; health, exception handling, trusted-proxy,
observability, abuse-protection, and CORS wiring; middleware pipeline
(forwarded headers first, then correlation, tenant trace context, security
headers, request logging, exception handler, boot gate, HTTPS redirect, CORS,
rate limiter, authentication, authorization, metrics, health probes,
controllers).

The boot gate (`BootRunner` plus `BootOptions`, `Boot:` section) has
production-safe defaults (`ApplyMigrations: false`, `RunSeeders: false`):

- Normal boot migrates only when `Boot:ApplyMigrations` is true and seeds
  only when `Boot:RunSeeders` is true; requesting seeders without migrations
  fails fast at startup validation.
- `--migrate-only` always migrates, optionally seeds, logs what it did, and
  exits without serving traffic. The compose `migrate` service uses exactly
  this entrypoint, and `api-prod` waits for it — so production traffic never
  serves against an unmigrated schema.
- Seeders resolve through `ISeeder`: the development tenant seeder runs only
  in Development; the RBAC seeder is an idempotent permission-catalog
  backfill.

## Configuration and environment separation

| Area | Development | Production |
|---|---|---|
| Secrets | Dev defaults in `appsettings.Development.json`; validator is a no-op outside Production. | `ContainerSecretsValidator` fails fast on weak or missing `POSTGRES_PASSWORD` / `auth:IssuerSigningKey`; secrets arrive via environment (`.env` never committed, excluded by `.dockerignore`). |
| Boot | Migrate plus seed (`Boot` true/true) for a one-command local stack. | Base file pins both off; single-server `api` migrates on boot by default; strict `api-prod` never migrates and waits for the `migrate` job. |
| Swagger | Served (`IsDevelopment()` only). | Not mapped; the path falls through to a 404. |
| CORS | Empty `cors:allowedOrigins` reflects any origin with credentials (dev only). | Empty origins throw at startup; at least one explicit origin is required, credentials are allowed, and `Content-Disposition` plus `X-Correlation-ID` are exposed. |
| Auth hardening | JWT error details included; an explicit `auth:AuthenticationDisabled` escape hatch exists only outside Production as a documented dev and test aid (any other environment throws). | Full validation (signing key, issuer, audience); error details suppressed. |
| Telemetry simulator | Enabled (synthetic tag values for local OEE and trend views). | Disabled. |

Rate limiting is partitioned: only tenant self-provisioning (`POST
api/tenants`) and credential sign-in (`POST api/auth/sign-in`) run under
per-IP fixed windows (defaults 60 and 100 requests per minute); all other
traffic bypasses the limiter. Trusted-proxy headers are default-deny: with no
configured `TrustedProxies` networks, forwarded headers are ignored entirely
so spoofed values cannot move the throttle or the client IP.

## Backup and restore

Backups are logical `pg_dump` dumps; there is no WAL archiving or
point-in-time recovery.

| Mechanism | Location | Behavior |
|---|---|---|
| Containerized nightly backup | `docker/backup/backup.sh` via the `backup` service | Dumps custom-format files (`mes_YYYYMMDD-HHMMSS.dump`) into the `pg_backups` volume every `BACKUP_INTERVAL_SECONDS` (default 86400) and prunes to `BACKUP_RETENTION_COUNT` (default 7). |
| Host cron backup | `scripts/backup-db.sh`, `scripts/restore-db.sh` | Host-side `pg_dump` piped through gzip into `$BACKUP_DIR` (default `/opt/mes-backups`, 14-day retention); restore stops `api`/`web` first, replays with `ON_ERROR_STOP=1`, then verifies `/health/ready`. |

Restore replays the newest dump (`pg_restore -c` for container dumps) and
re-checks readiness before traffic returns. Blob storage is the database
itself (attachments persist as rows), so a database dump is a full system
backup; file-level volume copies are not part of the procedure.

## Container hardening

- **Non-root runtime:** the API image ends with `USER app` (UID 1654) and the
  web image with `USER nginx`; both copy files with ownership set at build
  time.
- **Pinned digests:** every runtime and prebuilt image (SDK, ASP.NET runtime,
  Node build image, nginx, postgres, Seq) is pinned by digest, so rebuilds
  cannot silently float to a new base.
- **Resource limits:** every long-lived service carries CPU and memory limits
  (only the one-shot `migrate` job is exempt); all services restart
  automatically except `migrate` (`restart: no`).
- **Secret exclusion:** `.dockerignore` excludes `.env`, git metadata, and
  build outputs; no secret is baked into either image. The web runtime config
  (`/config.js` rendered from `$API_BASE_URL`) carries only the public API
  origin, never credentials.
- **Request-size alignment:** nginx accepts up to 12 MiB so that the
  application's own 11 MiB attachment edge limit (see
  [Endpoint catalog](endpoints.md)) is the binding rejection with a `400`,
  not the proxy.

Known boundary: the local-only agent-swarm image is unpinned and runs as
root; it is a development helper, never deployed. There are no
`read_only`/`cap_drop` filesystem constraints and no image signing yet.
