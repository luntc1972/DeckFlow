---
phase: 83-packet-service-srp-split
plan: 07
subsystem: api
tags: [csharp, srp-refactor, packet-service, deck-primer, xunit]

# Dependency graph
requires:
  - phase: 83-01
    provides: "25-test byte-identical regression harness (PacketByteIdentityFixtures + 4 *ByteIdentityTests.cs suites) — this plan's gate"
  - phase: 83-02
    provides: "PacketTextAssembler.BuildSectionedDecklistText + AppendKeyValueLine"
provides:
  - "DeckPrimerPacketService (the fourth and final packet service, resolver-free by design) migrated onto PacketTextAssembler"
  - "All four packet services (Analysis/Comparison/MetaGap/Primer) now delegate their decklist/request-context assembly mechanics to shared collaborators — PKTSVC-01/02/03 satisfied phase-wide"
  - "Source-scan regression test proving DeckPrimerPacketService has zero IScryfallCardResolver/ScryfallReferenceResolver references (PKTSVC-02 by verified absence)"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Fourth and final application of the PacketTextAssembler migration pattern established in 83-04/83-05/83-06 — Primer needed only the text-assembly collaborator (no ScryfallReferenceResolver, no DeckEntryReflagHelper), since it never resolves Scryfall cards and never reflags a commander from a flat entry list."
    - "Source-scan tripwire test (mirrors DeckFlow.Core.Tests/CarveOutGuardTests.cs's repo-root-walk pattern) asserting absence of a forbidden type reference, rather than presence of a required one — used here to lock a negative invariant (no Scryfall dependency) that a positive unit test cannot express."

key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/DeckPrimerPacketService.cs
    - DeckFlow.Web.Tests/DeckPrimerPacketServiceTests.cs

key-decisions:
  - "PKTSVC-02 satisfied for Primer by VERIFIED ABSENCE, not by wiring in a resolver: 83-RESEARCH.md Pitfall 3 flagged that Primer has zero existing Scryfall card-resolution code, and adding IScryfallCardResolver/ScryfallReferenceResolver to satisfy the requirement's literal wording ('consumed by all four') would be a net-new feature prohibited by the milestone's no-new-feature gate. Confirmed via grep before and after migration: zero hits."
  - "DeckPrimerPacketService.NormalizeSingleLine (internal static, char-by-char CollapseWhitespace) kept 100% unchanged — the 3 Primer prompt-variant files (ChatGpt/Claude/Gemini) call it directly by name; deleting or swapping it for JsonTextFormatterService's newline-only normalizer would break their compile and silently change Primer's output for any whitespace-irregular card/deck text."
  - "PacketTextAssembler.BuildSectionedDecklistText called with its two optional parameters left at their default 'off' values (includeVersions=false, oracleNameMap=null) since Primer's original BuildDecklistText never annotated printed-as names or version suffixes — the assembler reproduces Primer's exact 3-section shape by construction, with zero argument-shape difference from the removed private method."
  - "5 of BuildRequestContextText's 8 field lines (format, deck_name, commander, target_commander_bracket, target_ai_platform) migrated to PacketTextAssembler.AppendKeyValueLine, passing Primer's own NormalizeSingleLine as the Func<string?,string,string> delegate. The other 3 (workflow_step: int, primer_style: enum, selected_section_ids:/deck_source: multi-line blocks) are NOT 'key: value'-with-normalizer shaped and were left as direct AppendLine calls, matching the 83-04/83-05/83-06 precedent of leaving non-matching-shape lines untouched."
  - "This being the LAST migrating plan in the phase, all four PKTSVC requirements (01/02/03/04) were evaluated against their full text and marked complete via `requirements mark-complete` — PKTSVC-04 was already complete from 83-01; PKTSVC-01/02/03 were deferred by 83-02 through 83-06 specifically pending this plan's completion of the fourth and final service migration."

requirements-completed: [PKTSVC-01, PKTSVC-02, PKTSVC-03]

# Metrics
duration: ~35min
completed: 2026-07-04
---

# Phase 83 Plan 07: DeckPrimerPacketService Migration Summary

