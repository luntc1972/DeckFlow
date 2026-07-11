# Milestone: Cycle 16 — Content-KB Prod<->Git<->Studio Sync Hardening

**Status:** SHIPPED 2026-07-11
**CalVer:** 2026.07.3
**Phases:** 88-93
**Total Plans:** 30 (88: 3, 89: 6, 90: 7, 91: 9, 92: 2, 93: 3)

## Overview

A Studio/ops hardening cycle that makes the Content-KB the site serves exactly
what the operator published — no drift, no corruption, no ghost rows. Design
stance: **git is the single source of truth for bodies**; the prod DB
`content_site_index` row is subordinate and reconstructable from the git seed.
All sync is an idempotent one-way keyed upsert; every prod state change is
reflected in the git seed; bodies are content-hashed end-to-end; DirectPush
uses expand->verify->contract deploy ordering. No CDC/queues (fits 512MB/Render).

Two feature flags ship **OFF** by design (`sync.directpush-gitbody`,
`sync.reconcile`); flipping them ON in prod is gated behind an operator pre-flip
walk (`93-PREFLIP-CHECKLIST.md`, FU-1/FU-2/FU-3). All 17 SYNC requirements are
code-satisfied and verified.

## Phases

### Phase 88: Index-Row Integrity Hotfix

**Goal**: Kill the two live prod correctness bugs — a row can never be publicly
visible while `pending`, and sync diffing can never cross-match rows on a
colliding surrogate key.
**Depends on**: Nothing (first — live prod bugs, ships ahead of the hash foundation)
**Requirements**: SYNC-04, SYNC-05, SYNC-06
**Plans**: 3 (2 waves)

- [x] 88-01: Store approved-write mirror + serve-side approval filter + schema-ensure OFF switch
- [x] 88-02: Shared ContentNaturalKey helper + composite-key classifier + stored vocabulary + skip-log
- [x] 88-03: Studio coordinator dedup + ProdStoreFactory schema-ensure-off + no-DDL comment sweep

### Phase 89: Content-Hash Foundation

**Goal**: Every row's body is hashed end-to-end on one unified signature, so
drift is a single indexed comparison and body corruption (e.g. mojibake) is
detectable instead of silently served.
**Depends on**: Phase 88
**Requirements**: SYNC-01, SYNC-02, SYNC-03
**Plans**: 6 (3 waves)

- [x] 89-01: Shared ComputeBodySha256 helper + body-inclusive BuildSignature + ContentSiteIndexRow.BodySha256
- [x] 89-02: Store body_sha256 DDL/model/upsert plumbing + null-only backfill setter
- [x] 89-03: Delete Fingerprint, switch classifier to unified signature + one-signature-surface guard test
- [x] 89-04: index-seed.json export/load bodySha256 round-trip + golden fixture
- [x] 89-05: Publish-time hash compute + detail-render fail-open guard with structured warning
- [x] 89-06: One-time deterministic startup backfill pass (idempotent, DDL-free)

### Phase 90: DirectPush Correctness + Seed Sync (flag `sync.directpush-gitbody`)

**Goal**: DirectPush converges to the same consistent end-state as Publish —
bodies reach prod only through git, and a redeploy can never revert or leave a
DirectPush'd row half-consistent.
**Depends on**: Phase 89 (needs the body hash to hash-gate the ordering)
**Requirements**: SYNC-07, SYNC-08, SYNC-09, SYNC-10
**Plans**: 7 (4 waves)

- [x] 90-01: Git-body serving flip + `sync.directpush-gitbody` registration (seeded OFF)
- [x] 90-02: Read-only pre-flip git-coverage audit (Studio)
- [x] 90-03: Durable awaiting-confirm marker column (Core store, both dialects)
- [x] 90-04: Seed re-export via shared factory + drop [skip render] under flag + read-only Studio prod-flag accessor
- [x] 90-05: Coordinator re-plumb: split write, hash-match confirmer, post-confirm stamp/visibility
- [x] 90-06: DirectPush page expand->verify->contract re-sequencing + durable resume
- [x] 90-07: Authenticated deployed-body-hash endpoint (/app-only, natural-key, hash-match confirm surface)

### Phase 91: Reconcile + Seed Lifecycle (flag `sync.reconcile`)

**Goal**: Prod-side drift is detectable and reconcilable, and rows removed from
the seed actually leave prod — safely, gated behind a seed-ownership marker so a
bad seed can't mass-delete live rows.
**Depends on**: Phase 90
**Requirements**: SYNC-17, SYNC-11, SYNC-12
**Plans**: 9 (7 waves)

- [x] 91-01: Core seed_managed column + null-only backfill setter + shared seed-index reader
- [x] 91-02: Write-path stamping (seed loader + DirectPush) + ProdContentReader read extension
- [x] 91-03: Host-agnostic SeedManagedBackfill + dual-host wiring
- [x] 91-04: Pure Core 4-class reconcile classifier + discrepancy records/IDs
- [x] 91-05: Local SQLite discrepancy store: idempotent, resolution-by-absence, scope tags
- [x] 91-06: Studio reconcile orchestrator (prod read + git enum + seed parse) + report
- [x] 91-07: Dry-run coordinator + Reconcile page
- [x] 91-08: sync.reconcile flag + gated re-validated soft-hide Apply + Apply UI
- [x] 91-09: Operator human-verify checkpoint — APPROVED via fixture driver (ReconcileFixtureDriveTests); live UI/prod walk = FU-3 pre-flip gate

