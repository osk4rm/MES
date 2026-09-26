#!/usr/bin/env bash
#
# Behaviour tests for scripts/ci/swarm-lib.sh - the liveness helpers.
#
# Run locally (needs bash + jq; the swarm image has both):
#   docker run --rm -i --entrypoint bash -v "$PWD:/repo" asistoffmes-swarm:latest \
#     -c "tr -d '\r' < /repo/scripts/ci/tests/swarm-lib.test.sh | bash -s -- /repo/scripts/ci/swarm-lib.sh"
#
# It also runs in CI as the "swarm self-test" job on every push to master, on
# the sweep schedule and on manual dispatch, because a broken helper here does
# not fail loudly - it silently disables a gate, and that is exactly how the
# swarm froze overnight on 2026-09-26 (a search API 404 that fed a JSON body
# into `[ -ge ]` made the capacity check a no-op and 17 fix loops piled up).
##!/bin/sh
# Functional test for the liveness fixes: integer-safe counters, the
# ci-dispatch escape hatch, the bounded block retry and the
# issue-already-has-a-PR guard.
#
# The gh stub applies --jq with real jq and returns the SAME JSON shapes gh
# does ({"headRefName":...}, {"comments":[...]}, not bare arrays). A dumb stub
# that just echoes the payload would "pass" a function whose query is wrong -
# which is exactly how the search/issues 404 and the empty headRefName survived.
set -u

LIB="${1:?path to swarm-lib.sh}"
PASS=0
FAIL=0

ok() { PASS=$((PASS + 1)); printf '  ok   %s\n' "$1"; }
no() { FAIL=$((FAIL + 1)); printf '  FAIL %s\n' "$1"; }
check() { # <desc> <expected> <actual>
  if [ "$2" = "$3" ]; then ok "$1"; else no "$1 (expected '$2', got '$3')"; fi
}
checkc() { # <desc> <unexpected-substring> <haystack>
  case "$3" in
    *"$2"*) no "$1 (found '$2')" ;;
    *) ok "$1" ;;
  esac
}
checkh() { # <desc> <required-substring> <haystack>
  case "$3" in
    *"$2"*) ok "$1" ;;
    *) no "$1 (missing '$2' in '$3')" ;;
  esac
}

export GITHUB_REPOSITORY=osk4rm/MES
export ACTIONS_TOKEN=fake-token
export MAX_PARALLEL=3
export CI_APPROVAL_TRIES=2

SHA=aaaa1111bbbb2222cccc3333dddd4444eeee5555
HEAD=$SHA
PR_BRANCH=''
INFLIGHT='{"total_count":0}'
PR_LIST='[]'
ISSUES='[]'
LABELS=''
RUNS='[]'
APPROVE_RC=1
NOW=$(date +%s)

# Side effects go to files, not shell variables: the library calls gh from
# inside $(...) subshells (out=$(gh workflow run ...)), and a variable assigned
# in a subshell dies with it - which silently turned these assertions into
# "nothing happened" instead of a real check.
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
EDITS_LOG=$WORK/edits
DISPATCH_LOG=$WORK/dispatch
COMMENTS_FILE=$WORK/comments
: >"$EDITS_LOG"
: >"$DISPATCH_LOG"
: >"$COMMENTS_FILE"

jsonstr() { printf '%s' "$1" | jq -Rs .; } # raw text -> JSON string literal
comments() { cat "$COMMENTS_FILE"; } # what swarm_comments_body would see
last_marker() { grep '<!-- swarm-block ' "$COMMENTS_FILE" | tail -n 1; }
reset_logs() { : >"$EDITS_LOG"; : >"$DISPATCH_LOG"; }

