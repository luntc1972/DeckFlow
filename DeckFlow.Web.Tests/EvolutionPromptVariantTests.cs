using DeckFlow.Core.History;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Evolution;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class EvolutionPromptVariantTests
{
    private static DeckHistoryFile History() => new()
    {
        DeckName = "Tivit Ad Nauseam",
        Versions =
        [
            new DeckSnapshot
            {
                Id = 1,
                Date = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
                Commander = ["Tivit, Seller of Secrets"],
                Cards = [new SnapshotCard { Name = "Sol Ring", Qty = 1 }],
                Delta = new SnapshotDelta(),
            },
            new DeckSnapshot
            {
                Id = 2,
                Date = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
                Notes = "Cut nothing, added Remora.",
                Commander = ["Tivit, Seller of Secrets"],
                Cards = [new SnapshotCard { Name = "Sol Ring", Qty = 1 }, new SnapshotCard { Name = "Mystic Remora", Qty = 1 }],
                Delta = new SnapshotDelta { Adds = [new SnapshotCard { Name = "Mystic Remora", Qty = 1 }] },
            },
        ],
    };

    // AiPlatform.Normalize is case-sensitive ("ChatGPT"/"Claude"/"Gemini"; anything else
    // falls back to Default) — use the exact keys or the tests silently all hit ChatGPT.
    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_ContainsHeaderTimelineAndBothFullLists(string platformKey)
    {
        var registry = new EvolutionPromptVariantRegistry(
        [
            new ChatGptEvolutionPromptVariant(),
            new ClaudeEvolutionPromptVariant(),
            new GeminiEvolutionPromptVariant(),
        ]);

        var prompt = registry.Build(AiPlatform.Normalize(platformKey), History());

        Assert.Contains("Tivit Ad Nauseam", prompt);
        Assert.Contains("VERSION 1", prompt);
        Assert.Contains("LATEST — VERSION 2", prompt);
        Assert.Contains("Mystic Remora", prompt);
        Assert.Contains("Cut nothing, added Remora.", prompt);
        Assert.Contains("Commander: Tivit, Seller of Secrets", prompt);
    }

    [Fact]
    public void Build_EachPlatform_UsesItsOwnVariant()
    {
        // Distinguishing framing: only the ChatGPT variant carries EXECUTE NOW.
        var claude = new ClaudeEvolutionPromptVariant().Build(History());
        var gemini = new GeminiEvolutionPromptVariant().Build(History());

        Assert.Equal(AiPlatform.Claude, new ClaudeEvolutionPromptVariant().Platform);
        Assert.Equal(AiPlatform.Gemini, new GeminiEvolutionPromptVariant().Platform);
        Assert.DoesNotContain("EXECUTE NOW", claude);
        Assert.DoesNotContain("EXECUTE NOW", gemini);
    }

    [Fact]
    public void Build_ChatGptVariant_CarriesExecuteNowFraming()
    {
        var prompt = new ChatGptEvolutionPromptVariant().Build(History());
        Assert.Contains("EXECUTE NOW", prompt);
    }

    [Fact]
    public void RenderHistoryBody_SingleVersion_HasOnlyOneFullList()
    {
        var single = History() with { Versions = [History().Versions[0]] };
        var body = EvolutionHistoryRenderer.RenderHistoryBody(single);

        Assert.Contains("VERSION 1", body);
        Assert.DoesNotContain("LATEST", body);
    }
}
