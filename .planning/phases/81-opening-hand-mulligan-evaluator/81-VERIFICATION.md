---
phase: 81-opening-hand-mulligan-evaluator
verified: 2026-07-03T23:58:00Z
status: human_needed
score: 6/6 must-haves verified (code-level); 2 items require operator action before close
overrides_applied: 0
human_verification:
  - test: "Push branch plan/cycle-14-deck-eval-depth (currently 66 commits ahead of origin, tip cc22b91b, includes phases 79/80/81) and confirm GitHub Actions CI is green."
    expected: "CI pipeline (build + full test suite + format-gate) passes on the pushed branch — the project's authoritative gate per CLAUDE.md (WSL VSTest is unreliable for local-only confidence)."
    why_human: "Requires a push + remote CI run; cannot be executed from this local verification pass. Local build (0/0) and local test run (Core 1052/1052, Web 1151/1163 with 12 pre-existing Postgres-integration skips) both pass, but CI is the authoritative gate per project convention."
  - test: "With analysis.mulligan-eval flipped ON in a running instance, visually inspect the /manabase page's 'Opening hand' lens card at desktop (1280px) and mobile (390px) widths across at least the default site theme and one guild theme (e.g. Azorius)."
    expected: "The card renders cleanly (no overlap/clipping), the keepable-band marker/pill, color/curve line, keep-size process line, and representative-opener list are legible and match the paste-artifact prose; with the flag OFF no trace of the card appears."
    why_human: "Visual layout/theme rendering cannot be verified via grep/build; the Playwright spec (manabase-mulligan.spec.ts) was validated with --list only (4 tests parse cleanly) but was not executed against a live server in this session, per the executor's own SUMMARY note (self-skips when Scryfall is unreachable in sandbox)."
---

# Phase 81: Opening-Hand / Mulligan Evaluator — Verification Report

**Phase Goal:** On the `/manabase` tool, the player sees a keepable opening-hand probability plus color/curve, mulligan-process, on-curve-castability, and "has-a-plan" reads — all read off the single existing Monte-Carlo London-mulligan pass, never contradicting the manabase tool's own numbers, framed as a consistency signal not advice.

