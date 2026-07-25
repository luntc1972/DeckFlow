using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;

namespace DeckFlow.Core.Tests.StatedRulesExtraction;

public sealed class StatedRulesExtractorTests
{
    [Fact]
    public async Task ExtractAsync_CrossChunkRules_AreDeterministicallyDeduped()
    {
        string transcript = BuildMultiChunkTranscript();
        int chunkCount = TranscriptChunker.Chunk(transcript).Count;
        var duplicateRule = CreateRule(
            metric: "land_count",
            comparator: "range",
            sourceClip: "Play 37 to 42 lands in most shells.",
            valueMin: 37,
            valueMax: 42,
            confidence: 0.81);
        var service = new FakeLlmDistillationService
        {
            SelectResults = Enumerable
                .Range(0, chunkCount)
                .Select(_ => new SelectResult(["Play 37 to 42 lands in most shells."], ZeroUsage))
                .ToArray(),
            DisambiguateResults = Enumerable
                .Range(0, chunkCount)
                .Select(_ => new DisambiguateResult(["Play 37 to 42 lands in most shells."], ZeroUsage))
                .ToArray(),
            DecomposeResults = Enumerable
                .Range(0, chunkCount)
                .Select(index => new DecomposeResult([duplicateRule with { Confidence = 0.81 - (index * 0.01) }], ZeroUsage))
                .ToArray(),
            ReduceResultFactory = rules => new ReduceResult(rules.ToList(), ZeroUsage),
        };
        var extractor = new StatedRulesExtractor(service);

        IReadOnlyList<StatedRuleCandidate> result = await extractor.ExtractAsync(
            transcript,
            VideoDateUtc);

        StatedRuleCandidate rule = Assert.Single(result);
        Assert.Equal("land_count", rule.Metric);
        Assert.Equal("range", rule.Comparator);
        Assert.Equal(37, rule.ValueMin);
        Assert.Equal(42, rule.ValueMax);
        Assert.Equal(chunkCount, service.SelectInputs.Count);
        Assert.Equal(chunkCount, service.DisambiguateInputs.Count);
        Assert.Equal(chunkCount, service.DecomposeInputs.Count);
        Assert.Equal(chunkCount, service.ReduceInputs.Single().Count);
    }

    [Fact]
    public async Task ExtractAsync_DisambiguateDropsAmbiguousClaim_OmitsItsRule()
    {
        const string ambiguous = "Sometimes you can kind of do whatever feels right.";
        var service = new FakeLlmDistillationService
        {
            SelectResults =
            [
                new SelectResult(
                    ["Play 7 to 12 ramp pieces.", ambiguous],
                    ZeroUsage),
            ],
            DisambiguateResults =
            [
                new DisambiguateResult(["Play 7 to 12 ramp pieces in most commander decks."], ZeroUsage),
            ],
            DecomposeResults =
            [
                new DecomposeResult(
                [
                    CreateRule(
                        metric: "ramp",
                        comparator: "range",
                        sourceClip: "Play 7 to 12 ramp pieces in most commander decks.",
                        valueMin: 7,
                        valueMax: 12),
                ], ZeroUsage),
            ],
            ReduceResultFactory = rules => new ReduceResult(rules.ToList(), ZeroUsage),
        };
        var extractor = new StatedRulesExtractor(service);

        IReadOnlyList<StatedRuleCandidate> result = await extractor.ExtractAsync(
            "[00:00] Play 7 to 12 ramp pieces. [00:15] Sometimes you can kind of do whatever feels right.",
            VideoDateUtc);

        StatedRuleCandidate rule = Assert.Single(result);
        Assert.Equal(
            ["Play 7 to 12 ramp pieces.", ambiguous],
            service.DisambiguateInputs.Single());
        Assert.Equal(
            ["Play 7 to 12 ramp pieces in most commander decks."],
            service.DecomposeInputs.Single());
        Assert.DoesNotContain(result, candidate => candidate.SourceClip.Contains(ambiguous, StringComparison.Ordinal));
        Assert.Equal("ramp", rule.Metric);
    }

