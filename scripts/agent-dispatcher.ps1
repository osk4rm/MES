<#
.SYNOPSIS
    Local label-driven dispatcher for the MES Agent Swarm.

.DESCRIPTION
    A long-running loop that watches GitHub and reacts to label transitions
    (see docs/agent-workflow.md):

        issue [ai:implement]           -> mes-implementer -> PR [ai:review]
        PR [ai:review] + CI green      -> mes-reviewer    -> [ai:verify] / [ai:changes]
        PR [ai:verify]                 -> mes-verifier (read-only) -> [ai:e2e] / [ai:changes]
        PR [ai:changes]                -> mes-implementer (same session) -> [ai:review]
        PR [ai:e2e]                    -> mes-e2e-tester  -> [ai:ready] / [ai:changes] / [ai:blocked]
        PR [ai:ready]                  -> human merges

    It also reconciles docs/feature-tracker.md autonomously: whenever the set of
    open work items changes (or a cooldown/interval elapses) it runs mes-tracker,
    which publishes a PR on branch ai/tracker-sync. No manual button required.

    The dispatcher owns every workflow label; agents never touch them.

.PARAMETER IntervalSeconds
    Sleep between cycles. Default 20.

.PARAMETER MaxRounds
    Max review->fix / e2e->fix rounds per PR before ai:blocked. Default 3.

.PARAMETER Once
    Run a single cycle and exit (useful for testing and the dashboard).

.PARAMETER DryRun
    Print the planned action without running any agent or changing labels.

.PARAMETER Auto
    Pass --auto to opencode (only on an isolated clone).

.PARAMETER NoTracker
    Disable the autonomous feature-tracker reconciliation.

.PARAMETER TrackerIntervalMinutes
    Force a tracker sync at most this often even without a signature change.
    Default 30.

.PARAMETER TrackerCooldownMinutes
    Minimum time between two tracker syncs. Default 10.

.PARAMETER RunningTtlMinutes
    ai:running locks older than this are considered orphaned and cleared on
    startup. Default 30. Fresh locks are kept (may belong to a live agent).

.NOTES
    Requires: git, gh (authenticated), opencode, and (for e2e) Playwright MCP.
    Run from the repository root. State is kept in %TEMP%\opencode\dispatcher-state.json.
#>
[CmdletBinding()]
param(
    [int]$IntervalSeconds = 20,
    [int]$MaxRounds = 3,
    [switch]$Once,
    [switch]$DryRun,
    [switch]$Auto,
    [switch]$NoTracker,
    [int]$TrackerIntervalMinutes = 30,
    [int]$TrackerCooldownMinutes = 10,
    [int]$RunningTtlMinutes = 30
)

$ErrorActionPreference = 'Continue'

$Tmp = if ($env:TEMP) { $env:TEMP } elseif ($env:TMPDIR) { $env:TMPDIR } else { '/tmp' }
$LogDir = Join-Path $Tmp 'opencode'
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$StateFile = Join-Path $LogDir 'dispatcher-state.json'
$LockFile = Join-Path $LogDir 'dispatcher.lock'

function Acquire-Lock {
    if (Test-Path $LockFile) {
        $old = Get-Content $LockFile -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($old -and (Get-Process -Id ([int]$old) -ErrorAction SilentlyContinue)) {
            Write-Host "Another dispatcher is already running (pid $old). Exiting."
            return $false
        }
    }
    Set-Content -Path $LockFile -Value $PID -Encoding ascii
    return $true
}

