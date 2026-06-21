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
- **Paste decklist** — paste a list, one card per line (e.g. `4 Island`). Set and collector number are optional but help cards resolve to the exact printing.

Add an optional **Deck name** to label the report. Then press **Analyze Mana Base**.

Cards resolve through Scryfall by exact printing (set + collector number) first, so alternate or flavor names still match. Anything that can't be resolved is listed separately so you can correct a typo or an unsupported entry.

## Step 2 — Read The Report

The result panel shows:

- **Land count** — your actual land total vs. the count Karsten's math recommends for your curve, with an OK / short verdict.
- **Color table** — for each color: how many sources you run, how many the toughest spell of that color needs, and how far short you are (if at all).
- **Biggest fix** — the single most impactful change: which color to shore up first, how many sources to add, and the spell driving the requirement. If every color is already supported, the panel says so.

## Step 3 — Optional: Land-Swap Prompt

The math tells you *what* is short, but not *which* lands to add for your specific deck. The optional **"Want specific land swaps?"** block builds a small ChatGPT / Claude prompt framing the deficits, so you can ask an AI for concrete land recommendations. This is the only part of the workflow that needs an AI — the verdict itself does not.

## Notes

- The analyzer is tuned for Commander / cEDH singleton decks.
- The same engine is available from the CLI as the `manabase` command.
- This tool can be turned off by an administrator; when it is, this help topic is hidden too.
