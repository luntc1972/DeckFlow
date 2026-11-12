# Stax vocabulary corpus

This corpus records the floor-less Cut Lab `stax` detector added in T2-S2.
The source evidence is `DeckFlow.Web.Tests/Fixtures/CutLab/golden-niv-mizzet-ghost-counsel-facts.json`.
Cut Lab passes the front oracle text to the detector. The existing stax name catalog is checked first and remains unchanged; the interaction audit retains its name-only bucket.

## Corpus outcomes

| Card | Result | Front oracle evidence |
| --- | --- | --- |
| Authority of the Consuls | true | Creatures your opponents control enter tapped. |
| Blind Obedience | true | Artifacts and creatures your opponents control enter tapped. |
| Dauntless Dismantler | true | Artifacts your opponents control enter tapped. |
| Linvala, Keeper of Silence | true | Activated abilities of creatures your opponents control can't be activated. |
| Drana and Linvala | true | Activated abilities of creatures your opponents control can't be activated. |
| Voice of Victory | true | Your opponents can't cast spells during your turn. |
| Silence | false | Your opponents can't cast spells this turn. |
| Orim's Chant | false | Target player can't cast spells this turn. |
| Bilbo's Gambit | false | If the gift was promised, players can't cast spells this turn. |
| Mother of Runes | false | Target creature you control gains protection from the color of your choice until end of turn. |
| All fixture lands | false | Self-tapped wording does not restrict opponents. |

## Needles kept

- `your opponents control enter tapped`: Authority, Blind Obedience, and Dauntless Dismantler establish the opponent-control boundary.
- `your opponents control can't be activated`: Linvala and Drana and Linvala establish a continuing activation restriction.
- `your opponents can't cast`: Voice of Victory establishes a continuing restriction during your turn.
- `your opponents can't activate`: the parallel direct activation wording is included in the vocabulary and covered synthetically.
- `spells cost {`: accepted only when the same sentence also contains `more to cast`. Synthetic `Spells cost {1} more to cast.` supplies coverage absent from the fixture.
- `each player can't cast more than`: synthetic `Each player can't cast more than one spell each turn.` supplies the spell-limit wording.
- `each player can cast only`: the parallel positive spell-limit wording is included and covered synthetically.

Matching is case-insensitive. Each period-, newline-, or semicolon-delimited sentence is checked independently. A sentence containing `this turn` or `until end of turn` is rejected before needle matching, so temporary restrictions do not become stax. Other sentences on the same card do not suppress a continuing restriction.

## Needles rejected

- Bare `enters tapped`: this would classify self-tapped lands as stax.
- `this spell costs`: a card's own additional casting cost does not tax opponents.
- `spells cost` without `more to cast`: cost reductions are not taxation.
- Temporary casting or activation prohibitions: Silence, Orim's Chant, and Bilbo's Gambit are one-shot effects.
- Life-loss triggers and extra-turn replacement: these do not match the selected restriction vocabulary.

## Judgement cases and limits

Kambal, Consul of Allocation returns false: “Whenever an opponent casts a noncreature spell, that player loses 2 life and you gain 2 life.” This punishes casting but does not prohibit it or add a casting cost.

Trouble in Pairs returns false: “If an opponent would begin an extra turn, that player skips that turn instead.” Its remaining draw trigger also imposes no matching restriction. Expanding the vocabulary to extra-turn replacement or adding a name would require an owner decision; neither is needed for the six-of-seven acceptance threshold.

The detector is a deliberately bounded heuristic, not an oracle-text parser. Catalog positives remain authoritative even without oracle text. A case-insensitive `stax` crowd category independently grants the role. The role adds no floor, twin finding, or headroom, and `stax` categories are functional rather than stranded subthemes.
