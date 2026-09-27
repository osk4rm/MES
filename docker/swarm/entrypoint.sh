#!/usr/bin/env bash
# Entrypoint for the MES Agent Swarm container.
# Runs as non-root `swarm` (UID 10000, issue #357):
#  1. wire gh auth for git (clone + push)
#  2. clone/pull the repo into the isolated /work volume
#  3. install web deps once
#  4. start the dispatcher in the background (guarded by its own lockfile)
#  5. run the dashboard in the foreground
set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/osk4rm/MES.git}"
REPO_BRANCH="${REPO_BRANCH:-master}"
WORK=/work
HOME_DIR="${HOME:-/home/swarm}"

# Container-local writable dirs (all owned by `swarm`; the opencode state
# dir is the scoped named volume from docker-compose.swarm.yml).
mkdir -p "$HOME_DIR/.config" "$HOME_DIR/.local/share/opencode" \
  "$HOME_DIR/.cache/ms-playwright" "$HOME_DIR/.npm" 2>/dev/null || true

if [ ! -w "$WORK" ]; then
  echo "[swarm] ERROR: $WORK is not writable by $(id -un) (uid $(id -u))."
  echo "[swarm] The volume was likely created by a pre-#357 root container."
  echo "[swarm] Recreate it: docker compose -f docker-compose.yml -f docker-compose.swarm.yml down swarm && docker volume rm asistoff-mes_swarm_work"
  exit 1
fi

git config --global user.name  "${GIT_USER_NAME:-MES Agent Swarm}"
git config --global user.email "${GIT_USER_EMAIL:-mes-swarm@users.noreply.github.com}"
git config --global --add safe.directory "$WORK" || true

# --- auth first, so a private repo can be cloned over HTTPS -----------------
if [ -n "${GH_TOKEN:-}" ]; then
  if command -v gh >/dev/null 2>&1; then
    echo "[swarm] configuring git credentials via gh"
    gh auth setup-git || true
  fi
else
  echo "[swarm] WARNING: GH_TOKEN is empty — a private repo cannot be cloned and agents cannot push/comment"
fi

# opencode authenticates via env-passed OPENCODE_API_KEY (same model as
# GH_TOKEN); no host auth dirs are mounted (issue #357).
if [ -z "${OPENCODE_API_KEY:-}" ]; then
  echo "[swarm] WARNING: OPENCODE_API_KEY is empty — agents cannot call the AI service. Set it in .env (opencode.ai/auth)."
fi

if [ ! -d "$WORK/.git" ]; then
  echo "[swarm] cloning $REPO_URL -> $WORK"
  if ! git clone --branch "$REPO_BRANCH" "$REPO_URL" "$WORK"; then
    if ! git clone "$REPO_URL" "$WORK"; then
      echo "[swarm] ERROR: clone failed. For a private repo set GH_TOKEN in .env (gh auth token)."
      sleep 5
      exit 1
    fi
  fi
fi

cd "$WORK"
git fetch origin "$REPO_BRANCH" || true
git checkout "$REPO_BRANCH" || true
git pull --ff-only origin "$REPO_BRANCH" || true

if ! gh auth status >/dev/null 2>&1; then
  echo "[swarm] WARNING: gh is not authenticated — agents cannot push/open PRs. Set GH_TOKEN."
fi

if [ ! -d "$WORK/AsistOff.MES.Web/node_modules" ]; then
  echo "[swarm] installing frontend deps (npm ci)"
  (cd "$WORK/AsistOff.MES.Web" && npm ci) || echo "[swarm] npm ci failed — e2e may not start"
fi

if [ "${SWARM_AUTOSTART_DISPATCHER:-1}" = "1" ]; then
  echo "[swarm] starting dispatcher supervisor in background"
  # Supervise the dispatcher. It parses its own script once at start-up, so a
  # merge to master cannot reach a process that is already running; it updates
  # its clone between cycles and exits to be restarted with the new code. A
  # plain `&` start (what this used to do) meant a crash or an update silently
  # ended the swarm for the life of the container.
  (
    while true; do
      cd "$WORK"
      git fetch --quiet origin "$REPO_BRANCH" >/dev/null 2>&1 || true
      git merge --ff-only --quiet "origin/$REPO_BRANCH" >/dev/null 2>&1 || true
      pwsh -NoProfile -File "$WORK/scripts/agent-dispatcher.ps1"
      echo "[swarm] dispatcher exited ($?); restarting in 15s"
      sleep 15
    done
  ) >> /tmp/swarm-dispatcher.log 2>&1 &
fi

echo "[swarm] dashboard -> http://0.0.0.0:${DASHBOARD_PORT:-5178}"
exec node "$WORK/scripts/dashboard/server.mjs"
