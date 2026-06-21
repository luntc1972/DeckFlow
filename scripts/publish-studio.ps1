# Publish DeckFlow.Studio as a self-contained win-x64 single-file executable (DIST-01).
# Invokes the Windows dotnet.exe with the win-x64-selfcontained publish profile, strips
# non-distribution files, and zips the artifact folder.
#
# Usage:
#   .\scripts\publish-studio.ps1
#   .\scripts\publish-studio.ps1 -OutDir artifacts\studio-release -Version 2026.06.21
#
# Output: artifacts\studio-release\  (exe + wwwroot + appsettings.json + endpoints manifest)
#         artifacts\DeckFlowStudio-<version>.zip
$ErrorActionPreference = 'Stop'

param(
    [string]$OutDir  = "artifacts\studio-release",
    [string]$Version = (Get-Date -Format "yyyy.MM.dd")
)

# Resolve repo root (script lives in scripts\, so go up one level)
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..'))
Set-Location $RepoRoot

$DotNet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $DotNet)) {
    Write-Error "Windows dotnet.exe not found at '$DotNet'. Install .NET 10 SDK for Windows."
    exit 1
}

$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$AbsOutDir    = Join-Path $RepoRoot $OutDir
$ZipPath      = Join-Path $ArtifactsDir "DeckFlowStudio-$Version.zip"
$CsprojPath   = Join-Path $RepoRoot "DeckFlow.Studio\DeckFlow.Studio.csproj"

# Clean output dir so the script is re-runnable with no stale artifacts
if (Test-Path $AbsOutDir) {
    Write-Host "Cleaning $AbsOutDir ..."
    Remove-Item -Recurse -Force $AbsOutDir
}
New-Item -ItemType Directory -Force -Path $AbsOutDir | Out-Null
New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null

# Publish using the win-x64-selfcontained profile (carries config/RID/self-contained/
# single-file/native-extract/trim-off — do not re-pass them on the CLI)
Write-Host "Publishing DeckFlow.Studio (win-x64, self-contained, single-file) ..."
& $DotNet publish $CsprojPath -p:PublishProfile=win-x64-selfcontained -o $AbsOutDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed (exit code $LASTEXITCODE)."
    exit $LASTEXITCODE
}

# Strip non-distribution files
Write-Host "Stripping non-distribution files ..."
$StripPatterns = @('*.pdb', '*.xml', 'web.config', 'appsettings.Development.json')
foreach ($pattern in $StripPatterns) {
    Get-ChildItem -Path $AbsOutDir -Filter $pattern -Recurse -ErrorAction SilentlyContinue |
        Remove-Item -Force
}

# Report exe size
$ExePath = Join-Path $AbsOutDir "DeckFlow.Studio.exe"
if (Test-Path $ExePath) {
    $SizeMB = [math]::Round((Get-Item $ExePath).Length / 1MB, 1)
    Write-Host "Artifact: $ExePath  ($SizeMB MB)"
} else {
    Write-Error "DeckFlow.Studio.exe not found in output dir after publish."
    exit 1
}

# Zip the output dir
Write-Host "Zipping to $ZipPath ..."
if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }
Compress-Archive -Path "$AbsOutDir\*" -DestinationPath $ZipPath -Force
$ZipMB = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
Write-Host "Zip:      $ZipPath  ($ZipMB MB)"
Write-Host "Done. Distribute the zip or copy the folder to the target machine."
