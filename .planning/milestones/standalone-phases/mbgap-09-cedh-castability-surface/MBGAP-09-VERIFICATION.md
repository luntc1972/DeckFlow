---
phase: mbgap-09-cedh-castability-surface
verified: 2026-07-13T23:59:00Z
status: passed
score: 26/26 must-haves verified
overrides_applied: 0
notes: |
  Human-verify checkpoint for the UI (Plan 06) was ALREADY approved by the user
  with two-viewport light+dark screenshots on 2026-07-13 — not re-flagged.
  Judged against shipped behavior including the mid-phase fix (32da92c6/8a9d0efc:
  SpellRequirement.IsInteractionSpell pre-permanent-gate signal) and the
  post-approval simplify refactor (ee0a86e0).
---

# Phase MBGAP-09: cEDH Castability Surface — Verification Report

**Phase Goal:** Ship the cEDH early-interaction lens surface — per-spell by-turn-3 holdable metric from the existing Monte-Carlo sim (no second engine), aggregated cEDH-only behind flag `analysis.manabase.cedh-interaction-lens` (seeded ON, kill switch = byte-identical off), surfaced in the UI (third lens, full cEDH castability table with holdable badge, worst-5 expander, caution empty state) and BOTH prompt artifacts, with mandatory help/README docs.
**Verified:** 2026-07-13 (branch `gsd/mbgap-09-cedh-castability-surface`, diff base `88817b62`, HEAD `ee0a86e0`)
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (merged from all 7 plan frontmatters)

