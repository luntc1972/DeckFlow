# SPEC — Manabase ritual / one-shot burst mana

**Status:** APPROVED with conditions (Codex gpt-5.5 plan-review, 2026-07-10) — HIGH
O-2 (FastMana double-count) + 4 MED folded into §2/§3.1/§3.2/§4. Cleared to execute.
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

### In (v1) — instant/sorcery rituals ONLY (Codex O-2 HIGH resolution)
- **Instant/sorcery rituals**: a spell whose **front-face type line is Instant or
  Sorcery** and whose oracle has an unconditional `Add {…}` producing **more mana than
  its own mana cost** (net positive). Examples: Dark Ritual (`{B}` → `{B}{B}{B}`, net
  +2 B), Rite of Flame (net +1 R), Pyretic/Desperate Ritual (net +1), Cabal Ritual,
  Seething Song (net +2), Jeska's Will (model the fixed `{R}{R}{R}` floor only).
- **Hard exclusion of the FastMana lane (no double-count):** a card that satisfies the
  existing FastMana predicate (`ManaValue==0 && Artifact && ProducesMana`, `Cls:233`)
  is **never** an `OneShotMana`. This keeps **Lotus Petal, Lion's Eye Diamond, Jeweled
  Lotus, Lotus Bloom** in their current FastMana land-target lane and out of the sim
  burst — they are already credited (Codex: LED/Lotus Petal are NOT new entrants).
  Since the v1 predicate is *Instant/Sorcery* front face, artifacts are excluded by
  construction anyway; the FastMana check is the belt-and-suspenders assertion.

### Out (v1) — documented, deferred
- **All artifact fast mana** (Lotus Petal, LED, Jeweled Lotus, Chrome Mox, Grim
  Monolith, etc.) — stays in the FastMana land-target lane; moving it to the sim would
  change land targets and risk O-2. Revisit in a later phase if the FastMana land
  credit proves less accurate than a sim burst.
- **Sac-*outlet* mana** (Ashnod's Altar, Phyrexian Altar) — they sacrifice *another*
  permanent and are repeatable engines, not self-contained one-shots. Explicitly
  excluded (Codex MED).
- **Imprint/exile-cost** producers (Chrome Mox, Simian/Elvish Spirit Guide) — cost is
  a card removed from hand, not mana; harder to model, lower freq.
- **Triggered Treasure** (Dockside, Goldspan) — board/opponent-state dependent.
- **X-ritual scaling** beyond the fixed base (Jeska's Will politics, Cabal Ritual
  threshold) — model the guaranteed floor only.
- **Land-target credit** — Karsten's regression excludes one-shots; v1 changes
  castability only, not the recommended land count. Revisit under a cEDH-only credit
  during calibration if the data warrants.

## 3. Design

### 3.1 Classification (`ManabaseClassifier` / `CardFact`)
- New detector `IsOneShotBurstMana(card)` returning `(int NetMana, IReadOnlyList<ManaColor> Colors, ManaCost OwnCost)`.
  - **Conservative shape-match only (Codex MED):** require front-face type line
    Instant or Sorcery (`CardTypeLine.FrontFace`), an unconditional `Add {symbols}`
    clause (reuse `ReminderTextRegex` strip + the existing `Add`-parsing), and
    `NetMana = producedPips − ownCastPips > 0`. Do **not** reuse `ManaAmount.Parse`
    blindly for burst sizing — it returns 1 for "any combination"/multi-color splits;
    derive produced count from the literal `ProducedMana` list / the `Add {…}` symbols.
  - **Own cost** = the spell's `ManaCost` (colored pips + generic), captured as a
    `ManaCost` so the sim can test payability with MQ-02 semantics.
  - **Colors** = the produced colors (Dark Ritual → {B}; a "any color" ritual → all).
  - **Explicit exclusions:** FastMana-predicate cards (§2), sac-*outlets* (cost
    contains "Sacrifice" naming another permanent/creature — Ashnod's/Phyrexian Altar),
    and anything with a variable `{X}` in the produced amount (model fixed floor only,
    or skip if no fixed floor).
- Emit a dedicated `IReadOnlyList<OneShotMana>` on `ManabaseDeck` (NOT a `ManaSource`).
  Rituals must **not** count as color *sources* in the Karsten per-color requirement or
  `EffectiveSources` — they are burst, not durable supply. Never a rock/dork/land.

### 3.2 Simulation (`CastabilitySimulator`)
- Model a one-shot as a library card of a new `CardKind.OneShotMana` carrying
  `(netMana, colorMask, ownCost)`, drawn via the same shuffled prefix as everything else.
- **Exact insertion point (Codex MED):** on the tracked spell's cast-attempt turn T,
  **after** `TryDeployRamp` and `ReserveGenericForRamp`, while building `availableColors`
  and **before** `TotalMana` / `ColorsCoverable` (`CastabilitySimulator.cs:~1082`).
  - Test the ritual's **own cost** against the **pre-burst, post-ramp-reserve** sources
    via `ColorsCoverable(baseSources, ritualOwnPips, ritualOwnCost)` — so a Dark Ritual
    with no B source in play cannot fire.
  - If payable, **append** its `netMana`/colors to `availableColors` for **this cast
    attempt only**; do **not** add it to `rampOnBoard` (no persistence, no deploy-ramp
    chaining).
  - Then run the normal `ColorsCoverable(availableColors + burst, spellPips, spellCost)`
    for the target spell — preserving MQ-02 locked-color / DFS.
- **Chain guard (Codex MED):** own-cost gates are evaluated against the **base** pool
  only, never a progressively enlarged one — no ritual pays for another in v1. At most
  the rituals actually in hand contribute; document the simplification.
- **Color correctness:** because activation itself goes through `ColorsCoverable`, a red
  ritual can't help a blue-pip spell unless the rest of the pool covers blue.
- **Determinism:** one-shots use the existing per-spell FNV seed; no new RNG.

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
- Classifier (P1): Dark Ritual → net +2 B; Rite of Flame → +1 R; Cabal Ritual →
  fixed floor; Seething Song → +2. Negatives: a repeatable rock (Signet), a sac-outlet
  (Ashnod's Altar), a non-ritual sorcery → NOT one-shot. **Lane guard:** Lotus Petal
  and LED are **NOT** one-shot (they stay FastMana — asserts O-2 exclusion).
- Sim (P2): a mono-B deck short on B sources but holding Dark Ritual casts a 3-drop on
  curve at materially higher % with the flag on vs off; own-cost gate (no B source in
  play → Dark Ritual can't fire) respected; consumed (doesn't help turn T+1); a red
  ritual does NOT help a blue-pip spell.
- Flag (P3): `analysis.manabase.ritual-burst-mana` seeded **OFF** (seed test +
  `FeatureFlagCatalog` entry test); flag-off **byte-identical** at classifier, service,
  and report level (extend `ManabaseFlagBaselineHarness` + a service-level parity test).

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
