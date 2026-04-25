# File: build-msix.ps1
#
# End-to-end MSIX build driver for WinShot.
#
# Pipeline:
#   1. dotnet publish (Release, win-x64, self-contained single-file)
#   2. Stage publish output + Package.appxmanifest + Images\ into a temp dir
#   3. Patch the manifest's <Identity Version/> to match the .csproj version
#   4. MakeAppx.exe pack  →  dist\WinShot_0.2.0_x64.msix
#   5. (optional) Generate a self-signed cert if one doesn't exist
#   6. signtool.exe sign  →  signed .msix
#   7. Print the final paths and tell the user how to install the cert
#
# MSIX is FAR more finicky than a classic installer. Things that trip people:
#   * The package is signed with a cert whose Subject (CN=...) MUST match
#     <Identity Publisher="..."/>. If they differ by even a space, MakeAppx
#     will refuse to validate and signtool will produce an unverifiable file.
#   * Windows won't install a signed MSIX unless the signing cert is in
#     Trusted People (or Trusted Root) on the machine. We document how to
#     do that at the end of the run.
#   * Package identity (Name + Publisher + Version) is case-sensitive and
#     immutable across releases — bumping the Publisher would appear as a
#     side-by-side install instead of an upgrade.
#
# Usage:
#     .\build-msix.ps1
#     .\build-msix.ps1 -SkipPublish                      # just re-pack + sign
#     .\build-msix.ps1 -SkipSigning                      # unsigned for store
#     .\build-msix.ps1 -CertificateThumbprint <THUMB>    # use existing cert
#     .\build-msix.ps1 -CertSubject "CN=MyCompany"       # different CN
#
# The store submission path wants UNSIGNED packages (Microsoft re-signs),
# so use -SkipSigning if you're uploading to Partner Center.

[CmdletBinding()]
param(
    [string] $Configuration           = "Release",
    [string] $Runtime                 = "win-x64",
    [string] $PublishProfile          = "SingleFile",
    [string] $CertSubject             = "CN=WinShot.Dev",
    [string] $CertificateThumbprint   = "",
    [string] $CertPassword            = "WinShot.Dev.LocalOnly",
    [string] $WindowsSdkPath          = "",
    [switch] $SkipPublish,
    [switch] $SkipSigning,
    [switch] $GenerateCert
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RepoRoot        = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectPath     = Join-Path $RepoRoot "WinShot\WinShot.csproj"
$PackagingDir    = Join-Path $RepoRoot "Packaging"
$ManifestPath    = Join-Path $PackagingDir "Package.appxmanifest"
$ImagesDir       = Join-Path $PackagingDir "Images"
$DistDir         = Join-Path $RepoRoot "dist"
$PublishDir      = Join-Path $RepoRoot "WinShot\bin\$Configuration\net8.0-windows\publish\$Runtime"
$StageDir        = Join-Path $env:TEMP  "WinShot.MsixStage"
$CertOutputPath  = Join-Path $PackagingDir "WinShot.Dev.pfx"

function Write-Section([string] $Message) {
    Write-Host ""
    Write-Host ("=" * 72) -ForegroundColor DarkCyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host ("=" * 72) -ForegroundColor DarkCyan
}

# ----------------------------------------------- Windows SDK tool discovery --

function Find-WindowsSdkTool([string] $ToolName) {
    # Manual override wins.
    if ($WindowsSdkPath) {
        $candidate = Join-Path $WindowsSdkPath "$ToolName.exe"
        if (Test-Path $candidate) { return $candidate }
    }

    # PATH (Developer Command Prompt).
    $cmd = Get-Command "$ToolName.exe" -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    # Search standard SDK install roots. We pick the newest version found.
    $roots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "${env:ProgramFiles}\Windows Kits\10\bin"
    ) | Where-Object { $_ -and (Test-Path $_) }

    foreach ($root in $roots) {
        $versionedDirs = Get-ChildItem -Path $root -Directory `
            | Where-Object { $_.Name -match '^10\.0\.\d+\.\d+$' } `
            | Sort-Object Name -Descending

        foreach ($v in $versionedDirs) {
            $candidate = Join-Path $v.FullName "x64\$ToolName.exe"
            if (Test-Path $candidate) { return $candidate }
        }
    }
    return $null
}

# -------------------------------------------------- csproj version reading --

function Get-CsprojVersion {
    [xml]$csproj = Get-Content $ProjectPath
    $node = $csproj.Project.PropertyGroup.Version
    if (-not $node) { throw "Could not find <Version> in $ProjectPath" }
    # csproj Version is 3-part (e.g. 0.2.0); MSIX requires 4-part.
    $v = "$node"
    $parts = $v.Split('.')
    while ($parts.Count -lt 4) { $parts += '0' }
    return ($parts[0..3] -join '.')
}

# ---------------------------------------------------------------- publish --

if (-not $SkipPublish) {
    Write-Section "dotnet publish ($Configuration / $Runtime / $PublishProfile)"

    if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

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
} else {
    Write-Host "Skipping publish." -ForegroundColor Yellow
}

# ------------------------------------------------------------- stage dir ---

Write-Section "Stage MSIX payload"

if (Test-Path $StageDir) { Remove-Item $StageDir -Recurse -Force }
New-Item -ItemType Directory -Path $StageDir | Out-Null

