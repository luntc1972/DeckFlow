using DeckFlow.Core.Reporting;

namespace DeckFlow.Core.Tests;

public sealed class CategorySuggestionReporterMergeTests
{
    [Fact]
    public void MergeWeighted_ReportsSourceCountAndTotal()
    {
        var merged = CategorySuggestionReporter.MergeWeighted(
            ["Card Draw"],
            ["Draw", "Ramp"],
            Array.Empty<string>(),
            ["Draw"]);

        Assert.Collection(
            merged,
            row =>
            {
                Assert.Equal("Draw", row.Category);
                Assert.Equal(3, row.SourceCount);
                Assert.Equal(3, row.SourceTotal);
            },
            row =>
            {
                Assert.Equal("Ramp", row.Category);
                Assert.Equal(1, row.SourceCount);
                Assert.Equal(3, row.SourceTotal);
            });
    }

    [Fact]
    public void MergeWeighted_SourceTotalCountsOnlyNonEmptyInputs()
    {
        var merged = CategorySuggestionReporter.MergeWeighted(
            ["Ramp"],
            ["3"],
            Array.Empty<string>(),
            ["Ramp", "Protection"]);

        Assert.Collection(
            merged,
            row =>
            {
                Assert.Equal("Ramp", row.Category);
                Assert.Equal(2, row.SourceCount);
                Assert.Equal(2, row.SourceTotal);
            },
            row =>
            {
                Assert.Equal("Protection", row.Category);
                Assert.Equal(1, row.SourceCount);
                Assert.Equal(2, row.SourceTotal);
            });
    }

    [Fact]
    public void Merge_DedupsAcrossSources_ReturnsSingleCategory()
    {
        var merged = CategorySuggestionReporter.MergeWeighted(
            ["Card Draw"],
            ["Draw"],
            Array.Empty<string>(),
            Array.Empty<string>())
            .Select(weight => weight.Category);

        Assert.Equal(["Draw"], merged);
    }

    [Fact]
    public void Merge_AgreementDiffers_HigherAgreementRanksFirst()
    {
        var merged = CategorySuggestionReporter.MergeWeighted(
            ["Ramp", "Protection"],
            ["Ramp"],
            ["Ramp"],
            Array.Empty<string>())
            .Select(weight => weight.Category);

        Assert.Equal(["Ramp", "Protection"], merged);
    }

    [Fact]
    public void Merge_CategoryAppearsInTagger_PrefersTaggerSpelling()
    {
        var merged = CategorySuggestionReporter.MergeWeighted(
            ["Card Draw"],
            Array.Empty<string>(),
            Array.Empty<string>(),
            ["Draw"])
            .Select(weight => weight.Category);

        Assert.Equal(["Draw"], merged);
    }

    [Fact]
    public void Merge_JunkCategoryPresent_ExcludesJunk()
    {
        var merged = CategorySuggestionReporter.MergeWeighted(
            ["WTF?", "Ramp"],
            ["3"],
            ["PUMP✊"],
            Array.Empty<string>())
            .Select(weight => weight.Category);

        Assert.Equal(["Ramp"], merged);
    }

    [Fact]
    public void Merge_SourceAuthorityBreaksSingleSourceTie()
    {
        var merged = CategorySuggestionReporter.MergeWeighted(
            Array.Empty<string>(),
            Array.Empty<string>(),
            ["Aardvark", "Beaver"],
            ["Zebra"])
            .Select(weight => weight.Category);

        Assert.Equal(["Zebra", "Aardvark", "Beaver"], merged);
    }

    [Fact]
    public void Merge_And_ToText_Unchanged()
    {
        var merged = CategorySuggestionReporter.MergeWeighted(
            ["Card Draw", "Ramp"],
            ["Draw", "Ramp"],
            Array.Empty<string>(),
            ["Draw"])
            .Select(weight => weight.Category);

        var text = CategorySuggestionReporter.ToText(merged, "Guardian Project");

        Assert.Equal(["Draw", "Ramp"], merged);
        Assert.Equal("- Draw" + Environment.NewLine + "- Ramp", text);
    }
}
