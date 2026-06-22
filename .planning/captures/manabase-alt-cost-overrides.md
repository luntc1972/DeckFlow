# Spec: Manabase Alt-Cost / Reduced-Cost Overrides

**Branch:** `feature/manabase-alt-cost` (worktree `../deckflow-altcost`, off `main@422dab0e`)
**Status:** design locked, ready for `/gsd-plan-phase`
**Origin:** user report — castability under-rates cards whose real cost is below printed MV.

## Problem
The castability simulator keys off printed mana value → effective on-curve turn. Cards
whose true cost is lower than printed MV are scored as far less castable than they play.
Three distinct categories, none handled today:

| Category | Example | Oracle signal | Suggested effective MV |
|---|---|---|---|
| Free / alternative cost | Force of Will | "without paying its mana cost" / pitch | 0 (also clears pips) |
| Scaling self-reduction | **Blasphemous Act** ({8}{R}, MV 9) | "costs {1} less to cast for each …" | floor = colored pips → 1 ({R}) |
| Evoke / suspend | Grief, Crashing Footfalls | "Evoke {cost}", "Suspend N—{cost}" | the alternative cost |

Note: scaling self-reduction is NOT the existing `CostReducer` (that discounts *other*
spells, e.g. Medallions). It is a per-card, board-dependent reduction the sim can't compute.

## Decisions (locked)
1. **Hybrid**: auto-detect flags candidates and SUGGESTS an effective MV; user confirms/edits.
2. **Separate overrides box** (not inline decklist syntax), pre-populated with detected
   suggestions, one per line: `Blasphemous Act: 1`.
3. **Detect all three categories** in v1.
4. Built on `main` (manabase code lives there), in this worktree.

## Design — reuse the existing cost seam
- New `ManabaseClassifier.DetectSelfCost(CardFact)` → optional `(suggestedMv, reason)` for the
  three categories. Sibling to the existing `DetectCostReducer`.
- Overrides carried as `IReadOnlyDictionary<string,int>` on `ManabaseAnalysisOptions`
  (name → effective MV), threaded `ManabaseController → ManabaseAnalysisService →
  ManabaseAnalyzer.Analyze`.
- Apply in `ManabaseAnalyzer.EffectiveTurn`: when an override exists, it wins
  (`min(override, computed)`); **when target MV < colored pip count, also drop the colored
  requirement** so a free spell routes like a true 0-cost card.

## Consistency with the 0-cost fix (just shipped)
Setting an alt-cost card to MV 0 + cleared pips makes it route through the IDENTICAL path as a
real 0-cost colorless card (Ornithopter): `EffectiveTurn` floor = `Max(1, totalPips=0)` = 1,
simulator `effectiveCost = Max(1, …)` = 1 → "1 generic mana, turn 1" ≈ 99%. No new free-spell
branch — the override just makes the card look like the 0-cost cards already handled.
- Blasphemous Act → 1 keeps its `{R}` pip (MV 1 ≥ 1 pip) → routes like any red 1-drop.
- Caveat: a truly free spell wants 0 mana but the model floors at 1 (~99%, not 100%). Keep
  that — matching every 0-cost card. True-free=100% would be a separate model change for all
  0-cost cards, out of scope.

## UI
- Razor: "Reduced / alternative costs" textarea below the deck input, pre-filled after analysis
  with detected suggestions; editable; re-submit applies.
- Display: overridden castability rows show the effective MV with a marker (e.g. `1*`) and an
  "alt/reduced cost" note — keeps the "show the work" auditability.

## Tests (per web-page-change rule)
- Core xUnit: DetectSelfCost for each category (FoW→0, Blasphemous Act→1, evoke→alt);
  EffectiveTurn override wins; MV<pips drops color; override parser.
- Web: options plumbing; overrides applied end-to-end.
- Playwright: overrides box renders, pre-populates, applies; verify desktop + mobile across themes.

## Open question for planning
- Override input keyed by exact card name — handle DFC/split/alternate names and case/normalization.
- Persist overrides across re-submits (hidden field) so the box round-trips.

## Bundled UI fix: pill centering (carried in this phase)
STATUS: likely ALREADY fixed by `88724d84` (live on prod since 2026-06-21 ~23:38). Verified on
prod at 1280 / 768 / 834 / 1024 px — pill text reads centered in every case. The user's "still
not centered" was probably an iPad cache of the pre-fix CSS (hard-refresh clears it). Only chase
the below if the complaint persists after a confirmed cache clear.

The earlier fix (`88724d84`) added `justify-content/text-align: center` to the pill,
but pills are content-width (`inline-flex`), so on desktop that change is a visual no-op —
the label already fills its shrink-to-fit pill. If a real problem remains it is likely one of:
- give pills equal/min width so the label centers inside a wider box, or
- center the pill ROW within the segmented control,
- (mobile already stretches pills full-width + centers — that path is fine).
Confirm the exact visual during the UI step (screenshot desktop) and fix in `site-common.css`
only (no theme forks). Add a Playwright assertion for the corrected behavior; verify desktop +
mobile across themes.
