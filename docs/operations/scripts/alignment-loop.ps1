param(
    [string]$ApiBaseUrl = "http://localhost:5000/api",
    [string]$AuthToken = "",
    [int]$Count = 100,
    [int]$MaxLoops = 6,
    [int]$ApplyPerLoop = 40,
    [int]$SeedDelayMs = 120,
    [int]$PostSeedWaitSeconds = 20,
    [switch]$SkipReset,
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\\..\\..")
    $stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
    $OutDir = Join-Path $repoRoot ".runlogs\\alignment\\$stamp"
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

function Get-Headers {
    $headers = @{
        "Accept" = "application/json"
    }

    if (-not [string]::IsNullOrWhiteSpace($AuthToken)) {
        $headers["Authorization"] = "Bearer $AuthToken"
    }

    return $headers
}

function Invoke-Api {
    param(
        [ValidateSet("GET","POST")]
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $url = "$ApiBaseUrl/$Path".TrimEnd("/")
    $headers = Get-Headers

    if ($Method -eq "GET") {
        return Invoke-RestMethod -Method GET -Uri $url -Headers $headers
    }

    if ($null -eq $Body) {
        return Invoke-RestMethod -Method POST -Uri $url -Headers $headers -ContentType "application/json" -Body "{}"
    }

    $json = $Body | ConvertTo-Json -Depth 20
    return Invoke-RestMethod -Method POST -Uri $url -Headers $headers -ContentType "application/json" -Body $json
}

function New-SeedEntries {
    param([int]$TargetCount)

    $base = @(
        "Le mie figlie sono da mia madre.",
        "Mia madre si chiama Felicia.",
        "Felia oggi era a casa con le bambine.",
        "Irina e mia moglie, dice che parlo poco con lei.",
        "Sono andato a cena con Adi (fratello) e ho speso 100 euro e devo dargli 50 perche ha pagato lui.",
        "Ho dato 50 ad Adi.",
        "Oggi a Bressana ho camminato 30 minuti.",
        "Domani pranzo con Irina e Julieta.",
        "Sto lavorando al progetto Diario Intelligente.",
        "Mi sento stanco ma determinato."
    )

    $work = @(
        "Call con Cloris sul rilascio backend.",
        "Refactoring ingestione e node view completato.",
        "Bug su ricerca nodi: risolto filtro cross-tipo.",
        "Analisi costi OpenSearch e ottimizzazione query.",
        "Review pipeline CI/CD su Amplify e GitHub Actions."
    )

    $family = @(
        "Irina ha bisogno di piu attenzione questa settimana.",
        "Le bambine oggi sono da mamma Felicia.",
        "Con Adi ho discusso del weekend in famiglia.",
        "Julieta ha fatto i compiti con me.",
        "Vorrei organizzare una cena con tutta la famiglia."
    )

    $money = @(
        "Ho pagato 24 euro per la colazione con Irina, abbiamo diviso a meta.",
        "Adi mi deve 15 euro per il taxi.",
        "Ho speso 80 euro di spesa al supermercato.",
        "A pranzo abbiamo speso 60 euro in tre.",
        "Devo dare 20 euro a Irina per il regalo."
    )

    $mind = @(
        "Voglio migliorare la disciplina quotidiana.",
        "Devo ridurre il rumore operativo e concentrarmi sulle priorita vere.",
        "Oggi energia media, stress alto nel pomeriggio.",
        "La mia filosofia: piccoli step ogni giorno, costanza prima di tutto.",
        "Quando dormo poco divento piu impulsivo."
    )

    $all = New-Object System.Collections.Generic.List[string]
    $all.AddRange($base)

    $i = 0
    while ($all.Count -lt $TargetCount) {
        $bucket = switch ($i % 4) {
            0 { $work }
            1 { $family }
            2 { $money }
            default { $mind }
        }

        $text = $bucket[$i % $bucket.Count]
        $day = ($i % 28) + 1
        $all.Add("[$day] $text")
        $i++
    }

    return $all | Select-Object -First $TargetCount
}

function Wait-ReplayDrain {
    param([int]$MaxSeconds = 90)

    $deadline = (Get-Date).AddSeconds($MaxSeconds)
    while ((Get-Date) -lt $deadline) {
        $jobs = Invoke-Api -Method GET -Path "admin/feedback/replay-jobs?take=100"
        if (-not $jobs -or ($jobs | Where-Object { $_.status -in @("queued", "running") }).Count -eq 0) {
            return
        }

        Start-Sleep -Seconds 3
    }
}

Write-Host "Alignment loop start"
Write-Host "API: $ApiBaseUrl"
Write-Host "Target entries: $Count"
Write-Host "Max loops: $MaxLoops"
Write-Host "Output: $OutDir"

$runSummary = [ordered]@{
    startedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    apiBaseUrl = $ApiBaseUrl
    countTarget = $Count
    maxLoops = $MaxLoops
    reset = $false
    seeded = 0
    loops = @()
    final = @{}
}

if (-not $SkipReset) {
    Write-Host "Reset completo utente..."
    $resetResult = Invoke-Api -Method POST -Path "operations/reset/me?includeFeedback=true"
    $runSummary.reset = $true
    $runSummary.resetResult = $resetResult
}

$entries = New-SeedEntries -TargetCount $Count
Write-Host "Inserimento entry..."
for ($idx = 0; $idx -lt $entries.Count; $idx++) {
    $content = $entries[$idx]
    [void](Invoke-Api -Method POST -Path "entries" -Body @{ content = $content })
    $runSummary.seeded = $idx + 1

    if ($SeedDelayMs -gt 0) {
        Start-Sleep -Milliseconds $SeedDelayMs
    }
}

Write-Host "Attesa processamento iniziale..."
Start-Sleep -Seconds $PostSeedWaitSeconds

for ($loop = 1; $loop -le $MaxLoops; $loop++) {
    Write-Host "Loop $loop/$MaxLoops"
    $loopSummary = [ordered]@{
        loop = $loop
        normalized = $null
        reviewQueueCount = 0
        applied = 0
        warnings = @()
    }

    try {
        $normalize = Invoke-Api -Method POST -Path "operations/normalize/entities"
        $loopSummary.normalized = $normalize
    }
    catch {
        $loopSummary.warnings += "Normalize failed: $($_.Exception.Message)"
    }

    $queue = @()
    try {
        $queue = Invoke-Api -Method GET -Path "admin/review-queue?take=200"
    }
    catch {
        $loopSummary.warnings += "Review queue unavailable: $($_.Exception.Message)"
    }

    $queue = @($queue)
    $loopSummary.reviewQueueCount = $queue.Count

    if ($queue.Count -eq 0) {
        $runSummary.loops += $loopSummary
        Write-Host "Nessuna issue in review queue. Stop."
        break
    }

    $applied = 0
    foreach ($item in $queue) {
        if ($applied -ge $ApplyPerLoop) { break }
        if ([string]::IsNullOrWhiteSpace($item.suggestedTemplateId) -or [string]::IsNullOrWhiteSpace($item.suggestedPayloadJson)) {
            continue
        }

        try {
            $payload = $item.suggestedPayloadJson | ConvertFrom-Json
            $request = @{
                templateId = $item.suggestedTemplateId
                templatePayload = $payload
                scopeDefault = "USER"
                reason = "auto-alignment-loop-$loop"
            }

            [void](Invoke-Api -Method POST -Path "admin/feedback/cases/apply" -Body $request)
            $applied++
        }
        catch {
            $loopSummary.warnings += "Apply failed for $($item.issueType): $($_.Exception.Message)"
        }
    }

    $loopSummary.applied = $applied
    $runSummary.loops += $loopSummary

    if ($applied -eq 0) {
        Write-Host "Nessun feedback applicabile nel loop $loop. Stop."
        break
    }

    Write-Host "Replay drain..."
    Wait-ReplayDrain -MaxSeconds 90
    Start-Sleep -Seconds 4
}

Write-Host "Raccolta metriche finali..."
$dashboard = Invoke-Api -Method GET -Path "dashboard"
$nodes = Invoke-Api -Method GET -Path "nodes?limit=500"
$policy = $null
$finalQueue = @()

try {
    $policy = Invoke-Api -Method GET -Path "admin/policy/summary"
}
catch {
}

try {
    $finalQueue = @(Invoke-Api -Method GET -Path "admin/review-queue?take=200")
}
catch {
    $finalQueue = @()
}

$ambiguous = @($nodes.items | Where-Object { $_.resolutionState -eq "ambiguous" }).Count
$suppressedCandidates = @($nodes.items | Where-Object { $_.resolutionState -eq "suppressed_candidate" }).Count

$runSummary.final = [ordered]@{
    completedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    entries = $dashboard.stats.totalEntries
    concepts = $dashboard.stats.totalConcepts
    nodeCount = $nodes.totalCount
    ambiguousNodes = $ambiguous
    suppressedCandidates = $suppressedCandidates
    reviewQueueOpen = @($finalQueue).Count
    policySummary = $policy
}

$summaryPath = Join-Path $OutDir "summary.json"
$runSummary | ConvertTo-Json -Depth 20 | Out-File -FilePath $summaryPath -Encoding utf8

Write-Host ""
Write-Host "Alignment completed."
Write-Host "Summary: $summaryPath"
Write-Host "Final review queue open: $($runSummary.final.reviewQueueOpen)"
Write-Host "Ambiguous nodes: $($runSummary.final.ambiguousNodes)"
