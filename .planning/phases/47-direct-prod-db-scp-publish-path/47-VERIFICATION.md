---
phase: 47-direct-prod-db-scp-publish-path
verified: 2026-06-16T20:30:00Z
status: human_needed
score: 6/6 must-haves verified
overrides_applied: 0
human_verification:
  - test: "Live smoke: run DirectPush with real Render SSH + prod Postgres secrets"
    expected: "Files land under /data/content-kb/{slug}/, prod rows upsert via UpsertContentColumnsOnlyAsync, is_visible/is_evergreen on pre-existing prod rows are unchanged before/after"
    why_human: "No live SSH or Postgres connection is made in CI. Real end-to-end publish requires operator prod secrets (Studio:Scp:*, Studio:ProdConnectionString) and a registered SSH key on Render. This is item C of the Task 3 checkpoint; it was explicitly not run (47-03-SUMMARY lines 120-123)."
  - test: "Live banner verify: run Studio locally with real prod/SCP secrets configured"
    expected: "'TARGET: PRODUCTION' danger banner visible; SCP button disabled until checkbox checked; DB button locked with hint until SCP fully succeeds; startup logs show presence-only text only (no secret values)"
    why_human: "Item B3 of Task 3 checkpoint. The unconfigured state was verified at the checkpoint; the with-config live banner state was not run (no prod/SCP secrets used per 47-03-SUMMARY)."
---

# Phase 47: Direct Prod-DB + SCP Publish Path — Verification Report

**Phase Goal:** file-first SCP (artifacts to Render /data) THEN Postgres safe upsert (UpsertContentColumnsOnlyAsync); dry-run diff shows exactly what will change; partial-failure surfaces clearly; AI does not push main.
**Requirements:** PUB-04, PUB-05
**Verified:** 2026-06-16T20:30:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | SftpArtifactUploader (SSH.NET) implements request-based ISshArtifactUploader; artifact-first ordering enforced; per-file SshUploadResult returned | VERIFIED | `SftpArtifactUploader.cs:50-112` — `UploadArtifactsAsync(IReadOnlyList<SshUploadRequest>)` opens one SftpClient per call, uploads sequentially, returns per-file results. `ISshArtifactUploader.cs:24-27` — interface contract with `SshUploadRequest(LocalPath, RemoteRelativePath)`. |
| 2 | On-demand IProdStoreFactory; never a live prod-store DI singleton; uses UpsertContentColumnsOnlyAsync exclusively on prod | VERIFIED | `IProdStoreFactory.cs:21-31` — `ProdStoreFactory.Create` builds on-demand with `// Why:` D-03 comment. `Program.cs:53-55` — registers factory singleton, not a live store. `DirectPush.razor:684,695` — `ProdStoreFactory.Create(rawConnStr)` inside `Task.Run` then `prodStore.UpsertContentColumnsOnlyAsync(row, ...)`. No `UpsertRowAsync` or `UpsertRowPreservingVisibilityAsync` in page (grep confirmed). |
| 3 | Dry-run diff shows exactly what will change (New/Updated counts + per-row table) before any write; explicit "reviewed PRODUCTION" checkbox gates Stage 2 | VERIFIED | `DirectPush.razor:136-200` — Diff Preview card with `New: @_newCount` / `Updated: @_updatedCount` badges and per-row table rendered after `ComputeDiffAsync`. Checkbox `id="prodReviewed"` at line 195. Stage 2 button disabled expression `@(!_prodReviewed || _operationInFlight || !_diffReady)` at line 225. Stage 3 disabled `@(!_scpSuccess || _operationInFlight)` at line 311. WriteRowsAsync hard-guard at line 668: `if (!_scpSuccess || _operationInFlight || !_diffReady) return;`. |
| 4 | Partial-failure surfaces clearly: per-file list for SCP failures; per-row list for DB failures; Stage 3 stays locked on SCP failure; Stage 2 not re-locked on DB failure | VERIFIED | `DirectPush.razor:243-279` — per-file reconcile table with Failed/Uploaded badges and Reason column. Lines 329-367 — per-row reconcile table. `_scpSuccess` set to `false` when any file fails (line 629), keeping Stage 3 locked. DB failure does not reset `_scpSuccess` — reconcile comment at line 726-727 confirms. bUnit tests `DirectPush_ScpPartialFailure_Stage3Locked` and `DirectPush_DbPartialFailure_PerRowListShown` both pass (13/13 per 47-03-SUMMARY). |
| 5 | AI does not push main; no secret values reach markup or logs | VERIFIED | No push occurs — publish path is SCP + prod-DB write only; git push is never invoked by the Studio. Startup log presence-only confirmed: `Program.cs:118-119` logs `"configured/not configured"` only. Catch blocks use sanitized literals at `DirectPush.razor:575,655,707` — never `ex.Message`. Three sentinel-bearing bUnit tests prove this: `DirectPush_DiffReadFailure_SecretsNeverSurface`, `DirectPush_DbWriteFailure_SecretsNeverSurface`, `DirectPush_Secrets_NeverInMarkup`. |
| 6 | 13 bUnit tests (11 named + Theory expansion) pass; SSH.NET 2025.1.0 isolated to Studio only; blocking-human checkpoint approved | VERIFIED | All 11 named DirectPush test methods found in `DirectPushPageTests.cs` (grep count = 11 method declarations; `DirectPush_NotConfigured_ButtonsDisabled` is a `[Theory]` expanding to 3 cases = 13 discrete). `DeckFlow.Studio.csproj:22` pins `SSH.NET` Version `2025.1.0`. Test csproj has no SSH.NET reference. Core has no Renci/SSH.NET reference (grep confirmed). 47-03-SUMMARY lines 112-123 document operator approval of supply-chain gate. Commits `a1d14ed`, `a9f272d`, `e687b9b`, `9d45dc4`, `e85312b`, `6026419`, `ca9d824`, `a5c291c` all verified in git log. |

