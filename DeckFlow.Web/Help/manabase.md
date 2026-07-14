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
- **Paste decklist** — paste a list, one card per line (e.g. `4 Island`). Set and collector number are optional but help cards resolve to the exact printing.

Add an optional **Deck name** to label the report.

### Finding your commander

DeckFlow figures out your commander from the pasted list across the common export shapes — an explicit `Commander` section header or `#commander` tag, and Moxfield's header-less **Copy for MTGO** / **Copy Plain Text** formats, which leave the commander as a lone line *after* the `SIDEBOARD` section. Each detected commander is then validated for legality (Legendary Creature, Legendary Vehicle, a *"can be your commander"* planeswalker, or a Legendary Enchantment Background), and partner / background pairs are kept together.

If DeckFlow still can't pin a valid commander, it does **not** guess — it shows a **Pick your commander** panel with a drop-down of the deck's own commander-eligible cards, plus a name-search box to override with any exact commander. Choose one and press **Analyze Mana Base** again; your decklist and every other option carry over. (URL imports already carry the command zone, so the picker is mainly for pasted lists.)

### Deck type and commander importance

- **Deck type** — *Casual* (default; Karsten's full land target) or **cEDH** (a lower land count in the competitive ~28–32 band, assuming heavy fast mana). A dark-launch flag, `analysis.manabase.cedh-land-target`, ships OFF by default and is enabled on deckflow.gg; when on, it recalibrates **cEDH only** to a curve-anchored target that nudges toward how many lands winning lists of *your* commander actually run — a per-commander baseline built from the last six months of EDHTop16 results (with a commander-specific top-up for lower-play commanders). This drops the flat 28-land floor that flagged most real cEDH winners as "add lands," while still reading grindy, high-land commanders as healthy. When the flag is off, the current cEDH target stays byte-identical. A second experimental cEDH-only flag, `analysis.manabase.ritual-land-credit`, also ships OFF by default and is enabled on deckflow.gg; when on, it can trim that target further for ritual-heavy lists, and the *This deck's numbers* breakdown now names that ritual land credit on its own line.
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
- **Land count** — your actual land total vs. the count Karsten's math recommends for your curve (cEDH lowers that target), with an OK / short note. Modal double-faced cards with a **land back** (Agadeem's Awakening, Shatterskull Smashing, and the rest of the Zendikar / spell-side MDFC lands) count toward your land total, since that back really is a land you can play — so an MDFC-heavy deck reads closer to its recommended count.
- **Ramp** — how much acceleration the deck runs: the count of **mana rocks / dorks** (artifacts and creatures that tap for mana — not land-back MDFCs, and not a creature merely handed a mana ability by a granter like Cryptolith Rite) plus the ramp/draw pieces at ≤2 mana value. This acceleration is what lowers the recommended land count. Only **repeatable** ramp and true card draw earn that reduction — one-shot rituals (Dark Ritual) and Treasure-makers no longer soften the land target.
- **Color findings** — for each color: how many effective sources you run (duals, any-color rocks, and fetchlands are credited to every color they can make), the toughest spell driving the requirement, how many of that color's cards are under-supported, and their mean castability. An experimental flag, `analysis.manabase.scry-credit`, ships ON for new databases and is enabled on deckflow.gg; when on, each cheap nonland spell with a real **scry N** effect adds **+0.2 any-color effective sources** to the per-color source counts only, and the page and `.txt` download name the exact **N cheap scry spells × 0.2** credit when it applies. That credit does **not** change the land target, the castability sim, or the untapped-source figures, and a card that both draws and scries is deliberately allowed to count in both this source credit and the ramp/draw land-target credit. A second experimental flag, `analysis.manabase.colorless-snow`, also ships ON for new databases and is enabled on deckflow.gg; when on, the table gains dedicated **Colorless** and **Snow** rows, so true `{C}` costs need real colorless-producing sources and `{S}` costs need snow sources. Mana rocks with those costs can drive those rows too, and snow status comes from the card's front-face type line. The **weakest color** highlighted is the one a new source would most help (the broadest color-limited shortfall), so it points you at fixable mana — not at a color that merely happens to own a single expensive, late-casting bomb. A well-supported color carries no alarm bar even if it holds a hard-to-cast card (that card surfaces as a curve issue in the castability table instead). The per-color deficit badges are **heuristic guidance** about where another source would help most, not an exact promise about every draw.
- **Restricted-source disclosure** — when the experimental `analysis.manabase.restricted-lands` flag is enabled by an administrator (it ships OFF by default and is enabled on deckflow.gg), lands such as Cavern of Souls or Ancient Ziggurat that only spend mana cleanly in some deck compositions are marked with a `†` in their own small disclosure table, plus a footnote and an Unsupported Interactions panel entry naming those lands. That marker means DeckFlow is using a deck-composition approximation for that land, not a spell-by-spell spend simulation.
- **Unsupported interactions** — an honest disclosure (shown only when relevant) of cards the analysis cannot fully model: **X / variable-cost** spells are skipped from the castability simulation, and **flexible split pips** (**hybrid / Phyrexian / twobrid**) carry no hard color requirement (correct per Karsten — they are payable more than one way — but it means their color need is approximated). These cards are listed by name so a clean verdict never quietly hides them.
- **Castability** (Casual mode) — a table of each real spell's estimated chance to be cast **on its on-curve turn**, worst-first, with a low / ok / good chip, its **average delay** (how many turns late it typically becomes castable — *on curve* when it lands on time, else *+N.N turns*), and what's limiting it (*mana*, *color: X*, or *mana + color*). Your commander is pinned at the top. Mana rocks, dorks, and lands are counted in the math but not listed as rows. When `analysis.manabase.colorless-snow` is on (it ships ON for new databases and is enabled on deckflow.gg), this same sim also treats true `{C}` and `{S}` costs as real requirements, so the limiting factor can read **color: Colorless** or **color: Snow**; when the flag is off, behavior stays unchanged and colorless-folded pips are not simulated. A `*` next to a card's mana value means a reduced / alternative cost from your overrides was applied. cEDH mode hides this table.