gh() {
  JQ=''
  JFIELDS=''
  prev=''
  for a in "$@"; do
    [ "$prev" = '--jq' ] && JQ="$a"
    [ "$prev" = '--json' ] && JFIELDS="$a"
    prev="$a"
  done
  emit() { # <canned json>
    if [ -n "$JQ" ]; then printf '%s' "$1" | jq -r "$JQ" 2>/dev/null; else printf '%s' "$1"; fi
  }
  case "$*" in
    *search/issues*) emit "$INFLIGHT" ;;
    *'workflow list'*) emit '[{"name":"ci","path":".github/workflows/ci.yml","state":"active"}]' ;;
    *'workflow run'*) printf '%s\n' "$*" >>"$DISPATCH_LOG"; return 0 ;;
    *'/approve'*) return "$APPROVE_RC" ;;
    *'--body-file'*) cat "$5" >>"$COMMENTS_FILE" 2>/dev/null; printf '\n' >>"$COMMENTS_FILE" ;;
    *'pr list'*) emit "$PR_LIST" ;;
    *'issue list'*) emit "$ISSUES" ;;
    *'run list'*) emit "$RUNS" ;;
    *'pr view'*)
      case ",$JFIELDS," in
        *,comments,*) emit "{\"comments\":[{\"body\":$(jsonstr "$(comments)")}]}" ;;
        *,labels,*) emit "{\"labels\":[{\"name\":$(jsonstr "$LABELS")}]}" ;;
        *,headRefName,headRefOid,*) emit "{\"headRefName\":$(jsonstr "$PR_BRANCH"),\"headRefOid\":$(jsonstr "$HEAD")}" ;;
        *,headRefName,*) emit "{\"headRefName\":$(jsonstr "$PR_BRANCH")}" ;;
        *,headRefOid,*) emit "{\"headRefOid\":$(jsonstr "$HEAD")}" ;;
        *) emit '{}' ;;
      esac
      ;;
    *'issue view'*)
      case ",$JFIELDS," in
        *,labels,*) emit "{\"labels\":[{\"name\":$(jsonstr "$LABELS")}]}" ;;
        *) emit '{}' ;;
      esac
      ;;
    *edit*) printf '%s\n' "$*" >>"$EDITS_LOG" ;;
    *) : ;;
  esac
  return 0
}
git() { return 0; }

# shellcheck disable=SC1090
. "$LIB"

echo 'swarm_uint keeps non-numbers out of [ -ge ]'
check 'plain number passes through' 3 "$(swarm_uint 3)"
check 'json error body falls back' 0 "$(swarm_uint '{"message":"Not Found","status":"404"}' 0)"
check 'json body plus the old || echo 0 falls back' 0 "$(swarm_uint '{"message":"Not Found","status":"404"}0' 0)"
check 'empty string falls back' 0 "$(swarm_uint '' 0)"
check 'a negative value falls back' 0 "$(swarm_uint -1 0)"

echo 'swarm_in_flight_count is a number, never a JSON body'
check 'a real total_count passes through' 2 "$(INFLIGHT='{"total_count":2}'; swarm_in_flight_count 2>/dev/null)"
check 'a 404 body becomes 0' 0 "$(INFLIGHT='{   "message": "Not Found", "status": "404" }'; swarm_in_flight_count 2>/dev/null | tail -n 1)"
check 'an empty body becomes 0' 0 "$(INFLIGHT='null'; swarm_in_flight_count 2>/dev/null | tail -n 1)"

echo 'the capacity comparison no longer explodes'
if [ "$(INFLIGHT='{ "message": "Not Found", "status": "404" }'; swarm_in_flight_count 2>/dev/null)" -ge 3 ] 2>/dev/null; then
  no '[: -ge ] still receives a JSON body'
else
  ok '[: -ge ] gets a clean integer'
fi

echo 'annotations never leak into a captured value'
check 'swarm_error writes to stderr only' '' "$(swarm_error 'test annotation' 2>/dev/null)"

echo 'swarm_dispatch_ci is the human-free way past action_required'
reset_logs
APPROVE_RC=1
PR_BRANCH='ai/issue-272-smoke'
if swarm_dispatch_ci 285 "$SHA" 2>/dev/null; then ok 'dispatch succeeds'; else no 'dispatch failed'; fi
checkh 'it dispatches the ci workflow' 'workflow run .github/workflows/ci.yml' "$(cat "$DISPATCH_LOG")"
checkh 'on the PR head branch' '--ref ai/issue-272-smoke' "$(cat "$DISPATCH_LOG")"
reset_logs
PR_BRANCH=''
if swarm_dispatch_ci 285 "$SHA" 2>/dev/null; then no 'dispatch without a branch should fail'; else ok 'no branch -> no dispatch'; fi
checkc 'nothing is dispatched without a branch' 'workflow run' "$(cat "$DISPATCH_LOG")"

echo 'swarm_issue_has_open_pr stops the 17x re-implement loop'
PR_LIST='[{"number":276,"headRefName":"ai/issue-272-smoke"}]'
if swarm_issue_has_open_pr 272; then ok 'an issue with an open PR is detected'; else no 'open PR not detected'; fi
if swarm_issue_has_open_pr 281; then no 'a different issue must not match'; else ok 'other issues are unaffected'; fi
PR_LIST='[]'
if swarm_issue_has_open_pr 272; then no 'no PR -> not detected'; else ok 'no open PR -> re-implementable'; fi

