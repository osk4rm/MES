<#
.SYNOPSIS
    Seeds a running AsistOff MES stack with a rich end-to-end demo dataset.

.DESCRIPTION
    Drives the public HTTP API (no direct database access) to create master
    data, recipes with routing/BOM, production orders, lots, confirmations,
    stock movements, genealogy, losses, SPC, kanban, telemetry and maintenance
    so the whole shopfloor flow can be exercised in the UI.

    Requires a running gateway and the seeded dev admin account. Auth cookies
    are Secure, so they are captured from the sign-in Set-Cookie header and
    replayed manually as a Cookie request header (works over plain HTTP).

    Every object code is prefixed with a per-run tag (SD<tag>) so repeat runs
    accumulate data instead of colliding. Measure units are reused when their
    symbol already exists. Optional modules that the running image does not
    expose (e.g. an older build) are skipped with a warning.

.PARAMETER BaseUrl
    Gateway base URL. Default http://localhost:8080 (docker compose); use
    http://localhost:5243 for the `dotnet run` dev profile.

.PARAMETER Email
    Dev admin email. Default admin@dev.local.

.PARAMETER Password
    Dev admin password. Default Passw0rd!.

.PARAMETER Tag
    Per-run code tag (A-Z0-9, max 6 chars). Default is derived from the clock.

.PARAMETER Orders
    How many production orders to create. Default 8.

.PARAMETER DryRun
    Print the plan without calling the API.

.EXAMPLE
    pwsh -File scripts/seed/seed-demo-data.ps1
.EXAMPLE
    pwsh -File scripts/seed/seed-demo-data.ps1 -BaseUrl http://localhost:5243 -Orders 12
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:8080',
    [string]$Email = 'admin@dev.local',
    [string]$Password = 'Passw0rd!',
    [string]$Tag,
    [ValidateRange(1, 50)][int]$Orders = 8,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

if ([string]::IsNullOrWhiteSpace($Tag)) {
    $Tag = (Get-Date -Format 'ddHHmm') + (Get-Random -Minimum 10 -Maximum 99).ToString()
}
$Tag = ($Tag.ToUpper() -replace '[^A-Z0-9]', '')
if ($Tag.Length -gt 6) { $Tag = $Tag.Substring($Tag.Length - 6) }
$P = "SD$Tag"

$script:AuthHeaders = @{}
$script:Counts = [ordered]@{
    MeasureUnits = 0; ProductGroups = 0; Warehouses = 0; Departments = 0
    Machines = 0; Operators = 0; Skills = 0; Shifts = 0; ReasonCodes = 0
    Products = 0; Recipes = 0; Operations = 0; Orders = 0; Lots = 0
    Confirmations = 0; Scrap = 0; Downtime = 0; Andon = 0; Spc = 0
    Telemetry = 0; OpcUa = 0; Kanban = 0; Maintenance = 0
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
        Method              = $Method
        Uri                 = "$BaseUrl$Path"
        Headers             = $script:AuthHeaders
        SkipHttpErrorCheck  = $true
        TimeoutSec          = 60
    }
    if ($null -ne $Body) {
        $params.ContentType = 'application/json'
        $params.Body = ($Body | ConvertTo-Json -Depth 12)
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
    $r = Invoke-Mes -Method Post -Path $Path -Body $Body
    return $r.id
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
        code            = $Code
        productId       = $ProductId
        measureUnitId   = $MeasureUnitId
        quantity        = $Quantity
        supplierLotNumber = $null
        producedAt      = $null
        expiryDate      = $null
        notes           = $Notes
    }
}

