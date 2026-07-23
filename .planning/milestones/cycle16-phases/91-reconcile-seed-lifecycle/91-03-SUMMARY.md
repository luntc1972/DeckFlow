---
phase: 91-reconcile-seed-lifecycle
plan: 03
subsystem: content-kb
tags: [content-kb, seed-managed, backfill, sqlite, postgres, dual-host]

# Dependency graph
requires:
  - phase: 91-01
    provides: seed_managed column, SetSeedManagedIfNullAsync null-only setter, SeedIndexFileReader (3-outcome SeedIndexReadResult)
provides:
  - "SeedManagedBackfill (DeckFlow.Core/Content): host-agnostic D-02 backfill, availability-gated"
  - "ISeedKeyMembershipSource seam + WebSeedKeyMembershipSource + StudioSeedKeyMembershipSource"
  - "Dual-host startup wiring (web prod-pointed, Studio local) classifying legacy seed_managed IS NULL rows"
affects: [91-04-reconcile-classifier, 91-06-reconcile-orchestrator, 91-08-apply-gated-removal]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Skip-entire-run gate on SeedIndexReadResult.SeedAvailable==false before any store read/write - never classify against a missing/unreadable/unparsable seed (mirrors the 91-01 3-outcome contract into the backfill's control flow)"
    - "Host resolves seed-file path (Web: existing ContentKbArtifactPathResolver.SeedFilePath; Studio: git rev-parse repo root + content-kb/seed/index-seed.json), backfill class stays host-agnostic per the ContentBodyHashBackfill dual-host template"

key-files:
  created:
    - DeckFlow.Core/Content/SeedManagedBackfill.cs
    - DeckFlow.Core.Tests/Content/SeedManagedBackfillTests.cs
    - DeckFlow.Web/Services/Content/WebSeedKeyMembershipSource.cs
    - DeckFlow.Studio/Services/StudioSeedKeyMembershipSource.cs
  modified:
    - DeckFlow.Web/Program.cs
    - DeckFlow.Studio/Program.cs

key-decisions:
  - "SeedManagedBackfill.RunAsync short-circuits BEFORE calling GetAllRowsAsync when SeedAvailable==false - zero store reads/writes on an unavailable seed, not just zero writes after a full scan, keeping the skip path cheap and unambiguous in logs"
  - "A throwing ISeedKeyMembershipSource is caught in RunAsync and treated identically to an unavailable seed (SeedAvailable=false) - the safety gate covers both 'seed file says unavailable' and 'membership source itself blew up' the same way"
  - "StudioSeedKeyMembershipSource resolves repoRoot via IGitRepository.ResolveRepoRootAsync(...).GetAwaiter().GetResult() inside a synchronous GetSeedMembership() - ASP.NET Core carries no SynchronizationContext so this one-time startup block cannot deadlock; any resolution failure (not a git checkout, git missing) is caught and treated as an unavailable seed, never propagated"
  - "Web wiring places SeedManagedBackfill.RunAsync() immediately after the existing ContentBodyHashBackfill startup step (not interleaved with or reordering it) - satisfies both 'after seed load' and 'do not reorder ContentBodyHashBackfill'"

patterns-established:
  - "InMemorySeedManagedStore (Core.Tests): a minimal IContentSiteIndexStore fake implementing ONLY the two members the backfill calls (GetAllRowsAsync, SetSeedManagedIfNullAsync), every other member throws NotSupportedException so an accidental call to an unrelated store path fails loudly in a test rather than silently no-opping"

requirements-completed: [SYNC-17]

# Metrics
duration: ~40min
completed: 2026-07-09
---

# Phase 91 Plan 03: SeedManagedBackfill (D-02 Legacy Backfill) Summary

**Host-agnostic `SeedManagedBackfill` classifies every legacy `seed_managed IS NULL` row against CURRENT `index-seed.json` membership — but ONLY when the seed was genuinely present-and-parsed this run — wired at both web (prod-pointed) and Studio (local) startup.**

## Performance

- **Duration:** ~40 min
- **Started:** 2026-07-09T22:04:27Z (STATE.md handoff from 91-02)
- **Completed:** 2026-07-09T22:18:15Z
- **Tasks:** 2
- **Files modified:** 6 (2 modified, 4 created)

## Accomplishments
- `SeedManagedBackfill` (DeckFlow.Core/Content) — a sealed class mirroring `ContentBodyHashBackfill`'s host-agnostic, never-crash-startup shape. It gates on `ISeedKeyMembershipSource.GetSeedMembership().SeedAvailable`: when `false`, it performs ZERO store reads/writes and logs a warning — the row set stays entirely `NULL` so a later correct seed can still classify them. Only when the seed is genuinely present-and-parsed does it enumerate `GetAllRowsAsync`, derive each unclassified row's natural key via `ContentNaturalKey.TryDerive`, and classify true/false via `SetSeedManagedIfNullAsync` (a null-only write, so already-classified rows and re-runs are no-ops).
- `ISeedKeyMembershipSource` seam (defined alongside the backfill) — one synchronous method, `SeedIndexReadResult GetSeedMembership()`, that each host implements to resolve and read ITS OWN seed file via the shared `SeedIndexFileReader.Read`.
- `WebSeedKeyMembershipSource` — resolves the DEPLOYED `index-seed.json` via the existing `ContentKbArtifactPathResolver.SeedFilePath` (the exact path `ContentKbSeedLoader` already uses), so the backfill classifies against the same seed the web app just loaded.
- `StudioSeedKeyMembershipSource` — resolves the operator's git-checkout `{repoRoot}/content-kb/seed/index-seed.json` via `IGitRepository.ResolveRepoRootAsync` (the same repo-root resolution `GitBodyCoverageAudit` uses), blocking synchronously on the one async git call inside the otherwise-synchronous seam (safe — ASP.NET Core carries no `SynchronizationContext`). Any resolution failure (not a git checkout, git missing) is caught and treated as an unavailable seed.
- Dual-host wiring: web invokes `SeedManagedBackfill.RunAsync()` immediately after the existing `ContentBodyHashBackfill` startup step (itself already after seed load); Studio invokes it against the SAME local `content-kb.db` store right after its own local body-hash backfill.

## Task Commits

1. **Task 1: SeedManagedBackfill (host-agnostic, availability-gated, idempotent, startup-safe)** - `3522e039` (feat)
2. **Task 2: Dual-host wiring (web prod-pointed + Studio local)** - `fe3781d0` (feat)

_Plan metadata commit and STATE/ROADMAP updates follow this SUMMARY._

## Files Created/Modified
- `DeckFlow.Core/Content/SeedManagedBackfill.cs` - `ISeedKeyMembershipSource` interface + `SeedManagedBackfill` class (availability-gated classify pass)
- `DeckFlow.Core.Tests/Content/SeedManagedBackfillTests.cs` - `InMemorySeedManagedStore` + `FakeSeedKeyMembershipSource` test doubles; 7 behavior tests
- `DeckFlow.Web/Services/Content/WebSeedKeyMembershipSource.cs` - Deployed-seed membership source
- `DeckFlow.Studio/Services/StudioSeedKeyMembershipSource.cs` - Git-checkout membership source
- `DeckFlow.Web/Program.cs` - DI registration + startup invocation (after seed load, after body-hash backfill)
- `DeckFlow.Studio/Program.cs` - DI registration + startup invocation (after local body-hash backfill)

## Decisions Made
- `RunAsync` returns BEFORE calling `GetAllRowsAsync` when the seed is unavailable — zero store reads/writes, not merely zero writes after a full scan. Keeps the skip path cheap and makes the "zero rows touched" guarantee structurally obvious rather than incidental.
- A throwing `ISeedKeyMembershipSource.GetSeedMembership()` call is caught inside `RunAsync` and folded into the SAME unavailable-seed code path (`SeedIndexReadResult(false, EmptyKeys)`) — one gate, not two divergent safety mechanisms.
- `StudioSeedKeyMembershipSource` uses sync-over-async (`.GetAwaiter().GetResult()`) for the one Studio-side async step (git repo-root resolution) rather than widening `ISeedKeyMembershipSource` to an async interface — keeps the seam identical across both hosts (Web's file-path resolution is inherently synchronous) and is safe because ASP.NET Core hosts carry no captured `SynchronizationContext`.
- Web wiring inserts the new backfill call immediately AFTER the existing `ContentBodyHashBackfill` block rather than between seed-load and body-hash-backfill — satisfies "after seed load" (transitively, since body-hash-backfill already runs after seed-load) while leaving `ContentBodyHashBackfill`'s own code block completely untouched (plan's "do not reorder ContentBodyHashBackfill" constraint).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed literal NUL bytes introduced by JSON-escape decoding of `\u0000` during authoring**
- **Found during:** Task 1, immediately after writing `SeedManagedBackfill.cs` and `SeedManagedBackfillTests.cs`
- **Issue:** Typing the C# escape-sequence *text* `\u0000` (the required natural-key separator, matching `SeedIndexFileReader`/`ContentNaturalKey`) inside a tool-call string parameter gets JSON-decoded into an actual NUL byte (`0x00`) in the written file before it ever reaches disk as source text — the same "subagent NUL byte" hazard documented in project memory from 91-01. This affected one call site in `SeedManagedBackfill.cs` and (unexpectedly) four call sites in the test file where even a single literal space character in that exact syntactic position was similarly converted to a NUL byte.
- **Fix:** Located every literal `0x00` byte via a Python byte-scan and replaced it with the literal 6-character escape-sequence text `\u0000`, so the C# compiler (not the file bytes) produces the runtime NUL character. Verified zero remaining NUL bytes and pure-LF line endings in both files before building or committing.
- **Files modified:** `DeckFlow.Core/Content/SeedManagedBackfill.cs`, `DeckFlow.Core.Tests/Content/SeedManagedBackfillTests.cs`
- **Verification:** `dotnet build` clean (0 warnings) for both files; targeted test run 7/7 green; full `DeckFlow.Core.Tests` suite 1177/1177 green.
- **Committed in:** `3522e039` (Task 1 commit — the bytes never reached a prior commit)

---

**Total deviations:** 1 auto-fixed (1 bug, authoring-tool artifact, caught before commit)
**Impact on plan:** No scope creep — pure correctness fix to the exact code the plan specified, caught by build/test verification before any commit.

## Issues Encountered
None beyond the authoring-tool NUL-byte artifact documented above. No `TypeScript.Tasks.dll` lock contention was hit this run (no dev server was running).

## User Setup Required
None — no external service configuration required.

## Next Phase Readiness
- `SeedManagedBackfill`, `ISeedKeyMembershipSource`, and both host-specific membership sources are wired and running at startup on both hosts. Every `seed_managed IS NULL` row now gets classified on next boot when the seed is available; the ~70 prod-only rows become provably distinguishable from seed-owned rows once the web app restarts against a reachable seed.
- The safety gate (never classify against a missing/unreadable seed) is unit-tested at the `SeedManagedBackfill` level; live end-to-end confirmation (an actual prod boot with the deployed seed present) is a deploy-time verification, not something this plan can prove locally.
- Ready for 91-04 (reconcile classifier) and 91-06 (reconcile orchestrator), which can now rely on every row being either `true`, `false`, or (only if the seed was never available on any boot) `null` — never a false classification.
- No blockers.

---
*Phase: 91-reconcile-seed-lifecycle*
*Completed: 2026-07-09*

## Self-Check: PASSED

All 6 created/modified files verified present on disk; both task commit hashes (`3522e039`, `fe3781d0`) verified present in git log.
