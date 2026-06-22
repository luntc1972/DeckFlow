# Phase 64 — Research notes

Backing research: `.planning/manabase-mode-research.md` (web-sourced, 2026-06-21) + SPEC `.planning/manabase-modes-castability-SPEC.md`.

## Codebase facts the plan relies on (verified in worktree)

- `ManaSource` (ManabaseModels.cs): `Produces`, `Weight`, `IsLand` (false for rocks/dorks/MDFC backs), `EntersUntapped`. Non-land partial sources are ALREADY in `deck.Sources` with `IsLand=false`.
- `SpellRequirement`: `Name`, `ManaValue`, `Pips`, `IsGold`. **No flag distinguishing a ramp source from a payoff spell.**
- `ManabaseClassifier.AddSpellRequirement` adds any non-variable card that has a colored pip. **A mana dork like Birds of Paradise (`{G}`) is added as a SpellRequirement AND as a 0.5 partial source.** So to exclude rocks/dorks from the castability ROWS we must tag them.
- Variable-cost (X) spells are skipped (no meaningful on-curve turn) — keep skipping.
- **Colorless spells are currently skipped too (the `!hasColoredPip` early-return). New requirement: colorless spells that are NOT mana sources (Ugin, Wurmcoil Engine, Karn, big colorless artifacts/creatures) MUST appear in the castability list** with a mana-only cast chance. Only mana-producing rocks/dorks stay hidden from rows.
- `Hypergeometric.AtLeast(population, successes, draws, atLeast)` and `KarstenManabase.CastConsistency(...)`, `CardsSeenByTurn(turn, onPlay)`, `ConsistencyThreshold(mv)`, `SourcesNeeded(...)` all exist and are reused.
- `ManabaseAnalyzer.Analyze(deck)` is the single entry; `EffectiveSources(deck,color,untappedOnly)` already sums lands + non-land sources for color supply.

## Locked modeling decisions (v1)

1. **Exclude rocks/dorks from rows, include in math; include colorless non-ramp payoffs:** add `bool IsManaSource` to `SpellRequirement` (default false), set true in classifier when the card produces mana (`ProducesMana(card)` — same idea `AddPartialSources` uses). Stop the `!hasColoredPip` early-return so **colorless fixed-cost spells are added too** (Pips empty). Castability rows filter `!IsManaSource`, so colorless non-ramp payoffs show (P_color = empty product = 1.0 → cast chance = P_mana), while Sol Ring / Birds (IsManaSource) are hidden but still counted in `deck.Sources`. **Casual color-findings output stays byte-identical**: colorless spells have empty `Pips` so `BuildColorFindings`/`EnumerateUsedColors` skip them, and dorks remain in `deck.Spells` exactly as before. Keep skipping variable-cost (X) spells.

2. **Per-card castability = P_mana × P_color** at turn `T = ManaValue`, on the play, 7-card opener. Independence between "enough mana" and "enough colored sources" is an approximation — flag it in the UI caveat.
   - **P_mana (ramp-inclusive):**
     - `T ≤ 2`: lands only → `AtLeast(deckSize, lands, CardsSeenByTurn(T), T)`. (Ramp can't come online by turn 1–2 on curve.)
     - `T ≥ 3`: combined pool → `AtLeast(deckSize, lands + nonLandSourceCount, CardsSeenByTurn(T), T)`, where `nonLandSourceCount` = count of `deck.Sources` with `IsLand=false`. Approximation (ignores per-rock deploy cost/summoning sickness) — flag.
   - **P_color:** product over the spell's colors of `AtLeast(deckSize, colorSources(c), CardsSeenByTurn(T), pips(c))`, where `colorSources(c)` = rounded `EffectiveSources(deck, c, untappedOnly: T<=1)`. Gold cards already have ≥2 colors → product naturally penalizes them.
   - `CastPercent = round(100 × P_mana × P_color, 0)`.
   - `LimitingFactor`: the smaller of P_mana vs min color P → `"mana"`, `"color:<X>"`, or `"both"` when within ~3 pts.

3. **cEDH land target:** `KarstenManabase.CedhLandTarget(...)` = casual singleton target − 3.5, **clamped to ≥ 28**, with fast-mana/rocks fully credited (already are). Validation target: a sub-2.0-MV, ~12-ramp deck lands ~29–31. (Flat offset chosen over re-fitting a competitive regression — simplest thing that hits the 28–32 band the sources report.)

4. **cEDH thresholds:** keep `(89+M)%` for the source check but, for the cEDH summary, emphasize early (turn 1–3) colored access for the cheapest interaction. v1 keeps the existing source math; the mode mainly changes the land target + summary copy. (Deeper cEDH castability surface deferred.)

5. **Mode plumbing:** `ManabaseMode { Casual, Cedh }` (Core). `ManabaseAnalyzer.Analyze(deck, mode = Casual)` overload — default keeps every existing caller/test byte-identical. `ManabaseReport` gains additive `Mode` + `Castability` list.

## Open decisions surfaced to executor (low risk, pick + unit-test)
- P_mana ramp pool for `T≥3`: "add all non-land sources" (chosen) vs "only those of own-MV ≤ T−1". v1 = all; revisit if it overstates.
- Whether `LimitingFactor` "both" band is 3 pts (chosen) or tighter.
