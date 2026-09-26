# Production boot, migrate, seed, backup, and restore

This runbook covers safe production rollouts for AsistOff MES (issue #257).
Development boot is unchanged (`docker compose up` migrates + seeds the dev
tenant); everything below applies to the `production` / `migrate` / `backup`
compose profiles.

## Rule #1: production boot never migrates or seeds

The Gateway only applies EF Core migrations when `Boot:ApplyMigrations` is
`true`, and only runs `ISeeder`s when `Boot:RunSeeders` is `true`
(`BootOptionsValidator` rejects `RunSeeders=true` without
`ApplyMigrations=true`). Both default to `false`, and
`appsettings.Production.json` pins them off, so a production boot serves
traffic after logging:

```text
Skipped EF Core migrations on boot (Boot:ApplyMigrations=false). ...
Skipped seeders on boot (Boot:RunSeeders=false).
```

Environment overrides (both profiles also accept them):

| Variable | Default | Meaning |
|----------|---------|---------|
| `Boot__ApplyMigrations` | `false` | Apply pending migrations on boot |
| `Boot__RunSeeders` | `false` | Run `ISeeder`s after a successful migrate |
| `BOOT_RUN_SEEDERS` | `false` | Compose passthrough for the migrate job's `Boot__RunSeeders` |
| `BACKUP_INTERVAL_SECONDS` | `86400` | Seconds between dumps (nightly) |
| `BACKUP_RETENTION_COUNT` | `7` | How many recent dumps the backup service keeps |

## Migrate: upgrade the schema before the app starts

Schema upgrades run as a one-shot `migrate` job that must complete before
`api-prod` starts (`depends_on: service_completed_successfully`).

```bash
# Full production stack: migrate runs first, api-prod + backup follow.
docker compose --profile production up --build -d

# Or run just the migration job (e.g. in a deploy pipeline step):
docker compose --profile migrate run --rm migrate
```

Raw EF Core alternative (applies both contexts; the Gateway assembly is the
startup project):

```bash
dotnet ef database update \
  --project AsistOff.MES.Shared.Infrastructure \
  --startup-project AsistOff.MES.Gateway \
  --context DefaultContext

dotnet ef database update \
  --project AsistOff.MES.Multitenancy \
  --startup-project AsistOff.MES.Gateway \
  --context MultitenancyDbContext
```

Verify: `migrationBuilder` output ends with `Done`, and
`GET /health/ready` returns `200` once `api-prod` is up.

## Seed: opt-in backfills only

Seeders run only inside the migrate job (or a boot with both flags on), never
on a plain production boot:

```bash
BOOT_RUN_SEEDERS=true docker compose --profile migrate run --rm migrate
```

This runs the idempotent backfills (e.g. `RbacSeeder` provisions parity RBAC
rows for pre-existing tenants). `DevTenantSeeder` additionally requires the
`Development` environment, so it never provisions tenants in production.

## Backup: nightly pg_dump

The `production` profile includes the `backup` service
(`postgres:16-alpine` + `docker/backup/backup.sh`): every
`BACKUP_INTERVAL_SECONDS` it writes a timestamped custom-format dump
(`mes_YYYYMMDD-HHMMSS.dump`) to the `pg_backups` volume and deletes all but
the newest `BACKUP_RETENTION_COUNT` dumps.

```bash
# Standalone (no production stack): start just the backup loop.
docker compose --profile backup up -d backup

# Hourly dumps keeping 48 copies:
BACKUP_INTERVAL_SECONDS=3600 BACKUP_RETENTION_COUNT=48 \
  docker compose --profile production up -d

# List available dumps:
docker volume inspect pg_backups  # or:
docker compose --profile production exec backup ls -lt /backups
```

## Restore: replay a dump into the database

```bash
# 1. Stop everything writing to the database.
docker compose --profile production stop api-prod backup

# 2. Restore the chosen dump (custom format, clean + recreate objects).
docker compose --profile production run --rm \
  -v pg_backups:/backups postgres:16-alpine \
  pg_restore -h postgres -p 5432 -U "${POSTGRES_USER:-admin}" \
  -d "${POSTGRES_DB:-mes}" -c /backups/mes_<stamp>.dump

# 3. Bring the stack back (migrate job is a no-op when already current).
docker compose --profile production up -d
```

Always restore into a staging copy first when the plant database is at stake,
and confirm `/health/ready` is `200` before reopening traffic.

## Hardened containers and runtime defaults (issue #271)

### Non-root runtimes

Both runtime images run as non-root users: the API as the image's built-in
`app` user, the web frontend as `nginx` on unprivileged port **8080**
(compose maps `3000:8080`). Verify after `up`:

```bash
docker exec <project>-api-1 id -u   # non-zero (e.g. 1654)
docker exec <project>-web-1 whoami  # nginx
```

### Pinned base images

Every `FROM` line in `Dockerfile` and `AsistOff.MES.Web/Dockerfile`, plus the
prebuilt compose images (`postgres:16-alpine`, `datalust/seq:2024.3`), pins a
manifest digest (`image:tag@sha256:<digest>`). To refresh a pin after an
upstream patch release:

```bash
docker build --provenance=false .
docker inspect <image> --format '{{.RepoDigests}}'
# copy the sha256 digest back onto the matching FROM / image line
```

### Restart policies and resource limits

Every long-lived service (`postgres`, `seq`, `api`, `web`, `api-prod`,
`backup`, `swarm`) declares `restart:` plus memory/CPU
`deploy.resources.limits`. Only the one-shot `migrate` job uses
`restart: "no"` with no limits. Validate without starting anything:

```bash
docker compose config > /dev/null  # fails fast on bad interpolation
```

### Runtime API URL (no rebuild to repoint the backend)

The web container resolves its backend URL at startup
(`docker-entrypoint.sh` → `/config.js`, loaded by `index.html` before the
bundle). Precedence: `API_BASE_URL` env → build-time `VITE_API_BASE_URL` →
documented default `http://localhost:8080`.

```bash
# Plant backend reachable from operator browsers:
API_BASE_URL=https://mes.plant.local docker compose up -d web

# Inspect what a running container serves:
docker compose exec web cat /usr/share/nginx/html/config.js
```

An explicitly emptied value fails fast at container start:

```text
web: API_BASE_URL is set but empty. Set it to the browser-reachable API URL ...
```

### Secrets: generate, never default

Compose defines **no** default passwords or keys. Missing values fail fast at
`docker compose config` / `up` time:

```text
POSTGRES_PASSWORD is required - copy .env.example to .env and run sh scripts/generate-env.sh
```

First boot on any machine:

```bash
cp .env.example .env
sh scripts/generate-env.sh   # fills POSTGRES_PASSWORD (32 bytes) + AUTH_ISSUER_SIGNING_KEY (48 bytes)
docker compose --profile production up -d
```

`AUTH_ISSUER_SIGNING_KEY` (min 32 bytes / 256 bits) maps to
`auth:IssuerSigningKey` via `auth__IssuerSigningKey`. As a second layer, the
Gateway itself refuses a Production boot with a missing/weak
`POSTGRES_PASSWORD` or signing key (`ContainerSecretsValidator`) instead of
serving with the historical `root` / empty defaults. Development and CI are
unaffected: the guard is a no-op outside Production.
