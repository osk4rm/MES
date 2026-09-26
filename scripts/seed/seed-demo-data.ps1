<#
.SYNOPSIS
    Seeds a running AsistOff MES stack with a rich, report-ready demo dataset.

.DESCRIPTION
    Drives the public HTTP API (no direct database access) to create master
    data, multi-level complex recipes (DAG routing, byproducts, scrap uplift,
    batch/unit runtime modes), production orders across the whole lifecycle,
    lots, confirmations, RW/PW movements, genealogy, losses, Andon, SPC,
    telemetry, kanban and maintenance so every screen and report is populated.

    Auth cookies are Secure, so they are captured from the sign-in Set-Cookie
    header and replayed manually as a Cookie request header (works over HTTP).

    Time model: the API refuses confirmations reported before the order was
    released, so production output lands "now" (dense enough for the OEE/Loss
    panels). Downtime, scrap, SPC and telemetry timestamps ARE backdated across
    -Days so the reliability/trend/SPC/telemetry charts have history.

    Every code is prefixed with a per-run tag (SD<tag>), so repeat runs
    accumulate data instead of colliding. Measure units are reused by symbol.

.PARAMETER BaseUrl
    Gateway base URL. Default http://localhost:8080 (docker compose); use
    http://localhost:5243 for the `dotnet run` dev profile.

.PARAMETER Email
.PARAMETER Password
.PARAMETER Tag
    Per-run code tag (A-Z0-9, max 6 chars). Default derived from the clock.

.PARAMETER Orders
    Approximate number of finished-goods production orders (default 24).

.PARAMETER Days
    Backfill window for downtime/scrap/SPC/telemetry history (default 10).

.PARAMETER NewTenant
    Self-register a fresh tenant (demo-<tag>) and seed it, so every report
    starts clean. Login becomes admin@demo-<tag>.local with the same password.

.PARAMETER DryRun

.EXAMPLE
    pwsh -File scripts/seed/seed-demo-data.ps1
.EXAMPLE
    pwsh -File scripts/seed/seed-demo-data.ps1 -BaseUrl http://localhost:5243 -Orders 40 -Days 14
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:8080',
    [string]$Email = 'admin@dev.local',
    [string]$Password = 'Passw0rd!',
    [string]$Tag,
    [ValidateRange(1, 200)][int]$Orders = 24,
    [ValidateRange(1, 60)][int]$Days = 10,
    [switch]$NewTenant,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

if ([string]::IsNullOrWhiteSpace($Tag)) {
    $Tag = (Get-Date -Format 'ddHHmm') + (Get-Random -Minimum 10 -Maximum 99).ToString()
}
$Tag = ($Tag.ToUpper() -replace '[^A-Z0-9]', '')
if ($Tag.Length -gt 6) { $Tag = $Tag.Substring($Tag.Length - 6) }
$Prefix = "SD$Tag"

$script:AuthHeaders = @{}
$script:ConfirmMachineIndex = -1
$script:Counts = [ordered]@{
    MeasureUnits = 0; ProductGroups = 0; Warehouses = 0; Departments = 0
    Machines = 0; Operators = 0; Skills = 0; Shifts = 0; ReasonCodes = 0
    Products = 0; Recipes = 0; Operations = 0; SupplyOrders = 0; IntermediateOrders = 0
    Orders = 0; Lots = 0; Confirmations = 0; Scrap = 0; Downtime = 0; Andon = 0
    Spc = 0; Telemetry = 0; OpcUa = 0; Kanban = 0; Maintenance = 0
}

function Write-Section([string]$Text) {
    Write-Host ''
    Write-Host "=== $Text ===" -ForegroundColor Cyan
}

function Invoke-Mes {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        $Body,
        [switch]$AllowMissing
    )
    if ($DryRun) {
        if ($Method -ne 'Get') { return [pscustomobject]@{ id = [guid]::NewGuid() } }
        return $null
    }
    $params = @{
        Method             = $Method
        Uri                = "$BaseUrl$Path"
        Headers            = $script:AuthHeaders
        SkipHttpErrorCheck = $true
        TimeoutSec         = 120
    }
    if ($null -ne $Body) {
        $params.ContentType = 'application/json'
        $params.Body = ($Body | ConvertTo-Json -Depth 14)
    }
    $resp = Invoke-WebRequest @params
    if ($resp.StatusCode -eq 404 -and $AllowMissing) { return $null }
    if ($resp.StatusCode -ge 400) {
        throw "HTTP $($resp.StatusCode) $Method $Path :: $($resp.Content)"
    }
    if ([string]::IsNullOrWhiteSpace($resp.Content)) { return $null }
    return $resp.Content | ConvertFrom-Json
}

function New-Entity {
    param([string]$Path, [hashtable]$Body)
    return (Invoke-Mes -Method Post -Path $Path -Body $Body).id
}

function Get-Items {
    param([Parameter(Mandatory)][string]$Path)
    $r = Invoke-Mes -Method Get -Path $Path
    if ($null -eq $r) { return @() }
    if ($r.PSObject.Properties.Name -contains 'items') { return @($r.items) }
    return @($r)
}

function Try-Step {
    param([string]$Label, [scriptblock]$Action)
    try { & $Action } catch { Write-Warning "$Label skipped: $($_.Exception.Message)" }
}

function Get-OrCreateMeasureUnit {
    param([string]$Symbol, [string]$Name)
    $existing = Get-Items '/api/measure-units' | Where-Object { $_.symbol -eq $Symbol } | Select-Object -First 1
    if ($existing) { return $existing.id }
    $script:Counts.MeasureUnits++
    return New-Entity '/api/measure-units' @{ name = $Name; symbol = $Symbol; type = 1; isActive = $true }
}

function New-Lot {
    param([string]$Code, [string]$ProductId, [string]$MeasureUnitId, [decimal]$Quantity, [string]$Notes)
    $script:Counts.Lots++
    return New-Entity '/api/lots' @{
        code = $Code; productId = $ProductId; measureUnitId = $MeasureUnitId
        quantity = $Quantity; supplierLotNumber = $null; producedAt = $null
        expiryDate = $null; notes = $Notes
    }
}

# A pool of consumable lots per product code, so genealogy chains across phases.
$script:LotPool = @{}
function Add-LotToPool {
    param([string]$ProductCode, [string]$LotId, [decimal]$Quantity)
    if (-not $script:LotPool.ContainsKey($ProductCode)) { $script:LotPool[$ProductCode] = New-Object System.Collections.ArrayList }
    [void]$script:LotPool[$ProductCode].Add([pscustomobject]@{ id = $LotId; available = $Quantity })
}
function Take-FromPool {
    param([string]$ProductCode, [decimal]$Quantity)
    if (-not $script:LotPool.ContainsKey($ProductCode)) { return $null }
    foreach ($lot in $script:LotPool[$ProductCode]) {
        if ($lot.available -ge $Quantity) { $lot.available -= $Quantity; return $lot.id }
    }
    return $null
}

# ---------------------------------------------------------------- tenant
if ($NewTenant) {
    $slug = "demo-$($Tag.ToLower())"
    $Email = "admin@$slug.local"
    Write-Section "Create tenant ($slug)"
    if ($DryRun) {
        Write-Host "  [dry-run] would self-register tenant '$slug' with admin $Email"
    } else {
        $signup = Invoke-WebRequest -Uri "$BaseUrl/api/tenants" -Method Post -ContentType 'application/json' `
            -Body (@{
                name = $slug; displayName = "Demo $Tag"; contactEmail = $Email
                settings = ''; password = $Password; confirmPassword = $Password
            } | ConvertTo-Json) -SkipHttpErrorCheck -TimeoutSec 60
        if ($signup.StatusCode -ge 400) { throw "Tenant signup failed ($($signup.StatusCode)): $($signup.Content)" }
        $tenantId = ($signup.Content | ConvertFrom-Json).id
        Write-Host "  tenant $tenantId created; admin $Email"
    }
}

# ---------------------------------------------------------------- connect
Write-Section "Sign in ($Email)"
if ($DryRun) {
    $userId = [guid]::NewGuid()
} else {
    # A freshly self-registered tenant provisions its admin asynchronously
    # (outbox relay), so retry 401 for a short while before giving up.
    $login = $null
    for ($attempt = 1; $attempt -le 20; $attempt++) {
        $login = Invoke-WebRequest -Uri "$BaseUrl/api/auth/sign-in" -Method Post `
            -ContentType 'application/json' `
            -Body (@{ email = $Email; password = $Password } | ConvertTo-Json) `
            -SkipHttpErrorCheck -TimeoutSec 30
        if ($login.StatusCode -eq 200) { break }
        if ($login.StatusCode -ne 401) { break }
        Start-Sleep -Seconds 2
    }
    if ($login.StatusCode -ne 200) { throw "Sign-in failed ($($login.StatusCode)): $($login.Content)" }
    $access = $null
    foreach ($cookie in $login.Headers['Set-Cookie']) {
        if ($cookie -match '^mes_access=([^;]+)') { $access = $Matches[1] }
    }
    if (-not $access) { throw 'Sign-in returned no mes_access cookie.' }
    $userId = ($login.Content | ConvertFrom-Json).id
    $script:AuthHeaders = @{ Cookie = "mes_access=$access" }
    Write-Host "  authenticated, user $userId"
}
Write-Host "  run tag: $Tag (codes prefixed '$Prefix')"

