using DeckFlow.Core.Reporting;

namespace DeckFlow.Core.Tests;

public sealed class CategorySuggestionReporterMergeTests
{
    [Fact]
    public void Merge_DedupsAcrossSources_ReturnsSingleCategory()
    {
        var merged = CategorySuggestionReporter.Merge(
            ["Card Draw"],
            ["Draw"],
            Array.Empty<string>(),
            Array.Empty<string>());

        Assert.Equal(["Draw"], merged);
    }

    [Fact]
    public void Merge_AgreementDiffers_HigherAgreementRanksFirst()
    {
        var merged = CategorySuggestionReporter.Merge(
            ["Ramp", "Protection"],
            ["Ramp"],
            ["Ramp"],
            Array.Empty<string>());

        Assert.Equal(["Ramp", "Protection"], merged);
    }

    [Fact]
    public void Merge_CategoryAppearsInTagger_PrefersTaggerSpelling()
    {
        var merged = CategorySuggestionReporter.Merge(
            ["Card Draw"],
            Array.Empty<string>(),
            Array.Empty<string>(),
            ["Draw"]);

        Assert.Equal(["Draw"], merged);
    }

    [Fact]
    public void Merge_JunkCategoryPresent_ExcludesJunk()
    {
        var merged = CategorySuggestionReporter.Merge(
            ["WTF?", "Ramp"],
            ["3"],
            ["PUMP✊"],
            Array.Empty<string>());

        Assert.Equal(["Ramp"], merged);
    }
}