**Verified:** 2026-07-03T23:58:00Z
**Status:** human_needed (all code-level truths VERIFIED; 2 operator-owed steps remain — push/CI-green and live visual smoke, per this phase's own explicit "owed" notes in 81-03-SUMMARY.md)
**Re-verification:** No — initial verification

This is a goal-backward, code-read verification (not a SUMMARY.md trust exercise). Every claim below was checked against the actual diff/file contents, and the full Core + Web test suites were re-executed locally as part of this pass — not merely re-quoted from the SUMMARYs.

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria, cross-checked against MULLIGAN-01..06 requirements)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | With `analysis.mulligan-eval` ON, `/manabase` (page + paste artifact) shows a keepable-hand probability as a discrete metric + a color/curve read | ✓ VERIFIED | Paste artifact: `AppendMulliganEvaluationBlock` (`DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs:250-277`) renders `mull.KeepableBand`/`mull.KeepableHandPercent` + `mull.ColorCount`/`mull.AverageManaValue`. On-page: `Manabase.cshtml:266-313` renders the same figures via `report.MulliganEvaluation`. `ManabaseReportTextBuilderMulliganTests.Build_WithMulliganEvaluation_ContainsBlockWithFiguresAndTrackedSpell` and `ManabaseViewRenderTests.OnState_MulliganFlagTrue_RendersOpeningHandLensCardWithTrackedSpell` both assert the exact figures render; both pass. |
| 2 | The evaluator shows the London-mulligan PROCESS (keep/mull-to-6/bottom decisions) + per-opener ON-CURVE CASTABILITY + a "has a plan" flag — evaluation, not advice | ✓ VERIFIED | `ManabaseMulliganEvaluation.Kept7Percent/MulliganTo6Percent/MulliganTo5Percent` + `RepresentativeOpeners` (each carrying `TrackedSpellName`, `TrackedOnCurveTurn`, `OnCurveCastable`, `HasPlan`) computed in `ManabaseAnalyzer.ComputeMulliganEvaluation` (`ManabaseAnalyzer.cs:887-938`); `OnCurveCastable` is `firstCastableTurn <= turn` from the SAME `SimulateGame` call the cast rate uses (`CastabilitySimulator.cs:318-341`) — genuine turn-by-turn castability, not a total-mana proxy. Wording never contains "keep this hand"/"mulligan this hand" — asserted by `Build_WithMulliganEvaluation_NeverContainsPrescriptiveKeepMullAdvice` (pass). |
| 3 | All reads reuse the single existing Monte-Carlo pass (no second `Simulate`, no re-fetch), reuse `LondonMulligan`/`ColorKeepCap` as the single "keepable" definition, surface a BAND not a false-precision %, and never contradict the tool's own keep/cast numbers | ✓ VERIFIED | `ComputeMulliganEvaluation` reads only the already-built `castability` rows — no `Simulate(` call anywhere in its body (grep-confirmed). Keep-size bucketing in `Simulate` uses `int keptSize = LondonMulligan(...)` captured **before** the `Math.Min` clamp (`CastabilitySimulator.cs:252-254`), matching the plan's explicit correctness constraint. `KeepableBand` is a 3-tier band (`high`/`medium`/`low`), never a raw decimal (`ManabaseAnalyzer.cs:900-905`). Golden tests `Analyze_MonoColorFixture_NeverContradictsCastRate_...` and `Analyze_ColorScrewedFixture_LowerKeepableHandPercent_ThanWellFixedFixture` (both pass) prove the keepable figure moves in the same direction as, and never disagrees with, the cast-rate figure under the same `ColorKeepCap` gate. `Analyze_MulliganEvaluation_AddsNoSimulateCallsBeyondThePerSpellCastabilityRows` structurally proves no extra `Simulate` pass (re-derives the figure from frozen rows via the pure `ComputeMulliganEvaluationForTest` seam and asserts byte-for-byte equality with the live figure). |
| 4 | Heuristic reads framed as a consistency signal with the keep criterion stated narrowly next to the number — never an authoritative verdict | ✓ VERIFIED | Paste artifact: `"...a heuristic consistency signal, not a strategic keep judgment."` + closing `"First-pass read only - verify against the actual hand; not a keep/mulligan recommendation."` (`ManabaseReportTextBuilder.cs:254,276`). On-page: `"...a consistency signal, not a keep verdict"` pill + closing note `"...a consistency signal, not a keep/mulligan recommendation."` (`Manabase.cshtml:277,312`). |
| 5 | Flag `analysis.mulligan-eval` seeded OFF in both dialects with a catalog description; flag-OFF page/artifact byte-identical to baseline (per-surface test); no sim internals leaked out of Core; CI green + format gate clean before close | ⚠ PARTIAL (code-level VERIFIED; CI-green step not yet run — see human_verification) | Seed rows confirmed: `('analysis.mulligan-eval', FALSE)` (Postgres) / `('analysis.mulligan-eval', 0)` (SQLite) in `FeatureFlagStore.cs:232,271`, inserted between `wincon-map` and `primer.stale-flag` exactly as specified; catalog description at `FeatureFlagCatalog.cs:92-96` ends "Off = byte-identical output." `LondonMulligan`/`DeckColorCount` remain `private` (grep-confirmed, no visibility widening) — the ROADMAP's flagged planning risk did not materialize. Byte-identity proven at BOTH surfaces: `Build_NullMulligan_OutputByteIdenticalToOverloadWithoutMulliganParam` (paste artifact) and `ManabaseViewRenderTests.OffState_IsByteIdenticalToOnWithMulliganCardExcised` (on-page, longest-common-prefix/suffix excision proving the OFF middle region is `string.Empty`) — both pass. Local build is 0 warnings/0 errors on the full solution; local `dotnet.exe test` on both Core.Tests (1052/1052) and Web.Tests (1151/1163, 12 pre-existing Postgres-integration skips) is 100% green; `scripts/format-check-changed.sh ci` exits 0. **What is NOT yet done:** the branch (`plan/cycle-14-deck-eval-depth`, tip `cc22b91b`) is 66 commits ahead of `origin/main` and has not been pushed, so GitHub Actions CI (the project's stated *authoritative* gate) has not run on this code. This is an explicit operator-owed step per the phase's own SUMMARY, not a code defect. |

