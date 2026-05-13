param(
    [string]$ApiBaseUrl = "http://localhost:5216",
    [int]$AndroidDevices = 100,
    [int]$IphoneDevices = 100,
    [int]$AndroidVus = 50,
    [int]$IphoneVus = 50,
    [string]$MaxDuration = "2m",
    [string]$K6Path = "k6"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$testScript = Join-Path $PSScriptRoot "mobile-movement-log-load-test.js"
$artifactDir = Join-Path $repoRoot "artifacts\load-tests"

New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$env:API_BASE_URL = $ApiBaseUrl
$env:ANDROID_DEVICES = [string]$AndroidDevices
$env:IPHONE_DEVICES = [string]$IphoneDevices
$env:ANDROID_VUS = [string]([Math]::Min($AndroidVus, [Math]::Max($AndroidDevices, 1)))
$env:IPHONE_VUS = [string]([Math]::Min($IphoneVus, [Math]::Max($IphoneDevices, 1)))
$env:MAX_DURATION = $MaxDuration
$env:SUMMARY_FILE = "artifacts/load-tests/mobile-movement-log-android-$AndroidDevices-iphone-$IphoneDevices-summary.json"

Write-Host "Running mobile movement-log load test: ANDROID_DEVICES=$env:ANDROID_DEVICES IPHONE_DEVICES=$env:IPHONE_DEVICES ANDROID_VUS=$env:ANDROID_VUS IPHONE_VUS=$env:IPHONE_VUS MAX_DURATION=$env:MAX_DURATION API_BASE_URL=$env:API_BASE_URL"
& $K6Path run $testScript

if ($LASTEXITCODE -ne 0) {
    throw "k6 failed for Android=$AndroidDevices and iPhone=$IphoneDevices virtual devices."
}

Write-Host "Done. Summary was written to $env:SUMMARY_FILE."
