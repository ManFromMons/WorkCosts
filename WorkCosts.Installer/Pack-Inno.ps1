#requires -Version 5.1
<#
.SYNOPSIS
  Publishes Will I DIY? (unpackaged, self-contained) and compiles an Inno Setup installer.

.EXAMPLE
  .\WorkCosts.Installer\Pack-Inno.ps1
  .\WorkCosts.Installer\Pack-Inno.ps1 -Runtime x64 -Version 1.0.0
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86', 'arm64')]
    [string] $Runtime = 'x64',

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $Version = '1.0.0',

    [string] $IsccPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "Version must be three- or four-part (for example 1.0.0). Got: $Version"
}

$installerRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent $installerRoot
$appProject = Join-Path $repoRoot 'WorkCosts\WorkCosts.csproj'
$issPath = Join-Path $installerRoot 'WillIDIY.iss'
$rid = "win-$Runtime"
$platform = if ($Runtime -eq 'arm64') { 'ARM64' } else { $Runtime }
$publishDir = Join-Path $installerRoot "publish\$rid"
$outputDir = Join-Path $installerRoot 'Output'
$setupName = "WillIDIY-Setup-$Version-$Runtime"

function Get-IsccPath {
    if ($IsccPath) {
        if (-not (Test-Path -LiteralPath $IsccPath)) {
            throw "ISCC.exe not found at $IsccPath"
        }
        return (Resolve-Path -LiteralPath $IsccPath).Path
    }

    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    $uninstallRoots = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
    )
    foreach ($root in $uninstallRoots) {
        if (-not (Test-Path $root)) {
            continue
        }
        foreach ($key in Get-ChildItem -Path $root -ErrorAction SilentlyContinue) {
            $props = Get-ItemProperty -Path $key.PSPath -ErrorAction SilentlyContinue
            if ($null -eq $props -or $props.DisplayName -notmatch 'Inno Setup') {
                continue
            }
            foreach ($dir in @($props.InstallLocation, $(if ($props.DisplayIcon) { Split-Path -Parent $props.DisplayIcon }))) {
                if (-not $dir) {
                    continue
                }
                $iscc = Join-Path $dir 'ISCC.exe'
                if (Test-Path -LiteralPath $iscc) {
                    return $iscc
                }
            }
        }
    }

    throw @"
Inno Setup compiler (ISCC.exe) was not found. Install Inno Setup 6.3 or later, or pass -IsccPath.
https://jrsoftware.org/isinfo.php
"@
}

if (-not (Test-Path -LiteralPath $issPath)) {
    throw "Missing Inno script: $issPath"
}

$iscc = Get-IsccPath
Write-Host "Using Inno compiler: $iscc"

Write-Host "Publishing $rid ($Configuration)..."
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

dotnet publish $appProject `
    -c $Configuration `
    -r $rid `
    --self-contained true `
    -p:Platform=$platform `
    -p:Version=$Version `
    -p:PublishTrimmed=false `
    -p:WindowsAppSDKSelfContained=true `
    -p:WindowsPackageType=None `
    -p:EnableMsixTooling=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exe = Join-Path $publishDir 'WillIDIY.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Published output did not contain WillIDIY.exe (looked in $publishDir)."
}

if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# ISCC #define values are happier with forward slashes.
$publishDirDefine = ($publishDir -replace '\\', '/')

Write-Host "Compiling installer $setupName.exe..."
& $iscc `
    /Q `
    /O"$outputDir" `
    /F"$setupName" `
    "/DAppVersion=$Version" `
    "/DAppArch=$Runtime" `
    "/DPublishDir=$publishDirDefine" `
    $issPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE."
}

$setupPath = Join-Path $outputDir "$setupName.exe"
if (-not (Test-Path -LiteralPath $setupPath)) {
    throw "ISCC did not produce $setupPath."
}

Write-Host ""
Write-Host "Installer: $setupPath"
Write-Host ""
Write-Host "This is a normal Win32 setup (no sideloading, no publisher certificate)."
Write-Host "User data stays in %LOCALAPPDATA%\WorkCosts across upgrades and uninstalls."
