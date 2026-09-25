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
