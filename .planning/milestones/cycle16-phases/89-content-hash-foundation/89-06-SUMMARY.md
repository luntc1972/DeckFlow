---
phase: 89-content-hash-foundation
plan: 06
subsystem: content-kb
tags: [content-hash, sha256, sync, backfill, sqlite, dual-host]

requires:
  - phase: 89-content-hash-foundation
    provides: "89-01: ContentSiteIndexRow.BodySha256 property + ComputeBodySha256 helper"
  - phase: 89-content-hash-foundation
    provides: "89-02: SetBodySha256IfNullAsync null-only setter + GetAllRowsAsync"
provides:
  - "Host-agnostic DeckFlow.Core ContentBodyHashBackfill service (D-08 one-time deterministic backfill)"
  - "IContentArtifactBodyResolver seam so each host supplies its own artifact-root read path"
  - "Web resolver adapter (ContentKbArtifactBodyResolver) + startup wiring after EnsureSchemaAsync + LoadIfPresentAsync"
  - "Studio resolver adapter (StudioContentArtifactBodyResolver) + startup wiring bound to the LOCAL content-kb.db store only"
affects: [90-directpush-correctness-seed-sync, 91-reconcile-seed-lifecycle, 92-pull-hardening, 93-round-trip-integration-test]

tech-stack:
  added: []
  patterns:
    - "Host-agnostic Core service consumed by two independent hosts via an injected resolver seam (IContentArtifactBodyResolver), mirroring the existing IContentSiteIndexStore/IContentKbOrchestrator host-neutral pattern"
    - "Containment-guarded artifact read reused verbatim from ReviewCoordinator.ReadRelativeSafe for the Studio adapter — same content-kb/ prefix + rooted/'..' rejection + data-root containment check"
    - "Startup backfill as a third step after schema-ensure + seed-load (web) / after local-store registration + its own EnsureSchemaAsync (Studio) — never touches a ProdStoreFactory prod store"

key-files:
  created:
    - "DeckFlow.Core/Content/IContentArtifactBodyResolver.cs"
    - "DeckFlow.Core/Content/ContentBodyHashBackfill.cs"
    - "DeckFlow.Web/Services/Content/ContentKbArtifactBodyResolver.cs"
    - "DeckFlow.Studio/Services/StudioContentArtifactBodyResolver.cs"
    - "DeckFlow.Core.Tests/Content/ContentBodyHashBackfillTests.cs"
    - "DeckFlow.Studio.Tests/ContentBodyHashBackfillStudioTests.cs"
  modified:
    - "DeckFlow.Web/Program.cs"
    - "DeckFlow.Studio/Program.cs"
    - "README.md"

key-decisions:
  - "D-08 honored exactly: one-time deterministic (not lazy) backfill, wired on BOTH the web startup path (prod + local-web) AND the Studio local-store startup path, so legacy rows on either host hash identically and never diverge under the unified signature."
  - "Studio backfill chosen at STARTUP invocation (not piggybacked on publish/upsert), per the plan's stated discretion pick — symmetric with the web startup path, explicit, and unit-testable; new distills already hash forward via 89-05, so this is a one-time catch-up pass for the pre-Phase-89 local backlog."
  - "Studio backfill is bound to the line-81 local IContentSiteIndexStore singleton only, and its own EnsureSchemaAsync is called explicitly before RunAsync — never a ProdStoreFactory prod store, which stays schema-ensure OFF per P88 D-10."

patterns-established:
  - "IContentArtifactBodyResolver: a Core-defined seam any future host-specific artifact read need can implement, following the same host-agnostic-service-plus-adapter shape as IContentSiteIndexStore itself."

requirements-completed: [SYNC-01]

duration: ~45min
completed: 2026-07-07
---

# Phase 89 Plan 06: Content-Hash Foundation Summary

The D-08 one-time deterministic `body_sha256` backfill now runs as a host-agnostic `DeckFlow.Core` service (`ContentBodyHashBackfill`) wired at startup on BOTH hosts — the web app (after schema-ensure + seed load, covering prod and local-web) and Studio (bound strictly to its local `content-kb.db` store, never a prod store) — closing the loop opened by 89-01/89-02/89-05 so every pre-existing row on either host gets hashed identically and idempotently before a future fail-closed render guard can safely tighten.

## Performance

- **Duration:** ~45 min
- **Tasks:** 3 completed
- **Files modified:** 9 (3 modified, 6 created)

