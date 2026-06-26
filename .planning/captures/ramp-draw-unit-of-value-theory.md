# Capture: "Unit of Value" ramp/draw theory — evaluation + Phase 71 sketch

Source: creator KB video ("grand unified theory" of lands/ramp/draw). Summary +
timestamped clips pasted into session 2026-06-26. Evaluated against DeckFlow's
current manabase model.

## The theory (as stated)

- **Baseline = 1 unit of value/turn:** every turn you draw 1 card + play 1 land,
  free, for everyone. Cards measured as deviation from that baseline.
- **Valuation:** a full "time walk" (extra card + extra land) = 1 unit.
  Rampant Growth (1 land) = 0.5; Skyshroud Claim (2 lands) = 1.0;
  Divination (draw 2) = 1.0.
- **Effective value (context):** ramp's value is highest *before* your threshold
  (≈ commander MV / the turn your plan goes operational); draw's value climbs as
  the game extends and hand empties → ramp and draw are roughly **inverse**, need
  both.
- **Ordered failure modes (Liebig minimum):** land failure (catastrophic,
  immediate) → ramp failure (gradual ceiling) → draw failure (quiet, late). Fix
  in that order.
- **Build heuristic:** ~38+ lands fixed; a fixed **24-slot ramp+draw budget**
  split by threshold — 12/12 @ MV4 baseline; 14 ramp/10 draw for high-MV
  commanders; 8 ramp/16 draw for 1-2 drop commanders.

## Verdict

Good **teaching frame**, coarse as a **scoring engine**. Internally consistent
with DeckFlow's ramp-credit decisions, but blind to the dimensions DeckFlow
actually computes (color/fixing) and dogmatic where it asserts fixed counts.

### Right
- Common unit genuinely bridges the ramp(mana)↔draw(cards) measurement gap.
- Inverse ramp/draw curve is correct (ramp front-loaded, draw back-loaded).
- Ordered failure land→ramp→draw matches our health-band priority (lands gate,
  weakest color drops verdict).
- One-shot ramp ≈ 0 (needs extra card AND permanent land) **agrees with
  `manabase.ramp-credit-v2`**: one-shot rituals + Treasures don't earn the
  land-reduction credit. Strong alignment.

### Oversimplifies
1. **card = land = 0.5 unit is fungible-fiction.** Lands flood (steep diminishing
   returns); cards are optionality. Unit conflates throughput (mana) with
   selection (cards). Rampant Growth ≈ half-Divination is a false equivalence.
2. **Blind to color/fixing — the left-lens dimension DeckFlow measures.** Flat
   land-COUNT valuation erases color screw, the #1 real manabase failure. Theory
   is orthogonal to and silent on Karsten source-count.
3. **threshold = commander MV is a single-point proxy.** Combo/aristocrats/storm
   operational points aren't the commander; collapses a curve to a point.
4. **38 lands + fixed 24 ramp+draw = asserted dogma, not derived from the unit
   theory.** 62 fixed slots leave ~37 for wincons+interaction+synergy. Breaks for
   cEDH (~28-32 lands + fast mana) — which the article itself tags.
5. **ramp/draw overlap ignored** (cantrip rocks, Mystic Remora, wheels are both);
   binary split doesn't model engines vs one-shot draw.

### Relation to current model
DeckFlow already implements a more granular version: Karsten color-source counts +
Monte-Carlo castability sim + `ramp-credit-v2` (repeatable ramp/true draw lower
land target) + `land-ramp-sim`. The theory is a coarse explanatory layer above
what we compute. We do NOT currently recommend ramp/draw *slot counts* — that's
the one net-new idea worth harvesting.

## Phase 71 sketch (NOT scheduled — backlog candidate)

**Title:** Ramp/Draw Budget Advisory (effective-value lens)

**Goal:** add a third, clearly-labeled *advisory* panel to `/manabase` that
counts the deck's ramp and draw slots and compares them to a threshold-derived
target, framed as opinion — never overriding the sim or source-count.

**Scope (thin):**
- Reuse existing classification: ramp = `IsRockOrDork`/`ProducesMana` /
  land-ramp; draw = repeatable-draw + cantrip predicates already used by
  `ramp-credit-v2`. Do NOT build a new tagger.
- Derive `threshold` from commander MV (fallback: deck curve median) — expose the
  proxy explicitly in copy so users know it's crude.
- Target split: interpolate the article's heuristic (12/12 @ MV4, →14/10 high,
  →8/16 low) as a STARTING table; mark "community heuristic, not Karsten math".
- Output: "You have X ramp / Y draw; for a ~MV{n} threshold a common split is
  A/B" + a one-line over/under nudge. No verdict change, no health-band coupling.
- Behind a flag `manabase.ramp-draw-budget` (seeded OFF), same pattern as the MQ
  flags.

**Explicit non-goals:**
- Do NOT adopt the fungible "unit of value" as a scoring number.
- Do NOT touch land count, color counts, or the castability verdict.
- Do NOT apply the fixed 38-land / 24-slot budget as a hard rule (cEDH/combo
  break it) — advisory only, cEDH mode should widen or suppress it.

**Open questions:**
- cEDH mode: suppress the advisory or use a different (lower-land, fast-mana)
  table? The article's numbers are casual-shaped.
- How to count overlap cards (cantrip rocks) — half to each bucket, or both?
- Is a slot-count advisory even on-mission (paste-into-ChatGPT core value)? Maybe
  better as prompt-artifact text than a UI panel.

Decision owner: user. Promote via /gsd:review-backlog if pursued.