### Phase 92: Pull Hardening (no flag — Pull writes local-only, always on)

**Goal**: Pull-from-Prod adopts prod's state field-by-field without clobbering
operator-owned data or acting on a stale local checkout.
**Depends on**: Phase 91
**Requirements**: SYNC-13, SYNC-14, SYNC-15
**Plans**: 2 (2 waves)

- [x] 92-01: Core git behind-detection seam (Fetch/GetBehindCount) + BodyDivergenceStatus model
- [x] 92-02: Coordinator + page hardening (merged): staleness warn-then-proceed + freshness banner + divergence stamping/badge + per-entry opt-in + field-authority regression lock

### Phase 93: Round-Trip Integration Test (no flag; also the pre-flag-flip gate)

**Goal**: The entire sync loop — distill through reconcile — is locked by one
automated end-to-end test so future changes can't silently reintroduce any of
the fixed classes of drift.
**Depends on**: Phase 92
**Requirements**: SYNC-16
**Plans**: 3

- [x] 93-01: Test-host wiring (DeckFlow.Studio ProjectReference) + round-trip harness scaffold (real PG schema, real git temp repo, deterministic seams) + boot smoke [PostgresFact]
- [x] 93-02: SYNC-16 round-trip [PostgresFact]: full loop + hash-at-every-hop + no-revert-after-reseed + Pull field-authority + reconcile idempotent
- [x] 93-03: Operator pre-flip checklist (FU-1/FU-2/FU-3 + live flip steps for both flags)

---

## Milestone Summary

**Key Decisions:**
- Git = single source of truth for bodies; prod row subordinate + reconstructable from seed (no CDC/queues on 512MB Render).
- Both new flags (`sync.directpush-gitbody`, `sync.reconcile`) ship OFF; flip gated behind operator pre-flip walk.
- DirectPush D-09 confirm re-architected mid-cycle: from "public detail GET = reachable" (unsound — 4 race/404 failure modes) to an authenticated `/Admin/api/contentkb/deployed-body-hash` endpoint + hash-match poll (Codex plan-review HIGH).
- Reconcile removal = soft-hide only (`is_visible=false`, row retained), seed-drift rows only, two independent ownership gates (in-memory pre-filter + atomic `AND seed_managed=TRUE` SQL) — a prod-owned row is structurally unhideable.
- Pull bodies come from the git tree, not an SFTP download of prod `/data` (Codex rebuild adjudicated against — prod `/data` empty by design).

**Issues Resolved (live prod bugs killed):**
- C1 visible-while-pending rows (SYNC-04); C4 PinId key-collision cross-match (SYNC-05); M5 body-blind checksums / mojibake invisible (SYNC-01/03); M1/M3 /app-shadows-/data unreachable-or-stale body (SYNC-07); M6 premature pushed_to_prod stamp / "Never published" badge (SYNC-10); C2 additive-only seed reload never deletes (SYNC-12); M4 no prod reconcile (SYNC-11).

**Milestone-audit tech-debt (all CLEARED post-execution, 2026-07-11, commits `faade5f3..fbcbb73f`):**
- Seed relative path promoted to Core `ContentKbPaths.SeedRelativePath` (Web/Studio/CLI derive).
- Path-traversal guard consolidated to one public Core `ContentKbArtifactPath` (Web + Studio delegate; +19 Core tests); store's `IsWindowsRootedPath` reuses it.
- Shared Core read DTO/mapper/column-const (`ContentSiteIndexReadModel`/`ContentSiteIndexRowMapper`/`ContentSiteIndexReadColumns`); killed the ProdContentReader `awaiting_confirm_utc` read drift.
- Prod-connection seam `IStudioProdConnectionSource` replaces the inline `Studio:ProdConnectionString` idiom across DirectPush/Reconcile/PullFromProd coordinators + ContentKbReconcileOrchestrator.
- GitBodyCoverageAudit wired into Studio DI + a `/git-body-coverage` Blazor page + NavMenu + bUnit tests, referenced as the 0-missing gate in the pre-flip checklist.
- A dedicated security review of these surfaces (path guard, D-07 secret handling, admin endpoint, destructive Apply, prod TLS) found zero HIGH/MEDIUM findings.

**Deferred / operator-owned (post-ship — flags OFF at ship):**
- FU-3: live Studio-UI + real Render-Postgres + real flag-flip reconcile walk before flipping `sync.reconcile` (91-VERIFICATION `human_needed`).
- SYNC-16 real-deploy leg: the round-trip harness is green locally (Testcontainers PG); the real git-push -> Render redeploy -> /app confirm round-trip is a manual Docker/operator gate (D-07), not CI-enforced.
- FU-1 (updated-visible stale-body window: accept-by-design, copy corrected) and FU-2 (ON-row strand after indeterminate flag read: code fixed `53cfb036`) — recorded in the pre-flip checklist.

**Deferred backlog (not this cycle):**
- SYNC-F1 retire DirectPush (fold into Publish); SYNC-F2 scheduled/automatic reconcile runs.
- ReconcileCoordinator second prod-read simplification (destructive path, kept as defense-in-depth); TryResolveContained per-row GetFullPath hoist (low-sev).
