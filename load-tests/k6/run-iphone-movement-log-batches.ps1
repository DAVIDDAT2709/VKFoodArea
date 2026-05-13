param(
    [string]$ApiBaseUrl = "http://localhost:5216",
    [string[]]$DeviceCounts = @("100", "200"),
    [int]$Vus = 50,
    [string]$MaxDuration = "2m",
    [string]$K6Path = "k6"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$testScript = Join-Path $PSScriptRoot "iphone-movement-log-load-test.js"
$artifactDir = Join-Path $repoRoot "artifacts\load-tests"

New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

function Convert-DeviceCounts {
    param([string[]]$RawValues)

    $values = @()

    foreach ($raw in $RawValues) {
        foreach ($part in ($raw -split "[,\s;]+")) {
            $trimmed = $part.Trim()
            if ([string]::IsNullOrWhiteSpace($trimmed)) {
                continue
            }

            $parsed = 0
            if (-not [int]::TryParse($trimmed, [ref]$parsed) -or $parsed -le 0) {
                throw "Invalid device count '$trimmed'. Use values like '100,200' or '100 200'."
            }

            $values += $parsed
        }
    }

    if ($values.Count -eq 0) {
        throw "At least one device count is required."
    }

    return $values
}

$normalizedDeviceCounts = Convert-DeviceCounts -RawValues $DeviceCounts

foreach ($count in $normalizedDeviceCounts) {
    $env:API_BASE_URL = $ApiBaseUrl
    $env:DEVICES = [string]$count
    $env:VUS = [string]([Math]::Min($Vus, $count))
    $env:MAX_DURATION = $MaxDuration
    $env:SUMMARY_FILE = "artifacts/load-tests/iphone-movement-log-$count-summary.json"

    Write-Host "Running iPhone virtual movement-log load test: DEVICES=$env:DEVICES VUS=$env:VUS MAX_DURATION=$env:MAX_DURATION API_BASE_URL=$env:API_BASE_URL"
    & $K6Path run $testScript

    if ($LASTEXITCODE -ne 0) {
        throw "k6 failed for $count virtual iPhones."
    }
}

Write-Host "Done. Summaries were written under artifacts/load-tests."
