#!/usr/bin/env bash
# MES Agent Swarm — shared CI helpers for .github/workflows/ai-swarm.yml.
#
# Usage: source scripts/ci/swarm-lib.sh
# Mirrors the label state machine owned locally by scripts/agent-dispatcher.ps1:
#   issue[ai:implement] -> implement -> PR[ai:review] -> review -> PR[ai:verify]
#   -> verify -> PR[ai:e2e] -> e2e -> PR[ai:ready] (human merges)
#   any failure verdict -> PR[ai:changes] -> fix -> PR[ai:review]
#
# Conventions:
# - Every function that talks to GitHub needs GH_TOKEN in the environment
#   (GitHub Actions provides it as secrets.GITHUB_TOKEN).
# - ai:running is the cross-runner lock. Always pair swarm_acquire_lock with
#   swarm_auto_release (trap) so a crashed job cannot wedge the queue forever
#   (a human re-runs by removing + re-adding the trigger label).
# - Verdicts are strict: zero matches -> UNKNOWN, several DISTINCT values in
#   one output -> AMBIGUOUS. Both escalate to ai:blocked upstream.

swarm_has_label() { # <issue|pr> <number> <label> -> 0 when present
  local kind="$1" num="$2" label="$3" names
  names=$(gh "$kind" view "$num" --json labels --jq '.labels[].name' 2>/dev/null || true)
  printf '%s\n' "$names" | grep -qxF "$label"
}

swarm_add_label() { # <issue|pr> <number> <label>
  gh "$1" edit "$2" --add-label "$3" >/dev/null
}

swarm_remove_label() { # <issue|pr> <number> <label> (never fails the step)
  gh "$1" edit "$2" --remove-label "$3" >/dev/null 2>&1 || true
}

swarm_comment() { # <issue|pr> <number> <body-file>
  gh "$1" comment "$2" --body-file "$3" >/dev/null
}

swarm_say() { # <issue|pr> <number> <text> (short comment without a file)
  local kind="$1" num="$2" text="$3" tmp
  tmp=$(mktemp)
  printf '%s\n' "$text" >"$tmp"
  swarm_comment "$kind" "$num" "$tmp"
  rm -f "$tmp"
}

swarm_acquire_lock() { # <issue|pr> <number> -> 0 when lock taken, 1 when held
  local kind="$1" num="$2"
  if swarm_has_label "$kind" "$num" 'ai:running'; then
    echo "lock held: $kind #$num already has ai:running (another runner is working it)"
    return 1
  fi
  swarm_add_label "$kind" "$num" 'ai:running'
}

swarm_auto_release() { # <issue|pr> <number> — install EXIT trap to drop the lock
  local kind="$1" num="$2"
  # shellcheck disable=SC2064
  trap "swarm_release_lock $kind $num" EXIT
}

swarm_release_lock() { # <issue|pr> <number>
  swarm_remove_label "$1" "$2" 'ai:running' || true
}

swarm_verdict() { # <alternation> <file> -> value | UNKNOWN | AMBIGUOUS
  # Example: swarm_verdict 'APPROVED|CHANGES_REQUESTED' agent.log
  local pattern="VERDICT:[[:space:]]*($1)" file="$2" vals distinct
  vals=$(grep -oE "$pattern" "$file" 2>/dev/null | sed -E 's/.*VERDICT:[[:space:]]*//' || true)
  if [ -z "$vals" ]; then
    echo UNKNOWN
    return 0
  fi
  distinct=$(printf '%s\n' "$vals" | sort -u | wc -l | tr -d ' ')
  if [ "$distinct" -gt 1 ]; then
    echo AMBIGUOUS
    return 0
  fi
  printf '%s\n' "$vals" | head -n 1
}

swarm_comments_body() { # <issue|pr> <number> -> joined comment bodies
  gh "$1" view "$2" --json comments --jq '.comments[].body' 2>/dev/null || true
}

swarm_verdict_stdin() { # <alternation> — same as swarm_verdict but reads stdin
  local pattern="$1" tmp
  tmp=$(mktemp)
  cat >"$tmp"
  swarm_verdict "$pattern" "$tmp"
  rm -f "$tmp"
}

swarm_fail_count() { # <pr> -> number of failure verdicts in PR comments
  # Each fix round is preceded by exactly one failure verdict, so this count
  # approximates rounds already spent (stateless MaxRounds guard).
  swarm_comments_body pr "$1" | grep -cE 'VERDICT:[[:space:]]*(CHANGES_REQUESTED|TESTS_INSUFFICIENT|E2E_FAIL)' || true
}

swarm_find_pr_for_issue() { # <issue> -> newest open PR number on ai/issue-N-* or empty
  local issue="$1"
  gh pr list --state open --limit 100 --json number,headRefName \
    --jq "[.[] | select(.headRefName | startswith(\"ai/issue-$issue-\"))][0].number // empty" 2>/dev/null || true
}

swarm_pr_for_sha() { # <sha> -> open PR number with that head SHA or empty
  local sha="$1"
  gh pr list --state open --limit 100 --json number,headRefOid \
    --jq "[.[] | select(.headRefOid == \"$sha\")][0].number // empty" 2>/dev/null || true
}

swarm_default_branch() {
  gh repo view --json defaultBranchRef --jq '.defaultBranchRef.name' 2>/dev/null || echo master
}

swarm_wait_ci() { # <pr> <timeout-sec> -> pass | fail | timeout
  # Mirrors Get-CiState in agent-dispatcher.ps1. No checks at all -> pass.
  local pr="$1" timeout="$2" waited=0 states
  while [ "$waited" -lt "$timeout" ]; do
    states=$(gh pr checks "$pr" --json state --jq '[.[].state] | join(",")' 2>/dev/null || echo PENDING)
    if [ -z "$states" ]; then
      echo pass
      return 0
    fi
    upper=$(printf '%s' "$states" | tr '[:lower:]' '[:upper:]')
    case "$upper" in
      *FAILURE* | *ERROR* | *CANCELLED* | *TIMED_OUT* | *ACTION_REQUIRED* | *STARTUP_FAILURE*)
        echo fail
        return 0
        ;;
      *PENDING* | *QUEUED* | *IN_PROGRESS* | *STALE* | *EXPECTED*)
        sleep 30
        waited=$((waited + 30))
        ;;
      *)
        echo pass
        return 0
        ;;
    esac
  done
  echo timeout
}

swarm_require_auth() {
  if [ -z "${OPENCODE_API_KEY:-}" ]; then
    echo "::error::OPENCODE_API_KEY secret is not set. Add an OpenCode API key (opencode.ai/auth) to repo Settings > Secrets and variables > Actions, then re-add the trigger label."
    return 1
  fi
}

swarm_run_agent() { # <agent> <prompt> <logfile> — never fails the step by itself
  local agent="$1" prompt="$2" log="$3" code=0
  # --auto: the CI runner is an isolated clone, same rule as the local
  # dispatcher (-Auto). Agent permission files still deny pushes to main.
  opencode run --agent "$agent" --auto --format json "$prompt" 2>&1 | tee "$log" || code=$?
  echo "agent $agent exited with code $code (log: $log)"
  return 0
}
