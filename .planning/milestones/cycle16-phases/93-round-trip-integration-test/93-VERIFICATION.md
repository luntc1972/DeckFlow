---
phase: 93-round-trip-integration-test
verified: 2026-07-11T05:55:00Z
resolved: 2026-07-11T06:05:00Z
status: passed
score: 7/7 must-haves verified
overrides_applied: 0
resolution: "BLOCKER fixed in commit 60672e84 — RoundTripHarness.Dispose() now clears read-only attributes (ForceDeleteDirectory) before recursive delete. Re-run live under Docker with DECKFLOW_POSTGRES_TESTS=1 via cmd.exe: full DeckFlow.Web.Tests suite reports 91 passed / 0 failed / 0 skipped, including both RoundTripSmokeTests.Harness_Boots_Distill_Git_And_Postgres_Schema and RoundTripSyncLoopTests.RoundTrip_DistillToReconcile_HashMatchesEveryHop_NoRevertAfterReseed. Also fixed in 33677460: smoke-test artifactRoot now carries the content-kb segment (matches production ContentArtifactWriter path semantics)."
gaps_resolved:
  - truth: "The SYNC-16 [PostgresFact] runs green under Docker (the automated proof the phase exists to deliver)"
    status: resolved
    reason: "Live-executed both RoundTripSmokeTests.Harness_Boots_Distill_Git_And_Postgres_Schema and RoundTripSyncLoopTests.RoundTrip_DistillToReconcile_HashMatchesEveryHop_NoRevertAfterReseed against a real Docker Desktop + Testcontainers Postgres (Docker was off in the executor's environment during 93-01/93-02 execution, so this was never previously exercised live). Every in-body assertion passed — all SC1/SC2/SC3 checkpoints, Pull field-authority, and Reconcile idempotency logged success — but BOTH tests report FAILED because RoundTripHarness.Dispose() throws System.UnauthorizedAccessException while recursively deleting the temp git repo tree on Windows. Git marks loose object files read-only; Directory.Delete(path, recursive:true) does not clear read-only attributes before deleting on Windows, so cleanup throws and xUnit records the whole test as failed. Reproduced twice, 100% deterministic."
    artifacts:
      - path: "DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripHarness.cs"
        issue: "Dispose() (line ~202-208) calls Directory.Delete(directory, recursive: true) on RepoRoot/AppRoot/OriginRoot without clearing read-only attributes first; git's .git/objects/** loose-object files are read-only by design, so this throws UnauthorizedAccessException on Windows every run, failing the test at teardown even though the test body's assertions all pass"
    missing:
      - "A recursive delete helper in RoundTripHarness.Dispose() that clears FileAttributes.ReadOnly on every file (and unsets the read-only dir attribute) before calling Directory.Delete, for RepoRoot/AppRoot/OriginRoot"
human_verification: []
---

# Phase 93: Round-Trip Integration Test Verification Report

