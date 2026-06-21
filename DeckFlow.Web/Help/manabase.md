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

Then press **Analyze Mana Base**. Cards resolve through Scryfall by exact printing first, so alternate or flavor names still match; anything unresolved is listed separately.

## Step 2 — Read The Report

The result panel shows:

- **Land count** — your actual land total vs. the count Karsten's math recommends for your curve (cEDH lowers that target), with an OK / short verdict.
- **Color findings** — for each color: how many effective sources you run (duals, any-color rocks, and fetchlands are credited to every color they can make), the toughest spell driving the requirement, how many of that color's cards are under-supported, and their mean castability. The weakest color is highlighted; a lone hard-to-cast bomb still surfaces even if the rest of the color is fine.
- **Castability** (Casual mode) — a table of each real spell's estimated chance to be cast **on its on-curve turn**, worst-first, with a low / ok / good chip and what's limiting it (*mana*, *color: X*, or *mana + color*). Your commander is pinned at the top. Mana rocks, dorks, and lands are counted in the math but not listed as rows. cEDH mode hides this table.

The castability number comes from a Monte-Carlo simulation (it plays out thousands of games with a London mulligan), so read it as a **ranking aid**, not a guarantee.

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
