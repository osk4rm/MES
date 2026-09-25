<#
.SYNOPSIS
    Local orchestrator for the MES Agent Swarm (docs/agent-workflow.md).

.DESCRIPTION
    Picks an open GitHub issue labelled `ai:implement`, runs the mes-implementer
    agent to implement it and open a PR, then loops:
        mes-reviewer -> (if changes requested) mes-implementer fixes
    until the PR passes (MaxRounds = 0, the default, means unlimited rounds).
    A nonzero -MaxRounds caps the loop; when exhausted the issue is labelled
    `ai:blocked` for human attention.

.PARAMETER MaxRounds
    Maximum number of review -> fix rounds. 0 = unlimited (default).

.PARAMETER ImplementLabel
    Issue label the orchestrator consumes. Default `ai:implement`.

.PARAMETER BlockedLabel
    Label applied when the loop is exhausted. Default `ai:blocked`.

.PARAMETER Auto
    Pass --auto to opencode (auto-approve permissions that are not explicitly
    denied). Only use on an isolated clone.

.PARAMETER SyncTracker
    After the loop, run mes-tracker to reconcile docs/feature-tracker.md with
    GitHub and open a tracker-sync PR.

.NOTES
    Requires: git, gh (authenticated), opencode. Run from the repository root.
    Each agent runs in its own fresh session; only the implementer's session is
    reused across review rounds, so its code context survives.
#>
[CmdletBinding()]
param(
    [int]$MaxRounds = 0,
    [string]$ImplementLabel = 'ai:implement',
    [string]$BlockedLabel = 'ai:blocked',
    [switch]$Auto,
    [switch]$SyncTracker
)

$ErrorActionPreference = 'Continue'

function Invoke-Agent {
    param(
        [Parameter(Mandatory)][string]$Agent,
        [Parameter(Mandatory)][string]$Prompt,
        [string]$Session
    )
    $cliArgs = @('run', '--agent', $Agent, '--format', 'json')
    if ($Session) { $cliArgs += @('--session', $Session) }
    if ($Auto)    { $cliArgs += '--auto' }
    $cliArgs += $Prompt

    Write-Host "    > opencode run --agent $Agent$(if ($Session) { ' (continued session)' })"
    return (& opencode @cliArgs 2>&1 | Out-String)
}

function Get-SessionId {
    param([string]$Raw)
    foreach ($line in ($Raw -split "`n")) {
        if ($line -match '"sessionID"\s*:\s*"(ses_[^"]+)"') { return $Matches[1] }
    }
    return $null
}

