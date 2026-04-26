param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$QrCode = "poi:oc-vu",
    [string]$UserKey = "demo-smoke-test"
)

$ErrorActionPreference = "Stop"

function Join-Url {
    param([string]$Base, [string]$Path)

    return "$($Base.TrimEnd('/'))/$($Path.TrimStart('/'))"
}

$encodedQrCode = [System.Uri]::EscapeDataString($QrCode)
$resolve = Invoke-RestMethod -Method Get -Uri (Join-Url $BaseUrl "api/resolve-qr?code=$encodedQrCode")

if ($null -eq $resolve.poi) {
    throw "QR '$QrCode' did not resolve to a POI."
}

$latitude = [double]$resolve.poi.latitude
$longitude = [double]$resolve.poi.longitude
$now = [DateTime]::UtcNow.ToString("o")

$movementPayload = @{
    userKey = $UserKey
    latitude = $latitude
    longitude = $longitude
    accuracyMeters = 9
    source = "foreground"
    recordedAt = $now
} | ConvertTo-Json

$movement = Invoke-RestMethod `
    -Method Post `
    -Uri (Join-Url $BaseUrl "api/movement-logs") `
    -ContentType "application/json; charset=utf-8" `
    -Body $movementPayload

$historyPayload = @{
    poiId = 0
    poiName = $resolve.poi.name
    qrCode = $QrCode
    userKey = $UserKey
    language = "vi"
    triggerSource = "qr"
    mode = "tts"
    playedAt = $now
    durationSeconds = 15
    latitude = $latitude
    longitude = $longitude
} | ConvertTo-Json

$history = Invoke-RestMethod `
    -Method Post `
    -Uri (Join-Url $BaseUrl "api/narration-histories") `
    -ContentType "application/json; charset=utf-8" `
    -Body $historyPayload

$recentHistory = Invoke-RestMethod `
    -Method Get `
    -Uri (Join-Url $BaseUrl "api/narration-histories?top=10&userKey=$([System.Uri]::EscapeDataString($UserKey))")

if (-not ($recentHistory | Where-Object { $_.triggerSource -eq "qr" -and $_.id -eq $history.id })) {
    throw "Created narration history was not returned by the recent history API."
}

[PSCustomObject]@{
    status = "ok"
    resolvedQr = $QrCode
    poiName = $resolve.poi.name
    movementLogId = $movement.id
    narrationHistoryId = $history.id
    baseUrl = $BaseUrl
}