**Score:** 6/6 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Studio/Services/ISshArtifactUploader.cs` | ISshArtifactUploader interface + SshUploadRequest + SshUploadResult | VERIFIED | 53 lines; interface with `IReadOnlyList<SshUploadRequest>` signature; records with `RemoteRelativePath`; FailureReason doc states "Sanitized; never contains host/key/path secrets" |
| `DeckFlow.Studio/Services/IProdStoreFactory.cs` | IProdStoreFactory interface + ProdStoreFactory impl | VERIFIED | 33 lines; `ProdStoreFactory.Create` uses `PostgresConnectionStringNormalizer.Normalize` and `RelationalDatabaseProvider.Postgres`; D-03 `// Why:` comment present |
| `DeckFlow.Studio/Services/SftpArtifactUploader.cs` | SSH.NET SftpClient-backed implementation, >=60 lines | VERIFIED | 242 lines; one SftpClient per call; sequential foreach; `TryBuildRemotePath` path-traversal guard; `EnsureRemoteDirectory` walks segments; all three ex.Message references are inside `// Why:` comments only (not in returned strings) |
| `DeckFlow.Studio/Pages/DirectPush.razor` | 3-stage gated page @page /direct-push, >=250 lines | VERIFIED | 775 lines; `@page "/direct-push"`; 3-stage structure with btn-outline-primary (Stage 1) and two btn-danger (Stage 2/3); `WriteRowsAsync` hard-guard at line 668; `InvokeWriteRowsForTest()` seam at line 761 |
| `DeckFlow.Studio/Shared/NavMenu.razor` | Direct Push nav entry below Publish | VERIFIED | Line 33: `<NavLink class="nav-link" href="direct-push">` with `oi oi-data-transfer-upload` icon and "Direct Push" label |
| `DeckFlow.Studio.Tests/TestDoubles/FakeSshArtifactUploader.cs` | Per-file success/fail injection, no SSH.NET reference | VERIFIED | 39 lines; `HashSet<string> FilesToFail` keyed by `RemoteRelativePath`; `List<SshUploadRequest> UploadedFiles`; `progress?.Report(result)` per upload; no Renci/SSH.NET import |
| `DeckFlow.Studio.Tests/TestDoubles/FakeProdStoreFactory.cs` | Returns pre-seeded FakeContentSiteIndexStore | VERIFIED | 23 lines; `ArgumentNullException.ThrowIfNull` in ctor; `Create(string _)` returns `_prodStore` |
| `DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs` | UpsertMethodCalls tracking + fault injection | VERIFIED | 218 lines; `UpsertMethodCalls` at line 21; method names recorded in all three Upsert* methods; `KeysToFailOnUpsert`, `UpsertFailureMessage`, `ReadFailureMessage` fault-injection hooks; MEDIUM-4 full-row guard throws `InvalidOperationException` |
| `DeckFlow.Studio.Tests/DirectPushPageTests.cs` | 11 named bUnit tests, no stubs | VERIFIED | 365 lines; 11 method declarations matching exact names from plan; `DirectPush_NotConfigured_ButtonsDisabled` is `[Theory]` with 3 InlineData variants; `Render<DirectPush>()` un-stubbed; no "stub — implemented in 47-03" lines remain; `InvokeWriteRowsForTest()` seam used for MEDIUM-1 test |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `Program.cs` | `SftpArtifactUploader.cs` | `AddSingleton<ISshArtifactUploader, SftpArtifactUploader>()` | VERIFIED | `Program.cs:54` exact match |
| `Program.cs` | `IProdStoreFactory.cs` | `AddSingleton<IProdStoreFactory, ProdStoreFactory>()` | VERIFIED | `Program.cs:55` exact match |
| `DirectPush.razor` | `IProdStoreFactory.cs` | `ProdStoreFactory.Create(Configuration prod conn string)` inside Task.Run | VERIFIED | `DirectPush.razor:514-515,683-684` — `ProdStoreFactory.Create(rawConnStr)` in both ComputeDiffAsync and WriteRowsAsync; rawConnStr from `Configuration["Studio:ProdConnectionString"]` at action time |
| `DirectPush.razor` | `IContentSiteIndexStore.UpsertContentColumnsOnlyAsync` | `prodStore.UpsertContentColumnsOnlyAsync(row, _cts.Token)` | VERIFIED | `DirectPush.razor:695`; `UpsertRowAsync`/`UpsertRowPreservingVisibilityAsync` absent (grep confirmed) |
| `DirectPush.razor` | `ISshArtifactUploader.UploadArtifactsAsync` | `SshUploader.UploadArtifactsAsync(IReadOnlyList<SshUploadRequest>, progress, _cts.Token)` | VERIFIED | `DirectPush.razor:603-607,621` — requests built from `_approvedRows` with `SshUploadRequest(LocalPath, ArtifactPath)` |
| `NavMenu.razor` | `DirectPush.razor` | `NavLink href direct-push` | VERIFIED | `NavMenu.razor:33` |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `DirectPush.razor` diff preview | `_newCount`, `_updatedCount`, `_diffRows` | `ComputeDiffAsync` → `IndexStore.GetApprovedRowsAsync` + `prodStore.GetAllRowsAsync` → in-memory natural-key diff | Yes — real local store + real on-demand prod store query | FLOWING |
| `DirectPush.razor` SCP reconcile | `_fileResults` | `SshUploader.UploadArtifactsAsync` streaming via `Progress<SshUploadResult>` | Yes — per-file real SFTP result (or fake in tests) | FLOWING |
| `DirectPush.razor` DB reconcile | `_rowResults` | `prodStore.UpsertContentColumnsOnlyAsync` per-row try/catch | Yes — real prod upsert result (or fake in tests) | FLOWING |

