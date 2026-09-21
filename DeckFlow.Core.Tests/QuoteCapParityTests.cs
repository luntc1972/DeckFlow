using System.Text.Json;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Verifies quote counting parity with the plan's cross-language fixture contract.
/// </summary>
public sealed class QuoteCapParityTests
{
    [Fact]
    public void QuotedWordCount_AtCapDoubleStraight_MatchesReference() => AssertFixture(0);
    [Fact]
    public void QuotedWordCount_OneOverStraight_MatchesReference() => AssertFixture(1);
    [Fact]
    public void QuotedWordCount_CurlyDouble_MatchesReference() => AssertFixture(2);
    [Fact]
    public void QuotedWordCount_StraightSinglePair_MatchesReference() => AssertFixture(3);
    [Fact]
    public void QuotedWordCount_CurlySinglePair_MatchesReference() => AssertFixture(4);
    [Fact]
    public void QuotedWordCount_InternalStraightQuote_MatchesReference() => AssertFixture(5);
    [Fact]
    public void QuotedWordCount_InternalSingleQuote_MatchesReference() => AssertFixture(6);
    [Fact]
    public void QuotedWordCount_ApostrophesOnly_MatchesReference() => AssertFixture(7);
    [Fact]
    public void QuotedWordCount_PunctuationAdjacent_MatchesReference() => AssertFixture(8);
    [Fact]
    public void QuotedWordCount_ShortSpan_MatchesReference() => AssertFixture(9);
    [Fact]
    public void QuotedWordCount_TwoSpans_MatchesReference() => AssertFixture(10);
    [Fact]
    public void QuotedWordCount_EmbeddedNewline_MatchesReference() => AssertFixture(11);
    [Fact]
    public void QuotedWordCount_LeadingApostropheYear_MatchesReference() => AssertFixture(12);
    [Fact]
    public void QuotedWordCount_UnclosedDouble_MatchesReference() => AssertFixture(13);
    [Fact]
    public void QuotedWordCount_DecadeThenSinglePair_MatchesReference() => AssertFixture(14);

    private static void AssertFixture(int index)
    {
        var fixture = LoadFixtures()[index];
        Assert.Equal(fixture.QuotedWords, DistillationValidation.QuotedWordCount(fixture.Text));
    }

    private static IReadOnlyList<QuoteFixture> LoadFixtures()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "quote-cap-fixtures.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<IReadOnlyList<QuoteFixture>>(json)
            ?? throw new InvalidOperationException("Quote cap fixtures could not be deserialized.");
    }

    private sealed record QuoteFixture(string Name, string Text, int QuotedWords);
}
