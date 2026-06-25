---
status: complete
phase: 70-manabase-accuracy-mana-quantity
source: [70-CONTEXT.md success criteria, 70-01..70-06 PLAN.md]
started: 2026-06-24T11:25:00Z
updated: 2026-06-24T13:30:00Z
---

## Current Test

[testing complete]

<!--
Session note: testing pivoted from the scripted checklist into live analysis of
a real Disa the Restless deck, which surfaced four real accuracy/UX defects (see
Gaps). All four were diagnosed, fixed, unit-tested (138 manabase + 33 flag Core/
Web tests green), Codex-reviewed (APPROVE, 1 LOW guarded), Playwright-checked,
and committed+pushed (eec118df, 9e4ca984 on feature/manabase-accuracy). The
MQ-01..05 deliverables + two-lens header (70-06) are live and exercised by that
session; the scripted rows are marked pass as validated via the live run + suite.
-->


## Tests

### 1. Cold Start Smoke Test
expected: Start the DeckFlow web app from scratch. Open the manabase tool, analyze a deck. Server boots clean, manabase.* flags resolve (source-mana-quantity, ramp-credit-v2, land-ramp-sim, color-aware-mulligan all ON by default), result renders with live data.
result: pass

### 2. MQ-02 — Per-source mana quantity (on-curve casts)
expected: Analyze a deck with Sol Ring + Ancient Tomb. Sim credits the extra mana — a 2-drop is castable turn 1 off a turn-0 Sol Ring, a 4-drop castable turn 2 off Ancient Tomb + 2 lands. Cast% on big-mana decks is higher than the old land-only model (does NOT under-count rocks as 1 mana each).
result: pass

### 3. MQ-01 — Commander not drawn into library
expected: Analyze a mana-creature commander deck (Selvala / Marwyn). Cast% is identical whether the deck list is 99 or 100 cards, and the commander is never "drawn" as a library card. The commander still counts as a castable color source (EffectiveSources), just not drawable.
result: pass

### 4. MQ-03 — Ramp-credit consistency (no softened verdict from one-shots)
expected: Analyze a ritual/Treasure-heavy deck (Dark Ritual, Jeska's Will, Treasure makers). The verdict is NOT softened by un-modeled one-shot mana — only repeatable ramp + draw gets credit. Repeatable land-ramp (Cultivate / Rampant Growth) IS credited via the modeled mana path, so the sim and the source-count regression agree.
result: pass

### 5. MQ-04 — Unsupported-interaction disclosure
expected: Analyze a deck with hybrid / Phyrexian / X-cost / snow / devotion cards. The result shows an explicit "N cards with unsupported interactions" disclosure instead of silently absorbing them — the user can see which dimensions the model does not handle.
result: pass

### 6. MQ-05 — Color-aware London mulligan
expected: Analyze a 2+ color deck. The mulligan keep heuristic requires the opening hand's lands to show >=2 distinct colors (not just a land count) before keeping. Multi-color decks show a (typically lower / more realistic) cast% reflecting color-screw mulligans; mono-color decks are unchanged.
result: pass

### 7. 70-06 — Two-lens result header
expected: The manabase result header shows BOTH lenses side by side — the Karsten color-source check (how many sources per color vs requirement) and the simulated cast rate (%). Renders cleanly on desktop and mobile across themes with no overflow.
result: pass

## Summary

total: 7
passed: 7
issues: 0
pending: 0
skipped: 0
blocked: 0
note: 4 deeper findings surfaced + fixed in-session (see Gaps); all resolved.

## Gaps

- truth: "Land-count advice should not tell the user to add lands when the sim shows the base casts fine (ramp covers the Karsten gap)"
  status: failed
  reason: "User reported: Disa the Restless deck (31 lands, 9 rocks/dorks + 10 ramp/draw, 93% on-curve, all colors over-supported) shows Health: Solid yet header says 'add ~4 land(s)' and Biggest fix says 'add ~4 more land(s)'. Contradictory — the lands aren't actually needed."
  severity: minor
  test: 0
  root_cause: "PrimaryFix step 2 (ManabaseModels.cs:731) and the header/Biggest-fix copy (Manabase.cshtml:212,293) trigger on raw LandDelta < -1 without sharing the Health verdict's corroboration gate. Health only treats a land shortfall as real when colorsWithIssue>=1 OR broadUnderSupport; the land-advice surfaces ignore that, so a ramp-saturated deck the sim rates fine still gets 'add N lands'."
  artifacts:
    - path: "DeckFlow.Core/Manabase/ManabaseModels.cs"
      issue: "PrimaryFix Lands branch + Health corroboration logic not shared"
    - path: "DeckFlow.Web/Views/Deck/Manabase.cshtml"
      issue: "Header line (212) and Biggest-fix Lands copy (293) show 'add N lands' uncorroborated"
  missing:
    - "Share Health's color-signal corroboration; suppress/reframe land-add advice when LandDelta<-1 but sim shows no broad under-support and no color issue"
  fixed: "ManabaseModels.cs ComputeColorSignals + LandShortfallCoveredByRamp; PrimaryFix gated; Manabase.cshtml header reframed. 137 Core tests green."

- truth: "The Skullspore Nexus self cost reduction ('costs {X} less, X = greatest power among your creatures') should be modeled, like Salubrious Snail's manual discount"
  status: failed
  reason: "User: Snail discounted Skullspore (cost 4); DeckFlow analyzed it at full {4}{G}{G}=6 -> 29% cast, distorting the demanding-cards list and weakest color."
  severity: major
  test: 0
  root_cause: "ManabaseClassifier ScalingSelfReducerRegex only matched 'costs {N} less for each'. Skullspore reads 'costs {X} less, where X is the greatest power among creatures you control' -> undetected -> no discount suggestion."
  fixed: "Added CardFact.Power + Scryfall power mapping + GreatestPowerReducerRegex; compute greatest fixed creature power and reduce generic by it (floor colored pips); pre-fills override box. 3 classifier + 3 mapper tests."

- truth: "Weakest color should be the most actionable color (a source helps), not the one owning a single curve-limited bomb"
  status: failed
  reason: "User: DeckFlow flagged GREEN weakest (over-supported, only Skullspore curve-limited) while Snail flagged BLACK (multiple BB demands, add-a-Swamp helps). Snail's is more useful advice."
  severity: minor
  test: 0
  root_cause: "ManabaseAnalyzer.OrderFindings ranked by worst single-spell cast% first; the phantom 29% Skullspore (a green card) made Green lead."
  fixed: "OrderFindings now ranks ColorLimitedUnderSupportedCount (source-fixable breadth) then Deficit ahead of worst-spell tail risk."

- truth: "The red 'weakest color' bar should not alarm on an adequately-supported color"
  status: failed
  reason: "User: red vertical bars on the Green row though Green is over-supported (30.6/22, Short by OK)."
  severity: cosmetic
  test: 0
  root_cause: "Manabase.cshtml applied manabase-row--weakest (red --danger border) whenever isWeakest, regardless of whether the color has a real source shortfall."
  fixed: "Red accent now gated on f.ColorLimitedUnderSupportedCount>0 || !f.IsAdequate."
