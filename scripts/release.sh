#!/usr/bin/env bash
# Update DeckFlow.Web's tracked CalVer, create the release commit, and add the matching git tag.
#
# Usage:
#   bash scripts/release.sh 2026.07.6
set -euo pipefail

cd "$(dirname "$0")/.."

if [[ $# -ne 1 ]]; then
    echo "ERROR: expected exactly one version argument." >&2
    echo "Usage: bash scripts/release.sh YYYY.MM[.N]" >&2
    exit 1
fi

VERSION="$1"
CSPROJ="DeckFlow.Web/DeckFlow.Web.csproj"

if [[ ! "$VERSION" =~ ^[0-9]{4}\.(0[1-9]|1[0-2])(\.[0-9]+)?$ ]]; then
    echo "ERROR: version must match CalVer YYYY.MM or YYYY.MM.N (month must be two digits)." >&2
    exit 1
fi

if ! git diff --quiet --exit-code || ! git diff --cached --quiet --exit-code; then
    echo "ERROR: tracked git changes detected. Commit or stash tracked changes before releasing." >&2
    exit 1
fi

if git rev-parse -q --verify "refs/tags/$VERSION" >/dev/null; then
    echo "ERROR: git tag '$VERSION' already exists." >&2
    exit 1
fi

echo "Updating $CSPROJ to version $VERSION ..."
sed -E -i "s#(<Version>)[^<]+(</Version>)#\1$VERSION\2#" "$CSPROJ"

echo "Creating release commit ..."
git add "$CSPROJ"
git commit -m "chore(release): $VERSION"

echo "Creating git tag $VERSION ..."
# Annotated so `git push --follow-tags` (the hint below) actually pushes it.
git tag -a "$VERSION" -m "DeckFlow $VERSION"

echo "Tagged $VERSION. Now run: git push --follow-tags"
echo "The About page will show $VERSION after the next deploy."
