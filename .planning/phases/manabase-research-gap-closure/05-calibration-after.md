# cEDH land-target calibration — old flat-28 vs new hybrid (flag ON)

Sample: **3281** cEDH decks | actual lands mean 26.5
Old target: mean 28.1 (min 28.0 max 32.9)
New target: mean 25.4 (min 22.0 max 37.9)
New target with RitualCredit: mean 24.7 (min 22.0 max 37.9)

**Under-target (actual < target):** OLD 2511/3281 = **76.5%** → NEW 714/3281 = **21.8%**
**Under-target with RitualCredit:** 363/3281 = **11.1%**
Decks the new target un-flags (were under, now OK): **1801** | newly flagged under: 4
RitualCredit delta vs NEW: un-flags **351** | newly flagged under: 0
Baseline-backed (N≥10): 2877 | recalibrated no-baseline: 404
Safety-floor(22) hits: 53 | ceiling(45) hits: 0

## Under-flag by segment
| Segment | N | actual mean | old target | new target | RitualCredit | under OLD% | under NEW% | under RitualCredit% |
|---------|---|------------|-----------|-----------|-----------|-----------|-----------|-----------|
| Baseline N>=10 | 2877 | 26.4 | 28.1 | 25.4 | 24.7 | 76.9% | 21.0% | 9.6% |
| No baseline | 404 | 26.7 | 28.2 | 25.3 | 24.6 | 74.3% | 27.5% | 21.8% |

