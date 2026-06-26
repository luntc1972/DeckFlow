#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "usage: $0 <staged|ci>" >&2
  exit 2
}

infra_fail() {
  echo "INFRA: $*" >&2
  exit 2
}

resolve_dotnet() {
  if command -v dotnet >/dev/null 2>&1; then
    command -v dotnet
    return 0
  fi

  if command -v dotnet.exe >/dev/null 2>&1; then
    command -v dotnet.exe
    return 0
  fi

  if [ -x "/mnt/c/Program Files/dotnet/dotnet.exe" ]; then
    printf '%s\n' "/mnt/c/Program Files/dotnet/dotnet.exe"
    return 0
  fi

  infra_fail "dotnet executable not found"
}

canonicalize_absolute_path() {
  local path="$1"

  path="${path//$'\r'/}"
  path="${path//\\//}"

  if [[ "$path" =~ ^([A-Za-z]):/(.*)$ ]]; then
    local drive="${BASH_REMATCH[1],,}"
    path="/mnt/$drive/${BASH_REMATCH[2]}"
  elif [[ "$path" =~ ^/([A-Za-z])/(.*)$ ]]; then
    local drive="${BASH_REMATCH[1],,}"
    path="/mnt/$drive/${BASH_REMATCH[2]}"
  fi

  path="${path%/}"
  printf '%s\n' "$path"
}

normalize_repo_relative_path() {
  local path="$1"
  local canonical

  canonical="$(canonicalize_absolute_path "$path")"

  if [[ "$canonical" == "$ROOT/"* ]]; then
    printf '%s\n' "${canonical#"$ROOT"/}"
    return 0
  fi

  infra_fail "report path outside repo root: $path"
}

