namespace DeckFlow.Core.Analysis;

/// <summary>
/// Aggregates combo-lookup input into a ranked, banded <see cref="WinConMap"/>.
/// </summary>
public static class WinConMapAggregator
{
    /// <summary>Combo band threshold: mana value needed at or below this is <see cref="WinConBand.Early"/>.</summary>
    private const int EarlyBandMaxManaValue = 4;

    /// <summary>Combo band threshold: mana value needed at or below this (and above the Early threshold) is <see cref="WinConBand.Mid"/>.</summary>
    private const int MidBandMaxManaValue = 7;

    /// <summary>
    /// Computes the deck's win-condition / combo map: ranks + bands included combos, keeps
    /// one-card-away near-combos strictly separate, counts assembly paths, lists closing-power
    /// cards, and sets the combo-data-availability sentinel.
    /// </summary>
    /// <param name="combos">Included combos found for the deck.</param>
    /// <param name="nearCombos">One-card-away near-combos found for the deck.</param>
    /// <param name="closingCards">Candidate closing-power cards to classify.</param>
    /// <param name="comboDataAvailable"><see langword="true"/> when combo lookup ran (even if it found nothing); <see langword="false"/> when lookup failed/was unavailable.</param>
    public static WinConMap Compute(
        IReadOnlyList<WinConComboInput> combos,
        IReadOnlyList<WinConNearComboInput> nearCombos,
        IEnumerable<WinConClosingCardInput> closingCards,
        bool comboDataAvailable)
    {
        ArgumentNullException.ThrowIfNull(combos);
        ArgumentNullException.ThrowIfNull(nearCombos);
        ArgumentNullException.ThrowIfNull(closingCards);

        var closingList = new List<WinConClosingCard>();
        foreach (var card in closingCards)
        {
            if (card.Quantity <= 0)
            {
                continue;
            }

            var typeLine = card.TypeLine ?? string.Empty;
            var oracleText = card.OracleText ?? string.Empty;
            if (DeckStatClassifier.IsClosingPowerCard(typeLine, oracleText))
            {
                closingList.Add(new WinConClosingCard(card.Name, card.Quantity));
            }
        }

        if (!comboDataAvailable)
        {
            // Combo lookup failed/unavailable: no combos or near-combos to report, but the
            // closing-power read still stands — a combo-less/unavailable deck still gets a
            // win-condition read from its non-combo closers.
            return new WinConMap(
                Array.Empty<WinConCombo>(),
                Array.Empty<WinConNearCombo>(),
                0,
                closingList,
                false,
                WinConBand.Unknown);
        }

        // Rank: low ManaValueNeeded first (null last), then high Popularity first (null lowest),
        // then normalized joined CardNames via ordinal comparison as a FINAL deterministic
        // tie-breaker so equal-MV/equal-Popularity combos are input-order-independent — do NOT
        // rely on LINQ OrderBy stability alone. The join key is built from a TRIMMED,
        // case-insensitively RE-ORDERED copy of each combo's card names (never the display
        // CardNames itself) so two equal combos whose names arrive in a different intra-combo
        // order or casing still sort identically (Codex LOW finding #4).
        var rankedCombos = combos
            .OrderBy(c => c.ManaValueNeeded ?? int.MaxValue)
            .ThenByDescending(c => c.Popularity ?? -1)
            .ThenBy(c => string.Join("|", NormalizedTieBreakNames(c.CardNames)), StringComparer.Ordinal)
            .Select(c => new WinConCombo(c.CardNames, c.Results, c.ManaValueNeeded, c.Popularity, BandFor(c.ManaValueNeeded)))
            .ToList();

        // rankedCombos is already sorted ascending by ManaValueNeeded (null sorts last), so the
        // first entry's ManaValueNeeded is the fastest — unless it's null, which only happens
        // when EVERY combo has a null ManaValueNeeded (Unknown is then correct).
        var fastestManaValue = rankedCombos.Count > 0 ? rankedCombos[0].ManaValueNeeded : null;
        var overallBand = BandFor(fastestManaValue);

        var nearCombosList = nearCombos
            .Select(n => new WinConNearCombo(n.MissingCard, n.CardsInDeck, n.Results))
            .ToList();

        return new WinConMap(
            rankedCombos,
            nearCombosList,
            rankedCombos.Count,
            closingList,
            true,
            overallBand);
    }

    /// <summary>
    /// Maps a combo's mana value needed to a coarse assembly-speed band. Never returns a turn number.
    /// </summary>
    /// <param name="manaValueNeeded">Total mana value needed to assemble the combo, when known.</param>
    private static WinConBand BandFor(int? manaValueNeeded)
    {
        if (manaValueNeeded is null)
        {
            return WinConBand.Unknown;
        }

        if (manaValueNeeded.Value <= EarlyBandMaxManaValue)
        {
            return WinConBand.Early;
        }

        if (manaValueNeeded.Value <= MidBandMaxManaValue)
        {
            return WinConBand.Mid;
        }

        return WinConBand.Late;
    }

    /// <summary>
    /// Builds a normalized card-name sequence for the tie-break sort key ONLY — trims each name and
    /// orders the names within the combo case-insensitively so equivalent combos whose card names
    /// arrive in a different intra-combo order or casing sort identically. Never mutates the
    /// displayed <see cref="WinConCombo.CardNames"/> order on the output record.
    /// </summary>
    /// <param name="cardNames">The combo's card names in their original (display) order.</param>
    private static IEnumerable<string> NormalizedTieBreakNames(IReadOnlyList<string> cardNames)
        => cardNames
            .Select(name => name.Trim())
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
}
