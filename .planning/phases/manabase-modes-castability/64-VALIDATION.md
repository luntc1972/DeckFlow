# Phase 64 — Validation (VALIDATE-01 / SC4 / SC6)

## Reference deck #1 — Brago, King Eternal (Azorius WU blink)

Use this deck as the primary cross-check fixture: run it through the analyzer AND the
**Salubrious Snail mana calculator**, log per-color raw source counts + weakest color +
per-card cast% deltas below once Wave 1 is built.

**Quick characterization (for expectations):**
- Commander: **Brago, King Eternal** — `{2}{W}{U}`, MV 4, colors **W/U** (COMMANDER-01: WU is the elevated-priority identity).
- **Commander importance (COMMANDER-02): Central** — Brago is a recast-every-turn engine; cast ASAP every game. Run the analyzer at `CommanderImportance.Central` for this deck, and also spot-check `Low` to confirm the verdict shifts (Central should weight WU on-curve access hardest).
- Color identity: **Azorius (W/U)** only — clean two-color, good for color-balance validation.
- Lands ≈ 36–38: 9 Island, 9 Plains, + WU duals/utility (Adarkar Wastes, Glacial Fortress, Hallowed Fountain, Mystic Gate, Nimbus Maze, Prairie Stream, Sea of Clouds, Reflecting Pool, Hengegate↔Mistgate MDFC, Witch Enchanter↔Witch-Blessed Meadow MDFC, Flooded Strand fetch, Academy Ruins, Access Tunnel, Rogue's Passage, Urza's Saga, Wastes).
- Ramp / rocks (should be counted in pools, hidden from castability rows): Sol Ring, Arcane Signet, Azorius Signet, Talisman of Progress, Fellwar Stone, Thought Vessel, Springleaf Drum, Solemn Simulacrum.
- Exercises: WW/UU castability, two-color COLOR-AGG, commander pinning, ramp pool weighting, MDFC partial-land weighting, colorless payoffs (Sol Ring/Thought Vessel excluded; Wastes is a land).
- NOT heavily exercised: REDUCE-01 (no obvious "spells you cast cost less" static reducer) and GRANT-01 (no Cryptolith Rite / Relic of Legends). → add a second fixture deck that has a reducer + a granter to cover those paths.

**Expectations to verify (not yet run — Wave 1 pending):**
- Land target (Casual): Brago lists usually run ~36–38; analyzer should land near there.
- Weakest color: W vs U should be close; whichever has fewer untapped sources for its earliest double-pip should surface. Compare directly to Salubrious Snail.
- Commander Brago (WU, turn 4): should appear pinned in the castability list with a high but not 100% on-curve cast%.

### Results — analyzer run (real Scryfall resolve, 2026-06-21)

Produced by the gated harness `DeckFlow.Web.Tests/Manabase/BragoRealDeckHarness.cs` →
full dump in `64-harness-brago-output.md`. Resolved 100 cards / 84 distinct / **33 lands** /
avg MV **2.89** / 14 cheap ramp-draw / 0 cost-reducers.

Snail run via headless browser (gstack) at ianrh125.github.io/snail-analyzer, commander Brago,
importance "critical / 50x" (= our Central). Screenshot: `/tmp/snail-brago.png`.

| Metric | Our analyzer | Salubrious Snail | Verdict |
|---|---|---|---|
| **Weakest color** | **Blue** | **Blue** | ✅ MATCH |
| Best land to add | add land, Blue short → Island | **+1 Island** (+1.28%) | ✅ MATCH |
| Land delta direction | +3 lands | +Island helps most | ✅ consistent |
| Per-card ordering | Deadeye/Sun Titan worst, Brago high | same | ✅ MATCH |
| Per-card cast% magnitude | ~28–36 pts LOWER | baseline | ❌ see FINDING-3 |

