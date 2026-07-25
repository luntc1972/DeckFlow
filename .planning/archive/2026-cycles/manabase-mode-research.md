# Manabase Analyzer — Casual vs cEDH Modes + Curve/Castability Research

**Date:** 2026-06-21
**Goal:** Add two modes (casual / cEDH) to the manabase tool, research a cEDH formula, improve the casual formula, and add mana-curve castability analysis (when can each spell actually be cast).

**Method:** Two background research passes (deep-research workflow + focused curve agent), web-sourced. Caveat up front: the deep-research workflow's *verification* phase was rate-limited (all "0-0" votes = zero votes cast, NOT genuine refutes), so its 25 claims are search/fetch findings, unverified by the adversarial pass. They cross-corroborate each other and the curve agent's independently-verified findings, so confidence is moderate-to-high on the numbers that repeat across sources. Primary-source Karsten PDFs/articles were image-only or paywalled and could not be fetched directly; most numbers come from secondary sources citing Karsten (ScrollVault, Schulze/Medium, EDHREC, StarCityGames).

---

## 1. Where the current tool stands

`KarstenManabase` already implements:
- **Singleton land target** = `(deckSize-cmdrs)/60 × (19.59 + 1.90·avgMV + 0.27·cmdrCount) − 0.28·ramp − fastMana − 0.74·mdfcCommon − 0.38·mdfcMythic − 1.35`
- **60-card land target** = `32.65 + 3.16·avgMV − 0.28·ramp − fastMana − 0.74·mdfc1 − 0.38·mdfc2`
- **Colored sources** via conditional hypergeometric at the **(89+M)%** threshold (90% one-drops → 96% seven-drops), on the play, 7-card opener, **no mulligan modeling**.

No mode split. No curve/castability output. The model treats every deck as "casual singleton."

---

## 2. cEDH conventions (research findings)

| Metric | Casual / battlecruiser | cEDH | Source |
|---|---|---|---|
| Land count | 36–40 (Karsten ~37–38) | **28–32** (combo lists 28–30; 34 is "extremely high") | scrollvault, edhrec cEDH guide, threeforonetrading |
| Fast mana / rocks | ~8–10 ramp at avg MV 3.5 | **10–12 rocks** + nearly all playable fast mana (Sol Ring, Mana Crypt, Mana Vault, Chrome Mox, Mox Diamond/Opal/Amber, Jeweled Lotus) | edhrec cEDH guide |
| Avg mana value | ~3.0 | **~1.3–2.0** (often <2) | mtgproxycards, tappedout |
| ETB-tapped lands | tolerated | **≈0** (zero tapped lands) | edhrec cEDH guide |
| Free counterspells | optional | **≥3 floor, 5 comfortable** (FoW, Fierce Guardianship, Pact, etc.) | cEDH templates |
| Interaction | situational | **12–18 pieces, instant-speed at 0–2 mana**; a 3-mana sorcery answer is "already too slow" | cEDH templates |

**Why cEDH runs fewer lands:** free/cheap mana rocks act as extra "lands." Multiple sources count fast mana as a ~full land replacement and 2-mana rocks as 0.33–0.5 of a land. Combined with a sub-2.0 average MV, the regression target collapses toward ~28–31. One worked datapoint: a 30-land Sultai cEDH deck (~1.8 avg MV, 12 ramp) still hits its 3-drop on-curve at ~96% (scrollvault).

**Key insight for mode design:** cEDH doesn't optimize "cast my N-drop on turn N." It optimizes **turn 1–3 access to cheap colored interaction** (single/double blue pips for Force of Will, Swan Song, Mana Drain, Fierce Guardianship) plus enough fast mana to deploy a combo turns 2–5. So the *consistency target shifts earlier and toward color access, not land-drop progression.*

---

## 3. Curve & castability (research findings)

**Land-drop probability (the "can I cast my N-drop" engine):**
- 60-card canon (Karsten, widely reproduced): **25 lands → 3 lands by T3 = 90.4% on play / 94.6% on draw**; **4 by T4 = 74.7% play / 83.5% draw**.
- Commander 99/100-card: **~36 lands → ~90% to hit 3 lands by T3**; **~40 lands → ~90% to hit 4 by T4**; 46 = high-safety ceiling; 34 lands misses 4-by-T4 >30% of the time.
- Equivalence scaling: 25 lands/60 ≈ 41 lands/100.

