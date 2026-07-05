# P88 Prototype — Measured-Style Profile for Salubrious Snail

*Live prototype (2026-07-04): fetched all 39 public Archidekt Commander decks, computed the measured half of the style profile from Archidekt oracle data + the creator's own per-card category labels. Throwaway Python (`scratchpad/snail_stats.py`); production is C# in P88. This validates that the metrics are computable and surfaces design lessons.*

## Result: a real measured-style profile

```
Avg lands/deck:      37.41          (Karsten/CommandZone target ~37-38 — disciplined)
Avg nonland MV:      3.27           (midrange)
Avg combo cards:     6.97/deck      (Archidekt atomicCombos/potentialCombos flags)
Creator category-label coverage: 18% of nonland cards
```

**Creator-labeled category totals (canonical, across 39 decks):**
| cat | total | ~/deck |
|---|---|---|
| removal | 218 | 5.6 |
| ramp | 204 | 5.2 |
| draw | 122 | 3.1 |
| tutor | 28 | 0.7 |
| combo | 14 | 0.4 |
| counter | 11 | 0.3 |
| wipe | 9 | 0.2 |

**Oracle power-flags (free bracket signal):** gameChanger 0.33/deck, massLandDenial 0.31/deck → confirms **bracket 2-3 midrange, NOT cEDH** (matches the edhBracket metadata).

**Aggregate mana curve (nonland):** peaks MV2 (26%) + MV3 (26%), 11% at MV1, only 6% at 7+ → textbook midrange curve, low top-end.

**Colored-pip balance (catalog-wide):** G 26%, U 23%, W 20%, R 16%, B 15% → green/blue lean overall (per-deck identity varies; this is the aggregate brewer signature).

**Staple-strip validated (CS-05):** the most-shared cards are exactly the ubiquitous staples to strip before style clustering —
```
23/39 Command Tower · 19/39 Sol Ring · basics 15-17/39 · 14/39 Exotic Orchard
13/39 Negate · 10/39 Arcane Signet · 10/39 Rogue's Passage
```
**After strip, signature cards emerge** (in 3-6 decks — style-bearing, not ubiquitous):
```
Fellwar Stone · Soul Shatter · Thalia Guardian of Thraben · Hedron Archive
Elvish Mystic · Treasure Cruise · Teshar Ancestor's Apostle · Muddle the Mixture
Mizzium Mortars · Hunter's Insight · Liquimetal Torque
```
This is the raw material for the characteristic-card list and lift stats (CS-07).

## P88 design lessons (feed the phase plan)

1. **⚠ Creator categories cover only 18% of cards** — Archidekt user labels are sparse and inconsistent (some decks fully tagged, most barely). **Cannot rely on them alone.** They're a high-signal *seed*, but CS-06 MUST fill the other ~82% via Scryfall Tagger oracle tags / oracle-text heuristics. The 5.6 removal / 5.2 ramp figures above are **undercounts** (labeled subset only); true ratios are higher. Ordering (removal≈ramp > draw ≫ tutor/counter/wipe) is still trustworthy.

2. **Archidekt oracle payload is rich enough for most metrics with zero extra API calls:** `cmc` (curve), `manaCost` (pips), `types` (land/creature/type ratios), `gameChanger`/`massLandDenial`/`tutor`/`extraTurns` (bracket + power signals), `atomicCombos`/`potentialCombos`/`twoCardComboSingelton` (combo density), `salt`, `edhrecRank` (staple-ness / popularity). Scryfall is only needed to *fill category gaps* and for card-name grounding (P91), not for the core math.

3. **`edhrecRank` gives staple-ness for free** — high-rank (low number) + high cross-deck frequency = staple to strip. Can drive CS-05/CS-07 without a separate popularity source.

4. **Commander(s) must be excluded from the 99-card stats** — filtered via the `Commander` category label; verify every deck labels its commander or fall back to `deckFormat`/command-zone detection.

5. **Snail is a good fusion test case:** land discipline (37.4) *agrees* with stated Command-Zone-style templates, but the low gameChanger/wipe counts + broad color spread suggest a theme-first brewer who *underweights* board wipes vs the canonical "6 mass disruption" — a concrete **say-vs-do delta** for P90 to surface once the P89 stated-rules ledger exists.

## Coverage / caveats
- Ratios derived only from Archidekt's own oracle flags + 18% creator labels; the production extractor must tag the unlabeled remainder (CS-06) before the category ratios are authoritative.
- Pip balance is catalog-aggregate; per-deck color identity is the more useful per-profile feature.
- Per-deck JSON dumped to `scratchpad/snail_profile.json` (not committed — regenerable from the API).