The castability number comes from a **20,000-trial Monte-Carlo simulation**: it plays out games with a London mulligan — including **Commander's free first mulligan** (the first mulligan keeps seven; only later ones bottom a card) — **drawing every turn, including turn 1** (Commander is multiplayer, so the starting player draws their first turn; the skip-first-draw rule is two-player only). The simulation also models **how much** mana each source makes (Sol Ring and Ancient Tomb pay 2, Gilded Lotus pays 3 of one color) and is **color-aware when mulliganing** — it ships an opening hand that has enough lands but the wrong colors (a 2+ color deck wants at least two colors in its opening lands), the way a real player would. Repeatable **land-ramp** (Cultivate, Rampant Growth) is modeled too: the fetched land joins the simulation as persistent (colorless) mana one turn after the ramp spell resolves, so expensive payoffs in ramp decks are not under-rated. A card on its mana-value turn N has therefore seen its opening 7 plus one card per turn (7 + N). The sim still goes **first for board development** (one land drop per turn, no earlier plays), which is the conservative case for land sequencing, but it draws on turn 1 like every Commander player does. Read the number as a **ranking aid**, not a guarantee.

A dark-launch beta flag, `analysis.manabase.ritual-burst-mana` (default OFF), can also let **cEDH** analyses credit one-shot rituals such as Dark Ritual as temporary burst mana in that castability sim. This changes only early-turn cast percentages; it does **not** change the land count recommendation or color-source counts, and Casual mode stays unchanged.

### Command zone callout and companion handling

By default (the `analysis.manabase.commander-castability` flag, on — an admin can hide it), the report adds a **command zone** callout above the per-card Castability table. That callout lists each commander card that starts outside the 99, including partner pairs and Backgrounds, with its estimated chance to be cast on curve. Those cards move out of the per-card table for display only; the underlying health verdict and color findings stay the same.

Companions are handled separately from the command zone cards. DeckFlow can auto-detect a companion from the **Moxfield direct API**. Archidekt does not expose a reliable Companion category, so Archidekt decks, pasted lists, and the Moxfield Commander Spellbook fallback path rely on the manual companion designator instead.

