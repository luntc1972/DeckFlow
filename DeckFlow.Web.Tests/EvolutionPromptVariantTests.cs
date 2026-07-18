using DeckFlow.Core.History;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Evolution;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class EvolutionPromptVariantTests
{
    private static readonly IReadOnlyList<EvolutionCardReference> References =
    [
        new("Mystic Remora", "{U}", "Enchantment", "Cumulative upkeep {1}."),
        new("Sol Ring", "{1}", "Artifact", "{T}: Add {C}{C}."),
    ];

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

        var prompt = registry.Build(AiPlatform.Normalize(platformKey), History(), null);

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
        var claude = new ClaudeEvolutionPromptVariant().Build(History(), null);
        var gemini = new GeminiEvolutionPromptVariant().Build(History(), null);

        Assert.Equal(AiPlatform.Claude, new ClaudeEvolutionPromptVariant().Platform);
        Assert.Equal(AiPlatform.Gemini, new GeminiEvolutionPromptVariant().Platform);
        Assert.DoesNotContain("EXECUTE NOW", claude);
        Assert.DoesNotContain("EXECUTE NOW", gemini);
    }

    [Fact]
    public void Build_ChatGptVariant_CarriesExecuteNowFraming()
    {
        var prompt = new ChatGptEvolutionPromptVariant().Build(History(), null);
        Assert.Contains("EXECUTE NOW", prompt);
    }

    [Theory]
    [InlineData("ChatGPT", "CARD REFERENCE", "Name: Mystic Remora", "Oracle Text: Cumulative upkeep {1}.")]
    [InlineData("Claude", "<card_reference>", "<name>Mystic Remora</name>", "<oracle_text>Cumulative upkeep {1}.</oracle_text>")]
    [InlineData("Gemini", "## CARD REFERENCE", "Name: Mystic Remora", "Oracle Text: Cumulative upkeep {1}.")]
    public void Build_WithReferences_RendersPlatformSpecificReferenceSection(
        string platformKey,
        string sectionMarker,
        string nameMarker,
        string oracleMarker)
    {
        var registry = new EvolutionPromptVariantRegistry(
        [
            new ChatGptEvolutionPromptVariant(),
            new ClaudeEvolutionPromptVariant(),
            new GeminiEvolutionPromptVariant(),
        ]);

        var prompt = registry.Build(AiPlatform.Normalize(platformKey), History(), References);

        Assert.Contains(sectionMarker, prompt);
        Assert.Contains(nameMarker, prompt);
        Assert.Contains(oracleMarker, prompt);
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_WithoutReferences_OmitsReferenceSection(string platformKey)
    {
        var registry = new EvolutionPromptVariantRegistry(
        [
            new ChatGptEvolutionPromptVariant(),
            new ClaudeEvolutionPromptVariant(),
            new GeminiEvolutionPromptVariant(),
        ]);

        var promptWithoutReferences = registry.Build(AiPlatform.Normalize(platformKey), History(), null);
        var promptWithEmptyReferences = registry.Build(AiPlatform.Normalize(platformKey), History(), []);

        Assert.DoesNotContain("CARD REFERENCE", promptWithoutReferences);
        Assert.DoesNotContain("CARD REFERENCE", promptWithEmptyReferences);
        Assert.DoesNotContain("<card_reference>", promptWithoutReferences);
        Assert.DoesNotContain("<card_reference>", promptWithEmptyReferences);
    }

    [Fact]
    public void RenderHistoryBody_SingleVersion_HasOnlyOneFullList()
    {
        var single = History() with { Versions = [History().Versions[0]] };
        var body = EvolutionHistoryRenderer.RenderHistoryBody(single);

        Assert.Contains("VERSION 1", body);
        Assert.DoesNotContain("LATEST", body);
    }

    [Fact]
    public void RenderHistoryBody_EmptyVersions_DoesNotThrow()
    {
        var history = new DeckHistoryFile
        {
            DeckName = "Tivit Ad Nauseam",
            Versions = [],
        };

        var exception = Record.Exception(() => EvolutionHistoryRenderer.RenderHistoryBody(history));

        Assert.Null(exception);
        Assert.Contains("Versions: 0", EvolutionHistoryRenderer.RenderHistoryBody(history));
    }

    [Fact]
    public void RenderHistoryBody_CommanderHeader_UsesLatestVersion()
    {
        var history = new DeckHistoryFile
        {
            DeckName = "Tivit Ad Nauseam",
            Versions =
            [
                new DeckSnapshot
                {
                    Id = 1,
                    Date = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
                    Commander = [],
                    Cards = [new SnapshotCard { Name = "Sol Ring", Qty = 1 }],
                    Delta = new SnapshotDelta(),
                },
                new DeckSnapshot
                {
                    Id = 2,
                    Date = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
                    Commander = ["Tivit, Seller of Secrets"],
                    Cards =
                    [
                        new SnapshotCard { Name = "Sol Ring", Qty = 1 },
                        new SnapshotCard { Name = "Mystic Remora", Qty = 1 },
                    ],
                    Delta = new SnapshotDelta { Adds = [new SnapshotCard { Name = "Mystic Remora", Qty = 1 }] },
                },
            ],
        };

        var body = EvolutionHistoryRenderer.RenderHistoryBody(history);

        Assert.Contains("Commander: Tivit, Seller of Secrets", body);
    }
}