**DeckPrimerPacketService — the fourth and final packet service, and the only one of the four with zero Scryfall card-resolution code — migrated onto PacketTextAssembler, dropping from 905 to 866 LOC with zero change to its per-platform primer artifacts across all 3 AI variants; this closes out the phase, with all four PKTSVC requirements now marked complete.**

## Performance

- **Duration:** ~35 min
- **Tasks:** 1
- **Files modified:** 2 (1 source + 1 test file)

## Accomplishments

- `BuildAsync`'s decklist-text call site now delegates to `PacketTextAssembler.BuildSectionedDecklistText(playableEntries, possibleIncludeEntries)` (both optional parameters left at their default off values), replacing the private `BuildDecklistText` — reproducing Primer's exact Commander/Mainboard/Possible-Includes shape (no version suffix, no oracle-name annotation, since Primer never had either).
- Deleted the now-unused private `BuildDecklistText` method.
- `BuildRequestContextText`'s 5 `NormalizeSingleLine`-bearing key:value fields (`format`, `deck_name`, `commander`, `target_commander_bracket`, `target_ai_platform`) now route through `PacketTextAssembler.AppendKeyValueLine`, passing `DeckPrimerPacketService.NormalizeSingleLine` as the normalizer delegate. `workflow_step` (int), `primer_style` (enum), and the `selected_section_ids:`/`deck_source:` multi-line blocks are left as direct `AppendLine` calls (non-matching shape).
- H3 confirmed: `DeckPrimerPacketService.NormalizeSingleLine` kept 100% verbatim (internal static, unchanged body) — NOT deleted, NOT swapped for `JsonTextFormatterService.NormalizeSingleLine`. Build confirms the 3 Primer prompt-variant files (`ChatGpt`/`Claude`/`Gemini`) still compile against it directly (grep-confirmed 8 call sites across the 3 files, unaffected by this migration).
- Do-not-unify fence honored: `CollapseWhitespace` (char-by-char, ANY-whitespace-run collapse — different from Analysis/Comparison/MetaGap's newline-only collapse), `BuildCanonicalDeckSourceText`/`EvaluateStaleness` (staleness-hash cache-key text, doc comment says order/format MUST NOT change), and `BuildComboReferenceText` (Markdown combo formatter with popularity/mana-value ranking) are all untouched — confirmed unchanged by post-edit grep.
- Added `DeckPrimerPacketServiceTests.SourceFile_ReferencesNoScryfallResolutionType`: a source-scan tripwire (mirrors `CarveOutGuardTests.cs`'s repo-root-walk pattern) asserting the service file contains zero `IScryfallCardResolver`/`ScryfallReferenceResolver` substring references — locks PKTSVC-02's "no duplicate resolution path" claim as a regression guard, not just a doc comment.
- `DeckPrimerPacketService.cs`: 905 → 866 LOC (39-line net reduction: private `BuildDecklistText` removed, 5 request-context lines collapsed to 1-line `AppendKeyValueLine` calls each).
- Full `DeckFlow.Web.Tests` suite: build 0/0, 1214 passed / 12 PG-skip / 0 failed (unchanged from 83-06's baseline: this plan added 1 test and removed 0, a net wash against pre-existing drift). All 25 byte-identity tests from 83-01 remain green, including all 3 `DeckPrimerByteIdentityTests` cases (3-platform baseline via `PromptTextsByPlatform`, whitespace-bearing DeckName locking `CollapseWhitespace`, `tool.primer.stale-flag` ON/OFF no-op proof). The `--filter "FullyQualifiedName~DeckPrimer"` run (all Primer-named test classes combined) passes 48/48; `DeckPrimerPacketServiceTests.cs` alone passes 17/17 (16 pre-existing + the 1 new source-scan test).

## Task Commits

1. **Task 1: Delegate sectioned decklist (keep NormalizeSingleLine); verify zero Scryfall path** - `c8c554a4` (feat)

## Files Created/Modified

- `DeckFlow.Web/Services/DeckPrimerPacketService.cs` - Decklist text at the sole `BuildAsync` call site delegates to `PacketTextAssembler.BuildSectionedDecklistText`; `BuildRequestContextText`'s 5 normalized fields route through `PacketTextAssembler.AppendKeyValueLine` passing Primer's own `NormalizeSingleLine`. Removed private `BuildDecklistText`. `NormalizeSingleLine`/`CollapseWhitespace`/`BuildCanonicalDeckSourceText`/`EvaluateStaleness`/`BuildComboReferenceText` all UNCHANGED. Net: 905 → 866 LOC.
- `DeckFlow.Web.Tests/DeckPrimerPacketServiceTests.cs` - Added `SourceFile_ReferencesNoScryfallResolutionType` (source-scan tripwire proving zero Scryfall-resolution-type references) and its `FindServiceSourcePath` repo-root-walk helper.

## Decisions Made

See `key-decisions` in frontmatter above (verified-absence PKTSVC-02 satisfaction, `NormalizeSingleLine` preservation, default-off `BuildSectionedDecklistText` arguments, which 5 of the 8 request-context fields route through `AppendKeyValueLine`, phase-wide requirement mark-complete since this is the last migrating plan).

## Deviations from Plan

None - plan executed exactly as written. The plan's single task completed with build 0/0 and all targeted tests green on the first pass; no auto-fixes were needed.

## Issues Encountered

None.

## Known Stubs

None. All decklist/request-context text flows through the real `PacketTextAssembler` collaborator against real `DeckEntry` fixtures — no hardcoded empty/placeholder values.

## Threat Flags

None. This plan re-routes internal call graphs only (per the plan's own threat register T-83-13/T-83-14 dispositions — both mitigated by the do-not-unify fence staying intact and the zero-Scryfall-hits grep/test respectively); no new network endpoints, auth paths, file access patterns, or schema changes were introduced.

## Requirements Evaluation (phase-wide, as the final migrating plan)

Per the plan's instruction, all four PKTSVC requirements were evaluated against their full text now that all four packet services are migrated:

- **PKTSVC-01** ("shared prompt-assembly orchestration... each of the four packet services delegates to it"): all four services (Analysis 83-06, Comparison 83-04, MetaGap 83-05, Primer 83-07) now call `PacketTextAssembler.BuildSectionedDecklistText`/`AppendKeyValueLine`. **Marked complete.**
- **PKTSVC-02** ("Scryfall reference-resolution logic... consumed by all four packet services; no service retains a duplicate resolution code path"): three services (Analysis, Comparison, MetaGap) consume `ScryfallReferenceResolver`; Primer has and retains zero Scryfall resolution code, satisfying "no duplicate resolution path remains" by verified absence rather than literal consumption — the interpretation recorded in 83-RESEARCH.md Pitfall 3 and reaffirmed by every intervening plan's summary (83-02 through 83-06). **Marked complete.**
- **PKTSVC-03** ("each of the four packet services reduced to an orchestration shell... collaborators unit-tested in isolation"): all four services shrank materially (Analysis 2372→2254, Comparison 1033→924, MetaGap 956→909, Primer 905→866) by delegating their mechanical assembly clusters to independently-tested collaborators (`PacketTextAssembler`, `ScryfallReferenceResolver`, `DeckEntryReflagHelper`), retaining only genuinely divergent per-service logic (combo formatters, oracle-text normalization, cache-key text, commander-inference heuristics, JSON-validation guards). **Marked complete.**
- **PKTSVC-04** (byte-identical regression guard): already complete since 83-01; reconfirmed green after this plan (25/25).

All 4 requirement checkboxes flipped in `.planning/REQUIREMENTS.md` via `gsd-sdk query requirements.mark-complete PKTSVC-01 PKTSVC-02 PKTSVC-03` (PKTSVC-04 was already `[x]`); traceability table rows for all 4 now read `Complete`.

## Next Phase Readiness

- Phase 83 (Packet-Service SRP Split) is now fully complete — all four packet services migrated, all four PKTSVC requirements satisfied, 25/25 byte-identity tests green, build 0/0, full `DeckFlow.Web.Tests` suite 1214/1214 (12 PG-skip).
- Next per STATE.md's roadmap: Phase 84 (`--accent-strong` semantic-token migration).
- No blockers. No operator action required.

---
*Phase: 83-packet-service-srp-split*
*Plan: 07*
*Completed: 2026-07-04*

## Self-Check: PASSED

- FOUND: DeckFlow.Web/Services/DeckPrimerPacketService.cs
- FOUND: DeckFlow.Web.Tests/DeckPrimerPacketServiceTests.cs
- FOUND commit: c8c554a4
