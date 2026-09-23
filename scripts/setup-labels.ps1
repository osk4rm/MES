<#
.SYNOPSIS
    Creates/updates the GitHub labels used by the MES agent state machine.

.DESCRIPTION
    Idempotent: uses `gh label create --force`, so re-running updates colour and
    description. Run once per repository before starting the dispatcher.

.NOTES
    Requires: gh (authenticated) and repository access. Run from the repo root.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

# name; colour (no '#'); description
$labels = @(
    @('ai:implement',  '0E8A16', 'Ready for the implementation agent'),
    @('ai:running',    'FBCA04', 'An agent is currently processing this item (lock)'),
    @('ai:review',     '1D76DB', 'PR ready for the review agent (CI green)'),
    @('ai:verify',     '0E8A16', 'PR passed review; ready for test-verification agent'),
    @('ai:changes',    'D93F0B', 'Reviewer requested changes; implementer must fix'),
    @('ai:e2e',        '5319E7', 'PR ready for the Playwright e2e smoke agent'),
    @('ai:ready',      '006B75', 'CI + review + e2e green; ready for human merge'),
    @('ai:blocked',    'B60205', 'Agent flow escalated; needs human attention'),
    @('ai:auto-merge', 'C2E0C6', 'Opt-in: allow auto-merge when fully green (future)')
)

foreach ($l in $labels) {
    $name = $l[0]; $colour = $l[1]; $desc = $l[2]
    gh label create $name --color $colour --description $desc --force | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ok  $name"
    } else {
        Write-Host "  !!  $name (exit $LASTEXITCODE)"
    }
}

Write-Host ''
Write-Host 'Labels ready. Start the dispatcher with:'
Write-Host '  pwsh -File scripts/agent-dispatcher.ps1'