function Release-Lock {
    Remove-Item $LockFile -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------
# State
# ---------------------------------------------------------------------------
function Get-State {
    if (Test-Path $StateFile) {
        try {
            $s = Get-Content -Raw $StateFile | ConvertFrom-Json
            if (-not $s.issues) { $s | Add-Member -NotePropertyName issues -NotePropertyValue ([pscustomobject]@{}) -Force }
            if (-not $s.prs) { $s | Add-Member -NotePropertyName prs -NotePropertyValue ([pscustomobject]@{}) -Force }
            if (-not $s.tracker) { $s | Add-Member -NotePropertyName tracker -NotePropertyValue ([pscustomobject]@{ lastSync = 0; lastSignature = '' }) -Force }
            return $s
        } catch { }
    }
    return [pscustomobject]@{
        issues = [pscustomobject]@{}
        prs = [pscustomobject]@{}
        tracker = [pscustomobject]@{ lastSync = 0; lastSignature = '' }
    }
}

function Save-State {
    param($State)
    $State | ConvertTo-Json -Depth 8 | Set-Content -Path $StateFile -Encoding utf8
}

function Get-Prop {
    param($Obj, [string]$Name)
    if ($Obj -and ($Obj.PSObject.Properties.Name -contains $Name)) { return $Obj.$Name }
    return $null
}

# ---------------------------------------------------------------------------
# GitHub helpers
# ---------------------------------------------------------------------------
function Invoke-Gh {
    param([string[]]$GhArgs)
    return (& gh @GhArgs 2>&1 | Out-String)
}

function GhJson {
    param([string[]]$GhArgs)
    $out = Invoke-Gh $GhArgs
    try { return ($out | ConvertFrom-Json) } catch { return @() }
}

function Get-LabelNames {
    param($Item)
    if ($Item -and $Item.labels) { return @($Item.labels | ForEach-Object { $_.name }) }
    return @()
}

function Has-Label {
    param($Item, [string]$Name)
    return ((Get-LabelNames $Item) -contains $Name)
}

function Add-Label {
    param([string]$Kind, $Number, [string]$Label)
    if ($DryRun) { Write-Host "      [dry] $Kind #$Number +$Label"; return }
    Invoke-Gh @($Kind, 'edit', "$Number", '--add-label', $Label) | Out-Null
}

function Remove-Label {
    param([string]$Kind, $Number, [string]$Label)
    if ($DryRun) { Write-Host "      [dry] $Kind #$Number -$Label"; return }
    Invoke-Gh @($Kind, 'edit', "$Number", '--remove-label', $Label) | Out-Null
}

function Add-Comment {
    param([string]$Kind, $Number, [string]$Body)
    if ($DryRun) { Write-Host "      [dry] comment on $Kind #$Number"; return }
    Invoke-Gh @($Kind, 'comment', "$Number", '--body', $Body) | Out-Null
}

function Get-CiState {
    param($PrNumber)
    # Mirrors swarm_wait_ci: watches ONLY the `ci` workflow runs for the PR
    # head SHA. ai-swarm's own check runs are ignored — lock-label churn
    # spawns no-op runs that queue behind the lock holder, and their pending
    # checks used to poison this wait into timeouts (self-deadlock).
    $sha = Invoke-Gh @('pr', 'view', "$PrNumber", '--json', 'headRefOid', '--jq', '.headRefOid')
    if (-not $sha) { return 'pass' }
    $raw = Invoke-Gh @('run', 'list', '--workflow', 'ci', '--commit', "$sha", '--limit', '5', '--json', 'status,conclusion')
    $runs = $null
    try { $runs = $raw | ConvertFrom-Json } catch { $runs = @() }
    if (-not $runs -or @($runs).Count -eq 0) { return 'none' }
    $first = @($runs)[0]  # gh sorts newest-first
    if ($first.status -ne 'completed') { return 'pending' }
    if (@('failure', 'timed_out', 'cancelled', 'startup_failure', 'stale') -contains $first.conclusion) { return 'fail' }
    if (@('success', 'skipped', 'neutral') -contains $first.conclusion) { return 'pass' }
    return 'pending'  # action_required & co: a human may still approve
}

# ---------------------------------------------------------------------------
# Agent helpers
# ---------------------------------------------------------------------------
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
    if ($DryRun) { return '' }
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
    param([string]$Raw, [string]$Pattern)
    # Strict: verdict must be unambiguous. Multiple DISTINCT verdicts in one
    # output (e.g. reasoning quotes the other option) -> AMBIGUOUS -> blocked.
    # Callers take the last match only when all matches agree.
    $matches = [regex]::Matches($Raw, $Pattern)
    if ($matches.Count -eq 0) { return 'UNKNOWN' }
    $values = @($matches | ForEach-Object { $_.Groups[1].Value })
    $distinct = @($values | Sort-Object -Unique)
    if ($distinct.Count -gt 1) { return 'AMBIGUOUS' }
    return $distinct[0]
}

