---
phase: 90-directpush-correctness-seed-sync
verified: 2026-07-08T02:59:45Z
status: passed
score: 4/4 must-haves verified (SYNC-07, SYNC-08, SYNC-09, SYNC-10)
overrides_applied: 0
deferred:
  - truth: "The true end-to-end round-trip (DirectPush expand -> real Render autodeploy -> live deployed-body-hash endpoint 200+hash-match -> Stage 5 confirms -> visible) is exercised against a live/production-like deploy."
    addressed_in: "Phase 93"
    evidence: "ROADMAP Phase 93 goal: 'The entire sync loop — distill through reconcile — is locked by one automated end-to-end test ... runs against containerized Postgres + a real git tree.' Requirement SYNC-16. 90-CONTEXT.md <deferred> section explicitly states 'End-to-end containerized round-trip integration test -> Phase 93 (SYNC-16)'. Every Phase 90 plan's <verification> block independently flags this as 'MANUAL (async, live Render — NOT unit-coverable)' and defers it to 'operator flag-flip time' / Phase 93, not to this phase's close-out gate."
---

# Phase 90: DirectPush Correctness + Seed Sync Verification Report

**Phase Goal:** DirectPush converges to the same consistent end-state as Publish — bodies reach prod only through git, and a redeploy can never revert or leave a DirectPush'd row half-consistent.
**Verified:** 2026-07-08T02:59:45Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria, mapped 1:1 to SYNC-07..10)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | With `sync.directpush-gitbody` ON, a DirectPush'd row's body is served exclusively from git `/app` — the `/data` SFTP overlay is no longer part of the serving path. | VERIFIED | `ContentKbArtifactPathResolver.TryResolveExistingArtifact` (`DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs:126-129`): on a git-tree miss, `if (_flagCache.IsEnabled("sync.directpush-gitbody")) return MissingFile;` short-circuits BEFORE the `DataOverlayBase` block (line 131+). Flag OFF path is untouched (byte-identical). `ContentKbController.Detail` (`Controllers/ContentKbController.cs:128-133`) returns real `NotFound()` under the flag instead of the 200 "artifact unavailable" shell. Flag registered `FeatureFlagCatalog.cs:97-99`, seeded `FALSE` both dialects (`FeatureFlagStore.cs:234`, `:274`). |
| 2 | DirectPush re-exports `index-seed.json` (like Publish), so a fresh prod reseed reconstructs the DirectPush'd row instead of reverting it. | VERIFIED | `DirectPushCoordinator.CommitAndPushBodiesAsync` (`DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs:455-463`) calls the SAME `_orchestrator.ExportIndexToFileAsync(seedAbsPath, ...)` factory `PublishCoordinator` uses (no forked writer), throws on export failure before any body copy, and stages `SeedRelative = "content-kb/seed/index-seed.json"` alongside the copied bodies (`:492-495`) on EVERY invocation (not gated on changedCount). |
| 3 | `is_visible` flips only after the body has been committed, deployed, and hash-verified at `/app` — a row is never visible before its body is reachable. | VERIFIED | `DirectPushCoordinator.WriteContentAsync` (`:242-259`) performs ONLY `UpsertContentColumnsOnlyBatchAsync` + `SetAwaitingConfirmAsync` — no stamp/visibility call (grep-confirmed absent). `VerifyAndPublishAsync` (`:296-325`) calls `IDeployedBodyConfirmer.IsDeployedBodyConfirmedAsync` per row and invokes `ConfirmAndPublishAsync` ONLY for confirmed rows. `DeployedBodyConfirmer` (`DeckFlow.Studio/Services/DeployedBodyConfirmer.cs`) polls `GET Admin/api/contentkb/deployed-body-hash` (bounded 5 attempts / 3s backoff) and returns true only on `200 && bodySha256==expected` (ordinal, line ~113-121). The endpoint (`DeckFlow.Web/Controllers/Admin/ContentKbDeployedBodyController.cs`) resolves the natural-key row via unfiltered `GetByNaturalKeyAsync` (not `is_visible`-gated), resolves via the flag-INDEPENDENT `TryResolveGitArtifact` (git-only, no `DataOverlayBase` reference — grep-confirmed), 404s on a missing `/app` artifact, else returns `{ bodySha256 }` via the shared `ContentSiteIndexContentSignature.ComputeBodySha256` (no second hash routine). UI-level enforcement: `DirectPush.razor.cs` Stage 5 (`RunVerifyAndPublishAsync`, line 554+) is the only caller of `VerifyAndPublishAsync`; Stage 3 (`WriteRowsAsync`) calls only `WriteContentAsync`. |
| 4 | `pushed_to_prod_utc` is stamped only after prod confirms the deployed body, so a live DirectPush'd row never shows a "Never published" badge. | VERIFIED | `ConfirmAndPublishAsync` (`DirectPushCoordinator.cs:270-285`) is the ONLY method calling `StampPushedToProdAsync`/`SetVisibilityAsync`, in the original prod-first-then-local order, and is invoked exclusively from inside `VerifyAndPublishAsync` for confirmed rows. `WriteContentAsync` never stamps. The durable `awaiting_confirm_utc` marker (`ContentSiteIndexStore.cs:140-149` idempotent ALTER both dialects; `:688-763` `SetAwaitingConfirmAsync`/`ClearAwaitingConfirmAsync`, both keyed ONLY on `(natural_key_type, natural_key_value)` — no timestamp WHERE, F-51-PG-01 avoided) is set at content-write time and cleared post-confirm, making a mid-flight push durable and resumable (`DirectPushCoordinator.GetAwaitingConfirmRowsAsync`, `DirectPush.razor` "Awaiting Confirm — Resume Interrupted Push" card, `DirectPush.razor.cs:629` `ResumeVerifyAsync`). |

