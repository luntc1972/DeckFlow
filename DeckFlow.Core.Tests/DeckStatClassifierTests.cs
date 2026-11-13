using DeckFlow.Core.Analysis;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for <see cref="DeckStatClassifier"/> — covers each classifier's true and false
/// cases plus all <see cref="DeckStatClassifier.ParseManaToken"/> variants.
/// </summary>
public sealed class DeckStatClassifierTests
{
    // -----------------------------------------------------------------------
    // IsRampCard
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Basic Land — Forest", "", true)]              // land type line
    [InlineData("", "add one mana of any color", true)]        // add one mana
    [InlineData("", "add two mana of any color", true)]        // add two mana
    [InlineData("", "search your library for a basic land", true)]  // basic land search
    [InlineData("", "search your library for up to two land cards", true)]  // generic land search
    [InlineData("", "create a Treasure token", true)]          // Treasure creation phrase 1
    [InlineData("", "you create a Treasure token.", true)]     // Treasure token phrase 2
    [InlineData("Artifact", "search your library for up to two basic land cards, put them", true)]  // up-to + land
    [InlineData("Artifact", "{T}: Add {C}.", true)]            // Mind Stone / Talisman colorless line
    [InlineData("Artifact", "{T}, Pay 1 life: Add {U} or {B}.", true)]  // Talisman of Dominance second line
    [InlineData("Artifact", "{1}, {T}: Add {U}{B}.", true)]    // Dimir Signet
    [InlineData("Artifact", "{T}: Add {C}{C}.", true)]         // Sol Ring
    [InlineData("Creature — Elf", "{T}: Add {G}.", true)]      // mana dork (Llanowar Elves)
    [InlineData("Instant", "Add {B}{B}{B}.", true)]            // Dark Ritual (ritual acceleration)
    [InlineData("Artifact", "{T}: Add {C}. (This is a reminder.)", true)]  // mana ability with reminder text
    [InlineData("Sorcery", "Create a Treasure token. (It is an artifact with \"{T}, Sacrifice this artifact: Add one mana of any color.\")", true)]  // Treasure creation with reminder text
    [InlineData("Creature — Elf", "Reach (This creature can block creatures with flying.)\n{T}: Add {G}.", true)]  // mana dork with reminder text
    public void IsRampCard_TrueCases(string typeLine, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsRampCard(typeLine, oracleText));
    }

    [Theory]
    [InlineData("Creature — Elf", "Tap: deal 1 damage to target creature.", false)]  // vanilla creature
    [InlineData("Instant", "Counter target spell.", false)]                           // counterspell, not ramp
    [InlineData("Enchantment", "Whenever a creature dies, draw a card.", false)]     // draw, not ramp
    [InlineData("Instant", "Gift a Treasure (You may promise an opponent a gift as you cast this spell. If you do, they create a Treasure token before its other effects. It's an artifact with \"{T}, Sacrifice this token: Add one mana of any color.\")\nReturn target spell to its owner's hand. If the gift was promised, players can't cast spells this turn.", false)]  // Bilbo's Gambit gift-a-Treasure reminder text
    [InlineData("Creature — Human", "Flash (You may cast this spell any time you could cast an instant. Add {C}.)", false)]  // Flash reminder text is not ramp
    public void IsRampCard_FalseCases(string typeLine, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsRampCard(typeLine, oracleText));
    }

    // -----------------------------------------------------------------------
    // IsDrawCard
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Draw three cards", true)]
    [InlineData("Sacrifice a creature: Draw cards equal to the sacrificed creature's power, then discard three cards.", true)]
    [InlineData("When Grothama leaves the battlefield, each player draws cards equal to the amount of damage dealt to Grothama this turn by sources they controlled.", true)]
    [InlineData("Investigate.", true)]
    [InlineData("Connive.", true)]
    [InlineData("Draw a card.", true)]
    [InlineData("When this enters, draw a card.", true)]
    public void IsDrawCard_TrueCases(string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsDrawCard(oracleText));
    }

    [Theory]
    [InlineData("Target player draws two cards", false)]
    [InlineData("Target player draws cards equal to the number of creatures they control.", false)]
    [InlineData("Destroy target creature.", false)]
    [InlineData("Search your library for a basic land card.", false)]
    [InlineData("Raugrin Triome enters the battlefield tapped.\n{T}: Add {R}, {U}, or {W}.\nCycling {3} ({3}, Discard this card: Draw a card.)", false)]
    public void IsDrawCard_FalseCases(string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsDrawCard(oracleText));
    }

    // -----------------------------------------------------------------------
    // IsInteractionCard
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Instant", "", true)]                                         // instant type
    [InlineData("Sorcery", "destroy target creature.", true)]                 // destroy target
    [InlineData("Sorcery", "exile target nonland permanent.", true)]          // exile target
    [InlineData("Instant", "counter target spell.", true)]                    // counter target
    [InlineData("Instant", "return target spell to its owner's hand.", true)] // return target spell
    [InlineData("Creature", "fight target creature you don't control.", true)]// fight target
    public void IsInteractionCard_TrueCases(string typeLine, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsInteractionCard(typeLine, oracleText));
    }

    [Theory]
    [InlineData("Artifact", "add one mana of any color.", false)]  // ramp rock, not interaction
    [InlineData("Creature — Elf", "Tap: add G.", false)]           // mana dork, not interaction
    public void IsInteractionCard_FalseCases(string typeLine, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsInteractionCard(typeLine, oracleText));
    }

    // -----------------------------------------------------------------------
    // IsBoardWipeCard
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("destroy all creatures.", true)]
    [InlineData("destroy all artifacts and enchantments.", true)]   // "destroy all artifacts"
    [InlineData("destroy all enchantments.", true)]
    [InlineData("each creature gets -1/-1 until end of turn.", true)]  // each creature + gets -
    [InlineData("exile all nonland permanents.", true)]
    [InlineData("All creatures get -X/-X until end of turn.", true)]
    [InlineData("Sickening Dreams deals X damage to each creature and each player.", true)]
    [InlineData("Pestilence deals 1 damage to each creature and each player.", true)]
    [InlineData("deals 3 damage to each creature your opponents control.", true)]
    public void IsBoardWipeCard_TrueCases(string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsBoardWipeCard(oracleText));
    }

    [Theory]
    [InlineData("destroy target creature.", false)]    // single-target, not a wipe
    [InlineData("exile target artifact.", false)]      // single-target
    [InlineData("Creatures you control get -1/-1 until end of turn.", false)]
    [InlineData("deals 2 damage to each creature you control.", false)]
    [InlineData("deals 1 damage to each opponent.", false)]
    [InlineData("All creatures get +1/+1 until end of turn.", false)]
    public void IsBoardWipeCard_FalseCases(string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsBoardWipeCard(oracleText));
    }

    [Theory]
    [InlineData("Ancient Tomb", "{T}: Add {C}{C}. This land deals 2 damage to you.", false)]
    [InlineData("Authority of the Consuls", "Creatures your opponents control enter tapped.\nWhenever a creature an opponent controls enters, you gain 1 life.", true)]
    [InlineData("Bilbo's Gambit", "Gift a Treasure (You may promise an opponent a gift as you cast this spell. If you do, they create a Treasure token before its other effects. It's an artifact with \"{T}, Sacrifice this token: Add one mana of any color.\")\nReturn target spell to its owner's hand. If the gift was promised, players can't cast spells this turn.", false)]
    [InlineData("Bleachbone Verge", "{T}: Add {B}.\n{T}: Add {W}. Activate only if you control a Plains or a Swamp.", false)]
    [InlineData("Blind Obedience", "Extort (Whenever you cast a spell, you may pay {W/B}. If you do, each opponent loses 1 life and you gain that much life.)\nArtifacts and creatures your opponents control enter tapped.", true)]
    [InlineData("Bojuka Bog", "This land enters tapped.\nWhen this land enters, exile target player's graveyard.\n{T}: Add {B}.", false)]
    [InlineData("Caves of Koilos", "{T}: Add {C}.\n{T}: Add {W} or {B}. This land deals 1 damage to you.", false)]
    [InlineData("Command Tower", "{T}: Add one mana of any color in your commander's color identity.", false)]
    [InlineData("Concealed Courtyard", "This land enters tapped unless you control two or fewer other lands.\n{T}: Add {W} or {B}.", false)]
    [InlineData("Dauntless Dismantler", "Artifacts your opponents control enter tapped.\n{X}{X}{W}, Sacrifice this creature: Destroy each artifact with mana value X.", true)]
    [InlineData("Drana and Linvala", "Flying, vigilance\nActivated abilities of creatures your opponents control can't be activated.\nDrana and Linvala has all activated abilities of all creatures your opponents control. You may spend mana as though it were mana of any color to activate those abilities.", true)]
    [InlineData("Eclipsed Steppe", "({T}: Add {W} or {B}.)\nThis land enters tapped unless you control two or more basic lands.", false)]
    [InlineData("Eiganjo, Seat of the Empire", "{T}: Add {W}.\nChannel — {2}{W}, Discard this card: It deals 4 damage to target attacking or blocking creature. This ability costs {1} less to activate for each legendary creature you control.", false)]
    [InlineData("Emergence Zone", "{T}: Add {C}.\n{1}, {T}, Sacrifice this land: You may cast spells this turn as though they had flash.", false)]
    [InlineData("Godless Shrine", "({T}: Add {W} or {B}.)\nAs this land enters, you may pay 2 life. If you don't, it enters tapped.", false)]
    [InlineData("Hall of Echoes", "{T}: Add {C}.\n{5}: This land becomes a copy of target creature you control until end of turn. The \"legend rule\" doesn't apply to permanents you control this turn.", false)]
    [InlineData("Isolated Chapel", "This land enters tapped unless you control a Plains or a Swamp.\n{T}: Add {W} or {B}.", false)]
    [InlineData("Kambal, Consul of Allocation", "Whenever an opponent casts a noncreature spell, that player loses 2 life and you gain 2 life.", false)]
    [InlineData("Linvala, Keeper of Silence", "Flying\nActivated abilities of creatures your opponents control can't be activated.", true)]
    [InlineData("Miren, the Moaning Well", "{T}: Add {C}.\n{3}, {T}, Sacrifice a creature: You gain life equal to the sacrificed creature's toughness.", false)]
    [InlineData("Mother of Runes", "{T}: Target creature you control gains protection from the color of your choice until end of turn.", false)]
    [InlineData("Orim's Chant", "Kicker {W} (You may pay an additional {W} as you cast this spell.)\nTarget player can't cast spells this turn. If this spell was kicked, creatures can't attack this turn.", false)]
    [InlineData("Plains", "({T}: Add {W}.)", false)]
    [InlineData("Polluted Delta", "{T}, Pay 1 life, Sacrifice this land: Search your library for an Island or Swamp card, put it onto the battlefield, then shuffle.", false)]
    [InlineData("Shadowy Backstreet", "({T}: Add {W} or {B}.)\nThis land enters tapped.\nWhen this land enters, surveil 1. (Look at the top card of your library. You may put it into your graveyard.)", false)]
    [InlineData("Shattered Sanctum", "This land enters tapped unless you control two or more other lands.\n{T}: Add {W} or {B}.", false)]
    [InlineData("Shineshadow Snarl", "As this land enters, you may reveal a Plains or Swamp card from your hand. If you don't, this land enters tapped.\n{T}: Add {W} or {B}.", false)]
    [InlineData("Silence", "Your opponents can't cast spells this turn.", false)]
    [InlineData("Swamp", "({T}: Add {B}.)", false)]
    [InlineData("Trouble in Pairs", "If an opponent would begin an extra turn, that player skips that turn instead.\nWhenever an opponent attacks you with two or more creatures, draws their second card each turn, or casts their second spell each turn, you draw a card.", false)]
    [InlineData("Turbulent Moor", "({T}: Add {W} or {B}.)\nThis land enters tapped unless your opponents control eight or more lands.", false)]
    [InlineData("Voice of Victory", "Mobilize 2 (Whenever this creature attacks, create two tapped and attacking 1/1 red Warrior creature tokens. Sacrifice them at the beginning of the next end step.)\nYour opponents can't cast spells during your turn.", true)]
    [InlineData("Synthetic tax", "Spells cost {1} more to cast.", true)]
    [InlineData("Synthetic limit", "Each player can't cast more than one spell each turn.", true)]
    [InlineData("Synthetic self cost", "This spell costs {1} more to cast.", false)]
    [InlineData("Synthetic self tapped", "This permanent enters tapped.", false)]
    [InlineData("Synthetic activation", "Your opponents can't activate abilities.", true)]
    [InlineData("Synthetic positive limit", "Each player can cast only one spell each turn.", true)]
    [InlineData("Synthetic reduction", "Spells cost {1} less to cast.", false)]
    [InlineData("Synthetic temporary", "Your opponents can't activate abilities until end of turn.", false)]
    [InlineData("Synthetic mixed duration", "Your opponents can't cast spells this turn.\nCreatures your opponents control enter tapped.", true)]
    [InlineData("Avatar's Wrath", "Choose up to one target creature, then airbend all other creatures. (Exile them. While each one is exiled, its owner may cast it for {2} rather than its mana cost.)\nUntil your next turn, your opponents can't cast spells from anywhere other than their hands.\nExile Avatar's Wrath.", false)]
    [InlineData("Synthetic next turn", "Until your next turn, your opponents can't cast spells.", false)]
    [InlineData("Synthetic mixed next turn", "Until your next turn, your opponents can't cast spells.\nCreatures your opponents control enter tapped.", true)]
    [InlineData("Defense Grid", "Each spell costs {3} more to cast except during its controller's turn.", true)]
    [InlineData("Damping Sphere", "If a land is tapped for two or more mana, it produces {C} instead of any other type and amount.\nEach spell a player casts costs {1} more to cast for each other spell that player has cast this turn.", true)]
    [InlineData("Synthetic opponent tax", "Spells your opponents cast cost {2} more to cast.", true)]
    [InlineData("Synthetic each spell reduction", "Each spell costs {1} less to cast.", false)]
    [InlineData("Synthetic opponent reduction", "Spells your opponents cast cost {1} less to cast.", false)]
    public void IsStaxCard_CorpusAndSyntheticCases_MatchesContract(string name, string oracleText, bool expected)
    {
        Assert.True(expected == DeckStatClassifier.IsStaxCard(name, oracleText), name);
    }

    [Theory]
    [InlineData("Blood Moon")]
    [InlineData("Collector Ouphe")]
    [InlineData("Damping Sphere")]
    [InlineData("Drannith Magistrate")]
    [InlineData("Grand Arbiter Augustin IV")]
    [InlineData("Root Maze")]
    [InlineData("Rule of Law")]
    [InlineData("Sphere of Resistance")]
    [InlineData("Static Orb")]
    [InlineData("Thalia, Guardian of Thraben")]
    [InlineData("Trinisphere")]
    [InlineData("Winter Orb")]
    public void IsStaxCard_CatalogNameWithoutOracle_ReturnsTrue(string name)
    {
        Assert.True(DeckStatClassifier.IsStaxCard(name, ""));
    }

    // -----------------------------------------------------------------------
    // IsRecursionCard
    // -----------------------------------------------------------------------

    // Evidence labels identify fixtures for real cards; other rows use synthetic wording.
    [Theory]
    [InlineData("Accumulate Wisdom — avatar-facts.json", "Look at the top three cards of your library. Put one of those cards into your hand and the rest on the bottom of your library in any order. Put each of those cards into your hand instead if there are three or more Lesson cards in your graveyard.", false)]
    [InlineData("Synthetic wording pattern — conditional bounce", "Return target creature to its owner's hand if there are seven or more cards in your graveyard.", false)]
    [InlineData("Synthetic wording pattern — ongoing count", "Return target card to your hand as long as there are three cards in your graveyard.", false)]
    [InlineData("Synthetic wording pattern — per-card count", "Return target creature to its owner's hand for each card in your graveyard.", false)]
    [InlineData("Synthetic wording pattern — qualified source", "Put target creature card from your graveyard into your hand if there are seven or more cards in your graveyard.", true)]
    [InlineData("Synthetic wording pattern — in-source", "Return all creature cards in your graveyard to your hand.", true)]
    [InlineData("Alesha, Who Laughs at Fate — Manabase/fixtures/.manabase-arch-23563520-facts.json", "First strike\nWhenever Alesha attacks, put a +1/+1 counter on it.\nRaid — At the beginning of your end step, if you attacked this turn, return target creature card with mana value less than or equal to Alesha's power from your graveyard to the battlefield.", true)]
    [InlineData("Synthetic wording pattern — comparison qualifier", "Return target creature card with mana value less than or equal to 2 from your graveyard to your hand.", true)]
    [InlineData("Synthetic wording pattern — life count", "Return target creature to its owner's hand and you gain life equal to the number of cards in your graveyard.", false)]
    public void IsRecursionCard_ConditionsAndComparisons_MatchesContract(string evidence, string oracleText, bool expected)
    {
        Assert.True(expected == DeckStatClassifier.IsRecursionCard(oracleText), evidence);
    }

    [Theory]
    [InlineData("Animate Dead", "Enchant creature card in a graveyard\nWhen this Aura enters, if it's on the battlefield, it loses \"enchant creature card in a graveyard\" and gains \"enchant creature put onto the battlefield with this Aura.\" Return enchanted creature card to the battlefield under your control and attach this Aura to it. When this Aura leaves the battlefield, that creature's controller sacrifices it.\nEnchanted creature gets -1/-0.", true)]
    [InlineData("Reanimate", "Put target creature card from a graveyard onto the battlefield under your control. You lose life equal to that card's mana value.", true)]
    [InlineData("Necromancy", "You may cast this spell as though it had flash. If you cast it any time a sorcery couldn't have been cast, the controller of the permanent it becomes sacrifices it at the beginning of the next cleanup step.\nWhen this enchantment enters, if it's on the battlefield, it becomes an Aura with \"enchant creature put onto the battlefield with Necromancy.\" Put target creature card from a graveyard onto the battlefield under your control and attach this enchantment to it. When this enchantment leaves the battlefield, that creature's controller sacrifices it.", true)]
    [InlineData("Revival // Revenge", "Return target creature card with mana value 3 or less from your graveyard to the battlefield.\nDouble your life total. Target opponent loses half their life, rounded up.", true)]
    [InlineData("Shadow of the Grave", "Return to your hand all cards in your graveyard that you cycled or discarded this turn.", true)]
    [InlineData("Black Sun's Twilight", "Up to one target creature gets -X/-X until end of turn. If X is 5 or more, return a creature card with mana value X or less from your graveyard to the battlefield tapped.", true)]
    [InlineData("The Soul Stone", "Indestructible\n{T}: Add {B}.\n{6}{B}, {T}, Exile a creature you control: Harness The Soul Stone. (Once harnessed, its ∞ ability is active.)\n∞ — At the beginning of your upkeep, return target creature card from your graveyard to the battlefield.", true)]
    [InlineData("Bojuka Bog", "This land enters tapped.\nWhen this land enters, exile target player's graveyard.\n{T}: Add {B}.", false)]
    [InlineData("Boggart Trawler // Boggart Bog", "When this creature enters, exile target player's graveyard.\nAs this land enters, you may pay 3 life. If you don't, it enters tapped.\n{T}: Add {B}.", false)]
    [InlineData("Thraben Charm", "Choose one —\n• Thraben Charm deals damage equal to twice the number of creatures you control to target creature.\n• Destroy target enchantment.\n• Exile any number of target players' graveyards.", false)]
    [InlineData("Elixir of Immortality", "{2}, {T}: You gain 5 life. Shuffle this artifact and your graveyard into their owner's library.", false)]
    [InlineData("Cabal Ritual", "Add {B}{B}{B}.\nThreshold — Add {B}{B}{B}{B}{B} instead if there are seven or more cards in your graveyard.", false)]
    [InlineData("Shadowy Backstreet", "({T}: Add {W} or {B}.)\nThis land enters tapped.\nWhen this land enters, surveil 1. (Look at the top card of your library. You may put it into your graveyard.)", false)]
    [InlineData("Bloodchief Ascension", "At the beginning of each end step, if an opponent lost 2 or more life this turn, you may put a quest counter on this enchantment. (Damage causes loss of life.)\nWhenever a card is put into an opponent's graveyard from anywhere, if this enchantment has three or more quest counters on it, you may have that player lose 2 life. If you do, you gain 2 life.", false)]
    [InlineData("Ulamog, the Infinite Gyre", "When you cast this spell, destroy target permanent.\nIndestructible\nAnnihilator 4 (Whenever this creature attacks, defending player sacrifices four permanents of their choice.)\nWhen Ulamog is put into a graveyard from anywhere, its owner shuffles their graveyard into their library.", false)]
    [InlineData("Lurrus of the Dream-Den", "Companion — Each permanent card in your starting deck has mana value 2 or less. (If this card is your chosen companion, you may put it into your hand from outside the game for {3} as a sorcery.)\nLifelink\nOnce during each of your turns, you may cast a permanent spell with mana value 2 or less from your graveyard.", false)]
    [InlineData("Enlightened Confidant", "Lifelink\nAt the beginning of your end step, if you gained life this turn, surveil 1. If you put a card with mana value less than or equal to the amount of life you gained this turn into your graveyard this way, put that card into your hand.", false)]
    [InlineData("Starving Revenant", "When this creature enters, surveil 2. Then for each card you put on top of your library, you draw a card and you lose 3 life.\nDescend 8 — Whenever you draw a card, if there are eight or more permanent cards in your graveyard, target opponent loses 1 life and you gain 1 life.", false)]
    public void IsRecursionCard_GoldenCorpus_MatchesContract(string name, string oracleText, bool expected)
    {
        Assert.True(expected == DeckStatClassifier.IsRecursionCard(oracleText), name);
    }

    [Theory]
    [InlineData("return target card from your graveyard to your hand.", true)]
    [InlineData("return all land cards from your graveyard to the battlefield.", true)]
    [InlineData("return target permanent card from your graveyard to the battlefield.", true)]
    [InlineData("put that card from your graveyard to your hand.", true)]
    [InlineData("Return up to two target creature cards from your graveyard to your hand.", true)] // Synthetic wording pattern.
    [InlineData("Return another target creature card from your graveyard to your hand.", true)] // Synthetic wording pattern.
    [InlineData("Return two target cards from your graveyard to your hand.", true)] // Synthetic wording pattern.
    [InlineData("Return X target creature cards from your graveyard to the battlefield.", true)] // Synthetic wording pattern.
    [InlineData("Put up to one target creature card from a graveyard onto the battlefield under your control.", true)] // Synthetic wording pattern.
    public void IsRecursionCard_TrueCases(string oracleText, bool expected)
    {
        Assert.True(expected == DeckStatClassifier.IsRecursionCard(oracleText), oracleText);
    }

    [Theory]
    [InlineData("reanimate target creature card.", false)]
    [InlineData("return this creature from your graveyard to your hand.", false)]
    [InlineData("return this card from your graveyard to your hand.", false)]
    [InlineData("Flying\nCascade (When you cast this spell, exile cards from the top of your library until you exile a nonland card that costs less. You may cast it without paying its mana cost. Put the exiled cards on the bottom in a random order.)\nWhenever you cast a spell with cascade, return this card from your graveyard to your hand.", false)]
    [InlineData("draw a card.", false)]
    [InlineData("destroy target creature.", false)]
    [InlineData("Return Example Name from your graveyard to your hand.", false)] // Synthetic named self-return.
    [InlineData("Put this card from your graveyard onto the battlefield.", false)] // Synthetic self-return.
    public void IsRecursionCard_FalseCases(string oracleText, bool expected)
    {
        Assert.True(expected == DeckStatClassifier.IsRecursionCard(oracleText), oracleText);
    }

    // -----------------------------------------------------------------------
    // IsClosingPowerCard
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("", "each opponent loses 1 life.", true)]
    [InlineData("", "you win the game.", true)]
    [InlineData("", "take an extra turn.", true)]
    [InlineData("", "this creature has double strike.", true)]
    [InlineData("Creature — Beast — Craterhoof Behemoth", "", true)]         // Craterhoof in type line
    [InlineData("", "whenever this creature deals combat damage to a player, draw a card.", true)]
    [InlineData("Creature", "whenever this creature attacks, it gets +X/+X.", true)]
    public void IsClosingPowerCard_TrueCases(string typeLine, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsClosingPowerCard(typeLine, oracleText));
    }

    [Theory]
    [InlineData("Creature — Elf", "Tap: add G.", false)]
    [InlineData("Sorcery", "search your library for a basic land card.", false)]
    public void IsClosingPowerCard_FalseCases(string typeLine, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsClosingPowerCard(typeLine, oracleText));
    }

    // -----------------------------------------------------------------------
    // IsTutorCard
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Search your library for a card, then shuffle.", true)]            // generic tutor
    [InlineData("Search your library for a creature card, reveal it.", true)]      // typed non-land tutor
    [InlineData("Search your library for a nonland card, exile it, then shuffle.", true)]  // nonland tutor must not match the "land card" land-fetch exclusion
    public void IsTutorCard_TrueCases(string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsTutorCard(oracleText));
    }

    [Theory]
    [InlineData("Search your library for a basic land card and put it onto the battlefield.", false)]  // Rampant Growth
    [InlineData("Search your library for up to two basic land cards, then shuffle.", false)]           // Cultivate
    [InlineData("Search your library for a Mountain, then put that land onto the battlefield.", false)]  // land onto battlefield
    [InlineData("Draw a card.", false)]                                                                  // not a tutor
    public void IsTutorCard_FalseCases(string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsTutorCard(oracleText));
    }

    // -----------------------------------------------------------------------
    // IsFastManaCard
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Artifact", "{T}: Add {C}{C}.", "", true)]            // Mana Crypt: MV 0, artifact, produces mana
    [InlineData("Artifact", "Add {C}{C}{C}.", "", true)]              // Jeweled-Lotus-style: MV 0, "Add {"
    [InlineData("Artifact", "{T}: Add {C}{C}. (This is a reminder.)", "", true)]  // real mana ability before reminder text
    public void IsFastManaCard_TrueCases(string typeLine, string oracleText, string manaCost, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsFastManaCard(typeLine, oracleText, manaCost));
    }

    [Theory]
    [InlineData("Artifact", "{T}: Add {C}.", "{1}", false)]           // Sol Ring: MV 1, not fast mana
    [InlineData("Creature — Elf", "{T}: Add {G}.", "{G}", false)]     // mana dork, not artifact
    [InlineData("Artifact", "{T}: Tap target creature.", "", false)]  // MV 0 artifact but produces no mana
    [InlineData("Artifact", "Indestructible (Effects that say destroy don't destroy this. It's like a land with \"{T}: Add {C}.\")", "", false)]  // mana ability only in reminder text
    [InlineData("Artifact — Equipment", "Equip {2} (Attach to target creature you control. Add {C} is not real here.)", "", false)]  // mana production only in equip reminder text
    public void IsFastManaCard_FalseCases(string typeLine, string oracleText, string manaCost, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsFastManaCard(typeLine, oracleText, manaCost));
    }

    // -----------------------------------------------------------------------
    // IsRampOrDrawUnderThreeMv
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Sorcery", "Draw two cards.", "{1}{U}", true)]                       // MV 2, draw
    [InlineData("Artifact", "{T}: Add one mana of any color.", "{2}", true)]         // MV 2, ramp
    public void IsRampOrDrawUnderThreeMv_TrueCases(string typeLine, string oracleText, string manaCost, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsRampOrDrawUnderThreeMv(typeLine, oracleText, manaCost));
    }

    [Theory]
    [InlineData("Sorcery", "Draw four cards.", "{4}{U}", false)]                     // MV 5, over threshold
    [InlineData("Instant", "Counter target spell.", "{U}", false)]                   // MV 1 but neither ramp nor draw
    public void IsRampOrDrawUnderThreeMv_FalseCases(string typeLine, string oracleText, string manaCost, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsRampOrDrawUnderThreeMv(typeLine, oracleText, manaCost));
    }

    // -----------------------------------------------------------------------
    // IsCounterspellCard
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Counter target spell.", true)]
    [InlineData("Counter target spell unless its controller pays {3}.", true)]
    public void IsCounterspellCard_TrueCases(string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsCounterspellCard(oracleText));
    }

    [Theory]
    [InlineData("Counter target activated or triggered ability.", false)]  // ability counter, not spell
    [InlineData("Destroy target creature.", false)]                        // not a counter
    public void IsCounterspellCard_FalseCases(string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsCounterspellCard(oracleText));
    }

    // -----------------------------------------------------------------------
    // IsTargetedRemovalCard
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Instant", "Destroy target creature.", true)]
    [InlineData("Sorcery", "Exile target creature or planeswalker.", true)]
    [InlineData("Instant", "Target creature gets -3/-3 until end of turn.", true)]
    public void IsTargetedRemovalCard_TrueCases(string typeLine, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsTargetedRemovalCard(typeLine, oracleText));
    }

    [Theory]
    [InlineData("Sorcery", "Destroy all creatures.", false)]
    [InlineData("Instant", "Return target creature you control to its owner's hand.", false)]
    public void IsTargetedRemovalCard_FalseCases(string typeLine, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsTargetedRemovalCard(typeLine, oracleText));
    }

    // -----------------------------------------------------------------------
    // IsSelfTargetedInteraction
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Instant", "Target creature you control gains hexproof until end of turn.", true)]
    public void IsSelfTargetedInteraction_TrueCases(string typeLine, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsSelfTargetedInteraction(typeLine, oracleText));
    }

    [Theory]
    [InlineData("Instant", "Destroy target creature.", false)]
    public void IsSelfTargetedInteraction_FalseCases(string typeLine, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsSelfTargetedInteraction(typeLine, oracleText));
    }

    // -----------------------------------------------------------------------
    // IsPseudoRemovalCard
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Instant", "Return target creature to its owner's hand.", true)]
    [InlineData("Sorcery", "Put target creature into its owner's library third from the top.", true)]
    [InlineData("Instant", "Exile target creature. Return it to the battlefield under its owner's control at the beginning of the next end step.", true)]
    public void IsPseudoRemovalCard_TrueCases(string typeLine, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsPseudoRemovalCard(typeLine, oracleText));
    }

    [Theory]
    [InlineData("Instant", "Destroy target creature.", false)]
    [InlineData("Instant", "Return target creature you control to its owner's hand.", false)]
    public void IsPseudoRemovalCard_FalseCases(string typeLine, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsPseudoRemovalCard(typeLine, oracleText));
    }

    // -----------------------------------------------------------------------
    // IsProtectionCard
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Heroic Intervention", "Permanents you control gain indestructible until end of turn.", true)]
    [InlineData("Teferi's Protection", "Your life total can't change. You gain protection from everything. All permanents you control phase out.", true)]
    // Mother of Runes: singular "gains protection from" — the ROADMAP-named D-06 defect. This is
    // the plan's real Scryfall oracle text, quoted verbatim from 09.1-RESEARCH.md.
    [InlineData("Mother of Runes", "{T}: Target creature you control gains protection from the color of your choice until end of turn.", true)]
    // -----------------------------------------------------------------------------------------
    // 09.1-02 per-needle positive rows. Every row below uses an empty `name` argument (never a
    // real card's name) so `StaxProtectionCatalog.IsProtection` — which is checked FIRST inside
    // `IsProtectionCard` and always returns false for an empty/whitespace name — cannot be the
    // reason the assertion passes. Each oracle string is quoted verbatim from the real Scryfall
    // card named in the trailing comment (fetched from the same bulk archive plan `09.1-02` Task 1
    // derived counts from), never hand-authored, per RESEARCH.md's "Don't Hand-Roll" table.
    // -----------------------------------------------------------------------------------------
    [InlineData("", "Target creature you control gets +2/+2 and gains hexproof until end of turn. (It can't be the target of spells or abilities your opponents control.)", true)] // Blossoming Defense — gains hexproof (singular)
    [InlineData("", "Prevent all damage that would be dealt to creatures this turn. Creatures you control gain hexproof until end of turn. (They can't be the targets of spells or abilities your opponents control.)", true)] // Blinding Fog — gain hexproof (plural)
    [InlineData("", "Target creature gets +2/+2 and gains indestructible until end of turn. (Damage and effects that say \"destroy\" don't destroy it.)", true)] // Adamant Will — gains indestructible (singular)
    [InlineData("", "Whenever you attack with this creature and/or your commander, for each opponent, create a 1/1 red Goblin creature token that's tapped and attacking that player.\nSacrifice this creature: Creature tokens you control gain indestructible until end of turn.", true)] // Ainok Strike Leader — gain indestructible (plural)
    [InlineData("", "Lifelink\n{1}, Sacrifice this creature: Target creature or enchantment you control gains protection from the color of your choice until end of turn.", true)] // Alseid of Life's Bounty — gains protection from (singular)
    [InlineData("", "Choose a color. White creatures you control gain protection from the chosen color until end of turn.", true)] // Brave the Elements — gain protection from (plural)
    [InlineData("", "Double strike\nTeleport — {3}{W}: This creature phases out. (Treat it and anything attached to it as though they don't exist until your next turn.)", true)] // Blink Dog — phases out (singular)
    [InlineData("", "Whenever this creature blocks or becomes blocked by a creature, this creature and that creature phase out. (While they're phased out, they're treated as though they don't exist. Each one phases in before its controller untaps during their next untap step.)", true)] // Dream Fighter — phase out (plural)
    [InlineData("", "Enchant creature\nEnchanted creature has hexproof and can't be blocked by more than one creature.", true)] // Alpha Authority — has hexproof (Equipment/conditional status, singular)
    [InlineData("", "Flash\nEnchant creature\nEnchanted creature has shroud. (It can't be the target of spells or abilities.)", true)] // Alexi's Cloak — has shroud (Equipment/conditional status, singular)
    [InlineData("", "All Slivers have shroud. (They can't be the targets of spells or abilities.)", true)] // Crystalline Sliver — have shroud (Equipment/conditional status, plural)
    [InlineData("", "Flying\n{S}{S}: This creature gains shroud until end of turn. ({S} can be paid with one mana from a snow source. A creature with shroud can't be the target of spells or abilities.)", true)] // Frost Raptor — gains shroud (temporary grant, singular)
    [InlineData("", "You gain shroud until end of turn. (You can't be the target of spells or abilities.)\nCycling {2} ({2}, Discard this card: Draw a card.)", true)] // Gilded Light — gain shroud (temporary grant, plural)
    [InlineData("", "Regenerate target creature.", true)] // Death Ward — regenerate target
    [InlineData("", "Discard a card: Regenerate this creature. (The next time this creature would be destroyed this turn, instead tap it, remove it from combat, and heal all damage on it.)", true)] // Patchwork Gnomes — regenerate this creature
    [InlineData("", "Commander creatures you own get +3/+3 and have flying, deathtouch, \"Ward—Pay 3 life,\" and \"At the beginning of your upkeep, sacrifice a creature.\"", true)] // Cultist of the Absolute — ward—pay
    // -----------------------------------------------------------------------------------------
    // 09.1-02 ROADMAP-named misses (real card names, real oracle text) — genuine end-to-end
    // coverage on top of the per-needle rows above. None of these four names is on
    // StaxProtectionCatalog's curated list, so the assertion still exercises the needle table.
    // -----------------------------------------------------------------------------------------
    [InlineData("Swiftfoot Boots", "Equipped creature has hexproof and haste. (It can't be the target of spells or abilities your opponents control. It can attack and {T} no matter when it came under your control.)\nEquip {1} ({1}: Attach to target creature you control. Equip only as a sorcery.)", true)]
    [InlineData("Lightning Greaves", "Equipped creature has haste and shroud. (It can't be the target of spells or abilities.)\nEquip {0}", true)]
    [InlineData("Goblin Chirurgeon", "Sacrifice a Goblin: Regenerate target creature.", true)]
    [InlineData("Hexing Squelcher", "This spell can't be countered.\nWard—Pay 2 life.\nSpells you control can't be countered.\nOther creatures you control have \"Ward—Pay 2 life.\"", true)]
    public void IsProtectionCard_TrueCases(string name, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsProtectionCard(name, oracleText));
    }

    [Theory]
    [InlineData("Lightning Bolt", "Deal 3 damage to any target.", false)]
    // The five real "cannot be regenerated" removal spells named in this plan's objective —
    // Success Criterion 4's bounded over-match family. Real Scryfall oracle text throughout.
    [InlineData("Artifact Mutation", "Destroy target artifact. It can't be regenerated. Create X 1/1 green Saproling creature tokens, where X is that artifact's mana value.", false)]
    [InlineData("Damn", "Destroy target creature. A creature destroyed this way can't be regenerated.\nOverload {2}{W}{W} (You may cast this spell for its overload cost. If you do, change \"target\" in its text to \"each.\")", false)]
    [InlineData("Damnation", "Destroy all creatures. They can't be regenerated.", false)]
    [InlineData("Putrefy", "Destroy target artifact or creature. It can't be regenerated.", false)]
    [InlineData("Terminate", "Destroy target creature. It can't be regenerated.", false)]
    // A real, non-granting hexproof mention: Detection Tower strips hexproof FROM OPPONENTS so you
    // can target it — the exact removal-enabler collision that got bare `have hexproof` rejected in
    // docs/research/protection-vocabulary-corpus-2026-09.md. Unconditionally required by Criterion 4.
    [InlineData("Detection Tower", "{T}: Add {C}.\n{1}, {T}: Until end of turn, your opponents and creatures your opponents control with hexproof can be the targets of spells and abilities you control as though they didn't have hexproof.", false)]
    // A real, non-granting indestructible mention: Smite the Deathless strips indestructible from
    // the target rather than granting it. Unconditionally required by Criterion 4.
    [InlineData("Smite the Deathless", "Smite the Deathless deals 3 damage to target creature. That creature loses indestructible until end of turn. If that creature would die this turn, exile it instead.", false)]
    public void IsProtectionCard_FalseCases(string name, string oracleText, bool expected)
    {
        Assert.Equal(expected, DeckStatClassifier.IsProtectionCard(name, oracleText));
    }

    // Why: Success Criterion 2 as a machine-checked invariant, not prose. This is the RATIFIED
    // pairing rule from docs/research/protection-vocabulary-corpus-2026-09.md's "Ratified needles"
    // table (plan 09.1-02 Task 1), expressed as a small test-owned list — NOT yet asserted against
    // the live DeckStatClassifier.ProtectionOracleNeedles, which at RED time still holds only plan
    // 09.1-01's five needles. Plan 09.1-02 Task 3 adds a second, separate assertion once the table
    // is widened, projecting the real ProtectionOracleNeedles and asserting it exactly equals this
    // list; that second assertion is what legitimately stays red until Task 3 lands. Regenerate and
    // Ward are recorded with subject form "none" (no grammatically varying subject) and are excluded
    // from the pairing rule by that value, not by a name-based exception list.
    private static readonly IReadOnlyList<(string Effect, string SubjectForm)> RatifiedProtectionPairing =
    [
        ("hexproof", "singular"),       // gains hexproof
        ("hexproof", "plural"),         // gain hexproof
        ("hexproof", "singular"),       // has hexproof (Equipment/conditional status)
        ("indestructible", "singular"), // gains indestructible
        ("indestructible", "plural"),   // gain indestructible
        ("protection-from", "singular"),// gains protection from
        ("protection-from", "plural"),  // gain protection from
        ("phase-out", "singular"),      // phases out
        ("phase-out", "plural"),        // phase out
        ("shroud", "singular"),         // has shroud (Equipment/conditional status)
        ("shroud", "singular"),         // haste and shroud (Equipment/conditional status, conjunction-tail) — Lightning Greaves
        ("shroud", "plural"),           // have shroud (Equipment/conditional status)
        ("shroud", "singular"),         // gains shroud (temporary grant)
        ("shroud", "plural"),           // gain shroud (temporary grant)
        ("regenerate", "none"),         // regenerate target
        ("regenerate", "none"),         // regenerate this creature
        ("ward", "none"),               // ward—pay
    ];

    [Fact]
    public void RatifiedProtectionPairing_EveryPairableEffectHasBothSubjectForms()
    {
        IEnumerable<string> pairableEffects = RatifiedProtectionPairing
            .Where(entry => entry.SubjectForm != "none")
            .Select(entry => entry.Effect)
            .Distinct();

        foreach (string effect in pairableEffects)
        {
            bool hasSingular = RatifiedProtectionPairing.Any(entry => entry.Effect == effect && entry.SubjectForm == "singular");
            bool hasPlural = RatifiedProtectionPairing.Any(entry => entry.Effect == effect && entry.SubjectForm == "plural");

            Assert.True(hasSingular, $"Effect '{effect}' has no singular-subject needle in the ratified pairing list.");
            Assert.True(hasPlural, $"Effect '{effect}' has no plural-subject needle in the ratified pairing list.");
        }
    }

    // Why: this is the assertion Task 2 deferred — it proves the SHIPPED table matches what Task 1
    // ratified, by projecting the real DeckStatClassifier.ProtectionOracleNeedles and comparing it
    // against the test-owned expected list above. Both sides are sorted by (Effect, SubjectForm)
    // before comparing so the assertion checks content (every ratified (effect, subjectForm) pair
    // present with the right multiplicity), not incidental row order in either file.
    [Fact]
    public void ProtectionOracleNeedles_MatchesRatifiedPairing()
    {
        List<(string Effect, string SubjectForm)> actual = DeckStatClassifier.ProtectionOracleNeedles
            .Select(needle => (needle.Effect, needle.SubjectForm))
            .OrderBy(entry => entry.Effect, StringComparer.Ordinal)
            .ThenBy(entry => entry.SubjectForm, StringComparer.Ordinal)
            .ToList();

        List<(string Effect, string SubjectForm)> expected = RatifiedProtectionPairing
            .OrderBy(entry => entry.Effect, StringComparer.Ordinal)
            .ThenBy(entry => entry.SubjectForm, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, actual);
    }

    // Why: a needle that is listed in ProtectionOracleNeedles but unreachable through
    // IsProtectionCard is a defect — this asserts every row of the table actually classifies as
    // protection when supplied on its own as the whole oracle text.
    [Fact]
    public void IsProtectionCard_EveryProtectionOracleNeedle_ClassifiesAsProtection()
    {
        foreach (ProtectionNeedle needle in DeckStatClassifier.ProtectionOracleNeedles)
        {
            Assert.True(
                DeckStatClassifier.IsProtectionCard("Needle Test Card", needle.Text),
                $"Needle '{needle.Text}' (effect={needle.Effect}, subjectForm={needle.SubjectForm}) did not classify as protection.");
        }
    }

    // -----------------------------------------------------------------------
    // ParseManaToken
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("3", 3)]
    [InlineData("10", 10)]
    public void ParseManaToken_NumericReturnsValue(string token, int expected)
    {
        Assert.Equal(expected, DeckStatClassifier.ParseManaToken(token));
    }

    [Theory]
    [InlineData("X", 0)]
    [InlineData("x", 0)]
    public void ParseManaToken_X_ReturnsZero(string token, int expected)
    {
        Assert.Equal(expected, DeckStatClassifier.ParseManaToken(token));
    }

    [Theory]
    [InlineData("W/U", 1)]
    [InlineData("2/W", 1)]
    [InlineData("B/G", 1)]
    public void ParseManaToken_Hybrid_ReturnsOne(string token, int expected)
    {
        Assert.Equal(expected, DeckStatClassifier.ParseManaToken(token));
    }

    [Theory]
    [InlineData("W", 1)]
    [InlineData("U", 1)]
    [InlineData("B", 1)]
    [InlineData("R", 1)]
    [InlineData("G", 1)]
    [InlineData("C", 1)]
    public void ParseManaToken_ColoredSymbol_ReturnsOne(string token, int expected)
    {
        Assert.Equal(expected, DeckStatClassifier.ParseManaToken(token));
    }
}
