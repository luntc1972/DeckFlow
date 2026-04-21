using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class AnalysisQuestionCatalogTests
{
    [Fact]
    public void ResolveTexts_KeepsPlaceholder_WhenCardNamesMissing()
    {
        var result = AnalysisQuestionCatalog.ResolveTexts(["card-worth-it"], (IEnumerable<string>?)null, null);

        var question = Assert.Single(result);
        Assert.Equal("Is [card] worth including in this deck?", question);
    }

    [Fact]
    public void ResolveTexts_ExpandsPlaceholderQuestions_PerCard_AndLeavesOtherQuestionsSingle()
    {
        var result = AnalysisQuestionCatalog.ResolveTexts(
            ["strengths-weaknesses", "card-worth-it", "better-alternatives"],
            ["Sol Ring", "Arcane Signet"],
            null);

        Assert.Equal(
            [
                "What are the strengths and weaknesses of this deck?",
                "Is Sol Ring worth including in this deck?",
                "Is Arcane Signet worth including in this deck?",
                "What are better alternatives to Sol Ring?",
                "What are better alternatives to Arcane Signet?"
            ],
            result);
    }

    [Fact]
    public void ResolveTexts_PreservesCardInputOrder()
    {
        var result = AnalysisQuestionCatalog.ResolveTexts(
            ["better-alternatives"],
            ["Cyclonic Rift", "Sol Ring", "Arcane Signet"],
            null);

        Assert.Equal(
            [
                "What are better alternatives to Cyclonic Rift?",
                "What are better alternatives to Sol Ring?",
                "What are better alternatives to Arcane Signet?"
            ],
            result);
    }

    [Fact]
    public void ResolveTexts_NormalizesWhitespaceAndDuplicates_CaseInsensitively()
    {
        var result = AnalysisQuestionCatalog.ResolveTexts(
            ["card-worth-it"],
            ["  Sol Ring  ", "sol ring", " Arcane Signet "],
            "50");

        Assert.Equal(
            [
                "Is Sol Ring worth including in this deck?",
                "Is Arcane Signet worth including in this deck?"
            ],
            result);
    }
}
