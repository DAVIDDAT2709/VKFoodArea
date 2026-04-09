param(
    [string]$AvdName = "pixel_5_-_api_35_0",
    [string]$SdkRoot = "",
    [switch]$SkipBuild,
    [switch]$LaunchOnly
)

$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectDir "VKFoodArea.csproj"
$packageName = "com.companyname.vkfoodarea"
$apkOutputDir = Join-Path $projectDir "bin\Debug\net10.0-android"

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = $projectDir

function Assert-PathExists {
    param(
        [string]$PathValue,
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $PathValue)) {
        throw "$Label not found: $PathValue"
    }
}

function Resolve-SdkRoot {
    param([string]$RequestedSdkRoot)

    $candidates = @(
        $RequestedSdkRoot,
        $env:ANDROID_SDK_ROOT,
        $env:ANDROID_HOME,
        (Join-Path $env:LOCALAPPDATA "Android\Sdk"),
        "D:\Android\Sdk"
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($candidate in $candidates) {
        $adbCandidate = Join-Path $candidate "platform-tools\adb.exe"
        $emulatorCandidate = Join-Path $candidate "emulator\emulator.exe"

        if ((Test-Path -LiteralPath $adbCandidate) -and (Test-Path -LiteralPath $emulatorCandidate)) {
            return $candidate
        }
    }

    throw "Android SDK not found. Pass -SdkRoot or set ANDROID_SDK_ROOT."
}

function Get-RunningEmulatorId {
    $lines = & $adbPath devices

    foreach ($line in $lines) {
        if ($line -match "^(emulator-\d+)\s+device$") {
            return $Matches[1]
        }
    }

    return $null
}

function Wait-ForBootCompleted {
    param([string]$DeviceId)

    & $adbPath -s $DeviceId wait-for-device | Out-Null

    $deadline = [DateTime]::UtcNow.AddMinutes(4)

    do {
        Start-Sleep -Seconds 2
        $bootCompleted = (& $adbPath -s $DeviceId shell getprop sys.boot_completed | Out-String).Trim()
    } until ($bootCompleted -eq "1" -or [DateTime]::UtcNow -ge $deadline)

    if ($bootCompleted -ne "1") {
        throw "Emulator did not finish booting in time."
    }
}

function Ensure-EmulatorRunning {
    $deviceId = Get-RunningEmulatorId

    if ($deviceId) {
        Write-Host "Using emulator $deviceId"
        return $deviceId
    }

    Write-Host "Starting emulator $AvdName..."
    Start-Process -FilePath $emulatorPath -ArgumentList @("-avd", $AvdName) | Out-Null

    $deadline = [DateTime]::UtcNow.AddMinutes(3)

    do {
        Start-Sleep -Seconds 3
        $deviceId = Get-RunningEmulatorId
    } until ($deviceId -or [DateTime]::UtcNow -ge $deadline)

    if (-not $deviceId) {
        throw "No emulator appeared in adb devices."
    }

    Wait-ForBootCompleted -DeviceId $deviceId
    return $deviceId
}

function Build-Project {
    Write-Host "Building MAUI Android project..."
    & dotnet build $projectFile -c Debug -f net10.0-android

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed."
    }
}

function Get-SignedApkPath {
    Assert-PathExists -PathValue $apkOutputDir -Label "APK output directory"

    $apk = Get-ChildItem -Path $apkOutputDir -Filter "*-Signed.apk" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $apk) {
        throw "No *-Signed.apk file found in $apkOutputDir"
    }

    return $apk.FullName
}

function Install-Apk {
    param(
        [string]$DeviceId,
        [string]$ApkPath
    )

    Write-Host "Installing APK on $DeviceId..."
    & $adbPath -s $DeviceId install -r $ApkPath

    if ($LASTEXITCODE -ne 0) {
        throw "APK install failed."
    }
}

function Resolve-LauncherActivity {
    param([string]$DeviceId)

    $resolved = & $adbPath -s $DeviceId shell cmd package resolve-activity --brief $packageName

    if ($LASTEXITCODE -ne 0) {
        throw "Could not resolve launcher activity for $packageName."
    }

    $component = $resolved |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -match "^[^=]+/.+$" } |
        Select-Object -Last 1

    if ([string]::IsNullOrWhiteSpace($component)) {
        throw "No launchable activity found for $packageName."
    }

    return $component
}

function Launch-App {
    param([string]$DeviceId)

    $component = Resolve-LauncherActivity -DeviceId $DeviceId

    Write-Host "Launching $component on $DeviceId..."
    & $adbPath -s $DeviceId shell am start -W -n $component | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "App launch failed."
    }
}

if ([string]::IsNullOrWhiteSpace($SdkRoot)) {
    $SdkRoot = Resolve-SdkRoot -RequestedSdkRoot $SdkRoot
}

$adbPath = Join-Path $SdkRoot "platform-tools\adb.exe"
$emulatorPath = Join-Path $SdkRoot "emulator\emulator.exe"

Assert-PathExists -PathValue $projectFile -Label "Project file"
Assert-PathExists -PathValue $adbPath -Label "adb"
Assert-PathExists -PathValue $emulatorPath -Label "emulator"

$deviceId = Ensure-EmulatorRunning

if (-not $LaunchOnly) {
    if (-not $SkipBuild) {
        Build-Project
    }

    $apkPath = Get-SignedApkPath
    Install-Apk -DeviceId $deviceId -ApkPath $apkPath
}

Launch-App -DeviceId $deviceId

Write-Host ""
Write-Host "Done. Device: $deviceId"
