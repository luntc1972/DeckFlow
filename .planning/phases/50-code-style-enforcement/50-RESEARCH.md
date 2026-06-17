# Phase 50: Code-Style Enforcement — ReSharper Reconciliation + PR Gate — Research

**Researched:** 2026-06-14
**Domain:** .NET code-style enforcement tooling (`dotnet format`), git diff line-range intersection, git hooks, CI gating, cross-platform bash (WSL + Windows Git-Bash)
**Confidence:** HIGH (core mechanics probed live on this repo with .NET SDK 10.0.301; key behaviors verified, not assumed)

## Summary

This phase needs no new packages and no new frameworks. The entire gate is buildable from tooling already present: `dotnet format` (ships with the .NET 10 SDK — verified `10.0.301` on this machine), `git diff --unified=0`, and POSIX `bash`. The one load-bearing mechanism — "fail only when the formatter's would-be changes land on PR-added/modified lines" — was probed live and works cleanly via `dotnet format ... --verify-no-changes --report <json>`: the JSON report emits a `FileChanges[]` array with a `LineNumber` per violation, and those line numbers are relative to the **current (original) file**, so they intersect directly with the `+new` line ranges parsed from `git diff --unified=0` hunk headers. No apply-then-revert dance is required — `--verify-no-changes --report` is non-mutating and (per dotnet/format issue #1743) reports **correct** line numbers specifically in verify mode.

The carve-outs are safer than the SPEC fears. Three of the five carve-outs (own-line `[Attribute]`, raw-string indent, xmldoc indent) are expressed as `resharper_*` keys that **only JetBrains tools read** — `dotnet format` has no fixer for them, so the gate physically cannot break them. The remaining two (`{ get; init; }` preservation, switch-expression preference) were probed live: `dotnet format whitespace` never touches them, and `dotnet format style` on a real `init`-bearing record (`DeckFlow.Core/Models/DeckEntry.cs`) produced zero changes. The reconciliation risk is therefore concentrated in exactly one place: a ReSharper-exported `.editorconfig` key that, once merged, makes the formatter *want* to rewrite legacy code — which is precisely why D-01's line-level scoping (not file-level) is mandatory.

**Primary recommendation:** Build one shared bash script (`scripts/format-check-changed.sh`, D-04) that (1) computes the changed-line ranges for the current context (staged hunks for the hook, merge-base diff for CI), (2) runs `dotnet format <sln> --verify-no-changes --report <tmp.json>` scoped to the changed `.cs` files via `--include`, (3) parses the report's `LineNumber`s and the git hunk `+` ranges, and (4) exits non-zero only on intersection. Use the full MSBuild `dotnet format` (not `whitespace --folder`) so style rules are evaluated, accepting ~2-3s per file; the hook stays fast because it only includes staged `.cs` files. Deliver the hook as `.githooks/pre-commit` + a documented `git config core.hooksPath .githooks` opt-in.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Style source-of-truth | Repo config (`.editorconfig`) | — | Tool-agnostic; read by `dotnet format`, VS, Rider, ReSharper |
| Changed-line scoping logic | Shared bash script (`scripts/`) | — | D-04: one implementation, two invocation contexts |
| Local enforcement | Git hook (`.githooks/pre-commit`) | Shared script | Blocks commit pre-push; invokes the shared script on staged hunks |
| CI enforcement | GitHub Actions job (`format-gate`) | Shared script | D-05: parallel job, invokes the shared script on merge-base diff |
| Carve-out guard | xUnit test in existing test project | — | FMT-02: asserts 4 code carve-outs byte-identical after format |
| Reconciliation audit | `50-RECONCILIATION.md` (doc) | — | D-02/D-03: permanent record; RS export is throwaway |
| Formatter engine | `dotnet format` (SDK-bundled) | — | No new dependency; reads `.editorconfig` |

## Standard Stack

### Core
| Library / Tool | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `dotnet format` | bundled with SDK 10.0.301 `[VERIFIED: dotnet.exe --version + format --help on this repo]` | The formatter/verifier; reads `.editorconfig`, emits JSON report with per-line violations | First-party .NET tool; zero new dependency; this is the canonical changed-lines formatter for C# |
| `git` (diff `--unified=0`) | 2.43.0 `[VERIFIED: git --version]` | Source of changed-line ranges (staged + merge-base) | Already present; `--unified=0` gives exact added-line hunk boundaries |
| `bash` | POSIX | Hosts the shared diff-intersect script (D-04) | Cross-platform: WSL + Windows Git-Bash both ship bash |

### Supporting
| Tool | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xUnit | 2.9.3 (already in `DeckFlow.Core.Tests` / `DeckFlow.Web.Tests`) `[CITED: project CLAUDE.md Frameworks]` | Carve-out guard test (FMT-02) | Assert the 4 code carve-outs round-trip byte-identical |
| `jq` *(optional)* | n/a | Parse `--report` JSON | **AVOID adding** — not guaranteed on Windows Git-Bash; parse with `grep`/`sed`/`awk` instead (see Pitfall 4) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `--verify-no-changes --report` (non-mutating, report-driven) | apply-then-`git diff`-then-revert | Apply mode mutates the working tree (must stash/restore staged content — fragile in a pre-commit hook) AND per dotnet/format#1743 reports wrong line numbers in apply mode. Report mode is strictly better. |
| Full `dotnet format <sln>` (MSBuild) | `dotnet format whitespace --folder` (no MSBuild, <1s) `[VERIFIED: timed 0.9s vs 2.4s]` | Folder/whitespace mode is ~2.5x faster but **only runs whitespace rules** — it ignores style rules in `.editorconfig` (e.g. `var` prefs, brace style). If the reconciled RS keys add style rules, whitespace mode would silently not enforce them. Use full mode for correctness; speed is acceptable because only changed files are `--include`d. |
| `.githooks/` + `core.hooksPath` opt-in | Husky.Net / pre-commit framework | Both are new dependencies (forbidden without approval) and add a managed-tool layer. Native `core.hooksPath` is zero-dependency and cross-platform. |

**Installation:** None. `dotnet format` is part of the installed SDK; `git` and `bash` are present. **No new NuGet/npm packages required** — satisfies the "no new packages" constraint.

**Version verification (performed this session):**
```
dotnet --version           -> 10.0.301
dotnet format --help       -> supports --include, --exclude, --verify-no-changes, --report,
                              and subcommands whitespace / style / analyzers
dotnet format whitespace --help -> additionally supports --folder
git --version              -> 2.43.0
```

## Package Legitimacy Audit

> **Not applicable.** This phase installs **zero external packages**. All tooling (`dotnet format`, `git`, `bash`, xUnit) is already present in the SDK or the solution. No registry lookups, no slopcheck needed. If a planner later proposes adding `jq`, `husky`, or any pre-commit framework, that is a new dependency and must be flagged for operator approval per CLAUDE.md.

## Architecture Patterns

### System Architecture Diagram

```
                       ┌─────────────────────────────────────────────┐
                       │  scripts/format-check-changed.sh  (D-04)     │
                       │  ONE shared diff-intersect implementation    │
                       └─────────────────────────────────────────────┘
                              ▲                          ▲
          invoked by         │                          │        invoked by
          (staged context)   │                          │        (PR-base context)
                              │                          │
   ┌──────────────────────────┴───┐        ┌─────────────┴────────────────────┐
   │ .githooks/pre-commit         │        │ ci.yml  job: format-gate (D-05)   │
   │ (local, before commit)       │        │ runs on push + pull_request       │
   │ MODE=staged                  │        │ MODE=merge-base, fetch-depth: 0   │
   └──────────────────────────────┘        └───────────────────────────────────┘

   Inside the shared script:
   ┌────────────────────────────────────────────────────────────────────────┐
   │ 1. Determine changed-line ranges                                         │
   │    staged:     git diff --cached --unified=0 -- '*.cs'                    │
   │    merge-base: BASE=$(git merge-base <base-branch> HEAD)                  │
   │                git diff --unified=0 "$BASE"...HEAD -- '*.cs'              │
   │       → parse @@ -old +newStart[,newCount] @@  → set of (file, lineset)   │
   │                                                                          │
   │ 2. Collect changed .cs file list → pass to --include                     │
   │                                                                          │
   │ 3. dotnet format DeckFlow.sln --include <files> \                        │
   │       --verify-no-changes --report <tmp.json> --no-restore               │
   │       (non-mutating; JSON lists FileChanges[].LineNumber in ORIG file)   │
   │                                                                          │
   │ 4. INTERSECT report LineNumbers ∩ changed-line ranges per file           │
   │       intersection non-empty → exit 1 (block); empty → exit 0 (pass)     │
   └────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
                       reads .editorconfig (carve-outs intact)
```

A reader can trace the primary case: a developer stages a mis-formatted added line → `pre-commit` runs the shared script in `staged` mode → script finds the violation's `LineNumber` lands inside a staged `+` hunk → exit 1 → commit blocked. The same script in CI, against the merge-base diff, blocks the PR.

### Recommended Project Structure
```
.editorconfig                          # reconciliation target (carve-outs lines 45–82)
.githooks/
└── pre-commit                         # versioned hook; invokes shared script (MODE=staged)
scripts/
└── format-check-changed.sh            # D-04 shared diff-intersect logic
.github/workflows/
└── ci.yml                             # + new parallel job: format-gate (D-05)
<test project>/
└── CarveOutGuardTests.cs              # FMT-02 byte-identical assertions
.planning/phases/50-code-style-enforcement/
└── 50-RECONCILIATION.md               # D-02/D-03 permanent audit trail
CLAUDE.md                              # rewrite Formatting rule → source-of-truth model
```

### Pattern 1: Report-driven changed-line intersection (the load-bearing mechanism)
**What:** Run the formatter in **verify** mode with a JSON report; never mutate the tree. Intersect the report's line numbers with the diff's added lines.
**When to use:** Always — this is D-01's "true line-level diff-intersect."
**Verified report shape (probed live this session on a mis-formatted scratch file):**
```jsonc
// Source: dotnet format whitespace --verify-no-changes --report  (live probe, SDK 10.0.301)
[
  {
    "FilePath": "C:\\...\\Bad.cs",
    "FileChanges": [
      { "LineNumber": 5, "CharNumber": 19, "DiagnosticId": "WHITESPACE",
        "FormatDescription": "Fix whitespace formatting. Insert '\\s'." },
      { "LineNumber": 6, "CharNumber": 5,  "DiagnosticId": "WHITESPACE",
        "FormatDescription": "Fix whitespace formatting. Delete 4 characters." }
    ]
  }
]
```
`LineNumber` is 1-based and relative to the **original** file content (verified: the numbers matched the pre-format file, and dotnet/format#1743 confirms verify-mode line numbers are correct). The report is written even when violations exist; exit code is non-zero. An empty/clean file produces an empty report (probed: clean `DeckEntry.cs` → empty report, exit 0).

### Pattern 2: Parsing `git diff --unified=0` added-line ranges
**What:** Extract the set of added/modified line numbers per file from hunk headers.
**Verified hunk-header forms (live on this repo):**
```
@@ -5,0 +5 @@        → added 1 line at new-line 5            (count omitted = 1)
@@ -55,4 +58,13 @@   → 13 new lines starting at new-line 58
@@ -1 +0,0 @@        → pure deletion (new count 0) → SKIP (no added lines)
```
Parse rule: from `+newStart[,newCount]`, if `newCount == 0` skip; else the changed lines are `newStart .. newStart+newCount-1`. These `new`-side numbers are exactly what the verify-report `LineNumber` must be compared against (both index the post-edit / current file).
```bash
# Source: live probe; git 2.43.0
# staged context:
git diff --cached --unified=0 -- '*.cs'
# CI / merge-base context:
BASE=$(git merge-base "$BASE_REF" HEAD)
git diff --unified=0 "$BASE"...HEAD -- '*.cs'
```

### Pattern 3: One script, two contexts (D-04)
Drive context via an argument/env (`MODE=staged|ci`). The only difference is **how changed lines are computed** (cached hunks vs merge-base diff); steps 2–4 are identical. This is the DRY guarantee — line-scoping logic cannot drift between hook and CI.

### Anti-Patterns to Avoid
- **Apply-then-revert in the hook:** mutating a partially-staged working tree risks clobbering unstaged edits and trips dotnet/format#1743's wrong-line-number bug. Use `--verify-no-changes --report` instead.
- **`--verify-no-changes` on whole changed files without line intersection (file-level):** REJECTED by D-01 — a one-line edit in a legacy file with a pre-existing (newly-non-conformant) quirk would false-fail. The SPEC REQ-3 acceptance literally requires that PR to PASS.
- **`whitespace --folder` as the gate engine:** fast but skips style rules — would silently under-enforce any style key adopted from ReSharper.
- **Two-dot (`..`) instead of three-dot (`...`) for the CI base diff:** three-dot diffs against the merge-base (what the PR actually introduces), avoiding noise from base-branch commits the PR didn't make. Use `"$BASE"...HEAD`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Detecting which lines a formatter would change | A custom C# tokenizer / reformatter | `dotnet format --verify-no-changes --report` | The SDK formatter already knows every `.editorconfig` rule incl. the carve-outs; rolling your own desyncs from the config |
| Mapping diffs to line ranges | A regex over `git diff` default context | `git diff --unified=0` hunk headers | `--unified=0` removes context lines so `+start,count` is exactly the added set; no off-by-context errors |
| Hook management | A bespoke installer that writes into `.git/hooks` | `.githooks/` dir + `git config core.hooksPath` | Versioned, reviewable, no copy step that drifts; works identically on WSL + Windows Git-Bash |
| Carve-out regression detection | Manual eyeballing of diffs | xUnit test that formats a fixture string and asserts equality | Deterministic, runs in CI, fails loudly if a future config edit weakens a carve-out |

**Key insight:** Three of five carve-outs (`resharper_*` keys for attribute placement, raw-string indent, xmldoc indent) are **invisible to `dotnet format`** — it has no Roslyn fixer for them. The gate cannot break what it cannot rewrite. So the only carve-outs the gate could theoretically threaten are `init` and switch-expression — both verified inert under `dotnet format style` against a real record file. The guard test (FMT-02) is therefore a *regression tripwire on `.editorconfig` edits*, not a defense against the formatter itself.

## Runtime State Inventory

> This is a config/CI/doc phase, not a rename/refactor. Most categories are empty, stated explicitly.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — gate touches no database. Verified: phase only edits `.editorconfig`, `ci.yml`, hook, scripts, docs, `CLAUDE.md`, + one test file. | none |
| Live service config | **GitHub Actions workflow** (`ci.yml`) — lives in git, not a hidden UI. Adding `format-gate` job is a tracked code change. **GitHub branch protection** *may* reference required status check names — if the operator has branch protection requiring named checks, the new `format-gate` job name must be added there (UI/API, not git). | Verify branch-protection required-checks after first CI run; add `format-gate` if protection is on |
| OS-registered state | **`git config core.hooksPath`** — currently default (`.git/hooks`, verified `core.hooksPath` unset → returns default path). The hook is OPT-IN: each clone/dev must run `git config core.hooksPath .githooks` once. This is per-clone local git config, not committed. | Document the one-time opt-in in README/CLAUDE.md; CI does not need the hook (it runs the script directly) |
| Secrets/env vars | None — no secrets involved. | none |
| Build artifacts | None — no compiled output changes; `dotnet format` does not emit artifacts. The probe created `_fmtprobe*` scratch dirs which were **deleted** (verified clean: `git status` shows no fmtprobe residue). | none |

**The canonical question — after every file is updated, what runtime state still holds old behavior?** Only two: (1) each developer's local `core.hooksPath` must be set once for the hook to fire (opt-in by design, per D-05's discretion); (2) GitHub branch-protection required-check names (if configured) live outside git and would need the new job name added. Both are documented above.

## Common Pitfalls

### Pitfall 1: Windows `dotnet.exe` cannot resolve WSL `/tmp` paths
**What goes wrong:** `dotnet format --folder /tmp/xyz` throws `Folder '/tmp/...' does not exist`. Probed live — the Windows-native `dotnet.exe` invoked from WSL sees a Linux path it can't map.
**Why it happens:** This repo runs the **Windows** .NET SDK (`/mnt/c/Program Files/dotnet/dotnet.exe`) even under WSL; it needs Windows-accessible paths.
**How to avoid:** Keep all formatter inputs/outputs (the report JSON, any scratch) **inside the repo tree** (a Windows-visible `/mnt/c/...` path) rather than `/tmp`. Use a repo-relative temp file (e.g. `./.git/format-report.json` or `mktemp` redirected to a repo-local dir) and `--include` with repo-relative paths.
**Warning signs:** `Folder '...' does not exist` or empty reports despite known violations.

### Pitfall 2: File-level verify false-fails legacy files
**What goes wrong:** Touching one line in a quirky legacy file fails the gate because the *whole file* is non-conformant under a newly-adopted RS rule.
**Why it happens:** `--verify-no-changes` reports all violations in the file, not just on changed lines.
**How to avoid:** Always intersect report `LineNumber`s with the diff's added-line set (D-01). This is the entire reason the phase mandates line-level, not file-level.
**Warning signs:** The SPEC REQ-3 "legacy file passes" acceptance test fails.

### Pitfall 3: CI checkout has no history to diff against
**What goes wrong:** `git merge-base` fails or returns nothing because `actions/checkout` defaults to a shallow `fetch-depth: 1`.
**Why it happens:** Default shallow clone lacks the base branch and merge-base commit.
**How to avoid:** In the `format-gate` job, set `actions/checkout` `with: fetch-depth: 0` (full history). For `pull_request` events, diff against `origin/${{ github.base_ref }}`; for `push` events, diff against the merge-base with the default branch (or `${{ github.event.before }}` when valid). Verified locally: `git merge-base origin/main HEAD` resolves correctly with full history.
**Warning signs:** `fatal: Not a valid object name` / empty merge-base in CI logs.

### Pitfall 4: `jq` may be absent on Windows Git-Bash
**What goes wrong:** Parsing the report JSON with `jq` works in WSL/Ubuntu-CI but fails on a developer's Windows Git-Bash where `jq` isn't installed.
**Why it happens:** `jq` is not bundled with Git for Windows.
**How to avoid:** Parse the report with POSIX text tools (`grep -oE '"LineNumber": *[0-9]+'` then `sed`/`awk`), or — cleaner — group violations by file from the report and compare numerically in bash. Do **not** add `jq` as a dependency (would require operator approval).
**Warning signs:** `jq: command not found` on a Windows contributor's machine.

### Pitfall 5: EOL normalization on `.ps1/.bat/.cmd` carve-out files
**What goes wrong:** A hook that runs a broad formatter or `git add -A` could flip CRLF→LF on the Windows-script exception files.
**Why it happens:** `.gitattributes` pins `*.ps1/*.bat/*.cmd` to `eol=crlf`; a naive formatter or re-stage could fight that.
**How to avoid:** The gate **only** processes `*.cs` (filter `-- '*.cs'` in the diff and `--include` only `.cs`). `dotnet format` never touches `.ps1/.bat/.cmd`. The hook must not run any global `git add`/normalization. Verified scope: carve-out EOL files are outside the formatter's reach entirely.
**Warning signs:** Whitespace-only diff churn on `*.ps1/*.bat/*.cmd`.

### Pitfall 6: `whitespace` vs `style` — silent under-enforcement
**What goes wrong:** Using `dotnet format whitespace` (or `--folder`) skips code-style rules, so an adopted ReSharper style key (e.g. a `var` preference) is never enforced.
**Why it happens:** The `whitespace` subcommand only runs whitespace fixers `[CITED: learn.microsoft.com/dotnet/core/tools/dotnet-format]`.
**How to avoid:** Run full `dotnet format <sln> --include ...` (whitespace + style + analyzers) for the gate, OR explicitly run both `whitespace` and `style` subcommands. Confirm with the operator which rule classes the reconciled config introduces.
**Warning signs:** A deliberately style-violating (non-whitespace) added line passes the gate.

## Code Examples

### Extract changed-line ranges (staged + merge-base)
```bash
# Source: live-verified hunk formats on this repo (git 2.43.0)
# Emits "file:start:count" per added hunk; count 0 (pure deletion) is skipped.
emit_changed_ranges() {        # $1 = diff command output on stdin
  awk '
    /^\+\+\+ b\// { file = substr($0, 7); next }
    /^@@ / {
      # @@ -old +newStart[,newCount] @@
      plus = $3                      # like +58,13  or +5
      sub(/^\+/, "", plus)
      n = split(plus, a, ",")
      start = a[1]; count = (n > 1 ? a[2] : 1)
      if (count > 0) print file ":" start ":" count
    }'
}
# staged:     git diff --cached --unified=0 -- "*.cs" | emit_changed_ranges
# merge-base: git diff --unified=0 "$BASE"...HEAD -- "*.cs" | emit_changed_ranges
```

### Run the formatter in non-mutating verify mode with a report
```bash
# Source: live probe, SDK 10.0.301. Report path kept INSIDE the repo (Pitfall 1).
REPORT="./.git/format-report.json"     # repo-local, Windows-visible
dotnet format DeckFlow.sln \
  --include $CHANGED_CS_FILES \
  --verify-no-changes \
  --report "$REPORT" \
  --no-restore                          # speed; CI restores once in build job
# exit code non-zero ⇒ violations exist; $REPORT lists them with LineNumber.
```

### Carve-out guard test (FMT-02) — shape
```csharp
// Source: pattern, not copied API. xUnit already present in DeckFlow.Core.Tests.
// Format a fixture string with the reconciled .editorconfig and assert byte-identity.
// Four cases: { get; init; }, a raw-string literal, an own-line [Attribute], a switch expression.
[Fact]
public void InitAccessor_SurvivesFormatting_ByteIdentical()
{
    const string fixture = "public string Name { get; init; } = \"\";";
    var formatted = RunDotnetFormatOnSnippet(fixture);   // helper shells out to dotnet format
    Assert.Equal(fixture, formatted);                    // byte-for-byte
}
```
*Note:* the cleanest implementation shells out to `dotnet format whitespace --folder` on a temp `.cs` file written into a repo-local temp dir (Pitfall 1) carrying a copy of `.editorconfig`, then reads the file back. An in-memory Roslyn `Formatter` call is an alternative but pulls in analyzer wiring; the shell-out matches what the gate actually does and is the higher-fidelity assertion.

### CI job skeleton (D-05)
```yaml
# Source: pattern; mirrors existing ci.yml triggers (push + pull_request)
  format-gate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
        with:
          fetch-depth: 0                      # Pitfall 3: need history for merge-base
      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '10.0.x'
      - name: Restore (format needs project graph for style rules)
        run: dotnet restore DeckFlow.sln
      - name: Changed-lines format gate
        run: bash scripts/format-check-changed.sh ci
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `dotnet-format` as a separate global tool | `dotnet format` built into the SDK | .NET 6+ | No `dotnet tool install` needed; ships with SDK 10.0.301 here |
| Whole-file `dotnet format` in CI | Scoped `--include <changed files>` + line intersection | ongoing community practice | Enables changed-lines-only gating without mass reflow |
| Husky/pre-commit framework for hooks | Native `core.hooksPath` versioned hooks | git 2.9+ | Zero-dependency, cross-platform hook delivery |

**Deprecated/outdated:**
- The standalone `dotnet-format` NuGet/global tool — superseded by the SDK-bundled `dotnet format`. Do not install it.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The reconciled `.editorconfig` will introduce at least one *style* rule (not just whitespace), so the gate should use full `dotnet format`, not `whitespace --folder`. | Standard Stack / Pitfall 6 | If the RS export adds only whitespace keys, full mode is slower than necessary but still correct — low risk. Confirm at reconciliation time (D-02). |
| A2 | Operator has (or may add) GitHub branch protection requiring named status checks. | Runtime State Inventory | If branch protection isn't configured, the `format-gate` job still runs but isn't *required* to merge — the gate is advisory until added. Verify with operator. |
| A3 | The xUnit guard test (FMT-02) shells out to `dotnet format`; CI/WSL can invoke the Windows `dotnet.exe` from the test host. | Code Examples | Project CLAUDE.md notes "VSTest unreliable in WSL" — the guard test may need to run in CI (push-and-watch) rather than locally. Planner should target CI execution for FMT-02. |
| A4 | `git diff --unified=0` hunk `+`-side numbers and `dotnet format --report` `LineNumber`s both index the same (current) file content and are directly comparable. | Pattern 1 / Pattern 2 | Verified: report numbers matched the pre-format file in the live probe; dotnet/format#1743 confirms verify-mode line correctness. Low risk. |

## Open Questions

1. **Which rule classes does the operator's ReSharper export actually add?**
   - What we know: `dotnet format` reads `dotnet_*`/`csharp_*` keys; ignores `resharper_*`.
   - What's unclear: whether the RS "Export to .editorconfig" emits `dotnet_*` style keys the gate will enforce, or mostly `resharper_*` keys the gate ignores. This determines whether the gate needs `style` mode at all.
   - Recommendation: Run reconciliation (D-02) first; categorize each exported key as gate-enforceable (`dotnet_*`/`csharp_*`) vs JetBrains-only (`resharper_*`). The reconciliation report should note enforceability per key.

2. **Does FMT-02's guard test run locally or CI-only?**
   - What we know: CLAUDE.md flags VSTest as unreliable in WSL.
   - What's unclear: whether the shell-out guard test is stable enough to run in the local pre-commit path.
   - Recommendation: Make FMT-02 a CI-executed test (push-and-watch); do not gate the pre-commit hook on it.

3. **Base ref for `push` events (non-PR) in CI.**
   - What we know: `pull_request` gives `github.base_ref`; `push` does not.
   - What's unclear: cleanest base for direct pushes (feature branches without a PR).
   - Recommendation: For `push`, diff against merge-base with the default branch (`origin/main`), or use `github.event.before..github.sha` when `before` is a valid (non-zero) SHA. Document the chosen rule in the script.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK / `dotnet format` | The gate engine | ✓ | 10.0.301 | — (none needed) |
| `git` (`diff --unified=0`, `merge-base`) | Changed-line ranges | ✓ | 2.43.0 | — |
| `bash` | Shared script (D-04) | ✓ (WSL + Git-Bash) | POSIX | — |
| `awk`/`grep`/`sed` | Report + hunk parsing | ✓ | coreutils / busybox | — |
| `jq` | (optional JSON parse) | ✗ on Windows Git-Bash | — | Parse with grep/sed/awk (Pitfall 4) — DO NOT add jq |
| GitHub Actions runner | CI gate (D-05) | ✓ | `ubuntu-latest` | — |

**Missing dependencies with no fallback:** None.
**Missing dependencies with fallback:** `jq` — use POSIX text tools instead (no dependency added).

## Validation Architecture

> `workflow.nyquist_validation` is `true` in `.planning/config.json` → section included.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`) |
| Config file | per-project `.csproj`; no global runsettings |
| Quick run command | `dotnet test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Release --no-build` |
| Full suite command | `dotnet test DeckFlow.sln -c Release --no-build` |