# Payload = published output + manifest + images.
Copy-Item -Path (Join-Path $PublishDir "*") -Destination $StageDir -Recurse
Copy-Item -Path $ManifestPath -Destination (Join-Path $StageDir "AppxManifest.xml")

$stageImages = Join-Path $StageDir "Images"
New-Item -ItemType Directory -Path $stageImages | Out-Null
Copy-Item -Path (Join-Path $ImagesDir "*") -Destination $stageImages

# Patch <Identity Version="..."/> so it matches the csproj version without
# requiring a manual edit on every release bump. Also normalize Publisher
# to -CertSubject, so a user passing a custom subject doesn't desync.
$version = Get-CsprojVersion
Write-Host "  Package version: $version"
Write-Host "  Publisher:       $CertSubject"

[xml]$manifest = Get-Content (Join-Path $StageDir "AppxManifest.xml")
$manifest.Package.Identity.Version   = $version
$manifest.Package.Identity.Publisher = $CertSubject
$manifest.Save((Join-Path $StageDir "AppxManifest.xml"))

# ----------------------------------------------------------- pack .msix ---

Write-Section "MakeAppx pack"

$makeAppx = Find-WindowsSdkTool "MakeAppx"
if (-not $makeAppx) {
    throw @"
Could not locate MakeAppx.exe.
Install the Windows 10/11 SDK: https://developer.microsoft.com/windows/downloads/windows-sdk/
Or pass -WindowsSdkPath pointing at a folder that contains MakeAppx.exe.
"@
}
Write-Host "  Using: $makeAppx"

if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Path $DistDir | Out-Null }

$msixName = "WinShot_${version}_x64.msix"
$msixPath = Join-Path $DistDir $msixName
if (Test-Path $msixPath) { Remove-Item $msixPath -Force }

& $makeAppx pack /d $StageDir /p $msixPath /o
if ($LASTEXITCODE -ne 0) {
    throw "MakeAppx pack failed with exit code $LASTEXITCODE"
}
Write-Host "  Packed: $msixPath" -ForegroundColor Green

# ---------------------------------------------------------- sign .msix ---

if ($SkipSigning) {
    Write-Host ""
    Write-Host "Skipping signing (-SkipSigning). Result is UNSIGNED and will not install." -ForegroundColor Yellow
    Write-Host "(This is the correct mode for Microsoft Store / Partner Center uploads.)" -ForegroundColor Yellow
    return
}

Write-Section "Sign MSIX"

# Resolve which cert to use.
$cert = $null
if ($CertificateThumbprint) {
    $cert = Get-Item "Cert:\CurrentUser\My\$CertificateThumbprint" -ErrorAction SilentlyContinue
    if (-not $cert) { throw "No certificate with thumbprint $CertificateThumbprint in CurrentUser\My" }
} else {
    $cert = Get-ChildItem Cert:\CurrentUser\My `
        | Where-Object { $_.Subject -eq $CertSubject -and $_.HasPrivateKey } `
        | Sort-Object NotAfter -Descending `
        | Select-Object -First 1
}

if (-not $cert) {
    if (-not $GenerateCert) {
        throw @"
No code-signing cert found in CurrentUser\My with Subject '$CertSubject'.
Re-run with -GenerateCert to create a self-signed one (for sideloading only).
"@
    }

    Write-Host "  Generating self-signed cert Subject=$CertSubject ..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $CertSubject `
        -KeyUsage DigitalSignature `
        -FriendlyName "WinShot dev signing cert" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3",
                         "2.5.29.19={text}")

    # Export .pfx so the user can install it on other machines (or CI).
    $pwd = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
    Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" `
                          -FilePath $CertOutputPath `
                          -Password $pwd | Out-Null
    Write-Host "  Exported PFX → $CertOutputPath  (password: $CertPassword)" -ForegroundColor Yellow
}

Write-Host "  Cert: $($cert.Subject)  [$($cert.Thumbprint)]"

$signtool = Find-WindowsSdkTool "signtool"
if (-not $signtool) {
    throw "Could not locate signtool.exe in the Windows SDK."
}

# /fd SHA256 is required for MSIX; /a lets signtool pick from the cert store.
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint `
    /tr http://timestamp.digicert.com /td SHA256 $msixPath
if ($LASTEXITCODE -ne 0) {
    throw "signtool sign failed with exit code $LASTEXITCODE"
}

Write-Section "Done"
$msixSize = (Get-Item $msixPath).Length / 1MB
Write-Host ("MSIX:      {0}" -f $msixPath)            -ForegroundColor Green
Write-Host ("Size:      {0:N1} MB" -f $msixSize)      -ForegroundColor Green
Write-Host ""
Write-Host "To install this .msix on a dev machine:"  -ForegroundColor Gray
Write-Host "  1. Trust the signing cert (one-time):"  -ForegroundColor Gray
Write-Host "       Import-Certificate -FilePath '$CertOutputPath' \\" -ForegroundColor White
Write-Host "         -CertStoreLocation Cert:\LocalMachine\TrustedPeople" -ForegroundColor White
Write-Host "     (or double-click the .pfx and import to Local Machine\Trusted People)" -ForegroundColor Gray
Write-Host "  2. Install the package:"                 -ForegroundColor Gray
Write-Host "       Add-AppxPackage '$msixPath'"        -ForegroundColor White
Write-Host "  3. Launch from Start Menu, or run 'winshot.exe' from the alias." -ForegroundColor Gray
