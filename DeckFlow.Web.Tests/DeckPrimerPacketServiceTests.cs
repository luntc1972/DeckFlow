using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Primer;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Covers deck-primer packet generation, grounding blocks, and AI-platform fan-out.
/// </summary>
public sealed class DeckPrimerPacketServiceTests
{
    [Fact]
    public async Task NullCombos_EmitsDisclosure()
    {
        var service = CreateService(comboResult: null);

        var result = await service.BuildAsync(CreateRequest("Upgraded"));

        var prompt = result.PromptTextsByPlatform["ChatGPT"];
        Assert.Contains("No verified combos available — treat all synergies as speculative.", prompt);
    }

    [Fact]
    public async Task Combos_TwoStructurallySeparatedBlocks()
    {
        var service = CreateService(comboResult: new CommanderSpellbookResult(
            [
                new SpellbookCombo(
                    ["Thassa's Oracle", "Demonic Consultation"],
                    ["Win the game"],
                    "Resolve Oracle, then Consultation.")
            ],
            []));

        var result = await service.BuildAsync(CreateRequest("Optimized"));

        var prompt = result.PromptTextsByPlatform["ChatGPT"];
        Assert.Contains("## Known Combos (ground truth — do not speculate)", prompt);
        Assert.Contains("## Speculative Synergies (you propose)", prompt);
    }

    [Fact]
    public void NearCombos_CappedAt15()
    {
        var nearCombos = Enumerable.Range(1, 20)
            .Select(index => new SpellbookAlmostCombo(
                $"Missing {index}",
                [$"Have {index}A", $"Have {index}B"],
                ["Infinite mana"],
                $"Instruction {index}"))
            .ToList();

        var text = DeckPrimerPacketService.BuildComboReferenceText(
            new CommanderSpellbookResult([], nearCombos),
            "sufficient");

        Assert.Contains("## Near-Combos (one card away)", text);
        Assert.Equal(15, CountOccurrences(text, "Missing:"));
    }

    [Fact]
    public async Task CategoryZeroRows_OmitsBlock()
    {
        var service = CreateService(categoryRows: []);

        var result = await service.BuildAsync(CreateRequest("Upgraded"));

        var prompt = result.PromptTextsByPlatform["ChatGPT"];
        Assert.DoesNotContain("CATEGORY_DISTRIBUTION:", prompt);
    }

    [Fact]
    public async Task CategoryRows_ProduceCounts()
    {
        var service = CreateService(categoryRows:
        [
            new CategoryKnowledgeRow("Ramp", "Sol Ring", 1),
            new CategoryKnowledgeRow("Card Draw", "Mystic Remora", 1),
            new CategoryKnowledgeRow("Tutor", "Demonic Tutor", 1),
            new CategoryKnowledgeRow("Removal", "Swords to Plowshares", 1),
            new CategoryKnowledgeRow("Interaction", "Force of Will", 1)
        ]);

        var result = await service.BuildAsync(CreateRequest("Upgraded"));

        var prompt = result.PromptTextsByPlatform["ChatGPT"];
        Assert.Contains("CATEGORY_DISTRIBUTION: ramp=1, draw=1, tutor=1, interaction=2", prompt);
    }

    [Fact]
    public async Task Bracket5_RoutesPerSpikeVerdict()
    {
        var callCount = 0;
        var service = CreateService(
            topArchetypes:
            [
                new EdhTop16Entry { PlayerName = "Kraum / Tymna", TournamentName = "WUBR" },
                new EdhTop16Entry { PlayerName = "Kinnan", TournamentName = "UG" }
            ],
            onTopArchetypesRequested: () => callCount++);

        var result = await service.BuildAsync(CreateRequest("cEDH"));

        var prompt = result.PromptTextsByPlatform["ChatGPT"];
        Assert.Equal(1, callCount);
        Assert.Contains("TOP16: Kraum / Tymna, Kinnan", prompt);
    }

    [Fact]
    public async Task NonCedh_SkipsEdhTop16()
    {
        var callCount = 0;
        var service = CreateService(onTopArchetypesRequested: () => callCount++);

        var result = await service.BuildAsync(CreateRequest("Upgraded"));

        var prompt = result.PromptTextsByPlatform["ChatGPT"];
        Assert.Equal(0, callCount);
        Assert.Contains("TOP16: (none)", prompt);
    }