When you name a companion, DeckFlow estimates its castability by adding the companion's "put into hand" step as a simple **+3 generic mana** tax before the spell is cast. This is an approximation meant to rank how castable the companion is relative to the rest of the deck. It is not a rules-exact simulation of every game action or timing edge case.

A standing **"analysis is in beta"** notice sits at the top of every manabase result, whether or not the command-zone flag is enabled. It is a reminder that these numbers are a heuristic guide for comparing options, not a guarantee - treat them as directional.

### Untapped-source (Tap) analyzer

Shocklands and other **"pay N life"** lands (Steam Vents, Godless Shrine, and pay-life MDFC backs like Agadeem, the Undercrypt) are counted as entering **untapped** — the way you actually play them — so they help your turn-one casts. Conditional lands are handled more broadly than they used to be: **bond lands** (Sea of Clouds, Training Center) are treated as untapped in Commander; **check lands** (Glacial Fortress) and **Snarls** (Frostboil Snarl) are treated as untapped when your deck runs enough matching land types; and **fast lands**, **slow lands**, and **Mystic Sanctuary-style "three other Islands" lands** are resolved **inside the simulation, turn by turn, at the moment you play them**. Verge lands, Training Compound-style lands, and Vivid lands are also recognized, but those affect color access or limited-use fixing more than the untapped readout. Plain taplands still stay tapped.

By default (the `analysis.manabase.tap-analyzer` flag, on), the report (and its paste artifact) add an **untapped-source** readout — a land that enters tapped can't help you cast on curve, so this measures how much of your mana is available right away:

- **Untapped-source frequency** — the overall share of your mana sources that come in untapped.
- **Turn-1 untapped availability** — the deck-level chance you have an untapped source to spend on turn one.
- **Per-color untapped breakdown** (multi-color decks only) — the same untapped view split by color, so you can see which color is stuck behind tapped lands.

This layer is **informational only**. It never changes the land count, color counts, castability table, or health verdict. The flag is on by default; an admin can hide the block from `/Admin/Flags`.

### Opening hand and plan presence

By default (the `analysis.manabase.mulligan-eval` flag, on), the report (and its paste artifact) add an **opening-hand** block — a mulligan-focused read from the same Monte-Carlo sim:

- **Keepable hands** — the share of London-mulligan openers that keep, with a high/medium/low band. A keep is a *sweet-spot* land count, not just "any playable hand": **3 lands** is ideal, **2 lands** keeps only with a ramp piece, and a **4–5 land flood is mulliganed** (a high-mana-curve deck keeps its wider band, up to 5, since it genuinely wants more lands).
- **Keep-size process** — how often the deck keeps at seven versus mulligans to six or five.
- **Colors / curve** — the deck's color count and average mana value.
- **Representative openers** — a few sample keepable hands, each with the earliest turn its best card comes online.

With the `analysis.manabase.plan-presence` flag on (default; it needs the opening-hand block above, so it also requires `mulligan-eval`), that block gains a **plan-presence** line. It leads with **payoff on curve** — the share of keepable openers holding a **payoff** you can cast on curve, with its own high/medium/low band — because the broader "any win-directed card" number saturates high on real decks and does not separate stronger builds from weaker ones. The line then shows that composite percentage and a per-role breakdown (payoff / engine / tutor-combo / interaction).

- A combo or control deck reading **low payoff** is a correct profile, not a fault — its closer is the combo (a tutor-combo card), which shows in the role breakdown.
- **Payoffs & interaction need a permanent:** a **payoff** (a board threat) or **interaction** (removal / counters) counts toward a plan only when it is a **permanent** you can cast on curve — a one-shot burn/extra-turn finisher or a one-shot removal/counter leaves nothing on the board, so it earns no plan role. **Tutors and card draw still count even as instants/sorceries** — a sorcery tutor points at the permanent win, and card advantage furthers the plan. (A pure counterspell, being a non-permanent, no longer makes a hand "have a plan" on its own.)
- The representative openers prefer, at each mulligan depth (7 / 6 / 5), a hand that holds such a castable permanent plan card and name it — so you can see what a hand *with a plan* looks like, down to a mulligan to five.
- Roles come from your Category Knowledge Store, then Commander Spellbook combo pieces, then an oracle-text heuristic. It is a **consistency signal, never keep/mulligan advice**.