## By commander (N≥10) — over-correction check (grindy Sisay/Tayam should stay ~healthy, not over-flagged-OK)
| Commander | N | actual mean | old tgt | new tgt | RitualCredit | under OLD% | under NEW% | under RitualCredit% |
|-----------|---|------------|--------|--------|--------|-----------|-----------|-----------|
| Kraum, Ludevic's Opus / Tymna the Weaver | 442 | 25.9 | 28.0 | 25.0 | 23.5 | 99 | 7 | 1 |
| Kinnan, Bonder Prodigy | 327 | 25.8 | 28.0 | 24.8 | 24.8 | 98 | 12 | 12 |
| Rograkh, Son of Rohgahh / Thrasios, Triton H | 241 | 27.3 | 28.0 | 26.0 | 25.9 | 42 | 12 | 10 |
| Rograkh, Son of Rohgahh / Silas Renn, Seeker | 196 | 23.1 | 28.0 | 22.7 | 22.0 | 100 | 24 | 1 |
| Sisay, Weatherlight Captain | 177 | 27.7 | 28.0 | 25.9 | 25.7 | 28 | 5 | 1 |
| Thrasios, Triton Hero / Tymna the Weaver | 112 | 26.9 | 28.0 | 25.4 | 25.1 | 79 | 5 | 0 |
| Ral, Monsoon Mage // Ral, Leyline Prodigy | 105 | 21.6 | 28.0 | 22.8 | 22.0 | 100 | 77 | 31 |
| Ishai, Ojutai Dragonspeaker / Rograkh, Son o | 102 | 25.0 | 28.0 | 24.3 | 23.8 | 100 | 7 | 2 |
| Dargo, the Shipwrecker / Tymna the Weaver | 84 | 24.0 | 28.0 | 24.5 | 23.0 | 100 | 90 | 14 |
| Tivit, Seller of Secrets | 67 | 28.3 | 28.0 | 27.1 | 26.5 | 25 | 18 | 7 |
| Vivi Ornitier | 66 | 26.0 | 28.0 | 23.9 | 23.2 | 100 | 8 | 5 |
| Etali, Primal Conqueror // Etali, Primal Sic | 50 | 27.9 | 28.2 | 27.4 | 25.0 | 44 | 34 | 2 |
| Thrasios, Triton Hero / Yoshimaru, Ever Fait | 48 | 27.8 | 28.0 | 26.8 | 26.8 | 31 | 8 | 8 |
| Kefka, Court Mage // Kefka, Ruler of Ruin | 43 | 27.0 | 28.0 | 26.0 | 24.3 | 74 | 21 | 0 |
| Kenrith, the Returned King | 42 | 27.0 | 28.0 | 26.4 | 25.8 | 79 | 36 | 2 |
| Tayam, Luminous Enigma | 41 | 30.9 | 28.6 | 29.3 | 29.3 | 0 | 7 | 7 |
| Malcolm, Keen-Eyed Navigator / Vial Smasher  | 38 | 25.8 | 28.0 | 25.0 | 23.5 | 100 | 16 | 3 |
| Thrasios, Triton Hero / Vial Smasher the Fie | 38 | 26.1 | 28.0 | 24.2 | 22.7 | 97 | 3 | 0 |
| Magda, Brazen Outlaw | 37 | 24.4 | 28.1 | 25.7 | 25.4 | 97 | 81 | 81 |
| Lumra, Bellow of the Woods | 34 | 46.5 | 28.3 | 37.3 | 37.3 | 0 | 0 | 0 |
| Brigid, Clachan's Heart // Brigid, Doun's Mi | 33 | 27.4 | 28.8 | 28.0 | 28.0 | 70 | 52 | 52 |
| Winota, Joiner of Forces | 33 | 27.5 | 29.0 | 28.2 | 28.1 | 88 | 67 | 67 |
| Ob Nixilis, Captive Kingpin | 28 | 25.0 | 28.0 | 24.6 | 22.2 | 100 | 32 | 4 |
| Rocco, Cabaretti Caterer | 27 | 27.1 | 28.0 | 26.6 | 26.0 | 78 | 33 | 22 |
| Inalla, Archmage Ritualist | 25 | 25.8 | 28.0 | 25.3 | 23.9 | 100 | 40 | 0 |
| Zirda, the Dawnwaker | 24 | 26.5 | 28.0 | 25.7 | 24.6 | 83 | 21 | 4 |
| Scion of the Ur-Dragon | 23 | 27.3 | 28.0 | 26.2 | 24.9 | 61 | 9 | 0 |
| Glarb, Calamity's Augur | 22 | 28.5 | 28.3 | 27.2 | 26.4 | 41 | 27 | 5 |
| Terra, Magical Adept // Esper Terra | 22 | 26.4 | 28.0 | 24.7 | 23.3 | 86 | 0 | 0 |
| Tevesh Szat, Doom of Fools / Thrasios, Trito | 21 | 27.9 | 28.0 | 26.5 | 26.0 | 19 | 14 | 0 |
| Leonardo, the Balance / Michelangelo, the He | 20 | 26.3 | 28.0 | 24.6 | 23.5 | 90 | 0 | 0 |
| Kediss, Emberclaw Familiar / Malcolm, Keen-E | 18 | 27.8 | 28.5 | 27.4 | 26.8 | 50 | 28 | 22 |
| The Cabbage Merchant | 18 | 24.9 | 28.2 | 26.4 | 26.4 | 100 | 89 | 89 |
| The Wandering Minstrel | 18 | 37.2 | 28.0 | 30.2 | 29.5 | 0 | 6 | 6 |
| Atraxa, Grand Unifier | 17 | 27.7 | 28.0 | 26.3 | 25.8 | 41 | 12 | 0 |
| Esika, God of the Tree // The Prismatic Brid | 17 | 26.6 | 28.0 | 24.8 | 23.8 | 88 | 0 | 0 |
| Malcolm, Keen-Eyed Navigator / Tana, the Blo | 17 | 27.9 | 28.0 | 27.0 | 26.5 | 41 | 18 | 6 |
| Marneus Calgar | 17 | 26.8 | 28.0 | 26.5 | 25.9 | 71 | 41 | 29 |
| Derevi, Empyrial Tactician | 16 | 27.1 | 28.0 | 25.8 | 25.8 | 81 | 6 | 6 |
| Malcolm, Keen-Eyed Navigator / Tymna the Wea | 15 | 26.4 | 28.0 | 25.6 | 24.9 | 100 | 20 | 7 |
| Dihada, Binder of Wills | 14 | 24.0 | 28.0 | 24.1 | 22.6 | 100 | 64 | 0 |
| The Gitrog Monster | 14 | 35.9 | 28.0 | 31.2 | 30.3 | 0 | 0 | 0 |
| Krark, the Thumbless / Sakashima of a Thousa | 13 | 25.5 | 28.0 | 24.5 | 23.0 | 100 | 15 | 0 |
| Najeela, the Blade-Blossom | 13 | 28.2 | 28.0 | 26.4 | 25.5 | 62 | 15 | 8 |
| Stella Lee, Wild Card | 13 | 25.5 | 28.0 | 23.1 | 22.5 | 100 | 8 | 0 |
| Arcum Dagsson | 12 | 22.8 | 28.0 | 24.2 | 24.2 | 100 | 75 | 75 |
| Heliod, the Radiant Dawn // Heliod, the Warp | 12 | 26.7 | 28.0 | 26.3 | 26.3 | 100 | 8 | 8 |
| Shorikai, Genesis Engine | 12 | 26.8 | 28.0 | 26.7 | 26.7 | 75 | 50 | 50 |
| Elsha of the Infinite | 11 | 25.2 | 28.0 | 23.7 | 23.1 | 100 | 9 | 0 |
| Niv-Mizzet, Parun | 11 | 27.9 | 28.1 | 26.5 | 26.1 | 55 | 18 | 0 |
| Dargo, the Shipwrecker / Reyhan, Last of the | 10 | 23.3 | 28.0 | 24.0 | 22.5 | 100 | 90 | 10 |
| Gwenom, Remorseless | 10 | 21.6 | 28.0 | 22.7 | 22.1 | 100 | 70 | 50 |
| Kodama of the East Tree / Tymna the Weaver | 10 | 28.6 | 28.8 | 28.7 | 28.6 | 70 | 60 | 60 |
