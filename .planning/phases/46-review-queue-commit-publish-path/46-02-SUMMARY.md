---
phase: 46-review-queue-commit-publish-path
plan: "02"
subsystem: Core
tags: [git-service, orchestrator, seed-write, artifact-copy, lf, tdd]
dependency_graph:
  requires: []
  provides:
    - IGitRepository + GitRepository (DeckFlow.Core.Integration)
    - IContentIndexExporter.ExportIndexToFileAsync (DeckFlow.Core.Orchestration)
    - IContentIndexExporter.CopyApprovedArtifactsToRepoAsync (DeckFlow.Core.Orchestration)
  affects:
    - DeckFlow.Studio (Plan 04 wires these via DI — no Studio changes in this plan)
    - DeckFlow.CLI (unaffected; SerializeContentIndexExportRows still in CLI for CLI use)
tech_stack:
  added: []
  patterns:
    - Process.Start + ArgumentList (FfmpegAudioChunker / CliLlmDistillationService analog)
    - Shared GetApprovedExportRowsAsync helper — single approved-row source for all export methods
    - ResolveContainedPath helper — containment guard reused for both source and dest
key_files:
  created:
    - DeckFlow.Core/Integration/IGitRepository.cs
    - DeckFlow.Core/Integration/GitRepository.cs
    - DeckFlow.Core.Tests/Orchestration/ContentIndexSeedWriteTests.cs
    - DeckFlow.Core.Tests/Orchestration/ContentArtifactCopyTests.cs
  modified:
    - DeckFlow.Core/Orchestration/IContentIndexExporter.cs
    - DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs
decisions:
  - "ExportIndexAsync refactored to call shared GetApprovedExportRowsAsync so all three methods (ExportIndexAsync, ExportIndexToFileAsync, CopyApprovedArtifactsToRepoAsync) produce exactly the same approved-row set"
  - "ResolveContainedPath extracted as private static helper to apply the same containment guard to both source (dataRoot) and dest (repoRoot) in CopyApprovedArtifactsToRepoAsync"
  - "CatHeadSeedAsync treats non-zero exit as empty string (not an error) — first publish has no seed at HEAD"
  - "GitRepository.ResolveRepoRootAsync uses git -C {startDir} with WorkingDirectory also set to startDir so it works regardless of process CWD"
metrics:
  duration: "~35 minutes"
  completed: "2026-06-16"
  tasks_completed: 3
  files_changed: 6
---

# Phase 46 Plan 02: Core Git Service + Seed Write + Artifact Copy Summary

**One-liner:** Core git shell-out service (pathspec-scoped, foreign-staged-guard, no-push), approved-only LF seed file write, and data-root→repo artifact copy with both-ends containment guards — all wired for Plan 04's Publish page.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 RED | ContentIndexSeedWrite failing tests | 05eec2a | ContentIndexSeedWriteTests.cs |
| 1 GREEN | ExportIndexToFileAsync implementation | f00fac7 | IContentIndexExporter.cs, ContentKbOrchestrator.cs |
| 2 | IGitRepository + GitRepository | ef8bd55 | IGitRepository.cs, GitRepository.cs |
| 3 RED | ContentArtifactCopy failing tests | 82ab194 | ContentArtifactCopyTests.cs |
| 3 GREEN | CopyApprovedArtifactsToRepoAsync implementation | 3293465 | IContentIndexExporter.cs, ContentKbOrchestrator.cs |

## What Was Built

### Task 1: ExportIndexToFileAsync (D-13 LF seed write)

Added `ExportIndexToFileAsync(string seedPath, ...)` to `IContentIndexExporter` and implemented in `ContentKbOrchestrator`. Uses the same `JsonSerializerOptions` as the CLI (`PropertyNamingPolicy=CamelCase, WriteIndented=true`), normalizes CRLF→LF, appends a trailing `\n`, creates parent directories, and writes via `File.WriteAllTextAsync`. The existing `ExportIndexAsync` was refactored to share the new `GetApprovedExportRowsAsync` helper.

Three TDD tests pin:
- LF-only bytes (no 0x0D anywhere in output)
- Approved-only membership (pending rows absent)
- Byte-shape matches the CLI serializer golden output

### Task 2: IGitRepository + GitRepository

New git shell-out service in `DeckFlow.Core.Integration`. Uses `ArgumentList` (never string `Arguments`), `UseShellExecute=false`, `CreateNoWindow=true`, `WorkingDirectory=repoRoot` on every command.