### Behavioral Spot-Checks

Step 7b: SKIPPED — requires running Studio with prod/SCP secrets, which constitutes the live smoke test captured in human verification items. The codebase has no runnable entry point testable without external services for this phase's new surface. Build is known clean (per 47-03-SUMMARY).

### Probe Execution

Step 7c: No `probe-*.sh` files declared or present for this phase. Not applicable.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| PUB-04 | 47-01, 47-02, 47-03 | Direct prod-DB push: write approved rows via safe content-only upsert + upload matching markdown to Render /data via SCP; artifact-first ordering | SATISFIED | `SftpArtifactUploader` implements SCP upload. `DirectPush.razor` calls `UpsertContentColumnsOnlyAsync` only. Stage 3 gated on Stage 2 full success (`_scpSuccess`). 11 bUnit tests verify behavior. |
| PUB-05 | 47-01, 47-03 | Dry-run/preview + explicit confirmation; shows exactly which rows/artifacts; surfaces partial-failure for reconcile | SATISFIED | `ComputeDiffAsync` produces diff preview with New/Updated counts + per-row table before any write. `prodReviewed` checkbox gates SCP. Per-file and per-row reconcile lists rendered post-run. `DirectPush_DiffPreview_ShowsNewUpdatedCounts`, `DirectPush_ScpPartialFailure_Stage3Locked`, `DirectPush_DbPartialFailure_PerRowListShown` tests verify these behaviors. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `SftpArtifactUploader.cs` | 17, 87, 138 | `ex.Message` string literal | Info | All three occurrences are inside `// Why:` comment lines explaining why `ex.Message` is NOT used. No code path surfaces `ex.Message` in any return value or log call. Not a stub. |

