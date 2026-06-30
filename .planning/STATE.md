---
gsd_state_version: 1.0
milestone: Cycle 13
milestone_name: Deck Evaluation & Creator Output
status: executing
stopped_at: Completed 77-05-PLAN.md (score block rendered in /deck-analysis Step-3 — four-axis grid + bracket cross-check gated on Model.Score so OFF byte-identical; hidden ScoreJson round-trip field; score CSS confined to site-common.css with baked-hex band pills and no per-theme fork; IRazorViewEngine render test proves OFF byte-identity + ON grid; Web build 0/0; full Web suite 1011 pass / 12 skip / 0 fail).
last_updated: "2026-06-29T21:41:18.640Z"
last_activity: 2026-06-29 -- Phase 78 execution started
progress:
  total_phases: 4
  completed_phases: 3
  total_plans: 19
  completed_plans: 17
  percent: 75
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 78 — auto-refreshing-primer

## Current Position

Phase: 78 (auto-refreshing-primer) — EXECUTING
Plan: 1 of 3
Status: Executing Phase 78
Last activity: 2026-06-29 -- Phase 78 execution started

Progress: [█████████░] 94%

## Roadmap Summary

| # | Phase | Requirements | Status |
|---|-------|-------------|--------|
| 75 | Tap Analyzer Surface | TAP-01, TAP-02, TAP-03, TAP-04 | Complete (4/4 plans) |
| 76 | Bracket Classifier + Balancer | BRACKET-01, BRACKET-02, BRACKET-03, BRACKET-04, BRACKET-05 | Not started |
| 77 | Multi-Axis Deck Score | SCORE-01, SCORE-02, SCORE-03, SCORE-04 | Executing (5/6 plans) |
| 78 | Auto-Refreshing Primer | PRIMER-01, PRIMER-02, PRIMER-03, PRIMER-04 | Not started |

**Phase ordering rationale:**

- **75 first**: independent, zero risk, additive counters only; validates additive-field discipline before bracket work
- **76 second**: headline differentiator; produces `GameChangerCatalog` that gates Phase 77's Power axis
- **77 third**: depends on `GameChangerCatalog` from 76; folds score into existing analysis packet (no new tile)
- **78 last**: independent of 75-77; thin new work over existing `DiffEngine`/`TryComputeCacheKeyAsync` primitives

## Performance Metrics

**Velocity (Cycle 12 reference — most recent shipped):**

- Phases 70-74 + flag-key namespacing; build 0/0; Web suite 929 pass
- Claude (Opus 4.8) implements, Codex reviews (rule effective 2026-06-27)

## Accumulated Context

### Key Decisions (Cycle 13)

