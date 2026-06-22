# Task 5 — Brago validation (before / after)

Re-ran the captured Brago WU deck through the Core analyzer via `BragoRealDeckHarness`
(`DECKFLOW_MANABASE_HARNESS` / `.manabase-harness-on` sentinel) after Tasks 1–4.
Full dump: `.planning/phases/64-manabase-modes-castability/64-harness-brago-output.md`.

Deck: 100 cards · 84 distinct · **33 lands** · avg MV 2.89 · ramp/draw≤2 = 14.

## Primary goal — the white phantom deficit is gone

| Metric (Casual · Standard) | Before (plan repro) | After (Tasks 1–4) |
|---|---|---|
| White "needs N" | **30** (mulligan-blind) | **21** (sim-derived, free-mull aware) |
| White deficit | +5.4 (phantom) | none (23.6 sources ≥ 21) |
| Blue "needs N" | ~25 | 22 (23.5 sources ≥ 22) |
| Castability vs Salubrious Snail | — | mean **|Δ| 2.7 pts** over the 7 reference cards |

The mulligan-blind hypergeometric requirement that produced "needs 30 white" for a
turn-two `{W}{W}` is replaced; the requirement now tracks Karsten/Snail-style
mulligan-aware numbers. Free first mulligan (Task 1) is modeled.

## Verdict — accurate, not forced to "Functional"

The plan anticipated the deck would read **Functional** once the white phantom was
removed. With the now-accurate model it reads **Needs work**, driven by *real* signals
rather than the old phantom:

- **Casual:** 33 lands vs **35.8** target → genuinely ~3 short (`LandDelta −2.8`, below
  the −1 Functional bound), plus 6/30 blue cards under the 80% bar — mostly high-MV
  six-drops (Deadeye Navigator / Sun Titan / Y'shtola at ~53% on curve = a mana-quantity
  consistency gap, not a color phantom).
- **cEDH:** lands are fine (33 vs 32.3, `+0.7`), but blue is genuinely short for its
  high-MV payoffs (27 needed vs 23.5) → Needs work.

This is the correct outcome: the phase goal was an **accurate** verdict, not a specific
label or matching Snail. Predicates were left exactly as pinned in Task 3 — not relaxed
to manufacture a "Functional". The fix removed the false negative (phantom white deficit);
the residual "Needs work" reflects a real land/consistency shortfall a builder should act on.

## UI / test coverage

- Two-tier verdict + demanding-card list + average-delay column: rendered (Tasks 3–4),
  unit-covered by `ManabaseDisplayTests` (HealthLabel/HealthCss/DelayText),
  `ManabaseAnalyzerTests` (Healthy/Functional/NeedsWork predicates) and
  `CastabilitySimulatorCoverageTests` (delay = 0 / >0 / capped).
- Theme × viewport (desktop + mobile, no horizontal overflow) guarded by
  `e2e/manabase-primer-ui.spec.ts`.
- Per the existing `manabase.spec.ts` convention, e2e does **not** submit a real analysis
  (it would call Scryfall and is flaky in CI); the analysis math/verdict is covered by the
  xUnit suites and this harness instead.
