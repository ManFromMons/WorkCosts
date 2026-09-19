#Requires -Version 5.1
<#
.SYNOPSIS
  Start the lazygit-style agent ops TUI (queue, to-review, Cursor SDK chat).
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (git rev-parse --show-toplevel)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
    throw 'Run this from the WorkCosts git repository.'
}
$repoRoot = $repoRoot.Trim()
Set-Location $repoRoot

$tui = Join-Path $repoRoot 'tools/agent-tui'
if (-not (Test-Path -LiteralPath (Join-Path $tui 'package.json'))) {
    throw "Missing $tui. The agent TUI is not in this checkout."
}

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    $pkgRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    $wingetNode = Get-ChildItem -Path $pkgRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'OpenJS.NodeJS*' } |
        ForEach-Object {
            Get-ChildItem -LiteralPath $_.FullName -Filter node.exe -Recurse -ErrorAction SilentlyContinue |
                Select-Object -First 1
        } |
        Select-Object -First 1
    if ($wingetNode) {
        $env:Path = "$($wingetNode.DirectoryName);$env:Path"
    }
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js >= 22.13 is required (node not on PATH). Install from https://nodejs.org and reopen the terminal.'
}

Push-Location $tui
try {
    if (-not (Test-Path -LiteralPath (Join-Path $tui 'node_modules'))) {
        Write-Host 'Installing tools/agent-tui dependencies…'
        npm install
        if ($LASTEXITCODE -ne 0) {
            throw 'npm install failed.'
        }
    }
    $env:WORKCOSTS_ROOT = $repoRoot
    npm start
}
finally {
    Pop-Location
}
