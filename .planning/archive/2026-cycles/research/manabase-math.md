# Mana-base Math: Lands, Ramp & Color Sources vs Curve

Research compiled 2026-06-20 for DeckFlow deck-analysis features. Covers Commander (100), cEDH, and 60-card constructed.

> **Verification caveat:** This was gathered via the deep-research workflow. The
> adversarial-verify phase was rate-limited (every claim scored "3 abstain", NOT
> refuted) — so the workflow's auto-summary falsely reported "all refuted." The
> harvested claims below are real and match established Karsten/EDHREC knowledge.
> Confidence tags added manually: **[H]** high (well-known, corroborated), **[M]**
> medium (single blog source), **[L]** low (unreliable-rated source, verify before coding).

---

## 1. The core method — hypergeometric distribution

Every "do I have enough?" question reduces to one probability model. Given a deck
of `N` cards with `K` "successes" (lands, or sources of a color), drawn `n` cards,
the chance of seeing at least `k` successes is:

```
P(X >= k) = 1 - sum_{i=0}^{k-1} [ C(K,i) * C(N-K, n-i) / C(N, n) ]
```

- `n` = opening hand (7) + draws by the target turn. On the play, turn T → `n = 6 + T`
  (7-card hand, no turn-1 draw, then one per turn). On the draw → `n = 7 + T`.
- This is what every "mana calculator" (Karsten's tables, the hypergeometric
  calculators on stattrek / mtgazone) computes under the hood. **[H]**

**Probability thresholds:**
- Karsten's published bar = **(89 + M)%** by mana value M: 90% (1-drop) → 96% (7-drop),
  conditional on drawing ≥ M lands by turn M, on the play. **[H, verbatim]**
- Looser community heuristic (canadianhighlander.ca): 75% "okay" / 85% "reliable" / 90%
  "very reliable." **[M]**
- Beyond the threshold, added sources yield diminishing returns.

---

## 2. Color SOURCES needed for a given pip requirement

This is Frank Karsten's "How Many Sources Do You Need to Consistently Cast Your
Spells" (ChannelFireball 2018, TCGplayer 2022 update). The deliverable: a table of
sources needed to hit ~90% of casting on-curve.

**AUTHORITATIVE — verbatim from Karsten's TCGplayer 2022 update** (fetched via headless
browser 2026-06-20; the two tables below are his actual published numbers). **[H]**

**Threshold:** Karsten targets **(89 + M)%** consistency, where M = the spell's mana
value: **90%** for 1-drops, **91%** for 2-drops, 92% for 3, … up to **96%** for 7-drops.
Higher-MV spells get a stricter bar because missing your color on a 5-drop hurts far
more than on a 1-drop. The probability is **conditional**: it assumes you already drew
≥ M lands by turn M (land-count failures are handled separately in §3), on the play,
under a reasonable London-mulligan strategy.

### Table 1 — sources needed by DECK SIZE (Karsten's default land counts) **[H]**
Default lands assumed: 40-card→17, 60-card→25, 80-card→35, 99-card→41. Sorted low→high.

