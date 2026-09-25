#!/bin/sh
# Nightly PostgreSQL backup for the production compose profile (issue #257).
# Runs pg_dump on a loop, writes a timestamped dump to /backups, and prunes
# old dumps beyond the retention count.
#
# Environment:
#   POSTGRES_HOST            database host (default: postgres)
#   POSTGRES_PORT            database port (default: 5432)
#   POSTGRES_DB              database name (default: ${POSTGRES_DB:-mes})
#   POSTGRES_USER            backup user (default: ${POSTGRES_USER:-admin})
#   PGPASSWORD               backup user password (required)
#   BACKUP_INTERVAL_SECONDS  seconds between dumps; nightly default 86400
#   BACKUP_RETENTION_COUNT   how many recent dumps to keep (default: 7)
set -eu

: "${POSTGRES_HOST:=postgres}"
: "${POSTGRES_PORT:=5432}"
: "${POSTGRES_DB:=mes}"
: "${POSTGRES_USER:=admin}"
: "${BACKUP_INTERVAL_SECONDS:=86400}"
: "${BACKUP_RETENTION_COUNT:=7}"

if [ -z "${PGPASSWORD:-}" ]; then
  echo "backup: PGPASSWORD is required (set POSTGRES_PASSWORD or PGPASSWORD)." >&2
  exit 1
fi

mkdir -p /backups

while true; do
  stamp="$(date +%Y%m%d-%H%M%S)"
  file="/backups/mes_${stamp}.dump"
  echo "backup: writing ${file}"
  if pg_dump -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -F c -f "${file}"; then
    echo "backup: wrote ${file}"
  else
    echo "backup: pg_dump failed, removing partial ${file}" >&2
    rm -f "${file}"
  fi

  # Retention: keep the newest $BACKUP_RETENTION_COUNT dumps, delete the rest.
  # shellcheck disable=SC2012
  ls -1t /backups/mes_*.dump 2>/dev/null | tail -n "+$((BACKUP_RETENTION_COUNT + 1))" | xargs -r rm -f || true

  echo "backup: sleeping ${BACKUP_INTERVAL_SECONDS}s"
  sleep "${BACKUP_INTERVAL_SECONDS}"
done