function Get-IssueFromBranch {
    param([string]$Branch)
    if ($Branch -match '^ai/issue-(\d+)-') { return [int]$Matches[1] }
    return $null
}

function Get-SessionForIssue {
    param($State, [int]$IssueNumber)
    $entry = Get-Prop $State.issues "$IssueNumber"
    return (Get-Prop $entry 'sessionId')
}

function Set-IssueState {
    param($State, [int]$IssueNumber, $Session, $Pr, $Rounds)
    $entry = Get-Prop $State.issues "$IssueNumber"
    if (-not $entry) { $entry = [pscustomobject]@{} }
    if ($PSBoundParameters.ContainsKey('Session') -and $Session) { $entry | Add-Member -NotePropertyName sessionId -NotePropertyValue $Session -Force }
    if ($PSBoundParameters.ContainsKey('Pr') -and $Pr) { $entry | Add-Member -NotePropertyName pr -NotePropertyValue $Pr -Force }
    if ($PSBoundParameters.ContainsKey('Rounds') -and $Rounds) { $entry | Add-Member -NotePropertyName rounds -NotePropertyValue $Rounds -Force }
    $State.issues | Add-Member -NotePropertyName "$IssueNumber" -NotePropertyValue $entry -Force
}

function Get-Rounds {
    param($State, $PrNumber)
    $entry = Get-Prop $State.prs "$PrNumber"
    $r = Get-Prop $entry 'rounds'
    if ($r) { return [int]$r }
    return 0
}

function Set-Rounds {
    param($State, $PrNumber, [int]$Rounds)
    $entry = Get-Prop $State.prs "$PrNumber"
    if (-not $entry) { $entry = [pscustomobject]@{} }
    $entry | Add-Member -NotePropertyName rounds -NotePropertyValue $Rounds -Force
    $State.prs | Add-Member -NotePropertyName "$PrNumber" -NotePropertyValue $entry -Force
}

function Reset-Rounds {
    param($State, $PrNumber)
    Set-Rounds $State $PrNumber 0
}

function Set-LockTimestamp {
    param($State, [string]$Kind, $Number)
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $owner = Get-LockOwner
    if ($Kind -eq 'issue') {
        $entry = Get-Prop $State.issues "$Number"
        if (-not $entry) { $entry = [pscustomobject]@{} }
        $entry | Add-Member -NotePropertyName lockAcquiredAt -NotePropertyValue $now -Force
        $entry | Add-Member -NotePropertyName lockOwner -NotePropertyValue $owner -Force
        $State.issues | Add-Member -NotePropertyName "$Number" -NotePropertyValue $entry -Force
    } else {
        $entry = Get-Prop $State.prs "$Number"
        if (-not $entry) { $entry = [pscustomobject]@{} }
        $entry | Add-Member -NotePropertyName lockAcquiredAt -NotePropertyValue $now -Force
        $entry | Add-Member -NotePropertyName lockOwner -NotePropertyValue $owner -Force
        $State.prs | Add-Member -NotePropertyName "$Number" -NotePropertyValue $entry -Force
    }
    Save-State $State
}

function Clear-LockTimestamp {
    param($State, [string]$Kind, $Number)
    $entry = if ($Kind -eq 'issue') { Get-Prop $State.issues "$Number" } else { Get-Prop $State.prs "$Number" }
    if ($entry) {
        $entry.PSObject.Properties.Remove('lockAcquiredAt')
        $entry.PSObject.Properties.Remove('lockOwner')
    }
    Save-State $State
}

