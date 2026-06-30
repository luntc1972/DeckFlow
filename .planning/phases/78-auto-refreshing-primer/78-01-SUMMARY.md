---
phase: 78-auto-refreshing-primer
plan: 01
subsystem: api
tags: [primer, staleness, feature-flag, hashing, diff, sha256]

# Dependency graph
requires:
  - phase: 77-multi-axis-score
    provides: "Feature-flag seed/catalog conventions (analysis.multi-axis-score) reused for the new tool.primer.stale-flag row"
provides:
  - "DeckPrimerPacketService.StaleFlag const (tool.primer.stale-flag), seeded OFF in both Postgres and SQLite"
  - "internal-static BuildCanonicalDeckSourceText reused verbatim as the deck-only multiset hash (no second hash path)"
  - "Pure, network-free EvaluateStaleness(generatedPrimerHash, current, saved) -> PrimerStaleness(IsStale, ChangedCardCount, CurrentDeckHash)"
  - "Network-free TryParseDeckTextLocal that parses pasted exports locally and rejects URLs / blanks / unrecognized text"
  - "DeckMultisetHash exposed on DeckPrimerPacketResult, populated by BuildAsync over all loaded entries"
affects: [78-02-controller-wiring, 78-03-view-banner, deck-primer]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Staleness hash = PacketSessionCache.ComputeKey(BuildCanonicalDeckSourceText(entries)) — the SAME canonicalization the primer cache key hashes, so fresh/stale never disagrees with cache behavior"
    - "Changed-card count = DiffEngine(MatchMode.Loose).Compare(saved, current): ToAdd+CountMismatch+OnlyInArchidekt, PrintingConflicts EXCLUDED, clamped >= 0"
    - "Pure/synchronous staleness primitives perform no I/O; URL imports are explicitly rejected to prove no-fetch-on-resume (PRIMER-03)"

key-files:
  created:
    - DeckFlow.Web.Tests/DeckPrimerStalenessTests.cs
  modified:
    - DeckFlow.Web/Services/DeckPrimerPacketService.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
    - DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs

key-decisions:
  - "StaleFlag declared public const (per plan) vs the internal const used by DeckAnalysisPacketService flags"
  - "Reused the existing private BuildCanonicalDeckSourceText by widening it to internal static — no divergent hash path (Pitfall 1)"
  - "Printing-only swaps preserve the multiset hash (SetCode/CollectorNumber excluded) so they resolve to fresh and contribute 0 to the changed-card count"

patterns-established:
  - "Staleness equivalence relation locked by golden tests in both directions (reorder/printing=fresh; add/remove/qty=stale)"
  - "New IDeckPrimerPacketService members ship with default interface implementations so existing test fakes/stubs keep compiling"

requirements-completed: [PRIMER-02, PRIMER-04]

# Metrics
duration: 12min
completed: 2026-06-29
---

# Phase 78 Plan 01: Auto-Refreshing Primer (Staleness Primitives) Summary

**Deck-only SHA-256 multiset-hash staleness primitives — pure `EvaluateStaleness` (changed-card count via loose diff, printing swaps excluded) + network-free `TryParseDeckTextLocal` + `tool.primer.stale-flag` seeded OFF — locked by golden tests in both directions.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-06-29T15:06:20-06:00
- **Completed:** 2026-06-29T15:19:18-06:00
- **Tasks:** 2 (TDD: RED → GREEN each)
- **Files modified:** 5 (1 created, 4 modified)