Per-card cast rate (Snail "cast rate" vs our CastPercent, Casual):
| Card | Cost | Snail | Ours | Δ |
|---|---|---|---|---|
| Brago, King Eternal | 2WU | 85.4% | 53% | −32 |
| Deadeye Navigator | 4UU | 52.5% | 25% | −27 |
| Sun Titan | 4WW | 52.5% | 25% | −27 |
| Riptide Gearhulk | 1WWUU | 63.5% | 27% | −36 |
| Quantum Riddler | 3UU | 68.4% | 34% | −34 |
| Venser, the Sojourner | 3WU | 70.7% | 41% | −30 |
| Grand Abolisher | WW | 79.0% | 49% | −30 |

### FINDING-1 — RESOLVED (no fix needed)
Worry was the composite tie-break ranking Blue over higher-deficit White. **Snail independently
flags Blue weakest too** → our COLOR-AGG composite verdict is correct/aligned. No tie-break change.

### FINDING-3 — RESOLVED 2026-06-21 (Monte-Carlo simulator, mean |Δ| 3.4 pts)
Replaced the `P_mana × P_color` independence product with a seeded Monte-Carlo castability
simulator (`CastabilitySimulator.cs`): joint mana+color check, London mulligan, ramp deployed
in-sim, ETB-tapped lands online next turn, conditional granted sources Bernoulli-activated while
deployable ramp enters at full value (the sim models its deploy friction). Final Brago vs Snail:
Brago 84/85.4, Deadeye 49/52.5, Sun Titan 49/52.5, Riptide 62/63.5, Quantum 71/68.4,
Grand Abolisher 71/79.0, Archaeomancer 76/79.0 → **mean |Δ| 3.4 pts**, weakest color Blue,
ordering preserved. Only outlier: Grand Abolisher (WW T2, color-limited) at −8. Codex-reviewed
(BLOCK→fixed→APPROVE). Original analysis below for history.

### FINDING-3 (original) — cast% magnitude ~30 pts low vs Snail
Ordering + weakest-color match Snail, but every absolute cast% is ~28–36 pts lower. Root causes:
1. **No London mulligan model** — Snail uses Karsten's mulligan heuristic; we don't. This was
   already our #1 listed casual improvement and is the biggest single gap (a mulligan-to-keep
   raises early-land consistency a lot).
2. **Independence `P_mana × P_color`** is pessimistic — multiplying two correlated events
   understates the joint. Snail models it more tightly (and reports a single cast rate + an
   "average delay" rather than a strict on-curve product).
3. We evaluate strict "on its exact turn"; Snail's delay metric is more forgiving.
**Takeaway:** the model is directionally trustworthy (rank order + weakest color validated) but
the headline % reads low. Before/with Wave 2, add the mulligan model and reconsider the
independence multiply so the displayed % tracks Snail/Karsten. Until then, present the number as a
**relative** "hardest-to-cast" ranking, not an absolute probability (the UI caveat already leans
this way — strengthen it).

### Bonus — Snail's commander-importance scale (consider aligning)
Snail uses **4 tiers** (10x / 20x / 30x / 50x). Our 3-tier maps cleanly (Low≈10x,
Standard≈20–30x, Central≈50x); a 4th tier is optional polish, not required.

### FINDING-2 (expected, not a bug) — castability is a stricter metric than land-only odds
4-drops ~53%, 6-drops ~25%. These are `P(enough mana by turn) × P(both colors by turn)` on the
play, on-curve — lower than Karsten's land-only "N lands by turn N" because they also require the
colored pips simultaneously. Internally consistent (hand-checked: 4-drop pMana≈0.57 × color≈0.85
≈ 0.48–0.53). UI caveat copy already states this; just confirm users read the % as "on-curve incl.
colors," not "ever castable."

### Mode/importance behavior observed (sanity ✓)
- cEDH target (32.3) < Casual (35.8), above 28 floor — MODE-02 working.
- `Casual·Central` changed the summary's "hardest to cast" headline to **Brago (53%)** (commander
  pinned/elevated); `Standard`/`Low` headline the global worst (Deadeye Navigator 25%) — COMMANDER-02
  scaling visibly active without changing the land target (still 35.8) — orthogonality ✓.

