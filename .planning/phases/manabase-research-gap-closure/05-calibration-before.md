# cEDH land-target calibration — old flat-28 vs new hybrid (flag ON)

Sample: **3281** cEDH decks | actual lands mean 26.5
Old target: mean 28.1 (min 28.0 max 32.9)
New target: mean 25.4 (min 22.0 max 37.9)

**Under-target (actual < target):** OLD 2511/3281 = **76.5%** → NEW 714/3281 = **21.8%**
Decks the new target un-flags (were under, now OK): **1801** | newly flagged under: 4
Baseline-backed (N≥10): 2877 | recalibrated no-baseline: 404
Safety-floor(22) hits: 53 | ceiling(45) hits: 0

## Under-flag by segment
| Segment | N | actual mean | old target | new target | under OLD% | under NEW% |
|---------|---|------------|-----------|-----------|-----------|-----------|
| Baseline N>=10 | 2877 | 26.4 | 28.1 | 25.4 | 76.9% | 21.0% |
| No baseline | 404 | 26.7 | 28.2 | 25.3 | 74.3% | 27.5% |

## By commander (N≥10) — over-correction check (grindy Sisay/Tayam should stay ~healthy, not over-flagged-OK)
| Commander | N | actual mean | old tgt | new tgt | under OLD% | under NEW% |
|-----------|---|------------|--------|--------|-----------|-----------|
| Kraum, Ludevic's Opus / Tymna the Weaver | 442 | 25.9 | 28.0 | 25.0 | 99 | 7 |
| Kinnan, Bonder Prodigy | 327 | 25.8 | 28.0 | 24.8 | 98 | 12 |
| Rograkh, Son of Rohgahh / Thrasios, Triton H | 241 | 27.3 | 28.0 | 26.0 | 42 | 12 |
| Rograkh, Son of Rohgahh / Silas Renn, Seeker | 196 | 23.1 | 28.0 | 22.7 | 100 | 24 |
| Sisay, Weatherlight Captain | 177 | 27.7 | 28.0 | 25.9 | 28 | 5 |
| Thrasios, Triton Hero / Tymna the Weaver | 112 | 26.9 | 28.0 | 25.4 | 79 | 5 |
| Ral, Monsoon Mage // Ral, Leyline Prodigy | 105 | 21.6 | 28.0 | 22.8 | 100 | 77 |
| Ishai, Ojutai Dragonspeaker / Rograkh, Son o | 102 | 25.0 | 28.0 | 24.3 | 100 | 7 |
| Dargo, the Shipwrecker / Tymna the Weaver | 84 | 24.0 | 28.0 | 24.5 | 100 | 90 |
| Tivit, Seller of Secrets | 67 | 28.3 | 28.0 | 27.1 | 25 | 18 |
| Vivi Ornitier | 66 | 26.0 | 28.0 | 23.9 | 100 | 8 |
| Etali, Primal Conqueror // Etali, Primal Sic | 50 | 27.9 | 28.2 | 27.4 | 44 | 34 |
| Thrasios, Triton Hero / Yoshimaru, Ever Fait | 48 | 27.8 | 28.0 | 26.8 | 31 | 8 |
| Kefka, Court Mage // Kefka, Ruler of Ruin | 43 | 27.0 | 28.0 | 26.0 | 74 | 21 |
| Kenrith, the Returned King | 42 | 27.0 | 28.0 | 26.4 | 79 | 36 |
| Tayam, Luminous Enigma | 41 | 30.9 | 28.6 | 29.3 | 0 | 7 |
| Malcolm, Keen-Eyed Navigator / Vial Smasher  | 38 | 25.8 | 28.0 | 25.0 | 100 | 16 |
| Thrasios, Triton Hero / Vial Smasher the Fie | 38 | 26.1 | 28.0 | 24.2 | 97 | 3 |
| Magda, Brazen Outlaw | 37 | 24.4 | 28.1 | 25.7 | 97 | 81 |
| Lumra, Bellow of the Woods | 34 | 46.5 | 28.3 | 37.3 | 0 | 0 |
| Brigid, Clachan's Heart // Brigid, Doun's Mi | 33 | 27.4 | 28.8 | 28.0 | 70 | 52 |
| Winota, Joiner of Forces | 33 | 27.5 | 29.0 | 28.2 | 88 | 67 |
| Ob Nixilis, Captive Kingpin | 28 | 25.0 | 28.0 | 24.6 | 100 | 32 |
| Rocco, Cabaretti Caterer | 27 | 27.1 | 28.0 | 26.6 | 78 | 33 |
| Inalla, Archmage Ritualist | 25 | 25.8 | 28.0 | 25.3 | 100 | 40 |
| Zirda, the Dawnwaker | 24 | 26.5 | 28.0 | 25.7 | 83 | 21 |
| Scion of the Ur-Dragon | 23 | 27.3 | 28.0 | 26.2 | 61 | 9 |
| Glarb, Calamity's Augur | 22 | 28.5 | 28.3 | 27.2 | 41 | 27 |
| Terra, Magical Adept // Esper Terra | 22 | 26.4 | 28.0 | 24.7 | 86 | 0 |
| Tevesh Szat, Doom of Fools / Thrasios, Trito | 21 | 27.9 | 28.0 | 26.5 | 19 | 14 |
| Leonardo, the Balance / Michelangelo, the He | 20 | 26.3 | 28.0 | 24.6 | 90 | 0 |
| Kediss, Emberclaw Familiar / Malcolm, Keen-E | 18 | 27.8 | 28.5 | 27.4 | 50 | 28 |
| The Cabbage Merchant | 18 | 24.9 | 28.2 | 26.4 | 100 | 89 |
| The Wandering Minstrel | 18 | 37.2 | 28.0 | 30.2 | 0 | 6 |
| Atraxa, Grand Unifier | 17 | 27.7 | 28.0 | 26.3 | 41 | 12 |
| Esika, God of the Tree // The Prismatic Brid | 17 | 26.6 | 28.0 | 24.8 | 88 | 0 |
| Malcolm, Keen-Eyed Navigator / Tana, the Blo | 17 | 27.9 | 28.0 | 27.0 | 41 | 18 |
| Marneus Calgar | 17 | 26.8 | 28.0 | 26.5 | 71 | 41 |
| Derevi, Empyrial Tactician | 16 | 27.1 | 28.0 | 25.8 | 81 | 6 |
| Malcolm, Keen-Eyed Navigator / Tymna the Wea | 15 | 26.4 | 28.0 | 25.6 | 100 | 20 |
| Dihada, Binder of Wills | 14 | 24.0 | 28.0 | 24.1 | 100 | 64 |
| The Gitrog Monster | 14 | 35.9 | 28.0 | 31.2 | 0 | 0 |
| Krark, the Thumbless / Sakashima of a Thousa | 13 | 25.5 | 28.0 | 24.5 | 100 | 15 |
| Najeela, the Blade-Blossom | 13 | 28.2 | 28.0 | 26.4 | 62 | 15 |
| Stella Lee, Wild Card | 13 | 25.5 | 28.0 | 23.1 | 100 | 8 |
| Arcum Dagsson | 12 | 22.8 | 28.0 | 24.2 | 100 | 75 |
| Heliod, the Radiant Dawn // Heliod, the Warp | 12 | 26.7 | 28.0 | 26.3 | 100 | 8 |
| Shorikai, Genesis Engine | 12 | 26.8 | 28.0 | 26.7 | 75 | 50 |
| Elsha of the Infinite | 11 | 25.2 | 28.0 | 23.7 | 100 | 9 |
| Niv-Mizzet, Parun | 11 | 27.9 | 28.1 | 26.5 | 55 | 18 |
| Dargo, the Shipwrecker / Reyhan, Last of the | 10 | 23.3 | 28.0 | 24.0 | 100 | 90 |
| Gwenom, Remorseless | 10 | 21.6 | 28.0 | 22.7 | 100 | 70 |
| Kodama of the East Tree / Tymna the Weaver | 10 | 28.6 | 28.8 | 28.7 | 70 | 60 |
