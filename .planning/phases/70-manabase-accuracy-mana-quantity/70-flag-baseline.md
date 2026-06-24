# Phase 70 — consolidated flag baseline (Core analyzer)

Each flag is turned on in ISOLATION vs the all-off baseline. MQ-02/MQ-05 are
Analyze-time; MQ-03 is classify-time (re-classifies the deck). Cast% is the seeded
Monte-Carlo display value; the verdict probe path is unaffected by MQ-02/MQ-05.

## Deck: Brago (WU control) (100 cards, 84 distinct)

### Casual · MQ-02 source-mana-quantity

- Health: NeedsWork → NeedsWork
- Land target: 35.8 (unchanged by this flag)
- Weakest color: Blue → Blue
- Cast%: 7/58 cards changed · mean |Δ| 0.2 pts · range +0..+2

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Sun Titan | 6 | 53 | 55 | +2 |
| Y'shtola Rhul | 6 | 53 | 55 | +2 |
| Aang, Airbending Master | 5 | 77 | 78 | +1 |
| Deadeye Navigator | 6 | 53 | 54 | +1 |
| Peregrine Drake | 5 | 78 | 79 | +1 |
| Quantum Riddler | 5 | 75 | 76 | +1 |
| Venser, the Sojourner | 5 | 78 | 79 | +1 |

### Casual · MQ-05 color-aware-mulligan

- Health: NeedsWork → NeedsWork
- Land target: 35.8 (unchanged by this flag)
- Weakest color: Blue → Blue
- Cast%: 42/58 cards changed · mean |Δ| 1.8 pts · range -1..+6

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Dovin's Veto | 2 | 91 | 97 | +6 |
| Permission Denied | 2 | 90 | 96 | +6 |
| An Offer You Can't Refuse | 1 | 94 | 99 | +5 |
| Grand Abolisher | 2 | 76 | 81 | +5 |
| Loran's Escape | 1 | 94 | 99 | +5 |
| Swan Song | 1 | 94 | 99 | +5 |
| Swords to Plowshares | 1 | 94 | 99 | +5 |
| Ephemerate | 1 | 95 | 99 | +4 |
| Flare of Denial | 3 | 81 | 85 | +4 |
| Mystic Remora | 1 | 94 | 98 | +4 |
| Skyclave Apparition | 3 | 82 | 86 | +4 |
| Arcane Denial | 2 | 96 | 99 | +3 |
| Archaeomancer | 4 | 80 | 83 | +3 |
| Cloud of Faeries | 2 | 96 | 99 | +3 |
| Reflector Mage | 3 | 92 | 95 | +3 |

### Casual · MQ-03 ramp-credit-v2

- Health: NeedsWork → NeedsWork
- Land target: 35.8 → 36.1 (ramp/draw<=2 14 → 13)
- Weakest color: Blue → Blue
- Cast%: 0/58 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Casual · 70-03b land-ramp-sim

- Health: NeedsWork → Functional
- Land target: 35.8 (unchanged by this flag)
- Weakest color: Blue → Blue
- Cast%: 10/58 cards changed · mean |Δ| 0.3 pts · range -1..+3

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Sun Titan | 6 | 53 | 56 | +3 |
| Aang, Airbending Master | 5 | 77 | 79 | +2 |
| Deadeye Navigator | 6 | 53 | 55 | +2 |
| Peregrine Drake | 5 | 78 | 80 | +2 |
| Venser, the Sojourner | 5 | 78 | 80 | +2 |
| Y'shtola Rhul | 6 | 53 | 55 | +2 |
| Charming Prince | 2 | 96 | 95 | -1 |
| Eldrazi Displacer | 3 | 95 | 94 | -1 |
| Gossip's Talent | 2 | 96 | 95 | -1 |
| Quantum Riddler | 5 | 75 | 76 | +1 |

### Cedh · MQ-02 source-mana-quantity

- Health: NeedsWork → NeedsWork
- Land target: 32.3 (unchanged by this flag)
- Weakest color: Blue → Blue
- Cast%: 7/58 cards changed · mean |Δ| 0.2 pts · range +0..+2

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Sun Titan | 6 | 53 | 55 | +2 |
| Y'shtola Rhul | 6 | 53 | 55 | +2 |
| Aang, Airbending Master | 5 | 77 | 78 | +1 |
| Deadeye Navigator | 6 | 53 | 54 | +1 |
| Peregrine Drake | 5 | 78 | 79 | +1 |
| Quantum Riddler | 5 | 75 | 76 | +1 |
| Venser, the Sojourner | 5 | 78 | 79 | +1 |

### Cedh · MQ-05 color-aware-mulligan

- Health: NeedsWork → NeedsWork
- Land target: 32.3 (unchanged by this flag)
- Weakest color: Blue → Blue
- Cast%: 42/58 cards changed · mean |Δ| 1.8 pts · range -1..+6

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Dovin's Veto | 2 | 91 | 97 | +6 |
| Permission Denied | 2 | 90 | 96 | +6 |
| An Offer You Can't Refuse | 1 | 94 | 99 | +5 |
| Grand Abolisher | 2 | 76 | 81 | +5 |
| Loran's Escape | 1 | 94 | 99 | +5 |
| Swan Song | 1 | 94 | 99 | +5 |
| Swords to Plowshares | 1 | 94 | 99 | +5 |
| Ephemerate | 1 | 95 | 99 | +4 |
| Flare of Denial | 3 | 81 | 85 | +4 |
| Mystic Remora | 1 | 94 | 98 | +4 |
| Skyclave Apparition | 3 | 82 | 86 | +4 |
| Arcane Denial | 2 | 96 | 99 | +3 |
| Archaeomancer | 4 | 80 | 83 | +3 |
| Cloud of Faeries | 2 | 96 | 99 | +3 |
| Reflector Mage | 3 | 92 | 95 | +3 |

### Cedh · MQ-03 ramp-credit-v2

- Health: NeedsWork → NeedsWork
- Land target: 32.3 → 32.6 (ramp/draw<=2 14 → 13)
- Weakest color: Blue → Blue
- Cast%: 0/58 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · 70-03b land-ramp-sim

- Health: NeedsWork → NeedsWork
- Land target: 32.3 (unchanged by this flag)
- Weakest color: Blue → Blue
- Cast%: 10/58 cards changed · mean |Δ| 0.3 pts · range -1..+3

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Sun Titan | 6 | 53 | 56 | +3 |
| Aang, Airbending Master | 5 | 77 | 79 | +2 |
| Deadeye Navigator | 6 | 53 | 55 | +2 |
| Peregrine Drake | 5 | 78 | 80 | +2 |
| Venser, the Sojourner | 5 | 78 | 80 | +2 |
| Y'shtola Rhul | 6 | 53 | 55 | +2 |
| Charming Prince | 2 | 96 | 95 | -1 |
| Eldrazi Displacer | 3 | 95 | 94 | -1 |
| Gossip's Talent | 2 | 96 | 95 | -1 |
| Quantum Riddler | 5 | 75 | 76 | +1 |

### Karsten closed-form cross-check (Casual)

Karsten = P(≥T lands) × hardest-color CastConsistency (the Snail/Karsten metric). Multi-color cards: our sim requires ALL colors jointly, so sim ≤ Karsten-hardest-single is expected.
- Mean |sim − Karsten|: OFF 28.5 pts → ALL-ON 30.3 pts (OFF closer)

