using System.Text;
using System.Text.Json;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;

namespace DeckFlow.Core.Tests.StatedRulesExtraction;

public sealed class CliLlmDistillationStatedRulesGoldenTests
{
    private const string ValidOverride = "[\"wsl.exe\",\"claude\",\"-p\",\"{instruction}\",\"--output-format\",\"json\",\"--allowedTools\",\"\"]";
    private static readonly DateTimeOffset VideoDateUtc = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExtractAsync_SnailFixture_ProducesValidatedGroundedRepresentativeRules()
    {
        string transcript = await LoadFixtureAsync();
        IReadOnlyList<string> chunks = TranscriptChunker.Chunk(transcript);
        Assert.NotEmpty(chunks);

        var stdout = new Queue<string>();
        bool seededChunk = false;
        foreach (string _ in chunks)
        {
            stdout.Enqueue(
                ClaudeEnvelope(
                    """{"claims":["I usually want 37 to 42 lands in normal commander shells.","I reconsider the fourth or fifth board wipe because wipes are overrated.","I still want 7 to 12 ramp pieces.","Dockside Extortonist is one of the premium ramp cards I count in that band.","Sometimes you can kind of do whatever feels right and the numbers do not matter."]}"""));
            stdout.Enqueue(
                ClaudeEnvelope(
                    """{"claims":["I usually want 37 to 42 lands in normal commander shells.","I reconsider the fourth or fifth board wipe because wipes are overrated.","I still want 7 to 12 ramp pieces.","Dockside Extortonist is one of the premium ramp cards I count in that band."]}"""));

            if (!seededChunk)
            {
                stdout.Enqueue(ClaudeEnvelope(BuildSeededDecomposePayload()));
                seededChunk = true;
            }
            else
            {
                stdout.Enqueue(ClaudeEnvelope("""{"rules":[]}"""));
            }
        }

        stdout.Enqueue(ClaudeEnvelope(BuildReducePayload()));

        var service = CreateService(stdout);
        var grounder = new FakeCardNameGrounder("Dockside Extortionist");
        var extractor = new StatedRulesExtractor(service, grounder);

        IReadOnlyList<StatedRuleCandidate> result = await WithCommandOverrideAsync(
            ValidOverride,
            () => extractor.ExtractAsync(transcript, VideoDateUtc));

        DistillationValidation.ValidateStatedRules(result);

        StatedRuleCandidate landRule = Assert.Single(result, rule => rule.Metric == "land_count");
        Assert.Equal("range", landRule.Comparator);
        Assert.Equal(37, landRule.ValueMin);
        Assert.Equal(42, landRule.ValueMax);

        StatedRuleCandidate wipeRule = Assert.Single(result, rule => rule.Metric == "board-wipe");
        Assert.Equal("lte", wipeRule.Comparator);
        Assert.Equal(5, wipeRule.Value);

        const string cardSourceClip = "Dockside Extortonist is one of the premium ramp cards I count in that band.";
        StatedRuleCandidate cardRule = Assert.Single(result, rule => rule.SourceClip == cardSourceClip);
        Assert.Equal("Dockside Extortionist", cardRule.CardReference);
        Assert.True(cardRule.CardGrounded);
        Assert.Equal(cardSourceClip, cardRule.SourceClip);

        Assert.DoesNotContain(
            result,
            rule => rule.SourceClip.Contains(
                "Sometimes you can kind of do whatever feels right",
                StringComparison.Ordinal));
        Assert.Equal(["Dockside Extortonist"], grounder.Requests);
        Assert.Empty(stdout);
    }

    private static CliLlmDistillationService CreateService(Queue<string> stdoutQueue, TimeSpan? timeout = null)
        => new(
            "claude",
            (_, _, _) => Task.FromResult(stdoutQueue.Dequeue()),
            timeout);

