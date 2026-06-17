---
phase: 46-review-queue-commit-publish-path
verified: 2026-06-16T20:25:00Z
status: human_needed
score: 5/5
overrides_applied: 0
human_verification:
  - test: "Review queue — filter/badge/tint/expand/missing-artifact/batch interactive behavior"
    expected: "Pending tab active on load; tab count badges match row counts; tab switch filters table and clears selections; Approve a row → badge flips to Approved, row tints table-success, Approve button disables; expand a row with real artifact → Content Preview pre shows markdown (NOT 'Artifact missing'); expand a row whose file was renamed → alert-warning shown; batch bar appears when >=1 row checked; Approve Selected updates all and clears selections"
    why_human: "DeckFlow.Studio.Tests bUnit suite covers the component logic (11 Review facts), but interactive browser behavior (artifact resolver reading real on-disk files, circuit-drop disposal, full filter+badge render flow) cannot be confirmed programmatically. Plan 03 Task 3 is a blocking checkpoint that was stopped pre-browser-verify."
  - test: "Publish page — branch display, seed in repo tree, artifact copy, missing-source block, LF assertion, canonical-JSON Updated count, checkbox gate, scoped commit refuses foreign staged"
    expected: "Current branch displayed; N entries approved shown; Export & Preview Diff writes seed at repoRoot/content-kb/seed/index-seed.json (git status shows it); approved artifacts copied to repoRoot/content-kb/{slug}/{id}.md; removing one approved artifact source → publish-blocking alert-danger, no diff/commit shown; re-export with no approval changes → Updated=0; Commit disabled until checkbox checked; pre-staged unrelated file → GitForeignStagedChangesException message; successful commit shows SHA; push reminder shows 'git push origin {branch}' and no push button exists; 'file index-seed.json' reports ASCII text (not CRLF)"
    why_human: "DeckFlow.Studio.Tests PublishPageTests covers component logic (10 Publish facts) but cannot run actual git operations, write real files to disk, or verify LF via the file command. Plan 04 Task 3 is a blocking checkpoint stopped pre-browser-verify."
---

# Phase 46: Review Queue + Commit-Publish Path — Verification Report

**Phase Goal:** Operator can review distilled entries in a queue, approve or reject them, then publish approved entries to deckflow.gg via a git commit with a diff preview and a two-stage commit/push separation that prevents accidental Render auto-deploy.
**Verified:** 2026-06-16T20:25:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Review queue lists entries with approval badge, filter tabs with counts, row tinting, and optimistic per-row approve/reject writes `approval_status` immediately | VERIFIED | `Review.razor` (694 lines) at `@page "/review"`. Tabs: `nav nav-tabs` with Pending/Approved/Rejected/All, count badges computed from `_allRows.Count(r => r.ApprovalStatus == "pending")` etc. Row tint: switch `"approved" => "table-success"`, `"rejected" => "table-danger"`. Optimistic write: `IndexStore.SetApprovalStatusAsync(vm.NaturalKeyType, vm.NaturalKeyValue, "approved", _cts.Token)` then `vm.ApprovalStatus = "approved"` in place (no spinner). bUnit fact `ApproveEntry_OnPendingYoutubeRow_FlipsRowBadgeToApproved` passes. |
| 2 | Batch approve/reject writes all checked rows in one atomic round-trip via the batch `SetApprovalStatusAsync` overload | VERIFIED | `Review.razor:387,431` calls `IndexStore.SetApprovalStatusAsync(keys, "approved"/"rejected", _cts.Token)` (the `IReadOnlyList<(string,string)>` overload). `ContentSiteIndexStore` batch impl (lines 540–575) wraps all per-key UPDATEs in `BeginTransactionAsync` / `CommitAsync` inside a single connection — one logical round-trip, all-or-nothing. bUnit fact `BatchApprove_TwoPendingRows_CallsBatchOverloadWithBothKeys` passes. |
| 3 | Inline expand reads the artifact via the correct resolver (parent of `ArtifactRoot`, not doubled `content-kb` prefix), with containment guard; genuinely missing artifact shows warning and disables Approve | VERIFIED | `Review.razor:522–523`: `dataRoot = Directory.GetParent(Options.ArtifactRoot)?.FullName`; `artifactAbs = Path.GetFullPath(Path.Combine(dataRoot, artifactPath))`. grep confirms no `Path.Combine(Options.ArtifactRoot, row.ArtifactPath)`. Containment guard at lines 527–531 (prefix check). `FileNotFoundException`/`IOException` → `return null` (lines 535–541). `alert-warning` rendered when cache is null (line 174). Approve button `disabled="@(vm.ApprovalStatus == "approved" || missing)"` (line 136). |
| 4 | Publish page writes the LF seed into the repo tree, copies every approved artifact from the Studio data root into `repoRoot/content-kb`, computes diff counts via canonical per-row JSON (not record equality), gates commit behind reviewed-diff checkbox, commits only scoped repo-relative paths, refuses foreign pre-staged changes, and never pushes | VERIFIED | `Publish.razor:337`: `seedAbsPath = Path.GetFullPath(Path.Combine(_repoRoot, SeedRelative))` — seed under repo root, not data dir. `Publish.razor:364`: `Orchestrator.CopyApprovedArtifactsToRepoAsync(_dataRoot, _repoRoot, _cts.Token)`. `Publish.razor:274`: `dataRoot = Path.GetDirectoryName(Options.ArtifactRoot)`. Canonical JSON: `_canonicalJsonOptions` (camelCase+indented) used for `JsonSerializer.Serialize(r, _canonicalJsonOptions)` comparison (lines 253–414). `_diffReviewed` gate at line 157. `StageAndCommitAsync` with `_stagedPaths` (line 501). `GitForeignStagedChangesException` caught before `GitCommandException` (lines 516,518). No push button/call (grep confirms 0 non-comment push occurrences in GitRepository.cs). |
| 5 | `SetApprovalStatusAsync` single + batch on `IContentSiteIndexStore` / `ContentSiteIndexStore`; status allow-list validated before any DB write; only `approval_status` is mutated; admin fields preserved | VERIFIED | `IContentSiteIndexStore.cs:150,166` — two overload declarations. `ContentSiteIndexStore.cs:516,540` — implementations. `ValidateApprovalStatus` checks against `private static readonly string[] AllowedApprovalStatuses = ["pending", "approved", "rejected"]` — throws `ArgumentException` before any DB call. SQL: `SET approval_status = @status WHERE natural_key_type = @type AND natural_key_value = @value` — no `is_visible`/`is_hidden`/`is_evergreen` in SET clause (verified in implementation read). 7 new test facts + 3 Phase 43 facts = 10/10 pass (`ContentSiteIndexStoreApprovalTests`). |

