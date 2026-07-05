---
phase: 80-win-condition-combo-map
verified: 2026-07-02T19:08:00Z
status: deferred
deferral_note: "Deferred — Cycle 14 shipped (tag 2026.07.1) with this feature flag-OFF by default; user-facing UAT is owed at flag-flip time, tracked separately. Not a Cycle 15 blocker. Code-level verification passed (4/4 must-have truths); only the CI-green + live operator smoke gates remain, deferred to flag-flip."
score: 4/4 requirements verified (all must-have truths VERIFIED in code)
overrides_applied: 0
human_verification:
  - test: "Push branch plan/cycle-14-deck-eval-depth and confirm GitHub Actions CI is green"
    expected: "CI pipeline passes with the same 0/0 build and 1027/1137 test results reproduced locally in this verification"
    why_human: "Local build/test was run in this verification session and passed, but the branch has NOT been pushed (14 commits ahead of origin) — CI has never executed against this code; per CLAUDE.md this is the authoritative gate"
  - test: "Live operator smoke: flip analysis.wincon-map ON via /Admin/FeatureFlags in a running instance, load /deck-analysis Step-3 for a combo deck (e.g. Kiki-Jiki/Restoration Angel or Splinter Twin fixture) at desktop (1280px) and mobile (390px) widths across at least 2 themes, confirm the wincon-map readout renders (ranked combos, near-combo 'one card away' list, band, closing cards), download the zip and confirm 61-wincon-map.json is present and re-uploads correctly, then flip OFF and confirm the readout/hidden field/zip entry disappear"
    expected: "Visual readout matches the paste-artifact content; no layout breakage in any theme; zip round-trips; OFF state shows nothing new"
    why_human: "CSS rendering, cross-theme visual correctness, and the extended Playwright deck-analysis-render.spec.ts wincon-map assertions were written in this plan set but their execution was explicitly deferred by the executor to the operator (per SUMMARY.md 80-03, consistent with the project's no-browser-in-session convention)"
---

# Phase 80: Win-Condition & Combo Map Verification Report

