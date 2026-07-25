# Research — How commanders (draw / ramp / cheat / cost) affect the manabase

**Date:** 2026-07-16
**Method:** two deep-research passes (fan-out web search → fetch → 3-vote adversarial verify → synthesis). Raw JSON: `2026-07-16-manabase-draw-ramp-cheat-RAW.json` (general), `2026-07-16-commander-manabase-effect-RAW.json` (commander-specific).
**Quality caveat:** all sources are hobby/retailer blogs + community tools (ScrollVault, Salubrious Snail, Canadian Highlander, Schulze Medium, CoolStuffInc, Card Kingdom, Draftsim, Commander's Herald, kvnchen widget, EDHREC). Karsten's PRIMARY articles were not fetched directly — his math is reported second-hand. Direction of effect is consistent; exact coefficients diverge between authors.

## Pass 1 — general (draw / ramp / cost-cheating), cards in the 99

- **Card draw / selection → fewer lands.** Draw-heavy decks sit ~33–35 lands (vs 36–38). Karsten/Canadian-Highlander regression: **−0.28 lands per MV≤2 ramp OR draw** card. Schulze: cantrip = **1/3 of a mana source**; selection (scry) also a partial colored source. Do NOT double-count the colored-source credit (one framework omits it for circular-dependency).
- **Ramp → substitutes for lands ~2–3:1.** Land-fetch = 1.0 source, non-land rock/dork ≈ 0.75 (removal-prone). Ramp still needs real lands to hit drops + to cast the ramp itself (a 2-CMC green ramp spell wants ~21 green sources). Community split: some build lands as if rocks don't exist, add rocks on top.
- **Cost-cheating → castability only, not land count.** Evaluate a cost-modified card at its expected-paid cost, not printed cost. Weakest-supported branch (1 source; a related claim refuted).
- **REFUTED (do not implement):** "1.6× 60→99 colored-source scaling", "rocks = 0.5 colored source", "10 rocks → cut 5 lands (flat 2:1)".

**DeckFlow status:** most of this is ALREADY implemented — the classifier credits MV≤2 ramp/draw (−0.28), models land-ramp, detects cost reduction, and auto-reduces self-anchored free-casts.

## Pass 2 — the COMMANDER itself (the decisive pass)

### VERDICT: "commander that draws/ramps → fewer lands" is UNSOURCED and contra-modeled. Do NOT build it.

- **No published tool or formula credits the commander's OWN draw/ramp/card-advantage against the manabase.** Not merely unfound — explicitly excluded:
  - **Salubrious Snail** lists "Ramp in the command zone" AND "Draw and filtering to find more lands, in ANY form" under *"what this model does NOT take into account."*
  - **ScrollVault** — "does not account for the commander's individual card-draw abilities, ramp capacity, recastability from the command zone."
  - **Canadian Highlander** — its only ability credit (−0.28×(ramp+draw)) is defined over the 99, never the commander.
- **Command-zone guaranteed availability is NOT modeled as a discount.** The one tool that inputs "Commanders" (kvnchen Karsten widget) uses it to **RAISE** land count (+~0.27/commander, presence cost like a Companion) — the opposite of a credit, and it credits nothing for abilities. The intuition "a guaranteed repeatable engine should be weighted more than a 1-of" is community-plausible but entirely unaddressed; no source offers a coefficient.

### What IS sourced: the commander's COST drives land + ramp UP

- **Nate Burgess (via EDHREC):** `Lands = 31 + colorsInIdentity + commanderCMC` → **+1 land per CMC point.**
- **Karsten (via Wischkaemper):** 2-mana commander → 42 lands + Sol Ring; 6-mana → 38 lands + Sol Ring + **9 rocks** (lands dip, total sources rise 43→48). High-cost commanders want the 4th/5th land drop.
- **Ramp density scales with commander cost:** 8–10 ramp pieces, four-drops want turn-2 ramp, five-drops turn-3.
- Land bands: ~36 typical, 28–31 cEDH, 37–40 battlecruiser (6+ mana commanders).

**DeckFlow status:** ramp-by-commander-cost is ALREADY implemented (`ManabaseRampDrawBudget` keys its target off `CommanderManaValue`). The LAND target, however, EXCLUDES the commander from `avgMV` — so a cheap deck with an expensive commander is under-landed. That is the one sourced, non-redundant gap → the **commander-cost land floor** feature (see `../specs/2026-07-16-commander-cost-floor-design.md`).

## Decision log

- **Rejected:** "commander engine credit" (commander's draw/ramp → −0.28 lands). Unsourced, contradicted by every tool that models the command zone. **Do not revisit without new primary evidence** (e.g. a DeckFlow crawl-data calibration study, which would be original research nobody has published).
- **Adopted (Option A):** commander-cost land floor — `max(karstenTarget, 31 + colors + commanderCMC)`, Burgess-as-floor to avoid double-counting avgMV. Sourced, non-redundant, flag-gated seed ON.
- **Open (would need original data):** a fair coefficient for command-zone availability as a manabase discount — genuinely unaddressed in the literature; only DeckFlow's crawl data could settle it.
