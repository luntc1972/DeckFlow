# Calibration measurement — per-color detail for Avatar / Meren / graveyard-fungus

Mode: Casual. Run with flag OFF and flag ON (useHealthBandCastability=true).

---
## Avatar — Sokka/Aang (Jeskai)

**Band flag OFF:** Solid  |  **Band flag ON:** Workable

### All-color table (flag OFF run, same ColorFindings both ways)

| Color | ActualSrc | RequiredSrc | Deficit | ColorLimitedUnderSupp | SpellCount | Tolerance | AvgCast% | WorstSpell% | WorstSpell |
|---|---|---|---|---|---|---|---|---|---|
| White | 32.3 | 24 | -8.30 | 1 | 17 | 3 | 92.8 | 73.0 | Suki, Courageous Rescuer |
| Blue | 33.1 | 24 | -9.10 | 0 | 27 | 5 | 93.1 | 78.0 | Echocasting Symposium |
| Red | 33.3 | 18 | -15.30 | 0 | 15 | 3 | 95.1 | 92.0 | Boros Charm |

### Castability rows for White spells (weakest color)

Cards demanding this color, ordered by CastPercent ascending:

| Card | MV | Turn | Cast% | LimitingFactor |
|---|---|---|---|---|
| Suki, Courageous Rescuer | 3 | 2 | 73 | color:White |
| Avatar's Wrath | 4 | 4 | 90 | both |
| Aang, Swift Savior // Aang and La, Ocean's Fury | 3 | 2 | 91 | color:White |
| Boros Charm | 2 | 2 | 92 | color:Red |
| Lyse Hext | 3 | 2 | 92 | color:White |
| Jeskai Ascendancy | 3 | 3 | 93 | color:Blue |
| Sokka, Tenacious Tactician | 4 | 3 | 93 | color:Blue |
| Enlightened Tutor | 1 | 1 | 94 | color:White |
| Enter the Avatar State | 1 | 1 | 94 | color:White |
| Silence | 1 | 1 | 94 | color:White |
| Sokka's Charge | 4 | 4 | 94 | mana |
| Airbender's Reversal | 2 | 2 | 96 | color:White |
| Blind Obedience | 2 | 2 | 96 | color:White |
| Monastery Mentor | 3 | 2 | 96 | color:White |
| Sejiri Shelter // Sejiri Glacier | 2 | 2 | 96 | color:White |
| Airbending Lesson | 3 | 3 | 97 | mana |
| Tale of Momo | 3 | 3 | 97 | mana |

**White demanding spells:** 17 total  |  below 80%: 1  |  below 90%: 1

**White gate-test values:**
- WorstSpellCastPercent: 73.0
- AverageCastPercent:    92.8
- ColorLimitedUnderSupportedCount: 1
- Tolerance:             3
- Deficit (Req-Actual):  -8.30
- SpellCount:            17
- Gate B  (AvgCast < 85): does not fire
- Gate B' (AvgCast < 90): does not fire
- Gate C  (ColorLimitedUnderSupp >= 1 AND Worst < 80): FIRES
- Gate C' (ColorLimitedUnderSupp > tolerance AND Worst < 80): does not fire

---
## Meren Golgari ramp/ritual

**Band flag OFF:** Solid  |  **Band flag ON:** Workable

### All-color table (flag OFF run, same ColorFindings both ways)

| Color | ActualSrc | RequiredSrc | Deficit | ColorLimitedUnderSupp | SpellCount | Tolerance | AvgCast% | WorstSpell% | WorstSpell |
|---|---|---|---|---|---|---|---|---|---|
| Green | 25.0 | 23 | -2.00 | 0 | 17 | 3 | 93.3 | 71.0 | Old Gnawbone |
| Black | 25.5 | 23 | -2.50 | 0 | 25 | 4 | 93.1 | 76.0 | Massacre Wurm |

### Castability rows for Green spells (weakest color)

Cards demanding this color, ordered by CastPercent ascending:

| Card | MV | Turn | Cast% | LimitingFactor |
|---|---|---|---|---|
| Old Gnawbone | 7 | 7 | 71 | mana |
| Casualties of War | 6 | 6 | 80 | mana |
| Eternal Witness | 3 | 3 | 86 | color:Green |
| Meren of Clan Nel Toth | 4 | 4 | 94 | mana |
| Sakura-Tribe Scout | 1 | 1 | 94 | color:Green |
| Abrupt Decay | 2 | 2 | 95 | color:Black |
| Putrefy | 3 | 3 | 96 | both |
| Beast Within | 3 | 3 | 97 | both |
| Cultivate | 3 | 3 | 97 | both |
| Farseek | 2 | 2 | 97 | color:Green |
| Kodama's Reach | 3 | 3 | 97 | both |
| Nature's Lore | 2 | 2 | 97 | color:Green |
| Rampant Growth | 2 | 2 | 97 | color:Green |
| Sakura-Tribe Elder | 2 | 2 | 97 | color:Green |
| Sylvan Library | 2 | 2 | 97 | color:Green |
| Three Visits | 2 | 2 | 97 | color:Green |
| Wood Elves | 3 | 3 | 97 | both |