**Phase Goal:** The player sees an enumerated, ranked win-condition / combo map — how the deck wins, with redundancy and a coarse assembly band — grounded in the already-fetched Commander Spellbook data, gracefully disclosing data-unavailable, and byte-identical when OFF.
**Verified:** 2026-07-02T19:08:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | WinConMapAggregator strictly separates Combos vs NearCombos, never merged | VERIFIED | `DeckFlow.Core/Analysis/WinConMapAggregator.cs:80-90` builds `nearCombosList` as a wholly independent list; `WinConMapAggregatorTests.cs:130` (`Compute_NearCombosAreStrictlySeparateFromCombos`) asserts a near-combo card never appears in `Combos` |
| 2 | Ranking = ManaValueNeeded asc (null last) → Popularity desc (null lowest) → ordinal joined-CardNames tie-break, input-order-independent | VERIFIED | `WinConMapAggregator.cs:67-72` (`.OrderBy(... ?? int.MaxValue).ThenByDescending(... ?? -1).ThenBy(string.Join("|", ...), StringComparer.Ordinal)`); `WinConMapAggregatorTests.cs:55` reversed-input tie-break test passes |
| 3 | Coarse bands (Early ≤4, Mid 5-7, Late ≥8, Unknown=null), never turn numbers; OverallBand = fastest combo's band | VERIFIED | `WinConMapAggregator.cs:97-115` (`BandFor`, named consts `EarlyBandMaxManaValue=4`, `MidBandMaxManaValue=7`, chained-if not switch); `WinConMapAggregatorTests.cs:69-96` theory + overall-band tests pass |
| 4 | AssemblyPathCount excludes near-combos (== included-combo count only) | VERIFIED | `WinConMapAggregator.cs:87` (`rankedCombos.Count`); test at line 116 |
| 5 | ComboDataAvailable sentinel distinguishes "unavailable" (false, ClosingCards still populated) from "ran, found none" (true+empty) | VERIFIED | `WinConMapAggregator.cs:49-61`; tests at 175/193 |
| 6 | Closing cards reuse `DeckStatClassifier.IsClosingPowerCard` (no fork) | VERIFIED | `WinConMapAggregator.cs:43` calls `DeckStatClassifier.IsClosingPowerCard(typeLine, oracleText)` directly; grep confirms no forked logic |
| 7 | Flag `analysis.wincon-map` seeded OFF in both dialects + catalog description | VERIFIED | `FeatureFlagStore.cs:231` (`('analysis.wincon-map', FALSE)`), `:269` (`('analysis.wincon-map', 0)`); `FeatureFlagCatalog.cs:84-87` description; `ToolFlagSeedConsistencyTests.AnalysisWinConMapFlag_SeededOff_InBothDialects` passes |
| 8 | Single combo fetch reused — no second FindCombosAsync/Scryfall call | VERIFIED | `grep -c "FindCombosAsync" DeckAnalysisPacketService.cs` = 1; gate widened at `:701` to `(scoreEnabled \|\| winConMapEnabled \|\| requiresComboLookup)` |
| 9 | Block renders in all 3 prompt variants (ChatGpt/Claude/Gemini) with NO shared helper | VERIFIED | Each of the 3 variant files has its own independent `if (!string.IsNullOrWhiteSpace(winConMapText))` guard (grep count = 1 per file, 3 total, no shared method); `WinConMapPromptParityTests.cs` 3-platform `[Theory]` passes |
| 10 | ShouldBypassPacketCache preserves command-zone bypass AND adds wincon-map bypass at both read+write sites | VERIFIED | `DeckAnalysisPacketService.cs:335-339` (predicate), `:369` (read-side), `:918` (write-side); `WinConMapCacheBypassTests.cs` proves no ON packet is cached/replayed AND command-zone bypass regression still holds |
| 11 | "combo data unavailable" vs "no combos found" disclosure, distinct wording | VERIFIED | `BuildWinConMapText` (`DeckAnalysisPacketService.cs:1448-1455`) — two distinct branches; Razor view (`DeckAnalysis.cshtml:654-661`) mirrors both branches |
| 12 | Step-3 readout matches paste-artifact content (combos, near-combos, band, closing cards, unavailable branch) | VERIFIED | `DeckAnalysis.cshtml:650-714`; `DeckAnalysisWinConMapViewTests.WinConMapPresent_RendersCombosNearCombosBandAndClosers` and `WinConMapUnavailable_RendersDataUnavailableNote` pass |
| 13 | WinConMapJson hidden-field round-trip (Request↔ViewModel↔Result) | VERIFIED | `DeckAnalysisRequest.cs:160-164`, `DeckAnalysisViewModel.cs:46`, `DeckAnalysisPacketService.cs:67/440-457/906`, `DeckPacketController.cs:168/185/364/381` |
| 14 | 61-wincon-map.json conditional zip entry (BuildZip writer + LoadFromZip restore), restored on re-upload | VERIFIED | `PacketArtifactStore.cs:41/122/143/284/296`; `WinConMapSurfaceContractTests.WinConMapSurfaceContract_DownloadUploadRoundTrip_RestoresJsonAndRematerializesMap` passes |
| 15 | Fresh-download serialize fallback when posted field empty — entry never dropped | VERIFIED | `DeckPacketController.cs:258/287` (serialize-fallback locals); `DeckPacketControllerTests.DeckAnalysisDownload_FreshWithEmptyPostedWinConMapJson_StillWritesZipEntryViaSerializeFallback` opens the real zip and asserts the entry exists |
| 16 | Untrusted-input deserialize hardening — never throws, deep structural validation | VERIFIED | `DeckAnalysisPacketService.cs:1626-1701` (`TryDeserializeWinConMap`/`IsStructurallyValidWinConMap` + 3 per-shape helpers + size/count caps); `WinConMapSurfaceContractTests` theory covers oversize/malformed/blank/over-cap/undefined-enum/tampered-count cases, all yield null without throwing |
| 17 | Flag-OFF dual-layer byte-identity (page via IRazorViewEngine excision + zip entry-map/per-platform) | VERIFIED | `DeckAnalysisWinConMapViewTests.WinConMapNull_MarkupEqualsPopulatedMinusWinConBlock` (prefix/suffix equality + whitespace-only middle); `WinConMapSurfaceContractTests.WinConMapSurfaceContract_FlagOffZip_ExcludesWinConMapEntryAndSentinel` + `_FlagOffPrompt_HasNoWinConMapBlock` |
| 18 | New CSS in site-common.css, no new :root tokens | VERIFIED | `grep -c ":root" site-common.css` = 1, unchanged from pre-Phase-80 baseline (`git show 5b4076b0:...` also = 1); no theme `site-*.css` file touched |

