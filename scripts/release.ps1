# Update DeckFlow.Web's tracked CalVer, create the release commit, and add the matching git tag.
#
# Usage:
#   .\scripts\release.ps1 2026.07.6
$ErrorActionPreference = 'Stop'

if ($args.Count -ne 1) {
    Write-Error "Expected exactly one version argument. Usage: .\scripts\release.ps1 YYYY.MM[.N]"
    exit 1
}

$Version = $args[0]
$CsprojPath = Join-Path $PSScriptRoot '..\DeckFlow.Web\DeckFlow.Web.csproj'
$CsprojPath = (Resolve-Path $CsprojPath).Path
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if ($Version -notmatch '^[0-9]{4}\.(0[1-9]|1[0-2])(\.[0-9]+)?$') {
    Write-Error "Version must match CalVer YYYY.MM or YYYY.MM.N (month must be two digits)."
    exit 1
}

Set-Location $RepoRoot

& git diff --quiet --exit-code
$trackedUnstagedClean = ($LASTEXITCODE -eq 0)
& git diff --cached --quiet --exit-code
$trackedStagedClean = ($LASTEXITCODE -eq 0)
if (-not ($trackedUnstagedClean -and $trackedStagedClean)) {
    Write-Error "Tracked git changes detected. Commit or stash tracked changes before releasing."
    exit 1
}

& git rev-parse -q --verify "refs/tags/$Version" *> $null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Git tag '$Version' already exists."
    exit 1
}

Write-Host "Updating DeckFlow.Web/DeckFlow.Web.csproj to version $Version ..."
$content = [System.IO.File]::ReadAllText($CsprojPath)
$updated = [System.Text.RegularExpressions.Regex]::Replace(
    $content,
    '(<Version>)[^<]+(</Version>)',
    ('$1' + $Version + '$2'),
    1
)
[System.IO.File]::WriteAllText($CsprojPath, $updated)

Write-Host "Creating release commit ..."
& git add "DeckFlow.Web/DeckFlow.Web.csproj"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& git commit -m "chore(release): $Version"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Creating git tag $Version ..."
# Annotated so `git push --follow-tags` (the hint below) actually pushes it.
& git tag -a $Version -m "DeckFlow $Version"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Tagged $Version. Now run: git push --follow-tags"
Write-Host "The About page will show $Version after the next deploy."