| Card | MV | Karsten | sim OFF | sim ON | ON−Karsten |
|---|---|---|---|---|---|
| Venser, the Sojourner | 5 | 28 | 78 | 79 | +51 |
| Aang, Airbending Master | 5 | 28 | 77 | 78 | +50 |
| Peregrine Drake | 5 | 28 | 78 | 78 | +50 |
| Quantum Riddler | 5 | 28 | 75 | 77 | +49 |
| Brago, King Eternal | 4 | 44 | 87 | 88 | +44 |
| Delivery Moogle | 4 | 44 | 88 | 88 | +44 |
| Felidar Guardian | 4 | 44 | 88 | 88 | +44 |
| Seasoned Dungeoneer | 4 | 44 | 88 | 88 | +44 |
| Solemn Simulacrum | 4 | 44 | 89 | 88 | +44 |
| Starfield Vocalist | 4 | 44 | 88 | 88 | +44 |
| Teleportation Circle | 4 | 44 | 88 | 88 | +44 |
| Thassa, Deep-Dwelling | 4 | 44 | 88 | 88 | +44 |
| Witch Enchanter // Witch-Blessed Meadow | 4 | 44 | 88 | 88 | +44 |
| Riptide Gearhulk | 5 | 28 | 68 | 70 | +42 |
| Flare of Fortitude | 4 | 43 | 82 | 84 | +41 |

## Deck: Kenrith 5-color rocks (86 cards, 81 distinct)

### Casual · MQ-02 source-mana-quantity

- Health: Healthy → Healthy
- Land target: 22.8 (unchanged by this flag)
- Weakest color: none → none
- Cast%: 2/16 cards changed · mean |Δ| 0.1 pts · range -1..+0

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Atraxa, Praetors' Voice | 4 | 99 | 98 | -1 |
| Niv-Mizzet Reborn | 5 | 99 | 98 | -1 |

### Casual · MQ-05 color-aware-mulligan

- Health: Healthy → Healthy
- Land target: 22.8 (unchanged by this flag)
- Weakest color: none → none
- Cast%: 0/16 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Casual · MQ-03 ramp-credit-v2

- Health: Healthy → Healthy
- Land target: 22.8 → 22.8 (ramp/draw<=2 20 → 20)
- Weakest color: none → none
- Cast%: 0/16 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Casual · 70-03b land-ramp-sim

- Health: Healthy → Healthy
- Land target: 22.8 (unchanged by this flag)
- Weakest color: none → none
- Cast%: 0/16 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · MQ-02 source-mana-quantity

- Health: Healthy → Healthy
- Land target: 28.0 (unchanged by this flag)
- Weakest color: none → none
- Cast%: 2/16 cards changed · mean |Δ| 0.1 pts · range -1..+0

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Atraxa, Praetors' Voice | 4 | 99 | 98 | -1 |
| Niv-Mizzet Reborn | 5 | 99 | 98 | -1 |

### Cedh · MQ-05 color-aware-mulligan

- Health: Healthy → Healthy
- Land target: 28.0 (unchanged by this flag)
- Weakest color: none → none
- Cast%: 0/16 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · MQ-03 ramp-credit-v2

- Health: Healthy → Healthy
- Land target: 28.0 → 28.0 (ramp/draw<=2 20 → 20)
- Weakest color: none → none
- Cast%: 0/16 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · 70-03b land-ramp-sim

- Health: Healthy → Healthy
- Land target: 28.0 (unchanged by this flag)
- Weakest color: none → none
- Cast%: 0/16 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Karsten closed-form cross-check (Casual)

Karsten = P(≥T lands) × hardest-color CastConsistency (the Snail/Karsten metric). Multi-color cards: our sim requires ALL colors jointly, so sim ≤ Karsten-hardest-single is expected.
- Mean |sim − Karsten|: OFF 10.9 pts → ALL-ON 10.8 pts (ON closer to Karsten)

| Card | MV | Karsten | sim OFF | sim ON | ON−Karsten |
|---|---|---|---|---|---|
| The Ur-Dragon | 9 | 42 | 100 | 100 | +58 |
| Golos, Tireless Pilgrim | 5 | 83 | 100 | 100 | +17 |
| Kenrith, the Returned King | 5 | 83 | 100 | 100 | +17 |
| Teferi, Hero of Dominaria | 5 | 83 | 100 | 100 | +17 |
| Niv-Mizzet Reborn | 5 | 83 | 99 | 98 | +15 |
| Smothering Tithe | 4 | 90 | 100 | 100 | +10 |
| Kaalia of the Vast | 4 | 90 | 99 | 99 | +9 |
| Atraxa, Praetors' Voice | 4 | 90 | 99 | 98 | +8 |
| Cultivate | 3 | 95 | 100 | 100 | +5 |
| Kodama's Reach | 3 | 95 | 100 | 100 | +5 |
| Anguished Unmaking | 3 | 95 | 99 | 99 | +4 |
| Cyclonic Rift | 2 | 98 | 100 | 100 | +2 |
| Farseek | 2 | 98 | 100 | 100 | +2 |
| Nature's Lore | 2 | 98 | 100 | 100 | +2 |
| Assassin's Trophy | 2 | 98 | 99 | 99 | +1 |

## Deck: Meren Golgari ramp/ritual (83 cards, 61 distinct)

### Casual · MQ-02 source-mana-quantity

- Health: Functional → Functional
- Land target: 28.2 (unchanged by this flag)
- Weakest color: Green → Green
- Cast%: 2/39 cards changed · mean |Δ| 0.1 pts · range +0..+1

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Grave Titan | 6 | 80 | 81 | +1 |
| Old Gnawbone | 7 | 71 | 72 | +1 |

### Casual · MQ-05 color-aware-mulligan

- Health: Functional → Functional
- Land target: 28.2 (unchanged by this flag)
- Weakest color: Green → Green
- Cast%: 36/39 cards changed · mean |Δ| 1.2 pts · range +0..+3

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Abrupt Decay | 2 | 97 | 100 | +3 |
| Sakura-Tribe Scout | 1 | 97 | 100 | +3 |
| Bone Shards | 1 | 98 | 100 | +2 |
| Culling the Weak | 1 | 98 | 100 | +2 |
| Dark Ritual | 1 | 98 | 100 | +2 |
| Eternal Witness | 3 | 91 | 93 | +2 |
| Phyrexian Arena | 3 | 91 | 93 | +2 |
| Reanimate | 1 | 98 | 100 | +2 |
| Songs of the Damned | 1 | 98 | 100 | +2 |
| Vampiric Tutor | 1 | 98 | 100 | +2 |
| Animate Dead | 2 | 99 | 100 | +1 |
| Beast Within | 3 | 98 | 99 | +1 |
| Cabal Ritual | 2 | 99 | 100 | +1 |
| Casualties of War | 6 | 80 | 81 | +1 |
| Cultivate | 3 | 98 | 99 | +1 |

### Casual · MQ-03 ramp-credit-v2

- Health: Functional → Functional
- Land target: 28.2 → 29.3 (ramp/draw<=2 15 → 11)
- Weakest color: Green → Green
- Cast%: 0/39 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Casual · 70-03b land-ramp-sim

