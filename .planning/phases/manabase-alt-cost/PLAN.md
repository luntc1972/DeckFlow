---
slug: manabase-alt-cost
created: 2026-06-21
revised: 2026-06-21 (after Codex plan review — BLOCK resolved)
mode: phase
branch: feature/manabase-alt-cost
base: main@422dab0e
implementer: claude
reviewer: codex (gpt-5.4)
---

# Phase: Manabase Alt-Cost / Reduced-Cost Overrides

Castability under-rates cards whose real cost is below printed MV (free/pitch spells,
board-scaling self-reducers like Blasphemous Act, evoke/suspend). Add hybrid auto-detect +
a user-editable, pre-populated overrides box, applied as an EFFECTIVE MANA COST threaded
through every downstream consumer.

Design source: `.planning/captures/manabase-alt-cost-overrides.md`. Decisions locked:
hybrid · separate pre-populated box · all three categories · on main, in this worktree.

## Codex review resolutions (BLOCK → addressed)
- **HIGH-1/HIGH-2 (turn-only is insufficient):** the simulator's `effectiveCost` comes from
  `spell.ManaValue`+`Pips` and `BuildColorFindings` reads original pips — so altering only
  `EffectiveTurn` would NOT fix cast% or stop a free spell driving its color requirement.
  Resolution: the override produces an **effective `SpellRequirement`** (effective MV +
  effective pip map) that REPLACES the original in castability-row generation, the simulator
  call, AND color-finding aggregation. One substituted requirement, consumed everywhere.
- **HIGH-3 (evoke changes color, not just MV):** Grief `{2}{B}{B}` → evoke `{B}` keeps a
  colored pip at lower MV — a bare `name→int MV` model can't represent it. Resolution: the
  override value is an **effective mana cost string** parsed by the existing `ManaCost` parser
  → exact MV + pips. Free = `0`/empty (no pips), Blasphemous Act = `R`, Grief evoke = `B`.
- **MEDIUM (pip-drop heuristic too broad):** REMOVED. No "drop pips when MV<pips" rule —
  pips come solely from the parsed effective cost, so manual edits never silently strip color.
- **MEDIUM (name keying collisions, DFC/split):** key overrides by the RESOLVED card display
  name (post-Scryfall), `CardNormalizer` only as a fallback parse step; handle DFC/split/adventure.
- **MEDIUM/LOW (regex + tests):** suspend/evoke regexes tolerate `-`/`—`/line breaks and joined
  face text; add the missing interaction + round-trip + negative tests; result contract carries suggestions.

## Tasks

### Task 1 — Core: self-cost detection (Claude impl, Codex review)
Add `ManabaseClassifier.DetectSelfCost(CardFact) -> (string effectiveCost, string reason)?`,
sibling to `DetectCostReducer`. Returns an effective mana-cost STRING (parseable by `ManaCost`):
- Free / alt: "without paying its mana cost" / pitch wording → `"0"`.
- Scaling self-reduction: "costs {1} less to cast for each …" → the colored remainder, i.e.
  drop all generic, keep colored pips (Blasphemous Act `{8}{R}` → `"{R}"`).
- Evoke / suspend: parse `Evoke {cost}` / `Suspend N—{cost}` (tolerate `-`/`—`, line breaks,
  joined face text) → that cost string (Grief → `"{B}"`).
Null when nothing matches. Surface detected (cardName, effectiveCost, reason) on the report.
Tests: FoW→"0", Blasphemous Act→"{R}", Grief evoke→"{B}", a suspend card w/ em-dash, a deck-wide
reducer (Medallion) → null (negative control, must NOT be caught here).

### Task 2 — Core: effective-requirement substitution (Claude impl, Codex review)
Build an effective `SpellRequirement` from the override/detected cost (reuse `ManaCost.Parse`):
effective MV + effective Pips, preserving Name/Kinds/IsCommander/IsManaSource. Override key
match: resolved display name first, normalized fallback. Substitute it BEFORE the per-spell
pipeline so ALL three consumers see it:
- `EffectiveTurn` / `GenericReduction` (existing deck reducers still apply on top; override is the
  base requirement, deck reducers may lower further — `min`).
- `CastabilitySimulator.Simulate` — receives the effective MV + pips (effectiveCost/turn now
  derive from the substituted requirement, fixing HIGH-1).
- `BuildColorFindings` — iterates effective Pips, so a freed/recolored spell no longer drives
  its old color requirement (fixing HIGH-2).
Carry effective MV + an "overridden" flag onto `CardCastability` for display.
Tests: FoW "0" → ~99% AND drops blue from color findings; Blasphemous Act "{R}" → MV1 keeps {R}
(still a red driver, turn 1); Grief "{B}" → MV1 one black pip (not {2}{B}{B}); override + existing
reducer interaction (printed 5-drop + {1} reducer + override 3 → 3; override 4 → not worse than
reduced turn); no override → byte-identical to today.

