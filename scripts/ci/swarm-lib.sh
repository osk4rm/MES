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

swarm_refire_label() { # <issue|pr> <number> <label>
  # A duplicate --add-label is a no-op and emits no `labeled` event, so a
  # re-route onto a label the item already has never wakes the next job.
  #
  # ai:blocked is a human-attention state (no-progress fix round, CI waiting for
  # approval, unparsable verdict). Without this guard the block was not sticky:
  # the fixer blocked #268 for changing nothing and the queued reviewer, seeing
  # red CI, immediately re-fired ai:changes and started the next round anyway.
  # The block is still not something an AGENT may re-enter: recovery goes
  # through swarm_retry_blocked_prs, which re-arms only after a cooldown, only
  # a bounded number of times, and only for a block that recorded a
  # machine-readable marker. Blocks without that marker stay with a human.
  if [ "$1" = pr ] && swarm_has_label pr "$2" ai:blocked; then
    echo "  ai:blocked is set on PR #$2; not re-firing $3 (only the sweeper may re-arm a block)" >&2
    return 0
  fi
  swarm_remove_label "$1" "$2" "$3"
  swarm_add_label "$1" "$2" "$3"
}

swarm_log() { # diagnostics — stderr only, so $(swarm_*) captures stay clean
  echo "$*" >&2
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

swarm_say_once() { # <issue|pr> <number> <marker> <text> - comment once per marker
  # A marker that repeats (same head SHA, same gate) must not spam the PR with
  # the same explanation every 30-minute sweep. The marker goes into its own
  # comment (like the swarm-verdict markers) so the next run actually finds it;
  # posting only the human text re-commented on every run.
  if swarm_comments_body "$1" "$2" | grep -qF "$3"; then
    return 0
  fi
  swarm_say "$1" "$2" "<!-- $3 -->"
  swarm_say "$1" "$2" "$4"
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

swarm_last_verdict() { # <alternation> <file> -> value | UNKNOWN
  # Like swarm_verdict but returns the LAST verdict instead of AMBIGUOUS when
  # several rounds left different verdicts in the log/comments. A PR that was
  # CHANGES_REQUESTED and is now APPROVED must count as APPROVED, not blocked.
  local pattern="VERDICT:[[:space:]]*($1)" file="$2" last
  last=$(grep -oE "$pattern" "$file" 2>/dev/null | sed -E 's/.*VERDICT:[[:space:]]*//' | tail -n 1 || true)
  if [ -z "$last" ]; then
    echo UNKNOWN
  else
    echo "$last"
  fi
}

swarm_verdict_stdin() { # <alternation> — same as swarm_verdict but reads stdin
  local pattern="$1" tmp
  tmp=$(mktemp)
  cat >"$tmp"
  swarm_verdict "$pattern" "$tmp"
  rm -f "$tmp"
}

swarm_last_verdict_stdin() { # <alternation> — same as swarm_last_verdict but reads stdin
  local pattern="$1" tmp
  tmp=$(mktemp)
  cat >"$tmp"
  swarm_last_verdict "$pattern" "$tmp"
  rm -f "$tmp"
}

swarm_fail_count() { # <pr> -> number of failure verdicts in PR comments
  # Each fix round is preceded by exactly one failure verdict, so this count
  # approximates rounds already spent (round counter; MAX_ROUNDS=0 means the
  # fix loop is unlimited and this is observability only).
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

swarm_uint() { # <value> [fallback] - keep a non-number out of a [ -ge ] test
  # gh api prints its JSON error body on stdout AND exits non-zero, so the old
  # `... || echo 0` appended to it: the caller got `{...404...}0`. That string
  # reached `[ "$n" -ge 3 ]`, which only warns "integer expression expected" and
  # evaluates false - the MAX_PARALLEL capacity gate was blind while looking
  # fine, and issue #272 was re-implemented 17x in 6 hours underneath it.
  case "${1:-}" in
    '' | *[!0-9]*) echo "${2:-0}" ;;
    *) echo "$1" ;;
  esac
}

swarm_error() { # GitHub annotation - the 404 above stayed invisible for hours
  # stderr ONLY: callers capture these helpers with $(...) and compare the
  # result, so an annotation on stdout turns into "::error::...\n0" in a
  # [ -ge ] test - the exact silent breakage this function reports.
  echo "::error::$*" >&2
}

swarm_in_flight_count() { # -> number of open issues/PRs holding ai:running
  # Throughput guard: implement/sweep refuse new work at MAX_PARALLEL.
  # `gh api search/issues -f q=` implies POST, and POST /search/issues is 404.
  # Since 2026-09 the search API also REJECTS a query without is:issue /
  # is:pull-request (422), which left this gate blind on stdout as `0`. Count
  # the two kinds separately and sum them instead.
  local issues prs
  issues=$(gh api -X GET search/issues \
    -f q="repo:${GITHUB_REPOSITORY} is:issue label:ai:running state:open" \
    --jq '.total_count' 2>/dev/null || echo '')
  prs=$(gh api -X GET search/issues \
    -f q="repo:${GITHUB_REPOSITORY} is:pull-request label:ai:running state:open" \
    --jq '.total_count' 2>/dev/null || echo '')
  if [ "$(swarm_uint "$issues" x)" = x ] || [ "$(swarm_uint "$prs" x)" = x ]; then
    swarm_error "swarm_in_flight_count: search API returned issues='$issues' prs='$prs' - capacity gate is blind, assuming 0"
  fi
  echo $(( $(swarm_uint "$issues" 0) + $(swarm_uint "$prs" 0) ))
}

swarm_oldest_queued_issue() { # -> oldest open ai:implement issue without ai:running
  # Skips issues whose work is already in review. Re-firing ai:implement for an
  # issue that has an open PR re-runs the implementer against a diff that
  # already exists: #272 was re-implemented 17 times in 6 hours while its PR
  # (#276) sat in review. Newest label churn alone cannot be trusted to stop
  # it - the guard has to live here, where the re-fire is decided.
  local candidates n
  candidates=$(gh issue list --state open --label ai:implement --json number,createdAt,labels \
    --jq '[.[] | select((.labels | map(.name) | index("ai:running") | not))] | sort_by(.createdAt) | .[].number' 2>/dev/null || true)
  for n in $candidates; do
    if swarm_issue_has_open_pr "$n"; then
      swarm_log "issue #$n already has an open PR; not re-firing ai:implement"
      continue
    fi
    echo "$n"
    return 0
  done
}

swarm_issue_has_open_pr() { # <issue-number> -> 0 when the issue already has an open PR
  # The sweeper re-fires ai:implement for the oldest queued issue, and the
  # analyst adopts unlabeled proposals. Both used to pick #272 - whose PR (#276)
  # had been open since 00:24 - and re-ran the implementer 17 times. An issue
  # whose work is already in review must never be handed back to an implementer.
  local n="$1" pr
  pr=$(gh pr list --state open --limit 100 --json number,headRefName \
    --jq ".[] | select(.headRefName | startswith(\"ai/issue-$n-\")) | .number" 2>/dev/null | head -n 1 || true)
  [ -n "$pr" ]
}

swarm_unlabeled_count() { # open issues with no ai:* label (proposals awaiting spec)
  swarm_uint "$(gh issue list --state open --limit 100 --json labels \
    --jq '[.[] | select((.labels | map(.name) | map(select(startswith("ai:"))) | length) == 0)] | length' 2>/dev/null || echo '')" 0
}

swarm_backlog_count() { # queued ai:implement issues + unlabeled proposals
  local queued unlabeled
  queued=$(swarm_uint "$(gh issue list --state open --label ai:implement --json number --jq 'length' 2>/dev/null || echo '')" 0)
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

swarm_ci_state_once() { # <pr> -> pass | fail | approval | running | none
  # Single-shot classification of the newest `ci` run for the PR head SHA.
  local pr="$1" sha first upper
  sha=$(gh pr view "$pr" --json headRefOid --jq .headRefOid 2>/dev/null || true)
  if [ -z "$sha" ]; then
    echo none
    return 0
  fi
  first=$(gh run list --workflow ci --commit "$sha" --limit 5 --json status,conclusion \
    --jq 'if length == 0 then "none" else "\(.[0].status)/\(.[0].conclusion)" end' 2>/dev/null || echo 'unknown/unknown')
  upper=$(printf '%s' "$first" | tr '[:lower:]' '[:upper:]')
  case "$upper" in
    NONE) echo none ;;
    COMPLETED/SUCCESS | COMPLETED/SKIPPED | COMPLETED/NEUTRAL) echo pass ;;
    COMPLETED/FAILURE | COMPLETED/TIMED_OUT | COMPLETED/CANCELLED | COMPLETED/STARTUP_FAILURE | COMPLETED/STALE) echo fail ;;
    COMPLETED/ACTION_REQUIRED | WAITING/* | REQUESTED/*) echo approval ;;
    *) echo running ;;
  esac
}

swarm_branch_busy() { # <branch> -> 0 when a swarm run is still in flight for it
  # review/verify deliberately do not take ai:running (it is one global in-flight
  # lock and would block the parallel gate), so the sweeper cannot see them by
  # label. Without this check it re-fires a stage label while its agent is still
  # working, and two reviewers can return different verdicts for one diff.
  gh run list --branch "$1" --limit 20 --json status,workflowName 2>/dev/null \
    --jq '[.[] | select(.workflowName == "ai-swarm" and .status != "completed")] | length' \
    | grep -qE '^[1-9]'
}

swarm_nudge_stuck_prs() { # re-fire stage labels on PRs that lost their trigger
  # A stage job that times out waiting for CI keeps its label and relies on
  # the ci-completed event to retrigger — but workflow_run does not fire for
  # bot-actor runs, so the PR stalls forever. Re-fire the label (remove+add)
  # on any unlocked stage PR whose ci is not still running; the stage job then
  # re-evaluates immediately (and its wait loop auto-approves covers
  # action_required runs). Includes ai:changes: a re-route to the fixer that
  # was already labeled emits no event, so this sweep is the safety net.
  # Only PRs WITHOUT ai:running (no live job) and
  # without ai:blocked (human owns those) are touched.
  local pr label state branch
  for pr in $(gh pr list --state open --limit 50 --json number,headRefName,labels \
    --jq '[.[] | select((.labels | map(.name) | index("ai:running") | not) and (.labels | map(.name) | index("ai:blocked") | not))] | .[].number' 2>/dev/null); do
    branch=$(gh pr view "$pr" --json headRefName --jq .headRefName 2>/dev/null || true)
    if [ -n "$branch" ] && swarm_branch_busy "$branch"; then
      echo "PR #$pr has a run in flight; not nudging"
      continue
    fi
    for label in ai:review ai:verify ai:e2e ai:ready ai:changes; do
      if swarm_has_label pr "$pr" "$label"; then
        state=$(swarm_ci_state_once "$pr")
        # running: a live job owns it. approval: the automatic ci run parked and
        # cannot be approved by the workflow token - re-firing the stage is
        # still right, because the stage's own wait loop dispatches a ci run
        # that is not subject to the approval gate. Parking such a PR here is
        # what froze #285 (and, through ai:blocked, #276 and #268) overnight.
        if [ "$state" = running ]; then
          echo "PR #$pr $label ci=$state - not re-firing"
        else
          echo "stuck $label PR #$pr (ci=$state) - re-firing the label"
          swarm_refire_label pr "$pr" "$label"
        fi
        break
      fi
    done
  done
}

swarm_cleanup_stale_locks() { # release ai:running locks older than ${STALE_LOCK_MINUTES:-45}
  # Called from the sweep job. A crashed/lost job leaves ai:running forever,
  # blocking a MAX_PARALLEL slot. Lock age comes from the label timeline event.
  #
  # When the lock is stale we release it AND re-fire the stage the item is still
  # parked on. The old behaviour escalated a lost job to ai:blocked, which is a
  # manual-action dead end: a runner that vanished mid-stage froze the work for
  # a human even though nothing was actually wrong. A missing job must recycle
  # the stage automatically (0 manual actions is the whole point).
  local ttl="${STALE_LOCK_MINUTES:-45}" now kind list num added age lbl
  now=$(date +%s)
  for kind in pr issue; do
    if [ "$kind" = pr ]; then
      list=$(gh pr list --state open --limit 50 --json number,labels \
        --jq '[.[] | select(.labels | map(.name) | index("ai:running"))] | .[].number' 2>/dev/null || true)
    else
      list=$(gh issue list --state open --limit 50 --json number,labels \
        --jq '[.[] | select(.labels | map(.name) | index("ai:running"))] | .[].number' 2>/dev/null || true)
    fi
    for num in $list; do
      added=$(gh api "repos/${GITHUB_REPOSITORY}/issues/$num/timeline" --paginate \
        --jq '[.[] | select(.event == "labeled" and .label.name == "ai:running")] | last | .created_at' 2>/dev/null || true)
      if [ -z "$added" ]; then
        continue
      fi
      age=$(( (now - $(date -d "$added" +%s 2>/dev/null || echo "$now")) / 60 ))
      if [ "$age" -ge "$ttl" ]; then
        echo "stale lock: $kind #$num (ai:running for ${age}m >= ${ttl}m) — releasing and re-arming"
        swarm_remove_label "$kind" "$num" ai:running
        if [ "$kind" = pr ]; then
          for lbl in ai:review ai:verify ai:e2e ai:ready ai:changes; do
            if swarm_has_label pr "$num" "$lbl"; then
              swarm_refire_label pr "$num" "$lbl"
              break
            fi
          done
        fi
        swarm_say "$kind" "$num" "Agent flow (CI): the job holding this item disappeared (ai:running for ${age}m). The lock was released and the parked stage re-queued automatically — no action needed."
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
  # ai:blocked PRs are skipped — a human owns those, they must not stall the queue.
  local pr
  for pr in $(gh pr list --state open --limit 50 --json number,labels \
    --jq '[.[] | select((.labels | map(.name) | index("ai:blocked") | not))] | .[].number' 2>/dev/null); do
    if swarm_pr_touches_migrations "$pr"; then
      echo "$pr"
      return 0
    fi
  done
  return 0
}

swarm_check_conflict_markers() { # -> 0 when clean, 1 when conflict markers exist
  # Guards against the classic agent failure mode: "resolving" a merge and
  # committing `<<<<<<<` / `=======` / `>>>>>>>` leftovers into the tree.
  # Setext headings in markdown use `=======` legitimately, so angle markers
  # are checked everywhere and the `=======` line only outside markdown.
  local hits
  hits=$( {
    git grep -nE '^(<<<<<<< |=======$|>>>>>>> )' -- . ':(exclude)*.md' 2>/dev/null || true
    git grep -nE '^(<<<<<<< |>>>>>>> )' -- '*.md' 2>/dev/null || true
  } | sed '/^$/d' )
  if [ -n "$hits" ]; then
    echo 'ERROR: unresolved merge-conflict markers in the working tree:'
    printf '%s\n' "$hits"
    return 1
  fi
  echo 'no merge-conflict markers'
  return 0
}

swarm_sync_master() { # merge origin/<default> into HEAD; rc 0 clean, 2 conflicts
  # Always fetch before merging: on a shallow clone the merge-base is missing
  # and git invents bogus conflicts ("weird conflicts with master").
  local base
  base=$(swarm_default_branch)
  git fetch origin "$base" 2>/dev/null || true
  if git merge-base --is-ancestor "origin/$base" HEAD 2>/dev/null; then
    echo "up to date with origin/$base"
    return 0
  fi
  if git merge --no-edit "origin/$base"; then
    echo "merged origin/$base into $(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo HEAD)"
    return 0
  fi
  echo "MERGE CONFLICTS with origin/$base in:"
  git diff --name-only --diff-filter=U
  return 2
}

swarm_finish_merge_if_pending() { # commit a (possibly agent-resolved) merge
  if [ ! -f .git/MERGE_HEAD ]; then
    return 0
  fi
  # Stage everything first: agents often resolve files but forget `git add`.
  # Conflict markers in bad resolutions are caught by
  # swarm_check_conflict_markers right after this call.
  git add -A
  git commit --no-edit
}

swarm_pr_mergeable() { # <pr> -> MERGEABLE | CONFLICTING | UNKNOWN
  # GitHub computes mergeability asynchronously; retry past UNKNOWN.
  local pr="$1" state i
  for i in 1 2 3 4 5 6; do
    state=$(gh pr view "$pr" --json mergeable --jq '.mergeable // "UNKNOWN"' 2>/dev/null || echo UNKNOWN)
    state=$(printf '%s' "$state" | tr '[:lower:]' '[:upper:]')
    case "$state" in
      MERGEABLE | CONFLICTING)
        echo "$state"
        return 0
        ;;
    esac
    sleep 5
  done
  echo UNKNOWN
}

swarm_conflict_rules() { # prints the conflict-resolution contract for agent prompts
  cat <<'EOF'
A merge of master into the branch is IN PROGRESS with conflicts. Resolve every conflict, then `git add -A` and `git commit --no-edit` to complete the merge. Hard rules: NEVER leave conflict markers (`<<<<<<<`, `=======`, `>>>>>>>`) in any file. For `AsistOff.MES.Shared.Infrastructure/Migrations/DefaultContextModelSnapshot.cs` or any `*Designer.cs` NEVER hand-merge — run `git checkout origin/master -- AsistOff.MES.Shared.Infrastructure/Migrations/` to take master's migration state, delete your own migration files for this feature if present, then recreate the migration with `dotnet ef migrations add <Name> --project AsistOff.MES.Shared.Infrastructure --startup-project AsistOff.MES.Gateway --context DefaultContext`. For shared registry files (`AsistOff.MES.Web/src/i18n.ts`, `AsistOff.MES.Web/src/sitemap.ts`, `tests/AsistOff.MES.Integration.Tests/TestData/ApiContracts.cs`) keep the UNION of both sides and make sure the syntax stays valid — never drop the other side's entries. Finish with `dotnet build AsistOff.MES.sln` and `npm --prefix AsistOff.MES.Web run build` green.
EOF
}

swarm_approve_run() { # <run-id> -> 0 when the approve API accepts the call
  # GH_TOKEN is SWARM_PAT, which has no Actions scope, so gh api approve fails
  # and the old caller treated that stdout as a CI failure (re-review loop).
  # ACTIONS_TOKEN is the workflow GITHUB_TOKEN (permissions: actions: write).
  local run_id="$1" token out
  token="${ACTIONS_TOKEN:-}"
  if [ -z "$token" ]; then
    swarm_log "ACTIONS_TOKEN is unset; cannot approve ci run $run_id"
    return 1
  fi
  # gh prints the JSON error body on stdout AND exits non-zero; swallow both so
  # a rejected approve can never be mistaken for output by a caller.
  if ! out=$(GH_TOKEN="$token" gh api -X POST \
    "repos/${GITHUB_REPOSITORY}/actions/runs/${run_id}/approve" 2>&1); then
    swarm_log "approve of ci run $run_id rejected: $(printf '%s' "$out" | tr '\n' ' ' | cut -c1-160)"
    return 1
  fi
}

swarm_dispatch_ci() { # <pr> [sha] -> 0 when a fresh ci run was dispatched for the head
  # The escape hatch that needs no human and no PAT. A pull_request ci run
  # triggered by a github-actions[bot] push on a public repo parks in
  # action_required, and GITHUB_TOKEN cannot approve it (only a maintainer token
  # can - verified: the same endpoint 404s for GITHUB_TOKEN and succeeds for a
  # PAT). Eight ci runs sat parked for hours on #276/#268/#285 that way, which
  # is what froze the whole line overnight. workflow_dispatch runs are not
  # subject to the approval gate, so the swarm dispatches its own CI for the
  # head SHA and reads a real verdict from it.
  local pr="$1" sha="${2:-}" branch wf out
  branch=$(gh pr view "$pr" --json headRefName --jq .headRefName 2>/dev/null || true)
  if [ -z "$branch" ]; then
    swarm_log "cannot dispatch ci: no head branch for PR #$pr"
    return 1
  fi
  wf=$(gh workflow list --all --json name,path,state \
    --jq '.[] | select(.path | test("(^|/)ci\\.ya?ml$")) | .path' 2>/dev/null | head -n 1 || true)
  if [ -z "$wf" ]; then
    swarm_log 'cannot dispatch ci: no ci workflow file found'
    return 1
  fi
  if ! out=$(GH_TOKEN="${ACTIONS_TOKEN:-${GH_TOKEN:-}}" gh workflow run "$wf" --ref "$branch" 2>&1); then
    swarm_log "ci dispatch on $branch failed: $(printf '%s' "$out" | tr '\n' ' ' | cut -c1-160)"
    return 1
  fi
  swarm_log "dispatched a fresh ci run on $branch (head ${sha:-unknown})"
}

swarm_wait_ci() { # <pr> <timeout-sec> -> pass | fail | timeout | approval
  # Stdout is ONLY the result token. Diagnostics go to stderr — callers capture
  # this with $(...) and compare it to "pass". A leaked log line used to demote
  # ai:ready PRs back through review even when CI was green.
  #
  # Watches ONLY the `ci` workflow for the PR head SHA. ai-swarm's own checks
  # are ignored: lock-label churn spawns no-op runs whose pending state used
  # to poison this wait (self-deadlock).
  local pr="$1" timeout="$2" waited=0 sha first upper run_id tries=0
  sha=$(gh pr view "$pr" --json headRefOid --jq .headRefOid 2>/dev/null || true)
  if [ -z "$sha" ]; then
    echo pass
    return 0
  fi
  while [ "$waited" -lt "$timeout" ]; do
    first=$(gh run list --workflow ci --commit "$sha" --limit 5 --json status,conclusion \
      --jq 'if length == 0 then "none" else "\(.[0].status)/\(.[0].conclusion)" end' 2>/dev/null || echo 'unknown/unknown')
    upper=$(printf '%s' "$first" | tr '[:lower:]' '[:upper:]')
    case "$upper" in
      NONE)
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
      COMPLETED/ACTION_REQUIRED | WAITING/* | REQUESTED/*)
        if [ "$tries" -ge "${CI_APPROVAL_TRIES:-2}" ]; then
          # Approve and dispatch were both tried and the run is still parked:
          # only a human can move it. Say so instead of waiting out the clock.
          echo approval
          return 0
        fi
        run_id=$(gh run list --workflow ci --commit "$sha" --limit 1 --json databaseId \
          --jq '.[0].databaseId // empty' 2>/dev/null || true)
        if [ -z "$run_id" ]; then
          echo approval
          return 0
        fi
        tries=$((tries + 1))
        if swarm_approve_run "$run_id"; then
          swarm_log "ci run $run_id was parked on approval - approved it via ACTIONS_TOKEN"
        elif swarm_dispatch_ci "$pr" "$sha"; then
          swarm_log "ci run $run_id is parked and cannot be approved by this token - running our own ci on the head instead"
          swarm_say_once pr "$pr" "swarm-dispatch sha=$sha" \
            "Agent flow (CI): the automatic ci run for this head parked on manual approval (it was triggered by a bot push, and the workflow token cannot approve it). The swarm dispatched its own ci run for the same commit, so no human approve is needed - this PR will not stall."
        else
          swarm_log "ci run $run_id needs approval and neither approve nor dispatch worked"
          echo approval
          return 0
        fi
        ;;
      *) ;; # running, queued, pending, unknown
    esac
    sleep 30
    waited=$((waited + 30))
  done
  echo timeout
}

# --- gate verdicts, keyed to the head SHA ---------------------------------
#
# Freshness must never be inferred from timestamps. Rebase, --amend and runner
# clock skew all move commit dates, and a comparison that mis-reads "the verdict
# predates the fix" makes the swarm skip a gate on unreviewed code. The stage
# job therefore records the SHA it judged, in an HTML comment: invisible in the
# web UI, present in the API, and written by the job rather than by the agent,
# so agent compliance is not required. No marker for the current head means
# "not judged" — the safe direction, the gate runs.
#
# Markers are gate-scoped (gate=review / verify / e2e) because a single head SHA
# collects one marker per gate and the newest-marker-wins lookup would otherwise
# let a verify marker hide the review verdict for the same commit.

swarm_head_verdict() { # <pr> <gate> <sha> -> verdict recorded for <sha> by <gate> (newest wins), or empty
  local pr="$1" gate="$2" sha="$3"
  # Digits are legal in gate names (e2e); the old [a-z] guard silently voided
  # every e2e marker, so the e2e gate re-ran the agent on every re-fire.
  case "$gate" in
    '' | *[!a-z0-9]*) return 1 ;;
  esac
  case "$sha" in
    '' | *[!0-9a-f]*) return 1 ;;
  esac
  swarm_comments_body pr "$pr" 2>/dev/null \
    | grep -oE "swarm-verdict gate=$gate sha=$sha verdict=[A-Z0-9_]+" \
    | tail -n 1 | sed -E 's/.*verdict=//' || true
}

swarm_verdict_covers_head() { # <pr> <gate> <verdict> -> 0 when <verdict> was recorded for the current head SHA
  local pr="$1" gate="$2" want="$3" head got
  head=$(gh pr view "$pr" --json headRefOid --jq .headRefOid 2>/dev/null || true)
  [ -n "$head" ] || return 1
  got=$(swarm_head_verdict "$pr" "$gate" "$head")
  [ -n "$got" ] && [ "$got" = "$want" ]
}

swarm_mark_verdict() { # <pr> <gate> <sha> <verdict> — witness that <verdict> covers <sha>; no-op when head already moved
  # Refuses to mark a SHA that is no longer the head: the pass judged a
  # different tree than the one the marker would vouch for.
  local pr="$1" gate="$2" sha="$3" verdict="$4" head tmp
  case "$gate" in
    '' | *[!a-z0-9]*) return 1 ;;
  esac
  case "$sha" in
    '' | *[!0-9a-f]*) return 1 ;;
  esac
  case "$verdict" in
    '' | *[!A-Z0-9_]*) return 1 ;;
  esac
  head=$(gh pr view "$pr" --json headRefOid --jq .headRefOid 2>/dev/null || true)
  if [ "$head" != "$sha" ]; then
    echo "head moved ($sha -> ${head:-unknown}); not marking $gate=$verdict" >&2
    return 1
  fi
  tmp=$(mktemp)
  printf '<!-- swarm-verdict gate=%s sha=%s verdict=%s -->\n' "$gate" "$sha" "$verdict" >"$tmp"
  swarm_comment pr "$pr" "$tmp"
  rm -f "$tmp"
}

swarm_head_moved() { # <pr> <sha> -> 0 when the PR head is no longer <sha>
  # A pass that started before the last push judges a tree nobody ships. Observed
  # on #256: e2e began 23:18:01, a commit landed 23:20:09, the agent returned
  # 23:20:13 and the job still stamped ai:ready on the new head.
  local pr="$1" sha="$2" head
  [ -n "$sha" ] || return 1
  head=$(gh pr view "$pr" --json headRefOid --jq .headRefOid 2>/dev/null || true)
  [ -n "$head" ] && [ "$head" != "$sha" ]
}

swarm_unsatisfied_gates() { # <pr> — gates that do not cover the current head, one per line
  # ai:ready is a transition, not evidence: it can be added by a pass that ran
  # against an older head. Only the markers prove the head we are about to merge,
  # so the merge gate asks for them by name instead of trusting the label.
  local pr="$1" head
  head=$(gh pr view "$pr" --json headRefOid --jq .headRefOid 2>/dev/null || true)
  if [ -z "$head" ]; then
    echo review
    return 0
  fi
  swarm_verdict_covers_head "$pr" review APPROVED || echo review
  if ! swarm_is_docs_only "$pr"; then
    swarm_verdict_covers_head "$pr" verify TESTS_SOUND || echo verify
    # E2E_BLOCKED is an explicit "infrastructure, promote anyway", so any e2e
    # verdict for this head counts; a missing one means e2e never ran here.
    case "$(swarm_head_verdict "$pr" e2e "$head")" in
      E2E_PASS | E2E_FAIL | E2E_BLOCKED) ;;
      *) echo e2e ;;
    esac
  fi
}

swarm_pre_e2e_gates_satisfied() { # <pr> — review APPROVED and verify SOUND cover the current head
  # Used to tell a genuinely pending change request (a gate that still has to
  # be satisfied) from a stale ai:changes label left behind when a fix job lost
  # the race for ai:running and silently exited. A stale label must never stop
  # a PR whose gates already cover the head (#295 froze that way).
  local pr="$1"
  swarm_verdict_covers_head "$pr" review APPROVED || return 1
  if swarm_is_docs_only "$pr"; then
    return 0
  fi
  swarm_verdict_covers_head "$pr" verify TESTS_SOUND
}

swarm_gates_satisfied() { # <pr> — 0 when no required gate is missing for the current head
  [ -z "$(swarm_unsatisfied_gates "$1" 2>/dev/null)" ]
}

swarm_clear_stale_changes() { # <pr> — drop ai:changes when all pre-e2e gates already cover the head
  # Returns 0 when the label is safe to drop (or was absent), 1 when a real
  # change request is still open and the caller must not advance.
  local pr="$1"
  swarm_has_label pr "$pr" ai:changes || return 0
  if swarm_pre_e2e_gates_satisfied "$pr"; then
    echo 'ai:changes is stale (review+verify already cover this head); clearing it'
    swarm_remove_label pr "$pr" ai:changes
    return 0
  fi
  return 1
}

swarm_pr_body_hash() { # <pr> -> stable hash of the PR description (empty-safe)
  gh pr view "$1" --json body --jq '.body // ""' 2>/dev/null | git hash-object --stdin
}

swarm_remote_head_sha() { # <pr> -> head SHA as the git remote reports it
  # Authoritative and immediate. The REST headRefOid can still lag behind a push
  # the fixer just made, and a stale read there reads as "the round changed
  # nothing" - a false escalation to ai:blocked.
  local pr="$1" ref
  ref=$(gh pr view "$pr" --json headRefName --jq .headRefName 2>/dev/null || true)
  [ -n "$ref" ] || return 1
  git ls-remote origin "$ref" 2>/dev/null | cut -f1 | head -n 1
}

swarm_fix_made_no_progress() { # <pr> <head-before> <body-before> -> 0 when the round provably changed nothing
  # Requires positive proof before reporting no progress: a wrong "nothing
  # changed" costs a human, while a missed one only costs one more agent pass.
  local pr="$1" head_before="$2" body_before="$3" head_after body_after
  [ -n "$head_before" ] || return 1
  head_after=$(swarm_remote_head_sha "$pr") || return 1
  [ -n "$head_after" ] || return 1
  [ "$head_after" = "$head_before" ] || return 1
  body_after=$(swarm_pr_body_hash "$pr")
  [ -n "$body_after" ] || return 1
  [ "$body_after" = "$body_before" ]
}

swarm_record_no_progress() { # <pr> <sha> -> running no-progress streak for this sha
  # A round that changed neither the code nor the PR description is not
  # automatically a human problem: it is often a lost/denied push or a session
  # that edited files but never committed them. So the swarm retries with a
  # stronger prompt and only escalates after NO_PROGRESS_MAX consecutive
  # identical rounds — the point at which "the agent cannot move this" is real.
  local pr="$1" sha="$2" n
  n=$(swarm_comments_body pr "$pr" | grep -c "swarm-no-progress sha=$sha " || true)
  n=$((n + 1))
  swarm_say_once pr "$pr" "swarm-no-progress sha=$sha n=$n" \
    "Agent flow (CI): fix round $n of ${NO_PROGRESS_MAX:-3} changed neither the branch nor the PR description; retrying with a stronger prompt before any escalation."
  echo "$n"
}

swarm_open_gates() { # <pr> — wake review, and verify in parallel unless docs-only
  local pr="$1"
  swarm_refire_label pr "$pr" ai:review
  if swarm_is_docs_only "$pr"; then
    echo "docs-only PR #$pr; review gate only"
    return 0
  fi
  swarm_refire_label pr "$pr" ai:verify
  echo "PR #$pr gates: ai:review + ai:verify"
}

swarm_after_review_approved() { # <pr> — drop the review gate; advance only if verify is done or not required
  local pr="$1"
  swarm_remove_label pr "$pr" ai:review
  if swarm_is_docs_only "$pr"; then
    swarm_remove_label pr "$pr" ai:verify
    swarm_add_label pr "$pr" ai:ready
    swarm_say pr "$pr" 'Agent flow (CI): review APPROVED and the diff is docs-only — skipping verify/e2e straight to ai:ready.'
    echo '-> ai:ready (docs-only fast-path)'
    return 0
  fi
  if swarm_has_label pr "$pr" ai:blocked; then
    echo 'blocked already set; not advancing'
    return 0
  fi
  if ! swarm_clear_stale_changes "$pr"; then
    echo 'a change request is still open; not advancing'
    return 0
  fi
  if swarm_has_label pr "$pr" ai:verify; then
    echo 'verify still open; waiting for it'
    return 0
  fi
  if swarm_verdict_covers_head "$pr" verify TESTS_SOUND \
    || swarm_has_label pr "$pr" ai:e2e \
    || swarm_has_label pr "$pr" ai:ready; then
    if ! swarm_has_label pr "$pr" ai:e2e && ! swarm_has_label pr "$pr" ai:ready; then
      swarm_add_label pr "$pr" ai:e2e
      echo '-> ai:e2e'
    fi
    return 0
  fi
  # In-flight PR from before parallel gates: verify was never opened.
  swarm_refire_label pr "$pr" ai:verify
  echo '-> ai:verify (serial fallback)'
}

swarm_after_verify_sound() { # <pr> — drop the verify gate; advance when review is also done
  local pr="$1"
  swarm_remove_label pr "$pr" ai:verify
  if swarm_has_label pr "$pr" ai:blocked; then
    echo 'blocked already set; not advancing'
    return 0
  fi
  if ! swarm_clear_stale_changes "$pr"; then
    echo 'a change request is still open; not advancing'
    return 0
  fi
  if swarm_has_label pr "$pr" ai:review; then
    echo 'review still open; waiting for it'
    return 0
  fi
  if swarm_has_label pr "$pr" ai:e2e || swarm_has_label pr "$pr" ai:ready; then
    echo 'already advanced'
    return 0
  fi
  swarm_add_label pr "$pr" ai:e2e
  echo '-> ai:e2e'
}

swarm_block_pr() { # <pr> <stage|-> <reason> <message>
  # Every PR block goes through here so the sweeper can lift it again. Plain
  # ai:blocked used to mean frozen forever: the refire guard stops every agent
  # and the sweeper from re-entering, and only a fix round that happened to be
  # in flight at the same moment cleared it. Anything blocked while the line was
  # idle stayed stuck for good - #276 and #268 both sat blocked for six hours
  # with green gates. The marker records what to re-arm, why, when, and how many
  # attempts already happened, so recovery is automatic but bounded.
  local pr="$1" stage="$2" reason="$3" message="$4" attempt at
  [ "$stage" = '-' ] || swarm_remove_label pr "$pr" "$stage"
  swarm_add_label pr "$pr" ai:blocked
  attempt=$(( $(swarm_comments_body pr "$pr" | grep -c '<!-- swarm-block ' || true) + 1 ))
  at=$(date +%s)
  swarm_say pr "$pr" "<!-- swarm-block stage=$stage reason=$reason attempt=$attempt at=$at -->"
  swarm_say pr "$pr" "$message"
}

swarm_retry_blocked_prs() { # re-arm stale blocks, bounded by BLOCK_RETRY_MAX
  # A block must never be a dead end. After BLOCK_RETRY_MINUTES with no new
  # activity, re-arm the stage the block recorded - at most BLOCK_RETRY_MAX
  # times, then it stays blocked for a person. Without the cap a genuinely
  # unfixable PR would loop forever, which is what the block was invented to
  # prevent; without the floor the whole line can stop. The retry is purely
  # age-based (a human can pin a PR by re-adding ai:blocked, which resets
  # nothing - so a human who wants it left alone should comment, not relabel).
  local pr marker stage reason attempt at age branch now max_age max_tries
  now=$(date +%s)
  max_age="${BLOCK_RETRY_MINUTES:-120}"
  max_tries="${BLOCK_RETRY_MAX:-2}"
  for pr in $(gh pr list --state open --limit 50 --json number,labels \
    --jq '[.[] | select((.labels | map(.name) | index("ai:blocked")) and (.labels | map(.name) | index("ai:running") | not))] | .[].number' 2>/dev/null); do
    marker=$(swarm_comments_body pr "$pr" | grep '<!-- swarm-block ' | tail -n 1 || true)
    if [ -z "$marker" ]; then
      echo "PR #$pr is blocked without a swarm-block marker; leaving it to a human"
      continue
    fi
    stage=$(printf '%s' "$marker" | sed -n 's/.*stage=\([^ ]*\).*/\1/p')
    reason=$(printf '%s' "$marker" | sed -n 's/.*reason=\([^ ]*\).*/\1/p')
    attempt=$(printf '%s' "$marker" | sed -n 's/.*attempt=\([^ ]*\).*/\1/p')
    at=$(printf '%s' "$marker" | sed -n 's/.*at=\([^ ]*\).*/\1/p')
    if [ -z "$stage" ] || [ "$stage" = '-' ] || [ "$(swarm_uint "${at:-}" x)" = x ]; then
      echo "PR #$pr has an unreadable block marker; leaving it to a human"
      continue
    fi
    attempt=$(swarm_uint "${attempt:-1}" 1)
    if [ "$attempt" -ge "$max_tries" ]; then
      echo "PR #$pr already used $attempt/$max_tries block retries; leaving it to a human"
      continue
    fi
    age=$(( (now - at) / 60 ))
    if [ "$age" -lt "$max_age" ]; then
      echo "PR #$pr blocked ${age}m ago (< ${max_age}m); waiting before the retry"
      continue
    fi
    branch=$(gh pr view "$pr" --json headRefName --jq .headRefName 2>/dev/null || true)
    if [ -n "$branch" ] && swarm_branch_busy "$branch"; then
      echo "PR #$pr has a run in flight; not re-arming the block"
      continue
    fi
    echo "PR #$pr blocked ${age}m on '$reason' with $attempt/$max_tries retries used - re-arming $stage"
    swarm_remove_label pr "$pr" ai:blocked
    swarm_say pr "$pr" "Agent flow: the '$reason' block is ${age}m old and the cooldown has expired, so the swarm re-arms $stage (retry $((attempt + 1))/$max_tries). After that it stays blocked for a human."
    swarm_refire_label pr "$pr" "$stage"
  done
}