**Score:** 5/5 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Core/Content/IContentSiteIndexStore.cs` | `SetApprovalStatusAsync` single + batch declarations | VERIFIED | Two declarations at lines 150, 166 |
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` | `SetApprovalStatusAsync` single + atomic batch Dapper implementations | VERIFIED | Implementations at lines 516, 540; batch uses `BeginTransactionAsync`/`CommitAsync`; SQL sets only `approval_status` |
| `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreApprovalTests.cs` | 7 new approval test facts | VERIFIED | All 7 facts confirmed at lines 206, 222, 235, 260, 286, 298, 312 |
| `DeckFlow.Core/Integration/IGitRepository.cs` | Git shell-out contract — 5 methods, no push | VERIFIED | `GetCurrentBranchAsync`, `ResolveRepoRootAsync`, `DiffAsync`, `CatHeadSeedAsync`, `StageAndCommitAsync` all declared; doc comment explicitly notes no push counterpart |
| `DeckFlow.Core/Integration/GitRepository.cs` | `Process.Start` impl with `ArgumentList`, `WorkingDirectory`, pathspec-scoped commit, foreign-staged guard | VERIFIED | `ArgumentList` used throughout (≥29 usages confirmed); `WorkingDirectory` set; `git diff --cached --name-only` guard at line 149–166; `GitForeignStagedChangesException` thrown; `git commit -m {msg} -- {paths}` at lines 185–191; zero "push" occurrences in non-comment code |
| `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` | `ExportIndexToFileAsync` (LF-normalized seed) + `CopyApprovedArtifactsToRepoAsync` (containment-guarded artifact copy) | VERIFIED | `ExportIndexToFileAsync` at line 742 with `Replace("\r\n", "\n")` LF normalization (line 762); `CopyApprovedArtifactsToRepoAsync` at line 790; `ResolveContainedPath` private helper at line 835 with full guard (rooted rejection, `..` segment scan, `StartsWith(root+sep)`) |
| `DeckFlow.Studio/Pages/Review.razor` | Review queue page — ≥200 lines, `SetApprovalStatusAsync` calls, correct resolver | VERIFIED | 694 lines; `SetApprovalStatusAsync` called at 4 sites (lines 337, 359, 387, 431); resolver uses `Directory.GetParent(Options.ArtifactRoot)` at line 522 |
| `DeckFlow.Studio/Shared/NavMenu.razor` | Review + Publish nav entries in order Home/Harvest/Review/Publish | VERIFIED | `href="review"` with `oi oi-task` at line 23; `href="publish"` with `oi oi-cloud-upload` at line 28; order confirmed as Home → Harvest → Review → Publish → Direct Push |
| `DeckFlow.Studio/Pages/Publish.razor` | Publish page — ≥180 lines, `CopyApprovedArtifactsToRepoAsync`, two-stage gate | VERIFIED | 552 lines; `CopyApprovedArtifactsToRepoAsync` at line 364; `ExportIndexToFileAsync`, `DiffAsync`, `CatHeadSeedAsync`, `StageAndCommitAsync` all present; `_diffReviewed` checkbox gate at line 157 |
| `DeckFlow.Studio/Program.cs` | `IGitRepository` DI registration | VERIFIED | `builder.Services.AddSingleton<IGitRepository, GitRepository>()` at line 92 |
| `DeckFlow.Core.Tests/Orchestration/ContentIndexSeedWriteTests.cs` | 3 LF / approved-only / byte-shape facts | VERIFIED | Facts at lines 38, 53, 71 |
| `DeckFlow.Core.Tests/Orchestration/ContentArtifactCopyTests.cs` | 3 copy / missing-source / traversal-rejection facts | VERIFIED | Facts at lines 36, 75, 101 |
| `DeckFlow.Studio.Tests/ReviewPageTests.cs` | bUnit behavioral tests for Review queue | VERIFIED (post-plan-05) | 11 `[Fact]` methods; rendered against real `Review` component via `Render<Review>()`; substantive assertions (DOM badge counts, `WaitForAssertion`, `Find("button[aria-label='Approve Entry']")`). Added commit `367e4858` after Plan 05 completed — not reflected in 46-05-SUMMARY's "no test project" claim. |
| `DeckFlow.Studio.Tests/PublishPageTests.cs` | bUnit behavioral tests for Publish page | VERIFIED (post-plan-05) | 10 `[Fact]` methods covering branch display, export/diff flow, commit gate, `GitForeignStagedChangesException` message, no-push guard |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `Review.razor` approve/reject handlers | `IContentSiteIndexStore.SetApprovalStatusAsync` | `@inject IContentSiteIndexStore IndexStore` + natural key | WIRED | Lines 337, 359 (single); lines 387, 431 (batch) |
| `Review.razor` expand handler | Artifact on disk (data root, parent of ArtifactRoot) | `ReadArtifactSafe` → `Directory.GetParent(Options.ArtifactRoot)` + `Path.GetFullPath` containment | WIRED | Lines 522–533; containment guard lines 527–531; `FileNotFoundException` catch → null |
| `Publish.razor` export handler | `IContentKbOrchestrator.ExportIndexToFileAsync` + `CopyApprovedArtifactsToRepoAsync` | `Task.Run` → seed at `Path.Combine(repoRoot, SeedRelative)` → copy from `dataRoot = Path.GetDirectoryName(Options.ArtifactRoot)` | WIRED | Lines 337–365; missing-source catch blocks at lines 367–380 |
| `Publish.razor` diff | `IGitRepository.DiffAsync` + `CatHeadSeedAsync` | `Git.DiffAsync(_repoRoot, stagedReadOnly, ct)` / `Git.CatHeadSeedAsync(_repoRoot, SeedRelative, ct)` | WIRED | Lines 391, 397 |
| `Publish.razor` canonical JSON diff | `JsonSerializer.Serialize(r, _canonicalJsonOptions)` string comparison | `_canonicalJsonOptions` (camelCase+indented) defined at lines 253–257; `Canonical(r)` lambda at line 414 | WIRED | Avoids `IReadOnlyList<string>` reference-equality trap (comment at line 255) |
| `Publish.razor` commit handler | `IGitRepository.StageAndCommitAsync` (scoped repo-relative paths) | `_stagedPaths = [seedRelative] + copiedArtifactPaths`; `Git.StageAndCommitAsync(_repoRoot, _stagedPaths, _commitMessage, ct)` | WIRED | Line 501; `GitForeignStagedChangesException` caught first at line 516 |
| `ContentSiteIndexStore.SetApprovalStatusAsync` batch | `content_site_index.approval_status` via one `DbTransaction` | `BeginTransactionAsync` + `CommandDefinition(sql, params, transaction: transaction)` + `CommitAsync` | WIRED | Lines 556–573; SQL: `SET approval_status = @status WHERE natural_key_type = @type AND natural_key_value = @value` |
| `ContentKbOrchestrator.ExportIndexToFileAsync` | `index-seed.json` on disk (LF-only) | `json.Replace("\r\n", "\n") + "\n"` → `File.WriteAllTextAsync` | WIRED | Lines 762–763 |
| `ContentKbOrchestrator.CopyApprovedArtifactsToRepoAsync` | `repoRoot/content-kb/{slug}/{id}.md` | `ResolveContainedPath(dataRootFull, row.ArtifactPath)` → `File.Copy(source, dest, overwrite: true)` | WIRED | Lines 810–811; containment guard at lines 835–875; missing-source throws `InvalidOperationException` |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| `Review.razor` `_allRows` | `List<ReviewViewModel>` projected from `GetAllRowsAsync` | `IContentSiteIndexStore.GetAllRowsAsync` → SQLite `content_site_index` table | Yes — real DB query, not hardcoded | FLOWING |
| `Review.razor` `_expandCache[key]` | `string?` from `ReadArtifactSafe` | `File.ReadAllText(artifactAbs)` from data root | Yes — real file read | FLOWING |
| `Publish.razor` `_approvedCount` | `int` from `GetApprovedRowsAsync` | `IContentSiteIndexStore.GetApprovedRowsAsync` → DB query | Yes | FLOWING |
| `Publish.razor` `_rawDiff` | `string` from `Git.DiffAsync` | `Process.Start("git diff -- {paths}")` → stdout | Yes — real git output | FLOWING |
| `Publish.razor` diff counts | `_added`, `_updated`, `_removed` | Canonical JSON comparison of `CatHeadSeedAsync` (HEAD seed) vs current export rows | Yes — content-aware string comparison | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| `ExportIndexToFileAsync` compiles and is declared on interface | `grep -c ExportIndexToFileAsync DeckFlow.Core/Orchestration/IContentIndexExporter.cs` | 1 | PASS |
| `CopyApprovedArtifactsToRepoAsync` compiles and is declared on interface | `grep -c CopyApprovedArtifactsToRepoAsync DeckFlow.Core/Orchestration/IContentIndexExporter.cs` | 1 | PASS |
| `GitRepository` contains no push verb | `grep -i "push" GitRepository.cs` (non-comment) | 0 results | PASS |
| Batch impl uses single transaction | `grep -n "BeginTransactionAsync\|CommitAsync" ContentSiteIndexStore.cs` | Lines 556, 573 both present | PASS |
| LF normalization present in seed write | `grep "Replace.*\\\\r\\\\n" ContentKbOrchestrator.cs` | Line 762 found | PASS |
| All 7 approval test facts exist by name | `grep -n "SetApprovalStatusAsync_" ContentSiteIndexStoreApprovalTests.cs` | All 7 at lines 206–312 | PASS |
| 23 Core.Tests pass (plan 05 reported) | 46-05-SUMMARY records `Passed! - Failed: 0, Passed: 23` | Build + test gate confirmed | PASS |
| Review.razor renders real `Review` component in bUnit | `grep "Render<Review>()" ReviewPageTests.cs` | Line 63 | PASS |
| No `DeckFlow.CLI` references in Studio pages | `grep -c "DeckFlow.CLI" Review.razor Publish.razor` | 0 | PASS |