**Score:** 4/4 truths verified.

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | Live end-to-end round-trip against a real Render deploy (DirectPush -> autodeploy -> deployed-body-hash 200+hash-match -> Stage 5 confirms -> visible) | Phase 93 | ROADMAP Phase 93 (SYNC-16) is the containerized round-trip integration test; 90-CONTEXT.md `<deferred>` section explicitly assigns this to Phase 93; every 90-0x plan's `<verification>` block independently marks this "MANUAL (async, live Render — NOT unit-coverable)" and defers it, not treating it as a Phase 90 closure gate. |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` | `sync.directpush-gitbody` description | VERIFIED | Line 97-99, non-empty description. |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` | seeded FALSE both dialects | VERIFIED | Postgres line 234 `FALSE`, SQLite line 274 `0`. |
| `DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs` | flag-gated git-only resolution + flag-independent git-only method | VERIFIED | `TryResolveExistingArtifact` (flag-gated, :126-129) and `TryResolveGitArtifact` (flag-independent, :160-182, no `DataOverlayBase` reference). |
| `DeckFlow.Web/Controllers/ContentKbController.cs` | 404 under flag on MissingFile | VERIFIED | `Controllers/ContentKbController.cs:128-133`. |
| `DeckFlow.Web/Controllers/Admin/ContentKbDeployedBodyController.cs` | deployed-body-hash endpoint | VERIFIED | Full file read; `/Admin`-routed (inherits BasicAuth via `Program.cs:226` path-prefix branch), no `SameOriginRequestValidator` call, `GetByNaturalKeyAsync` (unfiltered) + `TryResolveGitArtifact` + `ComputeBodySha256`, 400/404/200 paths all present. |
| `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` | split write/confirm/verify methods | VERIFIED | `WriteContentAsync` (:242), `ConfirmAndPublishAsync` (:270), `VerifyAndPublishAsync` (:296), `GetAwaitingConfirmRowsAsync` (:337), seed re-export + flag-gated `[skip render]` in `CommitAndPushBodiesAsync` (:375-531). |
| `DeckFlow.Studio/Services/IDeployedBodyConfirmer.cs` / `DeployedBodyConfirmer.cs` | bounded hash-match poller | VERIFIED | Own files (not inlined); 5-attempt/3s-backoff bounded loop; fails closed on missing config; ordinal string compare only. |
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` | `awaiting_confirm_utc` column + set/clear | VERIFIED | Idempotent ALTER both dialects (:140-149), CREATE TABLE both dialects (:1230, :1255), `SetAwaitingConfirmAsync`/`ClearAwaitingConfirmAsync` (:688-763) keyed only on natural key. |
| `DeckFlow.Studio/Services/GitBodyCoverageAudit.cs` (D-11) | read-only pre-flip audit | VERIFIED | Depends only on `IProdContentReader`; grep confirms no `IProdStoreFactory`/write/DDL reference in the file. |
| `DeckFlow.Studio/StudioConfig.cs`, `Pages/DirectPush.razor(.cs)` | `IsConfirmerConfigured` badge/gate + Stage 5 + resume UI | VERIFIED | `StudioConfig.cs:17` third flag (default `false`, fail-closed); `DirectPush.razor` renders a third badge and gates Stage-1/Stage-5 actions on it; Stage 5 (`RunVerifyAndPublishAsync`) hard-guards on `_gitSuccess`; resume card wired to `GetAwaitingConfirmRowsAsync`/`ResumeVerifyAsync`. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `ContentKbArtifactPathResolver.TryResolveExistingArtifact` | `IFeatureFlagCache.IsEnabled("sync.directpush-gitbody")` | inline flag read short-circuiting overlay | VERIFIED | Line 126-129, before the `DataOverlayBase` block. |
| `DirectPushCoordinator.CommitAndPushBodiesAsync` | `IContentKbOrchestrator.ExportIndexToFileAsync` | shared seed-export factory | VERIFIED | :455-463, same factory Publish uses. |
| `DeployedBodyConfirmer` | `GET /Admin/api/contentkb/deployed-body-hash` | natural-key hash-match poll, admin BasicAuth | VERIFIED | URL built from `Studio:PublicSiteBaseUrl`, BasicAuth header from `Studio:AdminUser/AdminPassword`, ordinal hash comparison. |
| `DirectPushCoordinator.VerifyAndPublishAsync` | `ConfirmAndPublishAsync` | gated on confirmer result | VERIFIED | `:319-322`, only `confirmed` rows passed. |
| `ConfirmAndPublishAsync` | `prodStore.StampPushedToProdAsync` + `SetVisibilityAsync` | prod-first-then-local, post-confirm only | VERIFIED | `:280-284`, exact original 4-call order preserved + marker clear. |
| `ContentKbDeployedBodyController` | `IContentSiteIndexStore.GetByNaturalKeyAsync` + `TryResolveGitArtifact` + `ComputeBodySha256` | natural-key lookup -> git resolve -> hash | VERIFIED | Full chain read in controller source. |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| SYNC-07 | 90-01 | Bodies reach prod only via git `/app`; `/data` overlay dropped | SATISFIED | Resolver + controller flag-gate verified above. |
| SYNC-08 | 90-04 | DirectPush re-exports `index-seed.json` via shared factory | SATISFIED | `CommitAndPushBodiesAsync` seed export/staging verified above. |
| SYNC-09 | 90-05, 90-06, 90-07 | Hash-gated expand-contract ordering | SATISFIED | Endpoint + confirmer + coordinator split + UI Stage 5 gating all verified above. |
| SYNC-10 | 90-05, 90-03 | `pushed_to_prod_utc` stamped only post-confirm | SATISFIED | `ConfirmAndPublishAsync` is the sole stamp path, gated on confirm; marker durability verified above. |

No orphaned requirements: REQUIREMENTS.md traceability table maps exactly SYNC-07..10 to Phase 90, all four claimed `[x]` and all four independently confirmed against source.

### Anti-Patterns Found

Scanned all phase-touched production files (resolver, controller, catalog/store, coordinator, confirmer, StudioConfig, DirectPush.razor/.razor.cs, ContentSiteIndexStore, ContentArtifactSpec) for `TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER` and "not yet implemented"/"coming soon" phrasing.

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `ContentArtifactSpec.cs:17-18` | doc-comment example `video_id: "XXXXXXXXXXX"` | `XXX` substring | none | False positive — a documentation placeholder YouTube ID in an XML doc example, not a debt marker. No action needed. |

No blocking debt markers found in phase-touched code.

### Build / Test Gate (run directly, not trusted from SUMMARY)

Ran via `dotnet.exe` (Windows) from WSL per CLAUDE.md convention.

```
dotnet build DeckFlow.sln -c Debug
  Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test DeckFlow.Core.Tests -c Debug --no-build
  Passed! Failed: 0, Passed: 1149, Skipped: 0, Total: 1149

