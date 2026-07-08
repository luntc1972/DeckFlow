# Research: Does an opening hand "have a plan," and can we measure it?

**Date:** 2026-07-07
**Context:** DeckFlow manabase tool already reports keepable-hand % (Karsten-style, land-count London-mulligan sim) and on-curve castability. Question: can we add a stat for whether an opener holds a *coherent proactive game plan*, not just castable lands/spells — and is it actionable or noise?
**Method:** deep-research harness — 5 search angles, 20 sources, 92 extracted claims, 25 adversarially verified (3-vote, need 2/3 to kill). 21 confirmed, 1 refuted, 3 unverified (verify agents hit a session limit). Findings below are the confirmed set unless flagged.

## TLDR

"Has a plan" **is** measurable — but only as **role-coverage** (does the opener contain a card that advances the deck's actual win plan), and only **deck-specifically**. No generic off-the-shelf metric exists. Every working implementation encodes "plan" as role coverage and hard-codes it per deck. Keepable-% and on-curve castability measure *resources/curve* — a genuinely different axis, confirmed. Useful as a **deck-level consistency stat**; NOT useful as per-hand keep/mull advice (context-dependence kills that). DeckFlow's category-KB + Commander Spellbook knowledge is the moat that solves the per-deck generalization the open-source tools couldn't.

## 1. What pros mean by "plan" (hand-evaluation theory)

- **Reid Duke** (WotC canonical mulligan series): hand evaluation = "What is your game plan? What tools do you need to achieve it?" Plan is an explicit criterion beyond land count. Single key-card presence can dominate hand value (Bogles mulligans most hands lacking a hexproof creature). Early castability gets premium weight; mulligan more aggressively vs fast decks. [3-0] — https://magic.wizards.com/en/news/feature/mulligans-part-iii-constructed-2015-06-29
- **Matt Sperling** (cEDH, TopDeck.gg): ranked keep hierarchy — (1) Truly Broken Things, (2) Development (mana + critical permanents), (3) Interaction, (4) Card Advantage, (5) Non-Functional-Hand Avoidance. Most directly encodable cEDH framework. [unverified — verify agents errored] — https://topdeck.gg/articles/can-i-keep-this-how-to-mulligan-cedh
- **PVDDR**: every keep/mull scenario requires format, matchup, play/draw, game number. Hand quality is **context-dependent, not hand-intrinsic** — the fundamental limit on any matchup-agnostic "plan score." [2-1] — https://pvddr.substack.com/p/keep-or-mulligan-1

## 2. Existing quantifications (all 3-0 unless noted)

| Tool | Measures | Plan-aware? |
|---|---|---|
| Karsten keepable-hand | 2-3 lands + 2+ spells | No — pure resource count (DeckFlow's current stat class) |
| Karsten `optimal_curve_commander.py` | cumulative mana spent over 7 turns | Velocity proxy; keep rule role-aware (keep any 1+ land hand w/ Sol Ring) |
| mtgoncurve / landlord (Rust OSS) | per-card on-curve probability incl. mulligan sim | No — curve only; README explicitly has no plan/synergy heuristics |
| Goldfisher (Rust OSS) | avg winning turn, goldfish games | Yes — but `is_keepable_hand` **hand-coded per deck** (Pattern Combo keep = ≥1 Pattern/Rector AND ≥1 sac outlet). Direct evidence plan-eval is deck-specific |
| EmrXald EDH sim | % openers with exact Flash Hulk package | Yes — combo-presence; author concedes overly strict + hard-coded |
| Noah-R Mulligan-Decider | NN win-prob if kept (17lands data) | Collapses plan into learned scalar [unverified]; Limited-only, no Commander analog |
| mtg-mulligan.com | GPT-5 derives archetype/plan/key-cards from decklist | LLM approach [2-0 exists]; methodology opaque |

**Refuted [0-3]:** that mulligan policy can be derived from a single objective (max expected mana) with no plan notion. Verifiers killed it — mana-max alone insufficient.

**Academia:** MTG RL benchmark (arXiv 2605.06066) models mulligan as bare KEEP/MULL/BOTTOM; baseline agents keep on land-count ranges per archetype (1-5 aggro, 2-5 control). No published "hand coherence" metric. [3-0]

## 3. Measurable proxies, ranked for DeckFlow

1. **Role-coverage %** (best fit) — Monte Carlo you already run + per-card role tags: % of keepable openers holding ≥1 win-directed card castable on curve. This is Goldfisher/Sperling formalized. DeckFlow's category-knowledge store + Commander Spellbook = the generalization the OSS tools hard-coded. The moat.
2. **Sperling-tier coverage vector** — per opener flag development/interaction/card-advantage/broken-start. cEDH-native language.
3. **Synergy-pair presence** — % openers with ≥1 known synergy pair. Noisier; second phase.
4. **Velocity** (already ~have) — Karsten compounded-mana-spent ≈ on-curve cast rate.

## 4. Honest assessment: useful or noise?

**Useful — with framing discipline:**
- As a **deck-level consistency stat** ("94% of keepable hands contain a plan piece; cutting 3 payoffs for interaction drops it to 81%") — actionable, changes deckbuilding, differentiates two decks with identical land counts + curves. No Commander tool ships this.
- Slots into the existing OPENING HAND box: today "keepable" (resources) → add "with a plan" (function). Karsten evidence confirms these are different measurements — a 3-land/4-spell hand of removal+ramp with zero win-directed cards is keepable but planless.

**Noise — if overreached:**
- Per-hand keep/mull advice: PVDDR context-dependence means a hand-intrinsic score contradicts correct decisions often enough to erode trust. Keep the existing disclaimer ("consistency signal, not a keep/mulligan recommendation").
- Single blended 0-100 score hides which role is missing — report role components, not a composite.
- Role-tagging quality is the whole game. Misclassified roles → confidently wrong numbers, worse than no stat. Beta-flag like manabase.

## Sources (20 fetched; primary in bold)

- **magic.wizards.com/en/news/feature/mulligans-part-iii-constructed-2015-06-29** (Reid Duke)
- pvddr.substack.com/p/keep-or-mulligan-1
- **github.com/frankkarsten/MTG-Math**
- **mtgoncurve.com/about** · **github.com/mtgoncurve/landlord**
- **github.com/Cadiac/goldfisher**
- github.com/EmrXald/MTG_Mulligan_Simulation · github.com/nylonhat/MTG-Manacurve-simulation
- github.com/Noah-R/Mulligan-Decider
- mtg-mulligan.com · eldrazi.gg
- **blog.17lands.com/posts/london-mulligan/** · mtgds.wordpress.com (adjusted win rate)
- **arxiv.org/html/2605.06066** (MTG RL benchmark) · **arxiv.org/abs/2407.05879** (draft-choice NN) · **arxiv.org/abs/1810.03744** (card CNN/RNN)
- topdeck.gg/articles/can-i-keep-this-how-to-mulligan-cedh (Sperling) · grimdeck.com/blog/how-to-mulligan-commander · edhrec.com/articles/solve-the-equation-maybe-you-should-mulligan-more

## Caveats

- 12 verify agents + the synthesis agent died on a session limit; report synthesized by hand from raw verified claims. 3 claims unverified (Sperling hierarchy, Noah-R ML, mtg-mulligan decomposition) — flagged inline.
- Core findings all 3-0 across primary sources (Karsten repo, landlord, Goldfisher, WotC, arXiv).

Feeds: [`project_manabase_plan_presence_backlog`](../../) memory (scoped 5-phase plan + locked decisions).
