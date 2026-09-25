using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Tests.Knowledge;

public sealed class CommanderGridQueryTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData(" Éowyn ", "eowyn")]
    [InlineData("A\u0301", "a")]
    [InlineData("Æther", "æther")]
    public void Normalize_Value_ReturnsExpectedKey(string? value, string? expected)
    {
        Assert.Equal(expected, CommanderSearchKey.Normalize(value));
    }

    [Fact]
    public void Default_DefaultQuery_UsesDeckCountDescending()
    {
        var query = CommanderGridQuery.Default;

        Assert.Equal(CommanderSortColumn.DeckCount, query.SortBy);
        Assert.True(query.Descending);
        Assert.Null(query.SearchTerm);
    }

    [Theory]
    [InlineData(" name ", "name", "asc", CommanderSortColumn.Name, false)]
    [InlineData("term", "deck_count", "desc", CommanderSortColumn.DeckCount, true)]
    [InlineData("term", "last_processed", "asc", CommanderSortColumn.LastProcessed, false)]
    [InlineData("term", "unknown", "sideways", CommanderSortColumn.DeckCount, true)]
    public void FromRequest_Tokens_ReturnsCanonicalQuery(string? search, string? sortBy, string? sortDir, CommanderSortColumn expectedSort, bool expectedDescending)
    {
        var query = CommanderGridQuery.FromRequest(search, sortBy, sortDir);

        Assert.Equal(expectedSort, query.SortBy);
        Assert.Equal(expectedDescending, query.Descending);
        Assert.Equal(search?.Trim(), query.SearchTerm);
    }

    [Fact]
    public void FromRequest_OverlongSearch_TruncatesAtMaximumLength()
    {
        var query = CommanderGridQuery.FromRequest(new string('a', CommanderGridQuery.MaxSearchTermLength + 1), null, null);

        Assert.Equal(CommanderGridQuery.MaxSearchTermLength, query.SearchTerm!.Length);
    }

    [Fact]
    public void SqlPrefixPattern_Metacharacters_EscapesLiteralSearch()
    {
        var query = CommanderGridQuery.FromRequest("a\\b%_", null, null);

        Assert.Equal("a\\\\b\\%\\_%", query.SqlPrefixPattern);
    }

    [Fact]
    public void SqlPrefixPattern_CombiningMarksOnly_ReturnsNull()
    {
        var query = CommanderGridQuery.FromRequest("\u0301", null, null);

        Assert.Null(query.SqlPrefixPattern);
    }

    [Fact]
    public void CacheToken_NullAndLiteralHyphen_AreDistinct()
    {
        var defaultQuery = CommanderGridQuery.Default;
        var hyphenQuery = CommanderGridQuery.FromRequest("-", null, null);

        Assert.NotEqual(defaultQuery.CacheToken, hyphenQuery.CacheToken);
    }

    [Theory]
    [InlineData("deck_count", "desc", CommanderSortColumn.Name, "asc")]
    [InlineData("last_processed", "asc", CommanderSortColumn.Name, "asc")]
    [InlineData("name", "asc", CommanderSortColumn.DeckCount, "desc")]
    [InlineData("last_processed", "desc", CommanderSortColumn.DeckCount, "desc")]
    [InlineData("deck_count", "desc", CommanderSortColumn.LastProcessed, "desc")]
    [InlineData("name", "asc", CommanderSortColumn.LastProcessed, "desc")]
    public void NextDirectionToken_InactiveColumn_ReturnsColumnFirstDirection(string sortBy, string sortDir, CommanderSortColumn clicked, string expected)
    {
        var query = CommanderGridQuery.FromRequest(null, sortBy, sortDir);

        Assert.Equal(expected, query.NextDirectionToken(clicked));
    }

    [Theory]
    [InlineData("name", "asc", CommanderSortColumn.Name, "desc")]
    [InlineData("name", "desc", CommanderSortColumn.Name, "asc")]
    [InlineData("deck_count", "desc", CommanderSortColumn.DeckCount, "asc")]
    [InlineData("deck_count", "asc", CommanderSortColumn.DeckCount, "desc")]
    [InlineData("last_processed", "desc", CommanderSortColumn.LastProcessed, "asc")]
    [InlineData("last_processed", "asc", CommanderSortColumn.LastProcessed, "desc")]
    public void NextDirectionToken_ActiveColumn_ReversesDirection(string sortBy, string sortDir, CommanderSortColumn clicked, string expected)
    {
        var query = CommanderGridQuery.FromRequest(null, sortBy, sortDir);

        Assert.Equal(expected, query.NextDirectionToken(clicked));
    }
}
