namespace DeckFlow.Core.Manabase;

/// <summary>
/// Turns a list of <see cref="CardFact"/> (Scryfall-shaped data) into a
/// <see cref="ManabaseDeck"/> ready for <see cref="ManabaseAnalyzer"/>. Applies Karsten's
/// source-counting rules: full-weight lands, mana dorks at 0.5, rocks at 0.75, basic
/// fetches in 3+ color decks at ~0.67, and land/spell MDFC backs at 0.8 (mythic 1.0).
/// </summary>
public static class ManabaseClassifier
{
    /// <summary>Build a <see cref="ManabaseDeck"/> from classified card facts.</summary>
    /// <param name="cards">All cards in the deck (including any commanders, flagged).</param>
    /// <param name="isSingleton">True for Commander/singleton; false for 60-card constructed.</param>
    public static ManabaseDeck Classify(IReadOnlyList<CardFact> cards, bool isSingleton = true)
    {
        ArgumentNullException.ThrowIfNull(cards);

        int deckColorCount = CountDeckColors(cards);

        var sources = new List<ManaSource>();
        var spells = new List<SpellRequirement>();
        int totalCards = 0;
        int commanderCount = 0;
        double mvSum = 0;
        int nonlandCount = 0;
        int rampUnderThree = 0;

        foreach (CardFact card in cards)
        {
            totalCards += card.Quantity;
            if (card.IsCommander)
            {
                commanderCount += card.Quantity;
            }

            bool frontIsLand = IsLandType(card.TypeLine);
            if (frontIsLand)
            {
                AddLandCopies(sources, card, deckColorCount);
                continue;
            }

            // Spell front: contributes to the curve.
            if (!card.IsCommander)
            {
                mvSum += card.ManaValue * card.Quantity;
                nonlandCount += card.Quantity;
            }

            ParsedManaCost cost = ManaCostParser.Parse(card.ManaCost);
            AddSpellRequirement(spells, card, cost);

            if (card.ManaValue <= 2 && IsRampOrDraw(card))
            {
                rampUnderThree += card.Quantity;
            }

            AddPartialSources(sources, card);
        }

        double avgMv = nonlandCount > 0 ? mvSum / nonlandCount : 0;

        return new ManabaseDeck
        {
            TotalCards = totalCards,
            CommanderCount = commanderCount,
            Sources = sources,
            Spells = spells,
            AverageManaValue = Math.Round(avgMv, 2),
            RampAndDrawUnderThree = rampUnderThree,
            IsSingleton = isSingleton,
        };
    }

    private static int CountDeckColors(IReadOnlyList<CardFact> cards)
    {
        // Deck color count = colors the deck actually demands (hard pips in card costs incl.
        // the commander). Off-color fixers (Signet, Birds, Treasures) must NOT inflate it,
        // or a 2-color deck reads as 5-color and the fetch-weighting heuristic over-penalizes.
        var colors = new HashSet<ManaColor>();
        foreach (CardFact card in cards)
        {
            foreach (KeyValuePair<ManaColor, int> pip in ManaCostParser.Parse(card.ManaCost).Pips)
            {
                if (pip.Value > 0 && pip.Key != ManaColor.Colorless)
                {
                    colors.Add(pip.Key);
                }
            }
        }

        return colors.Count;
    }

    private static void AddLandCopies(List<ManaSource> sources, CardFact card, int deckColorCount)
    {
        IReadOnlyList<ManaColor> produces = MapColors(card.ProducedMana);
        bool basicFetch = IsBasicFetch(card);
        // A choice-fetch in a 3+ color deck can only grab one color at a time.
        double weight = basicFetch && deckColorCount >= 3 ? 0.67 : 1.0;
        bool untapped = !EntersTapped(card);

        for (int i = 0; i < card.Quantity; i++)
        {
            sources.Add(new ManaSource
            {
                Name = card.Name,
                Produces = produces,
                Weight = weight,
                EntersUntapped = untapped,
            });
        }
    }