### Probe Execution

No phase-declared probes in PLAN files. Step 7c: SKIPPED (no `scripts/*/tests/probe-*.sh` for this phase).

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
|-------------|---------------|-------------|--------|----------|
| REVQ-02 | 46-01, 46-03 | Operator can review each distilled entry, preview summary/tags, approve/reject/leave pending | SATISFIED | `SetApprovalStatusAsync` single overload wired from `Review.razor`; approval badge + table rendering confirmed; expand preview reads real artifact markdown; 11 bUnit facts cover filter, badge, tint, approve/reject, batch |
| REVQ-03 | 46-01, 46-03 | Queue supports batch approve/reject and status filters | SATISFIED | Batch `SetApprovalStatusAsync(IReadOnlyList<(string,string)>)` called at `Review.razor:387,431`; 4 filter tabs (Pending/Approved/Rejected/All) confirmed in markup; bUnit facts `BatchApprove_TwoPendingRows` and `BatchApprove_AllSelectedPendingRows_FlipAllToApproved` pass |
| PUB-03 | 46-02, 46-04 | Operator can export approved seed, see a diff, and stage+commit seed+artifacts | SATISFIED | `Publish.razor` wires `ExportIndexToFileAsync` (LF seed in repo tree) + `CopyApprovedArtifactsToRepoAsync` (artifact materialization) + `DiffAsync`/`CatHeadSeedAsync` (diff preview) + `StageAndCommitAsync` (scoped commit); two-stage gate with `_diffReviewed` checkbox; post-commit push reminder; 10 bUnit facts cover the flow |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `DeckFlow.Studio/Pages/Review.razor` | 508, 515, 530, 537, 541 | `return null` | Info | These are inside `ReadArtifactSafe` — the intentional graceful-degradation path for invalid/missing artifact paths. Each is guarded by a security check (rooted path, `..` segment, containment failure) or an exception catch (`FileNotFoundException`, `IOException`). The null is cached and renders the `alert-warning` UI state. NOT a stub. |

