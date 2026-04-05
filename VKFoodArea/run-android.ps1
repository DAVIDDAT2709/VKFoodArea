param(
    [string]$AvdName = "pixel_5_-_api_35_0",
    [string]$SdkRoot = "D:\Android\Sdk",
    [switch]$SkipBuild,
    [switch]$LaunchOnly
)

$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectDir "VKFoodArea.csproj"
$adbPath = Join-Path $SdkRoot "platform-tools\adb.exe"
$emulatorPath = Join-Path $SdkRoot "emulator\emulator.exe"
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

function Launch-App {
    param([string]$DeviceId)

    Write-Host "Launching $packageName on $DeviceId..."
    & $adbPath -s $DeviceId shell monkey -p $packageName -c android.intent.category.LAUNCHER 1 | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "App launch failed."
    }
}

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