# ---------------------------------------------------------------------------
# Actions
# ---------------------------------------------------------------------------
function Invoke-Implement {
    param($State, $Issue)
    $num = $Issue.number
    Write-Host "==> implement issue #$num : $($Issue.title)"
    Add-Label issue $num 'ai:running'
    Set-LockTimestamp $State 'issue' $num
    $raw = Invoke-Agent -Agent 'mes-implementer' -Prompt (
        "Implement GitHub issue #$num. Read it with 'gh issue view $num --comments'. " +
        "Follow AGENT.md and the area instructions. Write unit tests AND endpoint integration tests, but only run " +
        "'dotnet build AsistOff.MES.sln', 'dotnet test tests/AsistOff.MES.Shared.Tests' and " +
        "'cd AsistOff.MES.Web; npm run build' (CI runs the integration suite; do not start Docker, the app, or a database). " +
        "Then open a PR that closes #$num. " +
        "Use branch name ai/issue-$num-<slug>."
    )
    $session = Get-SessionId $raw
    Remove-Label issue $num 'ai:running'
    Clear-LockTimestamp $State 'issue' $num

    $pr = @(GhJson @('pr', 'list', '--state', 'open', '--limit', '100', '--json', 'number,headRefName,isDraft') |
        Where-Object { $_.headRefName -like "ai/issue-$num-*" } | Select-Object -First 1)
    if (-not $pr) {
        Write-Host "    no PR produced -> ai:blocked"
        Remove-Label issue $num 'ai:implement'
        Add-Label issue $num 'ai:blocked'
        Add-Comment issue $num 'Agent flow: implementer did not open a PR. Needs human attention.'
        return
    }
    if ($pr.isDraft) { Invoke-Gh @('pr', 'ready', "$($pr.number)") | Out-Null }
    Remove-Label issue $num 'ai:implement'
    Add-Label pr $pr.number 'ai:review'
    Set-IssueState $State $num -Session $session -Pr $pr.number
    Reset-Rounds $State $pr.number
    Write-Host "    PR #$($pr.number) -> ai:review"
}

function Invoke-Fix {
    param($State, $Pr, [string]$Reason)
    $prNum = $Pr.number
    $issueNum = Get-IssueFromBranch $Pr.headRefName
    $session = if ($issueNum) { Get-SessionForIssue $State $issueNum } else { $null }
    $rounds = (Get-Rounds $State $prNum) + 1

    Write-Host "==> fix PR #$prNum (round $rounds/$MaxRounds) : $Reason"
    if ($rounds -gt $MaxRounds) {
        Write-Host "    round limit reached -> ai:blocked"
        Remove-Label pr $prNum 'ai:changes'
        Remove-Label pr $prNum 'ai:review'
        Add-Label pr $prNum 'ai:blocked'
        Add-Comment pr $prNum "Agent flow: exceeded $MaxRounds review/fix rounds. Needs human attention."
        if ($issueNum) { Add-Label issue $issueNum 'ai:blocked' }
        return
    }

    Add-Label pr $prNum 'ai:running'
    Set-LockTimestamp $State 'pr' $prNum
    $prompt = "Review/e2e feedback on PR #$prNum requested changes. " +
        "Read it with 'gh pr view $prNum --comments'. Fix every point, re-run " +
        "'dotnet build AsistOff.MES.sln', 'dotnet test tests/AsistOff.MES.Shared.Tests' and " +
        "'cd AsistOff.MES.Web; npm run build' (not the integration suite), then push to the same branch and reply."
    if ($Reason -eq 'ci') { $prompt = "CI on PR #$prNum is red. Inspect 'gh pr checks $prNum' and the logs, fix the failure, re-run build/test locally, then push to the same branch." }
    Invoke-Agent -Agent 'mes-implementer' -Session $session -Prompt $prompt | Out-Null
    Remove-Label pr $prNum 'ai:running'
    Clear-LockTimestamp $State 'pr' $prNum
    Remove-Label pr $prNum 'ai:changes'
    Add-Label pr $prNum 'ai:review'
    Set-Rounds $State $prNum $rounds
    Write-Host "    -> ai:review"
}

