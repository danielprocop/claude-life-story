param(
    [string]$SsmParameterName = "/diario-intelligente/prod/default-connection",
    [string]$Region = "eu-west-1",
    [string]$OutDir = "",
    [Guid]$UserId = [Guid]::Empty,
    [switch]$IncludeEntryContent,
    [switch]$NoExport
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\\..\\..")

Write-Host "Fetching Aurora connection string from SSM: $SsmParameterName ($Region)"
$connectionString = aws ssm get-parameter `
    --name $SsmParameterName `
    --with-decryption `
    --region $Region `
    --query Parameter.Value `
    --output text

if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw "Empty connection string from SSM parameter $SsmParameterName"
}

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
    $OutDir = Join-Path $repoRoot ".runlogs\\data-quality\\$timestamp"
}

$env:DIARIO_CONNECTION_STRING = $connectionString

$argsList = @(
    "run",
    "--project", (Join-Path $repoRoot "backend\\DiarioIntelligente.OpsCli"),
    "--",
    "audit",
    "--out", $OutDir
)

if ($UserId -ne [Guid]::Empty) {
    $argsList += @("--user", $UserId.ToString())
}

if ($IncludeEntryContent) {
    $argsList += "--include-entry-content"
}

if ($NoExport) {
    $argsList += "--no-export"
}

Write-Host "Running audit..."
dotnet @argsList

Write-Host ""
Write-Host "Audit output:"
Write-Host "  $OutDir"
Write-Host "Report:"
Write-Host "  $(Join-Path $OutDir \"audit\\report.md\")"

