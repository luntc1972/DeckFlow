# Feature Research — Cycle 13: Deck Evaluation & Creator Output

**Domain:** Commander/cEDH deck-evaluation + creator-output features layered on an existing AI-paste-artifact engine
**Researched:** 2026-06-27
**Confidence:** HIGH on bracket definitions + Game Changers list (official WotC sources, cross-checked Feb 2026 update) and EDHRank axes (direct source); MEDIUM on bracket-*balancing* output shape and tap-analyzer exact readout (no incumbent ships the balancer; tap-analyzer described in prose, not screenshots); MEDIUM on primer-refresh semantics (no incumbent auto-refreshes — inferred from Moxfield's manual model + DeckFlow's diff engine).

---

## Context: What already exists in DeckFlow (do NOT re-spec)

The four Cycle 13 features build on shipped capability. Each new feature is categorized against these boundaries:

- **Import + enrichment** — Moxfield/Archidekt URL + text paste → `DeckEntry` list, Scryfall enrichment, `ScryfallCardFactMapper`. **Reuse for all four features.**
- **Manabase castability engine (P70-72)** — `CastabilitySimulator` (seeded Monte-Carlo, 20k trials, London mulligan), `ManabaseReport` with per-spell `CardCastability` (cast %, on-curve turn, average-delay), per-color findings, four-tier health verdict, ramp/draw counts, fast-mana count, MDFC counts, land target breakdown. **The `ManaSource.EntersUntapped` flag and per-trial land-drop simulation already model tapped state — Tap Analyzer surfaces this, it does NOT rebuild it.**
- **Command-zone detection** — commander/partner/background/companion classification (P72-73). **Feeds bracket commander-power signal and multi-axis Power axis.**
- **Deck Primer generator** — 31-section catalog, bracket presets, Commander Spellbook combo grounding, EdhTop16 matchup routing, per-AI artifact variants. **Auto-Refreshing Primer extends this — it does NOT replace it.**
- **AI-agnostic paste artifacts (ADR-0001)** — every workflow renders ChatGpt/Claude/Gemini variants with NO shared helper (intentional prose decoupling). **Every new paste artifact in Cycle 13 must render in all three variants WITHOUT extracting a shared builder.**
- **EdhTop16 meta client** + **CommanderSpellbook combo client** — combo density + meta archetype data. **Feeds bracket combo-floor and multi-axis Consistency axis.**
- **Feature flags + admin tool registry** — every new tool/surface ships flag-gated.

**Core thesis (overrides all):** every feature must end in output the user pastes into ChatGPT/Claude/Gemini and gets a useful answer in one round-trip, without reformatting. A score or bracket number that is NOT in a paste artifact only half-serves the thesis.

---

## FEATURE 1 — Bracket Classifier + Balancer

### Expected behavior
Two linked capabilities:
1. **Classifier** — given an imported deck, auto-detect which of the official 5 WotC brackets it currently sits in, by applying the hard floors (Game Changers count, mass land denial, 2-card infinite combos, extra-turn loops) and the soft signals (fast mana density, tutor count, combo speed).
2. **Balancer** (the uncontested differentiator) — given a *target* bracket N, output a concrete "cards to cut (and why)" artifact that moves the deck down to bracket N. No incumbent ships this — classifiers stop at "you are Bracket 4."

### Input signals
- **Hard floors (force a minimum bracket):**
  - **Game Changers count** — match deck cards against the official list (53 cards as of Feb 2026). 0 GC → B1/B2 eligible; 1-3 GC → minimum B3; 4+ GC → minimum B4.
  - **2-card infinite combos** — via existing CommanderSpellbook client; B1/B2 forbid intentional 2-card combos, B3 forbids *early-game* combos (must not reliably win before ~turn 6-7), B4/B5 unrestricted.
  - **Mass land denial** — forbidden B1-B3, allowed B4/B5. Detect Armageddon/Ravages of War/Winter Orb/Static Orb/Jokulhaups class.
  - **Extra turns / chaining** — B1 none, B2/B3 low quantity no chaining, B4/B5 allowed.
