# Lifegain enabler vocabulary

This is a specification-backed vocabulary, not a measured card corpus. The detector operates only on supplied oracle text, without card names, deck state, I/O, or Web dependencies. Family keys are returned in catalog order and deduplicated across commanders. Unknown active keys are ignored.

## Corpus outcomes

The following oracle fixtures are transcribed from ticket T2-S3A. They are synthetic contract coverage, not evidence of performance on golden decks. Escaped `\n` denotes a line break. The associated unit tests exercise these exact examples.

### Commander side

Expected active:

   - `Flying\nWhenever you gain life, you may pay that much life. If you do, draw that many cards.\n{T}: Each opponent loses 1 life and you gain 1 life.` (Niv-Mizzet, Ghost Counsel)
   - `Whenever you gain life, put two +1/+1 counters on Karlov of the Ghost Council.`
   - `Whenever you gain life, you may pay {1}. If you do, draw a card and each opponent loses 1 life.`
   - `Lifelink\nAt the beginning of your end step, if you gained life this turn, distribute up to that many +1/+1 counters among any number of other target creatures.`
   - `Whenever you gain or lose life during your turn, ...`

Expected inactive:
   - A commander that only gains life or has lifelink with no such trigger or condition
     (`Lifelink`, `When this creature enters, you gain 3 life.`).
   - `Whenever an opponent gains life, ...`
   - `Flying\nWhenever Zur the Enchanter attacks, you may search your library for an enchantment card with mana value 3 or less, put it onto the battlefield, then shuffle.`

### Card side