echo 'swarm_oldest_queued_issue never re-fires an implemented issue'
PR_LIST='[{"number":276,"headRefName":"ai/issue-272-smoke"}]'
ISSUES='[{"number":272,"createdAt":"2026-01-01T00:00:00Z","labels":[{"name":"ai:implement"}]}]'
check 'the queued issue with a PR is skipped' '' "$(swarm_oldest_queued_issue 2>/dev/null)"
ISSUES='[{"number":272,"createdAt":"2026-01-01T00:00:00Z","labels":[{"name":"ai:implement"}]},{"number":290,"createdAt":"2026-02-01T00:00:00Z","labels":[{"name":"ai:implement"}]}]'
check 'the next queued issue is returned instead' 290 "$(swarm_oldest_queued_issue 2>/dev/null)"
ISSUES='[{"number":290,"createdAt":"2026-02-01T00:00:00Z","labels":[{"name":"ai:implement"}]}]'
check 'a queued issue with no PR is still returned' 290 "$(swarm_oldest_queued_issue 2>/dev/null)"

echo 'swarm_retry_blocked_prs re-arms a stale block, but only within its budget'
PR_LIST='[{"number":276,"labels":[{"name":"ai:blocked"}]}]'
PR_BRANCH='ai/issue-272-smoke'
: >"$COMMENTS_FILE"
printf '<!-- swarm-block stage=ai:changes reason=no-progress attempt=1 at=%s -->\n' "$NOW" >>"$COMMENTS_FILE"
reset_logs
swarm_retry_blocked_prs >/dev/null
checkc 'a block younger than the cooldown is left alone' 'edit' "$(cat "$EDITS_LOG")"
: >"$COMMENTS_FILE"
printf '<!-- swarm-block stage=ai:e2e reason=no-progress attempt=1 at=%s -->\n' "$((NOW - 7200))" >>"$COMMENTS_FILE"
reset_logs
swarm_retry_blocked_prs >/dev/null
checkh 'a stale block is re-armed' 'add-label ai:e2e' "$(cat "$EDITS_LOG")"
checkh 'and the block label is dropped first' 'remove-label ai:blocked' "$(cat "$EDITS_LOG")"
: >"$COMMENTS_FILE"
printf '<!-- swarm-block stage=ai:e2e reason=no-progress attempt=2 at=%s -->\n' "$((NOW - 7200))" >>"$COMMENTS_FILE"
reset_logs
swarm_retry_blocked_prs >/dev/null
checkc 'a block that used its retries stays blocked' 'edit' "$(cat "$EDITS_LOG")"
: >"$COMMENTS_FILE"
printf 'Agent flow: needs human attention.\n' >>"$COMMENTS_FILE"
reset_logs
swarm_retry_blocked_prs >/dev/null
checkc 'a block without the marker is left to a human' 'edit' "$(cat "$EDITS_LOG")"
: >"$COMMENTS_FILE"
printf '<!-- swarm-block stage= reason= at= -->\n' >>"$COMMENTS_FILE"
reset_logs
swarm_retry_blocked_prs >/dev/null
checkc 'an unreadable marker is left to a human' 'edit' "$(cat "$EDITS_LOG")"

echo 'swarm_block_pr records a retryable marker'
: >"$COMMENTS_FILE"
reset_logs
swarm_block_pr 276 ai:verify no-verdict 'Agent flow: verifier produced no verdict.' >/dev/null 2>&1
checkh 'the stage label is removed' 'remove-label ai:verify' "$(cat "$EDITS_LOG")"
checkh 'the block label is added' 'add-label ai:blocked' "$(cat "$EDITS_LOG")"
checkh 'the marker names the stage' 'stage=ai:verify' "$(comments)"
checkh 'the marker names the reason' 'reason=no-verdict' "$(comments)"
checkh 'the marker counts attempts from 1' 'attempt=1' "$(comments)"
checkh 'the marker stamps the time' 'at=' "$(comments)"
: >"$COMMENTS_FILE"
printf '<!-- swarm-block stage=ai:verify reason=no-verdict attempt=1 at=1 -->\n' >>"$COMMENTS_FILE"
swarm_block_pr 276 ai:verify no-verdict 'Agent flow: again.' >/dev/null 2>&1
checkh 'the second block counts attempt=2' 'attempt=2' "$(last_marker)"
: >"$COMMENTS_FILE"
printf '<!-- swarm-block stage=ai:changes reason=no-progress attempt=1 at=1 -->\n' >>"$COMMENTS_FILE"
swarm_block_pr 276 ai:verify no-verdict 'Agent flow: other reason.' >/dev/null 2>&1
checkh 'the budget is per PR, shared across reasons (stricter on purpose)' \
  'attempt=2' "$(last_marker)"