**Score:** 5/5 roadmap success criteria code-verified; SC5's CI-green sub-clause is pending push (operator step, not a code gap).

### MULLIGAN-01..06 Requirement Cross-Check

| Requirement | Status | Evidence |
|---|---|---|
| MULLIGAN-01 (keepable % + color/curve read) | ✓ SATISFIED | See Truth #1 |
| MULLIGAN-02 (London mulligan PROCESS visible) | ✓ SATISFIED | See Truth #2 |
| MULLIGAN-03 (judged by ON-CURVE castability, not total mana) | ✓ SATISFIED | `OnCurveCastable = firstCastableTurn <= turn` derived from `SimulateGame`'s turn-by-turn land-drop simulation (`CastabilitySimulator.cs:327,592-768`), the same routine that drives the manabase tool's own cast%. |
| MULLIGAN-04 ("has a plan" evaluation flag, not turn-by-turn advisor) | ✓ SATISFIED | `HasPlan = stashedLands >= 2 && stashedColors >= planColorTarget && onCurveCastable` (`CastabilitySimulator.cs:328`) — composed condition, never "any non-land present"; rendered as "workable line"/"no clear line" labels only, never play-by-play instructions (grep-confirmed absence of turn-sequencing language in both surfaces). |
| MULLIGAN-05 (single sim pass; never contradicts tool's numbers) | ✓ SATISFIED | See Truth #3 |
| MULLIGAN-06 (flag-gated, seeded OFF both dialects, byte-identical OFF) | ✓ SATISFIED (code-level) | See Truth #5 |

### Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `DeckFlow.Core/Manabase/CastabilitySimulator.cs` | Two-stage pure-observation instrumentation, no rng draw, no second Simulate | ✓ VERIFIED | Stage 1 (lines 256-316, before `SimulateGame`) buckets by returned `keptSize`; Stage 2 (lines 323-342, after `SimulateGame`) builds the attributed sample from `firstCastableTurn`. No `rng.Next`/`NextDouble` added in either stage beyond the pre-existing partial-source roll (line 247, unrelated/pre-existing). |
| `DeckFlow.Core/Manabase/ManabaseModels.cs` | `OpeningHandSample` + `ManabaseMulliganEvaluation` + `CardCastability` additive fields, all `{ get; init; }` | ✓ VERIFIED | Lines 178-258 (CardCastability fields + OpeningHandSample) and 1119-1151 (ManabaseMulliganEvaluation) — all members `{ get; init; }`, matching the CLAUDE.md get-only carve-out. `ManabaseReport.MulliganEvaluation` additive, defaults null (line 967). |
| `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` | `ComputeMulliganEvaluation` aggregator, always computed, attached beside `TapAnalysis` | ✓ VERIFIED | `Analyze(...)` sets `MulliganEvaluation = ComputeMulliganEvaluation(...)` (line 187) unconditionally, mirroring `TapAnalysis` (line 184). |
| `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs` | Trailing `mulligan` param, null-guarded, hedged block | ✓ VERIFIED | Lines 44,154-167,250-277. |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` + `FeatureFlagStore.cs` | Seed OFF both dialects + description | ✓ VERIFIED | Confirmed above. |
| `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` | `MulliganEvalFlagKey` + fail-safe `IsFlagOn` read + `ShowMulliganEval` | ✓ VERIFIED | Lines 199,237,313; `IsFlagOn` uses `Snapshot().TryGetValue` (line 343-346), never `IsEnabled`. |
| `DeckFlow.Web/Controllers/ManabaseController.cs` | Download gates `mulligan:` on `ShowMulliganEval`; ViewModel mapping | ✓ VERIFIED | Line 132 (download gate), line 102 (ViewModel mapping). |
| `DeckFlow.Web/Models/ManabaseViewModel.cs` + `ManabaseDisplay.cs` | `ShowMulliganEval` flag + `KeepableMarker`/`AvgManaValueText` helpers | ✓ VERIFIED | `ManabaseViewModel.cs:54`; `ManabaseDisplay.cs:99-112`. |
| `DeckFlow.Web/Views/Deck/Manabase.cshtml` | Flag-guarded, one contiguous `manabase-mulliganlens` block, Razor `@` only | ✓ VERIFIED | Lines 266-314; no `Html.Raw` in the block (grep-confirmed). |
| `DeckFlow.Web/wwwroot/css/site-common.css` | Layout-only CSS, no new `:root` tokens, no theme fork | ✓ VERIFIED | `grep -c ":root"` = 1 (unchanged baseline); `git show d1b72048 --stat` touches only `Manabase.cshtml` + `site-common.css`, no `site-*.css` theme file. |
| Core test files (`CastabilitySimulatorMulliganTests.cs`, `ManabaseMulliganEvaluationTests.cs`, `ManabaseReportTextBuilderMulliganTests.cs`) | Meaningful, non-tautological coverage | ✓ VERIFIED | Read in full; assertions include a pinned cast%-byte-identity value, a directional same-seed singleton-vs-non-singleton proof, structural no-second-Simulate re-derivation, and prescriptive-language absence checks — not tautological self-checks. |
| `DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs` extensions | OFF no-markup + ON card + excision byte-identity | ✓ VERIFIED | Lines 67-122; excision test uses longest-common-prefix/suffix technique, asserts `offMiddle == string.Empty`. |
| `DeckFlow.Web/e2e/manabase-mulligan.spec.ts` | Desktop+mobile live-UX smoke | ✓ EXISTS, PARSES | `npx --no-install playwright test --list` returns 4 tests (2 assertions × 2 projects: chromium-desktop, chromium-mobile). Not executed live in this session (requires a running server + Scryfall reachability) — see human_verification. |

### Key Link Verification

| From | To | Via | Status | Details |
|---|---|---|---|---|
| `CastabilitySimulator.Simulate` trial loop | `LondonMulligan`'s returned keep value + tracked spell's `firstCastableTurn` | Two-stage capture | ✓ WIRED | Confirmed line-by-line above. |
| `ManabaseAnalyzer.ComputeMulliganEvaluation` | `IReadOnlyList<CardCastability>` rows (early low-MV first) | `OrderBy(ManaValue).ThenBy(OnCurveTurn)` + non-commander filter | ✓ WIRED | `ManabaseAnalyzer.cs:892-920`. |
| `ManabaseAnalysisService.AnalyzeAsync` | `ManabaseAnalysisResult.ShowMulliganEval` | `IsFlagOn(MulliganEvalFlagKey)` Snapshot read | ✓ WIRED | Confirmed. |
| `ManabaseController.Download` | `ManabaseReportTextBuilder.Build` mulligan arg | `result.ShowMulliganEval ? result.Report.MulliganEvaluation : null` | ✓ WIRED | Line 132. |
| `ManabaseController.Manabase` (analyze POST) | `ManabaseViewModel.ShowMulliganEval` | `ShowMulliganEval = result.ShowMulliganEval` | ✓ WIRED | Line 102. |
| `Manabase.cshtml` guard | opening-hand lens card | `@if (Model.ShowMulliganEval && Model.HasResult && report.MulliganEvaluation is { } mull)` | ✓ WIRED | Line 266, one contiguous block. |

### Data-Flow Trace (Level 4)

The on-page card and the paste artifact both render `report.MulliganEvaluation`, which is populated unconditionally by `ManabaseAnalyzer.Analyze` from the real per-spell `CastabilitySimulator.Simulate` output (20k trials each) — not a static/hardcoded stub. Traced: `Simulate` trial loop → `CardCastability.KeepableTrials/Kept7Trials/.../RepresentativeOpeners` → `ComputeMulliganEvaluation` aggregates over real rows → `ManabaseReport.MulliganEvaluation` → gated by `ShowMulliganEval` → rendered via `ManabaseReportTextBuilder`/`Manabase.cshtml`. No hardcoded empty returns or disconnected props found. **Status: FLOWING.**

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|---|---|---|---|
| Full solution builds clean | `dotnet.exe build DeckFlow.sln` | 0 Warnings, 0 Errors | ✓ PASS |
| Core mulligan tests | `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~Mulligan"` | 39/39 passed | ✓ PASS |
| Web mulligan/flag tests | `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~Mulligan\|FeatureFlagStoreSeed\|FeatureFlagCatalog"` | 52/52 passed | ✓ PASS |
| Full Core suite (regression check) | `dotnet.exe test DeckFlow.Core.Tests` | 1052/1052 passed | ✓ PASS |
| Full Web suite (regression check) | `dotnet.exe test DeckFlow.Web.Tests` | 1151/1163 passed, 12 pre-existing Postgres-integration skips | ✓ PASS |
| Format gate | `bash scripts/format-check-changed.sh ci` | exit 0 | ✓ PASS |
| Playwright spec parses | `npx --no-install playwright test --list e2e/manabase-mulligan.spec.ts` | 4 tests listed, no parse errors | ✓ PASS (list-only; not executed live) |

### Probe Execution

Not applicable — this is not a migration/tooling phase; no `scripts/*/tests/probe-*.sh` declared or referenced by the plans/SUMMARYs.

### Anti-Patterns Found

None. Scanned all 12 files modified across the 3 plans for `TODO`/`FIXME`/`XXX`/`HACK`/`PLACEHOLDER`/"not yet implemented"/"coming soon" — zero matches. No `Html.Raw` in the new card. No hardcoded empty-array/object stubs feeding the rendered figures (traced to real simulation output, see Data-Flow Trace). One pre-existing Codex-flagged issue (culture-dependent `ToString("F1")` on the on-page avg-MV, which would have drifted from the InvariantCulture paste artifact under a comma-decimal request culture) was found and fixed within this phase's own commit history (`cc22b91b`, pinned by a de-DE culture test) — already resolved, not an open gap.

### Human Verification Required

#### 1. Push + CI-green confirmation

**Test:** Push `plan/cycle-14-deck-eval-depth` (tip `cc22b91b`) to origin and confirm the GitHub Actions workflow passes.
**Expected:** CI build + full test suite + format-gate all green — this project's CLAUDE.md explicitly designates CI as the authoritative gate over local WSL test runs.
**Why human:** Requires a push action and waiting on a remote CI run; out of scope for a local, read-only verification pass. All local proxies (build 0/0, full local test suites 100% green, format-gate exit 0) already passed in this verification.

#### 2. Live visual smoke (desktop + mobile, themes)

**Test:** With `analysis.mulligan-eval` flipped ON via `/Admin/Flags` on a running instance, load `/manabase`, submit a deck, and visually inspect the "Opening hand" lens card at 1280px and 390px widths across the default theme and at least one guild theme (e.g. Azorius). Confirm no layout overlap/clipping and that flag-OFF shows no trace of the card.
**Expected:** Card renders cleanly at both breakpoints and in both themes; content matches the paste-artifact prose (keepable band, keep-size process, representative openers naming the tracked spell, has-a-plan read); flag OFF page is visually identical to pre-Phase-81 baseline.
**Why human:** CSS layout / theme visual rendering cannot be confirmed via source-code reading or the excision byte-identity test alone (which proves textual/DOM identity, not visual appearance). The Playwright spec exists and parses (4 tests) but was not executed against a live server in this verification session — this project's convention treats CI as authoritative for the Playwright run, but the *visual* review (does it look right) still requires a human per the project's UI-phase convention.

### Gaps Summary

No code-level gaps found. All 6 ROADMAP success criteria and all 6 MULLIGAN-01..06 requirements are implemented, wired end-to-end, and covered by non-tautological automated tests that pass locally (Core 1052/1052, Web 1151/1163). The instrumentation is provably pure-observation (no new rng draw, no second `Simulate` — proven structurally, not just claimed), the keep-size bucketing correctly handles the singleton free-mulligan edge case (proven by a directional same-seed test), byte-identity when the flag is OFF is proven at both the paste-artifact and on-page surfaces via null-guard and DOM-excision tests respectively, and a culture-format drift bug caught by Codex review during 81-03 was already fixed and pinned with a regression test.

The only open items are the two **operator-owed** steps the phase's own 81-03-SUMMARY.md explicitly calls out as outstanding: pushing the branch for CI confirmation, and a live visual smoke test across breakpoints/themes. Per this verification's explicit scope instructions, these are flagged as human-verification items (status `human_needed`), not treated as phase failures — the branch being unpushed and the live smoke not yet run do not indicate incomplete or broken implementation work.

---

*Verified: 2026-07-03T23:58:00Z*
*Verifier: Claude (gsd-verifier)*