swarm_block_for_approval() { # <pr> <stage-label>
  # Last resort: the workflow token can neither approve the parked run nor
  # dispatch a replacement (see swarm_dispatch_ci). Everything automatic has
  # been tried by the time this runs.
  local pr="$1" stage="$2"
  swarm_block_pr "$pr" "$stage" ci-approval \
    "Agent flow (CI): CI is parked on manual approval and the swarm could not approve or re-dispatch it. Needs one human approve on the ci run. The swarm will re-arm ${stage} on its own if nobody acts."
}

swarm_queued_count() { # open ai:implement issues that are not locked
  swarm_uint "$(gh issue list --state open --label ai:implement --json number,labels \
    --jq '[.[] | select((.labels | map(.name) | index("ai:running") | not))] | length' 2>/dev/null || echo '')" 0
}

swarm_label_docs_pr() { # <head-branch> — put a docs PR onto the review fast-path
  local branch="$1" pr
  pr=$(gh pr list --head "$branch" --state open --json number --jq '.[0].number // empty' 2>/dev/null || true)
  if [ -z "$pr" ]; then
    echo "no open PR for $branch"
    return 0
  fi
  if swarm_has_label pr "$pr" ai:ready || swarm_has_label pr "$pr" ai:blocked || swarm_has_label pr "$pr" ai:review; then
    echo "PR #$pr already gated"
    return 0
  fi
  swarm_add_label pr "$pr" ai:review
  echo "docs PR #$pr -> ai:review"
}

