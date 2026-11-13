# Stax vocabulary corpus

This corpus records the floor-less Cut Lab `stax` detector added in T2-S2.
The source evidence is `DeckFlow.Web.Tests/Fixtures/CutLab/golden-niv-mizzet-ghost-counsel-facts.json`.
Cut Lab passes the front oracle text to the detector. The stax name catalog is checked first; it includes Damping Sphere and the corrected name Thalia, Guardian of Thraben; the interaction audit reads the same name catalog (`InteractionAuditAggregator`), so both names now also appear in its stax and taxation bucket.

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
- `each spell costs`: Defense Grid; accepted only with `more to cast` in the same sentence.
- `spells your opponents cast cost`: synthetic opponent tax; accepted only with `more to cast` in the same sentence. Synthetic reducers cover both new tax needles.
- `each player can cast only`: the parallel positive spell-limit wording is included and covered synthetically.

Matching is case-insensitive. Each period-, newline-, or semicolon-delimited sentence is checked independently. A sentence containing `this turn`, `until end of turn`, or `until your next turn` is rejected before needle matching, so temporary restrictions do not become stax. Other sentences on the same card do not suppress a continuing restriction.

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

## T2-S2R oracle checks

Avatar's Wrath is false using its complete oracle text from `DeckFlow.Web.Tests/Manabase/avatar-facts.json`: "Until your next turn, your opponents can't cast spells from anywhere other than their hands." The temporary sentence is skipped. Synthetic coverage confirms that a separate permanent restriction still counts.

`DeckFlow.Web/Data/commander-staples/latest.json` contains Defense Grid and Damping Sphere as names only, and no existing JSON fixture supplies their oracle text. The following exact text was verified against the Scryfall named-card API on 2026-11-12 rather than recalled from memory:

- [Defense Grid](https://scryfall.com/card/9ed/293/defense-grid): "Each spell costs {3} more to cast except during its controller's turn." This matches `each spell costs` and the shared tax guard.
- [Damping Sphere](https://scryfall.com/card/dmr/219/damping-sphere): "If a land is tapped for two or more mana, it produces {C} instead of any other type and amount.\nEach spell a player casts costs {1} more to cast for each other spell that player has cast this turn." Neither new needle matches its tax sentence, which is also excluded by `this turn`. Its catalog entry supplies stax without weakening the duration rule.

Catalog-only coverage pins Damping Sphere and the exact name `Thalia, Guardian of Thraben`. All three tax needles share one explicit tax marker and require `more to cast`; reducers do not qualify.
