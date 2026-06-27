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
    public async Task CategoryStoreThrows_OmitsBlock_BuildSucceeds()
    {
        var service = CreateService(
            categoryRowsOverride: (_, _) => throw new InvalidOperationException("category store down"));

        var result = await service.BuildAsync(CreateRequest("Upgraded"));

        Assert.NotNull(result);
        var prompt = result.PromptTextsByPlatform["ChatGPT"];
        Assert.DoesNotContain("CATEGORY_DISTRIBUTION:", prompt);
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
    public async Task FullCedhStyle_AtCedhBracket_ForcesCedhPresetSections_AndPreservesStyle()
    {
        var service = CreateService();
        var request = CreateRequest("cEDH");
        request.PrimerStyle = PrimerOutputStyle.FullCedh;
        request.SelectedSectionIds =
        [
            "commander-identity",
            "mulligan-principles"
        ];
        var secondRequest = CreateRequest("cEDH");
        secondRequest.PrimerStyle = PrimerOutputStyle.FullCedh;
        secondRequest.SelectedSectionIds =
        [
            "verified-combos",
            "upgrade-paths"
        ];

        var cacheKey = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        var secondCacheKey = await service.TryComputeCacheKeyAsync(secondRequest, CancellationToken.None);
        var result = await service.BuildAsync(request);
        var prompt = result.PromptTextsByPlatform["ChatGPT"];
        var expectedSections = PrimerSectionCatalog.GetPresetForBracket("cEDH");

        Assert.NotNull(cacheKey);
        Assert.Equal(cacheKey, secondCacheKey);
        Assert.Contains("STYLE: FullCedh", prompt);
        Assert.Contains("primer_style: FullCedh", result.RequestContextText);
        Assert.Contains("cedh-meta-macro-matchups", prompt);
        Assert.Contains("stack-wars-and-fast-mana", prompt);
        Assert.DoesNotContain("battlecruiser-politics-and-social-pacing", prompt);
        Assert.Equal(expectedSections.Count, CountSectionsFromPrompt(prompt));
        Assert.Contains($"SECTIONS: {string.Join(", ", expectedSections)}", prompt);
    }

    [Fact]
    public async Task FullCedhStyle_OutsideCedhBracket_FallsBackToMoxfieldRich_WithoutForcedSections()
    {
        var service = CreateService();
        var request = CreateRequest("Optimized");
        request.PrimerStyle = PrimerOutputStyle.FullCedh;
        request.SelectedSectionIds =
        [
            "verified-combos",
            "budget-cut-ladder"
        ];
        var secondRequest = CreateRequest("Optimized");
        secondRequest.PrimerStyle = PrimerOutputStyle.FullCedh;
        secondRequest.SelectedSectionIds =
        [
            "verified-combos"
        ];

        var cacheKey = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        var secondCacheKey = await service.TryComputeCacheKeyAsync(secondRequest, CancellationToken.None);
        var result = await service.BuildAsync(request);
        var prompt = result.PromptTextsByPlatform["ChatGPT"];

        Assert.NotNull(cacheKey);
        Assert.NotEqual(cacheKey, secondCacheKey);
        Assert.Contains("STYLE: MoxfieldRich", prompt);
        Assert.Contains("primer_style: MoxfieldRich", result.RequestContextText);
        Assert.Contains("SECTIONS: verified-combos, budget-cut-ladder", prompt);
        Assert.DoesNotContain("cedh-meta-macro-matchups", prompt);
        Assert.DoesNotContain("stack-wars-and-fast-mana", prompt);
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

    [Fact]
    public void RankingBranch_PopularityDESC_HigherPopularityFirst()
    {
        var text = DeckPrimerPacketService.BuildComboReferenceText(
            new CommanderSpellbookResult(
                [
                    new SpellbookCombo(["LowPop"], ["Infinite mana"], "Instruction", Popularity: 100, ManaValueNeeded: 1),
                    new SpellbookCombo(["HighPop"], ["Infinite mana"], "Instruction", Popularity: 9000, ManaValueNeeded: 4)
                ],
                []),
            "sufficient");

        // HighPop (9000) must rank before LowPop (100) despite costing more mana.
        Assert.True(text.IndexOf("HighPop", StringComparison.Ordinal) < text.IndexOf("LowPop", StringComparison.Ordinal));
    }

    [Fact]
    public void RankingBranch_PopularityTie_CheaperManaFirst()
    {
        var text = DeckPrimerPacketService.BuildComboReferenceText(
            new CommanderSpellbookResult(
                [
                    new SpellbookCombo(["Pricey"], ["Infinite mana"], "Instruction", Popularity: 500, ManaValueNeeded: 5),
                    new SpellbookCombo(["Cheap"], ["Infinite mana"], "Instruction", Popularity: 500, ManaValueNeeded: 2)
                ],
                []),
            "sufficient");

        // Equal popularity → lower manaValueNeeded (Cheap=2) ranks before Pricey (5).
        Assert.True(text.IndexOf("Cheap", StringComparison.Ordinal) < text.IndexOf("Pricey", StringComparison.Ordinal));
    }

    [Fact]
    public void RankingBranch_BothFieldsAbsent_PreservesApiOrder()
    {
        var text = DeckPrimerPacketService.BuildComboReferenceText(
            new CommanderSpellbookResult(
                [
                    new SpellbookCombo(["FirstApi"], ["Infinite mana"], "Instruction"),
                    new SpellbookCombo(["SecondApi"], ["Infinite mana"], "Instruction")
                ],
                []),
            "sufficient");

        // Null popularity + null manaValueNeeded for all → stable API order preserved.
        Assert.True(text.IndexOf("FirstApi", StringComparison.Ordinal) < text.IndexOf("SecondApi", StringComparison.Ordinal));
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
        Func<string, CancellationToken, Task<IReadOnlyList<CategoryKnowledgeRow>>>? categoryRowsOverride = null,
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
            getCategoryRowsForCommanderAsyncOverride: categoryRowsOverride ?? ((_, _) => Task.FromResult(categoryRows ?? (IReadOnlyList<CategoryKnowledgeRow>)Array.Empty<CategoryKnowledgeRow>())),
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
            builder.AppendLine($"STYLE: {request.PrimerStyle}");
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

    private static int CountSectionsFromPrompt(string prompt)
    {
        const string prefix = "SECTIONS: ";
        var line = prompt.Split('\n').First(text => text.StartsWith(prefix, StringComparison.Ordinal));
        return line[prefix.Length..]
            .Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
    }
}