**Score:** 18/18 truths verified in code and tests

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Core/Analysis/WinConMap.cs` | Model + enum + input DTOs, zero Web reference | VERIFIED | Confirmed no `DeckFlow.Web`/`CommanderSpellbookResult` reference; all records/enum present exactly as specified |
| `DeckFlow.Core/Analysis/WinConMapAggregator.cs` | Compute() ranking/banding/separation/sentinel | VERIFIED | All behaviors match plan spec line-for-line |
| `DeckFlow.Core.Tests/WinConMapAggregatorTests.cs` | Golden fixtures for every behavior | VERIFIED | 18 tests, all pass (`dotnet test --filter WinConMapAggregator` → 18/18) |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` | Seed rows both dialects | VERIFIED | `('analysis.wincon-map', FALSE)` / `('analysis.wincon-map', 0)` present |
| `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` | Flag gate, widened combo gate, mapping, BuildWinConMapText, cache-bypass | VERIFIED | All wiring present and correctly ordered |
| `DeckFlow.Web.Tests/WinConMapPromptParityTests.cs` | 3-variant parity + null-path byte-identity | VERIFIED | 9 tests pass |
| `DeckFlow.Web.Tests/WinConMapCacheBypassTests.cs` | Cache-bypass regression | VERIFIED | 2 tests pass |
| `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` | WinConMap view-model property | VERIFIED | `{ get; init; }`, not get-only, not required |
| `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs` | 61-wincon-map.json writer/loader | VERIFIED | Conditional write, restore on load, not in required-entry guard |
| `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` | Flag-guarded readout + conditional hidden field | VERIFIED | Both present, guarded correctly |
| `DeckFlow.Web/wwwroot/css/site-common.css` | wincon-map layout classes | VERIFIED | Present, no new `:root` tokens |
| `DeckFlow.Web.Tests/WinConMapSurfaceContractTests.cs` | Artifact/zip byte-identity + hardening | VERIFIED | 23 test cases (per SUMMARY), spot-checked names match plan intent |
| `DeckFlow.Web.Tests/DeckAnalysisWinConMapViewTests.cs` | Page-level Razor excision-equality | VERIFIED | 4 tests, real `IRazorViewEngine` render, not a stub |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `WinConMapAggregator.Compute` | `DeckStatClassifier.IsClosingPowerCard` | per-card classification | WIRED | `WinConMapAggregator.cs:43` |
| `DeckAnalysisPacketService` combo gate | single `FindCombosAsync` task | widened boolean OR | WIRED | 1 call site total; gate includes `winConMapEnabled` |
| `DeckAnalysisPacketService.BuildAnalysisPrompt` | 3 prompt variants | `winConMapText` trailing param | WIRED | Threaded through registry + all 3 variants, each with independent guard |
| `TryComputeCacheKeyAsync` / `BuildAsync` write | `ShouldBypassPacketCache()` | shared predicate | WIRED | Both read (`:369`) and write (`:918`) sites use it |
| `DeckPacketController` Step-3 | `DeckAnalysisViewModel.WinConMap` + `request.WinConMapJson` | mapping + serialize | WIRED | Both Step-3 sites (2x) |
| `PacketArtifactStore.BuildZip`/`LoadFromZip` | `61-wincon-map.json` ↔ `request.WinConMapJson` | conditional write/restore | WIRED | Confirmed by round-trip test |
| `DeckAnalysisWinConMapViewTests` | `DeckAnalysis.cshtml` rendered output | `IRazorViewEngine` render + excision split | WIRED | Real render, not mocked |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|---------------------|--------|
| `DeckAnalysis.cshtml` wincon-map readout | `Model.WinConMap` | `DeckAnalysisPacketResult.WinConMap` ← `WinConMapAggregator.Compute(comboResult...)` ← live `FindCombosAsync` (Commander Spellbook API) or step-3 deserialize of round-tripped JSON | FLOWING | The map is computed from the actual fetched combo result (not a hardcoded stub); confirmed via `WinConMapCacheBypassTests` (real `BuildAsync` call renders block from a fake importer + fake combo service returning combo data) and the view test's `BuildWinConMap()` fixture used only for test isolation |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full solution builds clean | `dotnet.exe build DeckFlow.sln -c Debug` | 0 Warning(s), 0 Error(s) | PASS |
| Core win-con tests | `dotnet.exe test DeckFlow.Core.Tests --filter WinConMap` | 18/18 pass | PASS |
| Web win-con tests | `dotnet.exe test DeckFlow.Web.Tests --filter WinConMap` | 39/39 pass | PASS |
| Full Core suite | `dotnet.exe test DeckFlow.Core.Tests` | 1027/1027 pass | PASS — matches SUMMARY claim exactly |
| Full Web suite | `dotnet.exe test DeckFlow.Web.Tests` | 1137 pass / 12 skip (pre-existing PG) / 0 fail | PASS — matches SUMMARY claim exactly |
| Format gate | `scripts/format-check-changed.sh ci` | Clean, no violations | PASS |

