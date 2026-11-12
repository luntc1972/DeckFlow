# Recursion vocabulary corpus

The detector recognizes return or put instructions with non-self subjects, a graveyard source, and a battlefield or hand destination. It examines each sentence, line, or semicolon clause separately so unrelated zone changes do not combine. Animate Dead is a special paired pattern because its enchant ability establishes the source separately.

All 18 ticket cards use the checked-in golden facts fixture verbatim in classifier tests. Aurora Phoenix uses the checked-in Archidekt background-companion fixture. Synthetic grammar tests are not presented as card quotations. Tests never fetch live card text.

Judgement cases (golden fixture clauses; expected detector results):
- Black Sun's Twilight: true — "return a creature card with mana value X or less from your graveyard to the battlefield tapped."
- The Soul Stone: true — "return target creature card from your graveyard to the battlefield."
- Lurrus of the Dream-Den: false — "you may cast a permanent spell with mana value 2 or less from your graveyard."
- Enlightened Confidant: false — "If you put a card with mana value less than or equal to the amount of life you gained this turn into your graveyard this way, put that card into your hand."
- Starving Revenant: false — "When this creature enters, surveil 2."

Final action needle vocabulary:
- `return ` — The Soul Stone; Revival // Revenge; Shadow of the Grave.
- `put ` — Reanimate; Necromancy.
These verbs do not enumerate target counts or modifiers. Self references beginning with `this` are excluded. Bare named subjects without a card/type reference or selection determiner are excluded as self-return instructions; the string-only public signature does not supply the card's name. Synthetic tests cover up-to, another, numeric and X targets, plus a named self-return. No synthetic wording is attributed to a real card.
All action needles also require a graveyard source and hand or battlefield destination in the same clause. Animate Dead additionally uses the paired needles `enchant creature card in a graveyard` and `return enchanted creature card to the battlefield`.
Rejected needles:
- `graveyard` alone — Bojuka Bog and Thraben Charm exile graveyards.
- `from your graveyard to your hand` alone — Aurora Phoenix returns only itself (text sourced from archidekt-background-companion.json).
- `put that card into your hand` alone — Enlightened Confidant selects the newly surveilled card.
- `reanimate` alone — the existing synthetic `reanimate target creature card.` test supplies no source or destination. Its expected result changes from true to false; it is not a real card quotation.
No pre-existing self-return test changed expectation; the new Aurora Phoenix negative demonstrates the old detector's self-return false positive.

Source-versus-condition repair (T2-S1d): within an action clause, ignore tails introduced by `if`, `as long as`, `for each`, or `equal to the number of` before testing the source and destination. Graveyard source needles remain `from your graveyard`, `from a graveyard`, `from an opponent's graveyard`, and `in your graveyard`. The last form preserves Shadow of the Grave's destination-before-source instruction. Comparison qualifiers such as `less than or equal to` remain part of the effect, preserving Alesha, Who Laughs at Fate (text from `DeckFlow.Web.Tests/Manabase/fixtures/.manabase-arch-23563520-facts.json`). Accumulate Wisdom (text from `DeckFlow.Web.Tests/Manabase/avatar-facts.json`) is rejected because its graveyard count appears only in an `if` tail; conditional bounce and count cases are labelled synthetic in the tests. A valid source before a trailing condition still matches. Animate Dead's paired pattern remains unchanged.