**Karsten's 99-card land regression (alternate form found):** `Lands = 31.42 + 3.13·avgMV − 0.28·ramp`. Note the **per-MV slope is 3.13 in the 99-card form**, vs 1.90 in the 60-card form. The current tool's singleton path scales the *60-card 1.90 slope* by deckSize/60 — which lands close but is not identical to Karsten's directly-published 99-card 3.13 slope. Worth reconciling (see §5).

**Color-source counts (Karsten 60-card, ~90%):** 1 pip ≈ 14 sources (T1) / 12 (T3+); 2 pips ≈ 18–20; 3 pips ≈ 21+. ScrollVault's Commander adaptation multiplies by ~1.6× → ~22 (single) / ~29 (double) before universal fixers (Command Tower, Arcane Signet). *(This 1.6× is ScrollVault's adaptation, not Karsten's own Commander table — flag.)*

**Ramp shifts the effective curve** (no single canonical number — spans by what you're counting):
- Karsten land-count discount: **−0.28 land / cheap ramp**, **−1.0 land / fast-mana artifact** (confirms the tool's coefficients).
- 2-mana rock = **0.33 land** (land-count trim) or **0.5 land** (color-source count) — context-dependent.
- Schulze "mana source" weights: land-search 1.0, non-land ramp (Sol Ring/Signet/dork) 0.75, card-selection 0.5, cantrip 0.33.

**Healthy-curve metrics (for a curve-health readout):**
- Curve peaks at **MV 2–3**, taper after; concentrate cards at MV 2–4.
- EDHREC aggregate: modal MV = 2; ~15.7 cards at MV 2, ~15.4 at MV 3, ~1.5 at MV 8+.
- Bennie Smith (SCG): **6–8 cards at 0–1 MV**; warns against the "three-mana choke point" — >20% of the deck at MV 3 is a design flaw.
- Ramp by curve: avg MV 2.5 → 8–10 ramp; avg MV 3.5 → 12+ ramp.
- Avg Commander game ≈ 12 turns (rationale for capping high-MV cards).

---

## 4. Improving the casual formula (research-backed)

1. **Model the London mulligan.** Karsten's published *tables* bake in the London mulligan; the tool's hypergeometric explicitly does **not**. The mulligan raises effective consistency (you redraw bad opens), so the tool currently slightly **over-states** sources/lands needed vs Karsten's own numbers. A first-keepable-hand or simple mulligan-to-5 model would tighten agreement. (teryror gist, Schulze both flag this as the #1 gap.)
2. **Relax the (89+M)% threshold for casual multiplayer.** The thresholds are acknowledged as arbitrary; for long 4-player games you see far more cards, so requirements can drop by 1–2 sources. Consider a casual threshold of ~(85+M)% or a "games run long" draw-count bonus.
3. **Reconcile the land-count slope.** Decide between the scaled-60-card 1.90 slope (current) and Karsten's published 99-card 3.13 slope. They diverge most at high avg MV.
4. **Partial-source weighting is well-supported** — the tool's 0.74 untapped-MDFC / 0.38 tapped-MDFC / 0.28 ramp weights match published values (canadianhighlander, centurioncommander). Keep. Consider adding the 0.75-weight-for-removable-rocks idea (rocks die to artifact removal; lands rarely do) as an optional toggle.
5. **Low-curve over-recommendation:** the regression inflates land counts for very low-curve shells; sources say low-MV decks want 33–35 and rarely need >4 lands in play. A floor/curve-cap guard prevents recommending 38 lands to a 1.8-MV deck — which is exactly the cEDH-mode case.

---

## 5. Proposed concrete parameters

### cEDH mode
- **Land target:** drop to **28–31**. Cleanest implementation: keep the regression but (a) ensure fast mana + rocks are fully counted as land-discounts, and (b) apply a **lower floor (~28)** and a **−3 to −4 flat competitive adjustment** vs the casual output. Validation target: a sub-2.0-MV, 10–12-ramp deck should land at ~29–31.
- **Consistency target turn:** shift from "MV = turn" to an **early-interaction emphasis** — evaluate single/double-pip color access at **turns 1–3** (not at the spell's MV), since the spells that matter (free counters, cheap removal) are cast reactively early.
- **Threshold:** keep high color-access consistency (cEDH wants reliable early interaction), but **don't penalize land count** for it — solve color access via fixing + fast mana, not more lands.
- **Tapped-land penalty:** heavy in cEDH (≈0 tapped lands tolerated); untapped-only sources should dominate the early-turn requirement.

### Casual mode (default, improved)
- Add **London mulligan modeling** (raises consistency → fewer sources needed).
- Optional **relaxed multiplayer threshold** (~(85+M)% or extra cards-seen).
- **Low-curve land floor** so very low-MV casual decks aren't told to run 38 lands.

### Curve/castability feature (both modes)
- **Land-drop curve:** P(≥N lands by turn N) for the deck's actual land count — show the % for each land drop T1–T6. Flag any drop below the mode's threshold.
- **Per-spell castability:** for each spell, combine land-drop P(≥MV lands by turn MV) × color-source consistency → "this 5-drop casts on curve X% of the time."
- **Effective curve with ramp:** shift each spell's effective turn earlier by counting ramp (−1 turn per fast-mana, fractional for rocks/dorks).
- **Curve-health readout:** count cards by MV, flag the >20%-at-MV-3 choke point, check the 6–8 cards at 0–1 MV (heavier for cEDH), confirm the peak sits at MV 2–3.

---

## 6. Source list

- ScrollVault Commander land data / archetype curve-shape table — https://scrollvault.net/guides/commander-land-count-data.html
- ScrollVault how-many-lands (60-card 1.90 slope, ramp fractions) — https://scrollvault.net/guides/how-many-lands.html
- ScrollVault mana-bases (color sources, 1.6× Commander scaling) — https://scrollvault.net/guides/mana-bases.html
- ScrollVault manabase calculator — https://scrollvault.net/tools/manabase/
- Schulze, "The Math of Landbases in Commander" (land-drop %, ramp weights, mulligan) — https://medium.com/@schulze.mtg/the-math-of-landbases-in-magic-the-gathering-commander-3f03aadac92c
- EDHREC cEDH mana guide — https://edhrec.com/guides/edhrec-guide-to-mana-in-cedh
- EDHREC mana curves for beginners (aggregate data) — https://edhrec.com/articles/commander-mana-curves-for-beginners
- StarCityGames, Bennie Smith "three-mana choke point" — https://articles.starcitygames.com/articles/the-three-mana-choke-point-in-commander/
- canadianhighlander singleton manabase (partial-source weights) — https://canadianhighlander.ca/2023/07/17/how-to-build-a-manabase-for-singleton-formats/
- teryror gist (Karsten limitations, mulligan/turn-1 draw gaps) — https://gist.github.com/teryror/881d60e08480a56043895d3bbb83c374
- centurioncommander 1v1 variance (alt 31.42+3.13·avgMV form, MDFC weights) — https://www.centurioncommander.eu/variance-in-1v1-commander-a-deep-dive/
- intothe99 effective mana bases — https://www.intothe99.com/post/the-ultimate-guide-to-effective-mana-bases
- draftsim mana rocks / land counts — https://draftsim.com/how-many-mana-rocks-edh/ , https://draftsim.com/mtg-edh-deck-number-of-lands/
- Karsten originals (not cleanly fetched — image/paywall): TCGplayer 2022 source update, ChannelFireball color-sources, orkerhulen.dk land-drop PDF

**Confidence flags:** deep-research verification phase failed (rate-limited) — its claims are unverified but cross-corroborated. Original Karsten primary tables unfetched. Commander land-drop % vary by mulligan model. cEDH per-card interaction counts are community consensus, not one authoritative primer. The 1.6× color-source scaling is ScrollVault's, not Karsten's.
