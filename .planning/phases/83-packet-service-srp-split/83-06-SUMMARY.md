---
phase: 83-packet-service-srp-split
plan: 06
subsystem: api
tags: [csharp, srp-refactor, packet-service, deck-analysis, scryfall, xunit]

# Dependency graph
requires:
  - phase: 83-01
    provides: "25-test byte-identical regression harness (PacketByteIdentityFixtures + 4 *ByteIdentityTests.cs suites) — this plan's gate"
  - phase: 83-02
    provides: "PacketTextAssembler (BuildSectionedDecklistText, AppendKeyValueLine)"
  - phase: 83-03
    provides: "ScryfallReferenceResolver.ResolveBatchAsync (Cluster A shared batch-chunk-collect-fallback)"
provides:
  - "DeckAnalysisPacketService (the largest packet service, 2372 LOC) migrated onto ScryfallReferenceResolver + PacketTextAssembler — third and final Scryfall-resolving service done"
  - "Regression test locking ReleasedAt/IsMdfcLand recovery through the shared resolver's fallback path for a collection-miss fixture"
affects: ["83-07"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Third application of the deck/service-labeled HttpRequestException re-wrap pattern from 83-04/83-05 — but this time traced and confirmed to be a no-op for user-facing behavior: Analysis's OWN original message already contained 'cards/collection', so it already matched the same UpstreamErrorMessageBuilder.BuildDetailedScryfallMessage branch the resolver's generic message also matches. Re-wrapped anyway for exact exception.Message byte-parity, not because the routing would have changed."

key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
    - DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs

key-decisions:
  - "Verified (not assumed) that DeckAnalysis's error-path re-wrap is NOT load-bearing for controller routing, unlike Comparison (83-04, deck-label routing) and MetaGap (83-05, generic-message routing gap): DeckPacketController's DeckAnalysis actions call UpstreamErrorMessageBuilder.BuildScryfallMessage unconditionally (no message-substring routing check), and BOTH Analysis's original message text and the resolver's generic message text contain the substring 'cards/collection', so both hit UpstreamErrorMessageBuilder.BuildDetailedScryfallMessage's identical first branch and produce byte-identical final user-facing copy either way. Re-wrapped anyway (preserving Analysis's exact original exception.Message text) for defensive parity with the 83-04/83-05 precedent and to protect any future consumer of the raw exception message (e.g. logging), at negligible cost."
  - "requestByName lookup keyed by CardReferenceRequest.Name (OrdinalIgnoreCase) reconstructs the Scope/Quantity/IsCommander metadata the resolver's ScryfallReferenceResolution (RequestName/Card/FromFallback) does not carry — the collaborator intentionally ends at 'resolved card + original name + fallback flag'; Analysis's own 9-field CardReference construction and displayName-annotation logic stay in the service, matching 83-03's documented collaborator boundary."
  - "PacketTextAssembler.BuildSectionedDecklistText is called at all 3 existing DeckAnalysisPacketService.cs call sites (base decklist, includeVersions/oracleNameMap analysis variant, oracle-resolved set-upgrade variant) with the exact same argument shapes the private BuildDecklistText took — no call-site behavior change."
  - "BuildRequestContextText: only the 6 fields whose original line used NormalizeSingleLine as a 'key: value' pattern were converted to PacketTextAssembler.AppendKeyValueLine (format, deck_name, commander, target_commander_bracket, target_ai_platform, budget_upgrade_amount). workflow_step (int, no normalizer) and the card_specific_question_card_names list-item lines ('- {name}', not a 'key: value' shape) are left as direct AppendLine calls, matching the 83-04/83-05 precedent of leaving non-matching-shape lines untouched."
  - "PKTSVC-01/02/03 requirement checkboxes left unmarked in REQUIREMENTS.md (still 'Pending'), consistent with 83-04/83-05's own instruction — DeckPrimerPacketService (83-07, resolver-free by design) is the last remaining service; the verifier or 83-07 should flip these to Complete once Primer's plan confirms it has zero duplicate Scryfall-resolution paths."

requirements-completed: []

# Metrics
duration: ~50min
completed: 2026-07-04
---

# Phase 83 Plan 06: DeckAnalysisPacketService Migration Summary

**DeckAnalysisPacketService — the largest of the four packet services (2372 LOC) — migrated onto ScryfallReferenceResolver and PacketTextAssembler, dropping to 2254 LOC with zero change to its analysis/set-upgrade paste artifacts across all 3 AI platforms and every prompt-mutating flag ON/OFF; the 25 byte-identity tests from 83-01 remain green.**

## Performance

- **Duration:** ~50 min
- **Tasks:** 2
- **Files modified:** 2 (1 source + 1 test file)

## Accomplishments

- `LookupCardReferencesAsync` now delegates its batch-chunk-collect-fallback core to `ScryfallReferenceResolver.ResolveBatchAsync` (fallback strategy = `SearchPrintingFallbackCardAsync` — Analysis's own richer all-printings search, unchanged — `normalizeForScryfall: true`, also unchanged), replacing the private `Chunk<T>` and the private batch loop. Analysis's own post-processing is preserved exactly: the 9-field `CardReference` construction (`IsMdfcLand`/`ReleasedAt`/`Quantity`/`IsCommander`/etc.), the fallback-only `displayName` annotation (`submitted_name: ... | resolved_card: ...`, keyed off `FromFallback` + `NormalizeLookupName` comparison), and the separate `ExtractMechanicNames` `HashSet` extraction.
- Removed the private `Chunk<T>` and the now-unused `ScryfallBatchSize` constant.
- Traced (not assumed) the resolver's generic `HttpRequestException` re-wrap question the plan's critical_notes flagged: confirmed `DeckPacketController`'s DeckAnalysis actions call `UpstreamErrorMessageBuilder.BuildScryfallMessage` unconditionally with no per-caller message-substring routing (unlike Comparison's "Deck A"/"Deck B" check), and confirmed BOTH Analysis's own original exception text AND the resolver's generic text contain the substring `"cards/collection"`, so both hit `BuildDetailedScryfallMessage`'s identical first branch and produce byte-identical final user-facing copy either way — this landmine does NOT apply to Analysis the way it did to Comparison/MetaGap. Re-wrapped anyway with Analysis's exact original message text for defensive exception.Message parity (negligible cost, matches 83-04/83-05 precedent).
- `BuildDecklistText`/`FormatDecklistLine` replaced by `PacketTextAssembler.BuildSectionedDecklistText` at all 3 call sites (base decklist, `includeVersions`+`oracleNameMap` analysis variant, oracle-resolved set-upgrade variant) — identical argument shapes, identical Possible-Includes-stays-plain asymmetry (H1).
- `BuildRequestContextText`'s 6 `NormalizeSingleLine`-bearing key:value fields (`format`, `deck_name`, `commander`, `target_commander_bracket`, `target_ai_platform`, `budget_upgrade_amount`) now route through `PacketTextAssembler.AppendKeyValueLine`, passing Analysis's own `NormalizeSingleLine` as the normalizer delegate. Field list, order, and the non-matching-shape lines (`workflow_step`, `include_candidate_references_in_analysis`, the `card_specific_question_card_names` list items, `selected_analysis_questions`, `selected_set_codes`) are unchanged.
- H3 confirmed: `DeckAnalysisPacketService.NormalizeSingleLine` is KEPT verbatim (internal static, unchanged body) — NOT deleted, NOT swapped for `JsonTextFormatterService.NormalizeSingleLine`. Build confirms the 6 Analysis/SetUpgrade prompt-variant files (`ChatGpt`/`Claude`/`Gemini` × `Analysis`/`SetUpgrade`) still compile against `DeckAnalysisPacketService.NormalizeSingleLine` directly.
- Do-not-unify fence honored: `ResolvePreScryfallCommanderState`'s partner-aware commander reflag, `NormalizeOracleText`, `CollapseWhitespace`, `BuildComboReferenceText`, `BuildCanonicalDeckSourceText`, and the round-tripped-JSON validation guards (`IsStructurallyValidScore`/`MaxScoreJsonLength` etc.) are all untouched.
- Added `BuildAsync_CollectionMissResolvedViaPrintingFallback_PreservesReleasedAtAndMdfcLand` to `DeckAnalysisPacketServiceTests.cs`: a modal-DFC-land fixture ("Riverglide Pathway // Lavaglide Pathway") that misses `cards/collection` and is recovered via `SearchPrintingFallbackCardAsync`, asserting the resolved reference carries both its card faces' oracle text (proving `NormalizeOracleText` ran on the fallback-returned card) and the `[MDFC-land]` marker (proving `IsMdfcLand`/`ReleasedAt` survived the shared collaborator's fallback path into the 9-field `CardReference`).
- `DeckAnalysisPacketService.cs`: 2372 -> 2254 LOC (batch loop, `Chunk`, `ScryfallBatchSize`, `BuildDecklistText`, `FormatDecklistLine` removed; divergent clusters — partner reflag, oracle-text normalization, combo formatter, cache-key text, JSON-validation guards, per-flag prompt blocks — intentionally retained, per the plan's LOC-parity clarification).
- Full `DeckFlow.Web.Tests` suite: build 0/0, 1214 passed / 12 PG-skip / 0 failed (up from 83-05's 1213 baseline by exactly the 1 new test); all 25 byte-identity tests from 83-01 remain green, including all 12 `DeckAnalysisByteIdentityTests` cases (3-platform baseline, 6 individually-toggled flags, all-4-mutating-flags-ON companion/partner fixture, versioned-decklist + collection-miss-fallback fixture, whitespace-bearing fixture); the full 176-test (175 prior + 1 new) `DeckAnalysisPacketServiceTests` suite passes.

## Task Commits

1. **Task 1: Delegate Scryfall resolution to ScryfallReferenceResolver (all-printings + normalize ON)** - `3a6cb6b8` (feat)
2. **Task 2: Delegate sectioned decklist + migrate NormalizeSingleLine routing; confirm shell reduction** - `b282e97f` (feat)

## Files Created/Modified

- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` - Added `_scryfallReferenceResolver` field (instantiated `new ScryfallReferenceResolver(scryfallCardResolver)` in the ctor); `LookupCardReferencesAsync` delegates to `ResolveBatchAsync` with an exception re-wrap preserving Analysis's original message text; decklist text at all 3 call sites delegates to `PacketTextAssembler.BuildSectionedDecklistText`; `BuildRequestContextText`'s 6 normalized fields route through `PacketTextAssembler.AppendKeyValueLine` passing Analysis's own `NormalizeSingleLine`. Removed private `Chunk<T>`, `ScryfallBatchSize` const, `BuildDecklistText`, `FormatDecklistLine`. `NormalizeSingleLine` itself UNCHANGED (H3). Net: 2372 -> 2254 LOC.
- `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs` - Added `BuildAsync_CollectionMissResolvedViaPrintingFallback_PreservesReleasedAtAndMdfcLand`.

## Decisions Made

See `key-decisions` in frontmatter above (verified-not-load-bearing error-path re-wrap, `requestByName` metadata reconstruction, identical call-site argument shapes for `BuildSectionedDecklistText`, which 6 of the request-context fields route through `AppendKeyValueLine`, requirement checkboxes left unmarked pending 83-07).

## Deviations from Plan

None requiring a fix — one investigation worth flagging for the verifier:

**Investigated (no fix needed): the resolver's generic HttpRequestException re-wrap is NOT load-bearing for DeckAnalysis, unlike Comparison/MetaGap**
- **Found during:** Task 1, following the plan's `<critical_notes>` instruction to "WATCH the error-path" per the 83-04/83-05 precedent.
- **What was checked:** `DeckPacketController`'s `DeckAnalysis` GET/POST actions and `AnalysisPromptApiController`'s catch blocks — all call `UpstreamErrorMessageBuilder.BuildScryfallMessage(exception)` unconditionally, with NO message-substring routing check (unlike Comparison's "Deck A"/"Deck B" check that made the 83-04 re-wrap load-bearing). Then traced `UpstreamErrorMessageBuilder.BuildDetailedScryfallMessage`: its first branch matches on `message.Contains("cards/collection") || message.Contains("analysis packet")`. Analysis's ORIGINAL exception message ("...returned HTTP {code} while building the analysis packet.") already contains BOTH substrings; the resolver's generic message ("...returned HTTP {code}.") contains "cards/collection" only — but that alone is sufficient to hit the SAME branch and produce the SAME final copy ("Scryfall card reference lookup failed while building the analysis packet with HTTP {code}. Try again shortly.").
- **Conclusion:** no re-wrap was actually necessary for correct final user-facing behavior — this is a genuine difference from 83-04 (Comparison's deck-label routing) and 83-05 (MetaGap's message not matching either substring pre-migration, causing a real routing regression). Re-wrapped anyway, preserving Analysis's exact original `exception.Message` text, for defensive exception-message byte-parity at negligible cost and to match the established precedent's shape — not because the routing would otherwise have broken.
- **Files modified:** `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (the re-wrap is present, just confirmed non-load-bearing).
- **Verification:** No existing test asserts the exact HTTP-error message text (confirmed via grep, same finding as 83-04/83-05). Full suite green.
- **Committed in:** `3a6cb6b8` (Task 1 commit)

## Issues Encountered

None beyond the investigation documented above.

## Known Stubs

None. All resolved cards flow through the real `ScryfallReferenceResolver`/`ScryfallCardResolver` test seams (deterministic override delegates, no live HTTP) — no hardcoded empty/placeholder values.

## Threat Flags

None. This plan re-routes internal call graphs only (T-83-10/T-83-11/T-83-12's disposition from the plan's threat register); no new network endpoints, auth paths, file access patterns, or schema changes were introduced. Do-not-unify fence items (partner-aware reflag, JSON-validation guards) verified untouched by grep before commit.

## Next Phase Readiness

- `DeckAnalysisPacketService` is now an orchestration shell over `ScryfallReferenceResolver` and `PacketTextAssembler` (for its 2 mechanical clusters), retaining its intentionally-divergent clusters (partner reflag, full oracle-text normalization, combo formatter, cache-key text, JSON-validation guards, per-flag prompt blocks) — PKTSVC-01/02/03 are satisfied FOR THIS SERVICE, matching Comparison (83-04) and MetaGap (83-05). All 3 Scryfall-resolving packet services are now migrated onto the shared collaborators; only `DeckPrimerPacketService` (83-07, resolver-free by design per 83-RESEARCH.md Pitfall 3) remains.
- Requirement checkboxes (PKTSVC-01/02/03) left unmarked in `REQUIREMENTS.md`, per this plan's own instruction and the 83-04/83-05 precedent — the verifier or 83-07 should flip them to Complete once Primer's plan confirms it has zero duplicate Scryfall-resolution paths (it has none today).
- Re-run `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~ByteIdentity"` after 83-07 and treat any failure as a regression — confirmed still 25/25 green after this plan.
- No blockers. No operator action required.

---
*Phase: 83-packet-service-srp-split*
*Plan: 06*
*Completed: 2026-07-04*

## Self-Check: PASSED

- FOUND: DeckFlow.Web/Services/DeckAnalysisPacketService.cs (modified, 2254 LOC)
- FOUND: DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs (modified)
- FOUND commit: 3a6cb6b8
- FOUND commit: b282e97f
