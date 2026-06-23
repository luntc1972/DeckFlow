---
title: Mana Base
summary: Check whether a deck has enough lands and colored sources using Frank Karsten's source-count math — no AI needed for the verdict.
order: 35
requires_flag: feature.manabase.enabled
---

# Mana Base

The Mana Base page (`/manabase`) scores your deck's land count and colored-source supply against [Frank Karsten's source-count method](https://www.channelfireball.com/articles/how-many-sources-do-you-need-to-consistently-cast-your-spells/). The verdict is computed **directly in DeckFlow** — there is no AI round-trip needed to tell you whether the mana base holds up.

## Step 1 — Load A Deck

Pick one of two inputs:

- **Public URL** — paste an Archidekt or Moxfield deck link.
- **Paste decklist** — paste a list, one card per line (e.g. `4 Island`). Set and collector number are optional but help cards resolve to the exact printing. Include a `Commander` section header (your export usually does) so the analyzer can pin and weight your commander.

Add an optional **Deck name** to label the report.

### Deck type and commander importance

- **Deck type** — *Casual* (default; Karsten's full land target) or **cEDH** (a lower land count in the competitive ~28–32 band, assuming heavy fast mana).
- **How important is your commander?** — *Central* (must cast as early as possible every game, e.g. Brago), *Standard* (matters, cast when convenient), or *Low* (optional / late value). Central holds the commander's colors to a stricter threshold; it does not change the land target. Both selectors persist when you re-analyze.

### Reduced / alternative costs (optional)

Some cards cost far less than their printed mana value: pitch / free spells (Force of Will), board-scaling self-reducers (Blasphemous Act `{8}{R}` that you usually cast for `{R}`), and evoke / suspend. DeckFlow **auto-detects** these and pre-fills the **"Reduced / alternative costs"** box — one card per line as `Card Name: cost`, for example:

```
Force of Will: 0
Blasphemous Act: {R}
```

Edit or clear any line you disagree with (the cost is what the card *effectively* costs you, not its printed cost). The value is a mana **cost**, so it can change colors, not just lower the number: `0` makes a card behave like a true 0-cost spell (it stops demanding its colors), while `{R}` keeps one red pip. An applied override flows through the whole verdict — the castability simulation, the on-curve turn, and the per-color source findings — and the affected rows are flagged with a `*`. Leave the box empty to score every card at its printed cost.

Then press **Analyze Mana Base**. Cards resolve through Scryfall by exact printing first, so alternate or flavor names still match; anything unresolved is listed separately.

## Step 2 — Read The Report

The result panel shows:

- **Health verdict** — a four-tier scale that grades the *mana base*, not the curve:
  - **Excellent** — land count is within one of target and no color has any shortfall.
  - **Solid** — the base works with only minor notes: within a land or two of target, or a few demanding cards that cast late because they are *expensive* (a curve problem the mana base can't fix). Those demanding cards are still listed by name (e.g. *Solid — 1 demanding card: Grand Abolisher (77%)*) so you can decide whether they're worth the strain.
  - **Workable** — one contained color problem the base can fix: a single color short by a source or two, or one color with more color-starved cards than a small ratio of that color.
  - **Needs work** — a real, broad shortage: the deck is two-plus lands short, a color is short by several sources, or two or more colors are short.

  Crucially, a card that casts late only because of its **mana cost** (not its colors) never fails the base — that is a curve issue, surfaced in the castability table, not a mana-base fault. The source requirements are **mulligan-aware** (they account for Commander's free first mulligan) and clamped to Karsten's table as a ceiling, so a tight double-pip like a turn-two `{W}{W}` is neither flagged against an inflated requirement nor pushed past the math.
- **Biggest fix** — one actionable line, chosen so it never contradicts the health/land read: it points at the color that is genuinely short (add ~N sources), else at the land count (add ~N lands), else at trimming the top end — and never tells you to add a negative or "remove" source count.
- **Land count** — your actual land total vs. the count Karsten's math recommends for your curve (cEDH lowers that target), with an OK / short note.
- **Ramp** — how much acceleration the deck runs: the count of **mana rocks / dorks** (non-land mana sources) plus the ramp/draw pieces at ≤2 mana value. This acceleration is what lowers the recommended land count.
- **Color findings** — for each color: how many effective sources you run (duals, any-color rocks, and fetchlands are credited to every color they can make), the toughest spell driving the requirement, how many of that color's cards are under-supported, and their mean castability. The weakest color is highlighted; a lone hard-to-cast bomb still surfaces even if the rest of the color is fine.
- **Castability** (Casual mode) — a table of each real spell's estimated chance to be cast **on its on-curve turn**, worst-first, with a low / ok / good chip, its **average delay** (how many turns late it typically becomes castable — *on curve* when it lands on time, else *+N.N turns*), and what's limiting it (*mana*, *color: X*, or *mana + color*). Your commander is pinned at the top. Mana rocks, dorks, and lands are counted in the math but not listed as rows. A `*` next to a card's mana value means a reduced / alternative cost from your overrides was applied. cEDH mode hides this table.

The castability number comes from a Monte-Carlo simulation: it plays out thousands of games with a London mulligan — including **Commander's free first mulligan** (the first mulligan keeps seven; only later ones bottom a card) — **on the play** (going first, so the turn-one draw is skipped). A card on its mana-value turn N has therefore seen its opening 7 plus one card per turn after the first (7 + (N − 1)). On the play is the **conservative** case: in a multiplayer pod you are usually *on the draw* and see one extra card by your curve turn, so real odds run a little higher than shown — this matches the Karsten / Salubrious Snail baseline. Read the number as a **ranking aid**, not a guarantee.

## Step 3 — See The Formula

Two collapsible panels show exactly how the verdict was reached:

- **How the analysis works** — the methodology (Karsten's land regression, the cEDH adjustment, and the castability simulation), shown even before you analyze.
- **This deck's numbers** — the land-target formula with *your* deck's values plugged into every term, the per-color source tally, and the simulation parameters. Use it to audit why a color or a card was flagged.

## Step 4 — Optional: Land-Swap Prompt

The math tells you *what* is short, but not *which* lands to add for your specific deck. The optional **"Want specific land swaps?"** block builds a small ChatGPT / Claude prompt framing the deficits. This is the only part of the workflow that needs an AI — the verdict itself does not.

## Notes

- The analyzer is tuned for Commander / cEDH singleton decks.
- The land count uses Frank Karsten's published source-count work; the castability estimate is DeckFlow's own simulation, cross-checked against community calculators including [Salubrious Snail](https://www.salubrioussnail.com/manabase-tool).
- The same scoring engine is available from the CLI as the `manabase` command (the castability + mode UI is web-only).
- This tool can be turned off by an administrator; when it is, this help topic is hidden too.
