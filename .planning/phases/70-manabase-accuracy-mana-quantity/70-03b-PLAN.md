# 70-03b — MQ-03 defect 1: model land-ramp in the sim (sim ↔ regression agree)

**Finding:** Audit #2 defect 1 (deferred from 70-03). **Risk:** Med. **Blast radius:** the castability
sim's mana availability on land-ramp decks (Cultivate / Rampant Growth / Nature's Lore). Cast%-affecting
→ gated + baseline-diffed, same discipline as MQ-02/03/05.

## Problem
MQ-03 (`ramp-credit-v2`, now ON) lowers the Karsten land target for **repeatable land-ramp** that puts
a land onto the battlefield, but the castability **simulator never models that land** — land-ramp
spells (Cultivate, Rampant Growth, Nature's Lore, Three Visits, Farseek) are NOT in `deck.Sources`
(only rocks/dorks/MDFC-backs/granted are, via `AddPartialSources`). So the regression credits the ramp
on the target side while the sim shows no extra mana → sim ↔ regression disagree, and expensive
payoffs in ramp decks read too low.

## Scope (Codex MED — stated explicitly)
The sim models **all** repeatable land-ramp-to-battlefield, regardless of mana value. This **fully
covers** the MV≤2 subset the regression credits (so sim ↔ regression agree on that subset — the
defect-1 goal) **and additionally** models the common MV3 land-ramp (Cultivate, Kodama's Reach) the
regression does not credit. That is not new asymmetry: the regression's `MV≤2` gate is Karsten's
cheap-ramp term (a target-side heuristic), while the sim is simulating real mana — modeling MV3 ramp
is the sim being accurate, not disagreeing. The phase title's "agree" refers to the credited subset;
the rest is realism the sim should have anyway.

## Key realization (keeps this small)
A fetched land that enters the battlefield is, to the sim, just a **persistent colorless mana source
that comes online one turn after the ramp spell is cast** — identical to a colorless mana rock of
`DeployCost = the ramp spell's mana value`. So land-ramp is modeled by adding the spell to
`deck.Sources` as a **colorless, non-land ramp source** and letting the EXISTING ramp deploy path
(`TryDeployRamp` → online next turn) handle it. No new `CardKind`, no new game-loop branch — but it
**does** need a real deploy-cost data path (below) and one small `BuildLibrary` change for
self-exclusion.

This directly satisfies both prior Codex constraints from 70-03:
- **NOT a drawable land in the opener (Codex BLOCKER):** the source is `IsLand = false`, so it is a
  Ramp library card — `CountLands` / the mulligan band never count it, and it only "produces" once
  cast and online. No opener inflation.
- **Quantity-only / colorless (Codex MED):** `Produces = []` (mask 0), `ManaAmount = 1`. It adds
  generic mana only, never a color → `EffectiveSources` / `SimRequiredSources` / per-color deficit are
  untouched (`CardFact` has no structured fetched-land color, and MQ-03 must not move color counts).

## Locked decisions
- **Model land-ramp as a colorless ramp source.** In the classifier, a spell matching the
  land-ramp-to-battlefield predicate (`"search your library for" + "land" + "onto the battlefield"`,
  front-face) is added to `deck.Sources` as `ManaSource { Produces = [], IsLand = false, Weight = 1.0,
  ManaAmount = 1, DeployCost = card.ManaValue }`, emitted by `AddSourcesAsCards` as a `CardKind.Ramp`
  card of that cost.
- **Real deploy-cost path (Codex HIGH).** The existing `rampCostByName` map in `BuildLibrary` is built
  from `deck.Spells.Where(s => s.IsManaSource)` only; land-ramp spells are intentionally NOT
  `IsManaSource` (so they keep their castability rows), so they are absent from that map and would fall
  through to the default ramp cost (2), mistiming MV3 Cultivate. Fix: add an optional `int? DeployCost`
  to `ManaSource`; `AddSourcesAsCards` uses `source.DeployCost ?? rampCostByName[name] ?? default`.
  Existing rock/dork sources leave `DeployCost = null` → unchanged (still resolved via the spell map).
- **Self-exclusion (Codex MED).** A land-ramp spell keeps its castability ROW *and* becomes a library
  ramp source. When the sim scores that card's OWN row, the single physical copy must not also be
  drawable as ramp in the same game. Fix: thread the tested spell's name into `BuildLibrary` and skip
  **one** land-ramp source whose name matches it. (Only affects that card's own row; every other row
  sees the full source set.)
- **Reuse the land-ramp predicate from 70-03.** Extract the `landToBattlefield` check out of
  `IsRepeatableRampOrDraw` into a shared `IsLandRampToBattlefield(card)` so the classifier credit
  (target side) and the new sim source (mana side) key on the SAME definition — they can never drift.
- **Scope = all-MV land-ramp** (see Scope section above): models the MV≤2 credited subset *and* common
  MV3 ramp. The new sim source is independent of the regression's `MV≤2` credit gate.
- **Cast%-affecting → new flag `manabase.land-ramp-sim`, default OFF**, decided after baseline diff.
  Flag-OFF path adds no source → byte-identical to today's sim. Read once in the service BEFORE
  classification (the source is added at classify time) and threaded into `Classify(..., landRampSim)`
  — same discipline as `rampCreditV2`. (Independent of `ramp-credit-v2` so it can be rolled out /
  rolled back on its own, even though it completes the same finding.)
- **Weight 1.0** (a full card, like a drawn-and-cast rock) so it is NOT counted in `RampSourceCount`
  (which is rocks/dorks at weight ≤ 0.75) and is never treated as a partial Bernoulli source.

## Resolved (was open)
- **Self-representation → exclude self** (Codex MED): `BuildLibrary` skips one same-name land-ramp
  source when scoring that card's row (see Locked decisions). No impossible "cast Cultivate while a
  second Cultivate ramps" state.
- **Double-credit:** the target reduction (regression) and the sim source are DIFFERENT mechanisms
  (land target vs simulated mana), not double counting — exactly the sim↔regression alignment the
  finding asks for. The baseline must confirm cast% rises modestly on land-ramp decks and the verdict
  does not swing.

## Side-Effects Report
- **Files (direct):**
  - `DeckFlow.Core/Manabase/ManabaseModels.cs` — add optional `int? DeployCost { get; init; }` to
    `ManaSource` (null = resolve cost the old way; non-null = explicit ramp deploy cost). One additive
    field; existing sources unaffected.
  - `DeckFlow.Core/Manabase/ManabaseClassifier.cs` — extract `IsLandRampToBattlefield`; `Classify`
    gains `bool landRampSim = false`; when on, add the colorless ramp `ManaSource` (×`Quantity`,
    `DeployCost = ManaValue`).
  - `DeckFlow.Core/Manabase/CastabilitySimulator.cs` — `AddSourcesAsCards` honors `source.DeployCost`
    when set (else the existing `rampCostByName`); `BuildLibrary` + `Simulate` thread the tested
    spell's name so one same-name land-ramp source is excluded from that card's own row.
  - `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — new `LandRampSimFlagKey =
    "manabase.land-ramp-sim"`, read via `IsFlagOn` (fail-safe OFF) BEFORE classification, threaded
    into `ResolveAndClassifyAsync` → `Classify`.
  - `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — seed `manabase.land-ramp-sim` FALSE
    (both dialects).
- **Core model change:** one additive optional `ManaSource.DeployCost`. **No new `CardKind`, no new
  game-loop branch** (reuses `TryDeployRamp` / online-turn). The only simulator changes are the
  `DeployCost` lookup and the self-exclusion name skip.
- **Other `Classify` callers:** CLI / tests default `landRampSim = false` → byte-identical.
- **`Simulate` callers:** the tested-spell-name is already available (it's `spell.Name`); the internal
  probe path (`ManabaseAnalyzer.cs:613`) passes its probe name too — no land-ramp source matches a
  probe, so self-exclusion is a no-op there.
- **Contract:** report shape unchanged; only cast% moves, flag-on. **Interaction:** independent of the
  other three flags (orthogonal); test combos.
- **Backward-compat:** flag-OFF reproduces today's sim exactly (no source added; `DeployCost` stays
  null on every existing source).

## Steps
1. Add `int? DeployCost` to `ManaSource`; `AddSourcesAsCards` uses `source.DeployCost ??` the existing
   `rampCostByName` lookup.
2. Extract `IsLandRampToBattlefield(card)` from `IsRepeatableRampOrDraw`; both callers use it.
3. `Classify(..., bool landRampSim = false)`: when on, for each land-ramp spell add a colorless
   non-land ramp `ManaSource` (×`Quantity`, weight 1.0, amount 1, `DeployCost = ManaValue`).
4. Self-exclusion: thread the tested spell name into `BuildLibrary`; skip one same-name land-ramp
   source when scoring that card's row.
5. Wire `manabase.land-ramp-sim` in the service (read before classify, fail-safe off); seed FALSE both
   dialects; update the seed-contract test (it now covers four manabase flags).
6. Baseline diff via `ManabaseFlagBaselineHarness`: add a `landRampSim` ON/OFF pass on the Golgari /
   land-ramp decks; confirm cast% rises modestly on land-ramp payoffs, color counts + verdict steady.

## Tests (Core, xUnit)
- `LandRampSim_Off_ByteIdentical` — a Cultivate deck's cast% identical to pre-70-03b (off path).
- `LandRampSim_On_RaisesExpensivePayoffCast` — a deck with several Cultivate/Rampant Growth casts an
  expensive payoff MORE often with the flag on.
- `LandRampSim_AddsColorlessOnly` — color counts (`EffectiveSources`/`SimRequiredSources`/deficit)
  invariant ON vs OFF on a multicolor land-ramp deck.
- `LandRampSim_NotCountedAsLand` — actual land count + mulligan land band unchanged (source is
  non-land).
- `LandRampToHand_NotModeled` — a land-search-to-HAND spell (no "onto the battlefield") adds no source.
- `LandRampSim_DeployTiming` — the fetched mana comes online the turn AFTER the ramp spell's MV (reuse
  of the ramp online-turn path), not the same turn.
- `LandRampSim_DeployCostFromMv` — a MV3 land-ramp (Cultivate) deploys at cost 3, not the default 2
  (guards the `DeployCost` path; pre-fix it would mistime).
- `LandRampSim_SelfExcludedFromOwnRow` — scoring the land-ramp card's own row does not let a second
  copy of itself ramp it out early (self-exclusion).

## Tests (Web, xUnit) — mirror MQ-03/05 (Codex LOW)
- `ManabaseAnalysisServiceTests`: a `manabase.land-ramp-sim` plumbing test proving the flag is read via
  `IsFlagOn` (fail-safe OFF when the key is absent) and changes castability only when enabled.
- `FeatureFlagStoreSeedTests`: extend the seed-contract theory to assert `manabase.land-ramp-sim` is
  seeded (FALSE on introduction).

## Done when
- Build clean; manabase suite green; flag-OFF proven byte-identical; baseline diff produced + reviewed;
  flag default chosen + documented (README / `Help/manabase.md` if flipped on); Codex review of plan +
  diff (DeckFlow rule).