| # | Plan | Truth | Status | Evidence |
|---|------|-------|--------|----------|
| 1 | 01 | CardCastability carries per-trial by-turn-3 holdable counter, additive default 0 (D-05) | ✓ VERIFIED | `ManabaseModels.cs:263` `public int ByTurn3HoldableTrials { get; init; }` |
| 2 | 01 | Simulator records per trial whether spell castable from untapped/online sources on ≥1 of turns 1-3 (D-06, D-07) | ✓ VERIFIED | `CastabilitySimulator.cs:1251-1254` — check runs before the `currentTurn < turn` early-exit via `BuildOnlineSourceView` + `ColorsCoverable`; accumulated at `:420`, returned at `:454` |
| 3 | 01 | ManabaseInteractionLens record + nullable InteractionLens slot on ManabaseReport exist (D-04, D-08) | ✓ VERIFIED | `ManabaseModels.cs:1401` (Row), `:1418` (Lens), `:1210` (nullable slot) |
| 4 | 02 | Qualifying spells = PlanRole.Interaction with effective MV ≤ 2 after cost overrides (D-01, D-02) | ✓ VERIFIED | `ManabaseAnalyzer.cs:294-296` — `(PlanRoles.HasFlag(Interaction) \|\| IsInteractionSpell) && spell.ManaValue <= 2`; ManaValue is post-ApplyCostOverrides, NOT OnCurveTurn. The `\|\| IsInteractionSpell` OR is the mid-phase fix (32da92c6) preserving D-01 intent for instants stripped by the plan-presence permanent gate; regression-locked (8a9d0efc, `PlanRoleClassifierTests.cs:185-187`) |
| 5 | 02 | Lens computed cEDH-only; Casual and flag-off return null (D-15) | ✓ VERIFIED | `ManabaseAnalyzer.cs:186` gate + `:258-260` ternary; XML doc `:143-147` states byte-identical; `ManabaseAnalyzerTests.cs` 18 InteractionLens references incl. Casual-null/flag-off-null |
| 6 | 02 | Headline N/M uses CedhSupportThreshold (88), never forked (D-08) | ✓ VERIFIED | `ManabaseAnalyzer.cs:259` passes `CedhSupportThreshold` const (`:17`); no literal 88 in ComputeInteractionLens |
| 7 | 02 | Zero qualifying spells → populated lens with QualifyingCount 0, not null (D-03) | ✓ VERIFIED | `ComputeInteractionLens` (`:278-315`) always returns a populated record; QualifyingCount = rows.Count |
| 8 | 03 | Report-text artifact gains "Early interaction (turns 1-3)" block with N/M + worst spells (D-14) | ✓ VERIFIED | `ManabaseReportTextBuilder.cs:335-357` — header, N/M at threshold line, worst-5 rows (`Rows.Take(DefaultVisibleRows)`) |
| 9 | 03 | Swap prompt generic cEDH prose upgraded with real N/M + worst spells (D-14) | ✓ VERIFIED | `ManabaseSwapPromptBuilder.cs:53-74` — three-way branch: null=original sentence verbatim, empty=no-cheap-interaction prose, populated=N/M + worst-3 names |
| 10 | 03 | Both builders byte-identical when interactionLens null (kill switch) | ✓ VERIFIED | Optional params default null (`ReportTextBuilder.cs:63`, `SwapPromptBuilder.cs:41`); appends gated on non-null (`:192`); test-locked (8 lens references each test file) |
| 11 | 03 | Block carries raw-availability caveat + informational-only disclaimer (D-07, D-13) | ✓ VERIFIED | `ReportTextBuilder.cs:355-356` — "assumes you hold mana open" + "First-pass read only - informational signal, not a recommendation." |
| 12 | 04 | Flag analysis.manabase.cedh-interaction-lens seeded ON in both dialects (D-15) | ✓ VERIFIED | `FeatureFlagStore.cs:230` Postgres `TRUE`, `:273` SQLite `1`; catalog description `FeatureFlagCatalog.cs:112` |
| 13 | 04 | Service reads flag fail-safe, threads interactionLens into Analyze, exposes ShowCedhInteractionLens (D-15) | ✓ VERIFIED | `ManabaseAnalysisService.cs:214` const, `:306` IsFlagOn read, `:396` Analyze arg, `:124` result prop set at BOTH assembly sites (`:347`, `:449`) |
| 14 | 04 | Swap-prompt call site fed report.InteractionLens (D-14) | ✓ VERIFIED | `ManabaseAnalysisService.cs:430` and `:436` — both build arms |
| 15 | 04 | Flag-off produces byte-identical output (kill switch) | ✓ VERIFIED | Analyzer nulls the lens flag-off (truth 5); builders byte-identical on null (truth 10); ShowCastability cEDH gate (truth 17); test-locked in ManabaseAnalysisServiceTests (23 refs) |
| 16 | 04 (review fix) | classifyPlanRoles widened so lens has role tags when plan-presence OFF (D-01) | ✓ VERIFIED | `ManabaseAnalysisService.cs:321` `classifyPlanRoles: showPlanPresence \|\| showCedhInteractionLens` |
| 17 | 05 | ShowCastability mode-aware: cEDH renders table only when lens flag on (D-09) | ✓ VERIFIED | `ManabaseViewModel.cs:114-117` — rows AND (Casual OR (Cedh AND ShowCedhInteractionLens)); 3-state gate test-locked (`ManabaseViewModelTests.cs`, 9 refs) |
| 18 | 05 | ManabaseViewModel exposes ShowCedhInteractionLens fed from analysis result (D-10) | ✓ VERIFIED | `ManabaseViewModel.cs:66`; controller `ManabaseController.cs:120` `ShowCedhInteractionLens = result.ShowCedhInteractionLens` |
| 19 | 05 | Display helpers: holdable badge (thresholded), caveat gloss, worst-5 cap (D-11, D-12) | ✓ VERIFIED | `ManabaseDisplay.cs:130` InteractionHoldableMarker(percent, threshold — 88 not re-hardcoded), `:42` CedhInteractionLensGloss, `:19` DefaultVisibleInteractionCount (single-sourced to `ManabaseInteractionLens.DefaultVisibleRows = 5`, `ManabaseModels.cs:1421`), `:256` InteractionSummaryText (L2 remainder disclosure) |
| 20 | 05 | Report-text call site fed the lens (D-14) | ✓ VERIFIED | `ManabaseController.cs:156` `interactionLens: result.Report.InteractionLens` — simplify refactor (ee0a86e0) dropped the redundant Show-guard; behavior-equivalent since analyzer nulls the lens unless flag+cEDH (matches swap-prompt idiom) |
| 21 | 06 | cEDH renders third "Early interaction" lens; single/dual states still work (D-10) | ✓ VERIFIED | `Manabase.cshtml:545-577` section `#manabase-early-interaction`; wrapper modifier switch `:245-250` (1→single, 3→triple, default 2-up); nav entry `:256`. Human-approved screenshots (4 shots, classic+nyx, desktop+mobile) |
| 22 | 06 | Lens shows worst-5 + `<details>` expander disclosing remainder count (D-11, L2) | ✓ VERIFIED | `Manabase.cshtml:561-569` — `View all N interaction rows (M more)` summary; rows worst-first from analyzer sort |
| 23 | 06 | cEDH flag-on renders full table with holdable badge on interaction rows only; mode-note fallback preserved (D-09, D-12, D-15) | ✓ VERIFIED | `Manabase.cshtml:274-311` — "Held up (T1-3)" header + per-row conditional badge cell with data-label kept on empty cells; mode-note string count = 1, inside `else if (Mode == Cedh)` behind ShowCastability (`:914-917`) — unreachable flag-on, renders flag-off |
| 24 | 06 | Zero qualifying spells renders caution-styled empty state (D-03) | ✓ VERIFIED | `Manabase.cshtml:550-553` — `manabase-lens-short` "⚠ no cheap interaction found"; section not gated off on empty |
| 25 | 06 | 3-up lens grid responsive, collapses on mobile; layout CSS in site-common.css only (D-10) | ✓ VERIFIED | `site-common.css:2996` `.manabase-twolens--triple`; `grep -c manabase-twolens site.css` = 0; mobile collapse confirmed by human-approved mobile screenshots |
| 26 | 07 | Help documents lens/definition/threshold/caveat; README behavior entry with flag+ON+cEDH-only+byte-identical-off (M12, D-15) | ✓ VERIFIED | `Help/manabase.md:113-126` — five-part subsection: mechanism, flag framing, qualifying definition + 88% headline + worst-5 disclosure + empty-state caution bullets, verbatim "assumes you hold mana open", informational-only scope disclaimer + formula-panel cross-ref; `README.md:802` full behavior entry |