**Phase Goal:** The entire sync loop — distill through reconcile — is locked by one automated end-to-end test so future changes can't silently reintroduce any of the fixed classes of drift.
**Verified:** 2026-07-11T05:55:00Z
**Status:** gaps_found
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | RoundTripSyncLoopTests.cs wires the REAL coordinators (Publish/DirectPush/PullFromProd/Reconcile) against a real Testcontainers Postgres + real git tree; only LLM/SFTP/deploy-confirm transports are faked | ✓ VERIFIED | `RoundTripSyncLoopTests.cs:101-104` constructs `new PublishCoordinator(...)`, `new DirectPushCoordinator(...)`, `new PullFromProdCoordinator(...)` — all real production types from `DeckFlow.Studio.ViewModels`; `:300-303` constructs real `ContentKbReconcileOrchestrator`/`ReconcileCoordinator`. Only `CannedLlmDistillationService`, `RecordingSshArtifactUploader`, `AppTreeDeployedBodyConfirmer` (RoundTripSeams.cs) are doubles — confirmed by reading their implementations, each wraps or canned-returns, never fakes the coordinator/store logic itself. Live-executed: `docker version` confirmed Docker Desktop 4.74.0 running; `dotnet.exe test ... --filter RoundTrip` produced `[testcontainers.org]` log lines showing a real Postgres container (`postgres:16-alpine`) started and torn down. |
| 2 | SC2 hash chain uses the single `ContentSiteIndexContentSignature.ComputeBodySha256` surface at every hop | ✓ VERIFIED | `grep` for hashing calls in `RoundTripSyncLoopTests.cs` returns only `ComputeBodySha256` (8 call sites: distill-computed `:137`, seed-json compare `:165`, served-recompute `:193/229`, prod-row compare `:195/230/246/252`) — no second hashing scheme (no MD5/GetHashCode) present. Live-executed run's stdout shows every checkpoint line through "Reseed #1 ... hash matches every hop" and "DirectPush: row B confirmed" firing without an assertion failure. |
| 3 | SC3 no-revert-after-reseed is non-vacuous: rows A (published) and B (DirectPush'd) are approved + present in the re-exported seed before the second reseed | ✓ VERIFIED | `:146` explicit `SetApprovalStatusAsync(... "approved")` for A, `:203` same for B; `:232-234` asserts `seedEntriesAfterDirectPush` contains BOTH natural keys before the second `LoadIfPresentAsync` at `:239`. Live run confirms `reseededCount2 == 2` (both rows reseeded) and the no-revert assertions (`:244`, `:250`) passed — visible in the "Reseed #2 (redeploy): neither row A nor row B was reverted" checkpoint log line. |
| 4 | Reseed "reconstructs prod" proof is non-vacuous: prod row A asserted ABSENT before reseed, PRESENT after | ✓ VERIFIED | `:140-141` `Assert.Null(prodRowABeforeReseed)` before Publish/reseed; `:182-183` `Assert.NotNull(prodRowAAfterReseed)` after `LoadIfPresentAsync` — both assertions are in the test body and both passed in the live run (no assertion-failure message appeared; only the Dispose-time exception did, see Gap below). |
| 5 | Zero production-code change across the phase (only DeckFlow.Web.Tests/** + one csproj ProjectReference + .planning/**) | ✓ VERIFIED | `git diff --stat a265eec3^..33677460` (full phase-93 commit range) touches exactly: `.planning/{REQUIREMENTS,ROADMAP,STATE}.md`, 4 phase docs under `.planning/phases/93-*`, `DeckFlow.Web.Tests.csproj` (+4 lines, the one ProjectReference), and the 4 new files under `DeckFlow.Web.Tests/Integration/RoundTrip/`. Zero files outside `.planning/**` and `DeckFlow.Web.Tests/**`. |
| 6 | The [PostgresFact]s auto-skip cleanly in CI/without Docker (D-07 local-gate contract) | ✓ VERIFIED | `PostgresFactAttribute.cs` sets `Skip` when `DECKFLOW_POSTGRES_TESTS != "1"`; `PostgresContainerFixture.GetConnectionStringOrSkipAsync` throws `SkipException` if the flag is unset or container start fails. Confirmed live: a plain `dotnet.exe test --filter RoundTrip` run (no env var set) produced `[SKIP]` for all three PostgresFacts with the expected message, 0 failures. |
| 7 | 93-PREFLIP-CHECKLIST.md covers FU-1/FU-2/FU-3 with decision prompts + live flip steps for both flags + D-07 gate note | ✓ VERIFIED | File read in full: contains a "D-07 gate note" callout, FU-1 section with Option A/B decision checkboxes, FU-2 section with a decision/action checkbox, FU-3 with 5 ordered ACTION checkboxes, plus separate "Live flip steps" sections for `sync.directpush-gitbody` (5 steps) and `sync.reconcile` (3 steps). All required elements present. |
| 8 | **[Load-bearing, roadmap SC1]** The SYNC-16 test actually runs green under Docker — the automated proof the phase exists to deliver | ✗ FAILED | Live-executed twice (deterministic). Both `RoundTripSmokeTests` and `RoundTripSyncLoopTests` fail at `Dispose()` with `UnauthorizedAccessException: Access to the path '039f2d98...' is denied` from `RoundTripHarness.Dispose():206` → `Directory.Delete(directory, recursive: true)` on the git temp-repo tree. Git's loose object files are read-only by default; .NET's `Directory.Delete` does not clear that attribute on Windows before deleting, so cleanup throws and xUnit marks the whole test FAILED — even though every assertion inside the test body (SC1 loop wiring, SC2 hash-at-every-hop, SC3 no-revert, Pull field-authority, Reconcile idempotent) passed, as shown by the full sequence of stage-checkpoint log lines reaching the final "Reconcile: zero unexpected discrepancies" line with no assertion-failure text anywhere in the output. |

**Score:** 7/8 truths verified (6/7 plan-declared must-haves + 1 roadmap-derived truth added per Step 2a; the 8th is the load-bearing "test is green" truth implied by SC1/roadmap Progress table marking Phase 93 "Complete")

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripHarness.cs` | Real PG schema pre-create, real git bootstrap, /app deploy-copy | ✓ VERIFIED (exists, substantive, wired) — but its `Dispose()` is the source of the FAILED gap above | Read in full; every method backed by real production types (`ContentSiteIndexStore`, `GitRepository`-compatible bootstrap). Consumed by both `RoundTripSmokeTests` and `RoundTripSyncLoopTests`. |
| `DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripSeams.cs` | 4 deterministic doubles (LLM, SFTP, deploy-confirm, prod-reader/factory) | ✓ VERIFIED | Read in full; each seam is either a canned-return or wraps the real store instance — no coordinator/store logic faked. |
| `DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripSmokeTests.cs` | Harness boot proof `[PostgresFact]` | ✓ VERIFIED wired, ✗ execution FAILED (Dispose bug) | Live-executed; assertions passed, Dispose threw. |
| `DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripSyncLoopTests.cs` | The SYNC-16 full-loop `[PostgresFact]` | ✓ VERIFIED wired, ✗ execution FAILED (Dispose bug) | Live-executed; every checkpoint assertion passed per stdout, Dispose threw. |
| `.planning/phases/93-round-trip-integration-test/93-PREFLIP-CHECKLIST.md` | Operator pre-flip gate (D-08) | ✓ VERIFIED | Read in full; FU-1/FU-2/FU-3 + both flag flip sequences present. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `RoundTripSyncLoopTests.cs` | `RoundTripHarness.cs` (93-01) | `IClassFixture<PostgresContainerFixture>` + harness construction | WIRED | Confirmed by direct field usage (`_harness`) and live execution reaching every harness method. |
| served-body-recompute hop | distill/seed-json/prod-row hop | `ComputeBodySha256` equality chain | WIRED | Confirmed by grep (single surface) + live-run equality assertions all passing. |
| DirectPush'd + published rows | post-second-reseed state | `ContentKbSeedLoader.LoadIfPresentAsync` reseed + `GetByNaturalKeyAsync` assert-unchanged | WIRED | Confirmed by live-run assertions (`:244`, `:250`, `:246`, `:252`) all passing before the Dispose-time failure. |

### Behavioral Spot-Checks / Probe Execution

No `scripts/*/tests/probe-*.sh` convention exists in this repo (checked — none found), and this phase declares no probes of its own. Standard build check substituted:

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Solution builds clean | `dotnet.exe build DeckFlow.sln` | 0 Warning(s), 0 Error(s) | ✓ PASS |
| PostgresFacts auto-skip without the env flag | `dotnet.exe test DeckFlow.Web.Tests --filter RoundTrip` (no `DECKFLOW_POSTGRES_TESTS`) | 3 `[SKIP]` (RoundTripSmokeTests, RoundTripSyncLoopTests, plus the pre-existing PostgresStorageTests family), 0 failed | ✓ PASS |
| **The SYNC-16 round-trip fact itself, live with Docker** | `DECKFLOW_POSTGRES_TESTS=1 dotnet.exe test DeckFlow.Web.Tests --filter RoundTripSyncLoopTests` | All in-body assertions pass (full checkpoint log reached); test reported **FAILED** at teardown (`UnauthorizedAccessException` in `Dispose()`); reproduced twice, 100% deterministic | ✗ FAIL |
| Harness boot smoke, live with Docker | `DECKFLOW_POSTGRES_TESTS=1 dotnet.exe test DeckFlow.Web.Tests --filter RoundTripSmokeTests` | Same Dispose-time failure pattern; no assertion-failure text in output | ✗ FAIL |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|--------------|--------|----------|
| SYNC-16 | 93-01, 93-02, 93-03 | End-to-end integration test spanning distill→...→reconcile, asserting served==published, body_sha256 end-to-end, no-revert-after-reseed | ⚠ PARTIAL | The test's logic is proven correct (every in-body assertion passes when actually run against real Docker/Postgres), satisfying the *intent* of SYNC-16 as a design/assertion artifact. However, the test does not currently pass as an executable automated gate — REQUIREMENTS.md/ROADMAP.md mark SYNC-16 "Complete" and the 93-02/93-03 SUMMARYs describe it as "ready for an operator to run live with Docker" and "build-clean" without disclosing that a live Docker run (never previously performed, per 93-02's own "Docker is unavailable in this environment" note) fails at teardown. The requirement's own text ("runs against containerized Postgres + a real git tree") is not met by a FAILED test result. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripHarness.cs` | ~202-208 | `Directory.Delete(directory, recursive: true)` on a git-managed tree without clearing read-only attributes first | 🛑 Blocker | Causes the SYNC-16 test (and the 93-01 smoke test) to report FAILED every time it is actually run under Docker on Windows — the exact execution path this project's own CLAUDE.md prescribes (`dotnet.exe` from WSL / Windows host, since VSTest is unreliable in native WSL). No `TODO`/`FIXME` marker present — this is a functional bug, not a flagged debt item. |

No other debt markers (`TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER`) found in the phase's new files.

### Human Verification Required

None. The failure mode was reproducible and fully diagnosable by direct execution — no ambiguity requiring human judgment.

### Gaps Summary

**The test's assertions are correct; the test's execution is not green.** This is a nuanced but important distinction for an adversarial goal-backward verification:

- Positive finding: live-executing the full `RoundTripSyncLoopTests` fact against a real Docker Desktop + Testcontainers Postgres proves that every one of SC1/SC2/SC3, the store-flow non-vacuous proofs, the Pull field-authority assertion, and the Reconcile-idempotent assertion are **logically correct as written** — the stage-checkpoint log reaches its final line with no assertion failure. This had never been exercised live before this verification (93-02's own SUMMARY states Docker was unavailable in its execution environment), so this is new, valuable, and largely reassuring evidence about the underlying sync-loop correctness this phase set out to prove.
- Blocking finding: `RoundTripHarness.Dispose()` throws `UnauthorizedAccessException` while deleting the git temp-repo tree on Windows (git's loose objects are read-only; `Directory.Delete` doesn't clear that attribute first). This makes **both** Postgres-gated tests in this phase report **FAILED**, 100% reproducibely, under the exact `DECKFLOW_POSTGRES_TESTS=1 dotnet.exe test ...` command the phase's own PLAN/verification section and the 93-PREFLIP-CHECKLIST.md prescribe as the pre-flip gate. An operator following the checklist today, on Windows with Docker running, will see this test FAIL and — correctly, going by the letter of the checklist — must NOT flip `sync.directpush-gitbody` / `sync.reconcile` until it is fixed, even though the substance of the proof holds.
- This gap was never caught by prior execution because Docker was unavailable during both 93-01 and 93-02's own dispatches (documented in their SUMMARYs); this verification pass is the first time the fact has actually run against live infrastructure.
- Recommended fix (test-only, does not touch production code, small): in `RoundTripHarness.Dispose()`, recursively clear `FileAttributes.ReadOnly` on every file under `RepoRoot`/`AppRoot`/`OriginRoot` before calling `Directory.Delete(..., recursive: true)` — a standard, well-known workaround for deleting git-managed directory trees on Windows.

**This looks like a straightforward, scoped fix** rather than a design problem — the loop logic itself is sound. Recommend routing back through `/gsd-plan-phase --gaps` (or a small follow-up plan) to patch `RoundTripHarness.Dispose()`, then re-run this same live-Docker verification to confirm a genuinely green result before the operator relies on the 93-PREFLIP-CHECKLIST.md gate.

---

*Verified: 2026-07-11T05:55:00Z*
*Verifier: Claude (gsd-verifier)*
