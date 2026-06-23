# MQ-02 baseline diff — Brago (Core analyzer)

Sources with mana amount > 1: Sol Ring (2)

## Casual

- Health: off=NeedsWork → on=NeedsWork
- Lands 33 / target 35.8 (unchanged by MQ-02)
- Weakest color: off=Blue → on=Blue
- Cast%: 7/58 cards changed · mean |Δ| 0.2 pts · max +2

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Sun Titan | 6 | 53 | 55 | +2 |
| Y'shtola Rhul | 6 | 53 | 55 | +2 |
| Aang, Airbending Master | 5 | 77 | 78 | +1 |
| Deadeye Navigator | 6 | 53 | 54 | +1 |
| Peregrine Drake | 5 | 78 | 79 | +1 |
| Quantum Riddler | 5 | 75 | 76 | +1 |
| Venser, the Sojourner | 5 | 78 | 79 | +1 |

## Cedh

- Health: off=NeedsWork → on=NeedsWork
- Lands 33 / target 32.3 (unchanged by MQ-02)
- Weakest color: off=Blue → on=Blue
- Cast%: 7/58 cards changed · mean |Δ| 0.2 pts · max +2

| Card | MV | Off | On | Δ |
|---|---|---|---|---|
| Sun Titan | 6 | 53 | 55 | +2 |
| Y'shtola Rhul | 6 | 53 | 55 | +2 |
| Aang, Airbending Master | 5 | 77 | 78 | +1 |
| Deadeye Navigator | 6 | 53 | 54 | +1 |
| Peregrine Drake | 5 | 78 | 79 | +1 |
| Quantum Riddler | 5 | 75 | 76 | +1 |
| Venser, the Sojourner | 5 | 78 | 79 | +1 |


## Validator finding (2026-06-23, via gstack browser)

Drove the live ScrollVault / "Salubrious Snail" mana calculator
(https://scrollvault.net/tools/manabase/). It is a Karsten **colored-source**
calculator: it outputs sources-per-color (e.g. Blue 26/22) and a land-fixing
cast% Monte-Carlo. Per its own FAQ, mana rocks count as ~0.5–0.75 of a *colored
source* — it has **no concept of mana quantity** ("build the land base as if
rocks don't exist, then add rocks on top").

Conclusion: ScrollVault **cannot validate MQ-02**. MQ-02 changes the orthogonal
mana-quantity/affordability dimension (Sol Ring = 2 mana accelerates the curve);
the tool models color fixing only — exactly the dimension MQ-02 keeps invariant
(confirmed above: Brago color findings identical on/off; prior milestone
cross-check already matched this tool at mean Δ 2.8 pts on the color/cast model).

MQ-02 validation therefore rests on: (1) the golden-deck unit tests
(Sol Ring pays 2 → 2-drop turn 1; affordability rises; Gilded Lotus one-color;
flag-off byte-identical; color counts invariant) and (2) magnitude sanity
(Brago: +1/+2 on the top end, direction correct). A burst-mana deck would show a
larger but still bounded shift; there is no external tool that models the
quantity dimension to cross-check against.
