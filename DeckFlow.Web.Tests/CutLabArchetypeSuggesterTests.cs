using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for composition-based Cut Lab archetype suggestions.</summary>
public sealed class CutLabArchetypeSuggesterTests
{
    [Fact]
    public void Suggest_Archetype_TurboCombo_WhenThresholdMet()
    {
        var result = Suggest(TurboCards(), Classification(comboNames: ["Tutor 1"]));

        Assert.Equal("turbo-combo", result.Slug);
        Assert.Equal(CutLabArchetypeConfidence.High, result.Confidence);
    }

    [Fact]
    public void Suggest_Archetype_Stax_WhenThresholdMet()
    {
        var result = Suggest(Cards(5, categories: ["stax"]));

        Assert.Equal("stax", result.Slug);
    }

    [Fact]
    public void Suggest_Archetype_SpellslingerStorm_WhenThresholdMet()
    {
        var result = Suggest(Cards(30, typeLine: "Instant"));

        Assert.Equal("spellslinger-storm", result.Slug);
    }

    [Theory]
    [InlineData(1, "1 Storm card.")]
    [InlineData(2, "2 Storm cards.")]
    public void Suggest_Archetype_StormReason_UsesCorrectPlural(int count, string expectedReason)
    {
        var cards = Enumerable.Range(1, count)
            .Select(index => Card($"Storm {index}") with
            {
                SemanticProfile = new CutLabSemanticProfile(null, null, null, null, null, ["Storm"], null, null, null),
            })
            .ToArray();

        var result = Suggest(cards);

        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public void Suggest_Archetype_AggroVoltron_WhenThresholdMet()
    {
        var result = Suggest(Cards(10, typeLine: "Artifact — Equipment"));

        Assert.Equal("aggro-voltron", result.Slug);
    }

    [Fact]
    public void Suggest_Archetype_Control_WhenThresholdMet()
    {
        var result = Suggest(Cards(20, roles: ["interaction-targeted"]));

        Assert.Equal("control", result.Slug);
    }

    [Fact]
    public void Suggest_Archetype_MidrangeCombo_WhenThresholdMet()
    {
        var result = Suggest([Card("Combo")], Classification(comboNames: ["Combo"]));

        Assert.Equal("midrange-combo", result.Slug);
    }

    [Fact]
    public void Suggest_Archetype_ValueEngine_WhenThresholdMet()
    {
        var result = Suggest([Card("Value")]);

        Assert.Equal("value-engine", result.Slug);
        Assert.Equal(CutLabArchetypeConfidence.Low, result.Confidence);
    }

    [Fact]
    public void Suggest_Archetype_TurboBeatsStax_WhenBothMatch()
    {
        var result = Suggest([.. TurboCards(), .. Cards(5, categories: ["stax"])], Classification(comboNames: ["Tutor 1"]));

        Assert.Equal("turbo-combo", result.Slug);
    }

    [Fact]
    public void Suggest_Archetype_StaxAtFourPieces_FallsThrough()
    {
        var result = Suggest(Cards(4, categories: ["stax"]));

        Assert.Equal("value-engine", result.Slug);
    }

    [Fact]
    public void Suggest_Archetype_TurboAverageManaValue_ExcludesLandsAndCommander()
    {
        var cards = TurboCards().Append(Card("Land", 10, true)).Append(Card("Commander", 10, isCommander: true)).ToArray();
        var result = Suggest(cards, Classification(comboNames: ["Tutor 1"]));

        Assert.Equal("turbo-combo", result.Slug);
    }

    [Fact]
    public void Suggest_Archetype_QuantityWeighted()
    {
        var result = Suggest([Card("Stax A", categories: ["stax"], quantity: 4), Card("Stax B", categories: ["stax"])]);

        Assert.Equal("stax", result.Slug);
    }

    [Fact]
    public void Suggest_Archetype_ComboDataUnavailable_SkipsComboRulesLowConfidence()
    {
        var result = Suggest(TurboCards(), Classification(comboDataAvailable: false, comboNames: ["Tutor 1"]));

        Assert.Equal("value-engine", result.Slug);
        Assert.Equal(CutLabArchetypeConfidence.Low, result.Confidence);
        Assert.Contains("Combo data was unavailable", result.Reason);
    }

    [Fact]
    public void Suggest_Archetype_CategoryDataUnavailable_SkipsTutorAndStaxRules()
    {
        var result = Suggest([.. TurboCards(), .. Cards(5, categories: ["stax"])], Classification(categoryDataAvailable: false, comboNames: ["Tutor 1"]));

        Assert.Equal("midrange-combo", result.Slug);
        Assert.Equal(CutLabArchetypeConfidence.Low, result.Confidence);
    }

    [Fact]
    public void Suggest_Archetype_EmptyPool_ReturnsValueEngineLow()
    {
        var result = Suggest([]);

        Assert.Equal("value-engine", result.Slug);
        Assert.Equal(CutLabArchetypeConfidence.Low, result.Confidence);
    }

    private static CutLabArchetypeSuggestion Suggest(IReadOnlyList<CutLabAnalyzedCard> cards, CutLabClassificationContext? classification = null)
        => CutLabArchetypeSuggester.Suggest(cards, classification ?? Classification(), []);

    private static CutLabAnalyzedCard[] TurboCards()
        => Enumerable.Range(1, 8).Select(index => Card($"Tutor {index}", 1, roles: ["ramp"], categories: ["tutor"])).ToArray();

    private static CutLabAnalyzedCard[] Cards(int count, IReadOnlyList<string>? roles = null, IReadOnlyList<string>? categories = null, string typeLine = "")
        => Enumerable.Range(1, count).Select(index => Card($"Card {index}", roles: roles, categories: categories, typeLine: typeLine)).ToArray();

    private static CutLabAnalyzedCard Card(string name, double manaValue = 1, bool isLand = false, IReadOnlyList<string>? roles = null, IReadOnlyList<string>? categories = null, string typeLine = "", bool isCommander = false, int quantity = 1)
        => new(name, manaValue, isLand, roles ?? [], categories ?? []) { TypeLine = typeLine, IsCommander = isCommander, Quantity = quantity };

    private static CutLabClassificationContext Classification(bool comboDataAvailable = true, bool categoryDataAvailable = true, params string[] comboNames)
    {
        Dictionary<string, CutLabCardComboMembership> membership = new(CutLabCardNames.Comparer);
        foreach (string cardName in comboNames)
        {
            SpellbookCombo combo = new([cardName], ["Win"], "Win the game.");
            membership[CutLabCardNames.Normalize(cardName)] = new CutLabCardComboMembership([combo], []);
        }

        return new CutLabClassificationContext(
            [],
            comboDataAvailable,
            categoryDataAvailable,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            membership);
    }
}
