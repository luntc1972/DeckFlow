---
phase: 83-packet-service-srp-split
plan: 01
subsystem: testing
tags: [xunit, byte-identity, golden-testing, scryfall, prompt-variants, deck-analysis, deck-comparison, meta-gap, deck-primer]

# Dependency graph
requires: []
provides:
  - "PKTSVC-04 byte-identical regression harness covering all 4 packet services (Analysis/Comparison/MetaGap/Primer) x 3 AI platforms"
  - "Reusable PacketByteIdentityFixtures.cs (shared card catalog, deck fixtures, whitespace fixtures, CRLF/timestamp normalization) for Wave-2 migration plans"
  - "Documented per-service whitespace-collapse asymmetries (Analysis/Comparison/MetaGap: newline-only; Primer: any-whitespace-run) that Wave-2 must not accidentally unify"
affects: ["83-02", "83-03", "83-04", "83-05", "83-06", "83-07"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Golden-capture-from-real-run discipline: dump actual BuildAsync output to a temporary capture test + file, verify by inspection, embed as a checked-in C# raw string literal, then delete the capture test — never hand-type a golden."
    - "OS-independent byte comparison: normalize both the live `generated_at_utc` timestamp AND \\r\\n->\\n line endings (Windows dotnet.exe capture vs ubuntu-latest CI) before Ordinal comparison."

key-files:
  created:
    - DeckFlow.Web.Tests/PacketByteIdentityFixtures.cs
    - DeckFlow.Web.Tests/DeckAnalysisByteIdentityTests.cs
    - DeckFlow.Web.Tests/DeckComparisonByteIdentityTests.cs
    - DeckFlow.Web.Tests/MetaGapByteIdentityTests.cs
    - DeckFlow.Web.Tests/DeckPrimerByteIdentityTests.cs
    - DeckFlow.Web.Tests/AnalysisGoldens.cs
    - DeckFlow.Web.Tests/ComparisonGoldens.cs
    - DeckFlow.Web.Tests/MetaGapGoldens.cs
    - DeckFlow.Web.Tests/PrimerGoldens.cs
  modified: []

key-decisions:
  - "Golden constants factored into separate *Goldens.cs files (not inlined in the *ByteIdentityTests.cs files) for readability — a deviation from the plan's literal 5-file list, but still within the plan's stated persistence mechanism choice (checked-in string constants)."
  - "Discovered and fixed two real non-determinism bugs in the harness design itself before it could be trusted as a gate: a live generated_at_utc timestamp embedded in Analysis/Comparison artifacts, and Windows-vs-Linux CRLF/LF divergence (dotnet.exe captures with \\r\\n; CI runs ubuntu-latest with \\n)."
  - "One golden (Analysis's WhitespaceRequestContextText) uses an escaped regular C# string instead of a raw string literal, because it deliberately embeds a bare CR (H3 fixture) that the LF-only format gate rejects inside a raw literal — the CarveOutGuard exemption list does not need to change, this is a normal escaped-string workaround, not a new carve-out."

requirements-completed: [PKTSVC-04]

# Metrics
duration: ~90min
completed: 2026-07-04
---

# Phase 83 Plan 01: Byte-Identical Regression Harness Summary

**25 xUnit byte-identity tests across all 4 packet services (Analysis/Comparison/MetaGap/Primer) x 3 AI platforms, with goldens captured verbatim from real BuildAsync runs against today's unrefactored code — the safety net every Wave-2 migration in this phase must keep green.**

## Performance

- **Duration:** ~90 min
- **Tasks:** 2
- **Files modified:** 9 (5 test/fixture files matching the plan's file list + 4 factored-out golden-data files)

## Accomplishments

- Built `PacketByteIdentityFixtures.cs`: a shared Scryfall card catalog, deterministic collection/search/named response fakes, deck-entry builders, whitespace fixtures, and per-service service-construction seams (Analysis direct-ctor with `IFeatureFlagCache` control; Primer direct-ctor with the REAL ChatGPT/Claude/Gemini prompt variants) reused across all four suites.
- `DeckAnalysisByteIdentityTests.cs` (12 cases): 3-platform baseline (all 6 flags OFF), each of the 6 flag keys individually ON, ALL-4-mutating-flags-ON (companion/partner fixture), a combined versioned-decklist + single-slash-collection-miss-fallback fixture (locks the H1 asymmetry: commander/mainboard get versioned suffixes and DFC-slash truncation, Possible-Includes stays PLAIN), and a whitespace-bearing DeckName/StrategyNotes/MetaNotes fixture.
- `DeckComparisonByteIdentityTests.cs` (5 cases): 3-platform baseline (confirmed Comparison has no prompt-mutating flags), printed-name SearchFallback fixture, no-explicit-Commander-section reflag fixture.
- `MetaGapByteIdentityTests.cs` (5 cases): 3-platform baseline (confirmed no flags, no whitespace-bearing free-text fields), forced collection-miss -> `SearchFallbackCardAsync` fallthrough, no-explicit-Commander-section reflag fixture.
- `DeckPrimerByteIdentityTests.cs` (3 cases): baseline across all 3 enabled `PromptTextsByPlatform` keys, whitespace-bearing DeckName locking Primer's DIFFERENT char-by-char `CollapseWhitespace`, and a `tool.primer.stale-flag` ON/OFF proof that prompt bytes are unaffected (the service has no `IFeatureFlagCache` dependency at all — the flag is read only by the controller for a UI banner).
- Full `DeckFlow.Web.Tests` suite: build 0/0, 1194 passed / 12 PG-skip / 0 failed (up from the pre-existing 1158/12 baseline recorded in STATE.md — net +36 across the two commits once existing suite drift is accounted for, +25 of which are this plan's new byte-identity tests).

## Task Commits

1. **Task 1: Shared fixtures + Analysis & Comparison byte-identity suites** - `70259bff` (test)
2. **Task 2: MetaGap & Primer byte-identity suites** - `299e793f` (test)

## Files Created/Modified

- `DeckFlow.Web.Tests/PacketByteIdentityFixtures.cs` - Shared card catalog, deck/whitespace fixtures, response fakes, Analysis/Primer service-construction seams, `NormalizeForGoldenComparison` (timestamp + CRLF normalization).
- `DeckFlow.Web.Tests/DeckAnalysisByteIdentityTests.cs` - 12-case Analysis byte-identity suite.
- `DeckFlow.Web.Tests/DeckComparisonByteIdentityTests.cs` - 5-case Comparison byte-identity suite.
- `DeckFlow.Web.Tests/MetaGapByteIdentityTests.cs` - 5-case MetaGap byte-identity suite.
- `DeckFlow.Web.Tests/DeckPrimerByteIdentityTests.cs` - 3-case Primer byte-identity suite.
- `DeckFlow.Web.Tests/AnalysisGoldens.cs`, `ComparisonGoldens.cs`, `MetaGapGoldens.cs`, `PrimerGoldens.cs` - Golden constants, one file per service, factored out of the test files for readability.

## Decisions Made

- **Golden persistence mechanism:** checked-in C# string constants (raw string literals where possible; one escaped regular string where a raw literal would embed a bare CR that the format gate rejects), per the plan's "pick ONE mechanism" instruction — factored into separate `*Goldens.cs` files rather than inlined, for readability given each golden set runs 5-100+ KB of captured text.
- **Card catalog reuse:** one shared `PacketByteIdentityFixtures.CardCatalog()` (Kraum/Command Tower/Arcane Signet/Sol Ring/Ponder/Swords to Plowshares/Blex DFC/Atraxa/Counterspell/Wrath of God/Perfect Defense-Denting Blows) consumed by Analysis, Comparison, and MetaGap collection/search fakes — the collection fake always returns the FULL catalog (mirroring the existing `DeckAnalysisPacketServiceTests.CreateCollectionResponse` pattern), so any submitted name that does not EXACTLY match a catalog `Name` naturally falls through to the search/named fallback, which is exactly the collection-miss/fallback behavior item 2 of the plan's path-coverage requirements needs.
- **Analysis's all-flags-off baseline sets `ReferenceFullOracleFlag=true`** (meaning `IsEnabled()` returns true / the recency gate is OFF) to match the flag's documented default-on semantics; `WithSingleFlagOn` flips it to `false` for that one key specifically (representing the gate becoming enabled), since this flag's "on" state is inverted relative to the other 5.
- **MetaGap needs no whitespace fixture:** confirmed by reading `MetaGapRequest.cs` in full — its only string field is `CommanderName`, already covered by `JsonTextFormatterService.NormalizeSingleLine` (the same shared helper Comparison uses); there is no DeckName/StrategyNotes/MetaNotes-equivalent free-text field to lock, so item 5 legitimately does not apply to MetaGap (documented in the suite's XML doc comment rather than silently skipped).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Normalized the live `generated_at_utc` timestamp embedded in Analysis/Comparison artifacts**
- **Found during:** Task 1 (first real capture run of the Analysis baseline)
- **Issue:** `DeckAnalysisPacketService.cs:1253` and `DeckComparisonService.cs:576` both call `DateTime.UtcNow` directly inside `BuildReferenceText`/`BuildComparisonContextText`, embedding a live wall-clock timestamp into the exact text the harness must compare byte-for-byte. Without normalization, EVERY run of the suite would fail (or worse, a naive test author might have hand-typed a "close enough" golden, defeating W3's golden-capture-integrity requirement) — this is precisely the T-83-02 threat ("non-deterministic capture... causing flaky goldens") called out in the plan's threat register.
- **Fix:** Added `PacketByteIdentityFixtures.NormalizeVolatileTimestamps` — a compiled regex replacing `generated_at_utc: <ISO8601>` with a fixed placeholder — applied to both AnalysisPromptText/ReferenceText and the Comparison prompt/context before every comparison.
- **Files modified:** `PacketByteIdentityFixtures.cs`
- **Verification:** Re-ran the full suite twice in a row; identical pass with no flakiness.
- **Committed in:** `70259bff` (Task 1 commit)

**2. [Rule 1 - Bug] Fixed an OS-dependent CRLF/LF mismatch that would have broken the harness on Linux CI**
- **Found during:** Task 1, while designing the golden-capture pipeline (caught before any golden was finalized, not via a later CI failure)
- **Issue:** Every artifact under test is built with `StringBuilder.AppendLine`, which appends `Environment.NewLine` — `"\r\n"` under the Windows `dotnet.exe` this harness was captured with, but `"\n"` on the `ubuntu-latest` runner that actually gates this repo (`.github/workflows/ci.yml:17,44`). A golden captured verbatim on Windows and compared byte-for-byte on Linux CI would fail on every single assertion despite the actual prompt CONTENT being unchanged — a self-inflicted flaky-gate bug, not a real regression.
- **Fix:** Added `PacketByteIdentityFixtures.NormalizeForGoldenComparison` (composes the timestamp normalization above with a `"\r\n"` -> `"\n"` replace) and applied it to EVERY field compared in all four suites (previously only the two timestamp-bearing fields were normalized; `RequestContextText`/`DecklistText`/`FollowUpPromptText` etc. were not). Regenerated all golden `.cs` files from the same capture dumps with the same normalization applied so both sides of every assertion are `\n`-only regardless of which OS builds/runs the suite. The deliberate bare-CR whitespace fixture (H3) is unaffected — the replace only touches `"\r\n"` pairs, never a lone `"\r"`.
- **Files modified:** `PacketByteIdentityFixtures.cs`, `DeckAnalysisByteIdentityTests.cs`, `DeckComparisonByteIdentityTests.cs`, `AnalysisGoldens.cs`, `ComparisonGoldens.cs` (MetaGap/Primer goldens were captured with the fix already in place, so no rework needed there)
- **Verification:** Full suite re-run green after the fix; `scripts/format-check-changed.sh staged` clean.
- **Committed in:** `70259bff` (Task 1 commit)

**3. [Rule 3 - Blocking] Escaped-string workaround for one golden containing a deliberate bare CR**
- **Found during:** Task 1, first `scripts/format-check-changed.sh staged` run
- **Issue:** The H3 whitespace fixture deliberately embeds a bare `\r` (not part of `\r\n`) inside `StrategyNotes`/`MetaNotes` to lock Analysis's current whitespace-collapse behavior. When that captured golden was stored as a raw string literal (containing the literal CR byte), the repo's LF-only format gate (`scripts/format-check-changed.sh`) correctly flagged it as an `ENDOFLINE` violation.
- **Fix:** `AnalysisGoldens.WhitespaceRequestContextText` uses a normal escaped C# string literal (with explicit `\r`/`\n`/`\t` escape sequences) instead of a raw string literal for this ONE constant — every other golden in all four `*Goldens.cs` files remains a raw string literal.
- **Files modified:** `AnalysisGoldens.cs`
- **Verification:** `scripts/format-check-changed.sh staged` passes with exit 0; the test still asserts the exact bare-CR behavior via the escape sequence.
- **Committed in:** `70259bff` (Task 1 commit)

---

**Total deviations:** 3 auto-fixed (1 missing-critical harness-correctness fix, 1 bug fix preventing a self-inflicted CI flake, 1 blocking format-gate workaround)
**Impact on plan:** All three were necessary for the harness to be trustworthy and CI-green; none changed test scope or weakened any assertion. No production service file was touched.

## Issues Encountered

None beyond the three items documented above as deviations.

## Scope Note (W2, per plan's `<output>` instruction)

`PacketArtifactStore.cs`'s zip-serialization duplication (REFACTOR-BACKLOG.md row 6 / STATE.md:86) remains EXPLICITLY DEFERRED — out of PKTSVC-01..04 scope. This plan's success criteria cover prompt-assembly and Scryfall-resolution byte-identity, not zip serialization. Nothing silently dropped; it remains recorded in `REFACTOR-BACKLOG.md` for a future phase's scope check.

## Known Stubs

None. Every fixture builds real, deterministic output through the actual service pipelines (real `ScryfallCardResolver`, real prompt-variant registries) — no hardcoded empty/placeholder values flow into any asserted field.

## Threat Flags

None. This plan introduces no new network endpoints, auth paths, file access patterns, or schema changes — it is a pure test-harness addition that invokes existing services through their existing test seams (real collaborators + deterministic override delegates, no live HTTP).

## Next Phase Readiness

- PKTSVC-04's guard EXISTS and is green on the unrefactored services with no coverage hole through the four mandated path-coverage requirements (versioned decklist, per-consumer collection-miss fallback, per-service commander reflag, flag combinations, whitespace normalization).
- Plans 83-02 through 83-07 (the actual collaborator extractions per 83-RESEARCH.md's sequencing) can now proceed: re-run `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~ByteIdentity"` after each migration step and treat ANY failure as a regression, not an expected diff.
- No blockers. No operator action required.

---
*Phase: 83-packet-service-srp-split*
*Plan: 01*
*Completed: 2026-07-04*

## Self-Check: PASSED

- FOUND: DeckFlow.Web.Tests/PacketByteIdentityFixtures.cs
- FOUND: DeckFlow.Web.Tests/DeckAnalysisByteIdentityTests.cs
- FOUND: DeckFlow.Web.Tests/DeckComparisonByteIdentityTests.cs
- FOUND: DeckFlow.Web.Tests/MetaGapByteIdentityTests.cs
- FOUND: DeckFlow.Web.Tests/DeckPrimerByteIdentityTests.cs
- FOUND: DeckFlow.Web.Tests/AnalysisGoldens.cs
- FOUND: DeckFlow.Web.Tests/ComparisonGoldens.cs
- FOUND: DeckFlow.Web.Tests/MetaGapGoldens.cs
- FOUND: DeckFlow.Web.Tests/PrimerGoldens.cs
- FOUND commit: 70259bff
- FOUND commit: 299e793f