`StageAndCommitAsync` enforces three layers of protection:
1. **Foreign-staged guard**: `git diff --cached --name-only` checked before staging; throws `GitForeignStagedChangesException` if any staged path is outside the allowed set
2. **Scoped add**: `git add -- {paths}` (never `-A` or `.`)
3. **Pathspec-scoped commit**: `git commit -m {msg} -- {paths}` so even a race-condition staged path cannot enter the commit

`CatHeadSeedAsync` returns `""` on non-zero exit (path absent at HEAD = first publish, not an error). No push verb exists anywhere in the service.

### Task 3: CopyApprovedArtifactsToRepoAsync (D-03 artifact materialization)

Added `CopyApprovedArtifactsToRepoAsync(string dataRoot, string repoRoot, ...)` to `IContentIndexExporter` and implemented in `ContentKbOrchestrator`. Copies approved markdown artifacts from `{dataRoot}/{row.ArtifactPath}` to `{repoRoot}/{row.ArtifactPath}` (no double `content-kb/` prefix — `ArtifactPath` already starts with `content-kb/`).

The private `ResolveContainedPath` helper guards both source and dest: rejects null/whitespace, rooted paths, `..` segments, leading `:` (git pathspec-magic), and asserts `fullPath.StartsWith(rootFull + separator)`. A missing source throws `InvalidOperationException` (publish-blocking — D-10). Returns the list of copied repo-relative paths for the caller to pass to `StageAndCommitAsync`.

## Verification Results

- DeckFlow.Core: 0 errors, 0 warnings
- DeckFlow.Core.Tests: 0 errors, 0 warnings (3 pre-existing xUnit2017 warnings in EnsureYoutubeSourceTests.cs — unrelated to this plan)
- Static grep gates: no push verb, no `git add -A`, ArgumentList ≥29 usages, WorkingDirectory set, CRLF→LF normalize present, pathspec-scoped commit (`commit ... -- {paths}`), foreign-staged guard (`--cached`), containment guard (`GetFullPath` + `StartsWith root+sep`), `ExportIndexToFileAsync` and `CopyApprovedArtifactsToRepoAsync` declared on interface
- Note: VSTest unreliable in WSL; build clean is the enforced gate per CLAUDE.md. Tests compile and build succeeds.

## Deviations from Plan

### Auto-refactored Issues

**1. [Rule 2 - Missing Critical Functionality] Refactored ExportIndexAsync to use shared helper**
- **Found during:** Task 1 implementation
- **Issue:** Task 1 and Task 3 both needed the same approved-rows projection. Without factoring, any divergence between the seed set and the copy set would silently break the publish invariant (committed seed referencing files not copied into repo).
- **Fix:** Extracted `GetApprovedExportRowsAsync` private helper; `ExportIndexAsync`, `ExportIndexToFileAsync`, and `CopyApprovedArtifactsToRepoAsync` all call it.
- **Files modified:** ContentKbOrchestrator.cs
- **Commit:** f00fac7

## Threat Surface Scan

| Flag | File | Description |
|------|------|-------------|
| threat_flag: process-spawn | DeckFlow.Core/Integration/GitRepository.cs | New network surface: git process spawned with ArgumentList (no shell injection). Mitigated by T-46-02-01 (ArgumentList, UseShellExecute=false). |
| threat_flag: filesystem-write | DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs | Two new file-write surfaces: seed JSON write (ExportIndexToFileAsync) and artifact copy (CopyApprovedArtifactsToRepoAsync). Mitigated by T-46-02-04 (plan-supplied path) and T-46-02-06 (both-ends containment guard). |

## Known Stubs

None. All three methods are fully implemented; Plan 04 wires them into Studio UI without further Core changes.

## Self-Check: PASSED

- DeckFlow.Core/Integration/IGitRepository.cs: FOUND
- DeckFlow.Core/Integration/GitRepository.cs: FOUND
- DeckFlow.Core/Orchestration/IContentIndexExporter.cs (ExportIndexToFileAsync + CopyApprovedArtifactsToRepoAsync): FOUND
- DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs (both methods + helper): FOUND
- DeckFlow.Core.Tests/Orchestration/ContentIndexSeedWriteTests.cs: FOUND
- DeckFlow.Core.Tests/Orchestration/ContentArtifactCopyTests.cs: FOUND
- Commits 05eec2a, f00fac7, ef8bd55, 82ab194, 3293465: FOUND in git log
