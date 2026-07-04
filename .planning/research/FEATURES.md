# Feature Research — Cycle 14 "Deeper Deck Evaluation"

**Domain:** MTG Commander / cEDH deck-evaluation (paste-to-AI artifact engine)
**Researched:** 2026-06-30
**Confidence:** HIGH (interaction taxonomy + win-line archetypes + mulligan keep heuristics grounded in current Commander/cEDH sources; engine-dependency notes verified against the `deckflow-cycle14` worktree)

---

## Scope

Three new **read-only** deck-eval dimensions, each flag-gated, byte-identical when OFF, each layered on an
already-shipped engine piece:

1. **Interaction & answers audit** — finer taxonomy over the existing coarse `DeckStatClassifier.IsInteractionCard`.
2. **Win-condition & combo map** — deeper use of `CommanderSpellbookService` + `IsClosingPowerCard`.
3. **Opening-hand / mulligan evaluator** — a readout off the existing Monte-Carlo `CastabilitySimulator`
   (which already simulates a London-mulligan opening-hand keep decision today).

Each is a **paste-artifact section + a view readout**, NOT a new tool. The bar is the project's core value:
*useful in one ChatGPT/Claude/Gemini round-trip, no reformatting.* Output must be compact, labeled, and
self-explanatory — never a raw dump. Every artifact renders in all three AI variants WITHOUT a shared helper (ADR-0001).

---

## What already exists in DeckFlow (do NOT re-spec)

| Engine piece | What it gives Cycle 14 | Location (verified) |
|--------------|------------------------|---------------------|
| `DeckStatClassifier` | Coarse role booleans: `IsInteractionCard` (OR-of-everything), `IsBoardWipeCard`, `IsRecursionCard`, `IsRampCard`, `IsDrawCard`, `IsClosingPowerCard` | `DeckFlow.Core/Analysis/DeckStatClassifier.cs` |
| `CastabilitySimulator` | Seeded Monte-Carlo that **already draws openers and runs a London-mulligan keep/mull decision**, incl. a color-aware keep gate `ColorKeepSatisfiedForTest` (threshold `min(colors, lands, 2)`) and land+ramp keep logic | `DeckFlow.Core/Manabase/` (`ColorAwareMulliganTests`, `LandRampSimTests`) |
| `CommanderSpellbookService` | Combos present in the deck + "almost there" / missing-piece reads; returns `null` on API failure (graceful) | `DeckFlow.Web/Services/CommanderSpellbookService.cs` |
| Multi-axis score (P77) | Power / Speed / Control / Consistency 0-5 bands already in `/deck-analysis` + 3 paste variants | shipped Cycle 13 |
| Tutor / ramp counts | Already counted for the manabase + score work | `ManabaseReport`, classifier |

**Key insight:** the mulligan evaluator is the lowest-risk of the three — the simulator *already* makes the keep
decision internally; Cycle 14 surfaces the rate it already computes rather than building a second simulation.

---

## How These Three Features Work (Domain Grounding)

### 1. Interaction & answers audit — the standard taxonomy

Serious players bucket interaction into a small, well-understood set. The audit **counts by bucket** and **flags
coverage gaps** ("what can't this deck answer?"). It does NOT grade individual cards.