| Mana cost | Example | 40-card | 60-card | 80-card | 99-card |
|---|---|---|---|---|---|
| `5C`  | Drowner of Hope | 6 | 9 | 12 | 14 |
| `4C`  | Doubling Season | 6 | 9 | 14 | 15 |
| `3C`  | Collected Company | 7 | 10 | 15 | 16 |
| `2C`  | Reckless Stormseeker | 8 | 12 | 16 | 18 |
| `5CC` | Hullbreaker Horror | 8 | 12 | 17 | 20 |
| `1C`  | Ledger Shredder | 9 | 13 | 18 | 19 |
| `4CC` | Primeval Titan | 9 | 13 | 19 | 22 |
| `C`   | Monastery Swiftspear | 9 | **14** | 19 | 19 |
| `3CC` | Baneslayer Angel | 10 | 15 | 20 | 23 |
| `4CCC`| Nyxbloom Ancient | 10 | 16 | 22 | 26 |
| `2CC` | Wrath of God | 11 | 16 | 23 | 26 |
| `3CCC`| Massacre Wurm | 11 | 17 | 24 | 28 |
| `1CC` | Narset, Parter of Veils | 12 | 18 | 25 | 28 |
| `2CCC`| Garruk, Primal Hunter | 13 | 19 | 26 | 30 |
| `CC`  | Lord of Atlantis | 14 | **21** | 28 | 30 |
| `1CCC`| Cryptic Command | 14 | 21 | 29 | 33 |
| `1CCCC`| Unnatural Growth | 15 | 22 | 31 | 36 |
| `CCC` | Goblin Chainwhirler | 16 | **23** | 32 | 36 |
| `CCCC`| Dawn Elemental | 17 | 24 | 34 | 39 |

Canonical 60-card quick-reference: **single pip `C`=14, `1C`=13; double `CC`=21,
`1CC`=18; triple `CCC`=23.** 99-card EDH: **`C`=19, `CC`=30, `CCC`=36.** Note Commander
one-/two-drop requirements *dropped* vs older articles because Karsten now models the
free mulligan + free draw. Triple-pip-on-curve is the hardest constraint.

### Table 2 — 60-card sources by actual LAND COUNT (adjust off the default) **[H]**

| Mana cost | Example | 20 lands | 25 lands | 30 lands |
|---|---|---|---|---|
| `5C`  | Drowner of Hope | 7 | 9 | 10 |
| `4C`  | Doubling Season | 8 | 9 | 11 |
| `3C`  | Collected Company | 9 | 10 | 12 |
| `2C`  | Reckless Stormseeker | 10 | 12 | 13 |
| `5CC` | Hullbreaker Horror | 10 | 12 | 15 |
| `1C`  | Ledger Shredder | 11 | 13 | 14 |
| `4CC` | Primeval Titan | 11 | 13 | 16 |
| `C`   | Monastery Swiftspear | 12 | 14 | 15 |
| `3CC` | Baneslayer Angel | 12 | 15 | 17 |
| `4CCC`| Nyxbloom Ancient | 12 | 16 | 19 |
| `2CC` | Wrath of God | 13 | 16 | 19 |
| `3CCC`| Massacre Wurm | 14 | 17 | 20 |
| `1CC` | Narset, Parter of Veils | 15 | 18 | 21 |
| `2CCC`| Garruk, Primal Hunter | 15 | 19 | 22 |
| `CC`  | Lord of Atlantis | 18 | 21 | 23 |
| `1CCC`| Cryptic Command | 17 | 21 | 24 |
| `1CCCC`| Unnatural Growth | 18 | 22 | 26 |
| `CCC` | Goblin Chainwhirler | 19 | 23 | 27 |
| `CCCC`| Dawn Elemental | 20 | 24 | 29 |

More lands → each color needs MORE sources to hold its share. (The teryror-gist
matrices in §0-history were close approximations; these are Karsten's exact numbers.)

### Karsten's source-counting rules (verbatim) **[H]**
- **Gold / multicolor:** add **+1 source to each color's requirement** (you need all
  colors present — independent ~90%×90% = 81% would be too low). Skip if only one color
  is consistency-critical (a splash into mono-base = treat as that single pip).
- **Hybrid:** count **combined** sources of either color in the hybrid cost.
- **Mana dork** (Llanowar Elves, Birds), for MV≥2 spells: **0.5 source** per color it
  makes — *only if* the deck can reliably cast the dork (≥14 untapped green in 60-card).
  "Bolt the Bird" — it may die.
- **Mana rock** (Signets, Arcane Signet), for MV≥3 spells: **0.75 source** per color.
- **Cheap scry-1** effect ≈ **0.2 source**.
- **Fetch / choice-land** (Fabled Passage, Pathway): 2-color deck → **full source** both
  colors; 3+ colors with heavy requirements → **~2/3 source** each color.