swarm_commit_pending() { # commit any working-tree changes the agent left behind
  # The implementer/fixer is told to commit and push itself, but a session that
  # edits files and then ends (limit reached) leaves them uncommitted, and the
  # job's later `git push` would have nothing to send — which then reads as
  # "the round changed nothing" and escalates. The job owns the branch, so it
  # commits what is on disk before pushing. Logs are gitignored (*.log).
  if [ -n "$(git status --porcelain)" ]; then
    git add -A
    if git commit -m 'chore: apply agent changes' >/dev/null 2>&1; then
      echo 'committed working-tree changes the agent left uncommitted'
    fi
  fi
}

swarm_push_branch() { # <branch> [pat] -> 0 pushed | 1 generic failure | 2 workflow-permission failure
  # One place for every push the swarm makes. Falls back to the job's own
  # GITHUB_TOKEN when the PAT is rejected for touching `.github/workflows/*`
  # (a PAT needs the Workflows permission for that; the swarm must not dead-end
  # on it), then to `--force-with-lease` for a rewritten branch.
  local branch="$1" pat="${2:-}" out
  if out=$(git push origin "$branch" 2>&1); then
    return 0
  fi
  if printf '%s' "$out" | grep -qiE 'workflows? permission|refusing to allow|without .*workflows|workflow.*scope'; then
    swarm_error "push of $branch rejected for a workflow-file permission"
    if [ -n "${ACTIONS_TOKEN:-}" ]; then
      swarm_log 'retrying the push with the workflow GITHUB_TOKEN'
      git remote set-url origin "https://x-access-token:${ACTIONS_TOKEN}@github.com/${GITHUB_REPOSITORY}.git"
      git config --local --unset-all "http.https://github.com/.extraheader" 2>/dev/null || true
      if git push origin "$branch"; then
        if [ -n "$pat" ]; then swarm_use_pat_remote "$pat" >/dev/null 2>&1 || true; fi
        return 0
      fi
      if [ -n "$pat" ]; then swarm_use_pat_remote "$pat" >/dev/null 2>&1 || true; fi
    fi
    return 2
  fi
  if printf '%s' "$out" | grep -qiE 'rejected|non-fast-forward|fetch first|stale info'; then
    swarm_log 'push rejected (branch moved); retrying with --force-with-lease'
    if git push --force-with-lease origin "$branch"; then
      return 0
    fi
  fi
  swarm_log "push of $branch failed: $(printf '%s' "$out" | tr '\n' ' ' | cut -c1-200)"
  return 1
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
  # actions/checkout persists `http.<url>.extraheader` with the GITHUB_TOKEN
  # and that header OVERRIDES URL-embedded credentials — without dropping it
  # pushes still go out as github-actions[bot] (whose runs park in
  # action_required). Remove it so the PAT URL credentials take effect.
  git config --local --unset-all "http.https://github.com/.extraheader" 2>/dev/null || true
  echo 'origin rewired to SWARM_PAT credentials (persisted extraheader cleared)'
}

