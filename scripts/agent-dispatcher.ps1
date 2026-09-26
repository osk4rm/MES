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
    Max review->fix / e2e->fix rounds per PR before ai:blocked. 0 = unlimited
    (default): agents keep fixing until the PR is green.

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

.PARAMETER NoSelfUpdate
    Do not fast-forward the working copy onto origin. By default the dispatcher
    updates itself between cycles and exits so the new code takes effect (the
    container entrypoint restarts it). Skipped for a dirty working copy.

.NOTES
    Requires: git, gh (authenticated), opencode, and (for e2e) Playwright MCP.
    Run from the repository root. State is kept in %TEMP%\opencode\dispatcher-state.json.
#>
[CmdletBinding()]
param(
    [int]$IntervalSeconds = 20,
    [int]$MaxRounds = 0,
    [switch]$Once,
    [switch]$DryRun,
    [switch]$Auto,
    [switch]$NoTracker,
    [int]$TrackerIntervalMinutes = 30,
    [int]$TrackerCooldownMinutes = 10,
    [int]$RunningTtlMinutes = 30,
    [switch]$NoSelfUpdate
)

$ErrorActionPreference = 'Continue'

$Tmp = if ($env:TEMP) { $env:TEMP } elseif ($env:TMPDIR) { $env:TMPDIR } else { '/tmp' }
$LogDir = Join-Path $Tmp 'opencode'
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$StateFile = Join-Path $LogDir 'dispatcher-state.json'
$LockFile = Join-Path $LogDir 'dispatcher.lock'

# The isolated clone in the swarm container is a plain git checkout of the repo,
# so the top level is also where the dispatcher must update itself from.
$RepoRoot = (& git rev-parse --show-toplevel 2>$null | Out-String).Trim()
if (-not $RepoRoot) { $RepoRoot = (Get-Location).Path }

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

function Get-LastVerdictInComments {
    # Fallback path ONLY: the newest verdict wins, mirroring swarm_last_verdict
    # in scripts/ci/swarm-lib.sh. Get-Verdict would report AMBIGUOUS here, since
    # a long PR legitimately carries one APPROVED and one CHANGES_REQUESTED
    # comment from different rounds - that must not escalate a live PR.
    param([int]$PrNumber, [string]$Pattern)
    $comments = GhJson @('pr', 'view', "$PrNumber", '--json', 'comments')
    $last = $null
    foreach ($c in @($comments.comments)) {
        $m = [regex]::Matches([string]$c.body, $Pattern)
        if ($m.Count -gt 0) { $last = $m[$m.Count - 1].Groups[1].Value }
    }
    if (-not $last) { return 'UNKNOWN' }
    return $last
}

# --- gate verdicts, keyed to the head SHA ---------------------------------
# Same contract as the CI workflow (swarm_mark_verdict / swarm_verdict_covers_head):
# an HTML comment records the SHA a gate judged, so a re-fired label can skip the
# agent instead of re-judging an identical diff. Freshness is never inferred from
# timestamps - rebase and clock skew move commit dates.
function Get-HeadSha {
    param([int]$PrNumber)
    $sha = Invoke-Gh @('pr', 'view', "$PrNumber", '--json', 'headRefOid', '--jq', '.headRefOid')
    if (-not $sha) { return $null }
    return "$sha".Trim()
}

function Get-PrBodyHash {
    param([int]$PrNumber)
    $pr = GhJson @('pr', 'view', "$PrNumber", '--json', 'body')
    if (-not $pr) { return $null }
    $body = if ($null -eq $pr.body) { '' } else { [string]$pr.body }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($body))
        return [System.BitConverter]::ToString($bytes).Replace('-', '')
    }
    finally { $sha.Dispose() }
}

function Get-RecordedVerdict {
    param([int]$PrNumber, [string]$Gate, [string]$Sha)
    if (-not $Sha) { return $null }
    $comments = GhJson @('pr', 'view', "$PrNumber", '--json', 'comments')
    $pattern = "swarm-verdict gate=$Gate sha=$Sha verdict=([A-Z0-9_]+)"
    $last = $null
    foreach ($c in @($comments.comments)) {
        $m = [regex]::Matches([string]$c.body, $pattern)
        if ($m.Count -gt 0) { $last = $m[$m.Count - 1].Groups[1].Value }
    }
    return $last
}