function Get-Verdict {
    param([string]$Raw)
    # Strict: all VERDICT mentions must agree; mixed values -> AMBIGUOUS.
    $matches = [regex]::Matches($Raw, 'VERDICT:\s*(APPROVED|CHANGES_REQUESTED|TESTS_SOUND|TESTS_INSUFFICIENT)')
    if ($matches.Count -eq 0) { return 'UNKNOWN' }
    $distinct = @($matches | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    if ($distinct.Count -gt 1) { return 'AMBIGUOUS' }
    return $distinct[0]
}

# ---------------------------------------------------------------------------
# 1. Pick the next issue
# ---------------------------------------------------------------------------
$issues = gh issue list --label $ImplementLabel --state open --limit 1 `
    --json 'number,title' | ConvertFrom-Json
if (-not $issues) {
    Write-Host "No open issue labelled '$ImplementLabel'. Nothing to do."
    return
}
$num   = $issues.number
$title = $issues.title
Write-Host "==> Issue #$num : $title"

# ---------------------------------------------------------------------------
# 2. Implement (fresh session)
# ---------------------------------------------------------------------------
Write-Host "==> Implementation"
$implRaw = Invoke-Agent -Agent 'mes-implementer' -Prompt (
    "Implement GitHub issue #$num. Read it with 'gh issue view $num --comments'. " +
    "Follow AGENT.md and the area instructions. Write tests, run " +
    "'dotnet build AsistOff.MES.sln', 'dotnet test AsistOff.MES.sln' and " +
    "'cd AsistOff.MES.Web; npm run build', then open a PR that closes #$num. " +
    "Use branch name ai/issue-$num-<slug>."
)
$implSession = Get-SessionId $implRaw
Write-Host "    session: $implSession"

$pr = gh pr list --state open --json 'number,headRefName' |
    ConvertFrom-Json |
    Where-Object { $_.headRefName -like "ai/issue-$num-*" } |
    Select-Object -First 1
if (-not $pr) {
    Write-Host "==> Implementer did not open a PR. Labelling #$num '$BlockedLabel'."
    gh issue edit $num --add-label $BlockedLabel
    return
}
$prNum = $pr.number
Write-Host "==> PR #$prNum opened ($($pr.headRefName))."

# ---------------------------------------------------------------------------
# 3. Review -> fix loop
# ---------------------------------------------------------------------------
$round  = 0
$passed = $false
while ($MaxRounds -le 0 -or $round -lt $MaxRounds) {
    $round++
    $roundTag = if ($MaxRounds -gt 0) { "$round/$MaxRounds" } else { "$round (unlimited)" }
    Write-Host "==> Review round $roundTag (fresh reviewer session)"
    $reviewRaw = Invoke-Agent -Agent 'mes-reviewer' -Prompt (
        "Review PR #$prNum for AsistOff MES. Inspect only the diff with " +
        "'gh pr diff $prNum'. Post your findings with 'gh pr comment $prNum' " +
        "and end the body with exactly one verdict on its own line: " +
        "'VERDICT: APPROVED' or 'VERDICT: CHANGES_REQUESTED'. " +
        "Do not write any other VERDICT line."
    )
    $verdict = Get-Verdict $reviewRaw
    Write-Host "    review verdict: $verdict"

    if ($verdict -eq 'APPROVED') {
        Write-Host "==> Verify round $roundTag (fresh verifier session, read-only)"
        $verifyRaw = Invoke-Agent -Agent 'mes-verifier' -Prompt (
            "Verify that the tests in PR #$prNum genuinely prove the acceptance criteria " +
            "of issue #$num. Read-only audit: inspect the diff with 'gh pr diff $prNum', " +
            "map each criterion to test(s), check for weakened tests. DO NOT write files. " +
            "Post the report with 'gh pr comment $prNum' ending with exactly one verdict " +
            "on its own line: 'VERDICT: TESTS_SOUND' or 'VERDICT: TESTS_INSUFFICIENT'."
        )
        $vverdict = Get-Verdict $verifyRaw
        Write-Host "    verify verdict: $vverdict"
        if ($vverdict -eq 'TESTS_SOUND') { $passed = $true; break }
        $verdict = "VERIFY_$vverdict"
    }

    Write-Host "==> Changes requested ($verdict); implementer continues session $implSession"
    Invoke-Agent -Agent 'mes-implementer' -Session $implSession -Prompt (
        "Review/verification on PR #$prNum requested changes ($verdict). Read them with " +
        "'gh pr view $prNum --comments', fix every point, re-run " +
        "'dotnet build AsistOff.MES.sln', 'dotnet test AsistOff.MES.sln' and " +
        "'cd AsistOff.MES.Web; npm run build', then push to the same branch " +
        "and reply to the review."
    ) | Out-Null
}

if ($passed) {
    Write-Host "==> PR #$prNum approved. Ready for a human merge."
} else {
    Write-Host "==> Max rounds ($MaxRounds) reached. Labelling #$num '$BlockedLabel'."
    gh issue edit $num --add-label $BlockedLabel
}

# ---------------------------------------------------------------------------
# 4. Optional: reconcile the feature tracker
# ---------------------------------------------------------------------------
if ($SyncTracker) {
    Write-Host "==> Syncing feature tracker"
    Invoke-Agent -Agent 'mes-tracker' -Prompt (
        "Reconcile docs/feature-tracker.md with GitHub issues/PRs and the " +
        "codebase, then publish the update on branch ai/tracker-sync and open " +
        "a PR. Abort if the working tree is dirty."
    ) | Out-Null
}
