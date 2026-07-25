# 0003 — Ramp classifiers intentionally diverge; draw is shared

Date: 2026-07-24

## Context

Two subsystems classify cards by role, and both need a notion of "ramp" and
"draw":

- **Role tally** — `DeckStatClassifier` (`DeckFlow.Core/Analysis`). Broad,
  single-boolean predicates (`IsRampCard`, `IsDrawCard`) feeding the Cut Lab
  role panel, the analysis prompt `deck_stats` counting aid, and the multi-axis
  score. These are counting aids for a human/LLM reader — approximate is fine.
- **Manabase math** — `ManabaseClassifier` (`DeckFlow.Core/Manabase`). Several
  *tuned, flag-gated* ramp predicates (`IsRampPieceForBudget`,
  `IsRepeatableRampOrDraw`, `IsRockOrDork`, `IsLandRampToBattlefield`) calibrated
  for the castability simulator, the ramp/draw budget, and the Karsten −0.28
  land-target credit. Precision matters: mis-counting one source shifts a land
  recommendation.

A reviewer (or a well-meaning "reconcile the duplication" pass) naturally wants
to collapse these into one shared `IsRamp`. Doing so is a defect: the Manabase
predicates deliberately differ from each other (`rampCreditV2` narrows the land
credit vs the budget count) and from the broad role-tally boolean (they
sac-guard Lotus Petal, are front-face-aware, and exclude one-shot rituals from
persistent-source credit). A single boolean cannot serve both the "rough role
count" and the "Karsten source weight" purposes.

Draw is different. The robust, you-anchored literal-draw regex (originally
`ManabaseClassifier.YouCardDrawRegex`) is correct for *both* purposes, so it was
promoted to `DeckStatClassifier.MatchesYouCardDraw` and is now the single shared
literal-draw signal (see the 2026-07-24 draw reconcile). The one intentional
split that remains: `DeckStatClassifier.IsDrawCard` unions in `investigate` /
`connive` (clue/connive card-advantage) for the *role tally*, while Manabase's
draw term stays literal-draw only — clue/connive is card advantage but not a
Karsten "draw", so it must not enter the Manabase draw budget or land credit.

## Decision

- **Ramp: do not unify.** `DeckStatClassifier.IsRampCard` (role tally) and the
  Manabase ramp predicates stay separate by design. Changing one to match the
  other is prohibited without re-deriving the Manabase calibration.
- **Draw: shared regex, split union.** `MatchesYouCardDraw` (regex-only) is the
  single literal-draw source both subsystems reuse. The `investigate`/`connive`
  union lives only in `IsDrawCard` (role tally); Manabase's `IsYouCardDraw`
  delegates to `MatchesYouCardDraw` and never sees the union.

## Consequences

- The same card can be "ramp"/"draw" in the Cut Lab role panel or analysis
  `deck_stats` yet not credited in the Manabase land math (and vice-versa).
  This is expected, not drift.
- A change to `DeckStatClassifier.IsRampCard` can shift the analysis prompt and
  Cut Lab roles but MUST NOT be assumed to leave Manabase output unchanged —
  verify with the byte-identity + Manabase suites (a widening of `IsRampCard`
  on 2026-07-24 required rebaselining the analysis/comparison goldens; see
  commit history).
- Reviews and automated cleanup passes must not report the ramp predicates as
  cross-subsystem duplication.
- Adding a card-advantage keyword (e.g. a future clue-like mechanic) to
  `IsDrawCard` must NOT be propagated into Manabase's draw notion unless it is
  genuinely a Karsten literal draw.
