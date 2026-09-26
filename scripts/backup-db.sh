#!/usr/bin/env bash
set -euo pipefail

# Backup bazy PostgreSQL uruchomionej przez docker compose.
# Uruchamiaj z katalogu projektu (tam, gdzie jest docker-compose.yml).
#   ./scripts/backup-db.sh
# Zmienne: BACKUP_DIR (domyslnie /opt/mes-backups), POSTGRES_USER, POSTGRES_DB.

BACKUP_DIR="${BACKUP_DIR:-/opt/mes-backups}"
POSTGRES_USER="${POSTGRES_USER:-admin}"
POSTGRES_DB="${POSTGRES_DB:-mes}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y-%m-%d_%H%M%S)"
TARGET="$BACKUP_DIR/mes_${STAMP}.sql.gz"

docker compose exec -T postgres pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" | gzip > "$TARGET"

find "$BACKUP_DIR" -name 'mes_*.sql.gz' -mtime "+${RETENTION_DAYS}" -delete

echo "Backup zapisany: $TARGET"
