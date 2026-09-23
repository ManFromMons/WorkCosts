#Requires -Version 5.1
<#
.SYNOPSIS
  Maps docs/data/tiresaddict-gen.json to WorkCosts.Core/Data/car-details.json.

.PARAMETER SkipWikidata
  Do not call Wikidata; missing chassis falls back to the Tiresaddict model line.
#>
[CmdletBinding()]
param(
    [switch] $SkipWikidata
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'tools\BuildCarDetailsSeed\BuildCarDetailsSeed.csproj'
$extra = @()
if ($SkipWikidata) {
    $extra += '--skip-wikidata'
}

& dotnet run --project $project --configuration Release -- @extra
if ($LASTEXITCODE -ne 0) {
    throw "Build-CarDetailsSeed failed with exit code $LASTEXITCODE"
}
