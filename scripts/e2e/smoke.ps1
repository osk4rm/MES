<#
.SYNOPSIS
    Single-command runner for the committed login-to-lots Playwright smoke suite (issue #272).

.DESCRIPTION
    1. Starts the local stack via scripts/e2e/app.ps1 (backend :5243 + frontend :5173).
    2. Ensures the Playwright chromium browser is installed.
    3. Runs the smoke suite in AsistOff.MES.Web/e2e/smoke (seeds isolated
       SMK-* data through the API, drives login -> dispatch -> confirmation
       -> lot tree in the browser, cleans the confirmation + lots up).
    4. Stops the stack again unless -KeepStack is given.

    Traces and screenshots for failing checks are kept under
    AsistOff.MES.Web/test-results (trace: retain-on-failure, screenshot:
    only-on-failure); the HTML report lands in
    AsistOff.MES.Web/playwright-report.

.PARAMETER WaitSeconds
    How long to wait for the stack after `start`. Default 90.

.PARAMETER KeepStack
    Leave backend + frontend running after the suite finishes.

.EXAMPLE
    pwsh -File scripts/e2e/smoke.ps1
    pwsh -File scripts/e2e/smoke.ps1 -KeepStack
#>
[CmdletBinding()]
param(
    [int]$WaitSeconds = 90,
    [switch]$KeepStack
)

$ErrorActionPreference = 'Stop'

$Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$WebDir = Join-Path $Root 'AsistOff.MES.Web'
$isWin = if ($PSVersionTable.PSVersion.Major -ge 6) { $IsWindows } else { $true }
$npx = if ($isWin) { 'npx.cmd' } else { 'npx' }

try {
    Write-Host '== smoke: starting stack =='
    & ([IO.Path]::Combine($PSScriptRoot, 'app.ps1')) -Action start -WaitSeconds $WaitSeconds
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'smoke: stack did not become healthy (see docs/e2e-local-setup.md).'
        exit 1
    }

    Write-Host '== smoke: ensuring Playwright chromium =='
    # Run from the web directory instead of `npx --prefix`, whose flag
    # handling varies across npx versions.
    Push-Location $WebDir
    try {
        & $npx playwright install chromium
        if ($LASTEXITCODE -ne 0) { exit 1 }

        Write-Host '== smoke: running login-to-lots suite =='
        $env:E2E_FRONTEND_URL = 'http://localhost:5173'
        $env:E2E_API_URL = 'http://localhost:5243'
        & $npx playwright test
        $code = $LASTEXITCODE
    } finally {
        Pop-Location
    }
} finally {
    if (-not $KeepStack) {
        Write-Host '== smoke: stopping stack =='
        & ([IO.Path]::Combine($PSScriptRoot, 'app.ps1')) -Action stop
    }
}

if ($code -ne 0) {
    Write-Host ''
    Write-Host 'smoke: FAILED — open AsistOff.MES.Web/playwright-report/index.html,'
    Write-Host '  traces + screenshots for the failing check are under AsistOff.MES.Web/test-results.'
    exit $code
}

Write-Host 'smoke: PASS (login -> dispatch -> confirmation -> lot tree)'
