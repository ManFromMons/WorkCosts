#Requires -Version 5.1
<#
.SYNOPSIS
  Commit docs/features/to-review.md on main only, push main, return to the previous branch.

.DESCRIPTION
  Code must already be committed on the working branch. The only allowed dirty path is
  docs/features/to-review.md. Never force-pushes main.

.EXAMPLE
  powershell -File scripts/Update-ToReviewOnMain.ps1
.EXAMPLE
  powershell -File scripts/Update-ToReviewOnMain.ps1 -Message "to-review: block paste-html"
#>
[CmdletBinding()]
param(
    [string] $MainBranch = 'main',
    [string] $Remote = 'origin',
    [string] $Message = 'Update docs/features/to-review.md.',
    [string] $ToReviewRel = 'docs/features/to-review.md'
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
    $text = (& git @GitArgs | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "git $($GitArgs -join ' ') failed with exit code $LASTEXITCODE"
    }
    return $text
}

function Test-GitRef {
    param([Parameter(Mandatory)][string] $Ref)
    git rev-parse --verify --quiet $Ref | Out-Null
    return $LASTEXITCODE -eq 0
}

function Test-GitBlob {
    param([Parameter(Mandatory)][string] $RevPath)
    cmd.exe /c "git cat-file -e `"$RevPath`" 1>nul 2>nul" | Out-Null
    return $LASTEXITCODE -eq 0
}

function Get-DirtyPaths {
    $raw = @(& git status --porcelain -uall)
    if ($LASTEXITCODE -ne 0) {
        throw 'git status --porcelain failed'
    }
    $paths = New-Object System.Collections.Generic.List[string]
    foreach ($line in $raw) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $rest = $line.Substring(3)
        if ($rest -match ' -> ') {
            $paths.Add(($rest -split ' -> ', 2)[1].Trim('"'))
        }
        else {
            $paths.Add($rest.Trim('"'))
        }
    }
    return @($paths | Select-Object -Unique)
}

function Test-FileTracked {
    param([Parameter(Mandatory)][string] $RelPath)
    git ls-files --error-unmatch -- $RelPath 1>$null 2>$null
    return $LASTEXITCODE -eq 0
}

$repoRoot = Get-GitText -GitArgs @('rev-parse', '--show-toplevel')
Set-Location $repoRoot

$toReviewPath = Join-Path $repoRoot $ToReviewRel
$dirty = @(Get-DirtyPaths)
$unexpected = @($dirty | Where-Object { $_ -ne $ToReviewRel })
if ($unexpected.Count -gt 0) {
    throw "Working tree has uncommitted code. Commit a buildable unit first. Extra dirty paths: $($unexpected -join ', ')"
}
if ($dirty.Count -eq 0) {
    throw "Nothing to land. Edit $ToReviewRel (uncommitted), then run this script. Do not commit that file on this branch."
}
if (-not (Test-Path -LiteralPath $toReviewPath)) {
    throw "Expected payload at $ToReviewRel"
}

$current = Get-GitText -GitArgs @('branch', '--show-current')
if ([string]::IsNullOrWhiteSpace($current)) {
    throw 'Detached HEAD. Check out a branch before updating to-review.'
}

Invoke-Git -GitArgs @('fetch', $Remote)
$remoteMain = "$Remote/$MainBranch"

if ($current -ne $MainBranch) {
    $mainRef = $MainBranch
    if (Test-GitRef $remoteMain) {
        $mainRef = $remoteMain
    }
    $mainHasFile = Test-GitBlob "${mainRef}:$ToReviewRel"
    if ($mainHasFile -and (Test-FileTracked $ToReviewRel)) {
        git diff --quiet $mainRef HEAD -- $ToReviewRel
        if ($LASTEXITCODE -ne 0) {
            throw "$ToReviewRel is committed on '$current' and differs from $mainRef. Restore it on this branch (`git checkout $MainBranch -- $ToReviewRel`) and keep inbox commits on $MainBranch only."
        }
    }
}

$payload = Join-Path $env:TEMP ("workcosts-toreview-{0}.md" -f [guid]::NewGuid().ToString('N'))
Copy-Item -LiteralPath $toReviewPath -Destination $payload -Force

try {
    if ($current -ne $MainBranch) {
        if (Test-FileTracked $ToReviewRel) {
            Invoke-Git -GitArgs @('checkout', '--', $ToReviewRel)
        }
        elseif (Test-Path -LiteralPath $toReviewPath) {
            Remove-Item -LiteralPath $toReviewPath -Force
        }
    }

    if ($current -ne $MainBranch) {
        Invoke-Git -GitArgs @('checkout', $MainBranch)
    }

    if (Test-GitRef $remoteMain) {
        Invoke-Git -GitArgs @('merge', '--ff-only', $remoteMain)
    }

    $destDir = Split-Path -Parent $toReviewPath
    if (-not (Test-Path -LiteralPath $destDir)) {
        New-Item -ItemType Directory -Path $destDir | Out-Null
    }
    Copy-Item -LiteralPath $payload -Destination $toReviewPath -Force
    Invoke-Git -GitArgs @('add', '--', $ToReviewRel)

    git diff --cached --quiet -- $ToReviewRel
    if ($LASTEXITCODE -eq 0) {
        Write-Host "$ToReviewRel on $MainBranch is already up to date."
        Invoke-Git -GitArgs @('reset', 'HEAD', '--', $ToReviewRel)
    }
    else {
        Invoke-Git -GitArgs @('commit', '-m', $Message)
        Write-Host "Pushing $MainBranch (no force)..."
        Invoke-Git -GitArgs @('push', $Remote, $MainBranch)
    }

    if ($current -ne $MainBranch) {
        Invoke-Git -GitArgs @('checkout', $current)
        if (Test-FileTracked $ToReviewRel) {
            Invoke-Git -GitArgs @('checkout', '--', $ToReviewRel)
        }
        elseif (Test-Path -LiteralPath $toReviewPath) {
            Remove-Item -LiteralPath $toReviewPath -Force
        }
    }
}
finally {
    if (Test-Path -LiteralPath $payload) {
        Remove-Item -LiteralPath $payload -Force
    }
}

Write-Host "Done. Inbox is on $MainBranch. Read it with: git show ${Remote}/${MainBranch}:$ToReviewRel"
Invoke-Git -GitArgs @('status', '-sb')