# ---------------------------------------------------------------- lookups
Write-Section 'Measure units'
$muPcs = Get-OrCreateMeasureUnit 'szt' 'Sztuka'
$muKg  = Get-OrCreateMeasureUnit 'kg'  'Kilogram'
$muM   = Get-OrCreateMeasureUnit 'm'   'Metr'
$muOf = @{ pcs = $muPcs; kg = $muKg; m = $muM }

# ---------------------------------------------------------------- master data
Write-Section 'Product groups / warehouses / departments'
$pgRm  = New-Entity '/api/product-groups' @{ code = "$Prefix-RM"; name = "Surowce $Tag"; isActive = $true }
$pgSf  = New-Entity '/api/product-groups' @{ code = "$Prefix-SF"; name = "Polprodukty $Tag"; isActive = $true }
$pgFg  = New-Entity '/api/product-groups' @{ code = "$Prefix-FG"; name = "Wyroby gotowe $Tag"; isActive = $true }
$script:Counts.ProductGroups += 3

$whRm  = New-Entity '/api/warehouses' @{ name = "Magazyn surowcow $Tag" }
$whFg  = New-Entity '/api/warehouses' @{ name = "Magazyn wyrobow $Tag" }
$whBuf = New-Entity '/api/warehouses' @{ name = "Magazyn buforowy $Tag" }
$script:Counts.Warehouses += 3

$deptMach = New-Entity '/api/departments' @{ code = "$Prefix-OBR";  name = 'Obrobka' }
$deptMont = New-Entity '/api/departments' @{ code = "$Prefix-MONT"; name = 'Montaz' }
$deptPack = New-Entity '/api/departments' @{ code = "$Prefix-PAK";  name = 'Pakowanie' }
$deptQc   = New-Entity '/api/departments' @{ code = "$Prefix-QC";   name = 'Jakosc' }
$script:Counts.Departments += 4

Write-Section 'Shifts / skills / reason codes'
$shiftMorning = New-Entity '/api/shifts' @{ code = "$Prefix-S1"; name = 'Zmiana ranna';        startTime = '06:00:00'; endTime = '14:00:00'; isActive = $true }
$shiftAfter   = New-Entity '/api/shifts' @{ code = "$Prefix-S2"; name = 'Zmiana popoludniowa'; startTime = '14:00:00'; endTime = '22:00:00'; isActive = $true }
$shiftNight   = New-Entity '/api/shifts' @{ code = "$Prefix-S3"; name = 'Zmiana nocna';        startTime = '22:00:00'; endTime = '06:00:00'; isActive = $true }
$script:Counts.Shifts += 3

$skillCnc  = New-Entity '/api/skills' @{ code = "$Prefix-CNC";  name = 'Operator CNC';        isActive = $true }
$skillWeld = New-Entity '/api/skills' @{ code = "$Prefix-WELD"; name = 'Spawanie';            isActive = $true }
$skillAssy = New-Entity '/api/skills' @{ code = "$Prefix-ASSY"; name = 'Montaz';              isActive = $true }
$skillQc   = New-Entity '/api/skills' @{ code = "$Prefix-QC";   name = 'Kontrola jakosci';    isActive = $true }
$skillPack = New-Entity '/api/skills' @{ code = "$Prefix-PACK"; name = 'Pakowanie';           isActive = $true }
$script:Counts.Skills += 5

$rcBreakdown = New-Entity '/api/reason-codes' @{ code = "$Prefix-DT-BRK";  name = 'Awaria maszyny';       category = 1; isActive = $true; sortIndex = 1 }
$rcElectric  = New-Entity '/api/reason-codes' @{ code = "$Prefix-DT-ELEC"; name = 'Awaria elektryczna';   category = 1; isActive = $true; sortIndex = 2 }
$rcSetup     = New-Entity '/api/reason-codes' @{ code = "$Prefix-DT-SET";  name = 'Przezbrojenie';        category = 4; isActive = $true; sortIndex = 3 }
$rcNoMat     = New-Entity '/api/reason-codes' @{ code = "$Prefix-DT-MAT";  name = 'Brak materialu';       category = 1; isActive = $true; sortIndex = 4 }
$rcTolerance = New-Entity '/api/reason-codes' @{ code = "$Prefix-SC-TOL";  name = 'Odchylka tolerancji';  category = 2; isActive = $true; sortIndex = 5 }
$rcCrack     = New-Entity '/api/reason-codes' @{ code = "$Prefix-SC-CRK";  name = 'Pekniecie';            category = 2; isActive = $true; sortIndex = 6 }
$rcSurface   = New-Entity '/api/reason-codes' @{ code = "$Prefix-SC-SRF";  name = 'Wada powierzchni';     category = 2; isActive = $true; sortIndex = 7 }
$rcQuality   = New-Entity '/api/reason-codes' @{ code = "$Prefix-QC-REJ";  name = 'Reklamacja klienta';   category = 3; isActive = $true; sortIndex = 8 }
$script:Counts.ReasonCodes += 8

Write-Section 'Work centers (machines) + calendars'
$machineDefs = @(
    @{ code = "$Prefix-M-CNC1"; name = 'Frezarka CNC 1';         dept = $deptMach; cap = 8;  eff = 0.92 }
    @{ code = "$Prefix-M-CNC2"; name = 'Tokarka CNC 2';          dept = $deptMach; cap = 6;  eff = 0.90 }
    @{ code = "$Prefix-M-WELD1"; name = 'Stanowisko spawalnicze 1'; dept = $deptMont; cap = 4; eff = 0.85 }
    @{ code = "$Prefix-M-WELD2"; name = 'Stanowisko spawalnicze 2'; dept = $deptMont; cap = 4; eff = 0.86 }
    @{ code = "$Prefix-M-ASSY1"; name = 'Linia montazowa 1';       dept = $deptMont; cap = 10; eff = 0.95 }
    @{ code = "$Prefix-M-ASSY2"; name = 'Linia montazowa 2';       dept = $deptMont; cap = 10; eff = 0.94 }
    @{ code = "$Prefix-M-PACK"; name = 'Pakowaczka / malarnia';    dept = $deptPack; cap = 12; eff = 0.98 }
    @{ code = "$Prefix-M-QC";   name = 'Kontrola jakosci';         dept = $deptQc;   cap = 8;  eff = 0.97 }
)
$machines = @{}
foreach ($m in $machineDefs) {
    $machines[$m.code] = New-Entity '/api/machines' @{
        code = $m.code; name = $m.name; isActive = $true
        departmentId = $m.dept; capacity = $m.cap; efficiencyFactor = $m.eff
    }
    $script:Counts.Machines++
}
# Dedicated receiving work center for opening stock: keeps the large PW
# receipts out of the production machines' OEE counts.
$recvMachine = New-Entity '/api/machines' @{
    code = "$Prefix-M-RECV"; name = 'Stanowisko przyjecia (demo)'; isActive = $true
    departmentId = $deptQc; capacity = 100; efficiencyFactor = 1
}
$script:Counts.Machines++

