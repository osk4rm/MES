#!/bin/sh
# Generates the required compose secrets into .env (issue #271).
# Usage: sh scripts/generate-env.sh [.env]
# Creates the file from .env.example when missing, then fills only the empty
# POSTGRES_PASSWORD / AUTH_ISSUER_SIGNING_KEY lines with fresh openssl output.
# Existing non-empty values are never overwritten.
set -eu

ENV_FILE="${1:-.env}"
EXAMPLE_FILE=".env.example"

if [ ! -f "$ENV_FILE" ]; then
  if [ ! -f "$EXAMPLE_FILE" ]; then
    echo "generate-env: $EXAMPLE_FILE not found; run from the repository root." >&2
    exit 1
  fi
  cp "$EXAMPLE_FILE" "$ENV_FILE"
  echo "generate-env: created $ENV_FILE from $EXAMPLE_FILE"
fi

gen_secret() {
  # $1 = bytes of entropy. openssl may be missing on minimal CI images;
  # /dev/urandom + base64 is the fallback.
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -base64 "$1"
  else
    head -c "$1" /dev/urandom | base64
  fi
}

fill_if_empty() {
  # $1 = variable name, $2 = fresh value.
  name="$1"
  value="$2"
  if grep -Eq "^${name}=$" "$ENV_FILE"; then
    escaped=$(printf '%s' "$value" | sed 's/[&/\]/\\&/g')
    sed -i "s/^${name}=$/${name}=${escaped}/" "$ENV_FILE"
    echo "generate-env: set $name"
  else
    echo "generate-env: kept existing $name"
  fi
}

fill_if_empty "POSTGRES_PASSWORD" "$(gen_secret 32)"
fill_if_empty "AUTH_ISSUER_SIGNING_KEY" "$(gen_secret 48)"

echo "generate-env: done ($ENV_FILE). Never commit it."
