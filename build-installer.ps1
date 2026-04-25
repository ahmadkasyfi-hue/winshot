# File: build-installer.ps1
#
# End-to-end installer build driver.
#
# Steps:
#   1. dotnet publish (Release, win-x64, single-file, self-contained)
#   2. iscc Installer\WinShot.iss
#   3. Print the absolute path to the resulting WinShotSetup.exe
#
# Usage (from repo root, in an elevated-capable PowerShell — admin not
# strictly required to *build*, only to *run* the installer):
#
#     .\build-installer.ps1
#     .\build-installer.ps1 -SkipPublish            # re-pack only
#     .\build-installer.ps1 -InnoSetupPath "C:\Tools\InnoSetup\iscc.exe"
#
# Requirements:
#   - .NET 8 SDK in PATH  (dotnet --version >= 8.0)
#   - Inno Setup 6+       (iscc.exe)
#
# Why PowerShell: Windows-native, no extra tooling, supports strict error
# handling via $ErrorActionPreference and exit-code checks that survive
# tool failures cleanly.

[CmdletBinding()]
param(
    [string] $Configuration  = "Release",
    [string] $Runtime        = "win-x64",
    [string] $PublishProfile = "SingleFile",
    [string] $InnoSetupPath  = "",
    [switch] $SkipPublish,
    [switch] $SkipInstaller
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RepoRoot     = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectPath  = Join-Path $RepoRoot "WinShot\WinShot.csproj"
$InstallerIss = Join-Path $RepoRoot "Installer\WinShot.iss"
$DistDir      = Join-Path $RepoRoot "dist"
$PublishDir   = Join-Path $RepoRoot "WinShot\bin\$Configuration\net8.0-windows\publish\$Runtime"

function Write-Section([string] $Message) {
    Write-Host ""
    Write-Host ("=" * 72) -ForegroundColor DarkCyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host ("=" * 72) -ForegroundColor DarkCyan
}

function Find-InnoSetup() {
    if ($InnoSetupPath -and (Test-Path $InnoSetupPath)) { return $InnoSetupPath }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
    )
    foreach ($p in $candidates) {
        if ($p -and (Test-Path $p)) { return $p }
    }

    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    return $null
}

# --------------------------------------------------------------- publish -----

if (-not $SkipPublish) {
    Write-Section "dotnet publish ($Configuration / $Runtime / $PublishProfile)"

    # Clean to avoid stale files from prior profiles (framework-dependent vs
    # self-contained output can otherwise collide).
    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force
    }

    & dotnet publish $ProjectPath `
        --configuration $Configuration `
        --runtime       $Runtime `
        "/p:PublishProfile=$PublishProfile"

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path (Join-Path $PublishDir "WinShot.exe"))) {
        throw "Publish completed but WinShot.exe was not produced at $PublishDir"
    }

    $exeSize = (Get-Item (Join-Path $PublishDir "WinShot.exe")).Length / 1MB
    Write-Host ("WinShot.exe produced ({0:N1} MB)" -f $exeSize) -ForegroundColor Green
} else {
    Write-Host "Skipping publish (using existing $PublishDir)" -ForegroundColor Yellow
}

# ------------------------------------------------------------- installer -----

if ($SkipInstaller) {
    Write-Host "Skipping installer build." -ForegroundColor Yellow
    return
}

Write-Section "Inno Setup compile"

$iscc = Find-InnoSetup
if (-not $iscc) {
    throw @"
Could not locate Inno Setup (iscc.exe).
Install from https://jrsoftware.org/isdl.php (v6+), or pass -InnoSetupPath.
"@
}
Write-Host "Using: $iscc"

if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir | Out-Null
}

& $iscc $InstallerIss
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compile failed with exit code $LASTEXITCODE"
}

$setupExe = Join-Path $DistDir "WinShotSetup.exe"
if (-not (Test-Path $setupExe)) {
    throw "Inno Setup reported success but $setupExe was not produced."
}

$setupMb = (Get-Item $setupExe).Length / 1MB
Write-Section "Done"
Write-Host ("Installer: {0}" -f $setupExe)           -ForegroundColor Green
Write-Host ("Size:      {0:N1} MB" -f $setupMb)       -ForegroundColor Green
Write-Host ""
Write-Host "Run it to install WinShot:"              -ForegroundColor Gray
Write-Host "    & `"$setupExe`""                     -ForegroundColor White