$machineList = @($machines.Values)
# 24/7 calendar (all days, three shifts) so OEE planned time is never zero
# regardless of when the demo is run.
$calEntries = @()
foreach ($d in 0..6) {
    $calEntries += @{ dayOfWeek = $d; startTime = '06:00:00'; endTime = '14:00:00'; shiftId = $shiftMorning; isWorking = $true }
    $calEntries += @{ dayOfWeek = $d; startTime = '14:00:00'; endTime = '22:00:00'; shiftId = $shiftAfter;   isWorking = $true }
    $calEntries += @{ dayOfWeek = $d; startTime = '22:00:00'; endTime = '06:00:00'; shiftId = $shiftNight;   isWorking = $true }
}
foreach ($code in $machines.Keys) {
    Try-Step "calendar $code" { Invoke-Mes -Method Put -Path "/api/machines/$($machines[$code])/calendar" -Body @{ entries = $calEntries } | Out-Null }
}

Write-Section 'Operators + shift roster'
$operatorDefs = @(
    @{ id = "$Prefix-OP-01"; first = 'Adam';  last = 'Kowalski';    rate = 42.5; dept = $deptMach }
    @{ id = "$Prefix-OP-02"; first = 'Ewa';   last = 'Nowak';       rate = 39.0; dept = $deptMach }
    @{ id = "$Prefix-OP-03"; first = 'Piotr'; last = 'Wisniewski';  rate = 45.0; dept = $deptMont }
    @{ id = "$Prefix-OP-04"; first = 'Anna';  last = 'Wojcik';      rate = 41.0; dept = $deptMont }
    @{ id = "$Prefix-OP-05"; first = 'Marek'; last = 'Kaminski';    rate = 38.5; dept = $deptMont }
    @{ id = "$Prefix-OP-06"; first = 'Zofia'; last = 'Lewandowska'; rate = 44.0; dept = $deptPack }
    @{ id = "$Prefix-OP-07"; first = 'Tomasz'; last = 'Zielinski';  rate = 47.0; dept = $deptQc }
    @{ id = "$Prefix-OP-08"; first = 'Katarzyna'; last = 'Szymanska'; rate = 40.0; dept = $deptPack }
)
$operatorIds = @()
foreach ($o in $operatorDefs) {
    $operatorIds += New-Entity '/api/operators' @{
        identifier = $o.id; firstName = $o.first; lastName = $o.last
        ratePerHour = $o.rate; departmentId = $o.dept; userId = $userId
    }
    $script:Counts.Operators++
}
$shiftCycle = @($shiftMorning, $shiftAfter, $shiftNight)
for ($d = 0; $d -lt [math]::Min($Days, 5); $d++) {
    $date = (Get-Date).AddDays(-$d).ToString('yyyy-MM-dd')
    for ($i = 0; $i -lt $operatorIds.Count; $i++) {
        Try-Step "roster $d/$i" {
            New-Entity '/api/operator-shift-assignments' @{
                operatorId = $operatorIds[$i]; shiftId = $shiftCycle[($i + $d) % 3]; date = $date
            } | Out-Null
        }
    }
}

Write-Section 'Products'
$productDefs = @(
    @{ code = "$Prefix-P-STL";   name = 'Stal S235';           group = $pgRm; mu = $muKg }
    @{ code = "$Prefix-P-ALU";   name = 'Aluminium PA6';       group = $pgRm; mu = $muKg }
    @{ code = "$Prefix-P-PCB";   name = 'Plyta PCB';           group = $pgRm; mu = $muPcs }
    @{ code = "$Prefix-P-BOLT";  name = 'Srouba M6';           group = $pgRm; mu = $muPcs }
    @{ code = "$Prefix-P-PAINT"; name = 'Farba proszkowa';     group = $pgRm; mu = $muKg }
    @{ code = "$Prefix-P-CABLE"; name = 'Przewod 1.5mm';       group = $pgRm; mu = $muM }
    @{ code = "$Prefix-P-BEAR";  name = 'Lozysko 6204';        group = $pgRm; mu = $muPcs }
    @{ code = "$Prefix-P-GLASS"; name = 'Szyba hartowana';     group = $pgRm; mu = $muPcs }
    @{ code = "$Prefix-P-RUB";   name = 'Guma EPDM';           group = $pgRm; mu = $muKg }
    @{ code = "$Prefix-P-SCRAP"; name = 'Zlom produkcyjny';    group = $pgRm; mu = $muKg }
    @{ code = "$Prefix-P-FRAME"; name = 'Rama stalowa';        group = $pgSf; mu = $muPcs }
    @{ code = "$Prefix-P-PANEL"; name = 'Panel elektroniczny'; group = $pgSf; mu = $muPcs }
    @{ code = "$Prefix-P-MOTOR"; name = 'Silnik napedowy';     group = $pgSf; mu = $muPcs }
    @{ code = "$Prefix-P-HOUS";  name = 'Obudowa aluminium';   group = $pgSf; mu = $muPcs }
    @{ code = "$Prefix-P-HARN";  name = 'Wiazka kablowa';      group = $pgSf; mu = $muPcs }
    @{ code = "$Prefix-P-WA";    name = 'Widget A';            group = $pgFg; mu = $muPcs }
    @{ code = "$Prefix-P-WB";    name = 'Widget B';            group = $pgFg; mu = $muPcs }
    @{ code = "$Prefix-P-GX";    name = 'Gadget X';            group = $pgFg; mu = $muPcs }
    @{ code = "$Prefix-P-ARM";   name = 'Robotyczne ramie';    group = $pgFg; mu = $muPcs }
    @{ code = "$Prefix-P-CTRL";  name = 'Sterownik PLC';       group = $pgFg; mu = $muPcs }
)
$products = @{}
$productMu = @{}
foreach ($def in $productDefs) {
    $products[$def.code] = New-Entity '/api/products' @{
        code = $def.code; name = $def.name; isActive = $true
        scanBy = 2; productGroupId = $def.group
    }
    $productMu[$def.code] = $def.mu
    $script:Counts.Products++
}

# ---------------------------------------------------------------- recipes
$script:Recipes = @{}
function New-OpNode {
    param([string]$VersionId, [hashtable]$Op, [string]$RecipeCode, [hashtable]$WarehouseFor)
    $body = @{
        versionId = $VersionId
        code = "$RecipeCode-$($Op.code)"; name = $Op.name; sortIndex = $Op.sort
        setupTimeMinutes = $Op.setup; teardownTimeMinutes = $Op.teardown
        queueTimeMinutes = $Op.queue
        runTimeMode = $(if ($Op.mode) { $Op.mode } else { 1 })
        runTimePerUnitSeconds = $Op.unitSeconds
        runTimePerBatchMinutes = $Op.batchMinutes
        isOptional = [bool]$Op.optional
        allowParallelExecution = [bool]$Op.parallel
    }
    $script:Counts.Operations++
    $operation = Invoke-Mes -Method Post -Path '/api/operations' -Body $body
    foreach ($bom in @($Op.bom)) {
        if (-not $bom -or -not $bom.product) { continue }
        Invoke-Mes -Method Post -Path "/api/operations/$($operation.id)/bom-items" -Body @{
            operationId = $operation.id; productId = $products[$bom.product]
            measureUnitId = $productMu[$bom.product]; quantity = $bom.qty
            quantityType = $(if ($bom.type) { $bom.type } else { 1 })
            scrapPercentage = $bom.scrap; isOptional = [bool]$bom.optional
            preferredWarehouseId = $null
            consumptionTiming = $(if ($bom.timing) { $bom.timing } else { 1 })
        } | Out-Null
    }
    foreach ($out in @($Op.outputs)) {
        if (-not $out -or -not $out.product) { continue }
        Invoke-Mes -Method Post -Path "/api/operations/$($operation.id)/outputs" -Body @{
            operationId = $operation.id; productId = $products[$out.product]
            measureUnitId = $productMu[$out.product]; quantity = $out.qty
            quantityType = 1; outputType = $(if ($out.type) { $out.type } else { 1 })
        } | Out-Null
    }
    Invoke-Mes -Method Post -Path "/api/operations/$($operation.id)/resources" -Body @{
        operationId = $operation.id; preferredMachineId = $machines[$Op.machine]
        preferredDepartmentId = $null; requiredOperatorCount = $(if ($Op.operators) { $Op.operators } else { 1 })
        requiredRole = $null; notes = $null
    } | Out-Null
    return $operation.id
}

