using DeckFlow.Core.Analysis;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Suggests a deck archetype from available Cut Lab composition evidence.</summary>
public static class CutLabArchetypeSuggester
{
    /// <summary>Suggests the first matching archetype in specification priority order.</summary>
    /// <param name="cards">Analyzed cards in the current deck pool.</param>
    /// <param name="classification">Available category and combo classifications.</param>
    /// <param name="availableThemes">Available EDHREC commander themes.</param>
    /// <returns>A deterministic archetype suggestion.</returns>
    public static CutLabArchetypeSuggestion Suggest(
        IReadOnlyList<CutLabAnalyzedCard> cards,
        CutLabClassificationContext classification,
        IReadOnlyList<CutLabCommanderTheme> availableThemes)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(availableThemes);

        IReadOnlyList<CutLabAnalyzedCard> nonCommanderCards = cards.Where(card => !card.IsCommander).ToArray();
        // Why: fast mana here deliberately means non-land ramp with mana value <= 1 (Sol Ring, mana dorks, rituals), not DeckStatClassifier.IsFastManaCard (zero-MV mana artifacts only), because the turbo-combo fastMana >= 8 threshold depends on this broader definition.
        int fastMana = Count(nonCommanderCards, card => !card.IsLand && HasRole(card, "ramp") && card.ManaValue <= 1);
        int tutors = classification.CategoryDataAvailable ? Count(nonCommanderCards, card => HasCategory(card, "tutor")) : 0;
        int staxPieces = classification.CategoryDataAvailable ? CountStax(nonCommanderCards) : 0;
        int stormCards = Count(nonCommanderCards, card => card.SemanticProfile?.Keywords?.Contains("Storm", StringComparer.OrdinalIgnoreCase) == true);
        int spells = Count(nonCommanderCards, card => HasType(card, "Instant") || HasType(card, "Sorcery"));
        int voltronCards = Count(nonCommanderCards, card => HasType(card, "Equipment") || HasType(card, "Aura"));
        int interaction = Count(nonCommanderCards, card => HasRole(card, "interaction-targeted") || HasRole(card, "interaction-mass"));
        int creatures = Count(nonCommanderCards, card => HasType(card, "Creature"));
        bool comboPresent = classification.ComboDataAvailable && HasCompleteCombo(nonCommanderCards, classification);
        bool voltronTheme = availableThemes.Any(theme => theme.Slug.Contains("voltron", StringComparison.OrdinalIgnoreCase));
        double? averageManaValue = AverageManaValue(nonCommanderCards);
        // Why: T5 ranks turbo-combo first when combo, tutor, fast-mana, and average-MV thresholds all match.
        if (classification.ComboDataAvailable
            && classification.CategoryDataAvailable
            && comboPresent
            && tutors >= 8
            && fastMana >= 8
            && averageManaValue is <= 2.5)
        {
            return Create("turbo-combo", $"combo present, {tutors} tutors, {fastMana} fast mana, average mana value {averageManaValue.Value:0.0}.", classification);
        }

        // Why: T5 classifies five or more stax pieces as stax after turbo-combo has priority.
        if (classification.CategoryDataAvailable && staxPieces >= 5)
        {
            return Create("stax", $"{staxPieces} stax pieces.", classification);
        }

        // Why: T5 classifies thirty instants/sorceries or one Storm card as spellslinger-storm.
        if (spells >= 30 || stormCards >= 1)
        {
            return Create("spellslinger-storm", stormCards >= 1 ? $"{stormCards} Storm {(stormCards == 1 ? "card" : "cards")}." : $"{spells} instants and sorceries.", classification);
        }

        // Why: T5 classifies ten equipment/aura cards or an EDHREC voltron theme as aggro-voltron.
        if (voltronCards >= 10 || voltronTheme)
        {
            return Create("aggro-voltron", voltronTheme ? "Voltron EDHREC theme." : $"{voltronCards} equipment and auras.", classification);
        }

        // Why: T5 classifies at least twenty interaction cards and at most fifteen creatures as control.
        if (interaction >= 20 && creatures <= 15)
        {
            return Create("control", $"{interaction} interaction cards and {creatures} creature{(creatures == 1 ? string.Empty : "s")}.", classification);
        }

        // Why: T5 classifies any complete combo as midrange-combo after more specific patterns.
        if (classification.ComboDataAvailable && comboPresent)
        {
            return Create("midrange-combo", "combo present.", classification);
        }

        // Why: T5 specifies value-engine as the low-confidence fallback when no stronger pattern matches.
        return Create("value-engine", "no stronger pattern found.", classification, fallback: true);
    }

    private static CutLabArchetypeSuggestion Create(
        string slug,
        string reason,
        CutLabClassificationContext classification,
        bool fallback = false)
    {
        bool lowConfidence = fallback || !classification.ComboDataAvailable || !classification.CategoryDataAvailable;
        if (!classification.ComboDataAvailable)
        {
            reason = $"{reason} Combo data was unavailable, so combo archetypes were not checked.";
        }

        if (!classification.CategoryDataAvailable)
        {
            reason = $"{reason} Category data was unavailable, so tutor and stax archetypes were not checked.";
        }

        return new CutLabArchetypeSuggestion(slug, lowConfidence ? CutLabArchetypeConfidence.Low : CutLabArchetypeConfidence.High, reason);
    }

    private static int Count(IEnumerable<CutLabAnalyzedCard> cards, Func<CutLabAnalyzedCard, bool> predicate)
        => cards.Where(predicate).Sum(card => Math.Max(0, card.Quantity));

    private static int CountStax(IReadOnlyList<CutLabAnalyzedCard> cards)
    {
        if (!DeckPlanStrategyCatalog.TryGetBySlug("stax", out DeckPlanStrategyEntry stax))
        {
            return 0;
        }

        return Count(cards, card => DeckPlanStrategyCatalog.MatchesCategories(stax, card.Categories));
    }

    private static bool HasCompleteCombo(IReadOnlyList<CutLabAnalyzedCard> cards, CutLabClassificationContext classification)
        => cards.Any(card => classification.CardComboMembership.TryGetValue(CutLabCardNames.Normalize(card.Name), out CutLabCardComboMembership? membership)
            && membership.CompleteCombos.Count > 0);

    private static bool HasRole(CutLabAnalyzedCard card, string role)
        => card.Roles.Contains(role, StringComparer.Ordinal);

    private static bool HasCategory(CutLabAnalyzedCard card, string category)
        => card.Categories.Any(value => value.Contains(category, StringComparison.OrdinalIgnoreCase));

    private static bool HasType(CutLabAnalyzedCard card, string type)
        => card.TypeLine.Contains(type, StringComparison.OrdinalIgnoreCase);

    private static double? AverageManaValue(IReadOnlyList<CutLabAnalyzedCard> cards)
    {
        IReadOnlyList<CutLabAnalyzedCard> nonLands = cards.Where(card => !card.IsLand && card.Quantity > 0).ToArray();
        int quantity = nonLands.Sum(card => card.Quantity);
        return quantity == 0 ? null : nonLands.Sum(card => card.ManaValue * card.Quantity) / quantity;
    }
}