    [Fact]
    public async Task GeminiDisabled_ResultHasTwoPlatforms()
    {
        var service = CreateService(geminiEnabled: false, includeAllPlatforms: true);

        var result = await service.BuildAsync(CreateRequest("Upgraded"));

        Assert.Equal(["ChatGPT", "Claude"], result.PromptTextsByPlatform.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task GeminiEnabled_ResultHasThreePlatforms()
    {
        var service = CreateService(geminiEnabled: true, includeAllPlatforms: true);

        var result = await service.BuildAsync(CreateRequest("Upgraded"));

        Assert.Equal(["ChatGPT", "Claude", "Gemini"], result.PromptTextsByPlatform.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void RankingBranch_FallbackEmitsApiOrderInstruction()
    {
        var text = DeckPrimerPacketService.BuildComboReferenceText(
            new CommanderSpellbookResult(
                [
                    new SpellbookCombo(["A", "B"], ["Infinite mana"], "Instruction")
                ],
                []),
            "fallback");

        Assert.Contains("Keep the API order above and rank the practical combo lines yourself.", text);
    }

    private static DeckPrimerRequest CreateRequest(string bracket)
        => new()
        {
            DeckText = """
                Commander
                1 Atraxa, Praetors' Voice

                1 Sol Ring
                1 Arcane Signet
                """,
            TargetCommanderBracket = bracket,
            SelectedSectionIds =
            [
                "verified-combos",
                "near-combos",
                "role-count-grounding",
                "matchup-archetype-plan"
            ]
        };

    private static DeckPrimerPacketService CreateService(
        CommanderSpellbookResult? comboResult = null,
        IReadOnlyList<CategoryKnowledgeRow>? categoryRows = null,
        IReadOnlyList<EdhTop16Entry>? topArchetypes = null,
        Action? onTopArchetypesRequested = null,
        bool geminiEnabled = false,
        bool includeAllPlatforms = false)
    {
        var variants = includeAllPlatforms
            ? new IPrimerPromptVariant[]
            {
                new TestPrimerPromptVariant(AiPlatform.ChatGpt),
                new TestPrimerPromptVariant(AiPlatform.Claude),
                new TestPrimerPromptVariant(AiPlatform.Gemini)
            }
            : new IPrimerPromptVariant[]
            {
                new TestPrimerPromptVariant(AiPlatform.ChatGpt),
                new TestPrimerPromptVariant(AiPlatform.Claude)
            };

        return new DeckPrimerPacketService(
            new PrimerPromptVariantRegistry(variants),
            new PacketSessionCache(),
            loadDeckEntriesAsyncOverride: (_, _) => Task.FromResult<List<DeckEntry>>(
            [
                CreateDeckEntry("Atraxa, Praetors' Voice", "commander"),
                CreateDeckEntry("Sol Ring", "mainboard"),
                CreateDeckEntry("Arcane Signet", "mainboard")
            ]),
            findCombosAsyncOverride: (_, _) => Task.FromResult(comboResult),
            getTopArchetypesAsyncOverride: (_, _) =>
            {
                onTopArchetypesRequested?.Invoke();
                return Task.FromResult(topArchetypes ?? (IReadOnlyList<EdhTop16Entry>)Array.Empty<EdhTop16Entry>());
            },
            getCategoryRowsForCommanderAsyncOverride: (_, _) => Task.FromResult(categoryRows ?? (IReadOnlyList<CategoryKnowledgeRow>)Array.Empty<CategoryKnowledgeRow>()),
            geminiEnabled: geminiEnabled);
    }

    private static DeckEntry CreateDeckEntry(string name, string board)
        => new()
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = 1,
            Board = board
        };

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private sealed class TestPrimerPromptVariant : IPrimerPromptVariant
    {
        public TestPrimerPromptVariant(AiPlatform platform)
        {
            Platform = platform;
        }

        public AiPlatform Platform { get; }

        public string Build(
            DeckPrimerRequest request,
            string decklistText,
            IReadOnlyList<PrimerSectionEntry> selectedSections,
            CommanderSpellbookResult? comboResult,
            IReadOnlyList<EdhTop16Entry>? top16Entries,
            CategoryDistributionSummary? categoryDistribution,
            int bracketNumber,
            CancellationToken cancellationToken = default)
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine($"PLATFORM: {Platform.Key}");
            builder.AppendLine($"BRACKET: {bracketNumber}");
            builder.AppendLine($"SECTIONS: {string.Join(", ", selectedSections.Select(section => section.Id))}");
            builder.AppendLine(DeckPrimerPacketService.BuildComboReferenceText(comboResult, "sufficient"));
            builder.AppendLine(top16Entries is null || top16Entries.Count == 0
                ? "TOP16: (none)"
                : $"TOP16: {string.Join(", ", top16Entries.Select(entry => entry.PlayerName))}");
            if (categoryDistribution is not null)
            {
                builder.AppendLine($"CATEGORY_DISTRIBUTION: ramp={categoryDistribution.RampCount}, draw={categoryDistribution.DrawCount}, tutor={categoryDistribution.TutorCount}, interaction={categoryDistribution.InteractionCount}");
            }

            return builder.ToString().TrimEnd();
        }
    }
}