function New-Recipe {
    param([string]$Code, [string]$Name, [string]$FinishedCode, [array]$Operations)
    $script:Counts.Recipes++
    $recipeBody = @{ code = $Code; name = $Name; isActive = $true }
    if ($FinishedCode) { $recipeBody.primaryProductId = $products[$FinishedCode] }
    $recipe = Invoke-Mes -Method Post -Path '/api/recipes' -Body $recipeBody
    $versionId = $recipe.versions[0].id
    if (-not $versionId) {
        $detail = Invoke-Mes -Method Get -Path "/api/recipes/$($recipe.id)"
        $versionId = $detail.versions[0].id
    }
    $ids = @{}
    foreach ($op in $Operations) { $ids[$op.code] = New-OpNode -VersionId $versionId -Op $op -RecipeCode $Code }
    foreach ($op in $Operations) {
        $predecessors = @()
        foreach ($after in @($op.after)) {
            if ($after -and $ids.ContainsKey($after)) {
                $predecessors += @{ predecessorOperationId = $ids[$after]; dependencyType = 1; lagMinutes = $null }
            }
        }
        if ($predecessors.Count -gt 0) {
            Invoke-Mes -Method Put -Path "/api/operations/$($ids[$op.code])/dependencies" -Body @{ dependencies = $predecessors } | Out-Null
        }
    }
    Invoke-Mes -Method Post -Path "/api/recipe-versions/$versionId/release" | Out-Null
    $script:Recipes[$Code] = @{ id = $recipe.id; versionId = $versionId; code = $Code; product = $FinishedCode }
    return $script:Recipes[$Code]
}

Write-Section 'Recipes (multi-level, DAG routing, byproducts, scrap uplift)'
$receipt = New-Recipe -Code "$Prefix-R-RECEIPT" -Name "Przyjecie materialu $Tag" -FinishedCode $null -Operations @(
    @{ code = 'RECV'; name = 'Przyjecie i kontrola'; sort = 1; setup = 5; machine = "$Prefix-M-QC"; operators = 1 }
)

$rFrame = New-Recipe -Code "$Prefix-R-FRAME" -Name "Rama stalowa $Tag" -FinishedCode "$Prefix-P-FRAME" -Operations @(
    @{ code = 'CUT';  name = 'Ciecie profili';    sort = 1; setup = 12; unitSeconds = 30; machine = "$Prefix-M-CNC1"; operators = 1
       bom = @(@{ product = "$Prefix-P-STL"; qty = 2.0; scrap = 3 }) }
    @{ code = 'DRIL'; name = 'Wiercenie otworow'; sort = 2; setup = 8;  unitSeconds = 20; machine = "$Prefix-M-CNC2"; operators = 1
       bom = @(@{ product = "$Prefix-P-BOLT"; qty = 0.1 }); after = @('CUT')
       outputs = @(@{ product = "$Prefix-P-FRAME"; qty = 1; type = 5 }); outputsDone = $true }
)

$rPanel = New-Recipe -Code "$Prefix-R-PANEL" -Name "Panel elektroniczny $Tag" -FinishedCode "$Prefix-P-PANEL" -Operations @(
    @{ code = 'SMT'; name = 'Montaz SMT'; sort = 1; setup = 20; unitSeconds = 55; machine = "$Prefix-M-ASSY1"; operators = 2
       bom = @(@{ product = "$Prefix-P-PCB"; qty = 1 }, @{ product = "$Prefix-P-CABLE"; qty = 0.5; timing = 2 })
       outputs = @(@{ product = "$Prefix-P-PANEL"; qty = 1; type = 5 }) }
)

$rMotor = New-Recipe -Code "$Prefix-R-MOTOR" -Name "Silnik napedowy $Tag" -FinishedCode "$Prefix-P-MOTOR" -Operations @(
    @{ code = 'WIND'; name = 'Uzwojenie silnika'; sort = 1; setup = 15; unitSeconds = 120; machine = "$Prefix-M-CNC2"; operators = 2
       bom = @(@{ product = "$Prefix-P-BEAR"; qty = 2 }, @{ product = "$Prefix-P-STL"; qty = 0.4 })
       outputs = @(@{ product = "$Prefix-P-MOTOR"; qty = 1; type = 5 }) }
)

$rHousing = New-Recipe -Code "$Prefix-R-HOUS" -Name "Obudowa aluminium $Tag" -FinishedCode "$Prefix-P-HOUS" -Operations @(
    @{ code = 'CAST'; name = 'Odlewanie korpusu'; sort = 1; setup = 25; unitSeconds = 80; machine = "$Prefix-M-CNC1"; operators = 1
       bom = @(@{ product = "$Prefix-P-ALU"; qty = 1.2; scrap = 4 }) }
    @{ code = 'PAINT'; name = 'Malowanie proszkowe'; sort = 2; setup = 10; mode = 2; batchMinutes = 30; machine = "$Prefix-M-PACK"; operators = 1
       bom = @(@{ product = "$Prefix-P-PAINT"; qty = 0.2; type = 2 }); after = @('CAST')
       outputs = @(@{ product = "$Prefix-P-HOUS"; qty = 1; type = 5 }) }
)

$rHarness = New-Recipe -Code "$Prefix-R-HARN" -Name "Wiazka kablowa $Tag" -FinishedCode "$Prefix-P-HARN" -Operations @(
    @{ code = 'CRIMP'; name = 'Zaciskanie zlaczy'; sort = 1; setup = 6; unitSeconds = 25; machine = "$Prefix-M-ASSY2"; operators = 1
       bom = @(@{ product = "$Prefix-P-CABLE"; qty = 0.8 }, @{ product = "$Prefix-P-BOLT"; qty = 0.2 })
       outputs = @(@{ product = "$Prefix-P-HARN"; qty = 1; type = 5 }) }
)

$rWidgetA = New-Recipe -Code "$Prefix-R-WA" -Name "Widget A flow $Tag" -FinishedCode "$Prefix-P-WA" -Operations @(
    @{ code = 'CUT';  name = 'Ciecie i giecie';   sort = 1; setup = 15; teardown = 5; queue = 5; unitSeconds = 45; machine = "$Prefix-M-CNC1"; operators = 1
       bom = @(@{ product = "$Prefix-P-STL"; qty = 2.5; scrap = 5 }) }
    @{ code = 'WELD'; name = 'Spawanie ramy';     sort = 2; setup = 10; unitSeconds = 90; machine = "$Prefix-M-WELD1"; operators = 1
       bom = @(@{ product = "$Prefix-P-BOLT"; qty = 4; timing = 2 }); after = @('CUT') }
    @{ code = 'PRIME'; name = 'Malowanie podkladu'; sort = 3; setup = 8; mode = 2; batchMinutes = 25; machine = "$Prefix-M-PACK"; operators = 1
       bom = @(@{ product = "$Prefix-P-PAINT"; qty = 0.2 }); after = @('WELD') }
    @{ code = 'ASSY'; name = 'Montaz elektroniki'; sort = 4; setup = 5; unitSeconds = 60; machine = "$Prefix-M-ASSY1"; operators = 2
       bom = @(@{ product = "$Prefix-P-PANEL"; qty = 1 }, @{ product = "$Prefix-P-HARN"; qty = 1 }, @{ product = "$Prefix-P-CABLE"; qty = 0.3 }); after = @('WELD') }
    @{ code = 'FINAL'; name = 'Montaz koncowy i test'; sort = 5; setup = 5; unitSeconds = 40; machine = "$Prefix-M-ASSY2"; operators = 2
       bom = @(@{ product = "$Prefix-P-FRAME"; qty = 1 }); after = @('ASSY', 'PRIME')
       outputs = @(@{ product = "$Prefix-P-WA"; qty = 1; type = 1 }, @{ product = "$Prefix-P-SCRAP"; qty = 0.1; type = 3 }) }
)

