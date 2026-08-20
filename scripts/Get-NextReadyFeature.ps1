#Requires -Version 5.1
<#
.SYNOPSIS
  Print the next ready-for-agent feature id from origin/main, or QUEUE_EMPTY.

.DESCRIPTION
  Eligible: Status ready-for-agent, every Depends-on kebab is Status done, lowest Seq.
  Ignores to-review.md and *-delivery.md. Never force-pushes. Fetch only.
#>
[CmdletBinding()]
param(
    [string] $Remote = 'origin',
    [string] $MainBranch = 'main',
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

function Get-KebabFromId {
    param([string] $IdLine)
    if ([string]::IsNullOrWhiteSpace($IdLine)) {
        return $null
    }
    $backticked = [regex]::Match($IdLine, '`docs/features/([^`]+)\.md`')
    if ($backticked.Success) {
        return $backticked.Groups[1].Value
    }
    $plain = [regex]::Match($IdLine, 'docs/features/([^\s]+)\.md')
    if ($plain.Success) {
        return $plain.Groups[1].Value
    }
    return $null
}

function Get-DependsOn {
    param([string] $Raw)
    if ([string]::IsNullOrWhiteSpace($Raw) -or $Raw -eq 'none' -or $Raw -eq '—') {
        return @()
    }
    $parts = $Raw -split ','
    $ids = New-Object System.Collections.Generic.List[string]
    foreach ($part in $parts) {
        $token = $part.Trim().Trim('`')
        if ($token.StartsWith('docs/features/')) {
            $idMatch = [regex]::Match($token, 'docs/features/([^.\s]+)')
            if ($idMatch.Success) {
                $ids.Add($idMatch.Groups[1].Value)
            }
            continue
        }
        if (-not [string]::IsNullOrWhiteSpace($token) -and $token -ne 'none') {
            $ids.Add($token)
        }
    }
    return @($ids)
}

$repoRoot = (Get-GitText -GitArgs @('rev-parse', '--show-toplevel')).Trim()
Set-Location $repoRoot
Invoke-Git -GitArgs @('fetch', $Remote)
$treeRef = "$Remote/$MainBranch"

$paths = @(Get-GitText -GitArgs @('ls-tree', '--name-only', $treeRef, $FeaturesPrefix) -split "`r?`n" |
    Where-Object { $_ } |
    ForEach-Object { $_.Trim() })

$stories = @{}
foreach ($path in $paths) {
    $name = Split-Path -Leaf $path
    if ($name -eq 'to-review.md' -or $name -like '*-delivery.md') {
        continue
    }
    if (-not $name.EndsWith('.md')) {
        continue
    }

    $md = Get-GitText -GitArgs @('show', "${treeRef}:$path")
    $kebab = Get-KebabFromId (Get-HeaderValue $md 'Id')
    if ([string]::IsNullOrWhiteSpace($kebab)) {
        $kebab = [System.IO.Path]::GetFileNameWithoutExtension($name)
    }

    $seqRaw = Get-HeaderValue $md 'Seq'
    $seq = 0
    if (-not [int]::TryParse($seqRaw, [ref]$seq)) {
        $seq = [int]::MaxValue
    }

    $stories[$kebab] = [pscustomobject]@{
        Kebab     = $kebab
        Seq       = $seq
        Status    = ((Get-HeaderValue $md 'Status') -split '\s')[0]
        DependsOn = @(Get-DependsOn (Get-HeaderValue $md 'Depends-on'))
    }
}

$eligible = New-Object System.Collections.Generic.List[object]
foreach ($story in $stories.Values) {
    if ($story.Status -ne 'ready-for-agent') {
        continue
    }
    $blocked = $false
    foreach ($dep in $story.DependsOn) {
        if (-not $stories.ContainsKey($dep) -or $stories[$dep].Status -ne 'done') {
            $blocked = $true
            break
        }
    }
    if (-not $blocked) {
        $eligible.Add($story)
    }
}

if ($eligible.Count -eq 0) {
    Write-Output 'QUEUE_EMPTY'
    exit 0
}

$next = $eligible | Sort-Object Seq, Kebab | Select-Object -First 1
Write-Output $next.Kebab