## Accomplishments
- `tool.primer.stale-flag` seeded FALSE/0 in both Postgres and SQLite seed blocks (with the prior `analysis.multi-axis-score` line's trailing comma added so the SQL stays valid), plus a catalog operator description and a seed-test `[InlineData]`.
- `DeckPrimerPacketService.StaleFlag` const added; `BuildCanonicalDeckSourceText` widened to `internal static` and reused verbatim as the deck-only multiset hash via `PacketSessionCache.ComputeKey`.
- Pure, synchronous, I/O-free `EvaluateStaleness` returning `PrimerStaleness(IsStale, ChangedCardCount, CurrentDeckHash)`; changed-card count from `DiffEngine` Loose (`ToAdd + CountMismatch + OnlyInArchidekt`, `PrintingConflicts` excluded), clamped `>= 0`, suppressed (null) when no saved snapshot.
- Network-free `TryParseDeckTextLocal` that parses pasted Moxfield/Archidekt export text via the local `ParseText` path and returns null for blank / absolute Moxfield-or-Archidekt URL / unrecognized input (never the URL-importing loader → proves PRIMER-03 no-fetch).
- `DeckMultisetHash` exposed on `DeckPrimerPacketResult`, populated by `BuildAsync` over all loaded entries for later re-arm + zip persistence.
- Golden tests cover both directions of the equivalence relation, the changed-card count + count-suppressed fallback + clamp, the printing-only-swap=fresh case, and the local-parse blank/URL/valid/unrecognized/override cases.

## Task Commits

1. **Task 1 RED: assert flag seeds OFF** - `a2fdb31a` (test)
2. **Task 1 GREEN: seed flag + catalog + StaleFlag const** - `cdaab214` (feat)
3. **Task 2 RED: golden staleness tests** - `b64b499c` (test)
4. **Task 2 GREEN: staleness primitives + EvaluateStaleness + local parse** - `ee22a8d5` (feat)

_Note: TDD ordering honored — RED test committed before each GREEN implementation._

## Files Created/Modified
- `DeckFlow.Web/Services/DeckPrimerPacketService.cs` - StaleFlag const; `DeckMultisetHash` on result; internal-static `BuildCanonicalDeckSourceText`; pure `EvaluateStaleness` + `PrimerStaleness` record; network-free `TryParseDeckTextLocal`; parser injection + `parseDeckTextLocalOverride` test seam.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` - Seed `tool.primer.stale-flag` FALSE/0 in both dialect blocks (added trailing comma to the prior line).
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` - Operator description for the new flag.
- `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` - `[InlineData("tool.primer.stale-flag", false)]`.
- `DeckFlow.Web.Tests/DeckPrimerStalenessTests.cs` - Golden tests for the hash equivalence relation, the changed-card count, and the network-free local parse.

## Decisions Made
- Followed the plan's locked design exactly: deck-only hash via `BuildCanonicalDeckSourceText` + `ComputeKey` (NOT the full `TryComputeCacheKeyAsync`), `DiffEngine` Loose count excluding `PrintingConflicts`, URL rejection in the local parser.
- Parser injection landed via a backward-compatible constructor overload (7-arg ctor delegating to the full ctor that default-constructs the parsers) so the existing DI registration in `PacketServiceCollectionExtensions.cs` needed no edit — honoring the plan's "NO Program.cs edit needed" intent.
- New interface members were given default interface implementations so existing controller-test fakes (`StubDeckPrimerPacketService`) compile unchanged.

## Deviations from Plan

None functional — all locked design points implemented as written. One integration note: the plan's interfaces section said the DI ctor params would be "auto-resolved; NO Program.cs edit needed." In reality the primer service is registered via an explicit factory lambda in `DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs` (not auto-DI). This was resolved without changing the registration by adding a backward-compatible 7-arg constructor overload that delegates to the full parser-injecting ctor, plus default interface implementations on the two new members — so neither the registration nor the existing test stubs required edits.

## Issues Encountered
- **Shared-worktree collision:** another active session committed the Task 2 implementation (`eef4e5e4`) and an intervening `chore(78)` commit into this same worktree while this plan executed. The committed result matches the plan and the RED tests; the final HEAD builds clean and passes. No work was lost.

## Verification
- `dotnet build DeckFlow.sln -c Debug` (Windows SDK over WSL): **Build succeeded, 0 Warning(s), 0 Error(s).**
- `DeckPrimerStalenessTests` (golden): **12 passed, 0 failed.**
- Primer + feature-flag regression filter (`DeckPrimer|FeatureFlag`): **85 passed, 0 failed** (includes `FeatureFlagStoreSeedTests` asserting the flag seeds OFF and `FeatureFlagCatalogTests`).
- Full `DeckFlow.Web.Tests` run reported 972 passed / 12 skipped, then an environmental **test-host crash** during the `DapperTypeHandlerRoundTripTests` (Postgres) integration block; those tests pass 5/5 (5 skipped) when run in isolation, confirming the crash is the known WSL VSTest instability and is unrelated to this plan.

## Next Phase Readiness
- Single source of truth for "did the deck change since the primer was generated" is in place and tested. 78-02 can wire the controller (re-arm the hidden field from `DeckMultisetHash`, call `EvaluateStaleness` on resume-without-rebuild, persist the hash into the download zip) and 78-03 can render the banner gated on `StaleFlag`.
- Flag is seeded OFF in both dialects; an operator toggle in prod is required to surface the banner once the UI lands.

## Self-Check: PASSED

- All 5 plan files present on disk (4 modified + 1 created) + SUMMARY written.
- Task commits present on `plan/cycle-13-deck-eval`: `a2fdb31a` (test), `cdaab214` (feat), `b64b499c` (test), `ee22a8d5` (feat — amended from `eef4e5e4` by the concurrent session; identical content).
- `tool.primer.stale-flag` present twice in `FeatureFlagStore.cs` (both dialect seed blocks) and once in `FeatureFlagCatalog.cs`.

---
*Phase: 78-auto-refreshing-primer*
*Completed: 2026-06-29*