### Decklist (verbatim)
```
1 Brago, King Eternal (EMA) 198
1 Aang, Airbending Master (TLE) 74
1 Academy Ruins (DRC) 58
1 Access Tunnel (MKC) 247
1 Adarkar Wastes (DRC) 144
1 Aether Channeler (BLC) 160
1 Altar of the Brood (KTK) 216
1 An Offer You Can't Refuse (PLST) SNC-51
1 Arcane Denial (DRC) 70
1 Arcane Signet (SCD) 257
1 Archaeomancer (M14) 43
1 Avatar's Wrath (TLA) 12
1 Azorius Signet (SCD) 259
1 Charming Prince (FDN) 568
1 Cloud of Faeries (MOC) 219
1 Dawnbringer Cleric (CLB) 15
1 Deadeye Navigator (SLD) 902 *F*
1 Delivery Moogle (FIN) 15
1 Delney, Streetwise Lookout (MKM) 12
1 Displace (EMN) 55
1 Dovin's Veto (WAR) 193
1 Eldrazi Displacer (OGW) 13
1 Ephemerate (MH1) 7
1 Felidar Guardian (AER) 19
1 Fellwar Stone (PLST) CMD-248
1 Flare of Denial (MH3) 326
1 Flare of Fortitude (MH3) 26
1 Flooded Strand (MH3) 220
1 Ghostly Flicker (PLST) KHC-39
1 Ghostway (RVR) 308
1 Glacial Fortress (DRC) 159
1 Gossip's Talent (BLB) 51
1 Grand Abolisher (PBIG) 2p
1 Hallowed Fountain (RNA) 251
1 Hengegate Pathway / Mistgate Pathway (KHM) 260
1 Hide on the Ceiling (SPM) 32
9 Island (MKM) 280
1 Laboratory Maniac (UMA) 61
1 Loran's Escape (BRO) 14
1 Machine God's Effigy (BRC) 63
1 Mystic Gate (M3C) 359
1 Mystic Remora (SLD) 406
1 Nimbus Maze (IMA) 242
1 Peregrine Drake (DMR) 292
1 Permission Denied (REX) 17
1 Peter Parker's Camera (SPM) 171
1 Plagon, Lord of the Beach (J25) 37
9 Plains (M13) 230
1 Prairie Stream (M3C) 365
1 Quantum Riddler (EOE) 305
1 Reality Acid (TSR) 81
1 Recruiter of the Guard (MH3) 266
1 Reflecting Pool (PCLB) 358s *F*
1 Reflector Mage (OGW) 157
1 Relic of Progenitus (MB2) 230
1 Riptide Gearhulk (PDFT) 219p
1 Rishadan Cutpurse (PLST) MMQ-93
1 Rogue's Passage (DDM) 77
1 Sea of Clouds (CLB) 360
1 Seasoned Dungeoneer (CLB) 610
1 Skyclave Apparition (MB2) 18
1 Sol Ring (M3C) 305
1 Solemn Simulacrum (DRC) 138
1 Springleaf Drum (BRR) 118
1 Starfield Vocalist (EOE) 78
1 Strionic Resonator (M14) 224
1 Sun Titan (SLD) 1550
1 Swan Song (SLD) 1591
1 Swiftfoot Boots (LCC) 314
1 Swords to Plowshares (DSC) 106
1 Talisman of Progress (PIP) 249
1 Teleportation Circle (AFR) 39
1 Thassa, Deep-Dwelling (THB) 261
1 Thought Vessel (MB2) 100
1 Tribute Mage (MH1) 73
1 Urza's Saga (MB2) 114
1 Venser, Shaper Savant (J25) 66
1 Venser, the Sojourner (SLD) 1423★ *F*
1 Wall of Omens (2X2) 344
1 Wastes (SLD) 706
1 Whirler Rogue (NEC) 101
1 Whispersilk Cloak (DSC) 257
1 Witch Enchanter / Witch-Blessed Meadow (MH3) 239
1 Y'shtola Rhul (FIN) 86
```
