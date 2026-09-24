using DeckFlow.Web.Services.Harvest;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests CSV creation for the harvested-commanders export.</summary>
public sealed class CommandersListExportTests
{
    [Fact]
    public void BuildCsv_EmptyList_ReturnsOnlyHeader()
    {
        Assert.Equal("rank,commander,decks_categorized,last_processed_utc\n", CommandersListExport.BuildCsv(Array.Empty<HarvestedCommanderRow>()));
    }

    [Fact]
    public void BuildCsv_ThreeRows_PreservesGivenOrder()
    {
        var rows = new[] { Row("First"), Row("Second"), Row("Third") };
        var csv = CommandersListExport.BuildCsv(rows);
        Assert.Equal("rank,commander,decks_categorized,last_processed_utc\n1,\"First\",2,\n2,\"Second\",2,\n3,\"Third\",2,\n", csv);
    }

    [Fact]
    public void BuildCsv_Rows_AssignsRanksByListOrder()
    {
        var csv = CommandersListExport.BuildCsv(new[] { Row("A"), Row("B"), Row("C") });
        Assert.Contains("\n1,\"A\"", csv);
        Assert.Contains("\n2,\"B\"", csv);
        Assert.Contains("\n3,\"C\"", csv);
    }

    [Fact]
    public void BuildCsv_NameContainsQuote_EscapesQuote()
    {
        Assert.Contains("\"A \"\"Quote\"\"\"", CommandersListExport.BuildCsv(new[] { Row("A \"Quote\"") }));
    }

    [Fact]
    public void BuildCsv_NameContainsComma_RemainsSingleField()
    {
        Assert.Contains("\"A, B\"", CommandersListExport.BuildCsv(new[] { Row("A, B") }));
    }

    [Theory]
    [InlineData("=formula")]
    [InlineData("+formula")]
    [InlineData("-formula")]
    [InlineData("@formula")]
    public void BuildCsv_NameStartsFormulaCharacter_PrefixesApostrophe(string name)
    {
        Assert.Contains("\"'" + name + "\"", CommandersListExport.BuildCsv(new[] { Row(name) }));
    }

    [Fact]
    public void BuildCsv_NullLastProcessed_EmitsEmptyCell()
    {
        Assert.EndsWith(",\n", CommandersListExport.BuildCsv(new[] { Row("A", null) }), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCsv_OffsetLastProcessed_ConvertsToUtc()
    {
        Assert.Contains("2026-09-22T16:11:12Z", CommandersListExport.BuildCsv(new[] { Row("A", "2026-09-22T10:11:12-06:00") }));
    }

    [Fact]
    public void BuildCsv_OffsetlessLastProcessed_AssumesUtc()
    {
        Assert.Contains("2026-09-22T10:11:12Z", CommandersListExport.BuildCsv(new[] { Row("A", "2026-09-22 10:11:12") }));
    }

    [Fact]
    public void BuildCsv_UnparseableLastProcessed_PreservesStoredText()
    {
        Assert.Contains("\"unexpected date\"", CommandersListExport.BuildCsv(new[] { Row("A", "unexpected date") }));
    }

    [Fact]
    public void BuildCsv_NameContainsCommaAndQuote_HeaderAndDataHaveFourColumns()
    {
        var csv = CommandersListExport.BuildCsv(new[] { Row("A, \"B\"") });
        Assert.Equal(4, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Split(',').Length);
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)[1], ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)").Count + 1);
    }

    [Fact]
    public void BuildCsv_NullRows_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CommandersListExport.BuildCsv(null!));
    }

    private static HarvestedCommanderRow Row(string name, string? lastProcessedUtc = null) => new(name, 2, lastProcessedUtc);
}