### Task 3 — Web: overrides plumbing + suggestions contract (Claude impl, Codex review)
- `ManabaseAnalysisOptions.CostOverrides` = `IReadOnlyDictionary<string,string>` (name→cost string).
- `ManabaseRequest.CostOverridesText`; parse lines `Card Name: <cost>`. Storage/canonical format
  is fully BRACED (`0`, `{R}`, `{1}{R}`) because `ManaCost.Parse` only accepts braced symbols
  (verified — bare `R`/`1R` are NOT recognized). Add a small normalization layer that braces
  forgiving manual entry (`R`→`{R}`, `1R`→`{1}{R}`, bare integer `2`→`{2}`) BEFORE `ManaCost.Parse`;
  unparseable cost → ignore that line, never fatal. Detection (Task 1) emits the canonical braced form.
- `ManabaseAnalysisResult` gains `IReadOnlyList<(string Name, string Cost, string Reason)> Suggestions`
  (the result-contract surface Codex flagged).
- `ManabaseController` binds text → options; `ManabaseAnalysisService` passes overrides through and
  returns Suggestions. Pre-populate the box from Suggestions when the user supplied none; preserve
  the user's text verbatim on re-submit.
Tests (`DeckFlow.Web.Tests`): cost-text parser incl. braced (`{1}{R}`) AND shorthand-normalized
(`R`, `1R`, `2`) input, split/DFC name, bad line ignored; options→analyzer flow; "no user override
⇒ prepopulate from suggestions; user text present ⇒ preserve".

### Task 4 — View + UI (Claude impl, Codex review)
- `Manabase.cshtml`: "Reduced / alternative costs" textarea below the deck input, pre-filled from
  Suggestions after a run, editable, re-submit applies. CSS in `site-common.css` only.
- Castability rows: overridden MV shown with a marker (`1*`) + "alt/reduced cost" note.
- BUNDLED: verify pill centering (likely already fixed by 88724d84 — confirm at 1280 + 390;
  CSS-only change if a real defect remains).
Tests: Playwright `manabase.spec.ts` — box renders + pre-populates + applies (cast% changes);
overridden marker visible; desktop (1280) + mobile (390) across themes, no overflow.

## Files (ALLOWED SET — fence)
- `DeckFlow.Core/Manabase/ManabaseClassifier.cs` — DetectSelfCost + surface suggestions
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` — effective-requirement substitution across all 3 consumers
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` — consume effective requirement (if a signature tweak is needed)
- `DeckFlow.Core/Manabase/ManabaseModels.cs` — suggestion + overridden-flag fields
- `DeckFlow.Core/Manabase/ManaCost.cs` — reuse Parse (no change expected; verify it handles "0"/empty)
- `DeckFlow.Web/Models/ManabaseRequest.cs` — CostOverridesText
- `DeckFlow.Web/Models/ManabaseViewModel.cs` — suggestions + overrides round-trip
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — options plumbing + parser + Suggestions on result
- `DeckFlow.Web/Controllers/ManabaseController.cs` — bind + pass overrides
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` — overrides box + row marker
- `DeckFlow.Web/wwwroot/css/site-common.css` — box styling + pill-centering (if needed)
- `DeckFlow.Core.Tests/Manabase/ManabaseClassifierTests.cs`, `ManabaseAnalyzerTests.cs` — Core tests
- `DeckFlow.Web.Tests/*` — parser + plumbing + suggestions tests
- `DeckFlow.Web/e2e/manabase.spec.ts` — Playwright

## Constraints
- Override = effective mana COST in canonical BRACED form (`0`, `{R}`, `{1}{R}`); a normalization
  layer braces shorthand manual entry before `ManaCost.Parse` (which only accepts braced symbols).
- ONE substituted effective requirement consumed by EffectiveTurn + Simulate + BuildColorFindings.
- NO pip-drop heuristic; pips come from the parsed cost.
- Keep MV-0 consistent with shipped 0-cost handling (floor 1 mana / turn 1).
- Preserve carve-outs (init props, raw strings, switch exprs), LF endings, changed-lines format gate.
- Layout CSS in site-common.css only; never edit theme forks.

## Success criteria
- Force of Will override `0` → cast% ~99% AND it stops appearing as a blue source-driver.
- Blasphemous Act auto-detected → suggested `{R}`, box pre-filled; applying → MV1, keeps {R}.
- Grief evoke `{B}` → MV1 single black pip (not the printed {2}{B}{B}).
- Override + deck reducer interact correctly (min); overridden rows marked; user text round-trips.
- Build clean; Core + Web xUnit green; Playwright green desktop + mobile across themes.