No `TBD`, `FIXME`, or `XXX` markers found in any phase file.

### Human Verification Required

#### 1. Review Queue — Full Interactive Behavior

**Test:** Start Studio with `DECKFLOW_DISABLE_AUTO_BROWSER=true dotnet run --project DeckFlow.Studio` and open http://localhost:{port}/review. If content-kb.db is empty, confirm the All-tab empty state renders (page loads clean). With distilled rows present: verify Pending tab is active on load; tab count badges match row counts; clicking a tab filters the table and clears selections. Approve a pending row → badge flips to Approved, row tints table-success, Approve button disables. Reject another → badge/red tint. Expand a row that HAS a real artifact → Content Preview `<pre>` shows actual markdown (NOT the "Artifact missing" warning — this confirms the resolver does not double the `content-kb/` segment). Expand a row whose artifact was renamed/removed → alert-warning shown, Approve is disabled, Reject still works. Check 2+ rows → batch bar appears; Approve Selected updates all, deselects, refreshes counts.

**Expected:** All behaviors described above work correctly in the browser; no false "Artifact missing" for real artifacts.

**Why human:** The `ReadArtifactSafe` resolver reads files from disk using the real `Options.ArtifactRoot` path from Studio config. bUnit tests mock this path; only a live browser run with real on-disk data confirms the resolver produces correct paths against actual Studio data directory layout.

