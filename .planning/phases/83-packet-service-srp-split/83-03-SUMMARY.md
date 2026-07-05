---
phase: 83-packet-service-srp-split
plan: 03
subsystem: api
tags: [csharp, srp-refactor, packet-service, scryfall, deck-analysis, deck-comparison, meta-gap, xunit]

# Dependency graph
requires:
  - phase: 83-01
    provides: "25-test byte-identical regression harness (PacketByteIdentityFixtures + 4 *ByteIdentityTests.cs suites) that Wave-2 migrations must keep green"
  - phase: 83-02
    provides: "DeckFlow.Web/Services/Packets/ folder convention + PacketTextAssembler/DeckEntryReflagHelper sibling collaborators"
provides:
  - "ScryfallReferenceResolver (DeckFlow.Web/Services/Packets/) — ResolveBatchAsync (Cluster A: batch-chunk-collect-fallback) + ScryfallBatchResolution/ScryfallReferenceResolution, unconsumed by any service yet"
  - "6 new fixture-driven tests locking key-by-original-name, the single-slash normalize-fallthrough (H2), fallback-strategy-as-delegate, and original-order preservation before Wave-2 migration"
affects: ["83-04", "83-05", "83-06", "83-07"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Fallback-strategy-as-delegate: ResolveBatchAsync takes Func<string, CancellationToken, Task<ScryfallCard?>> rather than referencing SearchFallbackCardAsync or SearchPrintingFallbackCardAsync directly, preserving each service's intentionally-different miss-handling."
    - "normalizeForScryfall is an opt-in bool (default OFF) that affects only the submitted identifier; the match-back always compares the ORIGINAL request name to the returned card.Name, never the normalized submission."

key-files:
  created:
    - DeckFlow.Web/Services/Packets/ScryfallReferenceResolver.cs
    - DeckFlow.Web.Tests/ScryfallReferenceResolverTests.cs
  modified: []

key-decisions:
  - "Input contract is a flat IReadOnlyList<string> of already-deduplicated/ordered original request names (matching what all three current call sites already produce upstream — Analysis GroupBy+First, Comparison/MetaGap Distinct+OrderBy) — the collaborator does not re-dedupe or re-order on its own; it trusts the caller's list as the definition of 'original request order'."
  - "The non-2xx/null-Data HttpRequestException message is generic (not per-service-worded) since no current caller's artifact text embeds the exception message — only the exception TYPE and upstream StatusCode are load-bearing, matching the acceptance criterion's 'preserve the exact... throw (HttpRequestException with the upstream status)' wording, not literal message-text parity across services."
  - "ScryfallReferenceResolver/ScryfallBatchResolution/ScryfallReferenceResolution are internal sealed, matching the sibling PacketTextAssembler/DeckEntryReflagHelper visibility from 83-02; DeckFlow.Web.Tests already has InternalsVisibleTo access."

requirements-completed: []

# Metrics
duration: ~25min
completed: 2026-07-04
---

# Phase 83 Plan 03: ScryfallReferenceResolver Summary

**A single reusable Scryfall batch-resolution collaborator — chunk(75) -> cards/collection -> validate -> match-back-by-original-name -> per-miss fallback-delegate — that reproduces the mechanical core of all three current copy-pasted loops (Analysis/Comparison/MetaGap) byte-for-byte, fixture-tested and NOT yet wired into any service.**

## Performance

- **Duration:** ~25 min
- **Tasks:** 1
- **Files modified:** 2 (1 source + 1 test file, both new)

## Accomplishments

- `ScryfallReferenceResolver.ResolveBatchAsync` wraps the already-registered `IScryfallCardResolver` (no `new HttpClient(`, no per-call Polly pipeline construction — every upstream call routes through the injected resolver, which already owns RestSharp/Polly/`ScryfallThrottle`).
- Results are keyed by the caller's ORIGINAL request name; collection match-back compares the original name to the RETURNED card's `Name` (Ordinal-IgnoreCase) — never the normalized submission, never the returned name as the key.
- `normalizeForScryfall` is an opt-in `bool` (default `false`) affecting only the submitted `cards/collection` identifier; a test proves a single-slash `"A / B"` name normalized to `"A // B"` on submission does NOT match its own original request and correctly falls through to the fallback delegate, keyed by `"A / B"` with `FromFallback=true` — locking H2.
- The fallback strategy is a required `Func<string, CancellationToken, Task<ScryfallCard?>>` delegate parameter — neither `SearchFallbackCardAsync` nor `SearchPrintingFallbackCardAsync` is hardcoded, so Analysis's richer fallback and Comparison/MetaGap's simpler fallback both remain callable without the collaborator taking sides.
- One private static `Chunk<T>` (batch size 75) replaces the design surface for the three near-identical per-service copies (actual removal of those copies is deferred to each service's own Wave-2 migration plan, per this plan's explicit "do not touch any service yet" scope).
- 6 new fixture-driven tests (no live HTTP, real `ScryfallCardResolver` with deterministic override delegates): single-slash normalize-fallthrough (H2), printed-name-miss fallback recovery, clean-hit original-order preservation with a fallback delegate that throws if invoked (proving it's never called), mixed hit/fallback oracle-name-map keying, empty-input no-HTTP-calls, and non-2xx/null-Data `HttpRequestException` with the upstream status preserved.
- Full `DeckFlow.Web.Tests` suite: build 0/0, 1211 passed / 12 PG-skip / 0 failed (up from 83-02's 1205 baseline by exactly the 6 new tests); the 25 byte-identity tests from 83-01 remain green (unaffected — no production packet service file was touched).

## Task Commits

1. **Task 1: ScryfallReferenceResolver + ScryfallBatchResolution + shared Chunk** - `2bdb65e1` (feat)

## Files Created/Modified

- `DeckFlow.Web/Services/Packets/ScryfallReferenceResolver.cs` - `internal sealed class ScryfallReferenceResolver` with `ResolveBatchAsync` (Cluster A) + `internal sealed record ScryfallBatchResolution`/`ScryfallReferenceResolution` + private static `Chunk<T>`.
- `DeckFlow.Web.Tests/ScryfallReferenceResolverTests.cs` - 6 tests covering the single-slash fallthrough (H2), fallback recovery, clean-hit ordering, mixed oracle-name-map keying, empty input, and non-2xx status propagation.

## Decisions Made

- See `key-decisions` in frontmatter above (input-contract ordering/dedup responsibility, generic exception message, `internal sealed` visibility matching sibling collaborators).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Removed an unused `System.Net` using directive**
- **Found during:** Task 1, first `dotnet.exe build` after writing the collaborator
- **Issue:** An initial `using System.Net;` was added defensively but never referenced (the `HttpStatusCode` type flows through `RestResponse.StatusCode` without needing the explicit type name), which would have been flagged by the changed-lines format/lint gate as dead code.
- **Fix:** Removed the unused `using`.
- **Files modified:** `ScryfallReferenceResolver.cs`
- **Verification:** `dotnet.exe build` clean, 0 warnings.
- **Committed in:** `2bdb65e1` (Task 1 commit)

**2. [Rule 1 - Bug] Fixed a class-level XML doc `<paramref>` warning**
- **Found during:** Task 1, first `dotnet.exe build`
- **Issue:** The class-level `<remarks>` doc comment referenced `<paramref name="normalizeForScryfall"/>`, which is only valid inside a member's own doc comment, not a type-level one — `DeckFlow.Web.csproj`'s `GenerateDocumentationFile=true` surfaced this as `CS1734`.
- **Fix:** Changed the class-level reference to `<c>normalizeForScryfall</c>` (plain code-formatted text, matching how the same doc comment already referenced other parameter-shaped concepts at the class level).
- **Files modified:** `ScryfallReferenceResolver.cs`
- **Verification:** `dotnet.exe build` clean, 0 warnings (confirmed before commit).
- **Committed in:** `2bdb65e1` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (both build-warning cleanups caught before commit, no behavior change)
**Impact on plan:** Neither changed scope or weakened any assertion. No production packet service file was touched; this plan's file list (`ScryfallReferenceResolver.cs` + `ScryfallReferenceResolverTests.cs`) matches the plan exactly.

## Issues Encountered

None beyond the two build-warning cleanups documented above as deviations.

## Known Stubs

None. Every test exercises the real `ScryfallCardResolver` through its documented override-delegate test seam (no live HTTP) — no hardcoded empty/placeholder values flow into any asserted field.

## Threat Flags

None. This plan introduces no new network endpoints, auth paths, or file access patterns — the collaborator only calls the already-injected `IScryfallCardResolver` (which owns the actual Scryfall HTTP surface); acceptance criteria confirmed no direct `HttpClient`/Polly-pipeline construction was added.

## Next Phase Readiness

- `ScryfallReferenceResolver` exists, wraps the production `IScryfallCardResolver`, is independently fixture-tested for both fallback strategies, the normalize axis, and the single-slash match-by-original-name fallthrough — ready for Wave-2 migration plans (83-04 onward) to wire individual services onto it one at a time.
- Per this plan's explicit scope boundary: NOT registered in DI, NOT consumed by `DeckAnalysisPacketService`/`DeckComparisonService`/`MetaGapService` yet, and `DeckPrimerPacketService` is correctly untouched (it has no Scryfall card-resolution code today, per 83-RESEARCH.md Pitfall 3).
- Re-run `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~ByteIdentity"` after each future migration step and treat any failure as a regression, not an expected diff (confirmed still 25/25 green after this plan).
- No blockers. No operator action required.

---
*Phase: 83-packet-service-srp-split*
*Plan: 03*
*Completed: 2026-07-04*

## Self-Check: PASSED

- FOUND: DeckFlow.Web/Services/Packets/ScryfallReferenceResolver.cs
- FOUND: DeckFlow.Web.Tests/ScryfallReferenceResolverTests.cs
- FOUND commit: 2bdb65e1