$rWidgetB = New-Recipe -Code "$Prefix-R-WB" -Name "Widget B flow $Tag" -FinishedCode "$Prefix-P-WB" -Operations @(
    @{ code = 'BODY'; name = 'Obrobka korpusu';    sort = 1; setup = 12; unitSeconds = 50; machine = "$Prefix-M-CNC2"; operators = 1
       bom = @(@{ product = "$Prefix-P-ALU"; qty = 1.5; scrap = 3 }) }
    @{ code = 'MOUNT'; name = 'Montaz modulow';   sort = 2; setup = 8; unitSeconds = 70; machine = "$Prefix-M-ASSY1"; operators = 2
       bom = @(@{ product = "$Prefix-P-PANEL"; qty = 1 }, @{ product = "$Prefix-P-BOLT"; qty = 2 }); after = @('BODY') }
    @{ code = 'FIN';  name = 'Wykonczenie i pakowanie'; sort = 3; setup = 5; mode = 2; batchMinutes = 20; machine = "$Prefix-M-PACK"; operators = 1
       bom = @(@{ product = "$Prefix-P-PAINT"; qty = 0.3; type = 2 }); after = @('MOUNT')
       outputs = @(@{ product = "$Prefix-P-WB"; qty = 1; type = 1 }) }
)

$rGadget = New-Recipe -Code "$Prefix-R-GX" -Name "Gadget X flow $Tag" -FinishedCode "$Prefix-P-GX" -Operations @(
    @{ code = 'HOUS'; name = 'Montaz obudowy';    sort = 1; setup = 10; unitSeconds = 55; machine = "$Prefix-M-ASSY1"; operators = 1
       bom = @(@{ product = "$Prefix-P-HOUS"; qty = 1 }, @{ product = "$Prefix-P-RUB"; qty = 0.1 }) }
    @{ code = 'ELEC'; name = 'Montaz elektroniki'; sort = 2; setup = 12; unitSeconds = 65; machine = "$Prefix-M-ASSY2"; operators = 2
       bom = @(@{ product = "$Prefix-P-PANEL"; qty = 1 }, @{ product = "$Prefix-P-HARN"; qty = 1 }); after = @('HOUS') }
    @{ code = 'GLASS'; name = 'Montaz szyby';     sort = 3; setup = 6; unitSeconds = 35; machine = "$Prefix-M-ASSY1"; operators = 1
       bom = @(@{ product = "$Prefix-P-GLASS"; qty = 1; type = 3 }); after = @('ELEC') }
    @{ code = 'TEST'; name = 'Test funkcjonalny'; sort = 4; setup = 5; unitSeconds = 45; machine = "$Prefix-M-QC"; operators = 1
       optional = $true; after = @('GLASS')
       outputs = @(@{ product = "$Prefix-P-GX"; qty = 1; type = 1 }) }
)

$rArm = New-Recipe -Code "$Prefix-R-ARM" -Name "Ramie robotyczne - zlozony przeplyw $Tag" -FinishedCode "$Prefix-P-ARM" -Operations @(
    @{ code = 'BASECUT'; name = 'Ciecie podstawy';      sort = 1; setup = 20; teardown = 8; unitSeconds = 70; machine = "$Prefix-M-CNC1"; operators = 1
       bom = @(@{ product = "$Prefix-P-STL"; qty = 3.0; scrap = 5 }) }
    @{ code = 'BASETURN'; name = 'Toczenie podstawy';   sort = 2; setup = 15; unitSeconds = 90; machine = "$Prefix-M-CNC2"; operators = 1
       bom = @(@{ product = "$Prefix-P-ALU"; qty = 1.0 }); after = @('BASECUT') }
    @{ code = 'JOINT'; name = 'Frezowanie przegubow';   sort = 3; setup = 18; unitSeconds = 110; machine = "$Prefix-M-CNC1"; operators = 1
       bom = @(@{ product = "$Prefix-P-ALU"; qty = 0.8; scrap = 2 }); after = @('BASECUT') }
    @{ code = 'WELD'; name = 'Spawanie konstrukcji';    sort = 4; setup = 12; unitSeconds = 140; machine = "$Prefix-M-WELD1"; operators = 2
       bom = @(@{ product = "$Prefix-P-BOLT"; qty = 6; timing = 2 }); after = @('BASETURN', 'JOINT') }
    @{ code = 'MOTOR'; name = 'Montaz napedow';         sort = 5; setup = 10; unitSeconds = 100; machine = "$Prefix-M-WELD2"; operators = 2
       bom = @(@{ product = "$Prefix-P-MOTOR"; qty = 2 }); after = @('WELD') }
    @{ code = 'HARN'; name = 'Prowadzenie okablowania'; sort = 6; setup = 8; unitSeconds = 60; machine = "$Prefix-M-ASSY2"; operators = 1
       bom = @(@{ product = "$Prefix-P-HARN"; qty = 1 }, @{ product = "$Prefix-P-CABLE"; qty = 1.5 }); after = @('WELD') }
    @{ code = 'ASSY'; name = 'Montaz glowicy';          sort = 7; setup = 15; unitSeconds = 160; machine = "$Prefix-M-ASSY1"; operators = 3
       bom = @(@{ product = "$Prefix-P-FRAME"; qty = 1 }, @{ product = "$Prefix-P-HOUS"; qty = 2 }, @{ product = "$Prefix-P-PANEL"; qty = 2 })
       after = @('MOTOR', 'HARN') }
    @{ code = 'PAINT'; name = 'Malowanie koncowe';      sort = 8; setup = 12; mode = 2; batchMinutes = 45; machine = "$Prefix-M-PACK"; operators = 1
       bom = @(@{ product = "$Prefix-P-PAINT"; qty = 0.6; type = 2 }); after = @('ASSY') }
    @{ code = 'QC'; name = 'Kontrola koncowa';          sort = 9; setup = 5; unitSeconds = 90; machine = "$Prefix-M-QC"; operators = 1
       optional = $true; after = @('PAINT')
       outputs = @(@{ product = "$Prefix-P-ARM"; qty = 1; type = 1 }, @{ product = "$Prefix-P-SCRAP"; qty = 0.2; type = 3 }) }
)

$rCtrl = New-Recipe -Code "$Prefix-R-CTRL" -Name "Sterownik PLC flow $Tag" -FinishedCode "$Prefix-P-CTRL" -Operations @(
    @{ code = 'PCB'; name = 'Montaz PCB';       sort = 1; setup = 10; unitSeconds = 60; machine = "$Prefix-M-ASSY2"; operators = 2
       bom = @(@{ product = "$Prefix-P-PCB"; qty = 2 }, @{ product = "$Prefix-P-CABLE"; qty = 1.0 }) }
    @{ code = 'HOUS'; name = 'Montaz obudowy';  sort = 2; setup = 8; unitSeconds = 45; machine = "$Prefix-M-ASSY1"; operators = 1
       bom = @(@{ product = "$Prefix-P-RUB"; qty = 0.2 }); after = @('PCB') }
    @{ code = 'FLASH'; name = 'Programowanie';  sort = 3; setup = 5; unitSeconds = 30; machine = "$Prefix-M-QC"; operators = 1
       after = @('HOUS') }
    @{ code = 'FINAL'; name = 'Test i pakowanie'; sort = 4; setup = 5; mode = 2; batchMinutes = 15; machine = "$Prefix-M-PACK"; operators = 1
       bom = @(@{ product = "$Prefix-P-HARN"; qty = 1 }); after = @('FLASH')
       outputs = @(@{ product = "$Prefix-P-CTRL"; qty = 1; type = 1 }) }
)

# ---------------------------------------------------------------- stock + orders
function New-Order {
    param([string]$Code, [string]$ProductCode, [hashtable]$Recipe, [decimal]$Planned, [int]$Priority, [string]$DueDate, [string]$Notes)
    return New-Entity '/api/production-orders' @{
        code = $Code; productId = $products[$ProductCode]
        recipeId = $Recipe.id; recipeVersionId = $Recipe.versionId
        plannedQuantity = $Planned; measureUnitId = $productMu[$ProductCode]
        priority = $Priority; dueDate = $DueDate; notes = $Notes
    }
}
function Release-Order { param([string]$OrderId) Invoke-Mes -Method Post -Path "/api/production-orders/$OrderId/release" | Out-Null }