function Test-VerdictCoversHead {
    param([int]$PrNumber, [string]$Gate, [string]$Verdict)
    $sha = Get-HeadSha $PrNumber
    if (-not $sha) { return $false }
    return (Get-RecordedVerdict $PrNumber $Gate $sha) -eq $Verdict
}

function Add-VerdictMarker {
    param([int]$PrNumber, [string]$Gate, [string]$Sha, [string]$Verdict)
    # Same guards as swarm_mark_verdict: a malformed marker would never match
    # swarm_head_verdict and would silently stop suppressing duplicate passes.
    # -cnotmatch: PowerShell matching is case-insensitive by default, and the
    # marker format is case-sensitive (lowercase gate/sha, UPPER verdict).
    # Digits are legal in gate names (e2e): a [a-z] guard silently voided every
    # e2e marker, so the e2e stage re-ran its agent on every re-fire.
    if ($Gate -cnotmatch '^[a-z0-9]+$') { return }
    if ($Sha -cnotmatch '^[0-9a-f]+$') { return }
    if ($Verdict -cnotmatch '^[A-Z0-9_]+$') { return }
    # Refuses to mark a SHA that is no longer the head: the pass judged a
    # different tree than the marker would vouch for.
    $head = Get-HeadSha $PrNumber
    if ($head -ne $Sha) {
        Write-Host "      head moved ($Sha -> $(if ($head) { $head } else { 'unknown' })); not marking $Gate=$Verdict"
        return
    }
    if ($DryRun) { Write-Host "      [dry] mark $Gate=$Verdict for $Sha"; return }
    Add-Comment pr $PrNumber "<!-- swarm-verdict gate=$Gate sha=$Sha verdict=$Verdict -->"
}