dotnet test DeckFlow.Studio.Tests -c Debug --no-build
  Passed! Failed: 0, Passed: 347, Skipped: 3 (Postgres-gated), Total: 350

dotnet test DeckFlow.Web.Tests -c Debug --no-build
  Passed! Failed: 0, Passed: 1249, Skipped: 12 (Postgres-gated), Total: 1261
```

All three test projects match the counts independently claimed across the 90-0x SUMMARY.md files (Core 1149 / Studio 347+3 / Web 1249+12) — no drift, no regressions introduced by this verification's re-run.

### Human Verification Required

None blocking this phase's close-out. The one live-deploy round-trip item is explicitly deferred to Phase 93 (see Deferred Items above) per the phase's own CONTEXT.md scope fence and every plan's `<verification>` block — it was never scoped as a Phase 90 closure gate.

### Gaps Summary

None. All four ROADMAP success criteria for Phase 90 are independently verified against the actual shipped source (not SUMMARY narrative): the git-only serving flip is flag-gated and byte-identical when OFF; DirectPush re-exports the seed through the same factory Publish uses; `is_visible`/`pushed_to_prod_utc` only flip after a natural-key hash-match confirm against a dedicated, is_visible-independent, git-`/app`-only, authenticated endpoint; the awaiting-confirm marker makes the mid-flight state durable and resumable; the D-03 approval-mirror and operator-owned visibility columns (is_visible/is_hidden/is_evergreen) are provably excluded from the content-only upsert SQL. Build is clean (0/0) and all three test projects pass with the exact counts the summaries claim, independently re-run.

---

*Verified: 2026-07-08T02:59:45Z*
*Verifier: Claude (gsd-verifier)*
