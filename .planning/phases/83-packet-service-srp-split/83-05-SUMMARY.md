---
phase: 83-packet-service-srp-split
plan: 05
subsystem: api
tags: [csharp, srp-refactor, packet-service, meta-gap, scryfall, xunit]

# Dependency graph
requires:
  - phase: 83-01
    provides: "25-test byte-identical regression harness (PacketByteIdentityFixtures + 4 *ByteIdentityTests.cs suites) — this plan's gate"
  - phase: 83-02
    provides: "PacketTextAssembler (AppendKeyValueLine) + DeckEntryReflagHelper"
  - phase: 83-03
    provides: "ScryfallReferenceResolver.ResolveBatchAsync (Cluster A shared batch-chunk-collect-fallback)"
provides:
  - "MetaGapService migrated onto ScryfallReferenceResolver + DeckEntryReflagHelper + PacketTextAssembler.AppendKeyValueLine — second Wave-2 service done"
  - "Regression test locking oracle-name-map resolution through the shared resolver's fallback path"
affects: ["83-06", "83-07"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Second application of the deck-labeled/context-labeled HttpRequestException re-wrap pattern from 83-04: the shared resolver's generic 'cards/collection' message text is caught and re-wrapped with the service's own error copy at the call site, not pushed into the shared collaborator."

key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/MetaGapService.cs
    - DeckFlow.Web.Tests/MetaGapServiceTests.cs

key-decisions:
  - "MetaGap retains NO ScryfallCard list (unlike Comparison) — ResolveOracleNameMapAsync now returns only batchResolution.OracleNameMap directly from ScryfallReferenceResolver.ResolveBatchAsync, with no dedup-by-Name step needed (Comparison needed that step because it builds a Cards list for a ToDictionary(card => card.Name); MetaGap never held a Cards list)."
  - "Re-wrapped the shared resolver's generic HttpRequestException with MetaGap's ORIGINAL message text ('...building the cEDH meta-gap prompt...'), not a deck-labeled variant like Comparison's fix — MetaGap's controller (DeckPacketController.CedhMetaGap) has no per-caller message-text routing (unlike Comparison's 'Deck A'/'Deck B' check); it always calls UpstreamErrorMessageBuilder.BuildScryfallMessage. The resolver's generic message contains the substring 'cards/collection', which UpstreamErrorMessageBuilder.BuildDetailedScryfallMessage's first branch matches, producing the WRONG '...analysis packet...' copy. Re-wrapping with the original wording (which contains neither 'cards/collection' nor 'analysis packet') restores the original fallthrough to BuildSiteSpecificMessage's generic 'Scryfall returned HTTP {code}...' copy."
  - "Removed the now-dead 'using RestSharp;' import — RestRequest/Method.Post were the only RestSharp symbols in the file and both were removed by this migration (confirmed via git show HEAD~1, they were the sole usages). Pre-existing unused 'using Polly;'/'using Polly.Registry;'/'using System.Net;' were already dead before this task and are left untouched (out of this task's scope per the Scope Boundary rule)."
  - "BuildRequestContextText's 'commander' and 'target_ai_platform' lines (the only 2 of 7 fields that call NormalizeSingleLine) now route through PacketTextAssembler.AppendKeyValueLine, passing JsonTextFormatterService.NormalizeSingleLine (MetaGap's existing, already-shared normalizer) as the delegate; workflow_step/time_period/sort_by/min_event_size/max_standing/selected_reference_indexes lines are untouched (no normalizer applies to them)."

requirements-completed: []

# Metrics
duration: ~30min
completed: 2026-07-04
---

# Phase 83 Plan 05: MetaGapService Migration Summary

**MetaGapService migrated onto ScryfallReferenceResolver and DeckEntryReflagHelper (plus PacketTextAssembler.AppendKeyValueLine for its 2 normalized request-context fields), dropping from 956 to 909 LOC with zero change to its meta-gap paste artifact — the 25 byte-identity tests from 83-01 remain green.**

## Performance

- **Duration:** ~30 min
- **Tasks:** 1
- **Files modified:** 2 (1 source + 1 test file)

## Accomplishments

- `ResolveOracleNameMapAsync` now delegates to `ScryfallReferenceResolver.ResolveBatchAsync` (fallback strategy = `SearchFallbackCardAsync`, `normalizeForScryfall: false` — the same choice the original inline loop made), returning `batchResolution.OracleNameMap` directly since MetaGap never retained a `ScryfallCard` list — the collaborator's job ends exactly where MetaGap's original method ended.
- Removed the private batch-chunk-collect-fallback loop, the private `Chunk(IReadOnlyList<string>, int)` (the third, `Skip`/`Take`-based variant per 83-RESEARCH.md Cluster A), and the `ScryfallBatchSize` constant.
- `LoadDeckAsync`'s commander reflag now calls `DeckEntryReflagHelper.ReflagCommanderEntry` (verified byte-identical to MetaGap's own prior copy by 83-02's diff); the private `ReflagCommanderEntry` method is removed.
- `BuildRequestContextText`'s `commander`/`target_ai_platform` lines (the only 2 of 7 fields using `NormalizeSingleLine`) now route through `PacketTextAssembler.AppendKeyValueLine`, passing `JsonTextFormatterService.NormalizeSingleLine` as the delegate; the other 5 fields (`workflow_step`, `time_period`, `sort_by`, `min_event_size`, `max_standing`, `selected_reference_indexes`) are unchanged, matching Comparison's precedent of leaving non-normalized lines as direct `AppendLine` calls.
- Discovered and fixed (Rule 1) the same error-path landmine 83-04 found for Comparison: the shared resolver's generic `HttpRequestException` message contains the substring `"cards/collection"`, which `UpstreamErrorMessageBuilder.BuildDetailedScryfallMessage`'s first branch matches and would surface the wrong `"...analysis packet..."` copy for a cEDH meta-gap failure. Re-wrapped at the call site with MetaGap's original message text (`"...building the cEDH meta-gap prompt..."`, which matches neither `"cards/collection"` nor `"analysis packet"`), restoring the original fallthrough to `UpstreamErrorMessageBuilder.BuildSiteSpecificMessage`'s generic `"Scryfall returned HTTP {code}..."` copy. Confirmed via reading `DeckPacketController.CedhMetaGap`'s catch block — unlike Comparison, it has no deck-label message routing, so this fix is a single generic re-wrap rather than a per-deck-label one.
- Removed the now-dead `using RestSharp;` import (its only two usages, `RestRequest`/`Method.Post`, were both removed by this migration — confirmed via `git show HEAD~1`); the pre-existing unused `Polly`/`Polly.Registry`/`System.Net` imports were already dead before this task and left untouched per the Scope Boundary rule.
- Added `BuildAsync_CollectionMissRecoveredViaSearchFallback_OracleNameMapUnchanged` to `MetaGapServiceTests.cs`, extending the existing `FakeScryfallResolver` with a `MissingFromCollection` set so a name can be deliberately excluded from the `cards/collection` response, forcing the fallback delegate to run — locking that the oracle-name map resolves the same way through the shared collaborator's fallback path as it did through the pre-migration inline loop.
- `BuildCanonicalDecklistText`, `BuildCompactDecklist`/`BuildCompactRefDecklist`, `BuildComboReferenceText`, and `BuildCanonicalDeckSourceText` (Clusters C/D/F) are untouched, per 83-RESEARCH.md's do-not-unify guidance and the plan's `<do_not_unify>` block.
- Full `DeckFlow.Web.Tests` suite: build 0/0, 1213 passed / 12 PG-skip / 0 failed (up from 83-04's 1212 baseline by exactly the 1 new test); all 25 byte-identity tests from 83-01 remain green, including all 5 `MetaGapByteIdentityTests` cases (3-platform baseline, forced collection-miss fallback fixture, no-explicit-Commander-section reflag fixture); the full 64-test `MetaGapServiceTests` suite passes.

## Task Commits

1. **Task 1: Delegate MetaGap resolution + reflag to shared collaborators** - `5d3aba63` (feat)

## Files Created/Modified

- `DeckFlow.Web/Services/MetaGapService.cs` - Added `_scryfallReferenceResolver` field (instantiated `new ScryfallReferenceResolver(scryfallCardResolver)` in the ctor); `ResolveOracleNameMapAsync` delegates to `ResolveBatchAsync` with a re-wrapped exception preserving the original message; commander reflag delegates to `DeckEntryReflagHelper.ReflagCommanderEntry`; `BuildRequestContextText`'s two normalized fields route through `PacketTextAssembler.AppendKeyValueLine`. Removed private `Chunk`, private `ReflagCommanderEntry`, `ScryfallBatchSize` const, and the now-dead `using RestSharp;`. Net: 956 -> 909 LOC.
- `DeckFlow.Web.Tests/MetaGapServiceTests.cs` - Extended `FakeScryfallResolver` with a `MissingFromCollection` set (forces a collection-miss for a specific name); added `BuildAsync_CollectionMissRecoveredViaSearchFallback_OracleNameMapUnchanged`.

## Decisions Made

See `key-decisions` in frontmatter above (no Cards-list dedup needed since MetaGap never held one; generic — not deck-labeled — exception re-wrap since MetaGap's controller has no per-caller message routing; dead RestSharp import removal scoped to this task's own change; which 2 of 7 request-context fields route through `AppendKeyValueLine`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Re-wrapped the shared resolver's generic HttpRequestException to preserve MetaGap's original error copy**
- **Found during:** Task 1, tracing what a Scryfall 5xx response during MetaGap's oracle-name resolution would surface to the user post-migration (the same side-effects check 83-04 performed for Comparison, per this plan's `<critical_notes>` instruction to watch for it).
- **Issue:** `ScryfallReferenceResolver.ResolveBatchAsync`'s `HttpRequestException` message ("Scryfall card reference lookup (cards/collection) returned HTTP {code}.") contains the substring "cards/collection". `UpstreamErrorMessageBuilder.BuildDetailedScryfallMessage`'s first branch matches on "cards/collection" OR "analysis packet" and returns "...building the analysis packet..." — wrong copy for a cEDH meta-gap failure. `DeckPacketController.CedhMetaGap`'s catch block always calls `UpstreamErrorMessageBuilder.BuildScryfallMessage(exception)` (no message-text special-case like Comparison's "Deck A"/"Deck B" check), so this landmine would have fired unconditionally for MetaGap's Scryfall-failure path.
- **Fix:** Catch the resolver's `HttpRequestException` in `ResolveOracleNameMapAsync` and re-throw with MetaGap's ORIGINAL message text ("Scryfall card reference lookup failed while building the cEDH meta-gap prompt with HTTP {code}.") and the same `StatusCode`. This original text matches neither "cards/collection" nor "analysis packet" nor "set catalog"/"set card lookup", so `BuildDetailedScryfallMessage` returns null and the message correctly falls through to `BuildSiteSpecificMessage`'s generic "Scryfall returned HTTP {code}. Try again shortly." — identical to pre-migration behavior.
- **Files modified:** `DeckFlow.Web/Services/MetaGapService.cs`
- **Verification:** No existing test asserted the exact HTTP-error message text (confirmed via grep, same finding as 83-04) — verified by manual trace of `UpstreamErrorMessageBuilder.BuildDetailedScryfallMessage`/`BuildSiteSpecificMessage` and `DeckPacketController.CedhMetaGap`'s catch block. Full suite green post-fix.
- **Committed in:** `5d3aba63` (Task 1 commit)

**2. [Rule 1 - Bug] Removed the now-dead `using RestSharp;` import**
- **Found during:** Task 1, after removing the private batch-chunk loop that was RestSharp's only consumer in this file.
- **Issue:** `RestRequest`/`Method.Post` (the only two RestSharp symbols referenced anywhere in `MetaGapService.cs`, confirmed via `git show HEAD~1`) were both removed as part of delegating to `ScryfallReferenceResolver`, leaving `using RestSharp;` unreferenced.
- **Fix:** Removed the import. Left the separately pre-existing unused `using Polly;`/`using Polly.Registry;`/`using System.Net;` untouched — these were already dead before this task's changes and are out of scope per the Scope Boundary rule (build was already 0 warnings before and after, since this project does not enable IDE0005 as a compiler warning).
- **Files modified:** `DeckFlow.Web/Services/MetaGapService.cs`
- **Verification:** `dotnet.exe build` clean, 0 warnings, before and after.
- **Committed in:** `5d3aba63` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (1 behavior-preservation fix for an error-path message not exercised by the byte-identity harness, 1 dead-import cleanup directly caused by this task's own edit)
**Impact on plan:** Necessary to keep the migration truly behavior-neutral beyond just the byte-identical happy-path artifacts the plan's stated gate covers. No test scope was weakened; the error-path fix is invisible to all passing tests today because none of them exercise the Scryfall-5xx error path with an assertion on message text — flagged here for the verifier's awareness, matching 83-04's precedent.

## Issues Encountered

None beyond the two items documented above as deviations.

## Known Stubs

None. All resolved names flow through the real `ScryfallReferenceResolver`/`ScryfallCardResolver` test seams (deterministic override delegates, no live HTTP) — no hardcoded empty/placeholder values.

## Threat Flags

None. This plan re-routes internal call graphs only (T-83-08/T-83-09's disposition from the plan's threat register); no new network endpoints, auth paths, file access patterns, or schema changes were introduced. The error-path fix *reduces* risk of a wrong user-facing error message rather than introducing new surface.

## Next Phase Readiness

- `MetaGapService` is now an orchestration shell over `ScryfallReferenceResolver`, `DeckEntryReflagHelper`, and (for 2 fields) `PacketTextAssembler.AppendKeyValueLine` — PKTSVC-01/02/03 are satisfied FOR THIS SERVICE, but remain "Pending" in `REQUIREMENTS.md` (phase-wide wording: "each of the four packet services" / "all four packet services") since `DeckAnalysisPacketService` (83-06, the largest and most flag-interaction-heavy) and `DeckPrimerPacketService` (83-07, resolver-free by design) have not yet been migrated. Per this plan's own instruction, requirement checkboxes were left unmarked — the verifier or the final migrating plan should flip PKTSVC-01/02/03 to Complete once Analysis and Primer are also done.
- Re-run `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~ByteIdentity"` after each future migration step (83-06 onward) and treat any failure as a regression — confirmed still 25/25 green after this plan.
- No blockers. No operator action required.

---
*Phase: 83-packet-service-srp-split*
*Plan: 05*
*Completed: 2026-07-04*

## Self-Check: PASSED

- FOUND: DeckFlow.Web/Services/MetaGapService.cs (modified, 909 LOC)
- FOUND: DeckFlow.Web.Tests/MetaGapServiceTests.cs (modified)
- FOUND commit: 5d3aba63
