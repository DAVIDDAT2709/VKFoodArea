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

$appUserPayload = @{
    userKey = $UserKey
    username = $UserKey
    email = "$UserKey@vkfoodarea.local"
    fullName = "Smoke Test User"
    narrationLanguage = "vi"
    narrationPlaybackMode = "TTS"
    role = "User"
    isActive = $true
} | ConvertTo-Json

$appUser = Invoke-RestMethod `
    -Method Post `
    -Uri (Join-Url $BaseUrl "api/app-users/sync") `
    -ContentType "application/json; charset=utf-8" `
    -Body $appUserPayload

$appUserStatus = Invoke-RestMethod `
    -Method Get `
    -Uri (Join-Url $BaseUrl "api/app-users/status?userKey=$([System.Uri]::EscapeDataString($UserKey))")

if (-not $appUserStatus.isKnown) {
    throw "App user status did not return a known user after sync."
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
    appUserRole = $appUserStatus.role
    movementLogId = $movement.id
    narrationHistoryId = $history.id
    baseUrl = $BaseUrl
}