    private static string ClaudeEnvelope(string result, bool isError = false)
        => JsonSerializer.Serialize(
            new
            {
                type = "result",
                subtype = "success",
                is_error = isError,
                duration_ms = 1,
                duration_api_ms = 1,
                num_turns = 1,
                result,
                session_id = "test-session",
                total_cost_usd = 0,
                usage = new { input_tokens = 0, output_tokens = 0 },
            });

    private static async Task<T> WithCommandOverrideAsync<T>(
        string overrideValue,
        Func<Task<T>> action)
    {
        string? prior = Environment.GetEnvironmentVariable(CliLlmDistillationService.CliCommandEnvironmentKey);
        Environment.SetEnvironmentVariable(CliLlmDistillationService.CliCommandEnvironmentKey, overrideValue);

        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CliLlmDistillationService.CliCommandEnvironmentKey, prior);
        }
    }

    private static async Task<string> LoadFixtureAsync()
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "salubrious-snail-transcript.txt");
        return await File.ReadAllTextAsync(fixturePath, Encoding.UTF8).ConfigureAwait(false);
    }

    private static string BuildSeededDecomposePayload()
        => $$"""
        {"rules":[
          {"category":"mana","metric":"land_count","value":null,"value_min":37,"value_max":42,"comparator":"range","condition":null,"clip_timestamp_seconds":12,"source_clip":"I usually want 37 to 42 lands in normal commander shells.","confidence":0.95,"card_reference":null},
          {"category":"interaction","metric":"board-wipe","value":5,"value_min":null,"value_max":null,"comparator":"lte","condition":null,"clip_timestamp_seconds":27,"source_clip":"I reconsider the fourth or fifth board wipe because wipes are overrated.","confidence":0.91,"card_reference":null},
          {"category":"mana","metric":"ramp","value":null,"value_min":7,"value_max":12,"comparator":"range","condition":null,"clip_timestamp_seconds":41,"source_clip":"I still want 7 to 12 ramp pieces.","confidence":0.9,"card_reference":null},
          {"category":"mana","metric":"ramp","value":1,"value_min":null,"value_max":null,"comparator":"gte","condition":null,"clip_timestamp_seconds":55,"source_clip":"Dockside Extortonist is one of the premium ramp cards I count in that band.","confidence":0.88,"card_reference":"Dockside Extortonist"}
        ]}
        """;

    private static string BuildReducePayload()
        => $$"""
        {"rules":[
          {"category":"mana","metric":"land_count","value":null,"value_min":37,"value_max":42,"comparator":"range","condition":null,"clip_timestamp_seconds":12,"source_clip":"I usually want 37 to 42 lands in normal commander shells.","confidence":0.95,"card_reference":null},
          {"category":"interaction","metric":"board-wipe","value":5,"value_min":null,"value_max":null,"comparator":"lte","condition":null,"clip_timestamp_seconds":27,"source_clip":"I reconsider the fourth or fifth board wipe because wipes are overrated.","confidence":0.91,"card_reference":null},
          {"category":"mana","metric":"ramp","value":null,"value_min":7,"value_max":12,"comparator":"range","condition":null,"clip_timestamp_seconds":41,"source_clip":"I still want 7 to 12 ramp pieces.","confidence":0.9,"card_reference":null},
          {"category":"mana","metric":"ramp","value":1,"value_min":null,"value_max":null,"comparator":"gte","condition":null,"clip_timestamp_seconds":55,"source_clip":"Dockside Extortonist is one of the premium ramp cards I count in that band.","confidence":0.88,"card_reference":"Dockside Extortonist"}
        ]}
        """;

    private sealed class FakeCardNameGrounder(string canonicalName) : ICardNameGrounder
    {
        public List<string> Requests { get; } = [];

        public Task<CardGroundingResult> TryGroundAsync(string candidateName, CancellationToken cancellationToken = default)
        {
            Requests.Add(candidateName);
            return Task.FromResult(new CardGroundingResult(true, canonicalName));
        }
    }
}