function Confirm-Order {
    param(
        [string]$OrderId, [string]$ProductCode, [string]$MachineCode, [decimal]$Good, [decimal]$Scrap,
        [string]$ProducedLotCode, [array]$Consumed,  # each: @{ product=code; qty=decimal }
        [switch]$Receiving
    )
    $consumedEntries = @()
    $producedLotId = New-Lot -Code $ProducedLotCode -ProductId $products[$ProductCode] `
        -MeasureUnitId $productMu[$ProductCode] -Quantity $Good -Notes "seed output $ProducedLotCode"
    $rollup = @{}
    foreach ($mat in $Consumed) {
        if (-not $rollup.ContainsKey($mat.product)) { $rollup[$mat.product] = [decimal]0 }
        $rollup[$mat.product] += [decimal]$mat.qty
    }
    foreach ($material in $rollup.Keys) {
        $lotId = Take-FromPool -ProductCode $material -Quantity $rollup[$material]
        if (-not $lotId) {
            $lotId = New-Lot -Code "$ProducedLotCode-$material" -ProductId $products[$material] `
                -MeasureUnitId $productMu[$material] -Quantity ([math]::Round($rollup[$material] * 3, 2)) -Notes 'seed fallback lot'
        }
        $consumedEntries += @{ lotId = $lotId; quantity = [math]::Round($rollup[$material], 3) }
    }
    $script:Counts.Confirmations++
    # Round-robin across the production work centers so every machine's
    # OEE/reliability panel has data (a confirmation is attributed to one
    # machine). Opening-stock receipts go to the receiving work center.
    if ($Receiving) {
        $confirmMachine = $recvMachine
    } else {
        $script:ConfirmMachineIndex++
        $confirmMachine = $machineList[$script:ConfirmMachineIndex % $machineList.Count]
    }
    Invoke-Mes -Method Post -Path '/api/production-confirmations' -Body @{
        productionOrderId = $OrderId; machineId = $confirmMachine
        reportedByOperatorId = $operatorIds[(Get-Random -Minimum 0 -Maximum $operatorIds.Count)]
        reportedAt = [DateTime]::UtcNow.ToString('o')
        goodQuantity = $Good; scrapQuantity = $Scrap; notes = "seed confirmation $ProducedLotCode"
        producedLotId = $producedLotId; consumedLots = $consumedEntries
    } | Out-Null
    Add-LotToPool -ProductCode $ProductCode -LotId $producedLotId -Quantity $Good
    return $producedLotId
}

Write-Section 'Opening stock (raw materials)'
$rawProducts = @("$Prefix-P-STL", "$Prefix-P-ALU", "$Prefix-P-PCB", "$Prefix-P-BOLT", "$Prefix-P-PAINT", "$Prefix-P-CABLE", "$Prefix-P-BEAR", "$Prefix-P-GLASS", "$Prefix-P-RUB")
foreach ($raw in $rawProducts) {
    $script:Counts.SupplyOrders++
    $orderId = New-Order -Code "$Prefix-SUP-$($raw.Split('-')[-1])" -ProductCode $raw -Recipe $receipt -Planned 8000 -Priority 5 `
        -DueDate ([DateTime]::UtcNow.AddDays(30).ToString('o')) -Notes 'seed opening stock'
    Release-Order $orderId
    $lotId = Confirm-Order -OrderId $orderId -ProductCode $raw -MachineCode "$Prefix-M-QC" -Good 8000 -Scrap 0 `
        -ProducedLotCode "$Prefix-SUP-$($raw.Split('-')[-1])-L1" -Consumed @() -Receiving
}

Write-Section 'Intermediate production (feeds multi-level genealogy)'
$intermediateRecipes = @(
    @{ recipe = $rFrame; product = "$Prefix-P-FRAME"; machine = "$Prefix-M-CNC2"; mats = @(@{ product = "$Prefix-P-STL"; ratio = 2.0 }, @{ product = "$Prefix-P-BOLT"; ratio = 0.1 }) }
    @{ recipe = $rPanel; product = "$Prefix-P-PANEL"; machine = "$Prefix-M-ASSY1"; mats = @(@{ product = "$Prefix-P-PCB"; ratio = 1.0 }, @{ product = "$Prefix-P-CABLE"; ratio = 0.5 }) }
    @{ recipe = $rMotor; product = "$Prefix-P-MOTOR"; machine = "$Prefix-M-CNC2"; mats = @(@{ product = "$Prefix-P-BEAR"; ratio = 2.0 }, @{ product = "$Prefix-P-STL"; ratio = 0.4 }) }
    @{ recipe = $rHousing; product = "$Prefix-P-HOUS"; machine = "$Prefix-M-PACK"; mats = @(@{ product = "$Prefix-P-ALU"; ratio = 1.2 }, @{ product = "$Prefix-P-PAINT"; ratio = 0.2 }) }
    @{ recipe = $rHarness; product = "$Prefix-P-HARN"; machine = "$Prefix-M-ASSY2"; mats = @(@{ product = "$Prefix-P-CABLE"; ratio = 0.8 }, @{ product = "$Prefix-P-BOLT"; ratio = 0.2 }) }
)
foreach ($inter in $intermediateRecipes) {
    for ($batch = 1; $batch -le 8; $batch++) {
        $script:Counts.IntermediateOrders++
        $qty = Get-Random -Minimum 40 -Maximum 90
        $orderId = New-Order -Code "$Prefix-INT-$($inter.product.Split('-')[-1])-$batch" -ProductCode $inter.product -Recipe $inter.recipe `
            -Planned $qty -Priority 3 -DueDate ([DateTime]::UtcNow.AddDays(5).ToString('o')) -Notes 'seed intermediate'
        Release-Order $orderId
        $consumed = foreach ($mat in $inter.mats) { @{ product = $mat.product; qty = [math]::Round($qty * $mat.ratio, 2) } }
        Confirm-Order -OrderId $orderId -ProductCode $inter.product -MachineCode $inter.machine -Good $qty -Scrap 1 `
            -ProducedLotCode "$Prefix-INT-$($inter.product.Split('-')[-1])-$batch" -Consumed $consumed | Out-Null
    }
}