    [Fact]
    public async Task ExtractAsync_NullGrounder_PassesThroughCardReferenceAndValidation()
    {
        var rule = CreateRule(
            metric: "board-wipe",
            comparator: "lte",
            sourceClip: "I reconsider the fourth or fifth board wipe.",
            value: 5,
            cardReference: "Dockside Extortonist");
        var service = CreateSingleRuleService(rule);
        var extractor = new StatedRulesExtractor(service);

        IReadOnlyList<StatedRuleCandidate> result = await extractor.ExtractAsync(
            "[00:21] I reconsider the fourth or fifth board wipe.",
            VideoDateUtc);

        StatedRuleCandidate extracted = Assert.Single(result);
        Assert.Equal("Dockside Extortonist", extracted.CardReference);
        Assert.Null(extracted.CardGrounded);
        DistillationValidation.ValidateStatedRules(result);
    }

    [Fact]
    public async Task ExtractAsync_GrounderResolved_RewritesCanonicalNameAndPreservesSourceClip()
    {
        var sourceClip = "Dockside Extortonist is one of the premium ramp pieces I still count.";
        var rule = CreateRule(
            metric: "ramp",
            comparator: "gte",
            sourceClip: sourceClip,
            value: 1,
            cardReference: "Dockside Extortonist");
        var grounder = new FakeCardNameGrounder();
        grounder.Results["Dockside Extortonist"] = new CardGroundingResult(true, "Dockside Extortionist");
        var extractor = new StatedRulesExtractor(CreateSingleRuleService(rule), grounder);

        IReadOnlyList<StatedRuleCandidate> result = await extractor.ExtractAsync(
            "[00:33] Dockside Extortonist is one of the premium ramp pieces I still count.",
            VideoDateUtc);

        StatedRuleCandidate extracted = Assert.Single(result);
        Assert.Equal("Dockside Extortionist", extracted.CardReference);
        Assert.True(extracted.CardGrounded);
        Assert.Equal(sourceClip, extracted.SourceClip);
        Assert.Equal(["Dockside Extortonist"], grounder.Requests);
    }

    [Fact]
    public async Task ExtractAsync_GrounderMiss_KeepsRuleAndFlagsFalse()
    {
        var rule = CreateRule(
            metric: "ramp",
            comparator: "gte",
            sourceClip: "Dockside Extortonist is still worth a slot here.",
            value: 1,
            cardReference: "Dockside Extortonist");
        var grounder = new FakeCardNameGrounder();
        grounder.Results["Dockside Extortonist"] = new CardGroundingResult(false, "Dockside Extortonist");
        var extractor = new StatedRulesExtractor(CreateSingleRuleService(rule), grounder);

        IReadOnlyList<StatedRuleCandidate> result = await extractor.ExtractAsync(
            "[00:33] Dockside Extortonist is still worth a slot here.",
            VideoDateUtc);

        StatedRuleCandidate extracted = Assert.Single(result);
        Assert.Equal("Dockside Extortonist", extracted.CardReference);
        Assert.False(extracted.CardGrounded);
    }

    [Fact]
    public async Task ExtractAsync_NullCardReference_SkipsGrounderAndLeavesRuleUntouched()
    {
        var rule = CreateRule(
            metric: "land_count",
            comparator: "range",
            sourceClip: "Play 37 to 42 lands in most shells.",
            valueMin: 37,
            valueMax: 42,
            cardReference: null);
        var grounder = new FakeCardNameGrounder();
        var extractor = new StatedRulesExtractor(CreateSingleRuleService(rule), grounder);

        IReadOnlyList<StatedRuleCandidate> result = await extractor.ExtractAsync(
            "[00:00] Play 37 to 42 lands in most shells.",
            VideoDateUtc);

        StatedRuleCandidate extracted = Assert.Single(result);
        Assert.Null(extracted.CardReference);
        Assert.Null(extracted.CardGrounded);
        Assert.Empty(grounder.Requests);
    }

    private static readonly DateTimeOffset VideoDateUtc = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly TokenUsage ZeroUsage = new(0, 0);

    private static FakeLlmDistillationService CreateSingleRuleService(StatedRuleCandidate rule)
        => new()
        {
            SelectResults =
            [
                new SelectResult([rule.SourceClip], ZeroUsage),
            ],
            DisambiguateResults =
            [
                new DisambiguateResult([rule.SourceClip], ZeroUsage),
            ],
            DecomposeResults =
            [
                new DecomposeResult([rule], ZeroUsage),
            ],
            ReduceResultFactory = rules => new ReduceResult(rules.ToList(), ZeroUsage),
        };

