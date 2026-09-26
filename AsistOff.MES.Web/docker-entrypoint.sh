#!/bin/sh
# Web runtime entrypoint (issue #271): resolves the browser-reachable API base
# URL at container start and renders it to /config.js, which index.html loads
# before the bundle. Precedence: API_BASE_URL env > build-time
# VITE_API_BASE_URL > documented default http://localhost:8080.
# An explicitly emptied API_BASE_URL (or no usable value at all) fails fast
# with a named message instead of serving a UI pointed at the wrong backend.
set -eu

DEFAULT_API_BASE_URL="http://localhost:8080"

if [ "${API_BASE_URL+set}" = "set" ] && [ -z "$API_BASE_URL" ]; then
  echo "web: API_BASE_URL is set but empty. Set it to the browser-reachable API URL (example: API_BASE_URL=https://mes.example.com) or unset it to use the default ${DEFAULT_API_BASE_URL}." >&2
  exit 1
fi

API_URL="${API_BASE_URL:-${VITE_API_BASE_URL:-$DEFAULT_API_BASE_URL}}"

if [ -z "$API_URL" ]; then
  echo "web: API_BASE_URL is not configured and no default is available. Set the API_BASE_URL environment variable for the web container (example: API_BASE_URL=https://mes.example.com) or rebuild with VITE_API_BASE_URL. See docs/production-runbook.md." >&2
  exit 1
fi

# Rendered for the browser; the value is operator configuration, not a secret.
# shellcheck disable=SC3037
cat > /usr/share/nginx/html/config.js <<EOF
// Generated at container start from \$API_BASE_URL (issue #271). Do not edit.
window.__MES_CONFIG__ = { apiBaseUrl: "${API_URL}" };
EOF

echo "web: serving with apiBaseUrl=${API_URL}"

exec nginx -g "daemon off;"