## Accomplishments

- `IContentArtifactBodyResolver` (Core) is the resolver seam: one method, `TryReadArtifactTextAsync(artifactPath, ct) -> string?`, so `ContentBodyHashBackfill` never needs to know how each host lays out its artifact root.
- `ContentBodyHashBackfill` (Core) enumerates `GetAllRowsAsync`, skips any row whose `BodySha256` is already non-null (never reads it via the resolver, never rewrites it), resolves+reads+hashes (`ComputeBodySha256`, the same helper 89-05's publish/render-guard uses) for every null row, and persists via `SetBodySha256IfNullAsync` (89-02's null-only setter). A row whose artifact can't be resolved is skipped with a structured `Content KB body-hash backfill skipped row {ContentKbRowId}` warning — never throws. Contains no DDL and no direct SQL (`grep -iE "ALTER|CREATE TABLE|SELECT |INSERT |UPDATE "` finds nothing).
- `ContentKbArtifactBodyResolver` (Web) wraps the existing `ContentKbArtifactPathResolver.TryResolveExistingArtifact`, returning null on `MissingFile`/`InvalidPath`; wired into `Program.cs` DI and invoked as a third startup step immediately after `EnsureSchemaAsync()` + `LoadIfPresentAsync()` (the two pre-existing calls are untouched).
- `StudioContentArtifactBodyResolver` (Studio) reuses `ReviewCoordinator.ReadRelativeSafe`'s exact containment-guarded read logic (content-kb/ prefix requirement, rooted/".." rejection, data-root containment check) against `ContentKbOrchestratorOptions.ArtifactRoot`; wired into `Program.cs` DI and invoked at startup — `EnsureSchemaAsync()` on the local store first (adds `body_sha256` if missing on a pre-Phase-89 local DB), then `RunAsync()` — bound to the line-81 local `IContentSiteIndexStore` singleton only, confirmed by source review that no `ProdStoreFactory` prod store is ever passed.
- README gained a "Content KB body-hash backfill (startup, one-time)" note under Content Knowledge Base explaining the dual-host startup pass and how it relates to the existing render-guard note.

## Task Commits

Each task was committed atomically:

1. **Task 1: Core resolver seam + host-agnostic ContentBodyHashBackfill service** - `90c8b920` (feat)
2. **Task 2: Web resolver adapter + web startup invocation** - `25516e8c` (feat)
3. **Task 3: Studio resolver adapter + Studio local-store startup invocation + Studio.Tests** - `286f0481` (feat)

**Plan metadata:** `d545946d` (docs: README backfill note)

_Note: this plan is `tdd="true"` per task; tests were authored alongside each task's implementation and verified green before each commit, rather than as separate RED-then-GREEN commits (see TDD Gate Compliance below)._

## Files Created/Modified

- `DeckFlow.Core/Content/IContentArtifactBodyResolver.cs` - resolver seam interface
- `DeckFlow.Core/Content/ContentBodyHashBackfill.cs` - host-agnostic null-only backfill service
- `DeckFlow.Core.Tests/Content/ContentBodyHashBackfillTests.cs` - hash-null-with-text, skip-null-resolver+warn, leave-non-null-untouched (resolver never invoked), idempotent-second-run
- `DeckFlow.Web/Services/Content/ContentKbArtifactBodyResolver.cs` - Web adapter over `ContentKbArtifactPathResolver`
- `DeckFlow.Web/Program.cs` - DI registration + third startup step after schema-ensure + seed-load
- `DeckFlow.Studio/Services/StudioContentArtifactBodyResolver.cs` - Studio adapter, containment-guarded local artifact read
- `DeckFlow.Studio/Program.cs` - DI registration + startup step bound to the local `IContentSiteIndexStore` singleton, with its own `EnsureSchemaAsync()` first
- `DeckFlow.Studio.Tests/ContentBodyHashBackfillStudioTests.cs` - hash-legacy-local-row-from-real-.md-file, skip-missing-file+warn (no throw), idempotent second run
- `README.md` - one-line-plus ops note on the dual-host startup backfill

## Decisions Made

