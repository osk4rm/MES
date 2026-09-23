<#
.SYNOPSIS
    Starts / stops / checks the local app stack used by the mes-e2e-tester agent.

.DESCRIPTION
    Backend  : dotnet run --project AsistOff.MES.Gateway  (http profile, :5243)
    Frontend : npm run dev                                (:5173)

    Processes are started detached and their PIDs are recorded in a state file so
    that `-Action stop` and `-Action status` can find them. Logs go to
    <tmp>/opencode/e2e-*.log.

    Cross-platform: works on Windows (PowerShell 5.1 / pwsh) and on Linux
    (pwsh inside the swarm container).

.PARAMETER Action
    start | stop | status   (default: status)

.PARAMETER WaitSeconds
    How long to wait for both health endpoints after `start`. Default 90.

.NOTES
    The backend needs PostgreSQL (see AsistOff.MES.Gateway/appsettings.Development.json).
    If the database is not reachable the backend will exit and `start` reports the
    failure, which the e2e agent surfaces as VERDICT: E2E_BLOCKED.
#>
[CmdletBinding()]
param(
    [ValidateSet('start', 'stop', 'status')]
    [string]$Action = 'status',
    [int]$WaitSeconds = 90
)

$ErrorActionPreference = 'Continue'

$isWin = if ($PSVersionTable.PSVersion.Major -ge 6) { $IsWindows } else { $true }
$npm = if ($isWin) { 'npm.cmd' } else { 'npm' }

$Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$Tmp = if ($env:TEMP) { $env:TEMP } elseif ($env:TMPDIR) { $env:TMPDIR } else { '/tmp' }
$LogDir = Join-Path $Tmp 'opencode'
$StateFile = Join-Path $LogDir 'e2e-app-state.json'
$BackendPort = 5243
$FrontendPort = 5173

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Test-Port {
    param([int]$Port)
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $client.Connect('127.0.0.1', $Port)
        $client.Close()
        return $true
    } catch {
        return $false
    }
}

function Get-State {
    if (Test-Path $StateFile) {
        try { return Get-Content -Raw $StateFile | ConvertFrom-Json } catch { }
    }
    return [pscustomobject]@{ backendPid = $null; frontendPid = $null }
}

function Save-State {
    param($State)
    $State | ConvertTo-Json | Set-Content -Path $StateFile -Encoding utf8
}

function Start-Detached {
    param(
        [string]$File,
        [string[]]$ArgList,
        [string]$WorkDir,
        [string]$OutFile,
        [string]$ErrFile
    )
    $p = @{
        FilePath               = $File
        ArgumentList           = $ArgList
        WorkingDirectory       = $WorkDir
        PassThru               = $true
        RedirectStandardOutput = $OutFile
        RedirectStandardError  = $ErrFile
    }
    if ($isWin) { $p['WindowStyle'] = 'Hidden' }
    return Start-Process @p
}

function Stop-Pid {
    param($PidValue)
    if (-not $PidValue) { return }
    if ($isWin) {
        try { taskkill /PID $PidValue /T /F 2>&1 | Out-Null } catch { }
    } else {
        try { foreach ($k in @(pgrep -P $PidValue)) { kill -9 $k 2>$null } } catch { }
        try { kill -9 $PidValue 2>$null } catch { }
    }
}

switch ($Action) {
    'status' {
        $state = Get-State
        [pscustomobject]@{
            backendUp   = (Test-Port $BackendPort)
            frontendUp  = (Test-Port $FrontendPort)
            backendPid  = $state.backendPid
            frontendPid = $state.frontendPid
            backendUrl  = "http://localhost:$BackendPort"
            frontendUrl = "http://localhost:$FrontendPort"
        } | ConvertTo-Json
    }

    'stop' {
        $state = Get-State
        Stop-Pid $state.backendPid
        Stop-Pid $state.frontendPid
        Save-State ([pscustomobject]@{ backendPid = $null; frontendPid = $null })
        Write-Host 'e2e stack stopped.'
    }

    'start' {
        $state = Get-State

        if (-not (Test-Port $BackendPort)) {
            $beOut = Join-Path $LogDir 'e2e-backend.log'
            $beErr = Join-Path $LogDir 'e2e-backend.err.log'
            $be = Start-Detached -File 'dotnet' `
                -ArgList @('run', '--project', 'AsistOff.MES.Gateway', '--launch-profile', 'http') `
                -WorkDir $Root -OutFile $beOut -ErrFile $beErr
            $state.backendPid = $be.Id
            Write-Host "backend started (pid $($be.Id)) -> $beOut"
        } else {
            Write-Host "backend already up on :$BackendPort"
        }

        if (-not (Test-Port $FrontendPort)) {
            $feOut = Join-Path $LogDir 'e2e-frontend.log'
            $feErr = Join-Path $LogDir 'e2e-frontend.err.log'
            $fe = Start-Detached -File $npm `
                -ArgList @('run', 'dev') `
                -WorkDir (Join-Path $Root 'AsistOff.MES.Web') -OutFile $feOut -ErrFile $feErr
            $state.frontendPid = $fe.Id
            Write-Host "frontend started (pid $($fe.Id)) -> $feOut"
        } else {
            Write-Host "frontend already up on :$FrontendPort"
        }

        Save-State $state

        $deadline = (Get-Date).AddSeconds($WaitSeconds)
        while ((Get-Date) -lt $deadline) {
            if ((Test-Port $BackendPort) -and (Test-Port $FrontendPort)) { break }
            Start-Sleep -Seconds 2
        }

        $backendUp = Test-Port $BackendPort
        $frontendUp = Test-Port $FrontendPort
        [pscustomobject]@{
            backendUp  = $backendUp
            frontendUp = $frontendUp
            backendUrl = "http://localhost:$BackendPort"
            frontendUrl = "http://localhost:$FrontendPort"
        } | ConvertTo-Json

        if (-not ($backendUp -and $frontendUp)) {
            Write-Host ''
            Write-Host 'WARNING: stack did not become healthy in time.'
            Write-Host "  backend log: $(Join-Path $LogDir 'e2e-backend.err.log')"
            Write-Host "  frontend log: $(Join-Path $LogDir 'e2e-frontend.err.log')"
            exit 1
        }
    }
}