Both flags are on by default; an admin can hide either block from `/Admin/Flags`. The separate ritual-burst beta flag above is default OFF.

### cEDH: Early interaction

In **cEDH** mode, DeckFlow can also measure your deck's early cheap interaction coverage. For each qualifying interaction spell, the simulation checks whether you have enough **untapped** colored access to hold that spell up on at least one of turns 1-3, then rolls that into a per-card **holdable** percentage.

By default (the `analysis.manabase.cedh-interaction-lens` flag, on), **cEDH only** adds an **Early interaction** lens to the header strip and shows the full Castability table in cEDH mode:

- **Qualifying spells** — cards tagged `PlanRole.Interaction` with **effective MV <= 2** after any reduced / alternative cost overrides are applied.
- **Headline support check** — **N / M interaction held up by turn 3** at the same **88%** threshold the report uses for a met support read.
- **Tail-risk disclosure** — the lens shows the **worst 5** holdable interaction spells first, with a native **view all** expander for the rest.
- **Empty-state caution** — if no spells qualify, the lens shows a caution-style **no cheap interaction found** warning instead of silently disappearing.

This interaction lens is a **raw-availability** read and **assumes you hold mana open**.

This layer is **informational only**. It never changes the land count, color counts, castability math, castability sort, castability percentages, or health verdict. In cEDH mode it does newly make the full Castability table **visible** and adds a **holdable** badge on qualifying interaction rows. See **How the analysis works** and **This deck's numbers** for the exact interaction formulas and the deck-specific numbers plugged into them.

### Reading your deck

By default (the plain-language layer, on), the result also shows a short **Reading your deck** advisory:

- **Metric glosses** — brief plain-English help under the Karsten source check and simulated cast-rate lenses, plus the demanding-cards note when that warning renders.
- **Reading your deck** — a prioritized summary of the top fixes when there are issues, or a specific why-it-is-fine explanation when the mana base already clears the important checks.
- **Ramp / draw budget** (Casual only) — an advisory slot-budget line comparing your current split to a common ramp/draw split for the deck's threshold proxy.

This layer is advisory only. It never changes the land count, color counts, castability table, or health band. The ramp/draw split is a **community heuristic, not Karsten math**, and its threshold is a single-point proxy taken from either the commander's mana value or the deck's 75th-percentile curve point. If the verdict surfaces more than three distinct issues, the summary shows the top three and then appends **"...plus N more"** so nothing is silently hidden. In cEDH, the glosses can still render, but the ramp/draw budget is suppressed.

## Step 3 — See The Formula

Two collapsible panels show exactly how the verdict was reached:

- **How the analysis works** — the methodology (Karsten's land regression, the cEDH adjustment, the cEDH early-interaction metric, and the castability simulation), shown even before you analyze.
- **This deck's numbers** — the land-target formula with *your* deck's values plugged into every term, the per-color source tally, and the simulation parameters, including the cEDH interaction metric when that mode is active. Use it to audit why a color or a card was flagged.

## Step 4 — Optional: Land-Swap Prompt

The math tells you *what* is short, but not *which* lands to add for your specific deck. The optional **"Want specific land swaps?"** block builds a small ChatGPT / Claude prompt framing the deficits. This is the only part of the workflow that needs an AI — the verdict itself does not.

## Notes

- The analyzer is tuned for Commander / cEDH singleton decks.
- The land count uses Frank Karsten's published source-count work; the castability estimate is DeckFlow's own simulation, cross-checked against community calculators including [Salubrious Snail](https://www.salubrioussnail.com/manabase-tool).
- The same scoring engine is available from the CLI as the `manabase` command (the castability + mode UI is web-only).
- This tool can be turned off by an administrator; when it is, this help topic is hidden too.
