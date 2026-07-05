---
phase: 83-packet-service-srp-split
plan: 02
subsystem: api
tags: [csharp, srp-refactor, packet-service, deck-analysis, deck-comparison, meta-gap, deck-primer, xunit]

# Dependency graph
requires:
  - phase: 83-01
    provides: "25-test byte-identical regression harness (PacketByteIdentityFixtures + 4 *ByteIdentityTests.cs suites) that Wave-2 migrations must keep green"
provides:
  - "PacketTextAssembler (DeckFlow.Web/Services/Packets/) — BuildSectionedDecklistText (Cluster D) + AppendKeyValueLine (Cluster E), unconsumed by any service yet"
  - "DeckEntryReflagHelper (DeckFlow.Web/Services/Packets/) — ReflagCommanderEntry (Cluster B first-match reflag), unconsumed by any service yet"
  - "11 new characterization/unit tests locking both collaborators' behavior before Wave-2 migration"
affects: ["83-03", "83-04", "83-05", "83-06", "83-07"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "New DeckFlow.Web/Services/Packets/ folder for cross-service collaborators extracted from the four packet god-services, mirroring the DeckFlow.Studio *Coordinator precedent."
    - "Normalizer-as-delegate: AppendKeyValueLine takes Func<string?, string, string> rather than referencing any concrete NormalizeSingleLine, preserving the 3 non-byte-equivalent per-service normalizers."

key-files:
  created:
    - DeckFlow.Web/Services/Packets/PacketTextAssembler.cs
    - DeckFlow.Web/Services/Packets/DeckEntryReflagHelper.cs
    - DeckFlow.Web.Tests/PacketTextAssemblerTests.cs
    - DeckFlow.Web.Tests/DeckEntryReflagHelperTests.cs
  modified: []

key-decisions:
  - "BuildSectionedDecklistText's Possible-Includes line-building was factored into a private FormatPossibleIncludeLine helper (kept separate from FormatDecklistLine) to make the H1 asymmetry structurally obvious in code, not just documented in comments — matches the plan's ≤30-line-methods guideline."
  - "Characterization tests build their own expected-output fixtures (not captured goldens from a live BuildAsync run) since these two collaborators are net-new, unconsumed classes — there is no existing service output to capture from yet. The three shapes (Analysis/Comparison/Primer) are reproduced from the exact algorithm read out of each service's current BuildDecklistText/FormatDecklistLine, verified against 83-RESEARCH.md's line-cited descriptions."
  - "DeckEntryReflagHelper's body verified byte-identical via a direct `diff` of the two source line ranges (DeckComparisonService.cs:363-383 vs MetaGapService.cs:465-485) before extraction, per research Pitfall 1 and the plan's acceptance criterion."

requirements-completed: [PKTSVC-01, PKTSVC-03]

# Metrics
duration: ~30min
completed: 2026-07-04
---

# Phase 83 Plan 02: PacketTextAssembler + DeckEntryReflagHelper Summary

**Two new pure static collaborators under `DeckFlow.Web/Services/Packets/` — a sectioned-decklist/key-value text assembler and a first-match commander reflag helper — each characterization-tested and NOT yet wired into any of the four packet services.**

## Performance

- **Duration:** ~30 min
- **Tasks:** 2
- **Files modified:** 4 (2 source + 2 test files, all new)

## Accomplishments

- `PacketTextAssembler.BuildSectionedDecklistText` reproduces the exact Commander/Mainboard/Possible-Includes section layout used by Analysis (`includeVersions=true` + `oracleNameMap`), Comparison (`oracleNameMap` only), and Primer (neither) — including the H1 asymmetry where Possible-Includes lines never receive the version suffix or DFC `" // "` truncation even when `includeVersions=true`.
- `PacketTextAssembler.AppendKeyValueLine` takes the normalizer as a `Func<string?, string, string>` delegate rather than referencing any concrete `NormalizeSingleLine`, proven by a test asserting two different normalizer delegates produce two different outputs for the same tab/newline-bearing input.
- `DeckEntryReflagHelper.ReflagCommanderEntry` is a verbatim copy of the byte-identical `DeckComparisonService`/`MetaGapService` private methods (confirmed via `diff` before extraction, zero differences), with a doc comment recording that Analysis's partner-aware reflag is deliberately excluded.
- 11 new tests (6 for the assembler, 5 for the reflag helper) all pass; full `DeckFlow.Web.Tests` build 0/0; the 25 byte-identity tests from 83-01 remain green (unaffected, since no production service file was touched).

## Task Commits

1. **Task 1: PacketTextAssembler (sectioned decklist + key:value line writer)** - `004af4f9` (feat)
2. **Task 2: DeckEntryReflagHelper (Cluster B first-match commander reflag)** - `339c97a2` (feat)

## Files Created/Modified

- `DeckFlow.Web/Services/Packets/PacketTextAssembler.cs` - `internal static class` with `BuildSectionedDecklistText` (Cluster D) and `AppendKeyValueLine` (Cluster E); private helpers `AppendCommanderSection`/`AppendMainboardSection`/`AppendPossibleIncludesSection`/`FormatPossibleIncludeLine`/`FormatDecklistLine`.
- `DeckFlow.Web/Services/Packets/DeckEntryReflagHelper.cs` - `internal static class` with `ReflagCommanderEntry` (Cluster B), verbatim copy of the Comparison/MetaGap method.
- `DeckFlow.Web.Tests/PacketTextAssemblerTests.cs` - 6 tests: Analysis shape, Comparison shape, Primer shape, no-commander/no-possible-includes omission, and two `AppendKeyValueLine` delegate tests.
- `DeckFlow.Web.Tests/DeckEntryReflagHelperTests.cs` - 5 tests: single match, first-of-two matches, no match, `Quantity>1` not reflagged, already-commander idempotency.

## Decisions Made

- See `key-decisions` in frontmatter above (FormatPossibleIncludeLine extraction, characterization-test fixture design, verbatim-diff verification for the reflag helper).

## Deviations from Plan

None - plan executed exactly as written. Both collaborators were built as pure static classes with no service migration (deferred to Wave-2 plans 83-03 onward, per the plan's explicit scope boundary).

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Both collaborators exist, are independently unit-tested, and reproduce every current shape by construction — ready for Wave-2 migration plans (83-03 onward) to wire individual services onto them one at a time, re-running the 83-01 byte-identity harness after each migration step.
- ADR-0001 intact: no prompt prose lives in `PacketTextAssembler`; it owns structure only (decklist section layout + key:value line format).
- No blockers. No operator action required.

---
*Phase: 83-packet-service-srp-split*
*Plan: 02*
*Completed: 2026-07-04*

## Self-Check: PASSED

- FOUND: DeckFlow.Web/Services/Packets/PacketTextAssembler.cs
- FOUND: DeckFlow.Web/Services/Packets/DeckEntryReflagHelper.cs
- FOUND: DeckFlow.Web.Tests/PacketTextAssemblerTests.cs
- FOUND: DeckFlow.Web.Tests/DeckEntryReflagHelperTests.cs
- FOUND commit: 004af4f9
- FOUND commit: 339c97a2