### Probe Execution

Not applicable — this phase has no `scripts/*/tests/probe-*.sh` probes; verification used direct build/test execution instead (see Behavioral Spot-Checks).

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|--------------|--------|----------|
| WINCON-01 | 80-01, 80-02, 80-03 | Enumerated win-condition/combo map (combos + near-combos, assembly-path count) | SATISFIED | Aggregator + prompt block + Step-3 readout all present and tested |
| WINCON-02 | 80-01, 80-02 | Coarse assembly-band read grounded in `manaValueNeeded`; ranking by MV/popularity | SATISFIED | `BandFor` thresholds + ranking chain; note: `ManaValueNeeded`/`Popularity` parsing in `CommanderSpellbookService.cs` predates this phase (added in Cycle 8, commit `fb6166f2`) — Phase 80 is the first consumer to actually USE these previously-parsed-but-unconsumed fields, matching the "already-parsed-but-dropped" framing in REQUIREMENTS.md |
| WINCON-03 | 80-01, 80-02, 80-03 | "Combo data unavailable" disclosure distinct from "no combos"; closing cards for combo-less decks | SATISFIED | Sentinel in aggregator + distinct prose in `BuildWinConMapText` + distinct Razor branch |
| WINCON-04 | 80-02, 80-03 | Flag-gated, seeded OFF both dialects, byte-identical OFF (page+zip+3 artifacts), 3-variant parity, no shared helper | SATISFIED | Flag seeded OFF, both byte-identity layers proven by dedicated tests, ADR-0001 no-shared-helper confirmed by grep |

No orphaned requirements — REQUIREMENTS.md phase-80 mapping (WINCON-01..04) matches exactly what all three plans claim in their `requirements:` frontmatter.

### Anti-Patterns Found

None. Scanned all 16 files touched across the 3 plans for `TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER` and stub-return patterns — only pre-existing, unrelated HTML `placeholder=` attributes and one unrelated CSS `::placeholder` selector were found (not stubs).

### Human Verification Required

1. **Push branch + confirm CI green** — Local build (0/0) and full test suites (Core 1027/1027, Web 1137 pass/12 skip/0 fail) were reproduced in this verification session and match the SUMMARY.md claims exactly, but the branch is 14 commits ahead of `origin/plan/cycle-14-deck-eval-depth` (unpushed) — GitHub Actions CI, the project's authoritative gate, has never run against this code.
2. **Live operator visual smoke** — CSS/theme rendering correctness and the extended Playwright `deck-analysis-render.spec.ts` wincon-map assertions (desktop 1280px + mobile 390px, flag ON/OFF) were explicitly deferred to the operator by the executor (consistent with the project's no-browser-in-session convention). Automated evidence (Razor render test, CSS grep) is strong but does not substitute for an actual rendered-page visual check across themes.

### Gaps Summary

No code-level gaps found. All 18 derived observable truths across the three plans (80-01 Core aggregator, 80-02 Web/prompt integration, 80-03 UI surface + round-trip) are verified directly against the codebase — not merely claimed in SUMMARY.md. Every acceptance criterion in all three PLAN.md files was independently checked against the actual file contents (not just grep existence — logic, thresholds, guard placement, and test assertions were read in full). Full-suite test counts reproduced in this session match the SUMMARY.md claims exactly (Core 1027/1027, Web 1137/12-skip/0-fail), and the full solution builds with 0 warnings/0 errors.

The only open items are process/operational, not implementation gaps: the branch has not been pushed (so CI has not run) and live visual/theme verification has not been performed by a human. Per the decision tree, human-verification items present routes this to `human_needed` rather than `passed`, even though the score is 18/18.