- Health: Functional → Healthy
- Land target: 28.2 (unchanged by this flag)
- Weakest color: Green → none
- Cast%: 15/39 cards changed · mean |Δ| 1.3 pts · range +0..+14

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Old Gnawbone | 7 | 71 | 85 | +14 |
| Grave Titan | 6 | 80 | 89 | +9 |
| Casualties of War | 6 | 80 | 87 | +7 |
| Massacre Wurm | 6 | 76 | 82 | +6 |
| Sidisi, Undead Vizier | 5 | 92 | 95 | +3 |
| Deadly Rollick | 4 | 97 | 99 | +2 |
| Abrupt Decay | 2 | 97 | 98 | +1 |
| Beast Within | 3 | 98 | 99 | +1 |
| Cultivate | 3 | 98 | 99 | +1 |
| Grim Haruspex | 3 | 98 | 99 | +1 |
| Kodama's Reach | 3 | 98 | 99 | +1 |
| Meren of Clan Nel Toth | 4 | 97 | 98 | +1 |
| Sheoldred, the Apocalypse | 4 | 94 | 95 | +1 |
| Victimize | 3 | 98 | 99 | +1 |
| Wood Elves | 3 | 98 | 99 | +1 |

### Cedh · MQ-02 source-mana-quantity

- Health: Functional → Functional
- Land target: 28.0 (unchanged by this flag)
- Weakest color: Green → Green
- Cast%: 2/39 cards changed · mean |Δ| 0.1 pts · range +0..+1

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Grave Titan | 6 | 80 | 81 | +1 |
| Old Gnawbone | 7 | 71 | 72 | +1 |

### Cedh · MQ-05 color-aware-mulligan

- Health: Functional → Functional
- Land target: 28.0 (unchanged by this flag)
- Weakest color: Green → Green
- Cast%: 36/39 cards changed · mean |Δ| 1.2 pts · range +0..+3

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Abrupt Decay | 2 | 97 | 100 | +3 |
| Sakura-Tribe Scout | 1 | 97 | 100 | +3 |
| Bone Shards | 1 | 98 | 100 | +2 |
| Culling the Weak | 1 | 98 | 100 | +2 |
| Dark Ritual | 1 | 98 | 100 | +2 |
| Eternal Witness | 3 | 91 | 93 | +2 |
| Phyrexian Arena | 3 | 91 | 93 | +2 |
| Reanimate | 1 | 98 | 100 | +2 |
| Songs of the Damned | 1 | 98 | 100 | +2 |
| Vampiric Tutor | 1 | 98 | 100 | +2 |
| Animate Dead | 2 | 99 | 100 | +1 |
| Beast Within | 3 | 98 | 99 | +1 |
| Cabal Ritual | 2 | 99 | 100 | +1 |
| Casualties of War | 6 | 80 | 81 | +1 |
| Cultivate | 3 | 98 | 99 | +1 |

### Cedh · MQ-03 ramp-credit-v2

- Health: Functional → Functional
- Land target: 28.0 → 28.0 (ramp/draw<=2 15 → 11)
- Weakest color: Green → Green
- Cast%: 0/39 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · 70-03b land-ramp-sim

- Health: Functional → Functional
- Land target: 28.0 (unchanged by this flag)
- Weakest color: Green → Black
- Cast%: 15/39 cards changed · mean |Δ| 1.3 pts · range +0..+14

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Old Gnawbone | 7 | 71 | 85 | +14 |
| Grave Titan | 6 | 80 | 89 | +9 |
| Casualties of War | 6 | 80 | 87 | +7 |
| Massacre Wurm | 6 | 76 | 82 | +6 |
| Sidisi, Undead Vizier | 5 | 92 | 95 | +3 |
| Deadly Rollick | 4 | 97 | 99 | +2 |
| Abrupt Decay | 2 | 97 | 98 | +1 |
| Beast Within | 3 | 98 | 99 | +1 |
| Cultivate | 3 | 98 | 99 | +1 |
| Grim Haruspex | 3 | 98 | 99 | +1 |
| Kodama's Reach | 3 | 98 | 99 | +1 |
| Meren of Clan Nel Toth | 4 | 97 | 98 | +1 |
| Sheoldred, the Apocalypse | 4 | 94 | 95 | +1 |
| Victimize | 3 | 98 | 99 | +1 |
| Wood Elves | 3 | 98 | 99 | +1 |

### Karsten closed-form cross-check (Casual)

Karsten = P(≥T lands) × hardest-color CastConsistency (the Snail/Karsten metric). Multi-color cards: our sim requires ALL colors jointly, so sim ≤ Karsten-hardest-single is expected.
- Mean |sim − Karsten|: OFF 15.1 pts → ALL-ON 16.4 pts (OFF closer)

| Card | MV | Karsten | sim OFF | sim ON | ON−Karsten |
|---|---|---|---|---|---|
| Old Gnawbone | 7 | 28 | 71 | 73 | +45 |
| Grave Titan | 6 | 40 | 80 | 82 | +42 |
| Casualties of War | 6 | 40 | 80 | 81 | +41 |
| Sidisi, Undead Vizier | 5 | 54 | 92 | 93 | +39 |
| Massacre Wurm | 6 | 40 | 76 | 77 | +37 |
| Deadly Rollick | 4 | 69 | 97 | 98 | +29 |
| Meren of Clan Nel Toth | 4 | 69 | 97 | 98 | +29 |
| Damnation | 4 | 68 | 94 | 95 | +27 |
| Sheoldred, the Apocalypse | 4 | 68 | 94 | 95 | +27 |
| Beast Within | 3 | 82 | 98 | 99 | +17 |
| Cultivate | 3 | 82 | 98 | 99 | +17 |
| Eternal Witness | 3 | 76 | 91 | 93 | +17 |
| Kodama's Reach | 3 | 82 | 98 | 99 | +17 |
| Putrefy | 3 | 82 | 98 | 99 | +17 |
| Wood Elves | 3 | 82 | 98 | 99 | +17 |

## Deck: Archidekt 23563520 — Marchesa Value (99 cards, 95 distinct)

### Casual · MQ-02 source-mana-quantity

- Health: Workable → Workable
- Land target: 38.4 (unchanged by this flag)
- Weakest color: Red → Red
- Cast%: 0/60 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Casual · MQ-05 color-aware-mulligan

- Health: Workable → Workable
- Land target: 38.4 (unchanged by this flag)
- Weakest color: Red → Red
- Cast%: 5/60 cards changed · mean |Δ| 0.1 pts · range +0..+1

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Alesha, Who Laughs at Fate | 3 | 92 | 93 | +1 |
| Blood Artist | 2 | 97 | 98 | +1 |
| Liliana, Dreadhorde General | 6 | 42 | 43 | +1 |
| Virtue of Persistence // Locthwain Scorn | 7 | 28 | 29 | +1 |
| Zurgo, Thunder's Decree | 3 | 91 | 92 | +1 |

### Casual · MQ-03 ramp-credit-v2

- Health: Workable → Workable
- Land target: 38.4 → 38.4 (ramp/draw<=2 1 → 1)
- Weakest color: Red → Red
- Cast%: 0/60 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Casual · 70-03b land-ramp-sim

- Health: Workable → Workable
- Land target: 38.4 (unchanged by this flag)
- Weakest color: Red → Red
- Cast%: 0/60 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · MQ-02 source-mana-quantity