> Caveat (CLAUDE.md): VSTest is unreliable in WSL — prefer `dotnet build` clean + **push-and-watch CI** for test verification. FMT-02's guard test should be CI-verified.

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| FMT-01 | RS prefs merged into `.editorconfig`, carve-outs win | manual review + diff | inspect `50-RECONCILIATION.md` + `git diff .editorconfig` | ❌ doc/process (no unit test) |
| FMT-02 | 4 code carve-outs byte-identical after format | unit (xUnit) | `dotnet test --filter CarveOutGuard` | ❌ Wave 0 — new `CarveOutGuardTests.cs` |
| FMT-03 | CI fails mis-formatted added line, passes clean legacy edit | integration (live CI) | two test PRs (mis-format / legacy-only) + observe CI | ❌ Wave 0 — proven via test PRs, not a unit test |
| FMT-04 | Hook blocks mis-formatted staged commit, allows clean | manual / scripted | stage bad line → attempt commit → expect block | ❌ Wave 0 — scripted manual proof |
| FMT-05 | CLAUDE.md rewritten to source-of-truth model | doc review | grep that blanket prohibition is gone, carve-outs retained | ❌ doc check |

### Sampling Rate
- **Per task commit:** run `scripts/format-check-changed.sh staged` (dogfoods the gate on its own changes).
- **Per wave merge:** full `dotnet build DeckFlow.sln -c Release` clean + `dotnet test` (CI).
- **Phase gate:** both test PRs (FMT-03) green/red as expected; hook block/allow proven (FMT-04); CI overall green with `format-gate` present.

