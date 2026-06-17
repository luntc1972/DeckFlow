---
phase: 46-review-queue-commit-publish-path
plan: "03"
subsystem: DeckFlow.Studio / Pages
tags: [review-queue, blazor, approval-status, artifact-resolver, path-containment, nav]
dependency_graph:
  requires: [46-01 (SetApprovalStatusAsync interface + implementation)]
  provides: [Review.razor review queue UI (REVQ-02/03), NavMenu Review+Publish entries]
  affects: [DeckFlow.Studio NavMenu (nav order), Plan 04 Publish page (no file overlap)]
tech_stack:
  added: []
  patterns: [Blazor IDisposable cancel-on-circuit-drop, Task.Run off-sync-context IO, RenderFragment badge switch, optimistic immediate-write, expand-cache Dictionary<string,string?>, parent-of-ArtifactRoot path resolver with containment guard]
key_files:
  created:
    - DeckFlow.Studio/Pages/Review.razor
  modified:
    - DeckFlow.Studio/Shared/NavMenu.razor
decisions:
  - "Artifact path resolver: data root = Directory.GetParent(Options.ArtifactRoot) so the stored ArtifactPath content-kb/ prefix is honored exactly once; combining with ArtifactRoot directly doubled the segment and resolved every artifact MISSING (D-08/D-09 fix)"
  - "Containment guard: Path.IsPathRooted + dotdot segment scan + Path.GetFullPath prefix check; any violation stores null (graceful degradation, never throws, never crashes circuit)"
  - "Batch bar implemented via RenderFragment helper method (RenderBatchBar) to avoid @{} scoping issues at the top level of a Razor else block — local variables declared inside the method, not in an inline @{} before @if"
  - "Per-row approve/reject is optimistic (D-05): no spinner, DB write then in-memory mutation; IsArtifactMissing() returns true only after a cache entry exists with null value (not yet expanded = not yet known missing)"
  - "Natural key derivation: YoutubeVideoId non-blank -> (ContentSourceType.Youtube, YoutubeVideoId); else -> (ContentSourceType.Podcast, RssGuid)"
metrics:
  duration_minutes: 30
  completed_date: "2026-06-16"
  tasks_completed: 2
  files_changed: 2
requirements: [REVQ-02, REVQ-03]
---

# Phase 46 Plan 03: Review Queue + NavMenu Entries Summary

**One-liner:** Review queue Blazor page with per-row optimistic approve/reject, batch operations, inline expand with correct parent-of-ArtifactRoot resolver and containment guard, and filter tabs — stopped at human-verify checkpoint.

## Tasks Completed

| # | Task | Commit | Files |
|---|------|--------|-------|
| 1 | NavMenu Review + Publish entries | edb05c1 | DeckFlow.Studio/Shared/NavMenu.razor |
| 2 | Review.razor — queue, filter tabs, badges, expand (correct artifact resolver), optimistic approve/reject, batch | 4bb1bb8 | DeckFlow.Studio/Pages/Review.razor |

## Task 3 — Human-Verify Checkpoint (awaiting)

Task 3 is a `checkpoint:human-verify` gate. Execution stopped before the checkpoint; the operator must verify the Review queue behaviors in the browser.

## What Was Built

### Task 1 — NavMenu entries

Two new nav entries inserted immediately after Harvest in `NavMenu.razor`:
- Review: `href="review"`, icon `oi oi-task`
- Publish: `href="publish"`, icon `oi oi-cloud-upload`

Nav order: Home / Harvest / Review / Publish.

### Task 2 — Review.razor

Full review queue Blazor Server page at `@page "/review"`:

**Load:** `OnInitializedAsync` → `Task.Run` → `EnsureSchemaAsync` + `GetAllRowsAsync`; projects each `ContentSiteIndexRow` to a mutable `ReviewViewModel` (Title, VideoUrl, ArtifactPath, ApprovalStatus, tag lists, derived NaturalKeyType/Value, Selected, Expanded). Marshals back via `InvokeAsync(StateHasChanged)`.

**Filter tabs:** Bootstrap `nav nav-tabs` with Pending/Approved/Rejected/All. Count badges recomputed from in-memory list. Switching tabs clears all Selected flags. Pending active on load.

**Queue table:** `table table-sm table-hover align-middle`. Columns: select-all checkbox | Title (text-truncate 300px, external link) | Tags (max 3 badges + "+N more") | Approval badge | Actions | Expand toggle. Row tinting: `table-success` (approved), `table-danger` (rejected).

**Approval badge:** `RenderFragment` switch: pending → `bg-secondary`, approved → `bg-success`, rejected → `bg-danger`. Artifact missing overlay badge (`bg-warning text-dark`) shown alongside when `IsArtifactMissing()` is true.

**Per-row approve/reject (D-05 optimistic):** Calls `IndexStore.SetApprovalStatusAsync(keyType, keyValue, status, _cts.Token)` then mutates `vm.ApprovalStatus` in place. `Approve Entry` disabled when already approved OR artifact known-missing (D-10); its `aria-label` switches to "Approve Entry disabled — artifact file missing" when disabled for missing artifact. `Reject Entry` disabled only when already rejected (always allowed for missing artifact).