- Health: NeedsWork → NeedsWork
- Land target: 34.9 (unchanged by this flag)
- Weakest color: Red → Red
- Cast%: 0/60 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · MQ-05 color-aware-mulligan

- Health: NeedsWork → NeedsWork
- Land target: 34.9 (unchanged by this flag)
- Weakest color: Red → Red
- Cast%: 5/60 cards changed · mean |Δ| 0.1 pts · range +0..+1

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Alesha, Who Laughs at Fate | 3 | 92 | 93 | +1 |
| Blood Artist | 2 | 97 | 98 | +1 |
| Liliana, Dreadhorde General | 6 | 42 | 43 | +1 |
| Virtue of Persistence // Locthwain Scorn | 7 | 28 | 29 | +1 |
| Zurgo, Thunder's Decree | 3 | 91 | 92 | +1 |

### Cedh · MQ-03 ramp-credit-v2

- Health: NeedsWork → NeedsWork
- Land target: 34.9 → 34.9 (ramp/draw<=2 1 → 1)
- Weakest color: Red → Red
- Cast%: 0/60 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · 70-03b land-ramp-sim

- Health: NeedsWork → NeedsWork
- Land target: 34.9 (unchanged by this flag)
- Weakest color: Red → Red
- Cast%: 0/60 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Karsten closed-form cross-check (Casual)

Karsten = P(≥T lands) × hardest-color CastConsistency (the Snail/Karsten metric). Multi-color cards: our sim requires ALL colors jointly, so sim ≤ Karsten-hardest-single is expected.
- Mean |sim − Karsten|: OFF 15.7 pts → ALL-ON 15.8 pts (OFF closer)

| Card | MV | Karsten | sim OFF | sim ON | ON−Karsten |
|---|---|---|---|---|---|
| Kazuul, Tyrant of the Cliffs | 5 | 42 | 69 | 69 | +27 |
| Queen Mother Ramonda | 5 | 42 | 69 | 69 | +27 |
| Sphere of Safety | 5 | 43 | 70 | 70 | +27 |
| Bre of Clan Stoutarm | 4 | 59 | 85 | 85 | +26 |
| Mangara, the Diplomat | 4 | 59 | 85 | 85 | +26 |
| The Speed Demon | 5 | 42 | 68 | 68 | +26 |
| Exemplar of Light | 4 | 57 | 82 | 82 | +25 |
| Queen Marchesa | 4 | 59 | 84 | 84 | +25 |
| Fell the Profane // Fell Mire | 4 | 55 | 79 | 79 | +24 |
| Adeline, Resplendent Cathar | 3 | 67 | 87 | 87 | +20 |
| Bastion of Remembrance | 3 | 74 | 94 | 94 | +20 |
| Black Market Connections | 3 | 74 | 94 | 94 | +20 |
| Damn | 2 | 63 | 83 | 83 | +20 |
| Elenda's Hierophant | 3 | 75 | 95 | 95 | +20 |
| Funeral Room // Awakening Hall | 3 | 74 | 94 | 94 | +20 |

## Deck: Archidekt 23753514 — graveyard fungus (100 cards, 96 distinct)

### Casual · MQ-02 source-mana-quantity

- Health: Functional → Functional
- Land target: 36.1 (unchanged by this flag)
- Weakest color: Red → Red
- Cast%: 9/52 cards changed · mean |Δ| 0.3 pts · range +0..+3

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Archon of Cruelty | 8 | 47 | 50 | +3 |
| Blasphemous Act | 9 | 36 | 39 | +3 |
| Colossal Grave-Reaver | 8 | 47 | 50 | +3 |
| Protean Hulk | 7 | 59 | 62 | +3 |
| Butcher of Malakir | 7 | 60 | 62 | +2 |
| Anger | 4 | 94 | 95 | +1 |
| Flayer of the Hatebound | 6 | 72 | 73 | +1 |
| Tendershoot Dryad | 5 | 89 | 90 | +1 |
| Ziatora, the Incinerator | 6 | 73 | 74 | +1 |

### Casual · MQ-05 color-aware-mulligan

- Health: Functional → Functional
- Land target: 36.1 (unchanged by this flag)
- Weakest color: Red → Red
- Cast%: 5/52 cards changed · mean |Δ| 0.1 pts · range +0..+1

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Anger | 4 | 94 | 95 | +1 |
| Chainer, Nightmare Adept | 4 | 94 | 95 | +1 |
| Faithless Looting | 1 | 97 | 98 | +1 |
| Goblin Bombardment | 2 | 98 | 99 | +1 |
| Protean Hulk | 7 | 59 | 60 | +1 |

### Casual · MQ-03 ramp-credit-v2

- Health: Functional → Functional
- Land target: 36.1 → 36.1 (ramp/draw<=2 18 → 18)
- Weakest color: Red → Red
- Cast%: 0/52 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Casual · 70-03b land-ramp-sim

- Health: Functional → Functional
- Land target: 36.1 (unchanged by this flag)
- Weakest color: Red → Red
- Cast%: 27/52 cards changed · mean |Δ| 1.9 pts · range -1..+10

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Archon of Cruelty | 8 | 47 | 57 | +10 |
| Blasphemous Act | 9 | 36 | 46 | +10 |
| Colossal Grave-Reaver | 8 | 47 | 57 | +10 |
| Butcher of Malakir | 7 | 60 | 69 | +9 |
| Protean Hulk | 7 | 59 | 68 | +9 |
| Flayer of the Hatebound | 6 | 72 | 79 | +7 |
| Ziatora, the Incinerator | 6 | 73 | 80 | +7 |
| Massacre Wurm | 6 | 72 | 78 | +6 |
| Korvold, Fae-Cursed King | 5 | 89 | 93 | +4 |
| Tendershoot Dryad | 5 | 89 | 93 | +4 |
| Living Death | 5 | 89 | 92 | +3 |
| Mycoloth | 5 | 89 | 92 | +3 |
| Syr Konrad, the Grim | 5 | 89 | 92 | +3 |
| Anger | 4 | 94 | 96 | +2 |
| Chainer, Nightmare Adept | 4 | 94 | 96 | +2 |

### Cedh · MQ-02 source-mana-quantity

- Health: Functional → Functional
- Land target: 32.6 (unchanged by this flag)
- Weakest color: Red → Red
- Cast%: 9/52 cards changed · mean |Δ| 0.3 pts · range +0..+3

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Archon of Cruelty | 8 | 47 | 50 | +3 |
| Blasphemous Act | 9 | 36 | 39 | +3 |
| Colossal Grave-Reaver | 8 | 47 | 50 | +3 |
| Protean Hulk | 7 | 59 | 62 | +3 |
| Butcher of Malakir | 7 | 60 | 62 | +2 |
| Anger | 4 | 94 | 95 | +1 |
| Flayer of the Hatebound | 6 | 72 | 73 | +1 |
| Tendershoot Dryad | 5 | 89 | 90 | +1 |
| Ziatora, the Incinerator | 6 | 73 | 74 | +1 |

### Cedh · MQ-05 color-aware-mulligan

