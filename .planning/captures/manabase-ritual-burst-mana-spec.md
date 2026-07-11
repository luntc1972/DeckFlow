# SPEC — Manabase ritual / one-shot burst mana

**Status:** proposed (for Codex plan-review)
**Branch:** `feat/manabase-ritual-burst-mana`
**Author:** Claude (plan), 2026-07-10
**Depends on:** current manabase pipeline (post MDFC-real-land removal, `main@66ed4843`)

## 1. Problem & value

The castability simulator and land-target math give **zero credit to one-shot
burst mana** — rituals (Dark Ritual, Cabal Ritual, Rite of Flame, Pyretic/Desperate
Ritual, Seething Song, Jeska's Will) and sac-to-add fast mana (Lotus Petal, Lion's
Eye Diamond). `HasRepeatableManaAbility` (`ManabaseClassifier.cs:486`) deliberately
drops anything whose ability cost contains "Sacrifice" or that isn't a repeatable
`<cost>: Add`, so these cards are invisible to both ramp and castability.

This is the single biggest source of **cEDH under-crediting**: real cEDH lists run
28–31 lands *because* rituals + fast mana substitute for lands on the explosive
turns. Karsten's own 2023 cEDH analysis (see `docs/research/manabase-prior-art.md`
§1 "cEDH / game-length curves") is explicitly about ritual-heavy builds — "a missed
land drop = a wasted ritual." Our tool, blind to rituals, reports these decks as
needing more lands / lower cast% than they truly have.

**Value:** materially more accurate turn-1–3 castability for ritual-fuelled decks
(cEDH first, but rituals help casual combo too). This is also the prerequisite for
re-evaluating the cEDH land-target floor (backlog item B) — that floor can't be
judged correct until rituals are credited.

**Core-value tie-in:** the manabase verdict + swap prompt are prompt artifacts the
user pastes into an AI; a verdict that ignores the deck's rituals is wrong at the
source, so this defends the tool's central promise.

## 2. Scope

### In (v1)
- **Instant/sorcery rituals**: a spell whose front-face oracle has an unconditional
  `Add {…}` producing **more mana than its own cost** (net positive). Examples:
  Dark Ritual (`{B}` → `{B}{B}{B}`, net +2 B), Rite of Flame (net +1 R),
  Pyretic/Desperate Ritual (net +1), Cabal Ritual, Seething Song (net +2),
  Jeska's Will (net variable — model the base `{R}{R}{R}` component).
- **Sac-to-add fast mana**: `{0}`/cheap artifact with "Sacrifice … : Add" — Lotus
  Petal (net +1 any), Lion's Eye Diamond (net +3 one color).

### Out (v1) — documented, deferred
- **Imprint/exile-cost** producers (Chrome Mox, Simian Spirit Guide, Elvish Spirit
  Guide) — cost is a card removed from hand, not mana; harder to model, lower freq.
- **Triggered Treasure** (Dockside, Goldspan) — board/opponent-state dependent.
- **X-ritual scaling** beyond the fixed base (Jeska's Will politics, Cabal Ritual
  threshold) — model the guaranteed floor only.
- **Land-target credit** — Karsten's regression excludes one-shots; v1 changes
  castability only, not the recommended land count. Revisit under a cEDH-only credit
  during calibration if the data warrants.

## 3. Design

### 3.1 Classification (`ManabaseClassifier` / `CardFact`)
- New detector `IsOneShotBurstMana(card)` + a captured `(int NetMana, IReadOnlyList<ManaColor> Colors, int OwnCost)`.
  - Reuse the existing `Add`-parsing + `ReminderTextRegex` strip already in the ramp
    detectors; compute produced pips from `ProducedMana` and own cost from `ManaCost`
    (instants) or the sac ability's mana cost (0 for Lotus Petal).
  - `NetMana = producedCount − ownGeneric+coloredCost` (floor 0; only emit if > 0).
