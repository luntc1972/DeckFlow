---
phase: 46-review-queue-commit-publish-path
plan: "04"
subsystem: Studio
tags: [blazor, git, publish, seed-write, artifact-copy, diff, scoped-commit]
dependency_graph:
  requires:
    - 46-02 (IGitRepository, ExportIndexToFileAsync, CopyApprovedArtifactsToRepoAsync)
    - 46-03 (NavMenu /publish entry)
  provides:
    - Publish.razor (PUB-03 commit-publish path)
    - IGitRepository DI registration in Studio
  affects:
    - DeckFlow.Studio/Program.cs (one AddSingleton line)
    - DeckFlow.Studio/Pages/Publish.razor (new)
tech_stack:
  added: []
  patterns:
    - Task.Run + InvokeAsync disposal-safe sink (Harvest.razor pattern)
    - Canonical per-row JSON diff (JsonSerializer, camelCase+indented, not record equality)
    - Two-stage gate (export+diff → reviewed-diff checkbox → commit)
    - Scoped repo-relative commit (seed + CopyApprovedArtifactsToRepoAsync returned paths)
key_files:
  created:
    - DeckFlow.Studio/Pages/Publish.razor
  modified:
    - DeckFlow.Studio/Program.cs
decisions:
  - "Seed written under repoRoot (not Options.ArtifactRoot) so git can see the file — FIX per plan D-03"
  - "dataRoot derived as Path.GetDirectoryName(Options.ArtifactRoot) to avoid double content-kb/ segment"
  - "Canonical per-row JSON comparison avoids IReadOnlyList<string> reference-equality miscount (tag props)"
  - "GitForeignStagedChangesException caught before GitCommandException (subtype ordering)"
  - "Commit button also disabled when rawDiff is empty (no-changes guard in addition to _diffReviewed)"
  - "SC4 reinterpreted: Stage 1=export+diff, Stage 2=commit; push is deliberate out-of-app step (D-04)"
metrics:
  duration: "~15 minutes"
  completed: "2026-06-16"
  tasks_completed: 2
  files_changed: 2
  status: "IN-PROGRESS — stopped at checkpoint:human-verify (Task 3)"
---

# Phase 46 Plan 04: Publish.razor Commit-Publish Path Summary

**One-liner:** Studio Publish page with two-stage export+diff/commit gate, artifact copy data-root→repo, canonical-JSON diff counts, scoped repo-relative commit refusing foreign staged changes, and post-commit push reminder (never pushes).

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | Register IGitRepository in Studio DI | f20e061 | DeckFlow.Studio/Program.cs |
| 2 | Publish.razor — export+copy+diff stage + reviewed-diff gate + scoped commit | fd7586d | DeckFlow.Studio/Pages/Publish.razor |

## What Was Built

### Task 1: IGitRepository DI Registration

Added `builder.Services.AddSingleton<IGitRepository, GitRepository>()` to `DeckFlow.Studio/Program.cs` after the `IFfmpegAudioChunker` registration. The `DeckFlow.Core.Integration` using directive was already present — no duplicate added. No other registrations touched.

### Task 2: Publish.razor (PUB-03)

Created `DeckFlow.Studio/Pages/Publish.razor` with:

**Init (OnInitializedAsync):** Resolves `repoRoot` via `Git.ResolveRepoRootAsync`, `branch` via `GetCurrentBranchAsync`, `approvedCount` via `IndexStore.GetApprovedRowsAsync`, and `dataRoot = Path.GetDirectoryName(Options.ArtifactRoot)`. Both repoRoot and branch are displayed. If `ResolveRepoRootAsync` throws, an `alert-danger` is shown and export is disabled.

**Stage 1 — Export & Preview Diff:**
1. Writes the seed at `seedAbsPath = Path.GetFullPath(Path.Combine(repoRoot, "content-kb/seed/index-seed.json"))` — in the REPO tree, not the data dir (FIX per D-03).
2. Calls `Orchestrator.CopyApprovedArtifactsToRepoAsync(dataRoot, repoRoot, ct)` — a missing/unreadable source is a publish-blocking error caught in a `try/catch`, showing `alert-danger` and stopping before any diff/commit.
3. Builds staged path list: `[seedRelative] + copiedArtifactPaths` (repo-relative, not `Options.ArtifactRoot`-prefixed).
4. Calls `Git.DiffAsync(repoRoot, stagedPaths, ct)` for raw diff.
5. Computes Added/Updated/Removed via canonical per-row JSON (`JsonSerializer.Serialize(r, _canonicalJsonOptions)` with `PropertyNamingPolicy=CamelCase, WriteIndented=true`) keyed by `(NaturalKeyType, NaturalKeyValue)` — never record/list-reference equality (tag reference trap avoided).
6. Shows diff preview card (badge counts + `<pre>` scrollable raw diff + branch + seed path).

**Stage 2 — Commit (D-04):**
- Read-only pre-filled commit message: `"content: publish approved KB seed (N entries)"`.
- Reviewed-diff checkbox: `"I have reviewed the diff above and want to commit these changes."` — Commit button disabled until checked AND rawDiff is non-empty.
- Calls `Git.StageAndCommitAsync(repoRoot, _stagedPaths, message, ct)` — scoped repo-relative paths, never `-A`.
- Catches `GitForeignStagedChangesException` FIRST (before `GitCommandException`) → `"Cannot commit — unrelated changes are already staged"` message.
- After successful commit: shows SHA + entry count + push reminder with `git push origin {branch}` and `"Studio never pushes."` No push button, no push call anywhere.

**Disposal:** `_cts.Cancel() + _cts.Dispose()` on `IDisposable.Dispose()`. InvokeAsync sinks swallow `ObjectDisposedException`/`InvalidOperationException` throughout.

## Build Verification

- DeckFlow.Studio: 0 errors, 0 new warnings
- 1 pre-existing CS1574 in `IContentIndexExporter.cs` (cross-assembly `<cref>` to `IGitRepository.StageAndCommitAsync` from Plan 02) — not introduced by this plan

## Deviations from Plan

None — plan executed exactly as written. The seed-in-repo-tree fix, canonical-JSON diff, and missing-source blocking were all specified in the plan; this is the correct implementation of those requirements.

## Known Stubs

None. All five IGitRepository methods and both IContentIndexExporter methods are called. No hardcoded empty values flow to UI rendering.

## Threat Surface Scan

No new security surface beyond what the plan's threat model already covers:
- `T-46-04-01` (accidental deploy): no push call/button present.
- `T-46-04-02` (foreign staged): `GitForeignStagedChangesException` caught; `_stagedPaths` is the returned copied set + seedRelative.
- `T-46-04-05` (miscount): canonical JSON comparison implemented.
- `T-46-04-06` (broken seed): artifact copy blocking stop pattern implemented.
- `T-46-04-09` (dropped circuit): CTS + disposal-safe InvokeAsync sinks.

## Status

**STOPPED AT CHECKPOINT:HUMAN-VERIFY (Task 3)** — Studio has no test project; the human-verify checkpoint is the sole verification of Publish.razor's interactive behavior. See checkpoint details below.

## Self-Check: PASSED

- DeckFlow.Studio/Pages/Publish.razor: FOUND
- DeckFlow.Studio/Program.cs (IGitRepository registration): FOUND
- Commit f20e061 (Task 1): FOUND
- Commit fd7586d (Task 2): FOUND