function Invoke-Review {
    param($State, $Pr)
    $prNum = $Pr.number
    Write-Host "==> review PR #$prNum : $($Pr.title)"
    Add-Label pr $prNum 'ai:running'
    Set-LockTimestamp $State 'pr' $prNum
    $raw = Invoke-Agent -Agent 'mes-reviewer' -Prompt (
        "Review PR #$prNum for AsistOff MES. Inspect only the diff with 'gh pr diff $prNum'. " +
        "Post your findings with 'gh pr comment $prNum' and end the body with exactly one " +
        "verdict on its own line: 'VERDICT: APPROVED' or 'VERDICT: CHANGES_REQUESTED'. " +
        "Do not write any other VERDICT line (do not quote the alternative)."
    )
    Remove-Label pr $prNum 'ai:running'
    Clear-LockTimestamp $State 'pr' $prNum
    Remove-Label pr $prNum 'ai:review'

    $verdict = Get-Verdict $raw 'VERDICT:\s*(APPROVED|CHANGES_REQUESTED)'
    if (($verdict -eq 'UNKNOWN') -or ($verdict -eq 'AMBIGUOUS')) {
        $comments = GhJson @('pr', 'view', "$prNum", '--json', 'comments')
        $body = (@($comments.comments) | ForEach-Object { $_.body }) -join "`n"
        $verdict = Get-Verdict $body 'VERDICT:\s*(APPROVED|CHANGES_REQUESTED)'
    }

    switch ($verdict) {
        'APPROVED' { Add-Label pr $prNum 'ai:verify'; Write-Host "    -> ai:verify" }
        'CHANGES_REQUESTED' { Add-Label pr $prNum 'ai:changes'; Write-Host "    -> ai:changes" }
        default {
            Add-Label pr $prNum 'ai:blocked'
            Add-Comment pr $prNum 'Agent flow: reviewer produced no unambiguous verdict (none or multiple). Needs human attention.'
            Write-Host "    -> ai:blocked (review verdict $verdict)"
        }
    }
}

function Invoke-Verify {
    param($State, $Pr)
    $prNum = $Pr.number
    $issueNum = Get-IssueFromBranch $Pr.headRefName
    Write-Host "==> verify PR #$prNum : $($Pr.title)"
    Add-Label pr $prNum 'ai:running'
    Set-LockTimestamp $State 'pr' $prNum
    $issuePart = if ($issueNum) { " linked issue #$issueNum (read AC with 'gh issue view $issueNum --comments')" } else { "" }
    $raw = Invoke-Agent -Agent 'mes-verifier' -Prompt (
        "Verify that the tests in PR #$prNum genuinely prove the acceptance criteria$issuePart. " +
        "Read-only audit: inspect the diff with 'gh pr diff $prNum', map each criterion to the test(s) " +
        "that prove it, and check for weakened tests. DO NOT write files, commit, or push. " +
        "Post the report with 'gh pr comment $prNum' ending with exactly one verdict on its own line: " +
        "'VERDICT: TESTS_SOUND' or 'VERDICT: TESTS_INSUFFICIENT'. Do not write any other VERDICT line."
    )
    Remove-Label pr $prNum 'ai:running'
    Clear-LockTimestamp $State 'pr' $prNum
    Remove-Label pr $prNum 'ai:verify'

    $verdict = Get-Verdict $raw 'VERDICT:\s*(TESTS_SOUND|TESTS_INSUFFICIENT)'
    if (($verdict -eq 'UNKNOWN') -or ($verdict -eq 'AMBIGUOUS')) {
        $comments = GhJson @('pr', 'view', "$prNum", '--json', 'comments')
        $body = (@($comments.comments) | ForEach-Object { $_.body }) -join "`n"
        $verdict = Get-Verdict $body 'VERDICT:\s*(TESTS_SOUND|TESTS_INSUFFICIENT)'
    }

    switch ($verdict) {
        'TESTS_SOUND' { Add-Label pr $prNum 'ai:e2e'; Write-Host "    -> ai:e2e" }
        'TESTS_INSUFFICIENT' {
            Add-Label pr $prNum 'ai:changes'
            Add-Comment pr $prNum 'Agent flow: verifier found untested criteria or weakened tests (see verification report). Implementer must add real tests.'
            Write-Host "    -> ai:changes (tests insufficient)"
        }
        default {
            Add-Label pr $prNum 'ai:blocked'
            Add-Comment pr $prNum 'Agent flow: verifier produced no unambiguous verdict. Needs human attention.'
            Write-Host "    -> ai:blocked (verify verdict $verdict)"
        }
    }
}