### Wave 0 Gaps
- [ ] `scripts/format-check-changed.sh` — the shared diff-intersect engine (D-04); blocks FMT-03/04.
- [ ] `.githooks/pre-commit` — invokes the script in staged mode (FMT-04).
- [ ] `<test project>/CarveOutGuardTests.cs` — 4 byte-identity assertions (FMT-02).
- [ ] `ci.yml` `format-gate` job (D-05) — FMT-03.
- [ ] `50-RECONCILIATION.md` — FMT-01 audit (D-02/D-03).
- [ ] Two scratch test PRs (one mis-formatted added line; one legacy-file one-line edit) to prove both directions of FMT-03.

## Security Domain

> `security_enforcement` not present in `.planning/config.json`; treated as enabled. This is a CI/tooling/doc phase with no application attack surface, so most ASVS categories are N/A. The one real consideration is supply-chain / CI execution safety.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | gate runs no auth |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | minimal | script parses `git diff`/report output — treat file paths from diff as untrusted strings (quote in bash, avoid `eval`) |
| V6 Cryptography | no | — |
| V10/V14 (CI / build integrity) | yes | no new dependency; `format-gate` job adds no secrets; runs the same SDK already trusted by `build-and-test` |

### Known Threat Patterns for {CI bash gate}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Shell injection via crafted file path in a PR (filename with `;`/`$()`) | Tampering / EoP | Quote all `"$file"` expansions; never `eval`; pass file lists to `--include` as separate args, not a joined string |
| Hook bypass (`--no-verify`) gives false confidence | Repudiation | The hook is convenience; **CI `format-gate` is the authoritative gate** — do not rely on the hook alone (D-05 makes CI the enforcer) |
| New dependency as supply-chain vector | — | None added; `jq`/husky explicitly avoided |