Expected enabler:

   - `Whenever another creature enters, you gain 1 life.` (Soul Warden)
   - `{1}, Sacrifice a land: You gain 2 life.` (Zuran Orb)
   - `Each opponent loses X life. You gain life equal to the life lost this way.` (Exsanguinate)
   - `{T}: Each opponent loses 1 life and you gain 1 life.`
   - `First strike, lifelink`
   - `Target creature gains lifelink until end of turn.`
   - `Lifelink (Damage dealt by this creature also causes you to gain life.)`

   - `Whenever another creature enters, you may gain 1 life.` (Soul's Attendant)
   - `Whenever an artifact enters, you may gain 1 life.` (Leonin Elder)
   - `Whenever a player discards a card, you may gain 1 life.` (Confessor)
   - `Whenever a creature dies, you may gain 1 life.` (Deathgreeter)

Expected non-enabler:
   - `Exile target creature. Its controller gains life equal to its power.` (Swords to Plowshares)
   - `Target opponent gains 5 life.`
   - `Target player gains 4 life.` (decided: not counted; record it as a rejected needle with the
     reason "controller not guaranteed")
   - `You lose 2 life.` / `Pay 2 life: ...`
   - A pure trigger condition with no gain of its own:
     `Whenever you gain life, put two +1/+1 counters on this creature.` and
     `At the beginning of your end step, if you gained life this turn, draw a card.`
   - Empty or null text.

Additional repair must-not cases:

- `Whenever you gain life, you may pay {1}. If you do, draw a card.` is a trigger condition only and supplies no life gain.
- `You may pay 2 life.` offers a payment, not a life gain.

## Needles kept

- `Creatures you control have lifelink.` and `Target creature you control gains lifelink until end of turn.` supply lifegain through granted lifelink.
- `If you would gain life, you gain twice that much life instead.` qualifies because a doubler feeds the trigger.

- Commander triggers: `when` or `whenever` followed by `you gain life` or `you gain or lose life`. The explicit `you` boundary rejects opponent lifegain; a bare gain effect is not a trigger.
- Commander conditions: `if` or `as long as` followed by `you gain life`, `you gained life`, or `you have gained life`. These capture conditional rewards, including end-step checks.
- Card effects: `you gain ... life` or `you may gain ... life`, including `you gain life equal to ...`. Optional gain can supply the event when its controller chooses to gain life. Immediate `when`, `whenever`, and `if` prefixes are excluded so a trigger condition alone does not supply its own event.
- Card keyword: the whole word `lifelink`, whether printed on the card or granted to a creature. Reminder text does not need separate parsing.

All patterns are case-insensitive, culture-invariant, and compiled once. Separate commander-side and card-side regex arrays are paired by a private catalog entry. Another family is added by adding an entry with its key and patterns; callers remain unchanged. Matching does not lowercase or split the supplied strings. Only a nonempty active-family result requires a result collection.

## Needles rejected

- `Each time you gain life, draw a card.` is a pure trigger condition, not a gain.
- `Creatures your opponents control lose lifelink.` and `Target creature loses lifelink until end of turn.` remove an ability instead of supplying life gain.
- `You gain control of target creature if its owner has less life than you.` gains control, not life.
- `Each player gains 5 life.` and `You and target opponent each gain 3 life.` remain rejected: shared-recipient templates are outside the accepted controller-directed vocabulary.

- Bare commander `lifelink` and `you gain ... life`: gaining life alone does not establish a payoff trigger or condition.
- `an opponent gains life`: the trigger does not watch its controller's lifegain.
- `its controller gains life`: controller not guaranteed; Swords to Plowshares is not an enabler by this rule.
- `target opponent gains ... life`: the recipient is explicitly an opponent.
- `target player gains ... life`: controller not guaranteed.
- `you lose ... life` and `pay ... life`: losing or paying life does not gain life.
- Bare card-side `whenever you gain life` and `if you gained life`: a payoff without a gain effect cannot supply its own event.
- Substring `life` or `gain`: too broad to identify the event, recipient, and direction.

## Measurement and limits

Golden-deck measurement is added by ticket T2-S3B. No golden-deck precision, recall, or role assignments were measured here. This lexical vocabulary does not evaluate whether an ability can resolve in a particular game state, whether a granted lifelink creature is controlled by its caster, or whether replacement effects prevent a gain. The ticket explicitly includes granted lifelink.

## Golden deck measurement

The deck-aware pass assigns `enablers` to 40 of 129 golden facts. Twelve nonland cards remain in `other`. The per-card roles fixture and original 35-name `other` pin are unchanged.

[Certain — fixture and ticket compared] The ticket names 17 distinct cards despite describing a 19-card list. Of those 17, 16 receive `enablers`. `Reward the Faithful` misses: "Any number of target players each gain life equal to the greatest mana value among permanents you control." Its targeted-player wording is outside the unchanged detector's `you gain` and `lifelink` vocabulary. The specified minimum of 17 qualifying named cards is therefore unmet. Detector files remain untouched; this is a handoff risk, not a pass.

### All enablers (40, ordinal order)

- Alseid of Life's Bounty
- Authority of the Consuls
- Belladonna Took
- Blind Obedience
- Bloodchief Ascension
- Bofur, Reliable Guardian // Concerted Care
- Chaplain's Blessing
- Confessor
- Crypt Ghast
- Deathgreeter
- Doctor Doom, Unrivaled
- Dr. Pulaski, Cynical Clinician
- Elixir of Immortality
- Enlightened Confidant
- Exsanguinate
- Feast of Sanity
- Kambal, Consul of Allocation
- Kambal, Profiteering Mayor
- Leonin Elder
- Light of Hope
- Liliana the Faultless
- Lurrus of the Dream-Den
- MacCready, Lamplight Mayor
- March of Wretched Sorrow
- Martyr of Sands
- Miren, the Moaning Well
- Niv-Mizzet, Ghost Counsel
- Ocelot Pride
- Peace of Mind
- Professor Onyx
- Serra Ascendant
- Sheoldred, the Apocalypse
- Soul Warden
- Soul's Attendant
- Starving Revenant
- Tendrils of Agony
- The Meathook Massacre
- Twilight Prophet
- Union of the Third Path
- Zuran Orb

### Remaining nonland other (12, ordinal order)

- Bubbling Muck
- Chain of Smog
- Delney, Streetwise Lookout
- Enduring Tenacity
- Imp's Mischief
- Orim's Chant
- Reward the Faithful
- Sickening Dreams
- Silence
- The Queen of Dale
- Tinybones, Pocket Nuisance
- Toxic Deluge

### Additional enablers outside the named acceptance list (24)

These are detector outputs for review, not independently adjudicated false positives. Each quote is the fixture FrontOracleText used by the pass.

- **Alseid of Life's Bounty**: "Lifelink / {1}, Sacrifice this creature: Target creature or enchantment you control gains protection from the color of your choice until end of turn."
- **Authority of the Consuls**: "Creatures your opponents control enter tapped. / Whenever a creature an opponent controls enters, you gain 1 life."
- **Blind Obedience**: "Extort (Whenever you cast a spell, you may pay {W/B}. If you do, each opponent loses 1 life and you gain that much life.) / Artifacts and creatures your opponents control enter tapped."
- **Bloodchief Ascension**: "At the beginning of each end step, if an opponent lost 2 or more life this turn, you may put a quest counter on this enchantment. (Damage causes loss of life.) / Whenever a card is put into an opponent's graveyard from anywhere, if this enchantment has three or more quest counters on it, you may have that player lose 2 life. If you do, you gain 2 life."
- **Bofur, Reliable Guardian // Concerted Care**: "Lifelink"
- **Crypt Ghast**: "Extort (Whenever you cast a spell, you may pay {W/B}. If you do, each opponent loses 1 life and you gain that much life.) / Whenever you tap a Swamp for mana, add an additional {B}."
- **Doctor Doom, Unrivaled**: "Lifelink / {T}: You draw a card and lose 1 life. Then if your library has no cards in it, you win the game. (You win even if you have 0 life or didn't draw a card.)"
- **Enlightened Confidant**: "Lifelink / At the beginning of your end step, if you gained life this turn, surveil 1. If you put a card with mana value less than or equal to the amount of life you gained this turn into your graveyard this way, put that card into your hand."
- **Exsanguinate**: "Each opponent loses X life. You gain life equal to the life lost this way."
- **Feast of Sanity**: "Whenever you discard a card, this enchantment deals 1 damage to any target and you gain 1 life."
- **Kambal, Consul of Allocation**: "Whenever an opponent casts a noncreature spell, that player loses 2 life and you gain 2 life."
- **Light of Hope**: "Choose one — / • You gain 4 life. / • Destroy target enchantment. / • Put a +1/+1 counter on target creature."
- **Lurrus of the Dream-Den**: "Companion — Each permanent card in your starting deck has mana value 2 or less. (If this card is your chosen companion, you may put it into your hand from outside the game for {3} as a sorcery.) / Lifelink / Once during each of your turns, you may cast a permanent spell with mana value 2 or less from your graveyard."
- **March of Wretched Sorrow**: "As an additional cost to cast this spell, you may exile any number of black cards from your hand. This spell costs {2} less to cast for each card exiled this way. / March of Wretched Sorrow deals X damage to target creature or planeswalker and you gain X life."
- **Miren, the Moaning Well**: "{T}: Add {C}. / {3}, {T}, Sacrifice a creature: You gain life equal to the sacrificed creature's toughness."
- **Niv-Mizzet, Ghost Counsel**: "Flying / Whenever you gain life, you may pay that much life. If you do, draw that many cards. / {T}: Each opponent loses 1 life and you gain 1 life."
- **Ocelot Pride**: "First strike, lifelink / Ascend (If you control ten or more permanents, you get the city's blessing for the rest of the game.) / At the beginning of your end step, if you gained life this turn, create a 1/1 white Cat creature token. Then if you have the city's blessing, for each token you control that entered this turn, create a token that's a copy of it."
- **Professor Onyx**: "Magecraft — Whenever you cast or copy an instant or sorcery spell, each opponent loses 2 life and you gain 2 life. / +1: You lose 1 life. Look at the top three cards of your library. Put one of them into your hand and the rest into your graveyard. / −3: Each opponent sacrifices a creature with the greatest power among creatures that player controls. / −8: Each opponent may discard a card. If they don't, they lose 3 life. Repeat this process six more times."
- **Sheoldred, the Apocalypse**: "Deathtouch / Whenever you draw a card, you gain 2 life. / Whenever an opponent draws a card, they lose 2 life."
- **Starving Revenant**: "When this creature enters, surveil 2. Then for each card you put on top of your library, you draw a card and you lose 3 life. / Descend 8 — Whenever you draw a card, if there are eight or more permanent cards in your graveyard, target opponent loses 1 life and you gain 1 life."
- **Tendrils of Agony**: "Target player loses 2 life and you gain 2 life. / Storm (When you cast this spell, copy it for each spell cast before it this turn. You may choose new targets for the copies.)"
- **The Meathook Massacre**: "When The Meathook Massacre enters, each creature gets -X/-X until end of turn. / Whenever a creature you control dies, each opponent loses 1 life. / Whenever a creature an opponent controls dies, you gain 1 life."
- **Twilight Prophet**: "Flying / Ascend (If you control ten or more permanents, you get the city's blessing for the rest of the game.) / At the beginning of your upkeep, if you have the city's blessing, reveal the top card of your library and put it into your hand. Each opponent loses X life and you gain X life, where X is that card's mana value."
- **Union of the Third Path**: "Draw a card, then you gain life equal to the number of cards in your hand."