function Invoke-E2e {
    param($State, $Pr)
    $prNum = $Pr.number
    Write-Host "==> e2e PR #$prNum : $($Pr.title)"
    Add-Label pr $prNum 'ai:running'
    Set-LockTimestamp $State 'pr' $prNum
    $raw = Invoke-Agent -Agent 'mes-e2e-tester' -Prompt (
        "Run the Playwright end-to-end smoke test for PR #$prNum. Resolve the scope from " +
        "'gh pr diff $prNum --name-only' using the mes-e2e skill, ensure the stack with " +
        "'pwsh -File scripts/e2e/app.ps1 -Action start', then post the result with " +
        "'gh pr comment $prNum' ending with exactly one line: " +
        "'VERDICT: E2E_PASS', 'VERDICT: E2E_FAIL' or 'VERDICT: E2E_BLOCKED'."
    )
    Remove-Label pr $prNum 'ai:running'
    Clear-LockTimestamp $State 'pr' $prNum
    Remove-Label pr $prNum 'ai:e2e'

    $verdict = Get-Verdict $raw 'VERDICT:\s*(E2E_PASS|E2E_FAIL|E2E_BLOCKED)'
    if (($verdict -eq 'UNKNOWN') -or ($verdict -eq 'AMBIGUOUS')) {
        $comments = GhJson @('pr', 'view', "$prNum", '--json', 'comments')
        $body = (@($comments.comments) | ForEach-Object { $_.body }) -join "`n"
        $verdict = Get-Verdict $body 'VERDICT:\s*(E2E_PASS|E2E_FAIL|E2E_BLOCKED)'
    }

    switch ($verdict) {
        'E2E_PASS' {
            Add-Label pr $prNum 'ai:ready'
            Add-Comment pr $prNum 'Agent flow: CI + review + verify + e2e green. Ready for a human merge.'
            Write-Host "    -> ai:ready"
        }
        'E2E_FAIL' { Add-Label pr $prNum 'ai:changes'; Write-Host "    -> ai:changes" }
        default {
            Add-Label pr $prNum 'ai:blocked'
            Add-Comment pr $prNum 'Agent flow: e2e could not produce a verdict (stack down?). Needs human attention.'
            Write-Host "    -> ai:blocked (e2e inconclusive)"
        }
    }
}

# ---------------------------------------------------------------------------
# Feature tracker (autonomous)
# ---------------------------------------------------------------------------
function Get-WorkSignature {
    param($OpenIssues, $OpenPrs)
    # NOTE: ai:running is a transient lock churned every cycle — exclude it so
    # the tracker does not sync on every lock/unlock.
    $parts = @()
    foreach ($i in @($OpenIssues)) {
        $labels = @(Get-LabelNames $i | Where-Object { $_ -ne 'ai:running' } | Sort-Object) -join '+'
        $parts += "i$($i.number):$labels"
    }
    foreach ($p in @($OpenPrs)) {
        if ($p.headRefName -eq 'ai/tracker-sync') { continue }
        $labels = @(Get-LabelNames $p | Where-Object { $_ -ne 'ai:running' } | Sort-Object) -join '+'
        $parts += "p$($p.number):$labels"
    }
    return ($parts -join '|')
}

