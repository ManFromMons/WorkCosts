#Requires -Version 5.1
<#
.SYNOPSIS
  Print the next startable GNOME port slice from origin/main, or a named slice.

.DESCRIPTION
  Reads docs/platforms/gnome-build-order.md on origin/main. Eligible: Status
  ready-for-agent, Depends-on slices done, Requires-windows none or that
  docs/features story Status done. Fetch only; never force-pushes.

  Default: prints a kebab, PORT_CAUGHT_UP, PORT_WAITING_ON_WINDOWS:<kebab>,
  or PLAYBOOK_MISSING (exit 1).
#>
[CmdletBinding()]
param(
    [switch] $List,
    [string] $Slice,
    [string] $Remote = 'origin',
    [string] $MainBranch = 'main',
    [string] $PlaybookPath = 'docs/platforms/gnome-build-order.md',
    [string] $FeaturesPrefix = 'docs/features/'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param([Parameter(Mandatory)][string[]] $GitArgs)
    & git @GitArgs
    if ($LASTEXITCODE -ne 0) {
        throw "git $($GitArgs -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Get-GitText {
    param([Parameter(Mandatory)][string[]] $GitArgs)
    $text = (& git @GitArgs | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "git $($GitArgs -join ' ') failed with exit code $LASTEXITCODE"
    }
    return $text
}

function Try-GitText {
    param([Parameter(Mandatory)][string[]] $GitArgs)
    $text = (& git @GitArgs 2>$null | Out-String)
    if ($LASTEXITCODE -ne 0) {
        return $null
    }
    return $text
}

function Get-HeaderValue {
    param(
        [Parameter(Mandatory)][string] $Markdown,
        [Parameter(Mandatory)][string] $Label
    )
    $pattern = '(?m)^-\s+\*\*' + [regex]::Escape($Label) + ':\*\*\s*(.+?)\s*$'
    $match = [regex]::Match($Markdown, $pattern)
    if (-not $match.Success) {
        return $null
    }
    return $match.Groups[1].Value.Trim()
}

function Get-DependsOn {
    param([string] $Raw)
    if ([string]::IsNullOrWhiteSpace($Raw) -or $Raw -eq 'none' -or $Raw -eq '—') {
        return @()
    }
    $ids = New-Object System.Collections.Generic.List[string]
    foreach ($part in ($Raw -split ',')) {
        $token = $part.Trim().Trim('`')
        if (-not [string]::IsNullOrWhiteSpace($token) -and $token -ne 'none') {
            $ids.Add($token)
        }
    }
    return @($ids)
}

function Get-RequiresWindows {
    param([string] $Raw)
    if ([string]::IsNullOrWhiteSpace($Raw) -or $Raw -eq 'none' -or $Raw -eq '—') {
        return $null
    }
    return $Raw.Trim().Trim('`')
}

$repoRoot = (Get-GitText -GitArgs @('rev-parse', '--show-toplevel')).Trim()
Set-Location $repoRoot
Invoke-Git -GitArgs @('fetch', $Remote)
$treeRef = "$Remote/$MainBranch"

$playbook = Try-GitText -GitArgs @('show', "${treeRef}:$PlaybookPath")
if ($null -eq $playbook) {
    Write-Output 'PLAYBOOK_MISSING'
    exit 1
}

$windows = @{}
$featurePaths = @(Get-GitText -GitArgs @('ls-tree', '--name-only', $treeRef, $FeaturesPrefix) -split "`r?`n" |
    Where-Object { $_ } |
    ForEach-Object { $_.Trim() })
foreach ($path in $featurePaths) {
    $name = Split-Path -Leaf $path
    if ($name -eq 'to-review.md' -or $name -like '*-delivery.md' -or -not $name.EndsWith('.md')) {
        continue
    }
    $md = Get-GitText -GitArgs @('show', "${treeRef}:$path")
    $kebab = [System.IO.Path]::GetFileNameWithoutExtension($name)
    $windows[$kebab] = ((Get-HeaderValue $md 'Status') -split '\s')[0]
}

$sliceBlocks = [regex]::Split($playbook, '(?m)(?=^## Slice )')
$slices = New-Object System.Collections.Generic.List[object]
foreach ($block in $sliceBlocks) {
    if ($block -notmatch '(?m)^## Slice\s+(\d+)\s+') {
        continue
    }
    $order = [int]$Matches[1]
    $idRaw = Get-HeaderValue $block 'Id'
    if ([string]::IsNullOrWhiteSpace($idRaw)) {
        continue
    }
    $id = $idRaw.Trim().Trim('`')
    $slices.Add([pscustomobject]@{
        Order            = $order
        Id               = $id
        Status           = ((Get-HeaderValue $block 'Status') -split '\s')[0]
        DependsOn        = @(Get-DependsOn (Get-HeaderValue $block 'Depends-on'))
        RequiresWindows  = Get-RequiresWindows (Get-HeaderValue $block 'Requires-windows')
        Title            = Get-HeaderValue $block 'Branch-title'
    })
}

$byId = @{}
foreach ($s in $slices) {
    $byId[$s.Id] = $s
}

function Test-WindowsReady {
    param($SliceRow)
    if ([string]::IsNullOrWhiteSpace($SliceRow.RequiresWindows)) {
        return $true
    }
    $need = $SliceRow.RequiresWindows
    return ($windows.ContainsKey($need) -and $windows[$need] -eq 'done')
}

function Test-DepsDone {
    param($SliceRow)
    foreach ($dep in $SliceRow.DependsOn) {
        if (-not $byId.ContainsKey($dep) -or $byId[$dep].Status -ne 'done') {
            return $false
        }
    }
    return $true
}

function Get-BlockReason {
    param($SliceRow)
    if ($SliceRow.Status -eq 'done') {
        return 'slice already done'
    }
    if ($SliceRow.Status -ne 'ready-for-agent') {
        return "slice Status is $($SliceRow.Status)"
    }
    if (-not (Test-DepsDone $SliceRow)) {
        $missing = @($SliceRow.DependsOn | Where-Object { -not $byId.ContainsKey($_) -or $byId[$_].Status -ne 'done' })
        return "Depends-on not done: $($missing -join ', ')"
    }
    if (-not (Test-WindowsReady $SliceRow)) {
        return "Requires-windows $($SliceRow.RequiresWindows) is not done on origin/main"
    }
    return $null
}

if ($List) {
    Write-Output "GNOME port slices (from $treeRef)"
    foreach ($s in ($slices | Sort-Object Order)) {
        $extra = ''
        $reason = Get-BlockReason $s
        if ($s.Status -eq 'done') {
            $extra = ' [done]'
        }
        elseif ($null -eq $reason) {
            $extra = ' [startable]'
        }
        elseif ($reason -like 'Requires-windows*') {
            $extra = " [waiting on Windows $($s.RequiresWindows)]"
        }
        Write-Output ("  {0,2}. {1}  Status={2}{3}" -f $s.Order, $s.Id, $s.Status, $extra)
    }
    exit 0
}

if ($Slice) {
    $id = $Slice.Trim().Trim('`')
    if (-not $byId.ContainsKey($id)) {
        Write-Output "FOUND=false"
        Write-Output "REASON=unknown slice $id"
        exit 1
    }
    $row = $byId[$id]
    $reason = Get-BlockReason $row
    Write-Output "FOUND=true"
    Write-Output "KEBAB=$($row.Id)"
    Write-Output "ORDER=$($row.Order)"
    if ($null -eq $reason) {
        Write-Output 'STARTABLE=true'
        exit 0
    }
    Write-Output 'STARTABLE=false'
    Write-Output "REASON=$reason"
    exit 2
}

$eligible = @($slices | Where-Object { $null -eq (Get-BlockReason $_) } | Sort-Object Order)
if ($eligible.Count -gt 0) {
    Write-Output $eligible[0].Id
    exit 0
}

$allDone = @($slices | Where-Object { $_.Status -ne 'done' }).Count -eq 0
if ($allDone -or $slices.Count -eq 0) {
    Write-Output 'PORT_CAUGHT_UP'
    exit 0
}

$waiting = @($slices | Where-Object {
        $_.Status -eq 'ready-for-agent' -and (Test-DepsDone $_) -and -not (Test-WindowsReady $_)
    } | Sort-Object Order | Select-Object -First 1)
if ($null -ne $waiting) {
    Write-Output "PORT_WAITING_ON_WINDOWS:$($waiting.RequiresWindows)"
    exit 0
}

Write-Output 'PORT_CAUGHT_UP'
exit 0
