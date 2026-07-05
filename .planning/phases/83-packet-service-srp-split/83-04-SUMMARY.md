---
phase: 83-packet-service-srp-split
plan: 04
subsystem: api
tags: [csharp, srp-refactor, packet-service, deck-comparison, scryfall, xunit]

# Dependency graph
requires:
  - phase: 83-01
    provides: "25-test byte-identical regression harness (PacketByteIdentityFixtures + 4 *ByteIdentityTests.cs suites) — this plan's gate"
  - phase: 83-02
    provides: "PacketTextAssembler (BuildSectionedDecklistText, AppendKeyValueLine) + DeckEntryReflagHelper"
  - phase: 83-03
    provides: "ScryfallReferenceResolver.ResolveBatchAsync (Cluster A shared batch-chunk-collect-fallback)"
provides:
  - "DeckComparisonService migrated onto all three Wave-1 collaborators — orchestration shell, no duplicate resolution/text/reflag logic"
  - "Regression test locking the shared resolver's per-name match-back discrimination within one batch"
affects: ["83-05", "83-06", "83-07"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Deck-labeled HttpRequestException re-wrap: when a shared collaborator's exception message loses per-caller context that a controller's error-routing logic depends on (DeckPacketController checks for 'Deck A'/'Deck B' substrings to bypass UpstreamErrorMessageBuilder), re-wrap at the call site rather than pushing caller-specific text into the shared collaborator."

key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/DeckComparisonService.cs
    - DeckFlow.Web.Tests/DeckComparisonServiceTests.cs

key-decisions:
  - "Resolved-cards list is de-duplicated by Name (HashSet<string> OrdinalIgnoreCase) when building CardLookupResult.Cards from the resolver's per-request-name Resolutions list, since the resolver can return multiple resolutions whose underlying Card.Name coincides — matching the original code's fallback-merge dedup (needed because BuildDeckSummary's `cards.ToDictionary(card => card.Name)` throws on duplicate keys)."
  - "The resolver's generic HttpRequestException ('Scryfall card reference lookup (cards/collection) returned HTTP {code}.') is caught in LookupCardDetailsAsync and re-thrown with the ORIGINAL deck-labeled message text ('{deckLabel} Scryfall card reference lookup failed while building the comparison packet with HTTP {code}.') — discovered during side-effects analysis that DeckPacketController's HttpRequestException handler special-cases messages containing 'Deck A'/'Deck B' to bypass UpstreamErrorMessageBuilder entirely; without the re-wrap, the resolver's generic message (which contains the substring 'cards/collection') would instead match UpstreamErrorMessageBuilder.BuildDetailedScryfallMessage's first branch and surface the WRONG user-facing copy ('...building the analysis packet...' for a Comparison failure)."
  - "BuildRequestContextText's workflow_step line is left as a direct AppendLine (no NormalizeSingleLine call in the original — it's an int, not routed through AppendKeyValueLine); only the four NormalizeSingleLine-bearing lines (deck_a_name/deck_b_name/deck_a_bracket/deck_b_bracket/target_ai_platform) were converted to PacketTextAssembler.AppendKeyValueLine, per the plan's 'keeping Comparison's own field list/order' instruction."

requirements-completed: []

# Metrics
duration: ~40min
completed: 2026-07-04
---

# Phase 83 Plan 04: DeckComparisonService Migration Summary

**DeckComparisonService migrated onto all three Wave-1 collaborators (ScryfallReferenceResolver / PacketTextAssembler / DeckEntryReflagHelper), dropping from 1033 to 924 LOC with zero change to its comparison/follow-up paste artifacts — the 25 byte-identity tests from 83-01 remain green.**

## Performance

- **Duration:** ~40 min
- **Tasks:** 2
- **Files modified:** 2 (1 source + 1 test file)

## Accomplishments

- `LookupCardDetailsAsync` now delegates to `ScryfallReferenceResolver.ResolveBatchAsync` (fallback strategy = `SearchFallbackCardAsync`, `normalizeForScryfall: false` — the same choice the original inline loop made), replacing the private batch-chunk-collect-fallback loop and the private `Chunk<T>`.
- Resolved cards are de-duplicated by `Name` when translating the resolver's per-request-name `Resolutions` into the `CardLookupResult.Cards` list consumed by `BuildDeckSummary`'s `cardLookup` dictionary — preserving the original fallback-merge dedup that guards against a duplicate-key crash in `ToDictionary`.
- Discovered and fixed (Rule 1) a real behavior-preservation gap: the shared resolver's generic `HttpRequestException` message would have mis-routed through `UpstreamErrorMessageBuilder` in `DeckPacketController`'s error handler (which special-cases messages containing "Deck A"/"Deck B" to bypass the builder entirely) — re-wrapped the exception at the call site with the original deck-labeled message text so the controller's error-routing behavior is unchanged.
- `BuildDecklistText`/`FormatDecklistLine` replaced by `PacketTextAssembler.BuildSectionedDecklistText`; the private `ReflagCommanderEntry` (byte-identical to MetaGap's copy per 83-02's verified diff) replaced by `DeckEntryReflagHelper.ReflagCommanderEntry`.
- `BuildRequestContextText`'s four `NormalizeSingleLine`-bearing fields now route through `PacketTextAssembler.AppendKeyValueLine`, passing Comparison's own `JsonTextFormatterService.NormalizeSingleLine` as the normalizer delegate — field list, order, and the `workflow_step` line (which has no normalizer) are unchanged.
- Added a new regression test (`BuildAsync_FallbackResolvedCard_AnnotatesOnlyTheRenamedCard`) locking that, within one batch, a collection-hit name is NOT annotated while a SearchFallback-recovered name IS — proving the shared collaborator's per-name match-back still discriminates correctly post-migration.
- Cluster C/F/G methods (`BuildCanonicalDeckSourceText`, `BuildComboArtifactText`, `NormalizeOracleText`) are untouched, per 83-RESEARCH.md's do-not-unify guidance and the plan's `<do_not_unify>` block.
- Full `DeckFlow.Web.Tests` suite: build 0/0, 1212 passed / 12 PG-skip / 0 failed (up from 83-03's 1211 baseline by exactly the 1 new test); all 25 byte-identity tests from 83-01 remain green, including all 5 `DeckComparisonByteIdentityTests` cases (3-platform baseline, printed-name SearchFallback fixture, no-explicit-Commander-section reflag fixture).

## Task Commits

1. **Task 1: Delegate Scryfall resolution to ScryfallReferenceResolver** - `81d09887` (feat)
2. **Task 2: Delegate decklist text + reflag; confirm shell reduction** - `72e9f475` (feat)

## Files Created/Modified

- `DeckFlow.Web/Services/DeckComparisonService.cs` - Added `_scryfallReferenceResolver` field (instantiated `new ScryfallReferenceResolver(scryfallCardResolver)` in the ctor); `LookupCardDetailsAsync` delegates to `ResolveBatchAsync` with a deck-labeled exception re-wrap; decklist text delegates to `PacketTextAssembler.BuildSectionedDecklistText`; commander reflag delegates to `DeckEntryReflagHelper.ReflagCommanderEntry`; `BuildRequestContextText`'s normalized fields route through `PacketTextAssembler.AppendKeyValueLine`. Net: 1033 -> 924 LOC (private `Chunk<T>`, `BuildDecklistText`, `FormatDecklistLine`, `ReflagCommanderEntry`, and `ScryfallBatchSize` const removed).
- `DeckFlow.Web.Tests/DeckComparisonServiceTests.cs` - Added `BuildAsync_FallbackResolvedCard_AnnotatesOnlyTheRenamedCard`, locking the shared resolver's per-name discrimination within a mixed collection-hit/fallback batch.

## Decisions Made

See `key-decisions` in frontmatter above (resolved-cards dedup-by-Name, deck-labeled exception re-wrap to preserve `DeckPacketController`'s error-routing behavior, `workflow_step` line left un-normalized).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Re-wrapped the shared resolver's generic HttpRequestException to preserve DeckPacketController's error-routing behavior**
- **Found during:** Task 1, while tracing what a Scryfall 5xx response during Comparison's card lookup would surface to the user post-migration (side-effects analysis of the exception message's downstream consumers).
- **Issue:** `ScryfallReferenceResolver.ResolveBatchAsync`'s `HttpRequestException` message ("Scryfall card reference lookup (cards/collection) returned HTTP {code}.") has no deck label and contains the literal substring "cards/collection". `DeckPacketController.DeckComparison`'s catch block checks `exception.Message.Contains("Deck A", ...) || exception.Message.Contains("Deck B", ...)` to decide whether to show the raw message or fall through to `UpstreamErrorMessageBuilder.BuildScryfallMessage`. The ORIGINAL Comparison message ("{deckLabel} Scryfall card reference lookup failed...") always contained "Deck A"/"Deck B" and so was always shown as-is, bypassing the builder entirely. Naively adopting the resolver's message unchanged would flip this: the message would no longer match "Deck A"/"Deck B", falling through to `UpstreamErrorMessageBuilder.BuildDetailedScryfallMessage`, whose first branch matches on "cards/collection" (present in the resolver's message) and returns "...while building the **analysis** packet..." — visibly wrong copy for a Comparison failure.
- **Fix:** Catch the resolver's `HttpRequestException` in `LookupCardDetailsAsync` and re-throw with the original deck-labeled message text and the same `StatusCode`, preserving both the exception type and the controller's message-based routing decision.
- **Files modified:** `DeckFlow.Web/Services/DeckComparisonService.cs`
- **Verification:** No existing test asserted the exact HTTP-error message text (confirmed via grep), so this was not caught by an automated regression — verified by manual trace of `UpstreamErrorMessageBuilder.BuildDetailedScryfallMessage` and `DeckPacketController.DeckComparison`'s catch block logic. Full suite green post-fix.
- **Committed in:** `81d09887` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (a behavior-preservation fix for an error-path message that is not exercised by the byte-identity harness, since that harness only covers the happy path)
**Impact on plan:** Necessary to keep the migration truly behavior-neutral beyond just the byte-identical happy-path artifacts the plan's stated gate covers. No test scope was weakened; the fix is invisible to all passing tests today because none of them exercise the Scryfall-5xx error path with an assertion on message text — flagged here for the verifier's awareness since it's an error-path behavior preservation, not something the byte-identity gate itself would have caught.

## Issues Encountered

None beyond the one item documented above as a deviation.

## Known Stubs

None. All resolved cards flow through the real `ScryfallReferenceResolver`/`ScryfallCardResolver` test seams (deterministic override delegates, no live HTTP) — no hardcoded empty/placeholder values.

## Threat Flags

None. This plan re-routes internal call graphs only (T-83-07's disposition from the plan's threat register); no new network endpoints, auth paths, file access patterns, or schema changes were introduced. The one behavioral fix (exception re-wrap) *reduces* risk of a wrong user-facing error message rather than introducing new surface.

## Next Phase Readiness

- `DeckComparisonService` is now an orchestration shell over `ScryfallReferenceResolver`, `PacketTextAssembler`, and `DeckEntryReflagHelper` — PKTSVC-01/02/03 are satisfied FOR THIS SERVICE, but remain "Pending" in `REQUIREMENTS.md` (phase-wide wording: "each of the four packet services" / "all four packet services") since `DeckAnalysisPacketService` and `MetaGapService` have not yet been migrated (plans 83-05/83-06 per the phase's remaining incomplete-plans list). Per this plan's own instruction, requirement checkboxes were left unmarked — the verifier or the final migrating plan should flip PKTSVC-01/02/03 to Complete once Analysis and MetaGap are also migrated.
- Re-run `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~ByteIdentity"` after each future migration step (83-05 onward) and treat any failure as a regression — confirmed still 25/25 green after this plan.
- No blockers. No operator action required.

---
*Phase: 83-packet-service-srp-split*
*Plan: 04*
*Completed: 2026-07-04*

## Self-Check: PASSED

- FOUND: DeckFlow.Web/Services/DeckComparisonService.cs (modified, 924 LOC)
- FOUND: DeckFlow.Web.Tests/DeckComparisonServiceTests.cs (modified)
- FOUND commit: 81d09887
- FOUND commit: 72e9f475
