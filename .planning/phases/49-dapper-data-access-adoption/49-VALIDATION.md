---
phase: 49
slug: dapper-data-access-adoption
status: validated
nyquist_compliant: true
wave_0_complete: true
created: 2026-06-14
validated: 2026-06-14
---

# Phase 49 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Core.Tests + DeckFlow.Web.Tests) |
| **Config file** | none — existing test projects |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` (0/0 gate; VSTest unreliable in WSL) |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests` + `... test DeckFlow.Web.Tests` (run from Windows / CI; SQLite side) |
| **PG parity command** | `PostgresFactAttribute`-gated tests via `PostgresContainerFixture` (gated manual / env-flagged — NOT default CI) |
| **Estimated runtime** | ~build <60s; SQLite suite minutes; PG container start adds ~10-20s |

---

## Sampling Rate

- **After every task commit:** `dotnet build DeckFlow.sln` clean (0 errors / 0 new warnings)
- **After every plan wave:** Full `DeckFlow.Core.Tests` + `DeckFlow.Web.Tests` (SQLite) green; per-store tests for the converted store
- **After the spike (Wave 1 / FeedbackStore):** Spike gate evaluated, `49-GATE-VERDICT.md` written (PASS proceeds; FAIL halts), then the `49-01b` blocking decision gate writes `49-GATE-ABORT.md` (AUTHORIZED proceeds; ABORTED stops the phase — sweep plans 49-02/03/04 are not dispatched)
- **Before `/gsd:verify-work`:** Full suite green on SQLite + PG round-trip parity test (REQ-2) green on both providers
- **Max feedback latency:** build <60s; suite per wave

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | Test Class / Artifact | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-----------------------|--------|
| 49-01 T1 | 49-01 | 1 | REQ-1, REQ-2 | T-49-02, T-49-03, T-49-SC | Idempotent handler registration; fail-closed Parse | unit/build | `build DeckFlow.sln` + grep AddTypeHandler / MatchNamesWithUnderscores / EnsureRegistered | DapperTypeHandlers.cs (build gate) | ✅ green |
| 49-01 T2 | 49-01 | 1 | REQ-2 | T-49-02 | Round-trip parity (D-05) on both providers | unit | `test ... --filter ~DapperTypeHandlerRoundTrip` | **DapperTypeHandlerRoundTripTests** (Wave 0 — created+run by this task; SQLite [Fact] + [PostgresFact]) | ✅ green |
| 49-01 T3 | 49-01 | 1 | REQ-3 | T-49-01 | Zero store-local coercion; SQL verbatim/parameterized | unit | `test ... --filter ~Feedback` + comment-filtered coercion grep == 0 | AdminFeedbackControllerTests + feedback tests; **DapperTypeHandlerRoundTripTests** (REQ-2 proof) | ✅ green |
| 49-01 T4 | 49-01 | 1 | REQ-3 | T-49-01 | Objective falsifiable verdict | doc | `grep VERDICT: (PASS\|FAIL)` in 49-GATE-VERDICT.md | 49-GATE-VERDICT.md | ✅ green |
| 49-01b T1 | 49-01b | 1 | REQ-3 | T-49-GATE | Structural abort gate (blocking decision) | checkpoint:decision | `grep GATE: (AUTHORIZED\|ABORTED)` in 49-GATE-ABORT.md | 49-GATE-ABORT.md | ✅ green |
| 49-02 T1 | 49-02 | 2 | REQ-2, REQ-4 | T-49-04, T-49-05 | 5th handler (D-06); bool/DTO coercion global | unit | `test ... --filter ~BlockedVideo\|~ContentSource` + grep DateTimeOffsetTypeHandler | BlockedVideoStoreTests + ContentSourceStoreTests (+ContentSourceStoreSetEnabledTests) | ✅ green |
| 49-02 T2 | 49-02 | 2 | REQ-4 | T-49-04 | decimal + DTO coercion global; SQL verbatim | unit | `test ... --filter ~LlmSpendLedgerTests\|~WhisperSpendLedgerTests` | LlmSpendLedgerTests + WhisperSpendLedgerTests (SpendLedgerBase exercised via subclasses — no own class) | ✅ green |
| 49-02 T3 | 49-02 | 2 | REQ-4 | T-49-04, T-49-06 | UPSERT arithmetic verbatim; DDL raw | unit | `test ... --filter ~AdminBruteForceTrackerStoreTests` + wave-level full Web.Tests | AdminBruteForceTrackerStoreTests (only dedicated class); FeatureFlagStore/HarvestScheduleStore have NO own class → full Web.Tests is the real gate | ✅ green |
| 49-03 T1 | 49-03 | 3 | REQ-4 | T-49-07 | decimal + DTO coercion global; DDL raw | unit | `test ... --filter ~ContentHarvestRun` | ContentHarvestRunStoreTests | ✅ green |
| 49-03 T2 | 49-03 | 3 | REQ-4 | T-49-07, T-49-08 | Guid + nullable timestamp global; constraint-migration raw | unit | `test ... --filter ~HarvestRun` (web) | HarvestRunStoreTests | ✅ green |
| 49-03 T3 | 49-03 | 3 | REQ-4 | T-49-07, T-49-09 | DTO coercion global; constraint DDL raw | unit | `test ... --filter ~ContentVideoStore` | ContentVideoStoreTests + ContentVideoStoreDistillTests | ✅ green |
| 49-03 T4 | 49-03 | 3 | REQ-4 | T-49-07, T-49-09 | bool + DTO coercion global; ALTER/introspection raw | unit | `test ... --filter ~ContentSiteIndexStore` | ContentSiteIndexStoreTests + ...VisibilityTests + ...ApprovalTests | ✅ green |
| 49-04 T1 | 49-04 | 4 | REQ-4 | T-49-10 | DateTime coercion global; CoerceCount retained | unit | `test ... --filter ~CategoryKnowledgeStore` | CategoryKnowledgeStoreTests | ✅ green |
| 49-04 T2 | 49-04 | 4 | REQ-4 | T-49-10, T-49-11 | transaction: on every in-tx call; card-id cache intact | unit | `test ... --filter ~CategoryKnowledgeRepository` | CategoryKnowledgeRepositoryTests (17 facts + parity + dedup) | ✅ green |
| 49-04 T3 | 49-04 | 4 | REQ-5, REQ-6 | T-49-12 | Carve-out comment-only diff; phase-wide parity | unit/grep | full Core.Tests + Web.Tests + eligible-file ExecuteReaderAsync grep == 0 + git diff comment-only | RequestMetricsStore.cs (carve-out); full SQLite suites | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Map note: the REQ-2 round-trip test (`DapperTypeHandlerRoundTripTests.cs`) is the Wave 0 gap — it is created AND run inside 49-01 Task 2 (TDD), satisfying the Nyquist rule for REQ-2's dual-provider parity. Every conversion task maps to its store's confirmed existing test class on the SQLite side (class names verified by grep of DeckFlow.Core.Tests / DeckFlow.Web.Tests). Two stores (FeatureFlagStore, HarvestScheduleStore) have no dedicated test class — the wave-level full Web.Tests run is their real gate (a per-store filter would false-green).*