**Score:** 26/26 truths verified

### Required Artifacts

| Artifact | Expected (`contains`) | Status | Details |
|----------|----------------------|--------|---------|
| `DeckFlow.Core/Manabase/ManabaseModels.cs` | ByTurn3HoldableTrials | ✓ VERIFIED | Line 263; + IsInteractionSpell (197), lens records (1401/1418), report slot (1210); all `{ get; init; }` (carve-out honored) |
| `DeckFlow.Core/Manabase/CastabilitySimulator.cs` | ByTurn3Holdable | ✓ VERIFIED | Per-trial out-flag, pre-early-exit evaluation, no new RNG draw; simplify pass kept semantics (ColorsCoverable subsumes mana pre-check) |
| `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` | ComputeInteractionLens | ✓ VERIFIED | Lines 278-315, wired at 258-260 |
| `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs` | "Early interaction" | ✓ VERIFIED | Lines 337, 335-357 |
| `DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs` | interactionLens | ✓ VERIFIED | Param line 41, three-way cEDH branch 55-74 |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` | analysis.manabase.cedh-interaction-lens | ✓ VERIFIED | TRUE (PG :230) + 1 (SQLite :273); conflict clauses untouched |
| `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` | CedhInteractionLensFlagKey | ✓ VERIFIED | Line 214; full threading verified |
| `DeckFlow.Web/Models/ManabaseViewModel.cs` | ShowCedhInteractionLens | ✓ VERIFIED | Line 66 + mode-aware ShowCastability 114-117 |
| `DeckFlow.Web/Models/ManabaseDisplay.cs` | InteractionHoldable | ✓ VERIFIED | InteractionHoldableMarker :130, gloss :42, count :19, summary :256 |
| `DeckFlow.Web/Views/Deck/Manabase.cshtml` | manabase-early-interaction | ✓ VERIFIED | Section :547, nav :256; formula panels covered (:974 "How the analysis works", :1016 "This deck's numbers" with plugged-in numbers); card names Razor-encoded (no Html.Raw introduced) |
| `DeckFlow.Web/wwwroot/css/site-common.css` | manabase-twolens--triple | ✓ VERIFIED | Line 2996; zero manabase-twolens in site.css |
| `DeckFlow.Web/Help/manabase.md` | assumes you hold mana open | ✓ VERIFIED | Line 124 verbatim |
| `README.md` | analysis.manabase.cedh-interaction-lens | ✓ VERIFIED | Line 802 |
| `DeckFlow.Web/e2e/manabase-interaction-lens.spec.ts` | Playwright spec | ✓ VERIFIED | Exists (6317 bytes); asserts cEDH presence, Casual absence (:135-141), Held-up column (:123-128), expander (:117). Documented deviation: relocated from plan's nonexistent `DeckFlow.Web.Tests/Playwright/` path |
| `.planning/ui-design/mbgap-09/screenshots/` | 2-viewport light+dark shots | ✓ VERIFIED | 4 PNGs: classic/nyx × desktop/mobile |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| CastabilitySimulator trial loop | CardCastability.ByTurn3HoldableTrials | per-trial 0/1 accumulation | ✓ WIRED | `:420` accumulate → `:454` return |
| ManabaseReport | ManabaseInteractionLens | nullable InteractionLens slot | ✓ WIRED | `ManabaseModels.cs:1210` |
| ManabaseAnalyzer.Analyze | ManabaseReport.InteractionLens | `interactionLensActive ? ComputeInteractionLens(...) : null` | ✓ WIRED | `:258-260` |
| ComputeInteractionLens | castability rows + deck.Spells | name join, PlanRole.Interaction filter | ✓ WIRED | `:284-296` (OrdinalIgnoreCase dict + OR-extended filter) |
| ManabaseReportTextBuilder.Build | AppendInteractionLensBlock | `if (interactionLens is not null)` | ✓ WIRED | `:192-194` |
| ManabaseSwapPromptBuilder.Build | interaction prose | real N/M when present, generic fallback when null | ✓ WIRED | `:55-74` |
| ManabaseAnalysisService | ManabaseAnalyzer.Analyze | `interactionLens: interactionLens` | ✓ WIRED | `:396` |
| ManabaseAnalysisService | SwapPromptBuilder.Build | `interactionLens: report.InteractionLens` | ✓ WIRED | `:430`, `:436` (both arms) |
| ManabaseController | ViewModel.ShowCedhInteractionLens | result assignment | ✓ WIRED | `:120` |
| ManabaseController | ReportTextBuilder.Build | `interactionLens:` argument | ✓ WIRED | `:156` (simplify: unguarded, behavior-equivalent) |
| Manabase.cshtml lens strip | report.InteractionLens | third section gated on Show + cEDH + non-null | ✓ WIRED | `:218`, `:545-577` |
| RenderCastabilityTable | lens qualifying set | holdable `<td data-label>` on interaction rows | ✓ WIRED | `:239` name map, `:303-311` conditional cell |
| Help subsection | Step 3 formula panels | cross-reference | ✓ WIRED | `manabase.md:126` "See **How the analysis works** and **This deck's numbers**" |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| Third lens section | report.InteractionLens | ComputeInteractionLens ← ByTurn3HoldableTrials ← Monte-Carlo trials | Yes — live e2e render showed 11/12 held up, Counterspell 74% worst (post-fix, per 06-SUMMARY + approved screenshots) | ✓ FLOWING |
| Castability table badge | interactionHoldableByName | derived from report.InteractionLens.Rows (`:239`) | Yes — Playwright asserts `%` content in the cell | ✓ FLOWING |
| Report text artifact | interactionLens param | controller ← result.Report.InteractionLens | Yes — test-locked populated content | ✓ FLOWING |
| Swap prompt artifact | interactionLens param | service ← report.InteractionLens | Yes — test-locked N/M + worst names | ✓ FLOWING |

Notably, the initial wiring WAS hollow in the live pipeline (plan-presence permanent gate stripped PlanRole.Interaction from all instants → 0/0 lens) — caught by the LEAD's live e2e run and fixed mid-phase via `SpellRequirement.IsInteractionSpell` (32da92c6) with regression lock (8a9d0efc). The shipped state produces real data.

### Requirements Coverage (D-01..D-15)

| Req | Description | Status | Evidence |
|-----|-------------|--------|----------|
| D-01 | Qualifying = PlanRole.Interaction, effective MV ≤ 2 | ✓ SATISFIED | Analyzer filter (truth 4) + classifyPlanRoles OR (truth 16) + IsInteractionSpell pre-gate fix |
| D-02 | Effective MV after override machinery, not reducers | ✓ SATISFIED | Filter reads post-ApplyCostOverrides ManaValue, never OnCurveTurn; reducer-exclusion test in ManabaseAnalyzerTests |
| D-03 | Empty state = caution warning, not hidden | ✓ SATISFIED | Populated lens at QualifyingCount 0 (truth 7) + view caution (truth 24) + artifact cautions (truths 8, 9) |
| D-04 | Per-spell rows (show the work) | ✓ SATISFIED | ManabaseInteractionRow list in lens, view rows, artifact rows |
| D-05 | Sim-based per-trial, no second engine | ✓ SATISFIED | Bookkeeping inside existing SimulateGame loop; no new RNG draw |
| D-06 | By-turn-3 holdable: ≥1 of turns 1-3, one number | ✓ SATISFIED | `currentTurn <= 3` OR-accumulation pre-early-exit |
| D-07 | Raw availability v1 + caveat | ✓ SATISFIED | Caveat verbatim in view (:571), report text (:355), gloss (:42), help (:124) |
| D-08 | N/M at CedhSupportThreshold (88), reused | ✓ SATISFIED | Constant passed through; threshold param on marker helper |
| D-09 | cEDH renders full castability table, note suppressed flag-on | ✓ SATISFIED | ShowCastability gate + mode-note fallback preserved once |
| D-10 | Third lens in header strip, responsive 3-up | ✓ SATISFIED | Section + triple modifier + single/dual states via switch |
| D-11 | Worst-5 + `<details>` view-all expander | ✓ SATISFIED | DefaultVisibleRows=5 single-sourced; remainder count disclosed (L2) |
| D-12 | Identical columns + holdable badge on interaction rows only | ✓ SATISFIED | Conditional cell, empty cells keep data-label; worst-first sort untouched |
| D-13 | Informational v1, verdict/health untouched | ✓ SATISFIED | No verdict/land-target changes in diff; disclaimers in artifact + help + README |
| D-14 | Lens joins BOTH prompt artifacts | ✓ SATISFIED | Report-text block + swap-prompt upgraded prose, both call sites fed |
| D-15 | One cEDH-only flag seeded ON, off = byte-identical | ✓ SATISFIED | Dual-dialect seed ON, fail-safe read, null-lens byte-identical test-locked end-to-end |

No orphaned requirements: all 15 IDs claimed across the 7 plan frontmatters.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | none | — | Scanned all added lines in the 88817b62..HEAD diff (25 non-planning files): zero TBD/FIXME/XXX/TODO/HACK/placeholder markers, no Html.Raw on card names, no forked 88 literal, no layout CSS in site.css |

### Behavioral Spot-Checks

Per verification instructions, builds/tests were NOT re-run; results cited from summaries (executed on Windows dotnet.exe during the phase):

| Behavior | Result | Status |
|----------|--------|--------|
| Core test suite post-fix | 1430 pass / 0 fail | ✓ PASS (cited, 06-SUMMARY) |
| Web test suite post-fix | 1383 pass / 0 fail (14 pre-existing skips) | ✓ PASS (cited, 06-SUMMARY) |
| Playwright interaction-lens spec | 6/6 (chromium-desktop + chromium-mobile) | ✓ PASS (cited, 06-SUMMARY) |
| Live cEDH render | 11/12 held up, Counterspell 74% worst | ✓ PASS (cited, 06-SUMMARY post-fix e2e) |
| Simplify refactor (ee0a86e0) regressions | Post-refactor state re-verified by grep/read in this verification: all truths hold on HEAD | ✓ PASS |

### Human Verification Required

None outstanding. The Plan 06 blocking human-verify checkpoint (3-up strip rendering, mobile collapse, dark-theme legibility, badge column, mode-note suppression) was **approved by the user on 2026-07-13** against the four captured screenshots (`.planning/ui-design/mbgap-09/screenshots/`). The simplify refactor after approval touched Razor structure (RenderInteractionRow local function) but is markup-equivalent per its commit message and was applied before HEAD test runs.

### Deviations Assessed (all acceptable, none scope-reducing)

1. **Playwright spec path** — `DeckFlow.Web/e2e/` instead of plan's `DeckFlow.Web.Tests/Playwright/` (plan defect: directory does not exist; repo convention is `e2e/`).
2. **IsInteractionSpell OR in the D-01 filter** — mid-phase fix widening qualification to the classifier's pre-permanent-gate interaction merit; without it the lens was 0/0 on real decks. Preserves D-01 intent, regression-locked.
3. **Controller passes `report.InteractionLens` unguarded** (ee0a86e0) instead of plan 05's Show-guarded ternary — behavior-equivalent (analyzer nulls unless flag+cEDH), matches the swap-prompt idiom.
4. **Worst-5 constant single-sourced** as `ManabaseInteractionLens.DefaultVisibleRows` with `ManabaseDisplay.DefaultVisibleInteractionCount` aliasing it — satisfies the plan 05 artifact contract.

### Gaps Summary

None. All 26 merged must-have truths, 15 artifacts, and 13 key links verified directly against the codebase at HEAD (`ee0a86e0`). The phase goal — sim-derived by-turn-3 holdable metric, cEDH-only flag-gated lens + full table + badge + worst-5 expander + caution empty state, both prompt artifacts, and mandatory docs — is observably achieved.

---

_Verified: 2026-07-13_
_Verifier: Claude (gsd-verifier)_
