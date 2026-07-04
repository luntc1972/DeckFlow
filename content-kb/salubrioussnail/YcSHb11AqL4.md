---
source: "Salubrious Snail"
title: "My Brother and I Built a Tool to Yell at You About Your Manabase"
url: "https://www.youtube.com/watch?v=YcSHb11AqL4\u0026list=UUOYkwObFKjxko7oj56gVDag"
video_id: "YcSHb11AqL4"
tags:
  archetype: ["voltron","value-engine","control","reanimator","aggro","midrange"]
  bracket: ["Core","Upgraded","Optimized","cEDH"]
  card_category: ["ramp","draw","finishers"]
generated_utc: "2026-06-08T18:14:49Z"
---

## Summary

This video introduces a free manabase analysis tool built by the creator (Salubrious Snail) and his brother Ian, aimed at EDH and 60-card deckbuilders who want a quick second opinion on their manabase. The creator frames lands as serving two goals: covering a deck's color symbols and covering total mana values (hitting land drops); utility lands bridge the gap between them. Ian explains the tool uses a hypergeometric distribution and a branching tree across 32 color-combination categories (5 bits) to compute how often each spell is castable on or before its mana value, scoring decks by average casting delay and 'cast rate.' It tests small changes—adding a basic of each color or a Wastes—to flag whether color-fixing or land count is the bigger problem. A second model handles taplands via simplified opening-turn simulation, reporting how often conditional lands (checklands, slow lands, tango lands, Tainted lands, verges) enter tapped. The design prioritizes interpretability over accuracy, deliberately excluding draw spells, weighting ramp as a partial land (Karsten-style), and avoiding prescriptive targets so players set their own risk tolerance. The tool works poorly for lands-matter, heavy-ramp, or cost-avoidance decks; an advanced version is planned. Sponsored by Dragonshield.

## Key Clips

- Lands are one of the most important parts of a deck, the foundation upon which a deck is built, and as with construction a shaky foundation will lead to a shaky deck.
- A Ball Lightning has the same total symbol requirement as three Shocks, but those two selections of cards apply very different levels of demand on a deck's red mana. Likewise, Counterspell applies less strain on a deck's blue mana since it's a reactive card that will likely be played later in the game.
- Land bases exist to hit two goals: covering a deck's symbols (producing the colors needed to cast spells) and covering total mana values (assisted by hitting land drops). Utility lands bridge gaps between these two—if a deck wants 37 lands to hit land drops but only needs 33 for symbols, that leaves four slots for colorless-producing utility lands.
- The tool uses a hypergeometric distribution and a branching tree-like structure to calculate odds for every combination of multiple land categories. Lands are sorted into 32 categories—one for each possible combination of the 5 colors—numbered so the 5 bits storing each category also encode the colors its lands can produce.
- Average delay in casting spells is the benchmark used to judge decks, alongside 'Cast Rate'—how likely a card is castable on or before its mana value. If adding a waste reduces a deck's average delay by a substantial amount, that's a good indicator it might need more lands, or that land count is a bigger problem than color-fixing.
- The central conflict in building such a tool is accuracy vs interpretability. A model with more factors and weights is more accurate but harder to understand. A model with too much unexplained complexity becomes a black box users must blindly trust—better to have an imperfect but simpler model people can open the hood on.
- The team decided not to include any draw spells in the model. EDH lacks the low-cost draw spell density of formats like Legacy where cantrips make up 10-20% of a deck. Ramp is counted as a portion of a land that taps for its fixing colors, with weight determined by how often the deck can cast it on curve—so higher-costed ramp is weighted lower than lower-costed ramp.
- A separate model handles taplands by ignoring colors and simulating opening turns thousands of times, scoring by spells cast, then re-running with no taplands and one less tapland. The %-of-time-tapped statistic misses nuance—slow lands score very low tapped percentages but the algorithm only checks for two other lands, ignoring whether they're the wrong color or conditionally tapped themselves. %-of-time-tapped is kept because it's far more interpretable.

## Tags

**Archetypes/Strategy:** voltron, value-engine, control, reanimator, aggro, midrange
**Format/Bracket:** Core, Upgraded, Optimized, cEDH
**Card Categories:** ramp, draw, finishers