function Test-HeadMoved {
    param([int]$PrNumber, [string]$Sha)
    # A pass that started before the last push judges a tree nobody ships; its
    # verdict must not advance the pipeline for the head that replaced it.
    if (-not $Sha) { return $false }
    $head = Get-HeadSha $PrNumber
    return [bool]($head -and ($head -ne $Sha))
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

    Write-Host "==> fix PR #$prNum (round $rounds$(if ($MaxRounds -gt 0) { "/$MaxRounds" } else { " (unlimited)" })) : $Reason"
    if ($MaxRounds -gt 0 -and $rounds -gt $MaxRounds) {
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
    # No-progress fingerprint (code + PR description), same contract as
    # swarm_fix_made_no_progress: an unchanged round can only reproduce the
    # verdict we just got, because the gates are idempotent per head SHA.
    $headBefore = Get-HeadSha $prNum
    $bodyBefore = Get-PrBodyHash $prNum
    Invoke-Agent -Agent 'mes-implementer' -Session $session -Prompt $prompt | Out-Null
    Remove-Label pr $prNum 'ai:running'
    Clear-LockTimestamp $State 'pr' $prNum
    if ($headBefore -and (Get-HeadSha $prNum) -eq $headBefore -and (Get-PrBodyHash $prNum) -eq $bodyBefore) {
        Write-Host "    round changed neither code nor PR body -> ai:blocked"
        Remove-Label pr $prNum 'ai:changes'
        Add-Label pr $prNum 'ai:blocked'
        Add-Comment pr $prNum 'Agent flow: the fix round changed neither the code nor the PR description, so another review/verify round would judge the identical diff. Needs human attention.'
        return
    }
    Remove-Label pr $prNum 'ai:changes'
    # A round that changed something also clears a stale ai:blocked - the block
    # exists to stop a no-progress loop, and the dispatchers skip blocked PRs,
    # so a PR that recovered on a later round would otherwise stay frozen.
    if (Has-Label $Pr 'ai:blocked') {
        Remove-Label pr $prNum 'ai:blocked'
        Add-Comment pr $prNum 'Agent flow: this round changed code again, so the earlier ai:blocked is cleared and review starts over.'
    }
    Add-Label pr $prNum 'ai:review'
    Set-Rounds $State $prNum $rounds
    Write-Host "    -> ai:review"
}

function Invoke-Review {
    param($State, $Pr)
    $prNum = $Pr.number
    if (Test-VerdictCoversHead $prNum 'review' 'APPROVED') {
        Write-Host "==> review PR #$prNum : APPROVED already covers this head SHA; skipping"
        Remove-Label pr $prNum 'ai:review'
        # Mirrors swarm_after_review_approved: advance only when verify is settled.
        if ((Has-Label $Pr 'ai:changes') -or (Has-Label $Pr 'ai:blocked')) {
            Write-Host "    changes/blocked already set; not advancing"
        }
        elseif (Test-VerdictCoversHead $prNum 'verify' 'TESTS_SOUND') {
            Add-Label pr $prNum 'ai:e2e'
            Write-Host "    -> ai:e2e (verify already settled)"
        }
        elseif (-not (Has-Label $Pr 'ai:verify')) { Add-Label pr $prNum 'ai:verify' }
        return
    }
    $sha = Get-HeadSha $prNum
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
        $verdict = Get-LastVerdictInComments $prNum 'VERDICT:\s*(APPROVED|CHANGES_REQUESTED)'
    }
    if ($verdict -in @('APPROVED', 'CHANGES_REQUESTED')) { Add-VerdictMarker $prNum 'review' $sha $verdict }
    if (Test-HeadMoved $prNum $sha) {
        Add-Label pr $prNum 'ai:review'
        Add-Comment pr $prNum "Agent flow: head moved $sha -> $(Get-HeadSha $prNum) during the review pass. Verdict pinned to the reviewed SHA; review re-queued for the new head."
        Write-Host "    head moved during the pass; re-queued for the new head"
        return
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
    if (Test-VerdictCoversHead $prNum 'verify' 'TESTS_SOUND') {
        Write-Host "==> verify PR #$prNum : TESTS_SOUND already covers this head SHA; skipping"
        Remove-Label pr $prNum 'ai:verify'
        # Mirrors swarm_after_verify_sound: wait while the review gate is open.
        if ((Has-Label $Pr 'ai:changes') -or (Has-Label $Pr 'ai:blocked')) {
            Write-Host "    changes/blocked already set; not advancing"
        }
        elseif (-not (Has-Label $Pr 'ai:review')) {
            Add-Label pr $prNum 'ai:e2e'
            Write-Host "    -> ai:e2e (review already settled)"
        }
        return
    }
    $issueNum = Get-IssueFromBranch $Pr.headRefName
    $sha = Get-HeadSha $prNum
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
        $verdict = Get-LastVerdictInComments $prNum 'VERDICT:\s*(TESTS_SOUND|TESTS_INSUFFICIENT)'
    }
    if ($verdict -in @('TESTS_SOUND', 'TESTS_INSUFFICIENT')) { Add-VerdictMarker $prNum 'verify' $sha $verdict }
    if (Test-HeadMoved $prNum $sha) {
        Add-Label pr $prNum 'ai:verify'
        Add-Comment pr $prNum "Agent flow: head moved $sha -> $(Get-HeadSha $prNum) during the verification pass. Verdict pinned to the audited SHA; verify re-queued for the new head."
        Write-Host "    head moved during the pass; re-queued for the new head"
        return
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
    if (Test-VerdictCoversHead $prNum 'e2e' 'E2E_PASS') {
        Write-Host "==> e2e PR #$prNum : E2E_PASS already covers this head SHA; skipping"
        Remove-Label pr $prNum 'ai:e2e'
        Add-Label pr $prNum 'ai:ready'
        return
    }
    $sha = Get-HeadSha $prNum
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
        $verdict = Get-LastVerdictInComments $prNum 'VERDICT:\s*(E2E_PASS|E2E_FAIL|E2E_BLOCKED)'
    }
    if ($verdict -in @('E2E_PASS', 'E2E_FAIL', 'E2E_BLOCKED')) { Add-VerdictMarker $prNum 'e2e' $sha $verdict }
    if (Test-HeadMoved $prNum $sha) {
        Add-Label pr $prNum 'ai:e2e'
        Add-Comment pr $prNum "Agent flow: head moved $sha -> $(Get-HeadSha $prNum) during the e2e pass. Verdict pinned to the tested SHA; e2e re-queued for the new head."
        Write-Host "    head moved during the pass; re-queued for the new head"
        return
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

function Update-WorkingCopy {
    # PowerShell parses this whole file once at start-up, so a dispatcher that
    # keeps looping never picks up the fixes that CI is pushing to master: it
    # keeps enforcing rules from whenever it was started. That is how a stale
    # clone ended up blocking a PR whose gates had passed (#264, ai:blocked from
    # a dispatcher three commits behind). Fast-forward and let the caller exit
    # so the supervisor restarts us on the new code.
    # Returns $true when the working copy moved and the process should stop.
    if ($DryRun -or $NoSelfUpdate) { return $false }
    if (-not (Test-Path (Join-Path $RepoRoot '.git'))) { return $false }
    if (Get-RepoDirty) {
        Write-Host 'working copy has local changes; not self-updating (restart the dispatcher after committing)'
        return $false
    }
    & git fetch --quiet origin 2>&1 | Out-Null
    $remote = (& git rev-parse --verify --quiet 'origin/HEAD' 2>&1 | Out-String).Trim()
    if (-not $remote) { $remote = (& git rev-parse --verify --quiet 'origin/master' 2>&1 | Out-String).Trim() }
    if (-not $remote) { return $false }
    $head = (& git rev-parse --verify --quiet HEAD 2>&1 | Out-String).Trim()
    if (-not $head) { return $false }
    if ($head -eq $remote) { return $false }
    & git merge --ff-only --quiet $remote 2>&1 | Out-Null
    $after = (& git rev-parse --verify --quiet HEAD 2>&1 | Out-String).Trim()
    if ($after -eq $remote) {
        Write-Host "working copy updated $($head.Substring(0,7)) -> $($remote.Substring(0,7)); exiting so the new code runs"
        return $true
    }
    Write-Host "cannot fast-forward onto $remote (local branch diverged); continuing on $head"
    return $false
}

$script:BranchBusyCache = @{}

function Test-BranchBusy {
    # A PR can be driven by GitHub Actions and by this dispatcher at the same
    # time. Two drivers on one PR means two agents racing to write the same
    # verdict, which is how a review got re-fired under a running reviewer and
    # how a stale dispatcher stamped ai:blocked over green gates. ai:running
    # only covers the stages that take that lock, so ask the runs themselves.
    param($Pr)
    if (-not $Pr.headRefName) { return $false }
    $branch = [string]$Pr.headRefName
    if ($script:BranchBusyCache.ContainsKey($branch)) { return $script:BranchBusyCache[$branch] }
    $runs = @(GhJson @('run', 'list', '--branch', $branch, '--workflow', 'ai-swarm', '--limit', '5', '--json', 'status,conclusion'))
    $busy = @($runs | Where-Object { $_.status -ne 'completed' }).Count -gt 0
    $script:BranchBusyCache[$branch] = $busy
    if ($busy) { Write-Host "    #$($Pr.number): ai-swarm run in flight on $branch; leaving it to that runner" }
    return $busy
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

    $script:BranchBusyCache = @{}
    $openIssues = @(GhJson @('issue', 'list', '--state', 'open', '--limit', '100', '--json', 'number,title,url,labels,updatedAt'))
    $openPrs = @(GhJson @('pr', 'list', '--state', 'open', '--limit', '100', '--json', 'number,title,url,labels,headRefName,isDraft,body,updatedAt'))

    $implementable = @($openIssues | Where-Object { (Has-Label $_ 'ai:implement') -and -not (Has-Label $_ 'ai:running') -and -not (Has-Label $_ 'ai:blocked') })
    # ai:blocked is a human-attention state (no-progress fix round, CI waiting for
    # approval, unparsable verdict) and its documented recovery is manual, so no
    # stage may pick the PR up again - otherwise a blocked PR re-enters the loop
    # through the next queued job.
    $changes = @($openPrs | Where-Object { (Has-Label $_ 'ai:changes') -and -not (Has-Label $_ 'ai:running') -and -not (Has-Label $_ 'ai:blocked') -and -not (Test-BranchBusy $_) })
    $reviews = @($openPrs | Where-Object { (Has-Label $_ 'ai:review') -and -not (Has-Label $_ 'ai:running') -and -not (Has-Label $_ 'ai:blocked') -and -not $_.isDraft -and -not (Test-BranchBusy $_) })
    $verifies = @($openPrs | Where-Object { (Has-Label $_ 'ai:verify') -and -not (Has-Label $_ 'ai:running') -and -not (Has-Label $_ 'ai:blocked') -and -not $_.isDraft -and -not (Test-BranchBusy $_) })
    $e2es = @($openPrs | Where-Object { (Has-Label $_ 'ai:e2e') -and -not (Has-Label $_ 'ai:running') -and -not (Has-Label $_ 'ai:blocked') -and -not $_.isDraft -and -not (Test-BranchBusy $_) })

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
        # Never keep enforcing rules that master has already replaced. The
        # entrypoint supervises this process, so exiting here is how new code
        # gets loaded - and how a crash gets recovered from.
        if (Update-WorkingCopy) { return }
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