    private static void AddSpellRequirement(List<SpellRequirement> spells, CardFact card, ParsedManaCost cost)
    {
        bool hasColoredPip = cost.Pips.Any(p => p.Value > 0 && p.Key != ManaColor.Colorless);
        if (!hasColoredPip)
        {
            return;
        }

        // X/Y/Z spells: printed mana value is not the real cast turn, so an on-curve source
        // check at that turn is meaningless. Skip them rather than strain colors at a bogus turn.
        if (cost.HasVariableCost)
        {
            return;
        }

        spells.Add(new SpellRequirement
        {
            Name = card.Name,
            ManaValue = Math.Max(1, (int)Math.Round(card.ManaValue)),
            Pips = cost.Pips,
            IsGold = cost.DistinctColors >= 2,
        });
    }

    private static void AddPartialSources(List<ManaSource> sources, CardFact card)
    {
        // Land/spell MDFC back face: count as a partial colored source (0.8 / mythic 1.0).
        if (card.HasLandFace)
        {
            double mdfcWeight = IsMythic(card) ? 1.0 : 0.8;
            AddWeighted(sources, card, mdfcWeight);
            return;
        }

        if (card.ProducedMana.Count == 0 || !ProducesMana(card))
        {
            return;
        }

        // Mana dork (creature) ≈ 0.5; mana rock (artifact) ≈ 0.75.
        if (IsType(card.TypeLine, "Creature"))
        {
            AddWeighted(sources, card, 0.5);
        }
        else if (IsType(card.TypeLine, "Artifact"))
        {
            AddWeighted(sources, card, 0.75);
        }
    }

    private static void AddWeighted(List<ManaSource> sources, CardFact card, double weight)
    {
        IReadOnlyList<ManaColor> produces = MapColors(card.ProducedMana);
        if (produces.Count == 0)
        {
            return;
        }

        for (int i = 0; i < card.Quantity; i++)
        {
            sources.Add(new ManaSource { Name = card.Name, Produces = produces, Weight = weight, IsLand = false });
        }
    }

    private static IReadOnlyList<ManaColor> MapColors(IReadOnlyList<string> produced)
    {
        var colors = new List<ManaColor>();
        foreach (string letter in produced)
        {
            ManaColor? c = ManaCostParser.MapSymbol(letter.ToUpperInvariant());
            if (c is not null && !colors.Contains(c.Value))
            {
                colors.Add(c.Value);
            }
        }

        return colors;
    }

    private static bool IsLandType(string typeLine)
    {
        // Use the front face only (before "//") so MDFC spell-fronts aren't treated as lands.
        string front = typeLine.Split("//")[0];
        return IsType(front, "Land");
    }

    private static bool IsType(string typeLine, string type) =>
        typeLine.Contains(type, StringComparison.OrdinalIgnoreCase);

    private static bool ProducesMana(CardFact card) =>
        (card.OracleText?.Contains("Add ", StringComparison.OrdinalIgnoreCase) ?? false)
        || card.ProducedMana.Count > 0;

    private static bool IsBasicFetch(CardFact card)
    {
        string? text = card.OracleText;
        return text is not null
            && text.Contains("Search your library for a", StringComparison.OrdinalIgnoreCase)
            && text.Contains("basic land", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EntersTapped(CardFact card) =>
        card.OracleText?.Contains("enters the battlefield tapped", StringComparison.OrdinalIgnoreCase)
        ?? false;

    private static bool IsRampOrDraw(CardFact card)
    {
        string text = card.OracleText ?? string.Empty;
        bool ramp = (text.Contains("Search your library for", StringComparison.OrdinalIgnoreCase)
                && text.Contains("land", StringComparison.OrdinalIgnoreCase))
            || text.Contains("Add ", StringComparison.OrdinalIgnoreCase)
            || text.Contains("create a Treasure", StringComparison.OrdinalIgnoreCase);
        bool draw = text.Contains("draw a card", StringComparison.OrdinalIgnoreCase)
            || text.Contains("draw two cards", StringComparison.OrdinalIgnoreCase);
        return ramp || draw;
    }

    private static bool IsMythic(CardFact card) =>
        string.Equals(card.Rarity, "mythic", StringComparison.OrdinalIgnoreCase);
}