normalize_diff_path() {
  local path="$1"

  if [[ "$path" == \"* ]]; then
    infra_fail "unsupported/quoted filename in diff: $path"
  fi

  path="${path#b/}"
  path="${path#a/}"
  path="${path//\\//}"

  if [[ "$path" == /* ]] || [[ "$path" =~ ^[A-Za-z]:/ ]] || [[ "$path" =~ ^/[A-Za-z]/ ]]; then
    normalize_repo_relative_path "$path"
    return 0
  fi

  printf '%s\n' "$path"
}

is_valid_commit_ref() {
  local ref="$1"
  [ -n "$ref" ] && git cat-file -e "$ref^{commit}" >/dev/null 2>&1
}

select_ci_diff_args() {
  local zero_sha="0000000000000000000000000000000000000000"
  local before="${GITHUB_EVENT_BEFORE:-}"
  local integration_ref="origin/main"

  if ! is_valid_commit_ref HEAD; then
    infra_fail "HEAD is not a valid commit"
  fi

  local head_sha
  head_sha="$(git rev-parse HEAD)"

  if [ -n "${GITHUB_BASE_REF:-}" ]; then
    local pr_base="origin/${GITHUB_BASE_REF}"
    echo "format-gate base: $pr_base (pull_request origin/${GITHUB_BASE_REF})"
    DIFF_MODE="three-dot"
    DIFF_BASE="$pr_base"
    return 0
  fi

  if [ -n "$before" ] && [ "$before" != "$zero_sha" ] && is_valid_commit_ref "$before"; then
    local before_sha
    before_sha="$(git rev-parse "$before")"
    if [ "$before_sha" != "$head_sha" ]; then
      echo "format-gate base: $before (push github.event.before)"
      DIFF_MODE="two-dot"
      DIFF_BASE="$before"
      return 0
    fi
  fi

  if is_valid_commit_ref "$integration_ref"; then
    local merge_base
    local merge_base_sha

    merge_base="$(git merge-base "$integration_ref" HEAD 2>/dev/null || true)"
    if [ -n "$merge_base" ] && is_valid_commit_ref "$merge_base"; then
      merge_base_sha="$(git rev-parse "$merge_base")"
      if [ "$merge_base_sha" = "$head_sha" ]; then
        echo "format-gate base: $integration_ref (push origin/main merge-base == HEAD; HEAD already in main history; empty diff is correct)"
      else
        echo "format-gate base: $integration_ref (push origin/main merge-base $merge_base_sha)"
      fi
      DIFF_MODE="three-dot"
      DIFF_BASE="$integration_ref"
      return 0
    fi
  else
    echo "INFRA: $integration_ref is unavailable; using empty-tree fallback so this push is not silently skipped" >&2
  fi

  local empty_tree
  empty_tree="$(git hash-object -t tree /dev/null)"
  echo "format-gate base: $empty_tree (empty-tree sentinel; last resort because $integration_ref could not be resolved)"
  DIFF_MODE="empty-tree"
  DIFF_BASE="$empty_tree"
}

build_ci_diff() {
  case "$DIFF_MODE" in
    three-dot)
      git diff --unified=0 "$DIFF_BASE"...HEAD -- '*.cs'
      ;;
    two-dot|empty-tree)
      git diff --unified=0 "$DIFF_BASE" HEAD -- '*.cs'
      ;;
    *)
      infra_fail "unknown diff mode: $DIFF_MODE"
      ;;
  esac
}

collect_changed_lines() {
  local diff_file="$1"
  local current_file=""
  local line=""

  : > "$CHANGED_LINES_FILE"

  while IFS= read -r line; do
    if [[ "$line" == '+++ '* ]]; then
      local path="${line#+++ }"

      if [ "$path" = "/dev/null" ]; then
        current_file=""
        continue
      fi

      current_file="$(normalize_diff_path "$path")"
      continue
    fi

    if [[ "$line" =~ ^@@\ [^+]*\+([0-9]+)(,([0-9]+))?\ @@ ]]; then
      local start="${BASH_REMATCH[1]}"
      local count="${BASH_REMATCH[3]:-1}"
      local end
      local number

      if [ "$count" -eq 0 ]; then
        continue
      fi

      if [ -z "$current_file" ]; then
        infra_fail "diff hunk encountered before file header"
      fi

      end=$((start + count - 1))
      for ((number = start; number <= end; number += 1)); do
        printf '%s\t%s\n' "$current_file" "$number" >> "$CHANGED_LINES_FILE"
      done
      continue
    fi
  done < "$diff_file"
}

extract_changed_files() {
  local changed_file

  CHANGED_FILES=()
  while IFS= read -r changed_file; do
    CHANGED_FILES+=("$changed_file")
  done < <(awk -F '\t' '!seen[$1]++ { print $1 }' "$CHANGED_LINES_FILE")
}

parse_report_and_intersections() {
  local saw_any_token=0
  local current_file=""
  local token=""

  : > "$INTERSECTIONS_FILE"

  grep -Eq '^[[:space:]]*\[' "$REPORT" || infra_fail "format report missing opening array"
  grep -Eq '\][[:space:]]*$' "$REPORT" || infra_fail "format report missing closing array"

  grep -oE '"FilePath"[[:space:]]*:[[:space:]]*"([^"\\]|\\.)*"|"LineNumber"[[:space:]]*:[[:space:]]*[0-9]+' "$REPORT" > "$TOKENS_FILE" || true

  while IFS= read -r token; do
    saw_any_token=1

    if [[ "$token" == \"FilePath\"* ]]; then
      local raw_path
      raw_path="$(printf '%s\n' "$token" | sed -E 's/^"FilePath"[[:space:]]*:[[:space:]]*"(([^"\\]|\\.)*)"$/\1/')"
      raw_path="${raw_path//\\\\/\\}"
      raw_path="${raw_path//\\\//\/}"
      current_file="$(normalize_repo_relative_path "$raw_path")"
      continue
    fi

    if [[ "$token" == \"LineNumber\"* ]]; then
      local line_number

      [ -n "$current_file" ] || infra_fail "malformed format report: LineNumber without FilePath"
      line_number="$(printf '%s\n' "$token" | sed -E 's/^"LineNumber"[[:space:]]*:[[:space:]]*([0-9]+)$/\1/')"

      if grep -Fxq -- "$current_file"$'\t'"$line_number" "$CHANGED_LINES_FILE"; then
        printf '%s:%s\n' "$current_file" "$line_number" >> "$INTERSECTIONS_FILE"
      fi
    fi
  done < "$TOKENS_FILE"

  if [ "$saw_any_token" -eq 0 ]; then
    local compact
    compact="$(tr -d '[:space:]' < "$REPORT")"
    [ "$compact" = "[]" ] || infra_fail "malformed format report: no parseable FilePath/LineNumber tokens"
  fi
}

[ "$#" -eq 1 ] || usage
MODE="$1"
[ "$MODE" = "staged" ] || [ "$MODE" = "ci" ] || usage

ROOT_RAW="$(git rev-parse --show-toplevel)"
ROOT="$(canonicalize_absolute_path "$ROOT_RAW")"
ARTIFACTS_DIR="$ROOT/artifacts"
REPORT_REL="./artifacts/format-report.json"
REPORT="$ARTIFACTS_DIR/format-report.json"
DIFF_FILE="$ARTIFACTS_DIR/format-diff.txt"
CHANGED_LINES_FILE="$ARTIFACTS_DIR/format-changed-lines.tsv"
TOKENS_FILE="$ARTIFACTS_DIR/format-report.tokens"
INTERSECTIONS_FILE="$ARTIFACTS_DIR/format-intersections.txt"
DOTNET_BIN="$(resolve_dotnet)"

mkdir -p "$ARTIFACTS_DIR"
rm -f "$REPORT" "$DIFF_FILE" "$CHANGED_LINES_FILE" "$TOKENS_FILE" "$INTERSECTIONS_FILE"

cd "$ROOT"

if [ "$MODE" = "staged" ]; then
  git diff --cached --unified=0 -- '*.cs' > "$DIFF_FILE"
else
  select_ci_diff_args
  build_ci_diff > "$DIFF_FILE"
fi

if grep -Eq '^(---|\+\+\+) "' "$DIFF_FILE"; then
  infra_fail "unsupported/quoted filename in diff: git emitted a quoted path header"
fi

collect_changed_lines "$DIFF_FILE"
extract_changed_files

if [ "${#CHANGED_FILES[@]}" -eq 0 ]; then
  echo "no changed C# files"
  exit 0
fi

# Full dotnet format stays authoritative even though 50-01 only merged JetBrains-only keys,
# because this phase enforces style, not whitespace. Plan 50-03 must run the same mode.
set +e
"$DOTNET_BIN" format DeckFlow.sln \
  --include "${CHANGED_FILES[@]}" \
  --verify-no-changes \
  --report "$REPORT_REL" \
  --no-restore
status=$?
set -e

[ -r "$REPORT" ] || infra_fail "format report missing/unreadable — gate could not run"
[ -s "$REPORT" ] || infra_fail "format report empty — gate could not run"

parse_report_and_intersections

if [ -s "$INTERSECTIONS_FILE" ]; then
  sort -u "$INTERSECTIONS_FILE" | while IFS= read -r location; do
    echo "$location"
  done
  echo "run dotnet format <file> and re-stage only the changed lines"
  exit 1
fi

if [ "$status" -ne 0 ]; then
  echo "format check passed for changed lines; off-hunk violations ignored"
fi

exit 0