Write-Section "Finished orders ($Orders) across the whole lifecycle"
$finishedRecipes = @(
    @{ recipe = $rWidgetA; product = "$Prefix-P-WA";   machine = "$Prefix-M-ASSY2"; mats = @(@{ product = "$Prefix-P-STL"; ratio = 2.5 }, @{ product = "$Prefix-P-BOLT"; ratio = 4 }, @{ product = "$Prefix-P-PANEL"; ratio = 1 }, @{ product = "$Prefix-P-HARN"; ratio = 1 }, @{ product = "$Prefix-P-FRAME"; ratio = 1 }, @{ product = "$Prefix-P-PAINT"; ratio = 0.2 }) }
    @{ recipe = $rWidgetB; product = "$Prefix-P-WB";   machine = "$Prefix-M-ASSY1"; mats = @(@{ product = "$Prefix-P-ALU"; ratio = 1.5 }, @{ product = "$Prefix-P-PANEL"; ratio = 1 }, @{ product = "$Prefix-P-BOLT"; ratio = 2 }, @{ product = "$Prefix-P-PAINT"; ratio = 0.3 }) }
    @{ recipe = $rGadget;  product = "$Prefix-P-GX";   machine = "$Prefix-M-QC";    mats = @(@{ product = "$Prefix-P-HOUS"; ratio = 1 }, @{ product = "$Prefix-P-PANEL"; ratio = 1 }, @{ product = "$Prefix-P-HARN"; ratio = 1 }, @{ product = "$Prefix-P-GLASS"; ratio = 1 }, @{ product = "$Prefix-P-RUB"; ratio = 0.1 }) }
    @{ recipe = $rCtrl;    product = "$Prefix-P-CTRL";  machine = "$Prefix-M-PACK";  mats = @(@{ product = "$Prefix-P-PCB"; ratio = 2 }, @{ product = "$Prefix-P-CABLE"; ratio = 1 }, @{ product = "$Prefix-P-RUB"; ratio = 0.2 }, @{ product = "$Prefix-P-HARN"; ratio = 1 }) }
    @{ recipe = $rArm;     product = "$Prefix-P-ARM";   machine = "$Prefix-M-QC";    mats = @(@{ product = "$Prefix-P-STL"; ratio = 3 }, @{ product = "$Prefix-P-ALU"; ratio = 1.8 }, @{ product = "$Prefix-P-BOLT"; ratio = 6 }, @{ product = "$Prefix-P-MOTOR"; ratio = 2 }, @{ product = "$Prefix-P-HARN"; ratio = 1 }, @{ product = "$Prefix-P-CABLE"; ratio = 1.5 }, @{ product = "$Prefix-P-FRAME"; ratio = 1 }, @{ product = "$Prefix-P-HOUS"; ratio = 2 }, @{ product = "$Prefix-P-PANEL"; ratio = 2 }, @{ product = "$Prefix-P-PAINT"; ratio = 0.6 }) }
)
$perRecipe = [math]::Ceiling($Orders / $finishedRecipes.Count)
$orderNo = 0
foreach ($fin in $finishedRecipes) {
    for ($n = 1; $n -le $perRecipe; $n++) {
        $orderNo++
        $planned = Get-Random -Minimum 6 -Maximum 24
        $due = [DateTime]::UtcNow.AddDays((Get-Random -Minimum -5 -Maximum 12)).AddHours((Get-Random -Minimum 0 -Maximum 12))
        $orderId = New-Order -Code "$Prefix-ORD-$('{0:D3}' -f $orderNo)" -ProductCode $fin.product -Recipe $fin.recipe `
            -Planned $planned -Priority ((Get-Random -Minimum 1 -Maximum 6)) -DueDate $due.ToString('o') -Notes "seed order $orderNo"
        $script:Counts.Orders++
        $mode = $orderNo % 6
        if ($mode -eq 0) {
            # Planned only
            continue
        }
        Release-Order $orderId
        if ($mode -eq 5) {
            # Released, not started: dispatch board + active material reservations
            continue
        }
        $consumed1 = foreach ($mat in $fin.mats) { @{ product = $mat.product; qty = [math]::Round(($planned * 0.6) * $mat.ratio, 2) } }
        Confirm-Order -OrderId $orderId -ProductCode $fin.product -MachineCode $fin.machine -Good ([math]::Floor($planned * 0.6)) -Scrap (Get-Random -Minimum 1 -Maximum 5) `
            -ProducedLotCode "$Prefix-$('{0:D3}' -f $orderNo)-FG1" -Consumed $consumed1 | Out-Null
        if ($mode -in 3, 4) {
            $remaining = $planned - [math]::Floor($planned * 0.6)
            $consumed2 = foreach ($mat in $fin.mats) { @{ product = $mat.product; qty = [math]::Round($remaining * $mat.ratio, 2) } }
            Confirm-Order -OrderId $orderId -ProductCode $fin.product -MachineCode $fin.machine -Good $remaining -Scrap 0 `
                -ProducedLotCode "$Prefix-$('{0:D3}' -f $orderNo)-FG2" -Consumed $consumed2 | Out-Null
            Try-Step "complete $orderNo" { Invoke-Mes -Method Post -Path "/api/production-orders/$orderId/complete" | Out-Null }
            if ($mode -eq 4) { Try-Step "close $orderNo" { Invoke-Mes -Method Post -Path "/api/production-orders/$orderId/close" | Out-Null } }
        }
    }
}

# ---------------------------------------------------------------- losses
Write-Section "Downtime ($Days days) + scrap"
$downtimeReasons = @($rcBreakdown, $rcElectric, $rcSetup, $rcNoMat)
for ($i = 0; $i -lt ($Days * 3); $i++) {
    $script:Counts.Downtime++
    Try-Step "downtime $i" {
        $started = [DateTime]::UtcNow.AddDays(-(Get-Random -Minimum 0 -Maximum ($Days + 1))).AddMinutes(-(Get-Random -Minimum 0 -Maximum 720))
        $minutes = Get-Random -Minimum 12 -Maximum 240
        $dt = Invoke-Mes -Method Post -Path '/api/downtime-events' -Body @{
            machineId = $machineList[(Get-Random -Minimum 0 -Maximum $machineList.Count)]
            reasonCodeId = $downtimeReasons[(Get-Random -Minimum 0 -Maximum $downtimeReasons.Count)]
            startedAt = $started.ToString('o'); reportedByOperatorId = $operatorIds[(Get-Random -Minimum 0 -Maximum $operatorIds.Count)]
            notes = "seed downtime $i"
        }
        Invoke-Mes -Method Post -Path "/api/downtime-events/$($dt.id)/close" -Body @{ endedAt = $started.AddMinutes($minutes).ToString('o') } | Out-Null
    }
}
$scrapReasons = @($rcTolerance, $rcCrack, $rcSurface)
$activeOrders = @(Get-Items '/api/production-orders?pageSize=100' | Where-Object { $_.code -like "$Prefix-ORD-*" -and $_.status -in @(2, 3) })
for ($i = 0; $i -lt 24; $i++) {
    $script:Counts.Scrap++
    Try-Step "scrap $i" {
        $orderId = $null
        if ($activeOrders.Count -gt 0) { $orderId = $activeOrders[(Get-Random -Minimum 0 -Maximum $activeOrders.Count)].id }
        Invoke-Mes -Method Post -Path '/api/scrap-events' -Body @{
            machineId = $machineList[(Get-Random -Minimum 0 -Maximum $machineList.Count)]
            reasonCodeId = $scrapReasons[(Get-Random -Minimum 0 -Maximum $scrapReasons.Count)]
            quantity = Get-Random -Minimum 1 -Maximum 12
            reportedAt = [DateTime]::UtcNow.AddHours(-(Get-Random -Minimum 0 -Maximum 8)).ToString('o')
            reportedByOperatorId = $operatorIds[(Get-Random -Minimum 0 -Maximum $operatorIds.Count)]
            productionOrderId = $orderId; notes = "seed scrap $i"
        } | Out-Null
    }
}

Write-Section 'Andon'
$andonStates = @('active', 'ack', 'resolved', 'active', 'resolved')
foreach ($state in $andonStates) {
    $script:Counts.Andon++
    Try-Step "andon $state" {
        # Only one active Andon signal is allowed per work center, so probe
        # candidates until one accepts (older seed runs may hold some).
        $andon = $null
        foreach ($candidate in ($machineList | Sort-Object { Get-Random })) {
            try {
                $andon = Invoke-Mes -Method Post -Path '/api/andon-signals' -Body @{
                    machineId = $candidate; category = (Get-Random -Minimum 1 -Maximum 5)
                    reasonCodeId = $downtimeReasons[(Get-Random -Minimum 0 -Maximum $downtimeReasons.Count)]
                    raisedAt = [DateTime]::UtcNow.AddMinutes(-(Get-Random -Minimum 5 -Maximum 600)).ToString('o')
                    notes = "seed andon $state"
                }
                break
            } catch {
                if ($_.Exception.Message -notlike 'HTTP 409*') { throw }
            }
        }
        if (-not $andon) { throw 'no work center left without an active Andon signal' }
        if ($state -in @('ack', 'resolved')) { Invoke-Mes -Method Post -Path "/api/andon-signals/$($andon.id)/acknowledge" | Out-Null }
        if ($state -eq 'resolved') { Invoke-Mes -Method Post -Path "/api/andon-signals/$($andon.id)/resolve" -Body @{ resolvedAt = [DateTime]::UtcNow.ToString('o') } | Out-Null }
    }
}

Write-Section 'SPC characteristics + measurements'
$spcDefs = @(
    @{ code = "$Prefix-SPC-WA";  name = 'Grubosc powloki Widget A'; product = "$Prefix-P-WA";   machine = "$Prefix-M-PACK";  nominal = 100; lsl = 95;  usl = 105; lcl = 97;  ucl = 103; unit = 'um' }
    @{ code = "$Prefix-SPC-ARM"; name = 'Moment dokrecenia ramienia'; product = "$Prefix-P-ARM"; machine = "$Prefix-M-ASSY1"; nominal = 45;  lsl = 40;  usl = 50;  lcl = 42;  ucl = 48;  unit = 'Nm' }
    @{ code = "$Prefix-SPC-STL"; name = 'Twardosc stali';          product = "$Prefix-P-STL";  machine = "$Prefix-M-CNC1";  nominal = 200; lsl = 180; usl = 220; lcl = 190; ucl = 210; unit = 'HB' }
    @{ code = "$Prefix-SPC-PCB"; name = 'Rezystancja PCB';         product = "$Prefix-P-PCB";  machine = "$Prefix-M-ASSY1"; nominal = 10;  lsl = 9;   usl = 11;  lcl = 9.4; ucl = 10.6; unit = 'kOhm' }
)
foreach ($spc in $spcDefs) {
    $script:Counts.Spc++
    Try-Step "spc $($spc.code)" {
        $characteristic = Invoke-Mes -Method Post -Path '/api/spc-characteristics' -Body @{
            code = $spc.code; name = $spc.name; productId = $products[$spc.product]; machineId = $machines[$spc.machine]
            chartType = 1; nominalValue = $spc.nominal; lowerSpecLimit = $spc.lsl; upperSpecLimit = $spc.usl
            lowerControlLimit = $spc.lcl; upperControlLimit = $spc.ucl; sampleSize = 5; unit = $spc.unit; isActive = $true
        }
        $samples = 60
        for ($i = 0; $i -lt $samples; $i++) {
            $drift = ($i % 20) * 0.02
            $value = [math]::Round($spc.nominal + $drift + (Get-Random -Minimum -300 -Maximum 300) / 100.0, 2)
            if ($i -in 15, 34, 52) { $value = [math]::Round($spc.ucl + 1.5, 2) }
            Try-Step "spc reading $i" {
                Invoke-Mes -Method Post -Path '/api/spc-measurements' -Body @{
                    characteristicId = $characteristic.id; value = $value
                    measuredAt = [DateTime]::UtcNow.AddHours(-$i * 2).ToString('o'); notes = "seed $i"
                } | Out-Null
            }
        }
    }
}

Write-Section 'Telemetry + OPC UA'
foreach ($machineCode in @("$Prefix-M-CNC1", "$Prefix-M-CNC2", "$Prefix-M-WELD1", "$Prefix-M-ASSY1")) {
    Try-Step "telemetry $machineCode" {
        $tags = @(
            @{ node = 'ns=2;s=SpindleSpeed'; name = 'Predkosc wrzeciona' }
            @{ node = 'ns=2;s=FeedRate';     name = 'Posuw' }
            @{ node = 'ns=2;s=Temperature';  name = 'Temperatura' }
            @{ node = 'ns=2;s=Vibration';    name = 'Drgania' }
        )
        foreach ($t in $tags) {
            $script:Counts.Telemetry++
            $tag = Invoke-Mes -Method Post -Path '/api/telemetry-tags' -Body @{
                machineId = $machines[$machineCode]; nodeId = $t.node; displayName = $t.name
                dataType = 2; pollIntervalSeconds = 5
            }
            for ($i = 0; $i -lt 12; $i++) {
                Try-Step "reading $i" {
                    Invoke-Mes -Method Post -Path '/api/telemetry-readings' -Body @{
                        tagId = $tag.id; readAt = [DateTime]::UtcNow.AddMinutes(-$i).ToString('o')
                        doubleValue = [math]::Round(1000 + (Get-Random -Minimum -250 -Maximum 250), 2); quality = 1
                    } | Out-Null
                }
            }
        }
        $script:Counts.OpcUa++
        Invoke-Mes -Method Post -Path '/api/opcua-connections' -Body @{
            machineId = $machines[$machineCode]; endpointUrl = 'opc.tcp://localhost:4840'
            securityPolicy = 1; pollIntervalSeconds = 5
        } | Out-Null
    }
}

Write-Section 'Kanban'
$kanbanDefs = @(
    @{ product = "$Prefix-P-BOLT"; machine = "$Prefix-M-ASSY1"; wh = $whRm; qty = 50 }
    @{ product = "$Prefix-P-PCB";  machine = "$Prefix-M-ASSY1"; wh = $whRm; qty = 30 }
    @{ product = "$Prefix-P-PAINT"; machine = "$Prefix-M-PACK"; wh = $whRm; qty = 20 }
)
foreach ($kb in $kanbanDefs) {
    $script:Counts.Kanban++
    Try-Step "kanban $($kb.product)" {
        $loop = Invoke-Mes -Method Post -Path '/api/kanban/loops' -Body @{
            code = "$Prefix-KB-$($kb.product.Split('-')[-1])"; productId = $products[$kb.product]
            consumingMachineId = $machines[$kb.machine]; supplyingWarehouseId = $kb.wh
            cardQuantity = $kb.qty; cardsInCirculation = (Get-Random -Minimum 2 -Maximum 5); notes = 'seed kanban'
        }
        $cards = @()
        foreach ($n in 1..3) {
            $cards += Invoke-Mes -Method Post -Path "/api/kanban/loops/$($loop.id)/cards" -Body @{ cardNumber = "$Prefix-KB-$($kb.product.Split('-')[-1])-$n"; notes = "card $n" }
        }
        Invoke-Mes -Method Post -Path "/api/kanban/cards/$($cards[0].id)/consume" | Out-Null
        Invoke-Mes -Method Post -Path "/api/kanban/cards/$($cards[0].id)/order" | Out-Null
        Invoke-Mes -Method Post -Path "/api/kanban/cards/$($cards[0].id)/replenish" | Out-Null
        Invoke-Mes -Method Post -Path "/api/kanban/cards/$($cards[1].id)/consume" | Out-Null
        Invoke-Mes -Method Post -Path "/api/kanban/cards/$($cards[2].id)/consume" | Out-Null
    }
}

Write-Section 'Maintenance'
Try-Step 'maintenance' {
    foreach ($machineCode in @("$Prefix-M-CNC1", "$Prefix-M-WELD1", "$Prefix-M-ASSY1")) {
        $script:Counts.Maintenance++
        Invoke-Mes -Method Post -Path '/api/maintenance-plans' -Body @{
            code = "$Prefix-MP-$($machineCode.Split('-')[-1])"; name = "Przeglad $machineCode"
            machineId = $machines[$machineCode]; triggerType = 1; intervalDays = 30
            nextDueAt = [DateTime]::UtcNow.AddDays((Get-Random -Minimum 1 -Maximum 20)).ToString('o'); isActive = $true
        } | Out-Null
    }
    Invoke-Mes -Method Post -Path '/api/maintenance-plans' -Body @{
        code = "$Prefix-MP-METER"; name = 'Przeglad metrowy spawalni'
        machineId = $machines["$Prefix-M-WELD1"]; triggerType = 2; meterIntervalValue = 500; isActive = $true
    } | Out-Null
    Invoke-Mes -Method Post -Path '/api/maintenance-plans/evaluate-due' -Body @{ currentMeterReading = 1750 } -AllowMissing | Out-Null
    $states = @('complete', 'start', 'open', 'complete', 'open')
    $woIndex = 0
    foreach ($state in $states) {
        $woIndex++
        $script:Counts.Maintenance++
        $wo = Invoke-Mes -Method Post -Path '/api/maintenance-work-orders' -Body @{
            code = "$Prefix-MWO-$('{0:D2}' -f $woIndex)"; title = "Zadanie utrzymaniowe $state"
            machineId = $machineList[(Get-Random -Minimum 0 -Maximum $machineList.Count)]
            priority = (Get-Random -Minimum 1 -Maximum 5)
        }
        if ($state -in @('start', 'complete')) { Invoke-Mes -Method Post -Path "/api/maintenance-work-orders/$($wo.id)/start" | Out-Null }
        if ($state -eq 'complete') { Invoke-Mes -Method Post -Path "/api/maintenance-work-orders/$($wo.id)/complete" -Body @{ resolutionNotes = 'Wykonano przeglad' } | Out-Null }
    }
}

# ---------------------------------------------------------------- summary
Write-Section 'Summary'
$script:Counts.GetEnumerator() | ForEach-Object { Write-Host ("  {0,-18} {1}" -f $_.Key, $_.Value) }
Write-Host ''
Write-Host "Seed complete. Tag: $Tag" -ForegroundColor Green
Write-Host "Login: $Email / $Password"
Write-Host "UI: frontend http://localhost:3000 (vite dev :5173); API $BaseUrl"