- **Land/spell MDFC** (for color-source counting): non-mythic ≈ **0.8 source**, mythic ≈
  **full source**. (Different from land-*count* weighting: non-mythic 0.40 land, mythic
  0.75 land — see §3 formula, which uses 0.74/0.38 as the fit coefficients.)
- **Turn 1:** only **untapped** sources count (tap-lands don't help cast a 1-drop T1);
  turn 2+ count all sources. Keep ≤3 tap-lands in aggro, ≤9 in midrange/control (60-card).
- **Colorless / snow** = treat as its own "color" (Thought-Knot Seer needs 10 colorless
  sources in 60-card; Arcum's Astrolabe needs 14 snow sources).
- **X-spells / convoke / delve / cost-reduction:** model the typical lands you'd actually
  tap (e.g. treat Murktide as `1UU`).

### 40-card Limited **[H]**
Covered in Table 1 (default 17 lands; 16 aggro / 18 control). Source numbers in the
40-card column.

### Source-counting adjustments **[M]**
- **Discount fragile sources:** mana creatures / dorks vulnerable to removal count
  for less — e.g. count 4 copies of a mana-dork as ~2 effective sources. **[M]**
- Dual / fetch / any-color lands count as a source for **each** color they make.
- **Two-color 60-card dual-land requirements** (duals needed at 95% keepable-hand
  consistency; cells = thresholds for 2/3/4 of-each-color by turn 2-3-4): **[M]**

  | Lands | 0 off-color | 1 | 2 | 3 | 4 |
  |---|---|---|---|---|---|
  | 24 | 14/12/7 | 14/13/8 | 15/14/9 | 16/15/10 | 16/15/11 |
  | 26 | 14/13/8 | 15/14/9 | 16/15/10 | 17/15/11 | 18/16/12 |
  | 28 | 15/13/8 | 16/14/9 | 17/15/10 | 18/16/11 | 18/16/12 |

  Each off-color land added raises the dual requirement ~1–2.

---

## 3. Land COUNT as a function of curve (avg mana value)

### Frank Karsten land-count formulas (regression-fit to his simulations) **[M]**

**Commander / singleton (98–99 cards):**
```
lands = ((100 - commanders)/60)
        * (19.59 + 1.90*avgMV + 0.27*commanders)
        - 0.28*(ramp + draw)
        - fast
        - 0.74*mdfc1 - 0.38*mdfc2
        - 1.35
```

**60-card formats:**
```
lands = 32.65 + 3.16*avgMV
        - 0.28*(ramp + draw)
        - fast
        - 0.74*mdfc1 - 0.38*mdfc2
```

Where:
- `avgMV` = average mana value of nonland cards (the curve)
- `ramp + draw` = count of ramp/card-draw spells of **MV ≤ 2** (each ≈ **−0.28 lands**)
- `fast` = 0-cost mana artifacts (Lotus, Moxen) → **−1.0 land each** (1:1). **Sol Ring is an
  outlier worth ~0.8+** on its own.
- `mdfc1`/`mdfc2` = modal double-faced land-cards (one side a land) → −0.74 / −0.38 each.

> The **3.16 coefficient on avgMV (60-card)** vs **~1.90 (commander interior term)** is
> the key insight: in 60-card, each +1 average MV demands ~3 more lands; commander's
> larger deck + ramp dampens that.

A widely-cited simplification (ScrollVault, attributes to Karsten — **[L]**, verify):
```
lands ≈ 31.42 + 3.13*avgMV − 0.28*(ramp + draw)
```
→ a 3.0-MV, 10-ramp midrange commander deck ≈ **38 lands**. Lines up with the full
formula, so plausible.

### Curve-band rules of thumb

**60-card (Karsten CMC bands):** **[M]**
| Curve | mean CMC | lands |
|---|---|---|
| Aggro / low | ~0.5–2.1 | 19–22 |
| Midrange / control | ~2.1–3.3 | 23–26 |
| High-curve control | 3.3+ | 27 |

**Simulation-based update (teryror gist) — scales with deck size:** **[L]**
| | low curve | mid | high |
|---|---|---|---|
| 60-card | 22–23 | 26 | 30 |
| 99-card Commander | 36→31 | 42→38 | 49→38 |

(Commander ranges collapse toward the same number at high curve because ramp/draw
offsets dominate.)

---

## 4. Empirical statistics (real decks)

**EDHREC aggregate (superior-numbers-land-counts article):** **[M]**
- Average Commander deck: **~29 lands + 4.15 mana rocks**; recent-year decks trend to **31 lands**.
- **Preconstructed** decks average **>37 lands** — much higher than the user-aggregate ~29–31.
  (Aggregate is pulled down by ramp-heavy / low-curve optimized lists.)
- **Goldfish test, 100 random EDHREC decks:** 26% missed their T3 land drop with *zero*
  mana source T1–T3; another 21% missed the land drop but had a rock. → ~47% stumble,
  evidence the aggregate ~30 is arguably too low for casual curves. **[M]**

**cEDH:** runs **much lower land counts** (often ~29–31 or fewer) because avg MV is
**~1.7–2.0** vs **3+** casual, plus heavy fast-mana / low-curve. Land count tracks
inversely with ramp density. **[M]**

**ScrollVault recommended bands** (**[L]** — unreliable source, directional only):
- cEDH Turbo (1.8 avgMV, 12 ramp): **29–31 lands**
- Battlecruiser (3.5 avgMV) / Landfall (3.0): **38–40 lands**

---

## 5. RAMP — how much, and how it counts

**Draftsim guidance:** **[M]**
- Commander land target: **37–38 lands**.
- **Total mana sources (lands + ramp): 43–50.**
- Ramp scales with curve:
  - Low curve (1–3 drops): **6–8 ramp pieces**
  - High curve (5–8+ drops): **10–12+ ramp sources**

**How ramp substitutes for lands (from Karsten formula):**
- A ramp or draw spell of **MV ≤ 2 ≈ −0.28 lands**. So ~3.5 cheap ramp pieces let you
  cut one land.
- 0-cost fast mana ≈ −1 land. Sol Ring ≈ −0.8.
- → A common heuristic "**38 lands + 10–12 ramp**" is consistent: 10 ramp ≈ −2.8 lands
  off a ~40-land baseline → ~37 lands, matching Draftsim/precon averages.

---

## 6. Putting it together — a DeckFlow scoring recipe

For a loaded deck, DeckFlow can compute:

1. **Curve** → `avgMV` of nonland cards.
2. **Target lands** → plug `avgMV`, ramp/draw(≤2) count, fast-mana, MDFCs into the
   format formula (§3). Compare to actual land count → over/under flag.
3. **Ramp adequacy** → count ramp(≤2 MV) + fast mana; check against the curve band
   (§5). Low-curve 6–8, high-curve 10–12+.
4. **Per-color sources** → for each color, count sources (lands making it + rocks/
   dorks making it, dorks discounted ~0.5). For the deck's heaviest pip requirement
   per color (max pip count at lowest MV), look up the Karsten target (§2) and flag
   colors below their ~90% threshold. Single pip→14(60)/~19(100), double→20/~40, triple→23/~higher.
5. **Overall consistency** → hypergeometric P(≥ k lands by turn T) using §1 for a
   sanity "keepable hand" number.

---

## Sources (quality as rated by harvester)

| Source | Quality | Used for |
|---|---|---|
| canadianhighlander.ca — singleton manabase guide (2023) | blog | Karsten formulas, thresholds |
| mtgazone.com — hypergeometric calculator guide | blog | method, source discounting |
| edhrec.com — superior-numbers land counts | secondary | empirical averages, goldfish test |
| draftsim.com — how much ramp in commander | blog | ramp counts, 43–50 sources |
| gist teryror/881d60e... | blog | curve→land tables, dual-land math |
| nivanov129.github.io/karsten-calculator | blog | Karsten method, 3 deck sizes |
| scrollvault.net — land count data | **unreliable** | directional bands only |
| **tcgplayer — Karsten "How Many Sources… 2022 Update" (PRIMARY)** | authoritative | §1 threshold, §2 Tables 1+2 + all counting rules (verbatim via headless browser) |
| mtg.cardsrealm.com | unreliable | 0 claims |

> **§2 gap CLOSED — primary source captured (2026-06-20, headless browser).** The
> TCGplayer Karsten article is JS-rendered (blocks WebFetch/404) but opened fine in the
> gstack headless browser. §1 threshold, §2 Tables 1 & 2, and all source-counting rules
> are now Karsten's **verbatim** published numbers — [H]/authoritative. The earlier
> teryror-gist / scrollvault reconstructions agreed within ±1, confirming them.

---

## 7. Reference implementation — Salubrious Snail Manabase Tool

A live, free competitor/reference at **salubrioussnail.com/manabase-tool**, explicitly
**built on Frank Karsten's 2022 article** (same primary source as §2). Worth studying
before building DeckFlow's version — it solves the exact problem. Inspected 2026-06-20.

**Inputs:** paste a decklist (Moxfield "Copy for MTGO", Archidekt, TappedOut, deckstats,
MTGGoldfish Arena export, Scryfall) → Load → Compute.

**Two metrics it reports:**
- **Cast Rate** — for a spell of mana value X, P(you have the mana to cast it by turn X).
- **Average Delay** — avg turns past X you must wait to cast it (captures color-starved
  topdecking that cast-rate alone misses).
- Benchmarks: **90% cast rate / 0.3 delay = strong; 80% / 0.6 = needs work.**

**How it recommends fixes (clever, steal this):**
- Recomputes metrics for perturbed manabases: **"+1 Wastes"** (one extra colorless land)
  and **"+1 Basic"** per basic type.
- **+1 Wastes** big improvement → deck needs **more lands overall** (mana-value support).
- **+1 Plains** much better than +1 Wastes → deck needs **more white sources** (color
  support). Roughly equal → white is fine. This isolates "not enough lands" from "wrong
  colors" — exactly the two questions the user asked.

**Tap Analyzer:** models how often lands enter untapped and the early-curve cost of
tap-lands (re-runs with one-fewer and zero tap-lands to score the impact).

**Model accounts for:** ramp (rocks, dorks, land-tutors), MDFCs (played land-side when a
land is needed), landcycling, fetches (probability split between optimal + random grab),
X spells (X=3 single / 2 double / 1 triple pip), basic-land tutors, **Karsten mulligan
heuristic**.

**Model ignores (DeckFlow could differentiate here):** card draw/filtering (any form),
cost-cheaters (Reanimate, Sneak Attack — has an "ignore" toggle), multiple spells per
turn, rituals / treasures / powerstones / cost-reducers / command-zone ramp, playing
lands off the top, general tutors for lands, **colorless & snow costs**, kicker/modal costs.

**Other site assets:** EDHRECrec (recommender), Calculators page (embedded Google-Sheets
calculators), Featured Decklists. YouTube: youtube.com/@salubrioussnail (incl. "We Built
a Tool to Yell at You About Your Manabase"). Patreon-supported, ~$850/mo.

> **Takeaway for DeckFlow:** the "+1 Wastes vs +1 Basic" perturbation trick is a clean,
> implementable way to turn raw hypergeometric numbers into *actionable* "add lands" vs
> "add white sources" advice — better UX than dumping Karsten tables at the user. And the
> ignored-factors list (card draw, colorless/snow, rituals) is a ready feature-gap map if
> DeckFlow wants to one-up it.
