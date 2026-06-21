#!/usr/bin/env bash
# Publish DeckFlow.Studio as a self-contained win-x64 single-file executable (DIST-01).
# WSL wrapper: invokes the WINDOWS dotnet.exe (NOT the Linux dotnet) to produce a win-x64 artifact.
#
# Usage (from WSL, from the repo root or anywhere):
#   bash scripts/publish-studio.sh
#
# Output: artifacts/studio-release/  (exe + wwwroot + appsettings.json + endpoints manifest)
#         artifacts/DeckFlowStudio-<date>.zip
set -euo pipefail

# cd to repo root (script lives in scripts/, so go up one level)
cd "$(dirname "$0")/.."

# Windows dotnet.exe path (WSL mount)
DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"

# Preflight: verify the Windows dotnet.exe exists and is executable
[[ -x "$DOTNET" ]] || {
    echo "ERROR: Windows dotnet.exe not found at '$DOTNET'." >&2
    echo "       Install .NET 10 SDK for Windows and ensure it is at the default path." >&2
    exit 1
}

OUT_DIR="artifacts/studio-release"
VERSION=$(date +"%Y.%m.%d")
ZIP_PATH="artifacts/DeckFlowStudio-${VERSION}.zip"
CSPROJ="DeckFlow.Studio/DeckFlow.Studio.csproj"

# Clean output dir so the script is re-runnable with no stale artifacts
if [[ -d "$OUT_DIR" ]]; then
    echo "Cleaning $OUT_DIR ..."
    rm -rf "$OUT_DIR"
fi
mkdir -p "$OUT_DIR"
mkdir -p "artifacts"

# Publish using the win-x64-selfcontained profile (carries config/RID/self-contained/
# single-file/native-extract/trim-off — do not re-pass them on the CLI)
echo "Publishing DeckFlow.Studio (win-x64, self-contained, single-file) ..."
"$DOTNET" publish "$CSPROJ" -p:PublishProfile=win-x64-selfcontained -o "$OUT_DIR"

# Strip non-distribution files
echo "Stripping non-distribution files ..."
find "$OUT_DIR" -maxdepth 5 \( \
    -name '*.pdb' \
    -o -name '*.xml' \
    -o -name 'web.config' \
    -o -name 'appsettings.Development.json' \
\) -delete

# Report exe size
EXE="$OUT_DIR/DeckFlow.Studio.exe"
if [[ -f "$EXE" ]]; then
    SZ_BYTES=$(stat -c%s "$EXE")
    SZ_MB=$(awk "BEGIN { printf \"%.1f\", $SZ_BYTES/1048576 }")
    echo "Artifact: $EXE  (${SZ_MB} MB)"
else
    echo "ERROR: DeckFlow.Studio.exe not found in output dir after publish." >&2
    exit 1
fi

# Zip the output dir
# Use PowerShell Compress-Archive (always available in a WSL→Windows context).
# zip(1) is not guaranteed to be installed in the WSL distro; PowerShell is always
# present at /mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe.
echo "Zipping to $ZIP_PATH ..."
[[ -f "$ZIP_PATH" ]] && rm -f "$ZIP_PATH"
ABS_OUT=$(realpath "$OUT_DIR")
ABS_ZIP=$(realpath --no-symlinks "$(dirname "$ZIP_PATH")")/$(basename "$ZIP_PATH")
# Convert WSL paths to Windows paths for PowerShell
WIN_SRC=$(wslpath -w "$ABS_OUT")
WIN_ZIP=$(wslpath -w "$ABS_ZIP")
PWSH="/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe"
"$PWSH" -NoProfile -Command "Compress-Archive -Path '$WIN_SRC\*' -DestinationPath '$WIN_ZIP' -Force"
ZIP_MB=$(awk "BEGIN { printf \"%.1f\", $(stat -c%s "$ZIP_PATH")/1048576 }")
echo "Zip:      $ZIP_PATH  (${ZIP_MB} MB)"
echo "Done. Distribute the zip or copy the folder to the target machine."