function Get-RepoDirty {
    $out = (& git status --porcelain 2>&1 | Out-String).Trim()
    return (-not [string]::IsNullOrWhiteSpace($out))
}

function Get-LockOwner {
    return "$env:COMPUTERNAME/$env:USERNAME pid=$PID"
}

function Test-StaleLock {
    param($State, [string]$Kind, $Number, $Item)
    # Returns $true only when the ai:running lock is safe to clear:
    # older than RunningTtlMinutes by BOTH local state and GitHub updatedAt.
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $ttlSec = [math]::Max(1, $RunningTtlMinutes) * 60
    $entry = if ($Kind -eq 'issue') { Get-Prop $State.issues "$Number" } else { Get-Prop $State.prs "$Number" }
    $localTs = [int64](Get-Prop $entry 'lockAcquiredAt')
    $localAgeOk = ($localTs -gt 0) -and (($now - $localTs) -gt $ttlSec)
    if ($localTs -gt 0 -and -not $localAgeOk) { return $false }
    # No local timestamp (e.g. TEMP wiped) — fall back to GitHub updatedAt.
    try {
        $updated = [string]$Item.updatedAt
        if ($updated) {
            $updSec = [int64]([DateTimeOffset]::Parse($updated).ToUnixTimeSeconds())
            return (($now - $updSec) -gt $ttlSec)
        }
    } catch { }
    # No evidence at all: only stale if we had a local timestamp that expired.
    return $localAgeOk
}

function Invoke-TrackerIfDue {
    param($State, $OpenIssues, $OpenPrs)
    if ($NoTracker) { return }
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $last = [int64](Get-Prop $State.tracker 'lastSync')
    $lastSig = [string](Get-Prop $State.tracker 'lastSignature')
    $sig = Get-WorkSignature $OpenIssues $OpenPrs
    $sinceMin = ($now - $last) / 60
    $cooldown = [math]::Max(1, $TrackerCooldownMinutes)
    $interval = [math]::Max($cooldown, $TrackerIntervalMinutes)

    $changed = ($sig -ne $lastSig)
    $due = (($changed) -and ($sinceMin -ge $cooldown)) -or ($sinceMin -ge $interval)
    if (-not $due) { return }

    if (Get-RepoDirty) {
        Write-Host "==> tracker skipped: working tree dirty (commit/stash first). Will retry next cycle."
        return
    }

    Write-Host "==> feature tracker sync (changed=$changed, since=${sinceMin}m)"
    Invoke-Agent -Agent 'mes-tracker' -Prompt (
        "Reconcile docs/feature-tracker.md with GitHub issues/PRs and the codebase, " +
        "then publish the update on branch ai/tracker-sync (based on the repo default branch, " +
        "see 'gh repo view --json defaultBranchRef') and open or update a PR. " +
        "Abort if the working tree is dirty."
    ) | Out-Null

    $State.tracker | Add-Member -NotePropertyName lastSync -NotePropertyValue $now -Force
    $State.tracker | Add-Member -NotePropertyName lastSignature -NotePropertyValue $sig -Force
    Save-State $State
}

