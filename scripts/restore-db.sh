#!/usr/bin/env bash
set -euo pipefail

# Restore bazy PostgreSQL z pliku backupu (gzip, pg_dump).
#   ./scripts/restore-db.sh /opt/mes-backups/mes_2026-01-01_030000.sql.gz
# Najpierw zatrzymaj wszystko, co pisze do bazy:
#   docker compose stop api web

if [ $# -ne 1 ]; then
  echo "Uzycie: $0 <plik-backupu.sql.gz>" >&2
  exit 1
fi

BACKUP_FILE="$1"
POSTGRES_USER="${POSTGRES_USER:-admin}"
POSTGRES_DB="${POSTGRES_DB:-mes}"

if [ ! -f "$BACKUP_FILE" ]; then
  echo "Brak pliku: $BACKUP_FILE" >&2
  exit 1
fi

gunzip -c "$BACKUP_FILE" | docker compose exec -T postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1

echo "Restore zakonczony: $BACKUP_FILE -> $POSTGRES_DB"
echo "Sprawdz: curl -f http://localhost:8080/health/ready"
