# Manabase — Commander-Cost Land Floor

**Date:** 2026-07-16
**Status:** Approved design, pre-implementation
**Supersedes:** the abandoned "commander engine credit" design (rejected — unsourced; see `../research/2026-07-16-commander-manabase-research.md`).

## Problem

The manabase land target is derived from the **deck's** average mana value (`avgMV`) — and the commander is **excluded** from that average (`ManabaseClassifier` guards `mvSum` with `if (!card.IsCommander)`). So the commander's own cost has **zero** effect on the land target. A cheap deck with an expensive commander (e.g. a 6-mana commander over an otherwise low curve) is therefore **under-landed**: the tool sees a low `avgMV` and recommends few lands, but the deck still needs to reliably reach the commander's cost.

Research (sourced, multi-author): the commander's **cost** raises land/source requirements — Nate Burgess `Lands = 31 + colors + commanderCMC` (+1 land/CMC); Karsten's high-cost commanders want the 4th/5th land drop. (The ramp side of this — 8–10 ramp scaled by commander cost — is **already** implemented via `ManabaseRampDrawBudget`, which keys off `CommanderManaValue`. Untouched here.)

## Goal

When the commander is expensive relative to what the deck's `avgMV` implies, raise the land target to a **floor** that reliably supports casting the commander on-curve — using Burgess's published formula as a floor (a `max`), so it can never double-count the existing `avgMV`-based target.

## Non-Goals

- Crediting the commander's draw/ramp *abilities* (rejected — unsourced).
- Changing the ramp/draw budget advisory (already commander-cost-aware).
- Touching colored-source / castability math (this is a land-count floor only).
- Any new dependency.

## Design

### The floor

```
colorCount          = number of colors in the commander's color identity (deck.CommanderColors)
highestCommanderCmc = max mana value among commander cards (0 if none)
commanderCostFloor  = FloorBaseline + colorCount + highestCommanderCmc      // FloorBaseline default 31 (Burgess)
finalLandTarget     = max(existingTarget, commanderCostFloor)               // only when the flag is on
```

- **Floor, not additive** → for a normal-curve deck the existing Karsten target already exceeds the floor, so nothing changes. It only lifts the target for the cheap-deck / expensive-commander case, which is exactly the under-landed one. This structurally prevents double-counting `avgMV`.
- **`FloorBaseline`** is a tunable `public const` on `KarstenManabase` (default 31, per Burgess) — the single calibration point.
- **Partners / backgrounds:** use the **highest** commander mana value (consistent with the existing ramp-budget threshold), not the sum — avoids over-inflating two-commander decks.
- **Applied in `ManabaseAnalyzer.ComputeTargetLands`** after the existing target is computed, gated by a new `commanderCostFloor` bool. Flag off → `max` not applied → byte-identical. This mirrors where the abandoned design would have hooked, but as a `max` rather than a subtraction.

### Interaction with cEDH / modes

The floor applies to the computed `finalTarget` in every mode (Standard and cEDH) — a cEDH deck with a cheap curve but an expensive commander is still under-landed. Because it is a `max`, cEDH's typically-lower targets are only raised when the commander genuinely demands it. (If calibration later shows the floor over-biting cEDH, the flag can be turned off or the baseline lowered — hence tunable + flag.)

### Transparency

- `ManabaseLandTargetBreakdown` gains `CommanderCostFloor` (double, the floor value) and `CommanderCostFloorActive` (bool, true when the floor lifted the target above the base).
- `ManabaseReportTextBuilder` renders a line only when active, e.g.
  `Commander cost floor (6-MV commander, 3 colors): 40 lands` — so a user whose cheap deck got a higher target sees why.

### Flag

`analysis.manabase.commander-cost-floor`, **seeded ON** (the effect is sourced). Registered in `FeatureFlagCatalog` + seeded in `FeatureFlagStore`; `CommanderCostFloorFlagKey` const on `ManabaseAnalysisService`; read and threaded into `ManabaseAnalyzer.Analyze`. Seeded ON but still flagged so it can be disabled if crawl calibration shows over-biting.

## Data Flow

`Classify` already yields `CommanderCount` + per-card `IsCommander`/`ManaValue`; the analyzer already computes `CommanderColors(deck)`. `ComputeTargetLands` reads the highest commander MV + color count, builds `commanderCostFloor`, and returns `max(existingTarget, floor)` when the flag is on; records the floor in the breakdown; report text surfaces it. No request state mutated.

## Error Handling

- No commander (e.g. a 60-card / non-singleton deck) → `highestCommanderCmc` 0 → floor = baseline + colors, which will virtually always be ≤ the existing target → no lift. Safe.
- Flag off → `max` skipped → byte-identical target + text.
- Floor ≤ existing target (normal case) → no lift, `CommanderCostFloorActive = false`, no new report line.

## Testing

- **Floor lifts an under-landed deck:** cheap deck (low `avgMV`) + 6-MV, 3-color commander → target raised to `31+3+6 = 40`; `CommanderCostFloorActive == true`.
- **No change for a normal deck:** mid-curve deck whose Karsten target already ≥ floor → target unchanged, `Active == false`.
- **Cheap commander no-op:** 2-MV commander → floor ≈ Karsten baseline → no lift.
- **Partners use highest CMC:** two commanders (2-MV + 6-MV) → floor uses 6, not 8.
- **Flag off → byte-identical** target + report text for the under-landed deck.
- **Regression:** existing CedhCalibration / land-target / breakdown / report-text golden suites stay green with the flag ON for decks that don't trigger the floor (most fixtures), and are updated only where a fixture legitimately now hits the floor (verify each such change is the intended, sourced lift, not a bug).

## Backward Compatibility

- Flag seeded ON, but a `max` floor only ever **raises** targets for genuinely expensive-commander decks; normal decks are unaffected. Any golden-test movement must be inspected and confirmed as the intended lift.
- New Karsten const + analyzer bool are additive; existing callers (`Analyze` overloads) default the flag false → identical output where not wired.
- New flag row seeded ON; older DBs without the row read via the service's flag helper (confirm ON-default vs OFF-default at implementation; seed row makes it ON in prod regardless).

## Open Questions / Assumptions

- **`FloorBaseline` = 31** (Burgess). Assumption: Burgess's constant is compatible enough with the tool's target scale that the `max` bites only for expensive commanders. **Verify at implementation** by running the existing calibration decks with the flag ON and confirming only expected decks move — if many normal decks lift, the baseline is miscalibrated and should drop (that's why it's a tunable const + flag).
- **Color count source:** use `deck.CommanderColors` count (the analyzer already computes it). Confirm it returns the commander's color identity, not just pips present.
- **cEDH interaction:** assume the `max` is safe in cEDH; flag lets us disable if calibration disagrees.