# ---------------------------------------------------------------------------
# Cycle
# ---------------------------------------------------------------------------
function Invoke-Cycle {
    param($State)

    $openIssues = @(GhJson @('issue', 'list', '--state', 'open', '--limit', '100', '--json', 'number,title,url,labels,updatedAt'))
    $openPrs = @(GhJson @('pr', 'list', '--state', 'open', '--limit', '100', '--json', 'number,title,url,labels,headRefName,isDraft,body,updatedAt'))

    $implementable = @($openIssues | Where-Object { (Has-Label $_ 'ai:implement') -and -not (Has-Label $_ 'ai:running') -and -not (Has-Label $_ 'ai:blocked') })
    $changes = @($openPrs | Where-Object { (Has-Label $_ 'ai:changes') -and -not (Has-Label $_ 'ai:running') })
    $reviews = @($openPrs | Where-Object { (Has-Label $_ 'ai:review') -and -not (Has-Label $_ 'ai:running') -and -not $_.isDraft })
    $verifies = @($openPrs | Where-Object { (Has-Label $_ 'ai:verify') -and -not (Has-Label $_ 'ai:running') -and -not $_.isDraft })
    $e2es = @($openPrs | Where-Object { (Has-Label $_ 'ai:e2e') -and -not (Has-Label $_ 'ai:running') -and -not $_.isDraft })

    $acted = $false

    if (@($changes).Count -gt 0) {
        Invoke-Fix $State $changes[0] 'review'
        $acted = $true
    } elseif (@($reviews).Count -gt 0) {
        $pr = $reviews[0]
        $ci = Get-CiState $pr.number
        Write-Host "    PR #$($pr.number) ci=$ci"
        if ($ci -eq 'pending') {
            Write-Host "==> waiting for CI on PR #$($pr.number)"
        } elseif ($ci -eq 'fail') {
            Invoke-Fix $State $pr 'ci'
            $acted = $true
        } else {
            Invoke-Review $State $pr
            $acted = $true
        }
    } elseif (@($verifies).Count -gt 0) {
        Invoke-Verify $State $verifies[0]
        $acted = $true
    } elseif (@($e2es).Count -gt 0) {
        Invoke-E2e $State $e2es[0]
        $acted = $true
    } elseif (@($implementable).Count -gt 0) {
        Invoke-Implement $State $implementable[0]
        $acted = $true
    }

    Invoke-TrackerIfDue $State $openIssues $openPrs
    return $acted
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
Write-Host "MES agent dispatcher starting (interval ${IntervalSeconds}s, maxRounds $MaxRounds, dryRun=$DryRun)"

$locked = $false
if (-not $DryRun) {
    $locked = Acquire-Lock
    if (-not $locked) { return }
}

try {
    $state = Get-State

    # Clear only EXPIRED ai:running locks (TTL). Fresh locks belong to a live
    # agent — possibly on another host — and must not be stolen on restart.
    $staleIssues = @(GhJson @('issue', 'list', '--label', 'ai:running', '--state', 'open', '--limit', '100', '--json', 'number,updatedAt'))
    $cleared = 0; $kept = 0
    foreach ($i in $staleIssues) {
        if (Test-StaleLock $state 'issue' $i.number $i) {
            Remove-Label issue $i.number 'ai:running'
            Clear-LockTimestamp $state 'issue' $i.number
            $cleared++
        } else { $kept++ }
    }
    $stalePrs = @(GhJson @('pr', 'list', '--label', 'ai:running', '--state', 'open', '--limit', '100', '--json', 'number,updatedAt'))
    foreach ($p in $stalePrs) {
        if (Test-StaleLock $state 'pr' $p.number $p) {
            Remove-Label pr $p.number 'ai:running'
            Clear-LockTimestamp $state 'pr' $p.number
            $cleared++
        } else { $kept++ }
    }
    if ($cleared + $kept -gt 0) {
        Write-Host "ai:running locks: cleared $cleared expired (TTL ${RunningTtlMinutes}m), kept $kept fresh."
    }

    if ($Once) {
        Invoke-Cycle $state | Out-Null
        Save-State $state
        Write-Host 'Single cycle done.'
        return
    }

    while ($true) {
        try {
            $acted = Invoke-Cycle $state
            Save-State $state
            if (-not $acted) { Start-Sleep -Seconds $IntervalSeconds }
        } catch {
            Write-Host "!! cycle error: $_"
            Start-Sleep -Seconds $IntervalSeconds
        }
    }
} finally {
    if ($locked) { Release-Lock }
}
