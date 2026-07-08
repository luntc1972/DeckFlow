---
title: Mana Base
summary: Check whether a deck has enough lands and colored sources using Frank Karsten's source-count math — no AI needed for the verdict.
order: 35
requires_flag: tool.manabase.enabled
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

Some cards cost far less than their printed mana value: pitch / free spells (Force of Will), board-scaling self-reducers (Blasphemous Act `{8}{R}` that you usually cast for `{R}`), evoke / suspend, and creature-power reducers (The Skullspore Nexus `{4}{G}{G}`, which *"costs {X} less, where X is the greatest power among creatures you control"*). DeckFlow **auto-detects** these and pre-fills the **"Reduced / alternative costs"** box — one card per line as `Card Name: cost`, for example:

```
Force of Will: 0
Blasphemous Act: {R}
The Skullspore Nexus: {G}{G}
```

A creature-power reducer like The Skullspore Nexus is resolved against the **greatest fixed creature power in your deck** (the optimistic on-board value) and **auto-applied** to the analysis — so it casts at the reduced cost out of the box, not just as a suggestion. Edit its line to model a different assumption (or type the full printed cost to score it un-discounted).

Edit or clear any line you disagree with (the cost is what the card *effectively* costs you, not its printed cost). The value is a mana **cost**, so it can change colors, not just lower the number: `0` makes a card behave like a true 0-cost spell (it stops demanding its colors), while `{R}` keeps one red pip. An applied override flows through the whole verdict — the castability simulation, the on-curve turn, and the per-color source findings — and the affected rows are flagged with a `*`. Clearing a line scores that card at its printed cost — with one exception: costs the analysis reduces on its own (evoke/pitch and other self-costs, plus always-on deck-wide cost reducers) still apply, since those are how the card really casts, not optional suggestions.

Once you edit the box, your text is honored exactly as typed — including a box you deliberately empty to reject the suggestions. It is only pre-filled with the detected suggestions until you first touch it, so clearing it sticks instead of silently refilling on the next analysis. Any line the analyzer could not use — an unreadable cost, or a card name that matches nothing in your deck (usually a typo) — is called out under the box as **"N override line(s) not applied"** rather than dropped silently, so you can fix or remove it.

Then press **Analyze Mana Base**. Cards resolve through Scryfall by exact printing first, so alternate or flavor names still match; anything unresolved is listed separately.

## Step 2 — Read The Report

The result panel shows:

