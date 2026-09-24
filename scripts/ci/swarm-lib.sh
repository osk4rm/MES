#!/usr/bin/env bash
# MES Agent Swarm — shared CI helpers for .github/workflows/ai-swarm.yml.
#
# Usage: source scripts/ci/swarm-lib.sh
# Mirrors the label state machine owned locally by scripts/agent-dispatcher.ps1:
#   issue[ai:implement] -> implement -> PR[ai:review] -> review -> PR[ai:verify]
#   -> verify -> PR[ai:e2e] -> e2e -> PR[ai:ready] -> merge (squash, automatic)
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

swarm_retry() { # <tries> <delay-sec> <cmd...> — reruns flaky gh ops
  local tries="$1" delay="$2"
  shift 2
  local i=1
  while true; do
    if "$@"; then
      return 0
    fi
    if [ "$i" -ge "$tries" ]; then
      return 1
    fi
    echo "attempt $i/$tries failed: $* — retrying in ${delay}s"
    sleep "$delay"
    i=$((i + 1))
  done
}

swarm_add_label() { # <issue|pr> <number> <label>
  swarm_retry 3 10 gh "$1" edit "$2" --add-label "$3" >/dev/null
}

swarm_remove_label() { # <issue|pr> <number> <label> (never fails the step)
  swarm_retry 3 10 gh "$1" edit "$2" --remove-label "$3" >/dev/null 2>&1 || true
}

swarm_comment() { # <issue|pr> <number> <body-file>
  swarm_retry 3 10 gh "$1" comment "$2" --body-file "$3" >/dev/null
}

swarm_branch_for_issue() { # <issue> -> remote branch ai/issue-N-* or empty
  git fetch origin >/dev/null 2>&1 || true
  git branch -r --list "origin/ai/issue-$1-*" 2>/dev/null \
    | head -n 1 | sed 's|^ *origin/||;s| *$||'
}

swarm_open_pr() { # <branch> <title> <body-file> -> PR number; rc=3 on PR-permission block
  local branch="$1" title="$2" body="$3" out pr
  out=$(gh pr create --head "$branch" --base "$(swarm_default_branch)" \
    --title "$title" --body-file "$body" 2>&1)
  pr=$(printf '%s' "$out" | grep -oE 'https://github.com/[^ ]*/pull/[0-9]+' | grep -oE '[0-9]+$' | head -n 1)
  if [ -n "$pr" ]; then
    echo "$pr"
    return 0
  fi
  if printf '%s' "$out" | grep -q 'not permitted to create or approve pull requests'; then
    echo "$out" >&2
    return 3
  fi
  echo "$out" >&2
  return 1
}