swarm_require_auth() {
  if [ -z "${OPENCODE_API_KEY:-}" ]; then
    echo "::error::OPENCODE_API_KEY secret is not set. Add an OpenCode API key (opencode.ai/auth) to repo Settings > Secrets and variables > Actions, then re-add the trigger label."
    return 1
  fi
}

swarm_stage_lib() { # copy this lib to $RUNNER_TEMP — source THAT copy in later steps
  # Jobs that check out a PR head/merge ref OVERWRITE the workspace, so a later
  # `source scripts/ci/swarm-lib.sh` would load the PR branch's (possibly stale)
  # copy and "command not found" the newer helpers. The workflow file always
  # comes from the default branch, so pin its matching lib before switching.
  # Call as: `source scripts/ci/swarm-lib.sh && swarm_stage_lib` right after the
  # first (default-branch) checkout; later steps use:
  # `source "$RUNNER_TEMP/swarm-lib.sh"`.
  cp scripts/ci/swarm-lib.sh "${RUNNER_TEMP:-/tmp}/swarm-lib.sh"
  echo "swarm-lib pinned at ${RUNNER_TEMP:-/tmp}/swarm-lib.sh"
}

swarm_run_agent() { # <agent> <prompt> <logfile> — never fails the step by itself
  local agent="$1" prompt="$2" log="$3" code=0
  # --auto: the CI runner is an isolated clone, same rule as the local
  # dispatcher (-Auto). Agent permission files still deny pushes to main.
  opencode run --agent "$agent" --auto --format json "$prompt" 2>&1 | tee "$log" || code=$?
  echo "agent $agent exited with code $code (log: $log)"
  return 0
}