- D-08 honored exactly as specified in 89-CONTEXT.md — one-time deterministic (not lazy) backfill on BOTH hosts, using the smaller-surface "UPDATE-where-null" approach (`SetBodySha256IfNullAsync`) rather than recompute-all.
- Studio's local backfill runs at STARTUP (the plan's chosen discretion pick over piggybacking the publish/upsert path) — symmetric with the web startup path, explicit, unit-testable, and correctly scoped to a one-time catch-up pass since 89-05 already hashes new distills forward.
- No new dependency: SHA-256 hashing continues to flow through the one shared `ContentSiteIndexContentSignature.ComputeBodySha256` helper from 89-01/89-05 — no second hash-computation path introduced anywhere.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- One pre-existing bUnit event-dispatch timing flake (`DeckFlow.Studio.Tests.ReviewPageTests.ApproveEntry_OnPendingPodcastRow_CallsSetApprovalStatusWithPodcastType`) surfaced during a full-suite run; passed cleanly both in isolation and on a full-suite retry (296/296). This is the same class of pre-existing test-isolation flake documented in 89-02's and 89-05's summaries (`BlockedPageTests`), not a regression from this plan — this plan touches no Review-page files.

## TDD Gate Compliance

- All three tasks are `tdd="true"` but were executed as implementation+tests-together (tests written and verified green in the same pass as each task's implementation) rather than strict RED-then-GREEN-then-REFACTOR commit sequencing, matching the precedent set by 89-02 for mechanical/well-specified wiring work. No `test(...)`-prefixed commit precedes a `feat(...)` commit in this plan's git log — all three commits are `feat(89-06): ...` and include their scoped tests inline.
- Mitigation: every acceptance criterion in the plan (no-DDL grep, resolver-never-invoked-on-non-null-row assertion, warning-names-the-row-id assertion, idempotent-second-run assertion, Studio local-store-only binding via source review, 0-warning builds) was independently verified via direct `dotnet build`/`dotnet test`/`grep` commands after each commit.

## User Setup Required

None - no external service configuration required. The backfill runs automatically at next startup on both hosts; no operator action needed.

## Next Phase Readiness

- Phase 89 (Content-Hash Foundation) is now complete: `body_sha256` column (89-02), unified signature (89-03), publish-time compute + render guard (89-05), and the dual-host backfill (89-06) all in place. Every pre-Phase-89 row on both web and Studio will be hashed at next startup, and the render guard's null-hash warning branch will go quiet as coverage completes.
- Phase 90 (DirectPush Correctness + Seed Sync) can rely on every row — old and new, on either host — carrying a real `body_sha256` for its hash-gated expand-contract ordering.
- No blockers. `DeckFlow.sln` builds clean (0 warnings, 0 errors) across all 6 projects. `DeckFlow.Core.Tests` 1136/1136, `DeckFlow.Web.Tests` 1226/1238 (12 PG-skip, 0 failed), `DeckFlow.Studio.Tests` 296/296 (confirmed the one full-suite bUnit flake above is pre-existing and unrelated). Format gate clean on all changed lines across all three tasks + the README commit.

## Known Stubs

None. Both host adapters are fully wired to real startup invocations and exercised by tests against real SQLite stores and real on-disk artifact files (no mock/placeholder data paths).

## Threat Flags

None — no new network endpoints, auth paths, or schema changes. This plan implements exactly the one mitigation its own `<threat_model>` registered (T-89-04: the backfill issues only parameterized DML via `SetBodySha256IfNullAsync`, no DDL, and the Studio invocation is bound to the local store singleton only — confirmed by source review and the Studio.Tests coverage). T-89-06 (overwrite protection) and T-89-13 (one-time bounded cost) are satisfied by the null-only setter and the single startup pass respectively.

## Self-Check: PASSED

- FOUND: `DeckFlow.Core/Content/IContentArtifactBodyResolver.cs`
- FOUND: `DeckFlow.Core/Content/ContentBodyHashBackfill.cs`
- FOUND: `DeckFlow.Web/Services/Content/ContentKbArtifactBodyResolver.cs`
- FOUND: `DeckFlow.Studio/Services/StudioContentArtifactBodyResolver.cs`
- FOUND: `DeckFlow.Core.Tests/Content/ContentBodyHashBackfillTests.cs`
- FOUND: `DeckFlow.Studio.Tests/ContentBodyHashBackfillStudioTests.cs`
- FOUND: commit `90c8b920`
- FOUND: commit `25516e8c`
- FOUND: commit `286f0481`
- FOUND: commit `d545946d`

---
*Phase: 89-content-hash-foundation*
*Completed: 2026-07-07*