## Sources

### Primary (HIGH confidence)
- **Live probes on this repo (SDK 10.0.301, git 2.43.0):** `dotnet format --help`, `whitespace --help`, `--verify-no-changes --report` JSON shape, carve-out survival on `DeckEntry.cs` + scratch fixtures, `git diff --unified=0` hunk forms, `git merge-base origin/main HEAD`, WSL `/tmp` path failure, folder-vs-MSBuild timing.
- Project files read: `.editorconfig`, `.gitattributes`, `ci.yml`, `CLAUDE.md`, `50-SPEC.md`, `50-CONTEXT.md`.

### Secondary (MEDIUM confidence)
- [dotnet format command — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format) — subcommand scope (whitespace vs style), `--report`, `--include`.
- [dotnet/format issue #1743](https://github.com/dotnet/format/issues/1743) — verify-mode reports correct line numbers (apply-mode does not).
- [dotnet/format issue #2098](https://github.com/dotnet/format/issues/2098) — report-only / no-mutation usage.

### Tertiary (LOW confidence)
- General community guidance on changed-lines gating (Code Maze, MegaLinter descriptors) — corroborated against live probes before stating as fact.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every tool version verified live; zero new packages.
- Diff-intersect mechanism: HIGH — report JSON shape + line semantics + hunk parsing all probed on this repo.
- Carve-out safety: HIGH — `resharper_*` keys proven invisible to `dotnet format`; `init`/switch verified inert under `style`.
- Hook delivery: MEDIUM — `.githooks` + `core.hooksPath` is standard and cross-platform, but exact Windows Git-Bash invocation of `dotnet.exe` from the hook should be smoke-tested during execution.
- CI base-ref handling: MEDIUM — merge-base works locally; `push`-event base selection (A2/Open Q3) needs a documented rule.

**Research date:** 2026-06-14
**Valid until:** 2026-07-14 (stable tooling; `dotnet format` behavior is mature — 30 days)