- Health: Functional → Functional
- Land target: 32.6 (unchanged by this flag)
- Weakest color: Red → Red
- Cast%: 5/52 cards changed · mean |Δ| 0.1 pts · range +0..+1

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Anger | 4 | 94 | 95 | +1 |
| Chainer, Nightmare Adept | 4 | 94 | 95 | +1 |
| Faithless Looting | 1 | 97 | 98 | +1 |
| Goblin Bombardment | 2 | 98 | 99 | +1 |
| Protean Hulk | 7 | 59 | 60 | +1 |

### Cedh · MQ-03 ramp-credit-v2

- Health: Functional → Functional
- Land target: 32.6 → 32.6 (ramp/draw<=2 18 → 18)
- Weakest color: Red → Red
- Cast%: 0/52 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · 70-03b land-ramp-sim

- Health: Functional → Functional
- Land target: 32.6 (unchanged by this flag)
- Weakest color: Red → Red
- Cast%: 27/52 cards changed · mean |Δ| 1.9 pts · range -1..+10

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Archon of Cruelty | 8 | 47 | 57 | +10 |
| Blasphemous Act | 9 | 36 | 46 | +10 |
| Colossal Grave-Reaver | 8 | 47 | 57 | +10 |
| Butcher of Malakir | 7 | 60 | 69 | +9 |
| Protean Hulk | 7 | 59 | 68 | +9 |
| Flayer of the Hatebound | 6 | 72 | 79 | +7 |
| Ziatora, the Incinerator | 6 | 73 | 80 | +7 |
| Massacre Wurm | 6 | 72 | 78 | +6 |
| Korvold, Fae-Cursed King | 5 | 89 | 93 | +4 |
| Tendershoot Dryad | 5 | 89 | 93 | +4 |
| Living Death | 5 | 89 | 92 | +3 |
| Mycoloth | 5 | 89 | 92 | +3 |
| Syr Konrad, the Grim | 5 | 89 | 92 | +3 |
| Anger | 4 | 94 | 96 | +2 |
| Chainer, Nightmare Adept | 4 | 94 | 96 | +2 |

### Karsten closed-form cross-check (Casual)

Karsten = P(≥T lands) × hardest-color CastConsistency (the Snail/Karsten metric). Multi-color cards: our sim requires ALL colors jointly, so sim ≤ Karsten-hardest-single is expected.
- Mean |sim − Karsten|: OFF 23.0 pts → ALL-ON 23.4 pts (OFF closer)

| Card | MV | Karsten | sim OFF | sim ON | ON−Karsten |
|---|---|---|---|---|---|
| Korvold, Fae-Cursed King | 5 | 42 | 89 | 90 | +48 |
| Tendershoot Dryad | 5 | 42 | 89 | 90 | +48 |
| Living Death | 5 | 42 | 89 | 89 | +47 |
| Mycoloth | 5 | 42 | 89 | 89 | +47 |
| Syr Konrad, the Grim | 5 | 42 | 89 | 89 | +47 |
| Ziatora, the Incinerator | 6 | 28 | 73 | 74 | +46 |
| Flayer of the Hatebound | 6 | 28 | 72 | 73 | +45 |
| Butcher of Malakir | 7 | 18 | 60 | 62 | +44 |
| Massacre Wurm | 6 | 28 | 72 | 72 | +44 |
| Protean Hulk | 7 | 18 | 59 | 62 | +44 |
| Archon of Cruelty | 8 | 10 | 47 | 50 | +40 |
| Colossal Grave-Reaver | 8 | 10 | 47 | 50 | +40 |
| Anger | 4 | 58 | 94 | 95 | +37 |
| Chainer, Nightmare Adept | 4 | 58 | 94 | 95 | +37 |
| Deadly Rollick | 4 | 58 | 95 | 95 | +37 |

## Deck: Archidekt 23638601 — The boys are back in Town(os) (99 cards, 99 distinct)

### Casual · MQ-02 source-mana-quantity

- Health: Healthy → Healthy
- Land target: 35.1 (unchanged by this flag)
- Weakest color: none → none
- Cast%: 7/42 cards changed · mean |Δ| 0.4 pts · range +0..+4

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Angel of the Ruins | 7 | 84 | 88 | +4 |
| Darksteel Splicer | 7 | 84 | 88 | +4 |
| Thought Monitor | 7 | 84 | 88 | +4 |
| Sharuum the Hegemon | 6 | 90 | 92 | +2 |
| Wurmcoil Engine | 6 | 90 | 92 | +2 |
| Saheeli's Artistry | 6 | 90 | 91 | +1 |
| Sun Titan | 6 | 90 | 91 | +1 |

### Casual · MQ-05 color-aware-mulligan

- Health: Healthy → Healthy
- Land target: 35.1 (unchanged by this flag)
- Weakest color: none → none
- Cast%: 1/42 cards changed · mean |Δ| 0.0 pts · range +0..+1

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Thopter Foundry | 2 | 99 | 100 | +1 |

### Casual · MQ-03 ramp-credit-v2

- Health: Healthy → Healthy
- Land target: 35.1 → 35.1 (ramp/draw<=2 17 → 17)
- Weakest color: none → none
- Cast%: 0/42 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Casual · 70-03b land-ramp-sim

- Health: Healthy → Healthy
- Land target: 35.1 (unchanged by this flag)
- Weakest color: none → none
- Cast%: 15/42 cards changed · mean |Δ| 0.9 pts · range +0..+5

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Angel of the Ruins | 7 | 84 | 89 | +5 |
| Darksteel Splicer | 7 | 84 | 89 | +5 |
| Thought Monitor | 7 | 84 | 89 | +5 |
| Wurmcoil Engine | 6 | 90 | 94 | +4 |
| Saheeli's Artistry | 6 | 90 | 93 | +3 |
| Sharuum the Hegemon | 6 | 90 | 93 | +3 |
| Sun Titan | 6 | 90 | 93 | +3 |
| Eloise, Nephalia Sleuth | 5 | 97 | 98 | +1 |
| Fatestitcher | 4 | 98 | 99 | +1 |
| Karmic Guide | 5 | 96 | 97 | +1 |
| Mirrorworks | 5 | 97 | 98 | +1 |
| Padeem, Consul of Innovation | 4 | 98 | 99 | +1 |
| Phyrexian Delver | 5 | 96 | 97 | +1 |
| Solemn Simulacrum | 4 | 98 | 99 | +1 |
| Tezzeret the Seeker | 5 | 96 | 97 | +1 |

### Cedh · MQ-02 source-mana-quantity

- Health: Functional → Healthy
- Land target: 31.6 (unchanged by this flag)
- Weakest color: White → none
- Cast%: 7/42 cards changed · mean |Δ| 0.4 pts · range +0..+4

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Angel of the Ruins | 7 | 84 | 88 | +4 |
| Darksteel Splicer | 7 | 84 | 88 | +4 |
| Thought Monitor | 7 | 84 | 88 | +4 |
| Sharuum the Hegemon | 6 | 90 | 92 | +2 |
| Wurmcoil Engine | 6 | 90 | 92 | +2 |
| Saheeli's Artistry | 6 | 90 | 91 | +1 |
| Sun Titan | 6 | 90 | 91 | +1 |

### Cedh · MQ-05 color-aware-mulligan

- Health: Functional → Functional
- Land target: 31.6 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 1/42 cards changed · mean |Δ| 0.0 pts · range +0..+1

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Thopter Foundry | 2 | 99 | 100 | +1 |

### Cedh · MQ-03 ramp-credit-v2

