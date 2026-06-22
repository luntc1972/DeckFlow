# Task 5 — Brago validation (before / after)

Re-ran the captured Brago WU deck through the Core analyzer via `BragoRealDeckHarness`
(`DECKFLOW_MANABASE_HARNESS` / `.manabase-harness-on` sentinel) after Tasks 1–4.
Full dump: `.planning/phases/manabase-modes-castability/64-harness-brago-output.md`.

Deck: 100 cards · 84 distinct · **33 lands** · avg MV 2.89 · ramp/draw≤2 = 14.

## Primary goal — the white phantom deficit is gone

| Metric (Casual · Standard) | Before (plan repro) | After (Tasks 1–4) |
|---|---|---|
| White "needs N" | **30** (mulligan-blind) | **22** (sim-derived, free-mull aware) |
| White deficit | +5.4 (phantom) | none (23.6 sources ≥ 22) |
| Blue "needs N" | ~25 | 22 (23.5 sources ≥ 22; driver Flare of Denial) |
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

## Codex review fixes (post-Task-5)

Codex flagged two HIGH issues in the sim-derived requirement; both addressed:

- **HIGH-1 (gold contention):** a gold/multicolor driver's per-color requirement now adds a bounded
  bump (one per other color it needs) on top of the isolated mono-color figure, so a `{W}{U}` card
  no longer under-counts by ignoring its second pip. Modeling the secondary colors *inside* a
  ramp-free synthetic deck was tried and rejected — it conflated color access with mana quantity and
  blew a turn-4 commander up to "needs 33", the very phantom this phase removes.
- **HIGH-2 (driver ranking):** the worst-driver per color is now ranked on the **mulligan-aware sim
  requirement** (cached per color/pips/turn/threshold), not the old mulligan-blind hypergeometric.
- **Cap guard (surfaced by HIGH-2):** if even an all-on-color base can't clear the bar, the color is
  not the bottleneck (the card is mana-/curve-limited, already reflected in its castability %), so the
  requirement clamps to the irreducible pip minimum instead of running to the land ceiling. This keeps
  Brago's blue requirement at a sane 22 (driver Flare of Denial) rather than a phantom 33.

Regression test added (`Analyze_GoldDriver_RequiresAtLeastAsManySources_AsMonoSinglePip`). Suites:
Core Manabase 100/100, Web Manabase 65/65.

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
