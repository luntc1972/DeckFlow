# 50-02 Summary

Status: `COMPLETE`

## Objective

Build the shared changed-lines formatting gate for C# only: one diff-intersect script used by both the versioned pre-commit hook and a separate CI `format-gate` job, with fail-closed handling for missing reports, quoted diff paths, unmappable report paths, and unsafe CI base selection.

## Files

- `scripts/format-check-changed.sh`
- `.githooks/pre-commit`
- `.github/workflows/ci.yml`
- `.planning/phases/50-code-style-enforcement/50-02-SUMMARY.md`

## Diff-intersect mechanism

- `scripts/format-check-changed.sh` accepts `staged` and `ci`.
- `staged` uses `git diff --cached --unified=0 -- '*.cs'`.
- `ci` uses:
  - `pull_request`: `origin/${GITHUB_BASE_REF}...HEAD`
  - `push`: valid `github.event.before` first, then merge-base with `origin/${GITHUB_REF_NAME}`, then merge-base with `origin/main`
  - any empty / zero-SHA / invalid / `BASE==HEAD` result falls back to the empty-tree sentinel and logs why
- The script parses `@@ -old +newStart[,count] @@` headers into changed current-file line numbers and exits early with `no changed C# files` when there are no added/modified `.cs` lines.
- Formatter mode is full `dotnet format DeckFlow.sln --verify-no-changes --report ./artifacts/format-report.json --no-restore` scoped with `--include <changed .cs files>`. This stays on full mode even though 50-01 only merged JetBrains-only keys, because the gate is meant to enforce style, not whitespace-only. Plan `50-03` must use the same mode.
- The formatter run is wrapped in a scoped `set +e; ...; status=$?; set -e`, and the script branches on the JSON report rather than the formatter exit code so legacy off-hunk violations do not false-fail a clean changed hunk.
- Report `FilePath` values are normalized by:
  - backslash to slash
  - Windows drive path to `/mnt/<drive>/...`
  - optional Git-Bash `/c/...` path to `/mnt/c/...`
  - stripping the canonicalized repo root to compare repo-relative forward-slash paths
- Unmappable report paths fail closed as infrastructure errors. C-quoted diff filenames also fail closed before hunk parsing.
- Intersections are computed as `FileChanges[].LineNumber` ∩ changed hunk line numbers. Only intersecting violations fail the gate, and failures print `file:line`.

## Hook and CI

- `.githooks/pre-commit` is executable, documents the one-time `git config core.hooksPath .githooks` opt-in, and runs `bash scripts/format-check-changed.sh staged`.
- `.github/workflows/ci.yml` now has a separate `format-gate` job parallel to `build-and-test`.
- `format-gate` uses `actions/checkout@v6` with `fetch-depth: 0`, restores `DeckFlow.sln`, and runs `bash scripts/format-check-changed.sh ci`.
- The CI run step passes `GITHUB_BASE_REF`, `GITHUB_REF_NAME`, and `GITHUB_EVENT_BEFORE` through `env:`. `pull_request_target` was not introduced.

## Smoke results

- Local `dotnet` resolution used `C:\Program Files\dotnet\dotnet.exe` via `/mnt/c/Program Files/dotnet/dotnet.exe`.
- Before smoke, `dotnet restore DeckFlow.sln` was run because the gate intentionally uses `--no-restore`.
- Fail direction: a staged temporary file `DeckFlow.Core/FormatGateSmokeTemp.cs` containing `public string Name { get; set; }="";` failed as expected. Output named `DeckFlow.Core/FormatGateSmokeTemp.cs:5`.
- Pass direction: after changing that staged line to `public string Name { get; set; } = "";`, `bash scripts/format-check-changed.sh staged` exited `0`.
- The temporary smoke file was then unstaged and deleted; it is not present in the final diff or commit.

## Threat-model notes

- No `jq`, Husky, pre-commit framework, or other new dependency was added.
- No `eval` is present; changed file paths are passed as argv items to `dotnet format --include`.
- The report stays under the already-ignored `artifacts/` directory. `.gitignore` was not edited.
- The script only scopes diffs and formatter includes to `*.cs`, so `.ps1/.bat/.cmd` EOL carve-outs remain untouched.

## Acceptance results

- Shared script exists, is executable, and passes `bash -n`.
- Hook exists, is executable, and invokes the shared script in staged mode.
- CI has a separate `format-gate` job; `build-and-test` was left intact.
- Base-ref selection is logged and guarded against empty / invalid / `BASE==HEAD` by empty-tree fallback.
- Missing/unreadable reports, quoted diff filenames, and report paths outside the repo fail closed as infrastructure errors.
- Behavioral proof completed locally for the staged hook path: misformatted added line fails; cleaned staged edit passes.
- `.gitignore` remained unchanged.