- Health: Functional → Functional
- Land target: 31.6 → 31.6 (ramp/draw<=2 17 → 17)
- Weakest color: White → White
- Cast%: 0/42 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · 70-03b land-ramp-sim

- Health: Functional → Healthy
- Land target: 31.6 (unchanged by this flag)
- Weakest color: White → none
- Cast%: 15/42 cards changed · mean |Δ| 0.9 pts · range +0..+5

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Angel of the Ruins | 7 | 84 | 89 | +5 |
| Darksteel Splicer | 7 | 84 | 89 | +5 |
| Thought Monitor | 7 | 84 | 89 | +5 |
| Wurmcoil Engine | 6 | 90 | 94 | +4 |
| Saheeli's Artistry | 6 | 90 | 93 | +3 |
| Sharuum the Hegemon | 6 | 90 | 93 | +3 |
| Sun Titan | 6 | 90 | 93 | +3 |
| Eloise, Nephalia Sleuth | 5 | 97 | 98 | +1 |
| Fatestitcher | 4 | 98 | 99 | +1 |
| Karmic Guide | 5 | 96 | 97 | +1 |
| Mirrorworks | 5 | 97 | 98 | +1 |
| Padeem, Consul of Innovation | 4 | 98 | 99 | +1 |
| Phyrexian Delver | 5 | 96 | 97 | +1 |
| Solemn Simulacrum | 4 | 98 | 99 | +1 |
| Tezzeret the Seeker | 5 | 96 | 97 | +1 |

### Karsten closed-form cross-check (Casual)

Karsten = P(≥T lands) × hardest-color CastConsistency (the Snail/Karsten metric). Multi-color cards: our sim requires ALL colors jointly, so sim ≤ Karsten-hardest-single is expected.
- Mean |sim − Karsten|: OFF 27.3 pts → ALL-ON 27.7 pts (OFF closer)

| Card | MV | Karsten | sim OFF | sim ON | ON−Karsten |
|---|---|---|---|---|---|
| Angel of the Ruins | 7 | 21 | 84 | 88 | +67 |
| Darksteel Splicer | 7 | 21 | 84 | 88 | +67 |
| Thought Monitor | 7 | 21 | 84 | 88 | +67 |
| Sharuum the Hegemon | 6 | 32 | 90 | 92 | +60 |
| Wurmcoil Engine | 6 | 32 | 90 | 92 | +60 |
| Saheeli's Artistry | 6 | 32 | 90 | 91 | +59 |
| Sun Titan | 6 | 32 | 90 | 91 | +59 |
| Eloise, Nephalia Sleuth | 5 | 46 | 97 | 97 | +51 |
| Mirrorworks | 5 | 46 | 97 | 97 | +51 |
| Karmic Guide | 5 | 46 | 96 | 96 | +50 |
| Phyrexian Delver | 5 | 46 | 96 | 96 | +50 |
| Tezzeret the Seeker | 5 | 46 | 96 | 96 | +50 |
| Fatestitcher | 4 | 62 | 98 | 98 | +36 |
| Padeem, Consul of Innovation | 4 | 62 | 98 | 98 | +36 |
| Shorikai, Genesis Engine | 4 | 62 | 98 | 98 | +36 |

## Deck: Archidekt 8066726 — The Necrobloom (99 cards, 94 distinct)

### Casual · MQ-02 source-mana-quantity

- Health: NeedsWork → NeedsWork
- Land target: 34.1 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 28/61 cards changed · mean |Δ| 1.0 pts · range -1..+11

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Avenger of Zendikar | 7 | 39 | 50 | +11 |
| Ancient Greenwarden | 6 | 52 | 59 | +7 |
| Ojer Taq, Deepest Foundation // Temple of Civilization | 6 | 37 | 44 | +7 |
| Doubling Season | 5 | 77 | 81 | +4 |
| The Gitrog Monster | 5 | 76 | 79 | +3 |
| Titania, Protector of Argoth | 5 | 72 | 75 | +3 |
| Damn | 2 | 63 | 65 | +2 |
| Mirkwood Bats | 4 | 85 | 87 | +2 |
| Azusa, Lost but Seeking | 3 | 92 | 93 | +1 |
| Bala Ged Recovery // Bala Ged Sanctuary | 3 | 92 | 93 | +1 |
| Conduit of Worlds | 4 | 77 | 78 | +1 |
| Courser of Kruphix | 3 | 76 | 77 | +1 |
| Cultivate | 3 | 92 | 93 | +1 |
| Dread Return | 4 | 72 | 73 | +1 |
| Dryad of the Ilysian Grove | 3 | 92 | 93 | +1 |

### Casual · MQ-05 color-aware-mulligan

- Health: NeedsWork → NeedsWork
- Land target: 34.1 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 47/61 cards changed · mean |Δ| 2.5 pts · range -1..+9

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Swords to Plowshares | 1 | 72 | 81 | +9 |
| Ephemerate | 1 | 73 | 81 | +8 |
| Path to Exile | 1 | 73 | 81 | +8 |
| Damn | 2 | 63 | 70 | +7 |
| Distinguished Conjurer | 2 | 75 | 82 | +7 |
| Overgrown Estate | 3 | 61 | 68 | +7 |
| Assassin's Trophy | 2 | 84 | 90 | +6 |
| Quest for the Necropolis | 1 | 90 | 96 | +6 |
| Reanimate | 1 | 90 | 96 | +6 |
| Wight of the Reliquary | 2 | 84 | 90 | +6 |
| Generous Gift | 3 | 78 | 83 | +5 |
| Thalia and The Gitrog Monster | 4 | 67 | 72 | +5 |
| The Necrobloom | 4 | 68 | 73 | +5 |
| Dread Return | 4 | 72 | 76 | +4 |
| Felidar Retreat | 4 | 76 | 80 | +4 |

### Casual · MQ-03 ramp-credit-v2

- Health: NeedsWork → NeedsWork
- Land target: 34.1 → 34.7 (ramp/draw<=2 11 → 9)
- Weakest color: White → White
- Cast%: 0/61 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Casual · 70-03b land-ramp-sim

- Health: NeedsWork → NeedsWork
- Land target: 34.1 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 31/61 cards changed · mean |Δ| 2.4 pts · range -1..+23

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Avenger of Zendikar | 7 | 39 | 62 | +23 |
| Ancient Greenwarden | 6 | 52 | 70 | +18 |
| Doubling Season | 5 | 77 | 90 | +13 |
| The Gitrog Monster | 5 | 76 | 87 | +11 |
| Ojer Taq, Deepest Foundation // Temple of Civilization | 6 | 37 | 46 | +9 |
| Titania, Protector of Argoth | 5 | 72 | 81 | +9 |
| Mirkwood Bats | 4 | 85 | 91 | +6 |
| Oracle of Mul Daya | 4 | 87 | 93 | +6 |
| Skyshroud Claim | 4 | 87 | 93 | +6 |
| Splendid Reclamation | 4 | 87 | 93 | +6 |
| World Shaper | 4 | 88 | 93 | +5 |
| Felidar Retreat | 4 | 76 | 79 | +3 |
| Azusa, Lost but Seeking | 3 | 92 | 94 | +2 |
| Bala Ged Recovery // Bala Ged Sanctuary | 3 | 92 | 94 | +2 |
| Conduit of Worlds | 4 | 77 | 79 | +2 |