# ---------------------------------------------------------------- connect
Write-Section "Sign in ($Email)"
if ($DryRun) {
    $userId = [guid]::NewGuid()
    Write-Host "  [dry-run] would sign in and resolve the user id"
} else {
    $login = Invoke-WebRequest -Uri "$BaseUrl/api/auth/sign-in" -Method Post `
        -ContentType 'application/json' `
        -Body (@{ email = $Email; password = $Password } | ConvertTo-Json) `
        -SkipHttpErrorCheck -TimeoutSec 30
    if ($login.StatusCode -ne 200) {
        throw "Sign-in failed ($($login.StatusCode)): $($login.Content)"
    }
    $access = $null
    foreach ($cookie in $login.Headers['Set-Cookie']) {
        if ($cookie -match '^mes_access=([^;]+)') { $access = $Matches[1] }
    }
    if (-not $access) { throw 'Sign-in returned no mes_access cookie.' }
    $userId = ($login.Content | ConvertFrom-Json).id
    $script:AuthHeaders = @{ Cookie = "mes_access=$access" }
    Write-Host "  authenticated, user $userId"
}
Write-Host "  run tag: $Tag (codes prefixed '$P')"

# ---------------------------------------------------------------- master data
Write-Section 'Measure units'
$muPcs = Get-OrCreateMeasureUnit 'szt' 'Sztuka'
$muKg  = Get-OrCreateMeasureUnit 'kg'  'Kilogram'
Write-Host "  pcs=$muPcs kg=$muKg"

Write-Section 'Product groups / warehouses / departments'
$pgRm = New-Entity '/api/product-groups' @{ code = "$P-RM"; name = "Surowce $Tag"; isActive = $true }
$pgSf = New-Entity '/api/product-groups' @{ code = "$P-SF"; name = "Polprodukty $Tag"; isActive = $true }
$pgFg = New-Entity '/api/product-groups' @{ code = "$P-FG"; name = "Wyroby gotowe $Tag"; isActive = $true }
$script:Counts.ProductGroups += 3

$whRm  = New-Entity '/api/warehouses' @{ name = "Magazyn surowcow $Tag" }
$whFg  = New-Entity '/api/warehouses' @{ name = "Magazyn wyrobow $Tag" }
$whBuf = New-Entity '/api/warehouses' @{ name = "Magazyn buforowy $Tag" }
$script:Counts.Warehouses += 3

$deptMach = New-Entity '/api/departments' @{ code = "$P-OBR";  name = 'Obrobka' }
$deptMont = New-Entity '/api/departments' @{ code = "$P-MONT"; name = 'Montaz' }
$deptPack = New-Entity '/api/departments' @{ code = "$P-PAK";  name = 'Pakowanie' }
$script:Counts.Departments += 3

Write-Section 'Shifts / skills / reason codes'
$shiftMorning = New-Entity '/api/shifts' @{ code = "$P-S1"; name = 'Zmiana ranna';      startTime = '06:00:00'; endTime = '14:00:00'; isActive = $true }
$shiftAfter   = New-Entity '/api/shifts' @{ code = "$P-S2"; name = 'Zmiana popoludniowa'; startTime = '14:00:00'; endTime = '22:00:00'; isActive = $true }
$shiftNight   = New-Entity '/api/shifts' @{ code = "$P-S3"; name = 'Zmiana nocna';      startTime = '22:00:00'; endTime = '06:00:00'; isActive = $true }
$script:Counts.Shifts += 3

$skillWeld = New-Entity '/api/skills' @{ code = "$P-WELD"; name = 'Spawanie';        isActive = $true }
$skillAssy = New-Entity '/api/skills' @{ code = "$P-ASSY"; name = 'Montaz';          isActive = $true }
$skillCnc  = New-Entity '/api/skills' @{ code = "$P-CNC";  name = 'Operator CNC';    isActive = $true }
$skillQc   = New-Entity '/api/skills' @{ code = "$P-QC";   name = 'Kontrola jakosci'; isActive = $true }
$script:Counts.Skills += 4

$rcBreakdown = New-Entity '/api/reason-codes' @{ code = "$P-DT-BRK"; name = 'Awaria maszyny';        category = 1; isActive = $true; sortIndex = 1 }
$rcSetup     = New-Entity '/api/reason-codes' @{ code = "$P-DT-SET"; name = 'Przezbrojenie';         category = 4; isActive = $true; sortIndex = 2 }
$rcNoMat     = New-Entity '/api/reason-codes' @{ code = "$P-DT-MAT"; name = 'Brak materialu';        category = 1; isActive = $true; sortIndex = 3 }
$rcTolerance = New-Entity '/api/reason-codes' @{ code = "$P-SC-TOL"; name = 'Odchylka tolerancji';   category = 2; isActive = $true; sortIndex = 4 }
$rcCrack     = New-Entity '/api/reason-codes' @{ code = "$P-SC-CRK"; name = 'Pekniecie';             category = 2; isActive = $true; sortIndex = 5 }
$rcQuality   = New-Entity '/api/reason-codes' @{ code = "$P-QC-REJ"; name = 'Reklamacja klienta';    category = 3; isActive = $true; sortIndex = 6 }
$script:Counts.ReasonCodes += 6

Write-Section 'Work centers (machines) + calendars'
$machineDefs = @(
    @{ code = "$P-M-CNC1"; name = 'Frezarka CNC 1';        dept = $deptMach; cap = 8;  eff = 0.92 }
    @{ code = "$P-M-CNC2"; name = 'Tokarka CNC 2';         dept = $deptMach; cap = 6;  eff = 0.90 }
    @{ code = "$P-M-WELD"; name = 'Stanowisko spawalnicze'; dept = $deptMont; cap = 4;  eff = 0.85 }
    @{ code = "$P-M-ASSY"; name = 'Linia montazowa';        dept = $deptMont; cap = 10; eff = 0.95 }
    @{ code = "$P-M-PACK"; name = 'Pakowaczka';             dept = $deptPack; cap = 12; eff = 0.98 }
    @{ code = "$P-M-QC";   name = 'Kontrola jakosci';       dept = $deptPack; cap = 8;  eff = 0.97 }
)
$machines = @{}
foreach ($m in $machineDefs) {
    $machines[$m.code] = New-Entity '/api/machines' @{
        code = $m.code; name = $m.name; isActive = $true
        departmentId = $m.dept; capacity = $m.cap; efficiencyFactor = $m.eff
    }
    $script:Counts.Machines++
}
$entries = @()
foreach ($d in 1..5) {
    $entries += @{ dayOfWeek = $d; startTime = '06:00:00'; endTime = '14:00:00'; shiftId = $shiftMorning; isWorking = $true }
    $entries += @{ dayOfWeek = $d; startTime = '14:00:00'; endTime = '22:00:00'; shiftId = $shiftAfter;   isWorking = $true }
}
foreach ($code in $machines.Keys) {
    Try-Step "calendar $code" { Invoke-Mes -Method Put -Path "/api/machines/$($machines[$code])/calendar" -Body @{ entries = $entries } | Out-Null }
}

Write-Section 'Operators + shift roster'
$operatorDefs = @(
    @{ id = "$P-OP-01"; first = 'Adam';   last = 'Kowalski';  rate = 42.5; dept = $deptMach }
    @{ id = "$P-OP-02"; first = 'Ewa';    last = 'Nowak';     rate = 39.0; dept = $deptMach }
    @{ id = "$P-OP-03"; first = 'Piotr';  last = 'Wisniewski'; rate = 45.0; dept = $deptMont }
    @{ id = "$P-OP-04"; first = 'Anna';   last = 'Wojcik';    rate = 41.0; dept = $deptMont }
    @{ id = "$P-OP-05"; first = 'Marek';  last = 'Kaminski';  rate = 38.5; dept = $deptPack }
    @{ id = "$P-OP-06"; first = 'Zofia';  last = 'Lewandowska'; rate = 44.0; dept = $deptPack }
)
$operatorIds = @()
foreach ($o in $operatorDefs) {
    $operatorIds += New-Entity '/api/operators' @{
        identifier = $o.id; firstName = $o.first; lastName = $o.last
        ratePerHour = $o.rate; departmentId = $o.dept; userId = $userId
    }
    $script:Counts.Operators++
}
$today = (Get-Date).ToString('yyyy-MM-dd')
$tomorrow = (Get-Date).AddDays(1).ToString('yyyy-MM-dd')
$shiftCycle = @($shiftMorning, $shiftAfter, $shiftNight)
for ($i = 0; $i -lt $operatorIds.Count; $i++) {
    $day = if ($i % 2 -eq 0) { $today } else { $tomorrow }
    Try-Step "roster $i" {
        New-Entity '/api/operator-shift-assignments' @{
            operatorId = $operatorIds[$i]; shiftId = $shiftCycle[$i % 3]; date = $day
        } | Out-Null
    }
}

Write-Section 'Products'
$scanCode = 2
$productDefs = @(
    @{ code = "$P-P-STL";   name = 'Stal S235';        group = $pgRm; mu = $muKg }
    @{ code = "$P-P-ALU";   name = 'Aluminium PA6';    group = $pgRm; mu = $muKg }
    @{ code = "$P-P-PCB";   name = 'Plyta PCB';        group = $pgRm; mu = $muPcs }
    @{ code = "$P-P-BOLT";  name = 'Srouba M6';        group = $pgRm; mu = $muPcs }
    @{ code = "$P-P-PAINT"; name = 'Farba proszkowa';  group = $pgRm; mu = $muKg }
    @{ code = "$P-P-FRAME"; name = 'Rama stalowa';     group = $pgSf; mu = $muPcs }
    @{ code = "$P-P-PANEL"; name = 'Panel elektroniczny'; group = $pgSf; mu = $muPcs }
    @{ code = "$P-P-WA";    name = 'Widget A';         group = $pgFg; mu = $muPcs }
    @{ code = "$P-P-WB";    name = 'Widget B';         group = $pgFg; mu = $muPcs }
    @{ code = "$P-P-GX";    name = 'Gadget X';         group = $pgFg; mu = $muPcs }
)
$products = @{}
$productMu = @{}
foreach ($def in $productDefs) {
    $products[$def.code] = New-Entity '/api/products' @{
        code = $def.code; name = $def.name; isActive = $true
        scanBy = $scanCode; productGroupId = $def.group
    }
    $productMu[$def.code] = $def.mu
    $script:Counts.Products++
}

Write-Section 'Recipes with routing, BOM and outputs'
function New-Recipe {
    param(
        [string]$Code, [string]$Name, [string]$FinishedCode, [array]$Operations
    )
    $script:Counts.Recipes++
    $recipe = Invoke-Mes -Method Post -Path '/api/recipes' -Body @{
        code = $Code; name = $Name; isActive = $true; primaryProductId = $products[$FinishedCode]
    }
    $versionId = $recipe.versions[0].id
    if (-not $versionId) {
        $detail = Invoke-Mes -Method Get -Path "/api/recipes/$($recipe.id)"
        $versionId = $detail.versions[0].id
    }
    $previous = $null
    foreach ($op in $Operations) {
        $script:Counts.Operations++
        $operation = Invoke-Mes -Method Post -Path '/api/operations' -Body @{
            versionId = $versionId; code = "$Code-$($op.code)"; name = $op.name
            sortIndex = $op.sort; setupTimeMinutes = $op.setup
            runTimeMode = 1; runTimePerUnitSeconds = $op.unitSeconds
            isOptional = $false; allowParallelExecution = $false
        }
        foreach ($bom in $op.bom) {
            Invoke-Mes -Method Post -Path "/api/operations/$($operation.id)/bom-items" -Body @{
                operationId = $operation.id; productId = $products[$bom.product]
                quantity = $bom.qty; quantityType = 1; isOptional = $false
                consumptionTiming = 1; measureUnitId = $productMu[$bom.product]
            } | Out-Null
        }
        if ($op.output) {
            Invoke-Mes -Method Post -Path "/api/operations/$($operation.id)/outputs" -Body @{
                operationId = $operation.id; productId = $products[$op.output.product]
                quantity = $op.output.qty; quantityType = 1; outputType = $op.output.type
                measureUnitId = $productMu[$op.output.product]
            } | Out-Null
        }
        Invoke-Mes -Method Post -Path "/api/operations/$($operation.id)/resources" -Body @{
            operationId = $operation.id; preferredMachineId = $machines[$op.machine]
            requiredOperatorCount = $op.operators
        } | Out-Null
        if ($previous) {
            Invoke-Mes -Method Put -Path "/api/operations/$($operation.id)/dependencies" -Body @{
                dependencies = @(
                    @{ predecessorOperationId = $previous; dependencyType = 1; lagMinutes = $null }
                )
            } | Out-Null
        }
        $previous = $operation.id
    }
    Invoke-Mes -Method Post -Path "/api/recipe-versions/$versionId/release" | Out-Null
    return @{ id = $recipe.id; versionId = $versionId }
}

$recipeA = New-Recipe -Code "$P-R-WA" -Name "Widget A flow $Tag" -FinishedCode "$P-P-WA" -Operations @(
    @{ code = 'CUT';  name = 'Ciecie i giecie';      sort = 1; setup = 15; unitSeconds = 45; machine = "$P-M-CNC1"; operators = 1
       bom = @(@{ product = "$P-P-STL"; qty = 2.5 }); output = @{ product = "$P-P-FRAME"; qty = 1; type = 5 } }
    @{ code = 'WELD'; name = 'Spawanie ramy';        sort = 2; setup = 10; unitSeconds = 90; machine = "$P-M-WELD"; operators = 1
       bom = @(@{ product = "$P-P-BOLT"; qty = 4 }) }
    @{ code = 'ASSY'; name = 'Montaz elektroniki';   sort = 3; setup = 5;  unitSeconds = 60; machine = "$P-M-ASSY"; operators = 2
       bom = @(@{ product = "$P-P-FRAME"; qty = 1 }, @{ product = "$P-P-PCB"; qty = 1 }, @{ product = "$P-P-BOLT"; qty = 2 })
       output = @{ product = "$P-P-WA"; qty = 1; type = 1 } }
)
$recipeB = New-Recipe -Code "$P-R-WB" -Name "Widget B flow $Tag" -FinishedCode "$P-P-WB" -Operations @(
    @{ code = 'BODY'; name = 'Obrobka korpusu';      sort = 1; setup = 12; unitSeconds = 50; machine = "$P-M-CNC2"; operators = 1
       bom = @(@{ product = "$P-P-ALU"; qty = 1.5 }); output = @{ product = "$P-P-PANEL"; qty = 1; type = 5 } }
    @{ code = 'FIN';  name = 'Wykonczenie i montaz'; sort = 2; setup = 8;  unitSeconds = 70; machine = "$P-M-ASSY"; operators = 2
       bom = @(@{ product = "$P-P-PANEL"; qty = 1 }, @{ product = "$P-P-PAINT"; qty = 0.3 })
       output = @{ product = "$P-P-WB"; qty = 1; type = 1 } }
)

Write-Section "Production orders ($Orders) + confirmations + lots"
$recipeAProduct = "$P-P-WA"
$recipeBProduct = "$P-P-WB"
$recipeMaterials = @{
    "$P-P-WA" = @("$P-P-STL", "$P-P-PCB", "$P-P-BOLT")
    "$P-P-WB" = @("$P-P-ALU", "$P-P-PAINT")
}
$orderMachine = $machines["$P-M-ASSY"]
$now = [DateTime]::UtcNow
for ($i = 1; $i -le $Orders; $i++) {
    $useB = ($i % 3 -eq 0)
    $finished = if ($useB) { $recipeBProduct } else { $recipeAProduct }
    $recipe = if ($useB) { $recipeB } else { $recipeA }
    $planned = [decimal](40 + 10 * $i)
    $script:Counts.Orders++
    $order = Invoke-Mes -Method Post -Path '/api/production-orders' -Body @{
        code = "$P-ORD-$('{0:D3}' -f $i)"; productId = $products[$finished]
        recipeId = $recipe.id; recipeVersionId = $recipe.versionId
        plannedQuantity = $planned; measureUnitId = $productMu[$finished]
        priority = ($i % 5) + 1; dueDate = $now.AddDays(($i % 7) - 2).ToString('o')
        notes = "seed order $i"
    }
    Invoke-Mes -Method Post -Path "/api/production-orders/$($order.id)/release" | Out-Null

    $good1 = [math]::Floor($planned * 0.6)
    $consumed = @()
    $matIndex = 0
    foreach ($material in $recipeMaterials[$finished]) {
        $matIndex++
        $consumedQty = [math]::Round($good1 * (0.4 + 0.2 * $matIndex), 2)
        $lotId = New-Lot -Code "$P-$('{0:D3}' -f $i)-RM$matIndex" -ProductId $products[$material] `
            -MeasureUnitId $productMu[$material] -Quantity ([math]::Round($consumedQty * 1.5, 2)) `
            -Notes "seed raw lot $i/$matIndex"
        $consumed += @{ lotId = $lotId; quantity = $consumedQty }
    }
    $producedLot1 = New-Lot -Code "$P-$('{0:D3}' -f $i)-FG1" -ProductId $products[$finished] `
        -MeasureUnitId $productMu[$finished] -Quantity $good1 -Notes "seed output lot $i/1"
    $script:Counts.Confirmations++
    Invoke-Mes -Method Post -Path '/api/production-confirmations' -Body @{
        productionOrderId = $order.id; machineId = $orderMachine
        reportedByOperatorId = $operatorIds[($i - 1) % $operatorIds.Count]
        reportedAt = [DateTime]::UtcNow.AddSeconds($i).ToString('o')
        goodQuantity = $good1; scrapQuantity = 2; notes = "seed confirmation $i/1"
        producedLotId = $producedLot1; consumedLots = $consumed
    } | Out-Null

    $script:Counts.Scrap++
    Try-Step "scrap $i" {
        Invoke-Mes -Method Post -Path '/api/scrap-events' -Body @{
            machineId = $orderMachine; reasonCodeId = $(if ($i % 2) { $rcTolerance } else { $rcCrack })
            quantity = 2; reportedAt = [DateTime]::UtcNow.AddSeconds(15 + $i).ToString('o')
            reportedByOperatorId = $operatorIds[($i - 1) % $operatorIds.Count]
            productionOrderId = $order.id; notes = "seed scrap $i"
        } | Out-Null
    }

    if ($i % 2 -eq 0) {
        $good2 = $planned - $good1
        $producedLot2 = New-Lot -Code "$P-$('{0:D3}' -f $i)-FG2" -ProductId $products[$finished] `
            -MeasureUnitId $productMu[$finished] -Quantity $good2 -Notes "seed output lot $i/2"
        $script:Counts.Confirmations++
        Invoke-Mes -Method Post -Path '/api/production-confirmations' -Body @{
            productionOrderId = $order.id; machineId = $orderMachine
            reportedByOperatorId = $operatorIds[($i + 1) % $operatorIds.Count]
            reportedAt = [DateTime]::UtcNow.AddSeconds(30 + $i).ToString('o')
            goodQuantity = $good2; scrapQuantity = 0; notes = "seed confirmation $i/2"
            producedLotId = $producedLot2; consumedLots = @()
        } | Out-Null
        Try-Step "complete+close $i" {
            Invoke-Mes -Method Post -Path "/api/production-orders/$($order.id)/complete" | Out-Null
            Invoke-Mes -Method Post -Path "/api/production-orders/$($order.id)/close" | Out-Null
        }
    }
}

Write-Section 'Downtime + Andon'
$machineList = @($machines.Values)
for ($i = 0; $i -lt [math]::Min(6, $machineList.Count); $i++) {
    $script:Counts.Downtime++
    Try-Step "downtime $i" {
        $dt = Invoke-Mes -Method Post -Path '/api/downtime-events' -Body @{
            machineId = $machineList[$i]
            reasonCodeId = $(if ($i % 2) { $rcBreakdown } else { $rcSetup })
            startedAt = $now.AddHours(-12 + $i).ToString('o')
            reportedByOperatorId = $operatorIds[$i % $operatorIds.Count]
            notes = "seed downtime $i"
        }
        Invoke-Mes -Method Post -Path "/api/downtime-events/$($dt.id)/close" -Body @{ endedAt = $now.AddHours(-11 + $i).ToString('o') } | Out-Null
    }
}
$script:Counts.Andon++
Try-Step 'andon raise' {
    $andon = Invoke-Mes -Method Post -Path '/api/andon-signals' -Body @{
        machineId = $machines["$P-M-WELD"]; category = 1; reasonCodeId = $rcBreakdown
        raisedAt = $now.AddMinutes(-30).ToString('o'); notes = 'seed andon downtime'
    }
    Invoke-Mes -Method Post -Path "/api/andon-signals/$($andon.id)/acknowledge" | Out-Null
}
Try-Step 'andon resolve' {
    $andon2 = Invoke-Mes -Method Post -Path '/api/andon-signals' -Body @{
        machineId = $machines["$P-M-QC"]; category = 2; reasonCodeId = $rcQuality
        raisedAt = $now.AddHours(-2).ToString('o'); notes = 'seed andon quality'
    }
    Invoke-Mes -Method Post -Path "/api/andon-signals/$($andon2.id)/resolve" -Body @{ resolvedAt = $now.AddHours(-1).ToString('o') } | Out-Null
}

Write-Section 'SPC'
$script:Counts.Spc++
Try-Step 'spc' {
    $characteristic = Invoke-Mes -Method Post -Path '/api/spc-characteristics' -Body @{
        code = "$P-SPC-WA"; name = 'Grubosc powloki Widget A'
        productId = $products["$P-P-WA"]; machineId = $machines["$P-M-WELD"]
        chartType = 1; nominalValue = 100; lowerSpecLimit = 95; upperSpecLimit = 105
        lowerControlLimit = 97; upperControlLimit = 103; sampleSize = 5; unit = 'um'; isActive = $true
    }
    for ($i = 0; $i -lt 30; $i++) {
        $value = [math]::Round(100 + (Get-Random -Minimum -400 -Maximum 400) / 100.0, 2)
        if ($i -eq 17) { $value = 106.4 }
        Try-Step "spc reading $i" {
            Invoke-Mes -Method Post -Path '/api/spc-measurements' -Body @{
                characteristicId = $characteristic.id; value = $value
                measuredAt = $now.AddHours(-$i).ToString('o'); notes = "seed measurement $i"
            } | Out-Null
        }
    }
}

Write-Section 'Telemetry + OPC UA'
Try-Step 'telemetry' {
    $tags = @(
        @{ node = 'ns=2;s=SpindleSpeed'; name = 'Predkosc wrzeciona' }
        @{ node = 'ns=2;s=FeedRate';     name = 'Posuw' }
        @{ node = 'ns=2;s=Temperature';  name = 'Temperatura' }
    )
    foreach ($t in $tags) {
        $script:Counts.Telemetry++
        $tag = Invoke-Mes -Method Post -Path '/api/telemetry-tags' -Body @{
            machineId = $machines["$P-M-CNC1"]; nodeId = $t.node
            displayName = $t.name; dataType = 2; pollIntervalSeconds = 5
        }
        for ($i = 0; $i -lt 10; $i++) {
            Try-Step "reading $($t.node) $i" {
                Invoke-Mes -Method Post -Path '/api/telemetry-readings' -Body @{
                    tagId = $tag.id; readAt = $now.AddMinutes(-$i).ToString('o')
                    doubleValue = [math]::Round(1000 + (Get-Random -Minimum -200 -Maximum 200), 2)
                    quality = 1
                } | Out-Null
            }
        }
    }
    $script:Counts.OpcUa++
    Invoke-Mes -Method Post -Path '/api/opcua-connections' -Body @{
        machineId = $machines["$P-M-CNC1"]; endpointUrl = 'opc.tcp://localhost:4840'
        securityPolicy = 1; pollIntervalSeconds = 5
    } | Out-Null
}

Write-Section 'Kanban'
$script:Counts.Kanban++
Try-Step 'kanban' {
    $loop = Invoke-Mes -Method Post -Path '/api/kanban/loops' -Body @{
        code = "$P-KB-BOLT"; productId = $products["$P-P-BOLT"]
        consumingMachineId = $machines["$P-M-ASSY"]; supplyingWarehouseId = $whRm
        cardQuantity = 50; cardsInCirculation = 3; notes = 'seed kanban'
    }
    $cards = @()
    foreach ($n in 1..3) {
        $cards += Invoke-Mes -Method Post -Path "/api/kanban/loops/$($loop.id)/cards" -Body @{
            cardNumber = "$P-KB-BOLT-$n"; notes = "seed card $n"
        }
    }
    Invoke-Mes -Method Post -Path "/api/kanban/cards/$($cards[0].id)/consume" | Out-Null
    Invoke-Mes -Method Post -Path "/api/kanban/cards/$($cards[0].id)/order" | Out-Null
    Invoke-Mes -Method Post -Path "/api/kanban/cards/$($cards[0].id)/replenish" | Out-Null
    Invoke-Mes -Method Post -Path "/api/kanban/cards/$($cards[1].id)/consume" | Out-Null
}

Write-Section 'Maintenance'
Try-Step 'maintenance plan' {
    $script:Counts.Maintenance++
    Invoke-Mes -Method Post -Path '/api/maintenance-plans' -Body @{
        code = "$P-MP-CNC1"; name = 'Przeglad miesieczny CNC 1'
        machineId = $machines["$P-M-CNC1"]; triggerType = 1; intervalDays = 30
        nextDueAt = $now.AddHours(6).ToString('o'); isActive = $true
    } | Out-Null
    Invoke-Mes -Method Post -Path '/api/maintenance-plans' -Body @{
        code = "$P-MP-WELD"; name = 'Przeglad metrowy spawalnia'
        machineId = $machines["$P-M-WELD"]; triggerType = 2; meterIntervalValue = 500
        isActive = $true
    } | Out-Null
    Invoke-Mes -Method Post -Path '/api/maintenance-plans/evaluate-due' -Body @{ currentMeterReading = 1200 } -AllowMissing | Out-Null
    $workOrder = Invoke-Mes -Method Post -Path '/api/maintenance-work-orders' -Body @{
        code = "$P-MWO-1"; title = 'Wymiana lozyska wrzeciona'; machineId = $machines["$P-M-CNC1"]; priority = 3
    }
    Invoke-Mes -Method Post -Path "/api/maintenance-work-orders/$($workOrder.id)/start" | Out-Null
    Invoke-Mes -Method Post -Path "/api/maintenance-work-orders/$($workOrder.id)/complete" -Body @{ resolutionNotes = 'Wymieniono lozysko' } | Out-Null
}

# ---------------------------------------------------------------- summary
Write-Section 'Summary'
$script:Counts.GetEnumerator() | ForEach-Object { Write-Host ("  {0,-16} {1}" -f $_.Key, $_.Value) }
Write-Host ''
Write-Host "Seed complete. Tag: $Tag" -ForegroundColor Green
Write-Host "UI: $BaseUrl  (frontend http://localhost:3000, or :5173 for the vite dev server)"