**Green demanding spells:** 17 total  |  below 80%: 1  |  below 90%: 3

**Green gate-test values:**
- WorstSpellCastPercent: 71.0
- AverageCastPercent:    93.3
- ColorLimitedUnderSupportedCount: 0
- Tolerance:             3
- Deficit (Req-Actual):  -2.00
- SpellCount:            17
- Gate B  (AvgCast < 85): does not fire
- Gate B' (AvgCast < 90): does not fire
- Gate C  (ColorLimitedUnderSupp >= 1 AND Worst < 80): does not fire
- Gate C' (ColorLimitedUnderSupp > tolerance AND Worst < 80): does not fire

---
## Archidekt 23753514 — graveyard fungus

**Band flag OFF:** Solid  |  **Band flag ON:** Workable

### All-color table (flag OFF run, same ColorFindings both ways)

| Color | ActualSrc | RequiredSrc | Deficit | ColorLimitedUnderSupp | SpellCount | Tolerance | AvgCast% | WorstSpell% | WorstSpell |
|---|---|---|---|---|---|---|---|---|---|
| Green | 30.0 | 26 | -4.00 | 0 | 23 | 4 | 88.4 | 47.0 | Colossal Grave-Reaver |
| Red | 28.5 | 17 | -11.50 | 0 | 12 | 2 | 84.5 | 36.0 | Blasphemous Act |
| Black | 32.0 | 17 | -15.00 | 0 | 31 | 5 | 88.5 | 47.0 | Archon of Cruelty |

### Castability rows for Green spells (weakest color)

Cards demanding this color, ordered by CastPercent ascending:

| Card | MV | Turn | Cast% | LimitingFactor |
|---|---|---|---|---|
| Colossal Grave-Reaver | 8 | 8 | 47 | mana |
| Protean Hulk | 7 | 7 | 59 | mana |
| Ziatora, the Incinerator | 6 | 6 | 73 | mana |
| Mycoloth | 5 | 5 | 82 | mana |
| Korvold, Fae-Cursed King | 5 | 5 | 83 | mana |
| Tendershoot Dryad | 5 | 5 | 83 | mana |
| Eldritch Evolution | 3 | 3 | 86 | color:Green |
| Eternal Witness | 3 | 3 | 87 | color:Green |
| Nemata, Primeval Warden | 4 | 4 | 91 | mana |
| Rise of the Witch-king | 4 | 4 | 91 | mana |
| Artifact Mutation | 2 | 2 | 93 | color:Red |
| Slimefoot and Squee | 3 | 3 | 93 | both |
| Sprouting Thrinax | 3 | 3 | 93 | both |
| Cauldron of Essence | 3 | 3 | 96 | mana |
| Grisly Salvage | 2 | 2 | 96 | color:Black |
| Insidious Roots | 2 | 2 | 96 | color:Black |
| Wight of the Reliquary | 2 | 2 | 96 | color:Black |
| Farseek | 2 | 2 | 98 | color:Green |
| Hermit Druid | 2 | 2 | 98 | color:Green |
| Heroic Intervention | 2 | 2 | 98 | color:Green |
| Nature's Lore | 2 | 2 | 98 | color:Green |
| Rampant Growth | 2 | 2 | 98 | color:Green |
| Three Visits | 2 | 2 | 98 | color:Green |

**Green demanding spells:** 23 total  |  below 80%: 3  |  below 90%: 8

**Green gate-test values:**
- WorstSpellCastPercent: 47.0
- AverageCastPercent:    88.4
- ColorLimitedUnderSupportedCount: 0
- Tolerance:             4
- Deficit (Req-Actual):  -4.00
- SpellCount:            23
- Gate B  (AvgCast < 85): does not fire
- Gate B' (AvgCast < 90): FIRES
- Gate C  (ColorLimitedUnderSupp >= 1 AND Worst < 80): does not fire
- Gate C' (ColorLimitedUnderSupp > tolerance AND Worst < 80): does not fire