### Cedh · MQ-02 source-mana-quantity

- Health: NeedsWork → NeedsWork
- Land target: 30.6 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 28/61 cards changed · mean |Δ| 1.0 pts · range -1..+11

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Avenger of Zendikar | 7 | 39 | 50 | +11 |
| Ancient Greenwarden | 6 | 52 | 59 | +7 |
| Ojer Taq, Deepest Foundation // Temple of Civilization | 6 | 37 | 44 | +7 |
| Doubling Season | 5 | 77 | 81 | +4 |
| The Gitrog Monster | 5 | 76 | 79 | +3 |
| Titania, Protector of Argoth | 5 | 72 | 75 | +3 |
| Damn | 2 | 63 | 65 | +2 |
| Mirkwood Bats | 4 | 85 | 87 | +2 |
| Azusa, Lost but Seeking | 3 | 92 | 93 | +1 |
| Bala Ged Recovery // Bala Ged Sanctuary | 3 | 92 | 93 | +1 |
| Conduit of Worlds | 4 | 77 | 78 | +1 |
| Courser of Kruphix | 3 | 76 | 77 | +1 |
| Cultivate | 3 | 92 | 93 | +1 |
| Dread Return | 4 | 72 | 73 | +1 |
| Dryad of the Ilysian Grove | 3 | 92 | 93 | +1 |

### Cedh · MQ-05 color-aware-mulligan

- Health: NeedsWork → NeedsWork
- Land target: 30.6 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 47/61 cards changed · mean |Δ| 2.5 pts · range -1..+9

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Swords to Plowshares | 1 | 72 | 81 | +9 |
| Ephemerate | 1 | 73 | 81 | +8 |
| Path to Exile | 1 | 73 | 81 | +8 |
| Damn | 2 | 63 | 70 | +7 |
| Distinguished Conjurer | 2 | 75 | 82 | +7 |
| Overgrown Estate | 3 | 61 | 68 | +7 |
| Assassin's Trophy | 2 | 84 | 90 | +6 |
| Quest for the Necropolis | 1 | 90 | 96 | +6 |
| Reanimate | 1 | 90 | 96 | +6 |
| Wight of the Reliquary | 2 | 84 | 90 | +6 |
| Generous Gift | 3 | 78 | 83 | +5 |
| Thalia and The Gitrog Monster | 4 | 67 | 72 | +5 |
| The Necrobloom | 4 | 68 | 73 | +5 |
| Dread Return | 4 | 72 | 76 | +4 |
| Felidar Retreat | 4 | 76 | 80 | +4 |

### Cedh · MQ-03 ramp-credit-v2

- Health: NeedsWork → NeedsWork
- Land target: 30.6 → 31.2 (ramp/draw<=2 11 → 9)
- Weakest color: White → White
- Cast%: 0/61 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · 70-03b land-ramp-sim

- Health: NeedsWork → NeedsWork
- Land target: 30.6 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 31/61 cards changed · mean |Δ| 2.4 pts · range -1..+23

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Avenger of Zendikar | 7 | 39 | 62 | +23 |
| Ancient Greenwarden | 6 | 52 | 70 | +18 |
| Doubling Season | 5 | 77 | 90 | +13 |
| The Gitrog Monster | 5 | 76 | 87 | +11 |
| Ojer Taq, Deepest Foundation // Temple of Civilization | 6 | 37 | 46 | +9 |
| Titania, Protector of Argoth | 5 | 72 | 81 | +9 |
| Mirkwood Bats | 4 | 85 | 91 | +6 |
| Oracle of Mul Daya | 4 | 87 | 93 | +6 |
| Skyshroud Claim | 4 | 87 | 93 | +6 |
| Splendid Reclamation | 4 | 87 | 93 | +6 |
| World Shaper | 4 | 88 | 93 | +5 |
| Felidar Retreat | 4 | 76 | 79 | +3 |
| Azusa, Lost but Seeking | 3 | 92 | 94 | +2 |
| Bala Ged Recovery // Bala Ged Sanctuary | 3 | 92 | 94 | +2 |
| Conduit of Worlds | 4 | 77 | 79 | +2 |

### Karsten closed-form cross-check (Casual)

Karsten = P(≥T lands) × hardest-color CastConsistency (the Snail/Karsten metric). Multi-color cards: our sim requires ALL colors jointly, so sim ≤ Karsten-hardest-single is expected.
- Mean |sim − Karsten|: OFF 23.8 pts → ALL-ON 27.4 pts (OFF closer)

| Card | MV | Karsten | sim OFF | sim ON | ON−Karsten |
|---|---|---|---|---|---|
| Doubling Season | 5 | 29 | 77 | 81 | +52 |
| The Gitrog Monster | 5 | 29 | 76 | 80 | +51 |
| Titania, Protector of Argoth | 5 | 28 | 72 | 76 | +48 |
| Mirkwood Bats | 4 | 44 | 85 | 89 | +45 |
| Skyshroud Claim | 4 | 45 | 87 | 89 | +44 |
| World Shaper | 4 | 45 | 88 | 89 | +44 |
| Ancient Greenwarden | 6 | 17 | 52 | 60 | +43 |
| Oracle of Mul Daya | 4 | 45 | 87 | 88 | +43 |
| Splendid Reclamation | 4 | 45 | 87 | 88 | +43 |
| Avenger of Zendikar | 7 | 9 | 39 | 51 | +42 |
| Felidar Retreat | 4 | 40 | 76 | 81 | +41 |
| Conduit of Worlds | 4 | 41 | 77 | 80 | +39 |
| Dread Return | 4 | 39 | 72 | 78 | +39 |
| Fell the Profane // Fell Mire | 4 | 39 | 72 | 78 | +39 |
| Ojer Taq, Deepest Foundation // Temple of Civilization | 6 | 13 | 37 | 47 | +34 |

## Deck: Archidekt 7084567 — Oooooh you are in the army now (100 cards, 91 distinct)

### Casual · MQ-02 source-mana-quantity

- Health: Functional → Functional
- Land target: 39.9 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 24/61 cards changed · mean |Δ| 1.0 pts · range +0..+7

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Akroma, Vision of Ixidor | 7 | 37 | 44 | +7 |
| Chronicle of Victory | 6 | 38 | 44 | +6 |
| Farewell | 6 | 38 | 44 | +6 |
| Sun Titan | 6 | 38 | 44 | +6 |
| The Immortal Sun | 6 | 38 | 44 | +6 |
| Elspeth Conquers Death | 5 | 64 | 68 | +4 |
| Elspeth Resplendent | 5 | 65 | 68 | +3 |
| Akroma's Will | 4 | 81 | 83 | +2 |
| Day of Judgment | 4 | 81 | 83 | +2 |
| Flare of Fortitude | 4 | 81 | 83 | +2 |
| Keeper of the Accord | 4 | 81 | 83 | +2 |
| Myrel, Shield of Argive | 4 | 81 | 83 | +2 |
| The One Ring | 4 | 81 | 83 | +2 |
| Trouble in Pairs | 4 | 81 | 83 | +2 |
| Aerial Responder | 3 | 92 | 93 | +1 |

### Casual · MQ-05 color-aware-mulligan

- Health: Functional → Functional
- Land target: 39.9 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 0/61 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Casual · MQ-03 ramp-credit-v2