- **Soft signals (raise the bracket within the floor):** fast mana density (Sol Ring/Mana Crypt/Moxen — already counted as `FastMana` in `ManabaseDeck`), tutor count, ramp count (already in `ManabaseReport.RampSourceCount`), combo *speed* (combo pieces' average turn).

### Output the user wants
- **Classifier:** the bracket number + the specific reasons (which Game Changers were found, which combos, whether mass-land-denial/extra-turns present). NOT a black-box number.
- **Balancer artifact (paste-ready):** "To move this deck from Bracket 4 to Bracket 3, cut: Vampiric Tutor (Game Changer — over the 3-GC ceiling), Survival of the Fittest (Game Changer), [combo piece X] (enables a turn-4 2-card win). Suggested fair replacements: Fauna Shaman (similar creature tutor, slower, not a GC), Buried Alive (graveyard synergy, not a GC)." The real-world cut pattern (confirmed): swap a Game Changer for a strictly-fairer functional analog.

### Table stakes vs differentiator vs anti-feature
- **Table stakes:** Game Changer detection + bracket number. Multiple free tools do this (ScrollVault, Spellweave, Draftsim, Rate My Decks, commanderbrackets.com).
- **DIFFERENTIATOR (the headline):** the *balancer* — concrete cuts + fair-replacement suggestions to hit a chosen target bracket, delivered as a paste artifact. Research confirms this is uncontested. ScrollVault/Spellweave classify but do NOT prescribe cuts; only manual blog examples exist.
- **Anti-feature:** claiming a deck "IS" bracket N as gospel. WotC is explicit: brackets guide pregame conversation, the table makes the final call. DeckFlow must present the bracket as advisory + show its work, never as a verdict that overrides the pod.

### THE OFFICIAL 5 BRACKETS (cite: WotC Commander Brackets Beta, Oct 2025 + Feb 2026 updates)

| # | Name | Intent | Game Changers | 2-Card Combos | Extra Turns | Mass Land Denial | Tutors | Typical game length |
|---|------|--------|---------------|---------------|-------------|------------------|--------|--------------------|
| 1 | **Exhibition** | Ultra-casual, theme over winning; "stretching card legality is okay" | 0 | None intentional | None | Forbidden | Sparse | 9+ turns |
| 2 | **Core** | Average modern precon power; big turns/engines possible | 0 | None intentional | Low qty, no chaining | Forbidden | Sparse | 8+ turns |
| 3 | **Upgraded** | Tuned beyond precon, selective upgrades | **Up to 3** | No *early-game* combos (no reliable win before ~T6-7) | Low qty, no chaining | Forbidden | 6+ turns |
| 4 | **Optimized** | High-power, strongest cards, explosive starts, no tournament-meta focus | Unrestricted | Allowed | Allowed | Allowed | Fast/proactive |
| 5 | **cEDH** | Competitive, metagame-tuned, winning is the objective | Unrestricted | Allowed | Allowed | Allowed | Any turn |

**CRITICAL UPDATE (Oct 21 2025):** **Tutor restrictions were REMOVED from ALL brackets.** The panel dropped the vague "few tutors" guidance — not all tutors are equally problematic, and the combo restrictions already constrain decks. So the classifier must NOT use raw tutor count as a hard bracket gate; tutors are now only a soft power signal feeding the multi-axis Consistency score. (Earlier 2025 docs that gate on tutor count are STALE.)

### THE GAME CHANGERS LIST (53 cards, as of Feb 9 2026 update)

Base 48-card list after the Oct 21 2025 update, **plus** Feb 9 2026 additions (Farewell, Biorhythm) and other 2025 adds. The Oct-2025 48-card list (HIGH confidence, quoted from the WotC Oct 21 2025 announcement):

> Drannith Magistrate, Humility, Serra's Sanctum, Smothering Tithe, Enlightened Tutor, Teferi's Protection, Consecrated Sphinx, Cyclonic Rift, Force of Will, Fierce Guardianship, Gifts Ungiven, Intuition, Mystical Tutor, Narset Parter of Veils, Rhystic Study, Thassa's Oracle, Ad Nauseam, Bolas's Citadel, Braids Cabal Minion, Demonic Tutor, Imperial Seal, Necropotence, Opposition Agent, Orcish Bowmasters, Tergrid God of Fright, Vampiric Tutor, Gamble, Jeska's Will, Underworld Breach, Crop Rotation, Gaea's Cradle, Natural Order, Seedborn Muse, Survival of the Fittest, Worldly Tutor, Aura Shards, Coalition Victory, Grand Arbiter Augustin IV, Notion Thief, Ancient Tomb, Chrome Mox, Field of the Dead, Glacial Chasm, Grim Monolith, Lion's Eye Diamond, Mana Vault, Mishra's Workshop, Mox Diamond, Panoptic Mirror, The One Ring, The Tabernacle at Pendrell Vale

**Feb 9 2026 update:** **+Farewell** (board-reset; B3+ only), **+Biorhythm** (standard add on un-ban). **Lutri, the Spellchaser explicitly NOT added.** The "53 cards" figure (Spellweave, cross-checked) reflects these plus intermediate 2025 additions. The Oct-2025 removals (do NOT flag these as GCs): Expropriate, Jin-Gitaxias Core Augur, Sway of the Stars, Vorinclex Voice of Hunger, Kinnan Bonder Prodigy, Urza Lord High Artificer, Winota Joiner of Forces, Yuriko the Tiger's Shadow, Deflecting Swat, Food Chain.

> **IMPLEMENTATION NOTE:** the Game Changers list is a LIVING list (updated Feb/Apr/Oct 2025, Feb 2026, with May-June 2026 updates expected). It MUST be data, not code — a versioned, dated, admin-editable table (or seed file), not a hardcoded array. Pin the list version + date in the artifact so a stale classification is auditable. The current month is June 2026; verify the latest list at analysis time and surface the list-date to the user.

### Complexity: MEDIUM (classifier) / HIGH (balancer)
Classifier reuses combo client + fast-mana/ramp counts; the new work is the Game Changers data table + mass-land-denial/extra-turn card detection. Balancer is HIGH — it must rank candidate cuts (GC over ceiling first, then early-combo enablers), justify each, and ideally suggest fair replacements (could lean on the AI round-trip: DeckFlow emits the "here are the floor-violations, ask the AI for fair swaps" artifact rather than computing replacements locally).

---

## FEATURE 2 — Multi-Axis Deck Score (Power / Speed / Control / Consistency, 0-5)

### Expected behavior
Replace/augment single-number power scoring with a 4-axis radar (EDHRank model), each axis 0.0-5.0 (decimals allowed), rolled into the paste packet so the AI reasons about the deck across dimensions rather than one scalar.

### Input signals per axis (cite: EDHRank / mtgmana.rocks — direct source)

| Axis | Definition (EDHRank) | Card signals feeding it (available in DeckFlow today) |
|------|----------------------|-------------------------------------------------------|
| **Power** | Commander's power tier + individual card strength | Command-zone detection (commander identity) + per-card power proxy. *Gap:* DeckFlow has no card-quality DB; EDHREC-popularity or a curated tier table would be needed, OR delegate raw "card strength" judgment to the AI round-trip. |
| **Speed** | Avg mana value + number of mana producers (rocks + dorks) + card-advantage sources | **All available:** `ManabaseDeck.AverageManaValue`, `ManabaseReport.RampSourceCount` (rocks/dorks), `FastMana` count, ramp/draw counts. |
| **Control** | Board-wipe quantity + targeted-answer (removal/counter) density | *Partial:* needs an interaction classifier (board wipes, spot removal, counterspells). DeckFlow has category-knowledge crawl data + can pattern-match oracle text. |
| **Consistency** | Combo density + combo power, tutor count, card-advantage sources | **Mostly available:** CommanderSpellbook combo count (density), tutor detection, draw-piece count (`DrawPieceCount`). |

EDHRank example output (real): Ardenn // Akiri → Power 2.5, Speed 3.0, Control 2.5, Consistency 3.0. Confirms decimal granularity and per-axis independence.

### Output the user wants
- Four numbers + a one-line plain-language read per axis ("Speed 4.2 — heavy fast-mana, low curve; expects to deploy threats ahead of the table").
- In the paste artifact: the four axes + the *signals behind them* so the AI can critique ("your Control is 1.5 because you run only 2 board wipes and 3 spot-removal spells for a deck this combo-dense").

### Table stakes vs differentiator vs anti-feature
- **Table stakes (single number):** every power calculator outputs a 1-10 score (Rate My Decks uses 12 factors → one number; Draftsim; commanderpowermeter; edhpowerlevel). A single number alone is now baseline.
- **DIFFERENTIATOR:** the *4-axis decomposition in a paste artifact*. EDHRank decomposes but does NOT produce an AI-paste artifact; Rate My Decks bundles 12 factors into ONE number (explicitly does NOT split into Power/Speed/Control/Consistency). DeckFlow's edge = 4 axes + the underlying signals fed to the AI for round-trip critique.
- **Anti-feature — false precision / a single "objective power 7.3/10."** Every incumbent that ships one number gets argued with; even EDHRank's author admits "mine has some issues." Present axes as *directional signals with shown inputs*, NOT a definitive rating. Decimals are fine, but the artifact must expose the inputs so the score is contestable, not oracular.

### Complexity: MEDIUM
Speed + Consistency axes are ~80% reuse of existing manabase + combo + ramp/draw signals. Control needs a new interaction classifier (board-wipe / removal / counter detection from oracle text or category-knowledge). Power needs a card-strength proxy — the cleanest path consistent with the thesis is to compute Speed/Control/Consistency locally and let the AI weigh "raw card power" in the round-trip rather than building a card-quality DB. The radar/score is only half-valuable until it is *in the paste packet*.

---

## FEATURE 3 — Auto-Refreshing Primer

### Expected behavior
The Deck Primer artifact (already shipped) becomes deck-version-aware: when the underlying deck changes, the primer is either flagged stale (and the user prompted to regenerate) or selectively regenerated for the affected sections. Closes the universal "primers are manually maintained and decay" gap.

### How primers decay in the wild (cite: Moxfield, BlazeHero guide, MTGSalvation)
- Moxfield primers are **free-text Markdown** with collapsible section tabs (Win Conditions, Mulligan Guide, Game Plan early/mid/late, Matchups, Updates log, Card Synergies). There is **NO automatic link between the decklist and the primer prose** — change a card and the primer text is silently stale.
- Maintenance is fully manual: creators keep an "Updates" section by hand; MTGSalvation's approved-primer list literally tracks "currently being updated to match current changes." **No incumbent auto-refreshes or auto-flags staleness** — this is the open lane.

### Input signals
- DeckFlow already has a **diff engine** (`DiffEngine`, deck-vs-deck reconcile) and **cross-tool deck persistence** (P74, sessionStorage `deckflow.last-deck`). The staleness trigger = diff the current decklist against the decklist the primer was generated from.
- Section-to-card mapping: which primer sections depend on which cards (e.g., a combo-line section depends on the combo pieces; a mana-base section depends on lands; a matchup section is deck-strategy-level and rarely card-specific).

### Output the user wants (concrete refresh semantics — two tiers)
1. **Flag-stale (MVP, LOW-MEDIUM):** store the decklist hash (and/or card-set) the primer was generated from alongside the artifact. On re-open/re-import, diff; if changed, show "This primer was generated from a deck that has since changed: +Card A, -Card B (3 cards differ). Regenerate?" with a one-click regenerate. This is the smallest honest closure of the gap and fits the stateless paste model.
2. **Selective regenerate (differentiator, HIGH):** map sections → card dependencies; when the diff touches only cards in section X, regenerate ONLY section X's prompt artifact (or flag only that section stale), leaving hand-edited sections intact. Matches Moxfield's section-tab structure and respects that creators hand-tune prose they don't want clobbered.

### Table stakes vs differentiator vs anti-feature
- **Table stakes:** primer generation itself (already shipped) + a manual "Updates" log (the universal manual pattern).
- **DIFFERENTIATOR (the headline, per research = DeckFlow's clearest creator lane):** auto-detect staleness via deck diff + regenerate. No incumbent does this. Ties directly to the one-round-trip thesis — the output is a fresh paste-ready primer.
- **Anti-feature — silent full auto-regeneration that clobbers hand-edited prose.** Creators invest hours in primer voice/stories; a tool that silently overwrites their "Card Synergies — that game where X won" loses trust instantly. ALWAYS flag + ask before regenerating; never auto-overwrite. Prefer section-scoped regeneration so hand-edited sections survive a card swap elsewhere.

### Complexity: MEDIUM (flag-stale) / HIGH (section-scoped regenerate)
Flag-stale reuses the diff engine + a stored decklist hash — bounded. Section-scoped regenerate needs a section→card dependency map and per-section prompt assembly, and must honor ADR-0001 (render the regenerated section in all three AI variants with no shared helper).

---

## FEATURE 4 — Tap Analyzer surface (untapped frequency + opening-turn metric)

### Expected behavior
Surface, as discrete reported metrics, how often the deck's lands enter UNTAPPED and how reliably the deck can act on its earliest turns — exposing state the castability engine (P70-72) already simulates but does not currently report as a first-class number. Modeled on Salubrious Snail's Tap Analyzer.

### What Salubrious Snail's Tap Analyzer reports (cite: salubrioussnail.com/manabase-tool)
- "**How often your lands enter untapped, and how that affects your curve out.**" For conditionally-untapped lands (check lands, fast lands, Temple-type taplands, surveil lands), it computes the **probability the deck meets the untapped condition** turn-by-turn, derives the **overall rate at which lands enter tapped**, then **simulates the opening turns** to score early-game casting performance.
- Companion metrics it pairs this with: **Cast Rate** (P(enough mana for an MV-X spell by turn X)) and **Average Delay** (turns waited past turn X) — both of which DeckFlow's `CardCastability` ALREADY computes (`CastPercent`, `OnCurveTurn`, `AverageDelay`). Benchmarks: 90% cast rate / 0.3 avg delay = strong; 80% / 0.6 = needs improvement.

### Input signals (ALL already in DeckFlow's engine)
- `ManaSource.EntersUntapped` (bool, already modeled) and the per-trial land-drop sequence in `CastabilitySimulator` (`CardKind.UntappedLand` vs `TappedLand`). The simulator already distinguishes tapped vs untapped lands per trial — the untapped-frequency and turn-1 metrics are **derivable from existing simulation state, not a new model.**

### Output the user wants
- **Untapped frequency:** % of the deck's mana sources that enter untapped (static count) AND the simulated rate that the land played on a given early turn was available untapped (dynamic). For conditional lands, the probability the untapped condition is met by that turn.
- **Opening-turn metric:** the chance of an untapped colored source on turn 1 (can you actually DO something T1 — cast a T1 play / hold interaction), and the early-turn (T1-T3) tapped-land drag on the curve. This is the "can this deck start on time" readout that the aggregate cast-rate hides.
- In the report + paste packet: a line like "Untapped T1 source: 71% · Tapland drag costs ~0.4 turns of tempo on average · 18 of 38 lands enter conditionally tapped."

### Table stakes vs differentiator vs anti-feature
- **Table stakes:** none — almost no tool reports untapped frequency; this is rare.
- **DIFFERENTIATOR:** only Salubrious Snail ships it. DeckFlow already has the simulation substrate, so surfacing it is low marginal cost for a high-distinctiveness metric. Strengthens the existing manabase differentiator.
- **Anti-feature — rebuilding the castability ENGINE.** Explicitly out of scope (PROJECT.md). This is a READOUT of P70-72 state, not a new simulator. Also avoid an over-precise single "tempo score" — present the untapped-frequency + T1-availability as plain numbers with the benchmark, consistent with the existing plain-language verdict (P71).

### Complexity: LOW-MEDIUM
The simulation already classifies lands tapped/untapped per trial and tracks first-castable turns. The work is (a) instrument the simulator to emit untapped-frequency + turn-1-availability aggregates, (b) add report/view fields, (c) add the paste-packet line in all three AI variants. No new mathematical model.

---

## Feature Dependencies

```
Bracket Classifier
    └──requires──> Game Changers data table (NEW — versioned/dated, admin-editable)
    └──requires──> CommanderSpellbook combo client (EXISTS) — for combo-floor + early-combo detection
    └──requires──> Fast-mana / ramp counts (EXIST in ManabaseDeck/ManabaseReport)
    └──requires──> Mass-land-denial + extra-turn card detection (NEW — oracle-text/curated)

Bracket Balancer
    └──requires──> Bracket Classifier (must know current bracket + which floors are violated)
    └──requires──> AI round-trip for fair-replacement suggestions (paste artifact)

Multi-Axis Deck Score
    └──Speed axis──> AverageManaValue + RampSourceCount + FastMana (EXIST)
    └──Consistency axis──> CommanderSpellbook combo count (EXISTS) + tutor + draw counts
    └──Control axis──> Interaction classifier (NEW — board-wipe/removal/counter)
    └──Power axis──> Command-zone detection (EXISTS) + card-strength proxy (GAP → delegate to AI)

Auto-Refreshing Primer
    └──requires──> Deck Primer generator (EXISTS)
    └──requires──> DiffEngine + stored decklist hash (EXISTS / small add) — staleness trigger
    └──enhanced-by──> section→card dependency map (NEW — for selective regenerate)

Tap Analyzer surface
    └──requires──> CastabilitySimulator tapped/untapped + first-castable state (EXISTS)
    └──requires──> new aggregate emit + report fields + paste-packet line (NEW, small)

CROSS-CUTTING:
    Bracket Classifier ──feeds──> Multi-Axis Score (GC count is a Power/Consistency signal)
    Bracket Classifier ──feeds──> Deck Primer (bracket presets already exist; auto-classify the bracket)
    Multi-Axis Score ──feeds──> Deck Primer (axes enrich the primer's "power level" section)
    ALL paste artifacts ──MUST──> render ChatGpt/Claude/Gemini variants, NO shared helper (ADR-0001)
```

### Dependency notes
- **Balancer requires Classifier:** you cannot prescribe cuts to a target bracket until you know the current bracket and exactly which floors are violated.
- **Bracket Classifier feeds the Primer:** the Primer already has bracket presets the user picks manually; the Classifier can auto-suggest the bracket, removing a manual step.
- **Multi-Axis Score shares signals with Classifier:** Game Changer count, fast mana, combo density all feed both — compute once, consume in both. Sequence Classifier and Score in the same or adjacent phases to share the signal-extraction layer.
- **Tap Analyzer is independent** of the other three — it only touches the manabase engine and can ship in any order.

---

## MVP Definition

### Launch With (Cycle 13 core)
- [ ] **Bracket Classifier** — Game Changers data table (versioned) + hard-floor detection + bracket number with shown reasons. The classifier is the foundation the balancer needs.
- [ ] **Bracket Balancer paste artifact** — "cuts to hit target bracket N" with per-cut justification; this is the uncontested headline differentiator and the clearest thesis fit.
- [ ] **Multi-Axis Score (Speed + Consistency first)** — these two axes are ~80% existing-signal reuse; ship them in the paste packet immediately.
- [ ] **Tap Analyzer surface** — low-cost readout of existing P70-72 simulation state; strengthens the manabase differentiator.

### Add After Core (Cycle 13 complete if schedule allows)
- [ ] **Multi-Axis Control + Power axes** — Control needs a new interaction classifier; Power leans on the AI round-trip. Complete the 4-axis radar once Speed/Consistency are proven.
- [ ] **Auto-Refreshing Primer (flag-stale tier)** — stored decklist hash + diff + "regenerate?" prompt. Smallest honest closure of the decay gap.

### Future Consideration (next cycle)
- [ ] **Auto-Refreshing Primer (section-scoped regenerate)** — section→card dependency map; high complexity, defer until flag-stale validates the workflow.
- [ ] **Bracket Balancer fair-replacement automation** — local replacement-suggestion engine (vs delegating to the AI round-trip); only if the AI-delegated version proves insufficient.

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority | Reuse vs New |
|---------|------------|---------------------|----------|--------------|
| Bracket Classifier (GC table + floors + number) | HIGH | MEDIUM | P1 | 60% reuse (combo client, fast-mana/ramp counts) + new GC data table |
| Bracket Balancer artifact (cuts to target) | HIGH | HIGH | P1 | New ranking logic; delegates fair-swaps to AI round-trip |
| Multi-Axis Score — Speed + Consistency | HIGH | MEDIUM | P1 | 80% reuse (manabase + combo + ramp/draw signals) |
| Tap Analyzer surface | MEDIUM-HIGH | LOW-MEDIUM | P1 | 90% reuse (simulator already classifies tapped/untapped) |
| Multi-Axis Score — Control axis | MEDIUM | MEDIUM | P2 | New interaction classifier (board-wipe/removal/counter) |
| Multi-Axis Score — Power axis | MEDIUM | MEDIUM | P2 | Command-zone reuse + AI-delegated card-strength |
| Auto-Refreshing Primer — flag-stale | HIGH | MEDIUM | P2 | 70% reuse (DiffEngine + Primer); new stored-hash + prompt |
| Auto-Refreshing Primer — section-scoped regen | HIGH | HIGH | P3 | New section→card dependency map |

**Priority key:** P1 = Cycle 13 MVP; P2 = complete in Cycle 13 if schedule allows; P3 = defer.

---

## Competitor Feature Analysis

| Feature | Incumbents | Their approach | DeckFlow's approach |
|---------|-----------|----------------|---------------------|
| Bracket classification | ScrollVault, Spellweave, Draftsim, Rate My Decks, commanderbrackets.com | Paste deck → bracket number (+ some show GC count, combo detection, goldfish clock). Stop at the number. | Classify + **show work** + emit a paste artifact. Advisory, not verdict. |
| Bracket *balancing* (cuts to target) | **None** (only manual blog examples) | Manual: human picks a GC and swaps a fairer analog | **Auto-generate the cut list + justification as a paste artifact** — uncontested lane |
| Multi-axis score | EDHRank (mtgmana.rocks) | 4 axes (Power/Speed/Control/Consistency 0-5) on a web page; no AI artifact | Same 4 axes **+ underlying signals in the paste packet** for AI round-trip critique |
| Single power number | Rate My Decks (12 factors→1), Draftsim, edhpowerlevel, commanderpowermeter | One 1-10 number | Decompose; expose inputs; avoid false-precision verdict |
| Auto-refreshing primer | **None** (Moxfield = manual Markdown; AI deck tools offer "refine primer" but not deck-diff-triggered refresh) | Manual updates log; hand-maintained | **Deck-diff-triggered staleness flag + regenerate** — DeckFlow's clearest creator lane |
| Tap / untapped analysis | Salubrious Snail only | Untapped-condition probability + opening-turn sim | Surface existing P70-72 simulation state as a first-class metric |

---

## Sources

- WotC — Introducing Commander Brackets Beta: https://magic.wizards.com/en/news/announcements/introducing-commander-brackets-beta (HIGH — official bracket names/intent + per-bracket rule grid + initial 40-card GC list)
- WotC — Commander Brackets Beta Update Oct 21 2025: https://magic.wizards.com/en/news/announcements/commander-brackets-beta-update-october-21-2025 (HIGH — tutor restrictions REMOVED, 10 GCs removed, 48-card list quoted, turn-count clarification)
- WotC — Commander Brackets Beta Update Feb 9 2026: https://magic.wizards.com/en/news/announcements/commander-brackets-beta-update-february-9-2026 (HIGH — +Farewell, +Biorhythm, Lutri excluded, hybrid-mana shelved)
- Spellweave — Commander Brackets 2026 guide: https://spellweave.app/guides/commander-brackets (MEDIUM — "53 cards as of Feb 2026" figure, bracket combo-timing table)
- ScrollVault — bracket calculator + how-it-works: https://scrollvault.net/tools/commander-bracket/ , https://scrollvault.net/posts/how-scrollvault-bracket-calculator-works.html (MEDIUM — classifier pipeline: hard floors + soft signals + goldfish sim; no cut suggestions)
- EDHRank — mtgmana.rocks: https://mtgmana.rocks/tool_edhrank.html (HIGH — 4-axis definitions + per-axis card signals + decimal example)
- Rate My Decks: https://www.ratemydecks.com/en (MEDIUM — 12 factors → ONE number, confirms it does NOT split into 4 axes)
- Draftsim EDH power level: https://draftsim.com/edh-power-level/ (MEDIUM — single-axis GC-count + combo-speed model)
- Salubrious Snail — manabase tool: https://www.salubrioussnail.com/manabase-tool (HIGH — Tap Analyzer untapped-frequency + opening-turn sim, cast-rate/avg-delay benchmarks)
- Moxfield — writing primers + BlazeHero guide: https://moxfield.com/help/writing-primers , https://moxfield.com/decks/icKufeoz_U-4HMNlorzgnw/primer (MEDIUM — section structure; manual, no deck-link/staleness)
- MTGSalvation primer status thread: https://www.mtgsalvation.com/forums/the-game/commander-edh/543231 (LOW — confirms manual "updating to match changes" decay pattern)
- DeckFlow codebase: `DeckFlow.Core/Manabase/{ManabaseModels,CastabilitySimulator}.cs` (HIGH — `EntersUntapped`, tapped/untapped per-trial, `CardCastability` cast%/on-curve/avg-delay, ramp/fast-mana counts already exist)
- Prior research: `scratchpad-research/commander-feature-wants-report.md` (the Cycle 13 feature-gap basis)

---
*Feature research for: DeckFlow Cycle 13 — Deck Evaluation & Creator Output*
*Researched: 2026-06-27*
