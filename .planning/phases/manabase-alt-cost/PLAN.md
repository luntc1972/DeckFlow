---
slug: manabase-alt-cost
created: 2026-06-21
mode: phase
branch: feature/manabase-alt-cost
base: main@422dab0e
implementer: claude
reviewer: codex (gpt-5.4)
---

# Phase: Manabase Alt-Cost / Reduced-Cost Overrides

Castability under-rates cards whose real cost is below printed MV (free/pitch spells,
board-scaling self-reducers like Blasphemous Act, evoke/suspend). Add hybrid auto-detect +
a user-editable, pre-populated overrides box, applied through the existing cost-reduction seam.

Design source: `.planning/captures/manabase-alt-cost-overrides.md`. Decisions locked:
hybrid · separate pre-populated box · all three categories · on main, in this worktree.

## Tasks

### Task 1 — Core: self-cost detection (Claude impl, Codex review)
Add `ManabaseClassifier.DetectSelfCost(CardFact) -> (int suggestedMv, string reason)?`,
sibling to existing `DetectCostReducer`. Three categories from oracle text / type line:
- Free / alt: "without paying its mana cost", common pitch wording → suggest 0.
- Scaling self-reduction: "costs {1} less to cast for each …" → suggest floor = colored pip
  count (Blasphemous Act {8}{R} → 1). Distinct from DetectCostReducer (which discounts OTHER
  spells).
- Evoke / suspend: "Evoke {cost}", "Suspend N—{cost}" → suggest that alt cost's MV.
Return null when nothing matches. Expose detected suggestions on the deck/report so the Web
layer can pre-fill the box.
Tests: `ManabaseClassifierTests` — FoW→0, Blasphemous Act→1, an evoke card→alt, a plain card→null.

### Task 2 — Core: apply overrides in the analyzer (Claude impl, Codex review)
Thread `IReadOnlyDictionary<string,int> costOverrides` into `ManabaseAnalyzer.Analyze`.
In `EffectiveTurn`: when an override exists for the spell, it wins (`min(override, computed)`).
When target MV < colored pip count, also drop the colored requirement so a free spell routes
exactly like a true 0-cost colorless card (consistency with the just-shipped 0-cost fix:
floor stays at "1 mana, turn 1"). Carry the effective MV + an "overridden" flag onto
`CardCastability` for display.
Tests: override lowers on-curve turn; MV<pips clears pips (cast% jumps); MV≥pips keeps pip
(Blasphemous Act→1 still needs {R}); no override = unchanged.

### Task 3 — Web: overrides plumbing (Claude impl, Codex review)
- `ManabaseAnalysisOptions.CostOverrides` (name→MV).
- `ManabaseRequest` gains a `CostOverridesText` string; parse lines `Card Name: N`
  (tolerant of spacing/case; normalize names with the existing CardNormalizer).
- `ManabaseController` → `ManabaseAnalysisService.AnalyzeAsync` passes overrides through.
- After analysis, surface detected suggestions (Task 1) so the box can pre-populate when the
  user has not supplied their own. Round-trip the user's text on re-submit.
Tests (`DeckFlow.Web.Tests`): overrides-text parser; options flow into the analyzer;
suggestions surfaced; malformed lines ignored, not fatal.

### Task 4 — View + UI (Claude impl, Codex review)
- `Manabase.cshtml`: "Reduced / alternative costs" textarea below the deck input,
  pre-filled with detected suggestions after a run, editable, re-submit applies. CSS in
  `site-common.css` only (no theme forks).
- Castability rows: overridden MV shown with a marker (e.g. `1*`) + an "alt/reduced cost"
  note — preserves "show the work".
- BUNDLED: verify pill centering (likely already fixed by 88724d84 — confirm at 1280 + 390;
  only change CSS if a real defect remains; see capture note).
Tests: Playwright `manabase.spec.ts` — box renders + pre-populates + applies (cast% changes);
overridden marker visible; verify desktop (1280) + mobile (390) across themes, no overflow.

## Files (ALLOWED SET — fence)
- `DeckFlow.Core/Manabase/ManabaseClassifier.cs` — DetectSelfCost + surface suggestions
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` — apply overrides in EffectiveTurn + pip-drop
- `DeckFlow.Core/Manabase/ManabaseModels.cs` — suggestion + overridden-flag fields
- `DeckFlow.Web/Models/ManabaseRequest.cs` — CostOverridesText
- `DeckFlow.Web/Models/ManabaseViewModel.cs` — suggestions + overrides round-trip
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — options plumbing + parser
- `DeckFlow.Web/Controllers/ManabaseController.cs` — bind + pass overrides
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` — overrides box + row marker
- `DeckFlow.Web/wwwroot/css/site-common.css` — box styling + pill-centering (if needed)
- `DeckFlow.Core.Tests/Manabase/ManabaseClassifierTests.cs`, `ManabaseAnalyzerTests.cs` — Core tests
- `DeckFlow.Web.Tests/*` — parser + plumbing tests
- `DeckFlow.Web/e2e/manabase.spec.ts` — Playwright

## Constraints
- Reuse the existing cost seam (EffectiveTurn/GenericReduction) — do not fork a new free-spell path.
- Keep MV-override consistent with the shipped 0-cost handling (floor 1 mana / turn 1).
- Preserve carve-outs (init props, raw strings, switch exprs), LF endings, changed-lines format gate.
- Layout CSS in site-common.css only; never edit theme forks.

## Success criteria
- Force of Will with override 0 → castability rises to ~99% (routes like a 0-cost card).
- Blasphemous Act detected, suggested 1, box pre-filled; applying → cast% reflects MV 1 ({R}).
- Overridden rows visibly marked; suggestions pre-populate; user text round-trips.
- Build clean; Core + Web xUnit green; Playwright green desktop + mobile across themes.
