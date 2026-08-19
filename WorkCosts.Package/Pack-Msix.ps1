#requires -Version 5.1
<#
.SYNOPSIS
  Publishes Will I DIY? and packs a signed sideload MSIX (no Visual Studio required).

.EXAMPLE
  .\WorkCosts.Package\Pack-Msix.ps1
  .\WorkCosts.Package\Pack-Msix.ps1 -Runtime x64 -Version 1.0.0.0
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86', 'arm64')]
    [string] $Runtime = 'x64',

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $Version = '1.0.0.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Version must be four-part (for example 1.0.0.0). Got: $Version"
}

$packageRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent $packageRoot
$appProject = Join-Path $repoRoot 'WorkCosts\WorkCosts.csproj'
$manifestPath = Join-Path $packageRoot 'Package.appxmanifest'
$assetsRoot = Join-Path $repoRoot 'WorkCosts\Assets'
$rid = "win-$Runtime"
$platform = if ($Runtime -eq 'arm64') { 'ARM64' } else { $Runtime }
$outRoot = Join-Path $packageRoot "AppPackages\$Configuration\$rid"
$publishDir = Join-Path $outRoot 'publish'
$stageDir = Join-Path $outRoot 'msix-stage'
$msixPath = Join-Path $outRoot "WillIDIY_$Version`_$Runtime.msix"
$cerPath = Join-Path $outRoot 'WillIDIY.cer'

function Get-KitTool([string] $name) {
    $kitsBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (-not (Test-Path $kitsBin)) {
        throw "Windows SDK not found at $kitsBin. Install the Windows 10/11 SDK (MakeAppx / SignTool)."
    }

    $kitTools = Get-ChildItem -Path $kitsBin -Recurse -Filter $name -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -eq 'x64' -and $_.Directory.Parent.Name -match '^\d+\.\d+' } |
        Sort-Object { $_.Directory.Parent.Name } -Descending
    $tool = $kitTools | Select-Object -First 1
    if ($null -eq $tool) {
        throw "Could not find $name under $kitsBin."
    }
    return $tool.FullName
}

function Copy-PackageImage {
    param(
        [string] $SourceName,
        [string] $DestName
    )
    $source = Join-Path $assetsRoot $SourceName
    if (-not (Test-Path $source)) {
        throw "Missing package image: $source"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $stageDir "Images\$DestName") -Force
}

Write-Host "Publishing $rid ($Configuration)..."
if (Test-Path $outRoot) {
    Remove-Item -LiteralPath $outRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

dotnet publish $appProject `
    -c $Configuration `
    -r $rid `
    --self-contained true `
    -p:Platform=$platform `
    -p:PublishTrimmed=false `
    -p:WindowsAppSDKSelfContained=true `
    -p:WindowsPackageType=None `
    -p:EnableMsixTooling=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host 'Staging MSIX layout...'
New-Item -ItemType Directory -Path (Join-Path $stageDir 'Images') -Force | Out-Null
Get-ChildItem -LiteralPath $publishDir -Force | ForEach-Object {
    if ($_.Extension -in '.pdb', '.xml') {
        return
    }
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $stageDir $_.Name) -Recurse -Force
}

Copy-PackageImage 'StoreLogo.png' 'StoreLogo.png'
Copy-PackageImage 'SplashScreen.scale-200.png' 'SplashScreen.png'
Copy-PackageImage 'Square150x150Logo.scale-200.png' 'Square150x150Logo.png'
Copy-PackageImage 'Square44x44Logo.scale-200.png' 'Square44x44Logo.png'
Copy-PackageImage 'Wide310x150Logo.scale-200.png' 'Wide310x150Logo.png'
Copy-PackageImage 'LockScreenLogo.scale-200.png' 'LockScreenLogo.png'
Copy-PackageImage 'Square44x44Logo.targetsize-24_altform-unplated.png' 'Square44x44Logo.targetsize-24_altform-unplated.png'
Copy-PackageImage 'Square44x44Logo.targetsize-48_altform-lightunplated.png' 'Square44x44Logo.targetsize-48_altform-lightunplated.png'

$manifest = [xml](Get-Content -LiteralPath $manifestPath)
$manifest.Package.Identity.Version = $Version
$stagedManifest = Join-Path $stageDir 'AppxManifest.xml'
$manifest.Save($stagedManifest)

$exe = Join-Path $stageDir 'WillIDIY.exe'
if (-not (Test-Path $exe)) {
    throw "Published output did not contain WillIDIY.exe (looked in $stageDir)."
}

$makeappx = Get-KitTool 'makeappx.exe'
$signtool = Get-KitTool 'signtool.exe'

Write-Host "Packing $msixPath..."
& $makeappx pack /o /d $stageDir /p $msixPath
if ($LASTEXITCODE -ne 0) {
    throw "makeappx failed with exit code $LASTEXITCODE."
}

$subject = 'CN=WillIDIY'
$cert = Get-ChildItem -Path 'Cert:\CurrentUser\My' |
    Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($null -eq $cert) {
    Write-Host "Creating self-signed sideload certificate ($subject)..."
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $subject `
        -FriendlyName 'Will I DIY? sideload' `
        -KeyUsage DigitalSignature `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}

Write-Host "Signing with $($cert.Thumbprint)..."
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $msixPath
if ($LASTEXITCODE -ne 0) {
    throw "signtool failed with exit code $LASTEXITCODE."
}

Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

Write-Host ""
Write-Host "MSIX: $msixPath"
Write-Host "Certificate (share with testers so they can trust the publisher): $cerPath"
Write-Host ""
Write-Host "Install on this machine:"
Write-Host "  Add-AppxPackage -Path `"$msixPath`""
Write-Host "Install on another PC: import WillIDIY.cer into Local Machine\Trusted People, enable sideloading, then Add-AppxPackage."