| Bucket | Answers… | Oracle/type signal (heuristic) | Notes |
|--------|----------|-------------------------------|-------|
| **Targeted removal (spot)** | a *resolved* permanent | `destroy target`, `exile target`, `return target … to … hand` (bounce), `target creature gets -X/-X`, `fight` | Sub-read matters: creature-only vs. catch-all ("exile target permanent"). cEDH values *unconditional* + *instant-speed*. |
| **Counterspells (stack)** | a spell *before* it resolves | `counter target spell` / `counter target … ability` | Most-valued bucket in cEDH. Sub-flag "noncreature only" (a real, commonly-cited gap — most counters can't stop a creature). |
| **Board wipes (mass)** | many permanents at once | `destroy all`, `exile all`, `each creature gets -X`, "all creatures get" | Already covered by `IsBoardWipeCard` — reuse. Casual leans on these; cEDH runs few. |
| **Stax / taxation / denial** | opponents *acting at all* | "can't", "don't untap", "costs {1} more", "skip", "players can't search" | Hardest from text; high false-positive risk → **coarse presence read only** (see anti-features). |
| **Protection / resilience** | keeping *your own* engine alive | "hexproof", "indestructible", "can't be countered", "protection from", "ward", "return it to the battlefield" | Interaction *for your win*, not against theirs. Keep a **distinct** bucket — cEDH measures "counter-to-their-counter." |
| **Graveyard / recursion answers** | graveyard engines + reuse | "exile … graveyard" (hate) + existing `IsRecursionCard` (your own reuse) | Optional sub-bucket; graveyard *hate* is a common coverage gap. |

**Counting in practice:** each mainboard card is run through the bucket predicates against its normalized Scryfall
`oracleText` + `typeLine`. A card can land in multiple buckets (a modal "counter target spell or destroy target
creature" counts as both). Report **per-bucket counts + total interaction density**, then a **gap-flags line**
("0 counterspells", "no catch-all removal", "no graveyard hate"). Gap flags are the high-value output; raw counts are
table stakes.

**Reference density (orientation, not a grade):** casual decks run ~8-12 removal/counters combined; cEDH runs a much
higher density weighted toward counters + cheap unconditional removal. (Commander's Herald, EDHREC stax guide, TCGplayer.)

### 2. Win-condition & combo map — enumerating + classifying win lines

The map **names the deck's win lines, counts redundancy, and gives an assembly read** — "how does this deck actually
win, and how reliably?"

**Win-line archetypes (classification buckets):**
- **Infinite combos** — deterministic loops. cEDH canonical packages: *Thassa's Oracle* (mill-yourself, e.g. Thoracle
  + Demonic Consultation / Tainted Pact), *Underworld Breach* (graveyard-as-second-hand), *infinite mana* → converted
  to draw/damage/a wincon. (Draftsim cEDH wincons, Laboratory Maniacs, Learn cEDH.)
- **Value / engine wins** — incremental advantage that eventually closes (extra turns, damage doublers, card-advantage
  engines). Detected today by `IsClosingPowerCard`.
- **Commander damage / combat** — 21 commander damage, evasion + pump, go-wide tokens.
- **Alt-win / "you win the game" cards** — explicit text wins (Thoracle, Approach, lab-man).

**Redundancy & assembly — the genuinely useful reads:**
- **Redundancy** = independent copies/substitutes of a win line. cEDH idiom = *layered combos*: multiple combos sharing
  overlapping pieces so one removal spell doesn't shut you off. Reporting "win line X: 3 redundant enablers; win line Y:
  1 (fragile)" is the differentiator. (Learn cEDH "How Many Combos Are Too Many", Plagon primer.)
- **Assembly turn** = earliest realistic turn the combo comes online. A precise turn is unknowable without a full game
  sim → frame as a **band** ("early / mid / late", or "T2-4 with a tutor") derived from combined piece MV + the deck's
  tutor/ramp density (already counted). **Never** a hard turn number (false precision, breaks trust).
- **Tutorability** = can the deck *find* the missing piece? Tie to existing tutor count. Two specific cards + no tutors
  = "fragile"; one piece + 6 tutors = "consistent."

**Data source:** `CommanderSpellbookService` already returns the deck's combos with missing-piece reads. Cycle 14
deepens this: group into the archetype buckets, dedupe shared pieces, synthesize the redundancy + assembly-band line.
The `SpellbookCombo` ranking fields (`manaValueNeeded` / popularity / uses) are **parsed and dropped today** (known
backlog) — capturing `manaValueNeeded` directly sharpens the assembly band.

### 3. Opening-hand / mulligan evaluator — what makes a hand keepable

A Commander opener is "keepable" when it can **execute the deck's plan**, which players reduce to four checks:
- **Lands:** 3-4 is the standard functional keep; 2 keepable *with* ramp or cheap draw; 1 only for very low-curve decks;
  5+ flooded. (Draftsim mulligan rules, MTG EDH, EDHREC mulligan guide.)
- **Color access:** lands must produce the colors the hand's spells need (sim already enforces this via
  `ColorKeepSatisfiedForTest`, threshold `min(colors, lands, 2)`).
- **Ramp / acceleration:** a 2-land hand wants ramp to reach the curve.
- **A plan:** at least one meaningful action by ~turn 3 (control wants removal/a counter; combo wants setup/selection;
  aggro wants early threats).

**Framing "keepable-hand probability":** the sim is *already doing this internally* to decide keep/mull per opener.
Cycle 14 surfaces it as a discrete metric: **"~X% of opening hands are keepable"** (London mulligan; multiplayer
free-first-mull baseline) plus a **color/curve read** ("most keeps are land-light", "double-color hands rare on the
play"). Frame it as a **consistency signal** beside the existing Consistency axis — NOT a play/draw decision engine.
It answers "how often does this deck function out of the gate?", exactly the consistency question builders ask.

---

## Feature Landscape

### Table Stakes (Users Expect These)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Interaction counted by **named bucket** (removal / counters / wipes / protection / stax) | A single "interaction: 14" number is uninformative; players think in buckets | MEDIUM | Refine the coarse `IsInteractionCard` into per-bucket predicates; reuse `IsBoardWipeCard`/`IsRecursionCard` |
| Interaction **gap flags** ("0 counterspells", "no catch-all removal") | The whole point of an audit is finding holes | LOW | Derived from bucket counts; cheap once buckets exist |
| Win lines **named + grouped** (combo / value / commander-damage) | "How does this deck win" is the first eval question | MEDIUM | Group Spellbook combos + `IsClosingPowerCard` hits into archetype buckets |
| Combo **redundancy count** ("3 ways to assemble") | Single-combo fragility is the #1 cEDH deckbuilding concern | MEDIUM | Dedupe shared pieces across returned combos |
| **Keepable-hand %** from the existing sim | Consistency is one of the four already-shipped score axes | LOW | Sim already computes keep/mull per opener — surface the rate, don't rebuild |
| Color/curve read on opening hands | "land-light / color-screwed" is the common failure mode | LOW | Read off the same sim's per-opener color + land tallies |
| Compact, labeled paste-artifact section per feature | Core value: paste once, no reformatting | LOW-MED | Must render in all 3 AI variants WITHOUT a shared helper (ADR-0001) |

### Differentiators (Competitive Advantage)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Coverage-gap synthesis** ("can't answer resolved creatures; no graveyard hate") | No incumbent (EDHREC/Moxfield/Archidekt) tells you what your deck *can't* deal with | MEDIUM | Cross-bucket reasoning; the single highest-value output of the audit |
| **"How this deck wins" narrative** (win line + redundancy + tutorability + assembly band) | cedh-decklist-database does this *by hand* in primers; nobody auto-generates it | MEDIUM-HIGH | Combines combo map + tutor/ramp counts + MV bands into one prose-ready synthesis the AI expands |
| **Assembly-turn band** off MV + tutor density | A "comes online ~T3-4" read is primer-grade insight | MEDIUM | Must be a band, never a number. Capturing dropped `manaValueNeeded` sharpens it |
| **Keepable-hand % as a named eval metric** | Salubrious Snail sims the manabase but doesn't expose a "functional opener" number; the framing is novel | LOW-MED | Reuses the existing sim; the framing is the differentiator, not new compute |
| Protection as its **own** bucket (resilience ≠ removal) | cEDH separates "answers" from "protect-my-win"; lumping hides a real axis | LOW | A distinct predicate set; cheap once the taxonomy exists |
| All three **feeding the multi-axis score narrative** | Interaction → Control axis; keepable% → Consistency; combo map → Speed/Power | LOW | Optional cross-wiring; reinforces the shipped score |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **Exhaustive stax classification** (every tax/denial sub-type) | Stax is "interaction" too | Stax text is wildly heterogeneous; text heuristics produce high false-positive noise and look broken when wrong | Report a coarse **stax-presence (low/med/high)** read only; let the AI prose handle nuance |
| **Hard assembly-turn number** ("wins turn 3.4") | Sounds precise/authoritative | Real turn depends on draws/interaction/pod — false precision erodes trust the first time it's wrong | Turn **band** (early/mid/late, "T2-4 with a tutor") |
| **Per-card "is this good interaction?" grading** | Players love tier lists | Subjective, meta-dependent, instantly contested, maintenance treadmill | Count + categorize objectively; leave quality judgment to the AI round-trip |
| **Mulligan *decisions* / play-vs-draw advisor** ("keep this hand") | "Should I keep?" feels like the next step | Turns a deck stat into a per-hand coach — huge scope, needs real-time hand input, off-thesis | Aggregate **keepable %** only; it's a deck property, not a per-game tool |
| **Win-probability / win-rate %** per deck | Everyone wants a win-rate | Needs opponent modeling + game sim; meta sites explicitly *refuse* to publish win-rates because they can't | Speed/consistency **bands** + assembly reads, never a win-% |
| **Live game-state / board / combo tracker** | "Track my combo mid-game" | Out of band — this is a static deck-eval engine, not a play companion | Out of scope; defer permanently |
| **"Fix my interaction" auto-suggestions (cuts/adds)** | Natural follow-on from gap-flagging | Recommendation engine = different product surface; the thesis is *the AI recommends* off the paste artifact | Surface the gap; let ChatGPT propose cuts/adds in the round-trip |

---

## Feature Dependencies

```
Interaction & answers audit
    └──refines──> DeckStatClassifier.IsInteractionCard (coarse → bucketed)
                      └──reuses──> IsBoardWipeCard, IsRecursionCard

Win-condition & combo map
    └──requires──> CommanderSpellbookService (combo lookup, already wired, null-graceful)
    └──reuses────> DeckStatClassifier.IsClosingPowerCard
    └──enhanced-by──> SpellbookCombo.manaValueNeeded (currently dropped by parser — backlog)
    └──reuses────> existing tutor/ramp counts (assembly-band + redundancy input)

Opening-hand / mulligan evaluator
    └──reads-off──> CastabilitySimulator Monte-Carlo opener loop
                      └──already-has──> London-mulligan keep/mull logic
                      └──already-has──> color-aware keep gate (ColorKeepSatisfiedForTest)

All three
    └──render-into──> /deck-analysis view + 3 AI paste variants (ADR-0001: NO shared helper)
    └──gated-by──> per-feature flag, seeded OFF, byte-identical when OFF
    └──feed (optional)──> existing multi-axis score narrative
```

### Dependency Notes

- **Interaction audit refines, doesn't replace, `DeckStatClassifier`.** Current `IsInteractionCard` is a single
  OR-of-everything boolean; Cycle 14 needs *separable* predicates (removal vs. counter vs. protection vs. stax) so a
  card is counted into the right bucket(s). Board-wipe and recursion predicates already exist — reuse, don't re-implement.
- **Combo map depends on Spellbook being live and graceful-null.** `FindCombosAsync` returns `null` on API failure;
  the map must degrade to "combo data unavailable" rather than erroring (matches the existing pattern).
- **Mulligan evaluator is a *readout*, not new compute.** The simulator already draws openers and runs a London-mulligan
  keep decision (verified: `ColorAwareMulliganTests`, `LandRampSimTests`). Cheapest correct design exposes the keep-rate
  the sim already determines rather than adding a second simulation. **Lowest-risk of the three.**
- **Assembly band needs the dropped ranking field to be sharp.** `manaValueNeeded` is parsed and discarded today;
  capturing it (small parser change) turns the band from "guessed from piece MV" into "grounded in Spellbook's needed
  mana" — a worthwhile prerequisite for a strong combo-map read.

---

## MVP Definition

### Launch With (this cycle)

- [ ] **Bucketed interaction counts + gap flags** — the audit's core; refines `IsInteractionCard` into named buckets
- [ ] **Win-line grouping + redundancy count** — names how the deck wins and how many ways
- [ ] **Keepable-hand % + color/curve read** — surfaced from the existing sim (lowest cost, high value)
- [ ] **One compact paste-artifact section per feature**, rendered in all 3 AI variants, flag-gated OFF
- [ ] **Graceful degradation** when Spellbook is unavailable / deck has no detectable win line

### Add After Validation (next cycle)

- [ ] **Assembly-turn band sharpened by captured `manaValueNeeded`** — after the parser-field capture lands
- [ ] **Cross-wire all three into the multi-axis score narrative** — once each reads cleanly on its own
- [ ] **Graveyard-hate sub-bucket** in the interaction audit — if users ask for it

### Future Consideration (defer)

- [ ] **Matchup / meta-threat read** — explicitly out of scope (deepens cedh-meta-gap, a separate lane)
- [ ] **Stax fine-classification** — only if a robust (non-text-heuristic) data source appears
- [ ] **Interaction "fix" suggestions** — belongs to the AI round-trip, not the engine

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Keepable-hand % + color/curve read | HIGH | LOW | P1 |
| Bucketed interaction counts | HIGH | MEDIUM | P1 |
| Interaction gap-flag synthesis | HIGH | LOW | P1 |
| Win-line grouping + redundancy | HIGH | MEDIUM | P1 |
| Assembly-turn band (MV/tutor-derived) | MEDIUM | MEDIUM | P2 |
| Capture dropped `SpellbookCombo` ranking fields | MEDIUM | LOW | P2 |
| Protection-as-own-bucket | MEDIUM | LOW | P2 |
| Cross-wire into multi-axis score | MEDIUM | LOW | P3 |
| Stax fine-classification | LOW | HIGH | P3 (anti) |
| Mulligan per-hand advisor | LOW | HIGH | P3 (anti) |

**Priority key:** P1 = launch this cycle; P2 = add if schedule allows; P3 = defer / anti-feature.

## Competitor Feature Analysis

| Feature | EDHREC / Moxfield / Archidekt | cEDH primers (cedh-decklist-database) | DeckFlow's Approach |
|---------|-------------------------------|----------------------------------------|---------------------|
| Interaction audit | Archidekt auto-categorizes (Evasion/Protection) but rated "obnoxious"; no gap analysis | Hand-written in prose | Auto bucket-count **+ gap flags**, paste-ready |
| Win-con / combo map | Commander Spellbook = combo DB only, no redundancy/assembly read | Hand-written win-line + combo-line writeups | Auto-grouped win lines **+ redundancy + assembly band** |
| Mulligan / opener | None expose a "keepable %"; Salubrious Snail sims manabase but not framed as an opener metric | Primers list mulligan priorities by hand | **Keepable-hand %** as a named eval stat off the existing sim |

## Sources

- [The Counterspell Conundrum — Commander's Herald](https://commandersherald.com/the-counterspell-conundrum-rethinking-removal/)
- [EDHREC Guide to cEDH Stax](https://edhrec.com/guides/edhrec-guide-to-cedh-stax)
- [What is cEDH? — TCGplayer](https://www.tcgplayer.com/content/article/What-is-cEDH-An-Intro-to-Playing-Competitive-Commander/b9936e3b-6591-44c8-a0a1-902ecc12066f/)
- [The 7 Best Wincons in cEDH Ranked — Draftsim](https://draftsim.com/cedh-win-conditions-mtg/)
- [How Many Combos Are Too Many? — Learn cEDH](https://learncedh.com/intermediate-course/how-many-combos-are-too-many)
- [cEDH 101: Combos and Finishers — Laboratory Maniacs](https://labmaniacs.com/cedh-101-combos-and-finishers/)
- [Mulligans in Commander — Draftsim](https://draftsim.com/mtg-commander-mulligan-rules/)
- [MTG Commander Mulligan Guide — MTG EDH](https://mtgedh.com/mtg-commander-mulligan-guide-keep-or-ship/)
- [The EDHREC Guide to Mulligans](https://edhrec.com/guides/the-edhrec-guide-to-mulligans-in-commander)
- [Counter — MTG Wiki](https://mtg.fandom.com/wiki/Counter)
- Prior research: `.planning/research/commander-feature-wants-report.md`
- Worktree engine pieces verified: `DeckStatClassifier.cs`, `CastabilitySimulator` (ColorAwareMulligan/LandRampSim tests), `CommanderSpellbookService.cs`

---
*Feature research for: MTG Commander/cEDH deeper deck-evaluation (Cycle 14)*
*Researched: 2026-06-30*