---

## Wave 0 Requirements

- [x] `DeckFlow.Web.Tests/Integration/DapperTypeHandlerRoundTripTests.cs` — REQ-2 round-trip (DateTime/decimal/bool/Guid, SQLite + PG via `PostgresFactAttribute`); **created and run in 49-01 Task 2** (the prior MISSING reference). DateTimeOffset added by the round-trip's reuse of the 5th handler in the sweep.
- [x] Existing per-store test classes cover REQ-3..6 (FeedbackStore, CategoryKnowledgeRepository, Content/* stores already have SQLite harnesses — names confirmed by grep)
- [x] `PostgresContainerFixture` already present — no new test package

*Existing infrastructure covers all phase requirements; the only new test (the round-trip) is created in Wave 1 Task 2, so no separate scaffolding plan is required.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Postgres-side parity of converted stores | REQ-2, REQ-6 | CI does not spin a PG container; PG tests self-skip unless env-flagged; VSTest unreliable in WSL | Run `PostgresFactAttribute` tests with the PG env flag set (Testcontainers spins the container); confirm round-trip equality + 0 new failures |
| Spike gate verdict | REQ-3 | Human judgement on "zero store-local conversion" criterion (a)(b)(c) | After FeedbackStore conversion, grep for `GetInt64`/`GetBoolean`/`Parse`/`ToString("O")` in FeedbackStore.cs = 0; record PASS/FAIL in `49-GATE-VERDICT.md`; the `49-01b` blocking decision then writes `49-GATE-ABORT.md` and authorizes-or-aborts the sweep |

---

## Validation Audit 2026-06-14

| Metric | Count |
|--------|-------|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |

Post-execution audit (State A). All 14 task rows flipped ⬜→✅: 12 mapped store/round-trip test classes confirmed present on disk; `DapperTypeHandlerRoundTripTests` = 5 SQLite facts (DateTime/decimal/bool/Guid/DateTimeOffset) + raw on-disk write-path assertions; grep gates pass (`VERDICT: PASS`, `GATE: AUTHORIZED`, eligible-store non-DDL reader grep clean bar documented DDL/introspection carve-outs). Full suites green at execution: `DeckFlow.Core.Tests` 346/0, `DeckFlow.Web.Tests` 622/0/11-skip; no code changed since (docs-only commits). PG parity unchanged — remains the documented manual-only gate below.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (round-trip test created+run in 49-01 Task 2)
- [x] No watch-mode flags
- [x] Feedback latency < 60s (build gate)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-06-14 (revision)