- **Health verdict** — a four-tier scale that grades the *mana base*, not the curve:
  - **Excellent** — land count is within one of target and no color has any shortfall.
  - **Solid** — the base works with only minor notes: within a land or two of target, or a few demanding cards that cast late because they are *expensive* (a curve problem the mana base can't fix). Those demanding cards are still listed by name (e.g. *Solid — 1 demanding card: Grand Abolisher (77%)*) so you can decide whether they're worth the strain.
  - **Workable** — one contained color problem the base can fix: a single color short by a source or two, or one color with more color-starved cards than a small ratio of that color. A deck with a good average on-curve cast rate, no catastrophic color, no severe color deficit, and no broad color-access shortfall also stays here when a paper land shortfall stacks with one soft color issue.
  - **Needs work** — a real, broad shortage: a color is short by more than about two Karsten sources, or two or more colors are short — or the deck is two-plus lands short *and* the simulation corroborates it (a color issue or broad simulated under-support riding alongside). Broad under-support on its own does **not** red the verdict, and a paper land deficit alone never does either — a ramp-saturated deck whose cards all cast fine in the sim stays out of the red.

  Crucially, a card that casts late only because of its **mana cost** (not its colors) never fails the base — that is a curve issue, surfaced in the castability table, not a mana-base fault. The source requirements are **mulligan-aware** (they account for Commander's free first mulligan) and clamped to Karsten's table as a ceiling, so a tight double-pip like a turn-two `{W}{W}` is neither flagged against an inflated requirement nor pushed past the math.
- **Biggest fix** — one actionable line, chosen so it never contradicts the health/land read: it points at the color that is genuinely short (add ~N sources), else at the land count (add ~N lands), else at trimming the top end — and never tells you to add a negative or "remove" source count. It also will **not** tell you to add lands when the deck is below the Karsten count but the simulation shows every spell still casts fine (ramp covers the gap); in that case the land line reads *"~N under the Karsten count, but ramp covers it"* instead.
- **Land count** — your actual land total vs. the count Karsten's math recommends for your curve (cEDH lowers that target), with an OK / short note.
- **Ramp** — how much acceleration the deck runs: the count of **mana rocks / dorks** (artifacts and creatures that tap for mana — not land-back MDFCs, and not a creature merely handed a mana ability by a granter like Cryptolith Rite) plus the ramp/draw pieces at ≤2 mana value. This acceleration is what lowers the recommended land count. Only **repeatable** ramp and true card draw earn that reduction — one-shot rituals (Dark Ritual) and Treasure-makers no longer soften the land target.
- **Color findings** — for each color: how many effective sources you run (duals, any-color rocks, and fetchlands are credited to every color they can make), the toughest spell driving the requirement, how many of that color's cards are under-supported, and their mean castability. The **weakest color** highlighted is the one a new source would most help (the broadest color-limited shortfall), so it points you at fixable mana — not at a color that merely happens to own a single expensive, late-casting bomb. A well-supported color carries no alarm bar even if it holds a hard-to-cast card (that card surfaces as a curve issue in the castability table instead).
- **Unsupported interactions** — an honest disclosure (shown only when relevant) of cards the analysis cannot fully model: **X / variable-cost** spells are skipped from the castability simulation, and **flexible split pips** (**hybrid / Phyrexian / twobrid**) carry no hard color requirement (correct per Karsten — they are payable more than one way — but it means their color need is approximated). These cards are listed by name so a clean verdict never quietly hides them.
- **Castability** (Casual mode) — a table of each real spell's estimated chance to be cast **on its on-curve turn**, worst-first, with a low / ok / good chip, its **average delay** (how many turns late it typically becomes castable — *on curve* when it lands on time, else *+N.N turns*), and what's limiting it (*mana*, *color: X*, or *mana + color*). Your commander is pinned at the top. Mana rocks, dorks, and lands are counted in the math but not listed as rows. A `*` next to a card's mana value means a reduced / alternative cost from your overrides was applied. cEDH mode hides this table.

The castability number comes from a Monte-Carlo simulation: it plays out thousands of games with a London mulligan — including **Commander's free first mulligan** (the first mulligan keeps seven; only later ones bottom a card) — **drawing every turn, including turn 1** (Commander is multiplayer, so the starting player draws their first turn; the skip-first-draw rule is two-player only). The simulation also models **how much** mana each source makes (Sol Ring and Ancient Tomb pay 2, Gilded Lotus pays 3 of one color) and is **color-aware when mulliganing** — it ships an opening hand that has enough lands but the wrong colors (a 2+ color deck wants at least two colors in its opening lands), the way a real player would. Repeatable **land-ramp** (Cultivate, Rampant Growth) is modeled too: the fetched land joins the simulation as persistent (colorless) mana one turn after the ramp spell resolves, so expensive payoffs in ramp decks are not under-rated. A card on its mana-value turn N has therefore seen its opening 7 plus one card per turn (7 + N). The sim still goes **first for board development** (one land drop per turn, no earlier plays), which is the conservative case for land sequencing, but it draws on turn 1 like every Commander player does. Read the number as a **ranking aid**, not a guarantee.

### Command zone callout and companion handling

When the `analysis.manabase.commander-castability` feature flag is enabled, the report can add a **command zone** callout above the per-card Castability table. That callout lists each commander card that starts outside the 99, including partner pairs and Backgrounds, with its estimated chance to be cast on curve. Those cards move out of the per-card table for display only; the underlying health verdict and color findings stay the same.

Companions are handled separately from the command zone cards. DeckFlow can auto-detect a companion from the **Moxfield direct API**. Archidekt does not expose a reliable Companion category, so Archidekt decks, pasted lists, and the Moxfield Commander Spellbook fallback path rely on the manual companion designator instead.

When you name a companion, DeckFlow estimates its castability by adding the companion's "put into hand" step as a simple **+3 generic mana** tax before the spell is cast. This is an approximation meant to rank how castable the companion is relative to the rest of the deck. It is not a rules-exact simulation of every game action or timing edge case.

A standing **"analysis is in beta"** notice sits at the top of every manabase result, whether or not the command-zone flag is enabled. It is a reminder that these numbers are a heuristic guide for comparing options, not a guarantee - treat them as directional.

### Untapped-source (Tap) analyzer

When the `analysis.manabase.tap-analyzer` feature flag is enabled, the report (and its paste artifact) add an **untapped-source** readout — a land that enters tapped can't help you cast on curve, so this measures how much of your mana is available right away:

- **Untapped-source frequency** — the overall share of your mana sources that come in untapped.
- **Turn-1 untapped availability** — the deck-level chance you have an untapped source to spend on turn one.
- **Per-color untapped breakdown** (multi-color decks only) — the same untapped view split by color, so you can see which color is stuck behind tapped lands.

This layer is **informational only**. It never changes the land count, color counts, castability table, or health verdict. The flag defaults off, so the report and the downloaded `.txt` stay unchanged until an admin enables it.

### Reading your deck

When the admin enables the plain-language layer, the result can also show a short **Reading your deck** advisory:

- **Metric glosses** — brief plain-English help under the Karsten source check and simulated cast-rate lenses, plus the demanding-cards note when that warning renders.
- **Reading your deck** — a prioritized summary of the top fixes when there are issues, or a specific why-it-is-fine explanation when the mana base already clears the important checks.
- **Ramp / draw budget** (Casual only) — an advisory slot-budget line comparing your current split to a common ramp/draw split for the deck's threshold proxy.

This layer is advisory only. It never changes the land count, color counts, castability table, or health band. The ramp/draw split is a **community heuristic, not Karsten math**, and its threshold is a single-point proxy taken from either the commander's mana value or the deck's 75th-percentile curve point. In cEDH, the glosses can still render, but the ramp/draw budget is suppressed.

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
