# Phase 2 No-Go Findings Template

**Date:** {fill: YYYY-MM-DD}

## 1. Header

**Run provenance**
- Database host: `{fill: host}`
- Run timestamp: `{fill: UTC timestamp}`
- Harness commit SHA: `{fill: sha}`
- Commanders enumerated: `{fill: count and/or names}`
- Raw deck count: `{fill: count}`
- Deduped deck count: `{fill: count}`

## 2. Verdict, stated first

No Cut Lab role floor diverges by commander at the stated bar, therefore Phase 3 is a documented no-op.

## 3. What was measured

The run measured `lands`, `ramp`, `draw`, `interaction-targeted`, `interaction-mass`, `protection`, `engines`, `payoffs`, and `wincons` across the EDHREC commander x bracket grid and the Postgres per-deck corpus, over `{fill: deck counts}` and `{fill: qualifying cell counts}`, using `minDeckCount = {fill: n}`, P25 ratio band `{fill: band}`, z threshold `{fill: z}`, absolute-gap fallback `{fill: gap}`, and breadth minimum `{fill: commanders}`.

## 4. Why this is a result and not a failure

The run touched the intended corpora, and the provenance above proves it. A null result narrows the design space: the evidence did not clear the bar for commander-specific role floors, and shipping one anyway would have been worse than stopping at the negative answer.

## 5. What is still usable from this run

Keep the corpus baselines per role, the per-commander distributions, the lands calibration verdict, the EDHREC commander x bracket grid, and the land self-check deltas. The go/no-go being negative does not invalidate those measurements; it only says they do not justify a Phase 3 floor override.

## 6. The lands calibration reading

Lands calibration verdict: `{fill: reproduces | contradicts | insufficient data}`. A `reproduces` verdict is positive news about the harness even though it is a negative result about decks, because reproducing the known no-go on lands is the control that says the broader methodology is behaving honestly.

## 7. Known limitations that could mask a real effect

- Protection under-detection disclosure: see [01.1-02-DELTA.md](../01.1-plan-role-classifier-heuristic-fixes-fix-the-counters-counte/01.1-02-DELTA.md:47). Reopen only after Phase 01.2 closes this lower-bound problem.
- Oracle-only classification can miss role intent that is visible in real lists but not in text alone.
- Category-tag coverage gaps in the Postgres corpus can suppress real role counts and must be reduced before a reopen is credible.
- The `isComboPiece: false` fix removes one confound, but any residual combo tagging mismatch still weakens interpretation.
- This was a single-mode run; if the mode choice constrained the sample too hard, reopen only with a justified comparison mode, not threshold shopping.
- The Postgres corpus has no bracket field, so commander x bracket floors over real per-deck distributions are still unavailable; that gap should be closed before treating a revisit as high-value.

## 8. What Phase 3 becomes

Phase 3 becomes a documented no-op closeout. ROADMAP dependency line: "If Phase 2 returns no-go for every role, this phase becomes a documented no-op closeout and the cycle ends at Phase 4/5." Phase 4 (functional twins) is independent and remains the cycle's headline; this result does not block it.

## 9. What would change the answer

1. Phase 01.2 widens protection vocabulary so the `protection` role is not judged on a known lower bound.
2. Phase 5 captures bracket on the Postgres-side corpus so commander x bracket analysis can be run on real per-deck distributions instead of only the EDHREC grid.
3. A materially larger deduped N increases the chance of enough qualifying commanders clearing the same bar honestly.
4. A lower `minDeckCount` would widen coverage, but at the cost of noisier cells and a weaker claim; change it only as a pre-committed study design, not as a reaction to this run.

## 10. Do NOT do this

- **Bar-lowering after the fact**: do not lower the bar after seeing the data.
- **Near-miss cherry-picking**: do not promote a role because it almost cleared.
- **Signal-present inflation**: do not present a `signal-present` role as a go.
- **Threshold rerolling**: do not re-run with different thresholds until something passes.
- **Methodology laundering**: do not treat a methodology contradiction as a deck finding before the method problem is resolved.
