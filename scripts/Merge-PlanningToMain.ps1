#Requires -Version 5.1
<#
.SYNOPSIS
  Rebase Planning onto main, squash its commits, fast-forward main, push main and Planning.

.DESCRIPTION
  Never creates a merge commit on main. Never force-pushes main.
  Planning is rewritten (rebase + optional squash), so it is pushed with --force-with-lease.
  docs/features/to-review.md on main is preserved (inbox is not owned by Planning).

.EXAMPLE
  powershell -File scripts/Merge-PlanningToMain.ps1
.EXAMPLE
  powershell -File scripts/Merge-PlanningToMain.ps1 -Message "Add paste-HTML and zip export specs."
#>
[CmdletBinding()]
param(
    [string] $PlanningBranch = 'Planning',
    [string] $MainBranch = 'main',
    [string] $Remote = 'origin',
    [string] $Message
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

$repoRoot = Get-GitText -GitArgs @('rev-parse', '--show-toplevel')
Set-Location $repoRoot

$porcelain = Get-GitText -GitArgs @('status', '--porcelain')
if (-not [string]::IsNullOrWhiteSpace($porcelain)) {
    throw "Working tree is not clean. Commit or stash before merging $PlanningBranch into $MainBranch."
}

$current = Get-GitText -GitArgs @('branch', '--show-current')
Write-Host "Repo: $repoRoot"
Write-Host "Was on: $current"

Invoke-Git -GitArgs @('fetch', $Remote)

$remoteMain = "$Remote/$MainBranch"
$remotePlanning = "$Remote/$PlanningBranch"
$toReviewRel = 'docs/features/to-review.md'
$savedToReview = Join-Path $env:TEMP ("workcosts-main-toreview-{0}.md" -f [guid]::NewGuid().ToString('N'))
$hadToReviewOnMain = $false

try {
    Invoke-Git -GitArgs @('checkout', $MainBranch)
    if (Test-GitRef $remoteMain) {
        Invoke-Git -GitArgs @('merge', '--ff-only', $remoteMain)
    }

    if (Test-GitBlob "HEAD:$toReviewRel") {
        Copy-Item -LiteralPath (Join-Path $repoRoot $toReviewRel) -Destination $savedToReview -Force
        $hadToReviewOnMain = $true
    }

    Invoke-Git -GitArgs @('checkout', $PlanningBranch)

    try {
        Invoke-Git -GitArgs @('rebase', $MainBranch)
    }
    catch {
        Write-Host 'Rebase failed. Aborting rebase.'
        git rebase --abort 2>$null
        throw
    }

    $ahead = [int](Get-GitText -GitArgs @('rev-list', '--count', "${MainBranch}..HEAD"))
    Write-Host "$PlanningBranch is $ahead commit(s) ahead of $MainBranch after rebase."

    if ($ahead -gt 1) {
        if ([string]::IsNullOrWhiteSpace($Message)) {
            $log = Get-GitText -GitArgs @('log', '--reverse', '--format=- %s', "${MainBranch}..HEAD")
            $Message = "Apply Planning branch.`n`n$log"
        }
        Invoke-Git -GitArgs @('reset', '--soft', $MainBranch)
        Invoke-Git -GitArgs @('commit', '-m', $Message)
        $ahead = 1
        Write-Host "Squashed Planning onto one commit."
    }
    elseif ($ahead -eq 1 -and -not [string]::IsNullOrWhiteSpace($Message)) {
        Invoke-Git -GitArgs @('commit', '--amend', '-m', $Message)
        Write-Host "Amended the single Planning commit message."
    }

    Invoke-Git -GitArgs @('checkout', $MainBranch)

    if ($ahead -eq 0) {
        Write-Host "Nothing to merge. $MainBranch already contains $PlanningBranch."
    }
    else {
        Invoke-Git -GitArgs @('merge', '--ff-only', $PlanningBranch)
    }

    if ($hadToReviewOnMain -and (Test-Path -LiteralPath $savedToReview)) {
        $dest = Join-Path $repoRoot $toReviewRel
        $destDir = Split-Path -Parent $dest
        if (-not (Test-Path -LiteralPath $destDir)) {
            New-Item -ItemType Directory -Path $destDir | Out-Null
        }
        Copy-Item -LiteralPath $savedToReview -Destination $dest -Force
        Invoke-Git -GitArgs @('add', '--', $toReviewRel)
        git diff --cached --quiet -- $toReviewRel
        if ($LASTEXITCODE -ne 0) {
            Invoke-Git -GitArgs @('commit', '-m', 'Keep docs/features/to-review.md on main.')
            Write-Host "Preserved $toReviewRel from $MainBranch."
        }
    }

    Write-Host "Pushing $MainBranch (no force)..."
    Invoke-Git -GitArgs @('push', $Remote, $MainBranch)

    Write-Host "Pushing $PlanningBranch (--force-with-lease; history was rebased/squashed)..."
    if (Test-GitRef $remotePlanning) {
        Invoke-Git -GitArgs @('push', '--force-with-lease', $Remote, $PlanningBranch)
    }
    else {
        Invoke-Git -GitArgs @('push', '-u', $Remote, $PlanningBranch)
    }

    Write-Host "Done. $MainBranch and $PlanningBranch are on $Remote."
    Invoke-Git -GitArgs @('status', '-sb')
}
finally {
    if (Test-Path -LiteralPath $savedToReview) {
        Remove-Item -LiteralPath $savedToReview -Force
    }
}
