#Requires -Version 5.1
<#
.SYNOPSIS
  Start the unpackaged GTK4 + libadwaita Agent board (Gir.Core) on Windows.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
    throw 'Run this from the WorkCosts git repository.'
}
Set-Location $repoRoot
$env:WORKCOSTS_ROOT = $repoRoot

$gtkHint = 'Install GTK4 + libadwaita (MSYS2: pacman -S mingw-w64-x86_64-gtk4 mingw-w64-x86_64-libadwaita) and add the mingw64 bin directory to PATH. This board is unpackaged — not a Flatpak or MSIX.'
$probe = Get-Command gdbus.exe -ErrorAction SilentlyContinue
if (-not $probe) {
    Write-Warning $gtkHint
}

$csproj = Join-Path $repoRoot 'tools/agent-board/AgentBoard/AgentBoard.csproj'
if (-not (Test-Path -LiteralPath $csproj)) {
    throw "Missing $csproj"
}

try {
    dotnet run --project $csproj --no-launch-profile
}
catch {
    Write-Host $gtkHint
    throw
}