- Health: Functional → Functional
- Land target: 39.9 → 40.2 (ramp/draw<=2 3 → 2)
- Weakest color: White → White
- Cast%: 0/61 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Casual · 70-03b land-ramp-sim

- Health: Functional → Functional
- Land target: 39.9 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 11/61 cards changed · mean |Δ| 0.4 pts · range -1..+4

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Akroma, Vision of Ixidor | 7 | 37 | 41 | +4 |
| Sun Titan | 6 | 38 | 42 | +4 |
| Chronicle of Victory | 6 | 38 | 41 | +3 |
| Farewell | 6 | 38 | 41 | +3 |
| The Immortal Sun | 6 | 38 | 41 | +3 |
| Elspeth Conquers Death | 5 | 64 | 66 | +2 |
| Elspeth Resplendent | 5 | 65 | 67 | +2 |
| Armageddon | 4 | 82 | 81 | -1 |
| Consul's Lieutenant | 2 | 97 | 96 | -1 |
| Field Marshal | 3 | 93 | 92 | -1 |
| Savior of Ollenbock | 3 | 93 | 92 | -1 |

### Cedh · MQ-02 source-mana-quantity

- Health: Functional → Functional
- Land target: 36.4 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 24/61 cards changed · mean |Δ| 1.0 pts · range +0..+7

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Akroma, Vision of Ixidor | 7 | 37 | 44 | +7 |
| Chronicle of Victory | 6 | 38 | 44 | +6 |
| Farewell | 6 | 38 | 44 | +6 |
| Sun Titan | 6 | 38 | 44 | +6 |
| The Immortal Sun | 6 | 38 | 44 | +6 |
| Elspeth Conquers Death | 5 | 64 | 68 | +4 |
| Elspeth Resplendent | 5 | 65 | 68 | +3 |
| Akroma's Will | 4 | 81 | 83 | +2 |
| Day of Judgment | 4 | 81 | 83 | +2 |
| Flare of Fortitude | 4 | 81 | 83 | +2 |
| Keeper of the Accord | 4 | 81 | 83 | +2 |
| Myrel, Shield of Argive | 4 | 81 | 83 | +2 |
| The One Ring | 4 | 81 | 83 | +2 |
| Trouble in Pairs | 4 | 81 | 83 | +2 |
| Aerial Responder | 3 | 92 | 93 | +1 |

### Cedh · MQ-05 color-aware-mulligan

- Health: Functional → Functional
- Land target: 36.4 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 0/61 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · MQ-03 ramp-credit-v2

- Health: Functional → Functional
- Land target: 36.4 → 36.7 (ramp/draw<=2 3 → 2)
- Weakest color: White → White
- Cast%: 0/61 cards changed · mean |Δ| 0.0 pts · range +0..+0

### Cedh · 70-03b land-ramp-sim

- Health: Functional → Functional
- Land target: 36.4 (unchanged by this flag)
- Weakest color: White → White
- Cast%: 11/61 cards changed · mean |Δ| 0.4 pts · range -1..+4

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Akroma, Vision of Ixidor | 7 | 37 | 41 | +4 |
| Sun Titan | 6 | 38 | 42 | +4 |
| Chronicle of Victory | 6 | 38 | 41 | +3 |
| Farewell | 6 | 38 | 41 | +3 |
| The Immortal Sun | 6 | 38 | 41 | +3 |
| Elspeth Conquers Death | 5 | 64 | 66 | +2 |
| Elspeth Resplendent | 5 | 65 | 67 | +2 |
| Armageddon | 4 | 82 | 81 | -1 |
| Consul's Lieutenant | 2 | 97 | 96 | -1 |
| Field Marshal | 3 | 93 | 92 | -1 |
| Savior of Ollenbock | 3 | 93 | 92 | -1 |

### Karsten closed-form cross-check (Casual)

Karsten = P(≥T lands) × hardest-color CastConsistency (the Snail/Karsten metric). Multi-color cards: our sim requires ALL colors jointly, so sim ≤ Karsten-hardest-single is expected.
- Mean |sim − Karsten|: OFF 16.9 pts → ALL-ON 17.9 pts (OFF closer)

| Card | MV | Karsten | sim OFF | sim ON | ON−Karsten |
|---|---|---|---|---|---|
| Elspeth Conquers Death | 5 | 39 | 64 | 68 | +29 |
| Elspeth Resplendent | 5 | 39 | 65 | 68 | +29 |
| Day of Judgment | 4 | 55 | 81 | 83 | +28 |
| Flare of Fortitude | 4 | 55 | 81 | 83 | +28 |
| Trouble in Pairs | 4 | 55 | 81 | 83 | +28 |
| Akroma's Will | 4 | 56 | 81 | 83 | +27 |
| Archangel Elspeth | 4 | 55 | 81 | 82 | +27 |
| Armageddon | 4 | 56 | 82 | 83 | +27 |
| Keeper of the Accord | 4 | 56 | 81 | 83 | +27 |
| Marshal's Anthem | 4 | 55 | 81 | 82 | +27 |
| Myrel, Shield of Argive | 4 | 56 | 81 | 83 | +27 |
| Odric, Lunarch Marshal | 4 | 56 | 82 | 83 | +27 |
| Odric, Master Tactician | 4 | 55 | 81 | 82 | +27 |
| Teshar, Ancestor's Apostle | 4 | 56 | 82 | 83 | +27 |
| The One Ring | 4 | 56 | 81 | 83 | +27 |


---

## 70-03b land-ramp-sim baseline (added 2026-06-24)

Each flag is isolated vs the all-off baseline. `land-ramp-sim` is the **most impactful** of the four —
it models the fetched-land mana the sim previously ignored, so cast% rises on ramp decks and, unlike
MQ-02/03/05, the **Health verdict moves** (always *upward* — previously under-rated decks improve):

| Deck | Health Δ | Cast% |
|---|---|---|
| Brago (WU, Solemn etc.) | NeedsWork → **Functional** | 10/58, mean 0.3, −1..+3 |
| Kenrith 5c | Healthy → Healthy | 0/16 |
| Meren Golgari ramp | Functional → **Healthy** | 15/39, mean 1.3, 0..+14 |
| Marchesa | Workable → Workable | 0/60 |
| graveyard fungus | Functional → Functional | 27/52, mean 1.9, −1..+10 |
| Town(os) | Healthy → Healthy | 15/42, mean 0.9, 0..+5 |
| Necrobloom | NeedsWork → NeedsWork | 31/61, mean 2.4, −1..+23 |
| army-now | Functional → Functional | 11/61, mean 0.4, −1..+4 |

**Read:** the two Health flips are *corrections* — those decks were under-rated because the sim
credited the regression's land-ramp on the target side but modeled no extra mana, so expensive
payoffs read too low. Large single-card swings (+14 / +23) are on the very payoffs the ramp exists to
cast. No deck got *worse*. (Baseline is vs all-off; prod already runs MQ-02/03/05 ON, but the
land-ramp source is orthogonal to those, so the marginal effect is the same.)

**Recommendation: flip `manabase.land-ramp-sim` ON** — it is the accuracy fix the sim was missing, and
it only ever improves (never inflates beyond what the ramp casts). It is the only flag that shifts the
verdict, so it is the most consequential call.
