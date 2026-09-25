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

swarm_nudge_stuck_prs() { # re-fire stage labels on PRs that lost their trigger
  # A stage job that times out waiting for CI keeps its label and relies on
  # the ci-completed event to retrigger — but workflow_run does not fire for
  # bot-actor runs, so the PR stalls forever. Re-fire the label (remove+add)
  # on any unlocked stage PR whose ci is not still running; the stage job then
  # re-evaluates immediately (and its wait loop auto-approvals covers
  # action_required runs). Includes ai:changes: a re-route to the fixer that
  # was already labeled emits no event, so this sweep is the safety net.
  # Only PRs WITHOUT ai:running (no live job) and
  # without ai:blocked (human owns those) are touched.
  local pr label state
  for pr in $(gh pr list --state open --limit 50 --json number,labels \
    --jq '[.[] | select((.labels | map(.name) | index("ai:running") | not) and (.labels | map(.name) | index("ai:blocked") | not))] | .[].number' 2>/dev/null); do
    for label in ai:review ai:verify ai:e2e ai:ready ai:changes; do
      if swarm_has_label pr "$pr" "$label"; then
        state=$(swarm_ci_state_once "$pr")
        # approval: the run is parked and re-firing the stage just burns another
        # agent session. A human approves the ci run; the next sweep (state=pass)
        # re-fires. running: a live job owns it.
        if [ "$state" = running ] || [ "$state" = approval ]; then
          echo "PR #$pr $label ci=$state — not re-firing"
        else
          echo "stuck $label PR #$pr (ci=$state) — re-firing the label"
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
  local ttl="${STALE_LOCK_MINUTES:-45}" now kind list num added age
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
        echo "stale lock: $kind #$num (ai:running for ${age}m >= ${ttl}m) — releasing"
        swarm_remove_label "$kind" "$num" ai:running
        swarm_add_label "$kind" "$num" ai:blocked
        swarm_say "$kind" "$num" "Agent flow (CI): stale lock auto-cleared after ${age}m — the job that held it is gone. Re-add the work label to retry."
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
  local run_id="$1" token
  token="${ACTIONS_TOKEN:-}"
  if [ -z "$token" ]; then
    swarm_log "ACTIONS_TOKEN is unset; cannot approve ci run $run_id"
    return 1
  fi
  GH_TOKEN="$token" gh api -X POST "repos/${GITHUB_REPOSITORY}/actions/runs/${run_id}/approve" >/dev/null
}

swarm_wait_ci() { # <pr> <timeout-sec> -> pass | fail | timeout | approval
  # Stdout is ONLY the result token. Diagnostics go to stderr — callers capture
  # this with $(...) and compare it to "pass". A leaked log line used to demote
  # ai:ready PRs back through review even when CI was green.
  #
  # Watches ONLY the `ci` workflow for the PR head SHA. ai-swarm's own checks
  # are ignored: lock-label churn spawns no-op runs whose pending state used
  # to poison this wait (self-deadlock).
  local pr="$1" timeout="$2" waited=0 sha first upper run_id approved=""
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
        if [ -n "$approved" ]; then
          echo approval
          return 0
        fi
        run_id=$(gh run list --workflow ci --commit "$sha" --limit 1 --json databaseId \
          --jq '.[0].databaseId // empty' 2>/dev/null || true)
        if [ -z "$run_id" ]; then
          echo approval
          return 0
        fi
        swarm_log "ci run $run_id needs approval — approving via ACTIONS_TOKEN"
        if ! swarm_approve_run "$run_id"; then
          swarm_log "approve API failed for run $run_id"
          echo approval
          return 0
        fi
        approved=1
        ;;
      *) ;; # running, queued, pending, unknown
    esac
    sleep 30
    waited=$((waited + 30))
  done
  echo timeout
}

swarm_success_covers_head() { # <pr> <success-verdict> <alternation> -> 0 when latest verdict is that success and is newer than head
  # Skips a repeat LLM pass when the head SHA was already judged. A new commit
  # is older than the verdict comment only if the comment came after it.
  local pr="$1" success="$2" pattern="$3" result
  result=$(gh pr view "$pr" --json commits,comments --jq --arg success "$success" --arg pattern "$pattern" '
    ($commits | last | .committedDate // "") as $head
    | if $head == "" then "stale"
      else
        ([.comments[]
          | select(.body | test("VERDICT:\\s*(" + $pattern + ")"))
          | {at:.createdAt, v:(.body | capture("VERDICT:\\s*(?<v>" + $pattern + ")").v)}
        ] | last) as $last
        | if $last == null then "stale"
          elif $last.v != $success then "stale"
          elif $last.at >= $head then "fresh"
          else "stale" end
      end
  ' 2>/dev/null || echo stale)
  [ "$result" = fresh ]
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
  if swarm_has_label pr "$pr" ai:changes || swarm_has_label pr "$pr" ai:blocked; then
    echo 'changes or blocked already set; not advancing'
    return 0
  fi
  if swarm_has_label pr "$pr" ai:verify; then
    echo 'verify still open; waiting for it'
    return 0
  fi
  if swarm_success_covers_head "$pr" TESTS_SOUND 'TESTS_SOUND|TESTS_INSUFFICIENT' \
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
  if swarm_has_label pr "$pr" ai:changes || swarm_has_label pr "$pr" ai:blocked; then
    echo 'changes or blocked already set; not advancing'
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

swarm_block_for_approval() { # <pr> <stage-label>
  local pr="$1" stage="$2"
  swarm_remove_label pr "$pr" "$stage"
  swarm_add_label pr "$pr" ai:blocked
  swarm_say pr "$pr" "Agent flow (CI): CI is waiting for approval and ACTIONS_TOKEN could not approve the run. Needs one human approve on the ci run, then remove ai:blocked and re-add ${stage}. Not sending this back through the agents."
}

swarm_queued_count() { # open ai:implement issues that are not locked
  gh issue list --state open --label ai:implement --json number,labels \
    --jq '[.[] | select((.labels | map(.name) | index("ai:running") | not))] | length' 2>/dev/null || echo 0
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