echo 'swarm_clear_stale_changes drops only a genuinely stale ai:changes'
LABELS='ai:changes'
: >"$COMMENTS_FILE"
printf '<!-- swarm-verdict gate=review sha=%s verdict=APPROVED -->\n' "$HEAD" >>"$COMMENTS_FILE"
printf '<!-- swarm-verdict gate=verify sha=%s verdict=TESTS_SOUND -->\n' "$HEAD" >>"$COMMENTS_FILE"
reset_logs
if swarm_clear_stale_changes 276 >/dev/null; then ok 'stale changes cleared'; else no 'stale changes not cleared'; fi
checkh 'the stale label is removed' 'remove-label ai:changes' "$(cat "$EDITS_LOG")"
: >"$COMMENTS_FILE"
printf '<!-- swarm-verdict gate=review sha=%s verdict=APPROVED -->\n' "$HEAD" >>"$COMMENTS_FILE"
reset_logs
if swarm_clear_stale_changes 276 >/dev/null; then no 'a real change request must not be cleared'; else ok 'a real change request is kept'; fi
checkc 'nothing is removed when changes are real' 'remove-label ai:changes' "$(cat "$EDITS_LOG")"

echo 'swarm_record_no_progress counts the streak for one sha'
: >"$COMMENTS_FILE"
check 'first no-progress is 1' 1 "$(swarm_record_no_progress 276 "$HEAD" 2>/dev/null)"
check 'second no-progress is 2' 2 "$(swarm_record_no_progress 276 "$HEAD" 2>/dev/null)"
check 'a different sha starts over' 1 "$(swarm_record_no_progress 276 deadbeef 2>/dev/null)"

echo 'the nudge heals a PR parked on ci=approval'
PR_LIST='[{"number":285,"headRefName":"ai/issue-265-fanout","labels":[{"name":"ai:e2e"}]}]'
PR_BRANCH='ai/issue-265-fanout'
LABELS='ai:e2e'
RUNS='[{"status":"completed","conclusion":"action_required"}]'
reset_logs
swarm_nudge_stuck_prs >/dev/null 2>&1
checkh 'ci=approval is re-fired instead of skipped' 'add-label ai:e2e' "$(cat "$EDITS_LOG")"
RUNS='[{"status":"in_progress","conclusion":""}]'
reset_logs
swarm_nudge_stuck_prs >/dev/null 2>&1
checkc 'ci=running is still left alone' 'edit' "$(cat "$EDITS_LOG")"

echo 'swarm_say_once does not repeat itself'
: >"$COMMENTS_FILE"
swarm_say_once pr 285 'swarm-dispatch sha=abc' 'dispatched' >/dev/null 2>&1
FIRST=$(comments)
checkh 'the marker is persisted for the next run' 'swarm-dispatch sha=abc' "$FIRST"
swarm_say_once pr 285 'swarm-dispatch sha=abc' 'dispatched' >/dev/null 2>&1
check 'a repeated marker comments only once' "$FIRST" "$(comments)"
swarm_say_once pr 285 'swarm-dispatch sha=def' 'dispatched again' >/dev/null 2>&1
checkh 'a new marker for the same PR does comment' 'swarm-dispatch sha=def' "$(comments)"

echo 'the helpers survive `bash -e`, which is how Actions runs every step'
# `set -e` turns any non-zero helper return into a failed step. The guard in
# swarm_in_flight_count is a bare `[ ... ] && ...`; if bash ever treated the
# happy (numeric) path as a failure the whole capacity gate would exit 1 and
# kill the sweep job.
if (
  set -e
  INFLIGHT='{"total_count":2}'
  n=$(swarm_in_flight_count)
  case "$n" in 2) ;; *) exit 1 ;; esac
  : >"$COMMENTS_FILE"
  swarm_block_pr 276 ai:verify no-verdict 'Agent flow: under -e.' >/dev/null
  swarm_say_once pr 276 'swarm-dispatch sha=zzz' 'dispatched' >/dev/null
  swarm_retry_blocked_prs >/dev/null
  exit 0
) 2>/dev/null; then
  ok 'capacity + block + say_once + retry run clean under set -e'
else
  no 'something aborted the step under set -e'
fi

printf '\n%d passed, %d failed\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ]