**Artifact resolver (D-08/D-09 fix):** `ReadArtifactSafe(artifactPath)` helper:
1. Rejects rooted paths (`Path.IsPathRooted`)
2. Rejects paths with `..` segments (split + Any check)
3. Resolves against data root = `Directory.GetParent(Options.ArtifactRoot)?.FullName` — the stored `ArtifactPath` already carries `content-kb/`, so combining with the data root (parent of `ArtifactRoot = {studioDataDir}/content-kb`) yields the correct `{studioDataDir}/content-kb/{sourceSlug}/{id}.md`
4. `Path.GetFullPath` containment guard: `artifactAbs` must start with `canonicalDataRoot + Path.DirectorySeparatorChar`
5. `FileNotFoundException`/`IOException` → returns `null` (graceful degradation)
6. Cached in `_expandCache` keyed by `NaturalKeyValue`; null = missing

**Inline expand row (D-09):** Full-width `<tr colspan=6>` with `bg-light p-3`. When cache has text: `<h3 class="h6 fw-semibold">Content Preview</h3>` + `<pre>` (max-height 400px, monospace 0.875rem) + `<dl class="row mb-0">` of three tag sets. When cache is null: `alert alert-warning` with the exact UI-SPEC missing-artifact copy.

**Batch bar:** `RenderBatchBar()` RenderFragment method returns empty when no rows checked; otherwise renders the `d-flex gap-2` bar with `Approve Selected (N)` (`btn-primary`) and `Reject Selected (N)` (`btn-outline-danger`). (N) = eligible checked rows. Batch calls `SetApprovalStatusAsync(IReadOnlyList<(string,string)>, status, _cts.Token)` inside `Task.Run`; `finally` clears `_operationInFlight`, clears selections, calls `InvokeAsync(StateHasChanged)` with disposal-safe try/catch.

**IDisposable:** `_cts.Cancel(); _cts.Dispose()` in `Dispose()`.

## Verification

- `DeckFlow.Studio` builds 0 errors / 0 new warnings (pre-existing CS1574 warning in Core is out-of-scope, unchanged).
- `grep -c 'href="review"\|href="publish"' NavMenu.razor` → 2.
- `grep 'GetParent(Options.ArtifactRoot)'` → present; `grep -c 'Path.Combine(Options.ArtifactRoot, row.ArtifactPath)'` → 0 (doubled-prefix bug absent).
- `grep 'Path.GetFullPath'` → containment guard present.
- `grep 'FileNotFoundException'` → graceful IO catch present.
- `grep -c 'DeckFlow.CLI' Review.razor` → 0.
- All filter tab labels / count badge classes match UI-SPEC.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Razor @{} scoping at top-level else block**
- **Found during:** Task 2 first build
- **Issue:** Declaring variables in an `@{...}` code block at the top level of an `@else` branch and referencing them in a subsequent `@if` produced `RZ1010: Unexpected "{" after "@" character` on the first build
- **Fix:** Extracted the batch bar into a `RenderBatchBar()` RenderFragment method where local variable scoping works correctly; the method is invoked via `@RenderBatchBar()` in the markup
- **Files modified:** DeckFlow.Studio/Pages/Review.razor
- **Commit:** 4bb1bb8 (incorporated in same task commit after one fix iteration)

## Known Stubs

None. The review queue loads real data from `IContentSiteIndexStore.GetAllRowsAsync`, writes real approval status via `SetApprovalStatusAsync`, and reads real artifact files via the corrected resolver.

## Threat Flags

None beyond what is in the plan's threat register. All four threats mitigated:
- **T-46-03-01** (Tampering): status values are page constants "approved"/"rejected" — never operator-typed; store validates the allow-list; writes are Dapper-parameterized.
- **T-46-03-02** (DoS via missing artifact): file read wrapped in `Task.Run` with `FileNotFoundException`/`IOException` catch → null; renders warning and disables Approve; circuit stays alive.
- **T-46-03-03** (Path traversal): `IsPathRooted` + dotdot segment check + `Path.GetFullPath` containment guard rejects escape attempts before any file read.
- **T-46-03-04** (Stale circuit StateHasChanged): `InvokeAsync` sink swallows `ObjectDisposedException`/`InvalidOperationException`; `Dispose()` cancels CTS.

## Self-Check: PASSED

- [x] `DeckFlow.Studio/Pages/Review.razor` created — FOUND (694 lines)
- [x] `DeckFlow.Studio/Shared/NavMenu.razor` modified — FOUND
- [x] Task 1 commit edb05c1 — FOUND
- [x] Task 2 commit 4bb1bb8 — FOUND
- [x] DeckFlow.Studio builds 0 errors
- [x] All acceptance criteria grep checks pass
- [x] Task 3 checkpoint correctly not self-approved — awaiting human verify