#### 2. Publish Page — Seed in Repo Tree, Artifact Copy, LF, Scoped Commit

**Test:** From the repo root, start Studio and open http://localhost:{port}/publish. Confirm "Current branch: v1.7" (or current branch) and resolved repo root are displayed; approved count shown. Click Export & Preview Diff → verify in WSL: `git status --short content-kb/` shows the seed + copied artifact paths as modified/untracked (proving they are in the repo working tree, not only the Studio data dir). Verify LF: `file content-kb/seed/index-seed.json` → reports "ASCII text" (not "with CRLF line terminators"). Missing-source block: rename one approved artifact source under the Studio data dir → click Export → expect publish-blocking alert-danger naming the missing artifact, no diff/commit section shown. Re-export with no approval changes → Updated count = 0. Confirm Commit button disabled until "I have reviewed the diff above" checkbox is checked. Scoped-commit check: `git add` an unrelated file and stage it, then click Commit → expect "unrelated changes are already staged" message OR verify `git log -1 --stat` shows only seed + artifacts. After successful commit: SHA displayed, push reminder shows `git push origin {branch}`, no push button exists anywhere.

**Expected:** All behaviors work; `file` command confirms LF; `git status` confirms repo-tree materialization; missing-source blocks publish; canonical diff shows Updated=0 on re-export; foreign-staged guard fires; push never occurs automatically.

**Why human:** The commit flow requires a real git working tree and actual git process execution. `CatHeadSeedAsync` reads real HEAD state. LF verification requires the `file` command against actual written bytes. The GitForeignStagedChangesException path requires manually staging an unrelated file. These behaviors cannot be simulated in bUnit.

---

## Gaps Summary

No blocking gaps. All 5 truths verified, all required artifacts exist and are substantive, all key links confirmed wired with real data flowing. The two human verification items (above) are the only remaining gate — both are interactive behaviors that require a live browser + real git operations, not automated automation gaps.

**Post-plan-05 note:** `DeckFlow.Studio.Tests` (bUnit suite, 21 tests: 11 Review + 10 Publish) was added at commit `367e4858` on 2026-06-16 at 12:12, after Plan 05 completed at 10:39. The 46-05-SUMMARY "no test project" claim was accurate at time of writing but is now superseded. The bUnit tests cover the component behavioral logic that human-verify must confirm against real on-disk data.

---

_Verified: 2026-06-16T20:25:00Z_
_Verifier: Claude (gsd-verifier)_