    private static StatedRuleCandidate CreateRule(
        string metric,
        string comparator,
        string sourceClip,
        double? value = null,
        double? valueMin = null,
        double? valueMax = null,
        double confidence = 0.9,
        string? cardReference = null)
        => new()
        {
            Category = "mana",
            Metric = metric,
            Value = value,
            ValueMin = valueMin,
            ValueMax = valueMax,
            Comparator = comparator,
            Condition = null,
            ClipTimestampSeconds = 12,
            SourceClip = sourceClip,
            Confidence = confidence,
            CardReference = cardReference,
            CardGrounded = null,
            VideoDateUtc = VideoDateUtc,
        };

    private static string BuildMultiChunkTranscript()
    {
        var segments = Enumerable.Range(0, 24)
            .Select(
                index =>
                {
                    string timestamp = $"[{index / 60:D2}:{index % 60:D2}]";
                    string body = string.Join(
                        " ",
                        Enumerable.Repeat(
                            "This segment repeats enough filler words to force transcript chunking across timestamp boundaries.",
                            16));
                    return $"{timestamp} {body}";
                });
        return string.Join(" ", segments);
    }

    private sealed class FakeLlmDistillationService : ILlmDistillationService
    {
        public required IReadOnlyList<SelectResult> SelectResults { get; init; }
        public required IReadOnlyList<DisambiguateResult> DisambiguateResults { get; init; }
        public required IReadOnlyList<DecomposeResult> DecomposeResults { get; init; }
        public required Func<IReadOnlyList<StatedRuleCandidate>, ReduceResult> ReduceResultFactory { get; init; }
        public List<string> SelectInputs { get; } = [];
        public List<IReadOnlyList<string>> DisambiguateInputs { get; } = [];
        public List<IReadOnlyList<string>> DecomposeInputs { get; } = [];
        public List<IReadOnlyList<StatedRuleCandidate>> ReduceInputs { get; } = [];

        private int _selectIndex;
        private int _disambiguateIndex;
        private int _decomposeIndex;

        public Task<SummaryResult> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ClipsResult> ExtractClipsAsync(string transcript, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TagsResult> InferTagsAsync(string transcript, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SelectResult> SelectStatedClaimsAsync(string transcriptChunk, CancellationToken ct = default)
        {
            SelectInputs.Add(transcriptChunk);
            return Task.FromResult(SelectResults[_selectIndex++]);
        }

        public Task<DisambiguateResult> DisambiguateStatedClaimsAsync(IReadOnlyList<string> selectedClaims, CancellationToken ct = default)
        {
            DisambiguateInputs.Add(selectedClaims);
            return Task.FromResult(DisambiguateResults[_disambiguateIndex++]);
        }

        public Task<DecomposeResult> DecomposeStatedClaimsAsync(
            IReadOnlyList<string> disambiguatedClaims,
            DateTimeOffset videoDateUtc,
            CancellationToken ct = default)
        {
            DecomposeInputs.Add(disambiguatedClaims);
            return Task.FromResult(DecomposeResults[_decomposeIndex++]);
        }

        public Task<ReduceResult> ReduceStatedRulesAsync(
            IReadOnlyList<StatedRuleCandidate> allChunkRules,
            DateTimeOffset videoDateUtc,
            CancellationToken ct = default)
        {
            ReduceInputs.Add(allChunkRules.ToList());
            return Task.FromResult(ReduceResultFactory(allChunkRules));
        }
    }

    private sealed class FakeCardNameGrounder : ICardNameGrounder
    {
        public Dictionary<string, CardGroundingResult> Results { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Requests { get; } = [];

        public Task<CardGroundingResult> TryGroundAsync(string candidateName, CancellationToken cancellationToken = default)
        {
            Requests.Add(candidateName);
            if (Results.TryGetValue(candidateName, out CardGroundingResult? result))
            {
                return Task.FromResult(result);
            }

            return Task.FromResult(new CardGroundingResult(false, candidateName));
        }
    }
}