swarm_pr_permission_note() { # prints the one-checkbox fix for blocked PR creation
  cat <<'EOF'
GitHub Actions is not permitted to create pull requests in this repo. Fix (30s, human):
Settings > Actions > General > Workflow permissions > check
"Allow GitHub Actions to create and approve pull requests", then re-add the
trigger label to retry. No code change needed.
EOF
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

swarm_in_flight_count() { # -> number of open issues/PRs holding ai:running
  # Throughput guard: implement/sweep refuse new work at MAX_PARALLEL.
  gh api search/issues -f q="repo:${GITHUB_REPOSITORY} label:ai:running state:open" \
    --jq '.total_count' 2>/dev/null || echo 0
}

swarm_oldest_queued_issue() { # -> oldest open ai:implement issue without ai:running
  gh issue list --state open --label ai:implement --json number,createdAt,labels \
    --jq '[.[] | select((.labels | map(.name) | index("ai:running") | not))] | sort_by(.createdAt) | .[0].number // empty' 2>/dev/null || true
}

swarm_unlabeled_count() { # open issues with no ai:* label (proposals awaiting spec)
  gh issue list --state open --limit 100 --json labels \
    --jq '[.[] | select((.labels | map(.name) | map(select(startswith("ai:"))) | length) == 0)] | length' 2>/dev/null || echo 0
}

swarm_backlog_count() { # queued ai:implement issues + unlabeled proposals
  local queued unlabeled
  queued=$(gh issue list --state open --label ai:implement --json number --jq 'length' 2>/dev/null || echo 0)
  unlabeled=$(swarm_unlabeled_count)
  echo $((queued + unlabeled))
}

swarm_is_docs_only() { # <pr> -> 0 when every changed file is docs/markdown
  # Docs-only PRs skip verify/e2e after an APPROVED review: no runtime to test.
  # Kept tight on purpose — workflow/script changes still take the full path.
  local files non_docs
  files=$(gh pr diff "$1" --name-only 2>/dev/null | tr -d '\r' | grep -v '^$' || true)
  [ -n "$files" ] || return 1
  non_docs=$(printf '%s\n' "$files" | grep -vE '(^docs/|\.md$)' || true)
  [ -z "$non_docs" ]
}

swarm_has_actionable_gap() { # 0 when an unlabeled proposal or a tracker gap row exists
  # Cheap pre-check so the analyst agent only starts when there is real work.
  if [ "$(swarm_unlabeled_count)" -gt 0 ]; then
    return 0
  fi
  grep -qE '\| *gap *\|' docs/feature-tracker.md 2>/dev/null
}

swarm_cleanup_stale_locks() { # release ai:running locks older than ${STALE_LOCK_MINUTES:-45}
  # Called from the sweep job. A crashed/lost job leaves ai:running forever,
  # blocking a MAX_PARALLEL slot. We timestamp locks via label creation time
  # and clear anything older than the threshold.
  local ttl="${STALE_LOCK_MINUTES:-45}" now
  now=$(date +%s)
  for kind in pr issue; do
    local list
    if [ "$kind" = pr ]; then
      list=$(gh pr list --state open --limit 50 --json number,labels \
        --jq '[.[] | select(.labels | map(.name) | index("ai:running"))] | .[].number' 2>/dev/null || true)
    else
      list=$(gh issue list --state open --limit 50 --json number,labels \
        --jq '[.[] | select(.labels | map(.name) | index("ai:running"))] | .[].number' 2>/dev/null || true)
    fi
    for num in $list; do
      local added age
      added=$(gh api "repos/${GITHUB_REPOSITORY}/issues/$num/timeline" --paginate \
        --jq '[.[] | select(.event == "labeled" and .label.name == "ai:running")] | last | .created_at' 2>/dev/null || true)
      if [ -z "$added" ]; then continue; fi
      age=$(( (now - $(date -d "$added" +%s 2>/dev/null || echo "$now")) / 60 ))
      if [ "$age" -ge "$ttl" ]; then
        echo "stale lock: $kind #$num (ai:running for ${age}m >= ${ttl}m) — releasing"
        swarm_remove_label "$kind" "$num" ai:running
        swarm_add_label "$kind" "$num" ai:blocked
        swarm_say "$kind" "$num" "Agent flow (CI): stale lock auto-cleared after ${age}m. The job that held it is gone. Re-add the work label to retry."
      fi
    done
  done
}

swarm_pr_touches_migrations() { # <pr> -> 0 when PR changes EF migration files
  gh pr diff "$1" --name-only 2>/dev/null | tr -d '\r' \
    | grep -qE 'Migrations/.*\.cs$' || return 1
  return 0
}

swarm_in_flight_migration_pr() { # -> PR number with a migration in flight, or empty
  # Serialization guard: two PRs with EF migrations conflict on
  # DefaultContextModelSnapshot.cs. At most one migration PR in flight.
  local pr
  for pr in $(gh pr list --state open --limit 50 --json number --jq '.[].number' 2>/dev/null); do
    if swarm_pr_touches_migrations "$pr"; then
      echo "$pr"
      return 0
    fi
  done
  return 0
}

swarm_wait_ci() { # <pr> <timeout-sec> -> pass | fail | timeout
  # Mirrors Get-CiState in agent-dispatcher.ps1.
  # Watches ONLY the `ci` workflow runs for the PR head SHA. ai-swarm's own
  # check runs are ignored on purpose: lock-label churn spawns no-op runs
  # that queue behind the lock holder (same concurrency group), and their
  # pending checks used to poison this wait into timeouts (self-deadlock
  # that demoted ai:ready PRs back to ai:review).
  local pr="$1" timeout="$2" waited=0 sha first upper
  sha=$(gh pr view "$pr" --json headRefOid --jq .headRefOid 2>/dev/null || true)
  if [ -z "$sha" ]; then
    echo pass
    return 0
  fi
  while [ "$waited" -lt "$timeout" ]; do
    # Newest ci run for this SHA first (gh sorts runs newest-first).
    first=$(gh run list --workflow ci --commit "$sha" --limit 5 --json status,conclusion \
      --jq 'if length == 0 then "none" else "\(.[0].status)/\(.[0].conclusion)" end' 2>/dev/null || echo 'unknown/unknown')
    upper=$(printf '%s' "$first" | tr '[:lower:]' '[:upper:]')
    case "$upper" in
      NONE)
        # No ci run (yet) — give CI a minute to appear before assuming absent.
        if [ "$waited" -ge 60 ]; then
          echo pass
          return 0
        fi
        ;;
      COMPLETED/SUCCESS | COMPLETED/SKIPPED | COMPLETED/NEUTRAL)
        echo pass
        return 0
        ;;
      COMPLETED/FAILURE | COMPLETED/TIMED_OUT | COMPLETED/CANCELLED | COMPLETED/STARTUP_FAILURE | COMPLETED/STALE)
        echo fail
        return 0
        ;;
      *) ;; # running, queued, action_required (a human may still approve), unknown
    esac
    sleep 30
    waited=$((waited + 30))
  done
  echo timeout
}

swarm_use_pat_remote() { # [$pat] — push as a collaborator, not as github-actions[bot]
  # Pushes authenticated with GITHUB_TOKEN are actor github-actions[bot]; on a
  # public repo every pull_request CI run they trigger waits for manual
  # approval (action_required) and swarm_wait_ci never sees green. Rewiring
  # origin to SWARM_PAT makes the pusher a collaborator so CI starts at once.
  local pat="${1:-}"
  if [ -z "$pat" ]; then
    echo 'SWARM_PAT absent; pushes use GITHUB_TOKEN (CI may need approval)'
    return 0
  fi
  git remote set-url origin "https://x-access-token:${pat}@github.com/${GITHUB_REPOSITORY}.git"
  echo 'origin rewired to SWARM_PAT credentials'
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
