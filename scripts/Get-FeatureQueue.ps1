#Requires -Version 5.1
<#
.SYNOPSIS
  Print the feature dependency tree (Seq + Status), or resolve one Seq to a kebab.

.DESCRIPTION
  Reads docs/features/*.md from the working tree (Planning WIP included) and
  overlays origin/main + to-review work status. Ignores to-review.md and
  *-delivery.md. Fetch only; never force-pushes.

  With -Seq, prints KEY=value for the agent (KEBAB, STARTABLE, START_SKILL, ...).
#>
[CmdletBinding()]
param(
    [int] $Seq,
    [switch] $FromMain,
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
    if ([string]::IsNullOrWhiteSpace($Raw) -or $Raw -eq 'none' -or $Raw -eq [char]0x2014) {
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

function Get-Title {
    param([string] $Markdown)
    $h1 = [regex]::Match($Markdown, '(?m)^#\s+Feature:\s*(.+?)\s*$')
    if ($h1.Success) {
        return $h1.Groups[1].Value.Trim()
    }
    $any = [regex]::Match($Markdown, '(?m)^#\s+(.+?)\s*$')
    if ($any.Success) {
        return $any.Groups[1].Value.Trim()
    }
    return $null
}

function Get-InboxStatuses {
    param([string] $Markdown)
    $map = @{}
    if ([string]::IsNullOrWhiteSpace($Markdown)) {
        return $map
    }
    $blocks = [regex]::Split($Markdown, '(?m)^##\s+')
    foreach ($block in $blocks) {
        $line = ($block -split "`r?`n", 2)[0].Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line -eq 'Entries') {
            continue
        }
        $kebab = ($line -split '\s')[0].Trim()
        $status = Get-HeaderValue $block 'Status'
        if ($status) {
            $map[$kebab] = ($status -split '\s')[0]
        }
    }
    return $map
}

function Parse-StoryMarkdown {
    param(
        [Parameter(Mandatory)][string] $Markdown,
        [Parameter(Mandatory)][string] $FileName
    )
    $kebab = Get-KebabFromId (Get-HeaderValue $Markdown 'Id')
    if ([string]::IsNullOrWhiteSpace($kebab)) {
        $kebab = [System.IO.Path]::GetFileNameWithoutExtension($FileName)
    }

    $seqRaw = Get-HeaderValue $Markdown 'Seq'
    $seq = [int]::MaxValue
    if ($seqRaw -and -not [int]::TryParse($seqRaw, [ref]$seq)) {
        $seq = [int]::MaxValue
    }

    $status = Get-HeaderValue $Markdown 'Status'
    if ($status) {
        $status = ($status -split '\s')[0]
    }
    else {
        $status = 'unknown'
    }

    return [pscustomobject]@{
        Kebab      = $kebab
        Seq        = $seq
        Status     = $status
        MainStatus = $null
        DependsOn  = @(Get-DependsOn (Get-HeaderValue $Markdown 'Depends-on'))
        Title      = Get-Title $Markdown
        OnMain     = $false
        OnDisk     = $false
        Inbox      = $null
    }
}

function Get-WaitingOn {
    param($Story, $Stories)
    $waiting = New-Object System.Collections.Generic.List[string]
    foreach ($dep in $Story.DependsOn) {
        if (-not $Stories.ContainsKey($dep)) {
            $waiting.Add("${dep} (missing)")
            continue
        }
        if ($Stories[$dep].Status -ne 'done') {
            $depStory = $Stories[$dep]
            $seqLabel = if ($depStory.Seq -eq [int]::MaxValue) { '?' } else { [string]$depStory.Seq }
            $waiting.Add("${seqLabel} $dep ($($depStory.Status))")
        }
    }
    return @($waiting)
}

function Get-WaitingOnMain {
    param($Story, $Stories)
    $waiting = New-Object System.Collections.Generic.List[string]
    foreach ($dep in $Story.DependsOn) {
        if (-not $Stories.ContainsKey($dep)) {
            $waiting.Add("${dep} (missing)")
            continue
        }
        $depStory = $Stories[$dep]
        if (-not $depStory.OnMain) {
            $waiting.Add("$dep (not on origin/main)")
            continue
        }
        if ($depStory.MainStatus -ne 'done') {
            $seqLabel = Format-Seq $depStory.Seq
            $waiting.Add("${seqLabel} $dep ($($depStory.MainStatus))")
        }
    }
    return @($waiting)
}

function Test-DepsDone {
    param($Story, $Stories, [switch] $OnMain)
    foreach ($dep in $Story.DependsOn) {
        if (-not $Stories.ContainsKey($dep)) {
            return $false
        }
        $depStory = $Stories[$dep]
        if ($OnMain) {
            if (-not $depStory.OnMain -or $depStory.MainStatus -ne 'done') {
                return $false
            }
        }
        elseif ($depStory.Status -ne 'done') {
            return $false
        }
    }
    return $true
}

function Get-StartSkill {
    param([string] $Kebab)
    if ($Kebab -like 'source-*') {
        return 'start-add-source'
    }
    return 'start-implement'
}

function Get-PrimaryParent {
    param($Story, $Stories)
    if ($Story.DependsOn.Count -eq 0) {
        return $null
    }
    $found = @()
    foreach ($dep in $Story.DependsOn) {
        if ($Stories.ContainsKey($dep)) {
            $found += $Stories[$dep]
        }
    }
    if ($found.Count -eq 0) {
        return $null
    }
    $sorted = $found | Sort-Object Seq, Kebab
    return @($sorted)[0].Kebab
}

function Format-Seq {
    param([int] $Seq)
    if ($Seq -eq [int]::MaxValue) {
        return '?'
    }
    return [string]$Seq
}

function Format-StoryLine {
    param($Story, $Stories)
    $seq = Format-Seq $Story.Seq
    $title = $Story.Title
    if ([string]::IsNullOrWhiteSpace($title)) {
        $title = $Story.Kebab
    }
    $notes = New-Object System.Collections.Generic.List[string]
    $waiting = @(Get-WaitingOn $Story $Stories)
    $startableNow = ($Story.OnMain -and $Story.MainStatus -eq 'ready-for-agent' -and (Test-DepsDone $Story $Stories -OnMain))

    if ($startableNow) {
        $notes.Add('startable')
    }
    elseif ($Story.Status -eq 'ready-for-agent' -and -not $Story.OnMain) {
        $notes.Add('Planning only')
    }
    if ($waiting.Count -gt 0) {
        $notes.Add('waits on ' + ($waiting -join '; '))
    }
    if ($Story.Inbox -and $Story.Inbox -ne $Story.Status) {
        $notes.Add('inbox ' + $Story.Inbox)
    }

    $noteText = ''
    if ($notes.Count -gt 0) {
        $noteText = '  [' + ($notes -join '; ') + ']'
    }

    return "[$seq] $($Story.Kebab) - $title  ($($Story.Status))$noteText"
}

$repoRoot = (Get-GitText -GitArgs @('rev-parse', '--show-toplevel')).Trim()
Set-Location $repoRoot

try {
    Invoke-Git -GitArgs @('fetch', $Remote)
}
catch {
    Write-Warning "git fetch $Remote failed; using local refs. $($_.Exception.Message)"
}

$treeRef = "$Remote/$MainBranch"
$mainPaths = @()
$mainList = Try-GitText -GitArgs @('ls-tree', '--name-only', $treeRef, $FeaturesPrefix)
if ($mainList) {
    $mainPaths = @($mainList -split "`r?`n" | Where-Object { $_ } | ForEach-Object { $_.Trim() })
}

$stories = @{}

function Add-OrMergeStory {
    param($Parsed, [bool] $FromMainFile, [bool] $FromDisk)
    $key = $Parsed.Kebab
    if ($stories.ContainsKey($key)) {
        $existing = $stories[$key]
        if ($FromDisk) {
            $Parsed.OnMain = $existing.OnMain
            $Parsed.MainStatus = $existing.MainStatus
            $Parsed.OnDisk = $true
            $Parsed.Inbox = $existing.Inbox
            $stories[$key] = $Parsed
        }
        else {
            $existing.OnMain = $true
            $existing.MainStatus = $Parsed.Status
        }
        if ($FromMainFile) {
            $stories[$key].OnMain = $true
            if (-not $stories[$key].MainStatus) {
                $stories[$key].MainStatus = $Parsed.Status
            }
        }
        return
    }
    $Parsed.OnMain = $FromMainFile
    $Parsed.OnDisk = $FromDisk
    if ($FromMainFile) {
        $Parsed.MainStatus = $Parsed.Status
    }
    $stories[$key] = $Parsed
}

foreach ($path in $mainPaths) {
    $name = Split-Path -Leaf $path
    if ($name -eq 'to-review.md' -or $name -like '*-delivery.md') {
        continue
    }
    if (-not $name.EndsWith('.md')) {
        continue
    }
    $md = Try-GitText -GitArgs @('show', "${treeRef}:$path")
    if (-not $md) {
        continue
    }
    $parsed = Parse-StoryMarkdown -Markdown $md -FileName $name
    Add-OrMergeStory -Parsed $parsed -FromMainFile $true -FromDisk $false
}

if (-not $FromMain) {
    $localDir = Join-Path $repoRoot 'docs/features'
    if (Test-Path -LiteralPath $localDir) {
        Get-ChildItem -LiteralPath $localDir -Filter '*.md' -File | ForEach-Object {
            if ($_.Name -eq 'to-review.md' -or $_.Name -like '*-delivery.md') {
                return
            }
            $md = [System.IO.File]::ReadAllText($_.FullName)
            $parsed = Parse-StoryMarkdown -Markdown $md -FileName $_.Name
            Add-OrMergeStory -Parsed $parsed -FromMainFile $false -FromDisk $true
        }
    }
}

$inboxMd = Try-GitText -GitArgs @('show', "${treeRef}:docs/features/to-review.md")
$inbox = Get-InboxStatuses -Markdown $inboxMd
foreach ($key in @($stories.Keys)) {
    if ($inbox.ContainsKey($key)) {
        $stories[$key].Inbox = $inbox[$key]
    }
}

$bySeq = @{}
foreach ($story in $stories.Values) {
    if ($story.Seq -eq [int]::MaxValue) {
        continue
    }
    if ($bySeq.ContainsKey($story.Seq)) {
        $bySeq[$story.Seq] += @($story.Kebab)
    }
    else {
        $bySeq[$story.Seq] = @($story.Kebab)
    }
}

if ($PSBoundParameters.ContainsKey('Seq')) {
    if (-not $bySeq.ContainsKey($Seq)) {
        Write-Output "SEQ=$Seq"
        Write-Output 'FOUND=false'
        Write-Output 'STARTABLE=false'
        Write-Output "REASON=No story with Seq $Seq."
        exit 1
    }
    $matches = @($bySeq[$Seq])
    if ($matches.Count -gt 1) {
        Write-Output "SEQ=$Seq"
        Write-Output 'FOUND=false'
        Write-Output 'STARTABLE=false'
        Write-Output ("REASON=Duplicate Seq ${Seq}: " + ($matches -join ', '))
        exit 1
    }

    $story = $stories[$matches[0]]
    $waiting = @(Get-WaitingOn $story $stories)
    $waitingMain = @(Get-WaitingOnMain $story $stories)
    $depsDoneOnMain = Test-DepsDone $story $stories -OnMain
    $startable = $story.OnMain -and ($story.MainStatus -eq 'ready-for-agent') -and $depsDoneOnMain
    $reason = 'Ready to implement.'
    if (-not $story.OnMain) {
        if ($story.Status -eq 'draft') {
            $reason = 'Status is draft. Finish plan-feature before implementing.'
        }
        else {
            $reason = 'Story is not on origin/main. Land Planning first (merge-planning).'
        }
    }
    elseif ($story.MainStatus -eq 'done') {
        $reason = 'Status is done on origin/main. A change needs a new plan, not this Seq.'
    }
    elseif ($story.MainStatus -eq 'draft') {
        $reason = 'Status is draft on origin/main. Finish plan-feature and merge-planning before implementing.'
    }
    elseif ($story.MainStatus -ne 'ready-for-agent') {
        $reason = "Status on origin/main is $($story.MainStatus); need ready-for-agent."
    }
    elseif (-not $depsDoneOnMain) {
        $reason = 'Dependencies are not done on origin/main: ' + ($waitingMain -join '; ')
    }

    Write-Output "SEQ=$($story.Seq)"
    Write-Output "KEBAB=$($story.Kebab)"
    Write-Output "TITLE=$($story.Title)"
    Write-Output "STATUS=$($story.Status)"
    if ($story.MainStatus) {
        Write-Output "MAIN_STATUS=$($story.MainStatus)"
    }
    else {
        Write-Output 'MAIN_STATUS='
    }
    Write-Output "ON_MAIN=$($story.OnMain.ToString().ToLowerInvariant())"
    Write-Output "DEPS_DONE=$($depsDoneOnMain.ToString().ToLowerInvariant())"
    Write-Output "STARTABLE=$($startable.ToString().ToLowerInvariant())"
    Write-Output "START_SKILL=$(Get-StartSkill $story.Kebab)"
    Write-Output "REASON=$reason"
    if ($waiting.Count -gt 0) {
        Write-Output ("WAITING=" + ($waiting -join '; '))
    }
    if ($waitingMain.Count -gt 0) {
        Write-Output ("WAITING_MAIN=" + ($waitingMain -join '; '))
    }
    if ($startable) {
        exit 0
    }
    exit 2
}

$children = @{}
foreach ($story in $stories.Values) {
    $parent = Get-PrimaryParent $story $stories
    if ([string]::IsNullOrWhiteSpace($parent)) {
        continue
    }
    if (-not $children.ContainsKey($parent)) {
        $children[$parent] = New-Object System.Collections.Generic.List[object]
    }
    $children[$parent].Add($story)
}

$roots = @($stories.Values | Where-Object {
        $null -eq (Get-PrimaryParent $_ $stories)
    } | Sort-Object Seq, Kebab)

function Get-ChildStories {
    param([string] $Kebab)
    if (-not $children.ContainsKey($Kebab)) {
        return @()
    }
    return @($children[$Kebab] | Sort-Object Seq, Kebab)
}

function Write-Tree {
    param($Story, [string] $Prefix, [bool] $IsLast, $Seen)
    if ($Seen.ContainsKey($Story.Kebab)) {
        $seq = Format-Seq $Story.Seq
        if ($Prefix -eq '') {
            $marker = ''
        }
        elseif ($IsLast) {
            $marker = $Prefix + '-- '
        }
        else {
            $marker = $Prefix + '+- '
        }
        Write-Output ($marker + "[$seq] $($Story.Kebab)  (cycle, already listed)")
        return
    }
    $Seen[$Story.Kebab] = $true

    if ($Prefix -eq '') {
        Write-Output (Format-StoryLine $Story $stories)
        $nextPrefix = '    '
    }
    else {
        $branch = if ($IsLast) { '-- ' } else { '+- ' }
        Write-Output ($Prefix + $branch + (Format-StoryLine $Story $stories))
        if ($IsLast) {
            $nextPrefix = $Prefix + '   '
        }
        else {
            $nextPrefix = $Prefix + '|  '
        }
    }

    $kids = @(Get-ChildStories $Story.Kebab)
    for ($i = 0; $i -lt $kids.Count; $i++) {
        $last = ($i -eq ($kids.Count - 1))
        $usePrefix = if ($Prefix -eq '') { '    ' } else { $nextPrefix }
        Write-Tree -Story $kids[$i] -Prefix $usePrefix -IsLast $last -Seen $Seen
    }
}

Write-Output 'Work queue (Seq / dependency tree)'
if ($FromMain) {
    Write-Output "Source: $treeRef only"
}
else {
    Write-Output "Source: working tree overlay on $treeRef"
}
Write-Output ''

$seen = @{}
foreach ($root in $roots) {
    Write-Tree -Story $root -Prefix '' -IsLast $true -Seen $seen
    Write-Output ''
}

$dupes = @($bySeq.GetEnumerator() | Where-Object { $_.Value.Count -gt 1 } | Sort-Object Name)
if ($dupes.Count -gt 0) {
    Write-Output 'Duplicate Seq values:'
    foreach ($d in $dupes) {
        Write-Output ("  $($d.Key): " + ($d.Value -join ', '))
    }
    Write-Output ''
}

$startable = @($stories.Values | Where-Object {
        $_.OnMain -and $_.MainStatus -eq 'ready-for-agent' -and (Test-DepsDone $_ $stories -OnMain)
    } | Sort-Object Seq, Kebab)

$planningOnlyReady = @($stories.Values | Where-Object {
        $_.Status -eq 'ready-for-agent' -and -not $_.OnMain
    } | Sort-Object Seq, Kebab)

Write-Output '---'
if ($startable.Count -eq 0) {
    Write-Output 'Startable Seq (on origin/main, deps done): none'
}
else {
    $bits = @($startable | ForEach-Object { "$($_.Seq) $($_.Kebab)" })
    Write-Output ('Startable Seq (on origin/main, deps done): ' + ($bits -join ', '))
}
if ($planningOnlyReady.Count -gt 0) {
    $bits = @($planningOnlyReady | ForEach-Object { "$($_.Seq) $($_.Kebab)" })
    Write-Output ('Ready on Planning only (merge-planning first): ' + ($bits -join ', '))
}

$mainEligible = @($stories.Values | Where-Object {
        $_.OnMain -and $_.MainStatus -eq 'ready-for-agent' -and (Test-DepsDone $_ $stories -OnMain)
    } | Sort-Object Seq, Kebab)
if ($mainEligible.Count -eq 0) {
    Write-Output 'Next pickup (origin/main): QUEUE_EMPTY'
}
else {
    $n = @($mainEligible)[0]
    Write-Output "Next pickup (origin/main): $($n.Seq) $($n.Kebab)"
}
