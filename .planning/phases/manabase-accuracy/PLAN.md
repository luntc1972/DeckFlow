---
slug: manabase-accuracy
created: 2026-06-22
mode: phase
branch: feat/manabase-mulligan-accuracy
base: main@d1ae8e69
implementer: claude
reviewer: codex (gpt-5.4)
---

# Phase: Mana Base Accuracy — mulligan, verdict, average delay

Goal: make the Mana Base verdict ACCURATE (not match Salubrious Snail). The Brago deck reads
"needs work" because of two model gaps, both surfaced on a real deck:
- the colored-source requirement is **mulligan-blind** (says "needs 30 white sources" for
  Grand Abolisher `{W}{W}`; Karsten's mulligan-aware table is ~20; deck runs 24.6 → phantom deficit),
- the sim's London mulligan does **not** model Commander's **free first mulligan**, so tight
  double-pips (turn-2 `WW`) are under-rated.
The health verdict is also binary — one sub-threshold card flips the whole base to "needs work".

## Repro (captured)
Brago WU deck, 99 cards: 33 lands vs ~35; **Health: needs work**. White finding: 24.6 sources,
"needs 30 for Grand Abolisher", deficit 5.4, 5/27 under-supported. Grand Abolisher cast 77%
(`{W}{W}`, MV 2, turn 2). The "needs 30" is the mulligan-blind hypergeometric; Karsten's real
WW-by-turn-2 is ~20, which 24.6 clears.

## Tasks

### Task 1 — Commander free first mulligan (Claude impl, Codex review)
`CastabilitySimulator.LondonMulligan` uses `keep = 7 - mull` (standard London). Model the mulligan
as explicit `(depth, bottomCount)` state — NOT derived from keep — so later depths bottom correctly
(Codex HIGH-1):
- **Singleton/Commander (free first mull):** depth 0 → keep 7 / bottom 0; depth 1 → keep 7 / bottom 0
  (the free mull); depth 2 → keep 6 / bottom 1; depth 3 → keep 5 / bottom 2. Cap depth at 3.
- **Non-singleton (standard London):** depth 0 → 7/0; depth 1 → 6/1; depth 2 → 5/2.
- `BottomCards` consumes the explicit `bottomCount`.
- Land-keep bands (Codex MEDIUM-1): keep the SAME 7-card band `[2, hiCap]` for singleton depth 0 AND
  depth 1 (the free mull rejects mediocre 7s but not infinitely); tighten only once bottoming starts
  (depth ≥ 2). Gate the free mull on `deck.IsSingleton`.
Tests: singleton free-mull raises cast% vs the old schedule AND is byte-stable across repeated calls
(determinism); non-singleton unchanged; 0/1/2/3-land openers select correctly per singleton vs not.

### Task 2 — Mulligan-aware source requirement drives BOTH display and verdict (Claude impl, Codex review)
Do NOT just delete the hypergeometric `Deficit` from the verdict (false-negative risk, Codex HIGH-2).
Replace it with a mulligan-aware requirement computed from the SIM (option A; option B dropped):
- Add `MulliganAwareRequiredSources` / `MulliganAwareDeficit` to `ColorSourceFinding`. Compute via the
  sim on the color's DRIVING spell: bounded binary search over colored-source count for the smallest
  count whose sim cast% ≥ threshold. Reduced trials (~4–5k) for the search; **confirm the boundary**
  by re-checking N and N-1 and re-running at full trials if reduced-trial noise inverts them.
- `IsCompositeProblem` becomes `UnderSupportedCount > 0 || MulliganAwareDeficit > 0`. The displayed
  "needs N" and the verdict now come from the SAME mulligan-aware metric (no more "needs 30").
- Cache the search per **driving spell identity** (effective cost signature + onCurveTurn + isGold +
  threshold + singleton flag), not `(color, pips, turn)` — overrides/gold change the requirement
  (Codex MEDIUM-2/3). The search must run on the OVERRIDDEN effective requirement (post ApplyCostOverrides).
- Perf: ~6 reduced-trial sims per color (≤5 colors) ≈ 10–15% over the existing per-spell sims; bounded,
  well under the request timeout.
Tests: Brago white no longer shows a phantom deficit (≈20 not 30); required-sources monotonic in source
count and stable under cost overrides; no deficit when the driver's sim cast% ≥ threshold.

### Task 3 — Two-tier health verdict (Claude impl, Codex review)
Replace binary OK/needs-work with a graded `ManabaseHealth` enum, EXACT numeric predicates (Codex HIGH-3):
- **Healthy** — `LandDelta >= -1` AND every color `UnderSupportedCount == 0`.
- **Functional** — `LandDelta >= -1` AND every color `MulliganAwareDeficit <= 0` AND every color
  `UnderSupportedCount <= max(1, ceil(colorCards * 0.15))`. Surfaces the demanding cards
  (e.g. "Functional — 1 demanding card: Grand Abolisher (77%)").
- **NeedsWork** — otherwise (a color systematically short: real mulligan-aware deficit, or
  under-supported beyond the ratio, or lands short).
- Keep `IsHealthy` (== `Health == Healthy`) for back-compat but update ALL consumers (Codex LOW-3):
  `BuildSummary`, the view's "needs work" copy, `ManabaseViewModel`, and the existing
  `DeckFlow.Web.Tests/Manabase/BragoRealDeckHarness.cs` (asserts the old boolean).
Tests: Brago → Functional; a white-screwed deck → NeedsWork; a clean deck → Healthy; harness verdict
class updated without regressing WeakestColor/summary.

### Task 4 — Average delay metric (Claude impl, Codex review)
Record each trial's first castable turn; report `avgDelay = mean(max(0, firstCastableTurn - onCurveTurn))`
over **all** trials (Codex MEDIUM-4):
- `firstCastableTurn = lastSimulatedTurn + 1` when the spell never becomes castable within the grace
  window (explicit cap, not implementation-dependent).
- Ramp/early casts clamp to 0 (never negative). Add `AverageDelay` to `CardCastability`.
- Surface per castability row (e.g. "+0.4 turns") as supporting context.
Tests: always-on-curve → 0; colour-starved → > 0; never-castable → capped horizon delay.

### Task 5 — Validate on the Brago deck + UI (Claude impl, Codex review)
- Re-run the captured Brago deck; confirm the verdict reads Functional and the white "needs N" is
  sane (~20, not 30). Document the before/after numbers.
- View: render the two-tier verdict, the demanding-card list, and the average-delay column;
  CSS in site-common.css only. Help + README updated for the new verdict + delay column.
Tests: Playwright — verdict + delay column render desktop + mobile across themes; xUnit covers the
math (Tasks 1–4).

## Files (ALLOWED SET — fence)
- DeckFlow.Core/Manabase/CastabilitySimulator.cs — free first mulligan, first-castable-turn / avg delay
- DeckFlow.Core/Manabase/ManabaseAnalyzer.cs — verdict off sim, mulligan-aware required-sources, two-tier
- DeckFlow.Core/Manabase/ManabaseModels.cs — verdict enum/fields, AvgDelay, demanding-card list
- DeckFlow.Core/Manabase/KarstenManabase.cs — (only if a hypergeometric mulligan correction is chosen)
- DeckFlow.Web/Models/ManabaseViewModel.cs, Views/Deck/Manabase.cshtml, wwwroot/css/site-common.css,
  Help/manabase.md — verdict + delay UI/help
- DeckFlow.Core.Tests/Manabase/* , DeckFlow.Web.Tests/Manabase/* (incl. BragoRealDeckHarness.cs —
  update old IsHealthy boolean assertion) , DeckFlow.Web/e2e/manabase.spec.ts
- README.md

## Constraints
- Accuracy is the goal; Snail is a sanity reference only, never a target number.
- Free mulligan ONLY for singleton/Commander (deck.IsSingleton); 60-card keeps standard London.
- Mind perf: bounded reduced-trial sims for required-sources; keep analysis within the request timeout.
- Preserve seeded determinism (no Math.Random/global RNG); reproducible across runs.
- Carve-outs (init props, raw strings, switch exprs), LF endings, changed-lines format gate.
- Layout CSS in site-common.css only; never edit theme forks.

## Resolved by Codex plan review (APPROVE-WITH-CHANGES)
- Required-sources: **option A (sim-derived)**; option B dropped (approximation-on-approximation).
- Free mull modeled as explicit `(depth, bottomCount)` state; bands held for singleton depth 0–1,
  tightened from depth 2; depth capped at 3.
- Verdict keys off a **mulligan-aware** deficit field (not deletion) — no false negatives.
- Two-tier predicates pinned numerically (Healthy / Functional / NeedsWork above).
- Average delay is **display + verdict-supporting context**, "never castable" capped at horizon.
- Downstream `IsHealthy` consumers (summary, view copy, view model, BragoRealDeckHarness) updated.