- Emit a new **`ManaSource` kind** — either `IsOneShot=true` on `ManaSource`, or a
  parallel `IReadOnlyList<OneShotMana>` on `ManabaseDeck`. Prefer a dedicated list to
  avoid polluting the color-source census (rituals must NOT count as color *sources*
  in the Karsten per-color requirement — they're burst, not durable supply).
- **Never** classified as a rock/dork/land; excluded from `EffectiveSources`.

### 3.2 Simulation (`CastabilitySimulator`)
- Model a one-shot as a library card of a new `CardKind.OneShotMana` carrying
  `(netMana, colorMask, ownCost)`.
- On the **cast-attempt turn T** for the tracked spell (not before): after normal
  land/ramp mana is tallied, if a one-shot is **in hand** (drawn by T) **and its own
  cost is payable** from the turn's available mana, add its `netMana` of its color(s)
  to the pool **for that turn only**, then consume it (does not persist to T+1).
  - Mirrors the `TryDeployRamp` own-cost gate (`gateRampOnCastable`) but one-shot and
    same-turn, not online-next-turn.
  - Chain-safe cap: allow at most the rituals actually in hand; do not let one ritual
    pay for another speculatively in v1 (bounded, avoids runaway combos) — document
    the simplification.
- Integrate the added mana into `ColorsCoverable` (respect MQ-02 locked-color / DFS).
- **Determinism:** one-shots participate in the existing per-spell FNV seed; no new
  RNG (drawn-status already comes from the shuffled prefix).

### 3.3 Land target
- No change in v1 (`KarstenManabase` untouched). One-shots do not enter the FastMana
  bucket (that's 0-cost *artifacts* kept as persistent-ish substitutes) unless the
  card already qualifies there (Lotus Petal is `MV==0 && Artifact && ProducesMana`, so
  it ALREADY earns a FastMana land credit — must not double-count: a card credited in
  FastMana is excluded from the new one-shot sim path, or vice-versa; pick one lane).
  **Open question O-2.**

### 3.4 Flag & rollout
- **New flag** `analysis.manabase.ritual-burst-mana`, **default OFF** at first (ship
  dark, calibrate, then flip ON) — do NOT fold into the settled `accuracy` bundle
  until calibrated. Flag-off = byte-identical (the maintained invariant).
- Weighted toward cEDH in messaging, but the mechanic applies in both modes.

## 4. Tests
- Classifier: Dark Ritual → net +2 B; Rite of Flame → +1 R; Lotus Petal → +1 any;
  a repeatable rock (Signet) and a non-ritual sorcery → NOT one-shot; LED → +3 one color.
- Sim: a mono-B deck that misses B-source count but holds Dark Ritual casts a 3-drop
  on curve at materially higher % with the flag on vs off; own-cost gate (no B source
  → Dark Ritual can't fire) respected; one-shot consumed (doesn't help turn T+1).
- Flag-off byte-identical guard (extend `ManabaseFlagBaselineHarness`).
- No double-count: Lotus Petal doesn't get both FastMana land credit AND one-shot sim
  burst (per O-2 resolution).

## 5. Risks & open questions
- **O-1 (over-credit):** a ritual must help only its enabling turn and only when its
  own cost is payable; verify cast% gains are bounded (single-digit, cEDH-shaped),
  not blanket inflation. Calibrate against 3–5 known cEDH lists.
- **O-2 (double-count lane):** Lotus Petal / 0-cost artifacts already touch the
  FastMana land-target bucket. Decide: (a) one-shots are sim-only and never touch
  FastMana, or (b) FastMana cards are the "persistent-ish" lane and rituals are the
  instant/sorcery lane only. **Recommend (b):** keep artifact fast-mana in FastMana
  (land credit) and scope the new sim burst to **instant/sorcery rituals + explicit
  sac-to-add**, so Lotus Petal stays where it is and LED/Dark Ritual are the new
  entrants. Simpler, no double-count.
- **O-3 (calibration shift):** turning the flag on raises cEDH cast% and may move
  health bands; re-baseline goldens + document the expected direction.
- **O-4 (land-target/floor interaction):** once cast% reflects rituals, re-judge the
  cEDH 28 floor (backlog B). Likely the floor stays or drops, not rises.
- **O-5 (Jeska's Will / variable):** model only the fixed `{R}{R}{R}` floor; note the
  politics upside is uncredited (conservative).

## 6. Phasing
1. **P1 — classification + model** (Core): detector, net-mana capture, `OneShotMana`
   list on `ManabaseDeck`, unit tests. No sim wiring yet (flag off = inert).
2. **P2 — sim burst** (Core): `CastabilitySimulator` one-shot path + own-cost gate +
   consume + `ColorsCoverable` integration; sim tests.
3. **P3 — flag + surface + calibration**: wire the flag in `ManabaseAnalysisService`,
   re-baseline goldens/harness, update rules-doc + README + Help; decide default.
4. **(later) B** — cEDH floor re-evaluation once P1–P3 land.

## 7. Non-goals
- No card-economy modeling (a ritual costs a card; the sim scores castability, not
  advantage — consistent with how it treats every other resource).
- No change to the per-color Karsten source requirement (rituals are burst, not
  durable color supply).