No other anti-patterns found. No TBD/FIXME/XXX markers in any phase-modified file. No placeholder returns. No hardcoded empty collections as final values (initial state variables that get populated by real fetches are not stubs per verifier rules).

### Human Verification Required

### 1. Live End-to-End Publish Smoke (Item C)

**Test:** With real Render SSH secrets (Studio:Scp:Host, Studio:Scp:Port, Studio:Scp:Username, Studio:Scp:KeyFile, Studio:Scp:RemoteArtifactRoot) and prod Postgres connection string (Studio:ProdConnectionString) configured in user-secrets, run a full Stage 1 → Stage 2 → Stage 3 publish. Select 1-2 approved entries.
**Expected:** Files appear under /data/content-kb/{slug}/ on the Render disk. Prod rows are inserted/updated in the Postgres site-index. Pre-existing prod rows have is_visible and is_evergreen unchanged from before (query prod before and after). Per-file and per-row success lists show all green.
**Why human:** No live SSH or Postgres connection is made in CI. Requires operator prod secrets and a Render-registered SSH key. Item C was explicitly not run at the Task 3 checkpoint (47-03-SUMMARY lines 120-123 confirm items B3 and C were not run).

### 2. Live Banner Verify with Secrets Configured (Item B3)

**Test:** Run Studio locally (`DECKFLOW_DISABLE_AUTO_BROWSER=true`) with prod connection string and SCP secrets configured in user-secrets. Navigate to /direct-push.
**Expected:** "TARGET: PRODUCTION" red danger banner appears. SCP (btn-danger) button is disabled until the "I have reviewed what will be written to PRODUCTION above." checkbox is checked. DB (btn-danger) button shows "Locked until every artifact has uploaded successfully." hint and is disabled until SCP fully succeeds. Startup logs contain only "Studio prod connection: configured" and "Studio SCP: configured" — no secret values.
**Why human:** The unconfigured state was verified at the Task 3 checkpoint. The with-config live banner state (item B3) was not run. The UI state machine is proven by 13 bUnit tests, but the live visual render with real secrets present requires human inspection.

### Gaps Summary

No gaps. All must-have truths are VERIFIED at the code level. The two human verification items represent live-smoke testing that is inherently operator-only (requires prod secrets and Render SSH access) — not a code deficiency.

The phase goal is achieved in the codebase: artifact-first SCP path exists and is substantive (242-line SftpArtifactUploader with path-traversal guards, sanitized errors, sequential client), the Postgres safe upsert path is wired (UpsertContentColumnsOnlyAsync exclusively, on-demand factory), the diff preview shows exactly what will change (New/Updated counts + per-row table), partial-failure surfaces per-item (per-file and per-row reconcile tables, Stage 3 stays locked on SCP failure), and AI does not push main. 13 bUnit tests covering all critical paths pass. Security audit 14/14 CLOSED.

---

_Verified: 2026-06-16T20:30:00Z_
_Verifier: Claude (gsd-verifier)_