- **Granularity = coarse:** 4 phases is the natural minimum — one per coherent capability.
- **Tap Analyzer (Phase 75):** additive counters ONLY inside the existing 20k-trial loop; `{ get; init; }` fields only (never `required`); flag `analysis.manabase.tap-analyzer` seeded OFF.
- **Phase 75 Wave 0 (plan 75-01):** type surface + full RED xUnit suite landed before any computation; `TapMarker` (flat 80%, D4) implemented as a pure helper (GREEN); computation lands in 75-02, flag/view wiring in 75-03.
- **Phase 75 Wave 2 (plan 75-03):** flag `analysis.manabase.tap-analyzer` registered + seeded OFF in BOTH dialects (idempotent `ON CONFLICT DO NOTHING`); read fail-safe-OFF via `IsFlagOn` and threaded service→result→controller→download; download passes `tap` only when ON (TAP-04 byte-identity when OFF). 75-04 page UI card still pending.
- **Phase 75 Wave 3 (plan 75-04) — DONE, phase complete:** flag-guarded "Untapped sources" card on /manabase (reuses `.manabase-lens` chrome) + two layout-only CSS classes (`.manabase-taplens`, `.manabase-taplens-split`, 640px collapse) in `site-common.css` (no theme-fork edit); per-color list gated on `ColorFindings.Count > 1`, cEDH renders full card (D2), ✓/⚠ via flat-80% `TapMarker` (D4). Entire card inside `@if` → byte-identical when OFF, enforced by an `IRazorViewEngine` OFF/ON render test (a source-text scan can't discriminate). **Deviation:** dropped the planned `report?.TapAnalysis` null-conditional for `report.TapAnalysis` (report non-null under the outer result guard) to avoid a new CS8602 warning. Human-verify APPROVED (Classic/Azorius/Nyx + mobile; screenshots under `.planning/ui-design/cycle13/screenshots/`). TAP-01/02/04 complete; flag still seeded OFF in prod.
- **Game Changers data (Phase 76):** versioned seed file + `IMemoryCache`, NOT a `.cs` literal; existing `CommanderBracketCatalog.cs` hardcoded data migrated to Core; tutors are NOT a bracket gate (Oct-2025 WotC change).
- **Bracket surface (Phase 76):** flag `tool.bracket.enabled` seeded OFF; Spellbook null = disclosed in artifact, never silent "zero combos".
- **Multi-Axis Score (Phase 77):** folds into existing `/deck-analysis` paste packet; no new tool tile; all 3 prompt variants hand-edited per ADR-0001; no shared helper (shared helpers have been reverted before).
- **Auto-Refreshing Primer (Phase 78):** stale-FLAG only (no auto-rebuild, no silent re-fetch); canonical name+quantity multiset hash as the staleness key (not raw textarea text).
- **ADR-0001 holds:** every new artifact section (bracket, score, stale-banner if in prompt) must be hand-edited into all 3 variants — ChatGpt/Claude/Gemini — with a parity test.
- **Phase 77 (plan 77-05) — DONE:** the on-page render of `Model.Score`. Score block inserted in `/deck-analysis` Step-3 (between `<h3>Analysis Summary</h3>` and the per-category `<div class="stack">`) gated on `@if (Model.Score is not null)` — server-computed init-only, never form-bound — so the flag-OFF/absent path is byte-identical. Four `.chatgpt-score-card`s (Power/Speed/Control/Consistency) each encode the band **4 redundant ways** (numeral, `aria-hidden` 5-pip meter, word pill, baked-hex color) + `role="group"` and a full `aria-label="@axis score: @band of 5, @BandLabel"` (never color-only, UI-SPEC §9). Bracket cross-check note `--agree`/`--diverge` with a leading `✓`/`⚠` glyph + class + text, `role="note"`. Hidden `<textarea name="ScoreJson" hidden>` beside `DeckProfileJson`, gated on `Request.ScoreJson` non-empty, round-trips the score across the Step-3 re-post without adding a field on the OFF page. All score values render through auto-encoding Razor `@`-expressions (no `@Html.Raw`, T-77-05-01). **All CSS in `site-common.css` ONLY** (responsive 4->2->1 grid, baked-hex `.chatgpt-score-band--0..--5` pills that carry their own legible ink so NO per-theme fork is needed — `grep -c chatgpt-score site.css` == 0, Pitfall 7); cross-check borrows `var(--success)`/`var(--gold-warning)` left-border. OFF byte-identity proven by an `IRazorViewEngine` render test (`DeckAnalysisScoreViewTests`) asserting prefix+suffix exact-equality around the **excised** score block after neutralizing the per-render antiforgery token — not mere class-string absence (Codex MED). Web build 0/0; full Web suite **1011 pass / 12 skip / 0 fail**. Commits `3ffcbd61` (view), `34b0fc4e` (CSS), `c7a77778` (test). Deviations (Rule 3): render harness uses `DeckPacketController` (no `DeckController` exists) + registers `IOptions<AiPlatformOptions>` for `_AiSelector`; antiforgery token neutralized before byte-compare (Rule 1). 77-06 (README + theme/mobile human-verify) still pending.
- **Phase 77 (plan 77-04) — DONE:** end-to-end score wiring behind `analysis.multi-axis-score` (seeded OFF in BOTH dialects, idempotent `ON CONFLICT DO NOTHING`; lockstep seed+catalog guards updated). `BuildAsync` reads the flag via the explicit `_flagCache.Snapshot().TryGetValue` default-OFF pattern (never `IsEnabled`), computes `DeckStatAggregator.Compute` (current-deck non-commander refs) + `BracketClassifier.Classify` + `MultiAxisScorer.Score`, and builds the paste-safe ASCII `BuildScoreBlockText` (UI-SPEC §10) threaded into all 3 variants via the 77-03 `scoreBlockText` param. **ONE combo fetch reused** (widened the single `comboTask` gate to `scoreEnabled || RequiresComboLookup`; `grep -c FindCombosAsync` unchanged — no second `comboForScoreTask`, Codex HIGH avoided); the prompt receives `promptComboResult = RequiresComboLookup ? comboResult : null` and the Spellbook timing row stays gated so OFF output is byte-identical. `comboDetectionAvailable = comboResult is not null` threaded (Pitfall 1). `ScoreJson` Step-3 round-trip is untrusted-input hardened: length-capped (8192) typed deserialize in try/catch -> null (`TryDeserializeScore`, threat T-77-04-01). `IGameChangerCatalogService` injected (DI + TestServiceFactory + DiComposition test registered). `DeckAnalysisViewModel.Score` surfaced + controller serializes `request.ScoreJson` (render lands in 77-05 — KNOWN STUB: Score populated, not yet rendered in `DeckAnalysis.cshtml`). Web build 0/0; full Web suite **1008 pass / 12 skip / 0 fail** (entire byte-identity suite green proves OFF path unchanged). Commits `6203ee9e` (flag), `6429771d` (models), `59cc42c3` (wiring+tests). SCORE-01..04 marked complete. Deviation: persistence tests as a `partial` of `DeckAnalysisPacketServiceTests` to reuse the `CreateService` fake graph (DRY); catalog registered in 2 hand-rolled test compositions (Rule 3 blocking).
- **Phase 77 (plan 77-03) — DONE:** `string? scoreBlockText = null` threaded as the last trailing optional param through `IAnalysisPromptVariant.Build`, `AnalysisPromptVariantRegistry.Build`, and all three concrete variants. Each variant hand-inserts its own guard `if (!IsNullOrWhiteSpace(scoreBlockText)) { AppendLine(); AppendLine(scoreBlockText); }` at its own position (ChatGPT/Gemini after `## DECK CONTEXT`, before `## EVIDENCE RULES`; Claude after the `<commander>` block) — ADR-0001, NO shared helper. Variants do NOT build the text; the caller (77-04) supplies the pre-built string, so the param defaults to null and the OFF path is byte-identical today. `AnalysisScorePromptParityTests` (9 tests = 3×3 platforms): present / OFF-path **excision byte-identity** (not marker-absence, per Codex HIGH) / all-four-axis figures-match. Deviation: updated `StubTestAnalysisVariant` (AiPlatformExtensionTests) for the new param (Rule 3 blocking). Web build 0/0; full Web suite 995 pass / 12 skip / 0 fail. SCORE-04 prompt-threading done; actual score wiring (`BuildScoreBlockText` + `BuildAnalysisPrompt` call) lands in 77-04.
- **Phase 77 Wave 1 (plan 77-02) — DONE:** the deterministic scorer heart. `DeckMultiAxisScore` + `DeckScoreRationale` sealed records (positional, XML doc) and a pure static `MultiAxisScorer.Score(stats, gameChangerCount, twoCardComboCount, comboDetectionAvailable, bracketNumber)` guarded by `ArgumentNullException.ThrowIfNull`. Four axes via **chained-if threshold gates** (NOT switch — re-indent carve-out): Power GC-dominant + combo/fast-mana modifiers; Speed avg-MV-driven + fast-mana + ramp/draw-under-3; Control interaction + wipes + counters; Consistency tutors + combo redundancy + curve smoothness. `BandLabel` is the ONLY switch expression (None/Low/Modest/Moderate/High/Extreme, `>5` clamps). Every band `Math.Clamp(0,5)` — no decimals (SCORE-01). Combo-unavailable discloses `combo data unavailable` (never `0 combos`). Cross-check misaligns only on gross contradiction (Power>=4 & bracket<=2, or Power<=1 & bracket>=4) with ASCII ` - ` divergence copy. Rationale strings ASCII + InvariantCulture decimals (locale-deterministic, paste-safe). 18 `MultiAxisScorerTests` GREEN on FIRST calibration (golden cEDH Power/Speed>=4 vs battlecruiser<=2, Control>=4, Consistency>=4, BandLabel theory, combo-disclosure, cross-check align/diverge, null guard) — no cutpoint retuning needed. Core build 0/0; full Core suite 944 pass (+18). Commits `5a2bda0b` feat, `3f0b27d4` test. SCORE-02 band derivation done; packet wiring (`BuildScoreBlockText` + `BuildAnalysisPrompt` call + flag) still lands in 77-04.
- **Phase 77 Wave 0 (plan 77-01) — DONE:** four deck signals added to Core — `DeckStatClassifier.IsTutorCard/IsFastManaCard/IsRampOrDrawUnderThreeMv/IsCounterspellCard` predicates + `DeckStatSummary.Tutors/FastMana/RampDrawUnderThreeMv/Counters` as additive `{ get; init; }` fields (never positional, never `required`), quantity-weighted in `DeckStatAggregator.Compute`. Land-fetch ramp excluded from tutor count (basic land / land card / land onto the battlefield); MV>=1 rocks (Sol Ring) excluded from fast mana. Core build 0/0; 926 Core tests pass. SCORE-02 NOT yet complete — it spans plans 01/02/04 (signal derivation lands in 77-02).

### Key Pitfalls to Watch

- **Do NOT gate bracket on tutor count** (Oct-2025 WotC rule change explicitly removed tutor gates).
- **Do NOT rebuild the CastabilitySimulator for Tap Analyzer** — accumulate counters in the existing loop only.
- **Do NOT extract shared prompt text** across the 3 prompt variants — ADR-0001 forbids it and reverts have already happened.
- **Do NOT auto-rebuild the primer on stale detection** — stale flag only; explicit user regenerate.
- **Spellbook null must be disclosed**, not silently treated as "zero combos."

### Pending Todos

None for Cycle 13 yet.

### Blockers/Concerns

- **Phase 77 Control axis classifier approach** (oracle-text heuristics vs category-knowledge labels vs hybrid) is unresolved — decide in Phase 77 planning.
- **Phase 76 B3 "early-game combo" timing threshold** is a prose WotC definition, not a crisp turn number; resolution is to disclose the approximation in the artifact — confirm this is acceptable at Phase 76 planning.

### Carry-Forward (still open from prior cycles)

| Item | Status |
|------|--------|
| Operator owed: flip Cycle 12 manabase/analysis flags in prod | Operator task |
| Operator owed: manual prod deploy (autodeploy OFF since 2026-06-27) | Operator task |
| `deckflow_admin` credential deletion (password rotated) | Operator task |
| Full dual-dialect branch collapse (PG DDL parity prereq) | Backlog |
| SEO/growth lane (SEO-01..05) | Deferred |
| Scheduled/bulk harvest (AUTO-03/04) | Deferred |
| Phase 75 P01 | 30min | 3 tasks | 11 files |
| Phase 75 P03 | ~20min | 3 tasks | 7 files |
| Phase 77 P01 | ~5 min | 2 tasks | 4 files |
| Phase 77 P02 | ~10 min | 2 tasks | 3 files |
| Phase 77 P03 | ~12 min | 2 tasks | 6 files |
| Phase 77 P04 | ~40 min | 3 tasks | 14 files |
| Phase 77 P04 | 40min | 3 tasks | 14 files |
| Phase 77 P05 | ~25min | 3 tasks | 3 files |

## Session Continuity

Last session: 2026-06-29T18:18:12.912Z
Stopped at: Completed 77-05-PLAN.md (score block rendered in /deck-analysis Step-3 — four-axis grid + bracket cross-check gated on Model.Score so OFF byte-identical; hidden ScoreJson round-trip field; score CSS confined to site-common.css with baked-hex band pills and no per-theme fork; IRazorViewEngine render test proves OFF byte-identity + ON grid; Web build 0/0; full Web suite 1011 pass / 12 skip / 0 fail).
Resume: `/gsd:execute-phase 77` to run the remaining Phase 77 plan (77-06 README + theme/mobile human-verify).
