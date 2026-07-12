using System.Text;
using System.Text.Json;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CliLlmDistillationServiceTests
{
    private const string ValidOverride = "[\"wsl.exe\",\"claude\",\"-p\",\"{instruction}\",\"--output-format\",\"json\",\"--allowedTools\",\"\"]";

    [Fact]
    public void BuildStartInfo_PinsStandardStreamEncodingsToUtf8()
    {
        // Guards against CP437/OEM mojibake: redirected streams left unset default to
        // Console.OutputEncoding on Windows, mangling the child CLI's UTF-8 output.
        var spec = new CliCommandSpec("claude", ["-p", "prompt"], CliEnvelopeKind.ClaudeJson);

        var startInfo = CliLlmDistillationService.BuildStartInfo(spec);

        Assert.Equal(65001, startInfo.StandardOutputEncoding!.CodePage);
        Assert.Equal(65001, startInfo.StandardErrorEncoding!.CodePage);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.True(startInfo.RedirectStandardInput);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(new[] { "-p", "prompt" }, startInfo.ArgumentList);
    }

    [Fact]
    public async Task Summarize_CleanJsonClaudeEnvelope_ReturnsSummaryAndZeroUsage()
    {
        var stdout = new Queue<string>([ClaudeEnvelope("""{"summary":"Build around sacrifice payoffs."}""")]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.SummarizeAsync("transcript"));

        Assert.Equal("Build around sacrifice payoffs.", result.Summary);
        Assert.Equal(new TokenUsage(0, 0), result.Usage);
    }

    [Fact]
    public async Task Summarize_FencedJsonInsideResult_StripsFence()
    {
        var stdout = new Queue<string>([ClaudeEnvelope("```json\n{\"summary\":\"Fence stripped.\"}\n```")]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.SummarizeAsync("transcript"));

        Assert.Equal("Fence stripped.", result.Summary);
    }

    [Fact]
    public async Task Summarize_ClaudeEnvelopeUnwrapsResult()
    {
        var stdout = new Queue<string>([ClaudeEnvelope("""{"summary":"Envelope unwrapped."}""")]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.SummarizeAsync("transcript"));

        Assert.Equal("Envelope unwrapped.", result.Summary);
    }

    [Fact]
    public async Task Summarize_WrapperProseAndBraceInString_ExtractsSingleBalancedObject()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope("""Here is the result: {"summary":"a {brace} inside the string"} thanks!""")
        ]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.SummarizeAsync("transcript"));

        Assert.Equal("a {brace} inside the string", result.Summary);
    }

    [Fact]
    public async Task Summarize_MissingRequiredField_ReturnsEmptySummaryWithoutRetry()
    {
        var stdout = new Queue<string>([ClaudeEnvelope("""{"not_summary":"x"}""")]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.SummarizeAsync("transcript"));

        Assert.Equal(string.Empty, result.Summary);
        Assert.Empty(stdout);
    }

    [Fact]
    public async Task Summarize_GarbageThenValid_RetriesAndSucceeds()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope("not json"),
            ClaudeEnvelope("""{"summary":"Recovered."}""")
        ]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.SummarizeAsync("transcript"));

        Assert.Equal("Recovered.", result.Summary);
        Assert.Empty(stdout);
    }

    [Fact]
    public async Task Summarize_PersistentGarbage_ThrowsAfterThreeAttempts()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope("not json"),
            ClaudeEnvelope("still not json"),
            ClaudeEnvelope("never json")
        ]);
        var service = CreateService(stdout);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WithCommandOverrideAsync(ValidOverride, () => service.SummarizeAsync("transcript")));

        Assert.Contains("failed after 3 attempts", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(stdout);
    }

    [Fact]
    public async Task ClassifyAsync_KeepVerdict_ReturnsKeep()
    {
        var stdout = new Queue<string>([ClaudeEnvelope("""{"verdict":"keep","reason":"deckbuilding philosophy"}""")]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.ClassifyAsync("transcript"));

        Assert.Equal("keep", result.Verdict);
        Assert.Equal("deckbuilding philosophy", result.Reason);
    }

    [Fact]
    public async Task ClassifyAsync_DropVerdict_ReturnsDrop()
    {
        var stdout = new Queue<string>([ClaudeEnvelope("""{"verdict":"drop","reason":"trivia"}""")]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.ClassifyAsync("transcript"));

        Assert.Equal("drop", result.Verdict);
        Assert.Equal("trivia", result.Reason);
    }

    [Fact]
    public async Task Summarize_ClaudeIsError_ThrowsSanitizedNoResultBody()
    {
        const string sentinel = "SECRET_TRANSCRIPT_SENTINEL";
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope($"error includes {sentinel}", isError: true),
            ClaudeEnvelope($"error includes {sentinel}", isError: true),
            ClaudeEnvelope($"error includes {sentinel}", isError: true)
        ]);
        var service = CreateService(stdout);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WithCommandOverrideAsync(ValidOverride, () => service.SummarizeAsync("transcript")));

        Assert.DoesNotContain(sentinel, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractClips_ValidPayload_ReturnsClipsAndZeroUsage()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope(
                """{"clips":[{"timestamp_seconds":120,"excerpt":"Use the commander as a draw engine."},{"timestamp_seconds":null,"excerpt":"Protect the combo turn."},{"timestamp_seconds":480,"excerpt":"Close with a sacrifice loop."}]}""")
        ]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.ExtractClipsAsync("transcript"));

        Assert.Equal(3, result.Clips.Count);
        Assert.Equal(120, result.Clips[0].TimestampSeconds);
        Assert.Null(result.Clips[1].TimestampSeconds);
        Assert.Equal(new TokenUsage(0, 0), result.Usage);
    }

    [Fact]
    public async Task Summarize_LongSummary_TruncatesWithoutRetry()
    {
        var longSummary = string.Join(" ", Enumerable.Range(1, 205).Select(index => $"word{index}"));
        var stdout = new Queue<string>([ClaudeEnvelope($$"""{"summary":"{{longSummary}}"}""")]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.SummarizeAsync("transcript"));

        Assert.Equal(200, DistillationValidation.CountWords(result.Summary));
        Assert.DoesNotContain("word201", result.Summary, StringComparison.Ordinal);
        Assert.Empty(stdout);
    }

    [Fact]
    public async Task ExtractClips_InvalidCountAndNegativeTimestamp_SanitizesWithoutRetry()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope(
                """{"clips":[{"timestamp_seconds":-5,"excerpt":"drop"},{"timestamp_seconds":10,"excerpt":"1"},{"timestamp_seconds":20,"excerpt":"2"},{"timestamp_seconds":30,"excerpt":"3"},{"timestamp_seconds":40,"excerpt":"4"},{"timestamp_seconds":50,"excerpt":"5"},{"timestamp_seconds":60,"excerpt":"6"},{"timestamp_seconds":70,"excerpt":"7"},{"timestamp_seconds":80,"excerpt":"8"},{"timestamp_seconds":90,"excerpt":"9"}]}""")
        ]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.ExtractClipsAsync("transcript"));

        Assert.Equal(8, result.Clips.Count);
        Assert.Equal([10, 20, 30, 40, 50, 60, 70, 80], result.Clips.Select(clip => clip.TimestampSeconds).ToArray());
        Assert.Empty(stdout);
    }

    [Fact]
    public async Task InferTags_ValidPayload_ReturnsTagsAndZeroUsage()
    {
        var stdout = new Queue<string>([ClaudeEnvelope(ValidTagsJson())]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.InferTagsAsync("transcript"));

        Assert.Equal(["aristocrats"], result.Archetype);
        Assert.Equal(["Optimized"], result.Bracket);
        Assert.Equal(["draw", "win-cons"], result.CardCategory);
        Assert.Equal(new TokenUsage(0, 0), result.Usage);
    }

    [Fact]
    public async Task InferTags_VocabInvalidAndDuplicate_SanitizesWithoutRetry()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope("""{"archetype":["banana","Aristocrats","ARISTOCRATS","tokens"],"bracket":["Optimized","optimized","battlecruiser"],"card_category":["artifacts","draw","DRAW","ramp"]}""")
        ]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.InferTagsAsync("transcript"));

        Assert.Equal(["aristocrats", "tokens"], result.Archetype);
        Assert.Equal(["Optimized"], result.Bracket);
        Assert.Equal(["draw", "ramp"], result.CardCategory);
        Assert.Empty(stdout);
    }

    [Fact]
    public async Task InferTags_AllInvalidOrNullArrays_ReturnsEmptyLists()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope("""{"archetype":null,"bracket":["battlecruiser"],"card_category":[" ","tempo"]}""")
        ]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.InferTagsAsync("transcript"));

        Assert.Empty(result.Archetype);
        Assert.Empty(result.Bracket);
        Assert.Empty(result.CardCategory);
        Assert.Empty(stdout);
    }

    [Fact]
    public async Task SelectStatedClaimsAsync_ValidPayload_ReturnsClaimsAndZeroUsage()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope("""{"claims":["Run at least ten ramp pieces.","Cut four-drops that do not advance the combo."]}""")
        ]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.SelectStatedClaimsAsync("chunk"));

        Assert.Equal(
            ["Run at least ten ramp pieces.", "Cut four-drops that do not advance the combo."],
            result.Claims);
        Assert.Equal(new TokenUsage(0, 0), result.Usage);
    }

    [Fact]
    public async Task DisambiguateStatedClaimsAsync_ValidPayload_ReturnsClaimsAndZeroUsage()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope("""{"claims":["Run at least ten ramp pieces in this list.","Cut four-drops that do not advance the combo plan."]}""")
        ]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.DisambiguateStatedClaimsAsync(["Run at least ten ramp pieces.", "Cut four-drops."]));

        Assert.Equal(
            ["Run at least ten ramp pieces in this list.", "Cut four-drops that do not advance the combo plan."],
            result.Claims);
        Assert.Equal(new TokenUsage(0, 0), result.Usage);
    }

    [Fact]
    public async Task DecomposeStatedClaimsAsync_ValidPayload_ReturnsSanitizedRulesWithVideoDate()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope(
                """{"rules":[{"category":"mana","metric":"land_count","value":10,"value_min":null,"value_max":null,"comparator":"gte","condition":null,"clip_timestamp_seconds":95,"source_clip":"I want at least ten lands here.","confidence":0.92,"card_reference":null}]}""")
        ]);
        var service = CreateService(stdout);
        var videoDateUtc = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.DecomposeStatedClaimsAsync(
                ["Run at least ten ramp pieces in this list."],
                videoDateUtc));

        var rule = Assert.Single(result.Rules);
        Assert.Equal("mana", rule.Category);
        Assert.Equal("land_count", rule.Metric);
        Assert.Equal(10, rule.Value);
        Assert.Equal(95, rule.ClipTimestampSeconds);
        Assert.Equal(videoDateUtc, rule.VideoDateUtc);
        Assert.Equal(new TokenUsage(0, 0), result.Usage);
    }

    [Fact]
    public async Task ReduceStatedRulesAsync_ValidPayload_ReturnsSanitizedRulesAndZeroUsage()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope(
                """{"rules":[{"category":"interaction","metric":"interaction","value":7,"value_min":null,"value_max":null,"comparator":"gte","condition":"archetype:midrange","clip_timestamp_seconds":null,"source_clip":"I still want at least seven interaction pieces in midrange shells.","confidence":0.81,"card_reference":null}]}""")
        ]);
        var service = CreateService(stdout);
        var videoDateUtc = new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);
        IReadOnlyList<StatedRuleCandidate> allChunkRules =
        [
            new()
            {
                Category = "interaction",
                Metric = "interaction",
                Value = 7,
                ValueMin = null,
                ValueMax = null,
                Comparator = "gte",
                Condition = "archetype:midrange",
                ClipTimestampSeconds = 30,
                SourceClip = "At least seven pieces of spot removal.",
                Confidence = 0.7,
                CardReference = null,
                CardGrounded = null,
                VideoDateUtc = videoDateUtc,
            }
        ];

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.ReduceStatedRulesAsync(allChunkRules, videoDateUtc));

        var rule = Assert.Single(result.Rules);
        Assert.Equal("interaction", rule.Category);
        Assert.Equal("interaction", rule.Metric);
        Assert.Equal("archetype:midrange", rule.Condition);
        Assert.Equal(videoDateUtc, rule.VideoDateUtc);
        Assert.Equal(new TokenUsage(0, 0), result.Usage);
    }

    [Fact]
    public async Task Summarize_RunnerHangs_TimesOutWithoutHanging()
    {
        var service = new CliLlmDistillationService(
            "claude",
            async (_, _, ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return string.Empty;
            },
            TimeSpan.FromMilliseconds(20));

        var call = WithCommandOverrideAsync(
            ValidOverride,
            () => service.SummarizeAsync("transcript", CancellationToken.None));
        var completed = await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(call, completed);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    [Fact]
    public async Task Summarize_RunnerHangs_CallerNoneStillTimesOut()
    {
        var service = new CliLlmDistillationService(
            "claude",
            async (_, _, ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return string.Empty;
            },
            TimeSpan.FromMilliseconds(20));

        var call = WithCommandOverrideAsync(
            ValidOverride,
            () => service.SummarizeAsync("transcript"));
        var completed = await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(call, completed);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    [Fact]
    public async Task BuildSpec_JsonArrayOverrideWithPlaceholder_SubstitutesInstructionAtPosition()
    {
        CliCommandSpec? capturedSpec = null;
        string? capturedStdin = null;
        var service = new CliLlmDistillationService(
            "claude",
            (spec, stdin, _) =>
            {
                capturedSpec = spec;
                capturedStdin = stdin;
                return Task.FromResult(ClaudeEnvelope("""{"summary":"Captured."}"""));
            });

        await WithCommandOverrideAsync(
            ValidOverride,
            () => service.SummarizeAsync("transcript"));

        Assert.NotNull(capturedSpec);
        Assert.Equal("wsl.exe", capturedSpec.FileName);
        Assert.Equal("transcript", capturedStdin);
        var arguments = capturedSpec.ArgumentList.ToArray();
        var promptIndex = Array.IndexOf(arguments, "-p") + 1;
        Assert.True(promptIndex > 0);
        Assert.StartsWith(DistillationSchemas.SummarySystemPrompt, arguments[promptIndex], StringComparison.Ordinal);
        Assert.Contains(DistillationSchemas.SummarySchema, arguments[promptIndex], StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSpec_JsonArrayOverride_PreservesEmptyAllowedToolsArg()
    {
        CliCommandSpec? capturedSpec = null;
        var service = new CliLlmDistillationService(
            "claude",
            (spec, _, _) =>
            {
                capturedSpec = spec;
                return Task.FromResult(ClaudeEnvelope("""{"summary":"Captured."}"""));
            });

        await WithCommandOverrideAsync(
            ValidOverride,
            () => service.SummarizeAsync("transcript"));

        Assert.NotNull(capturedSpec);
        var arguments = capturedSpec.ArgumentList.ToArray();
        var allowedToolsIndex = Array.IndexOf(arguments, "--allowedTools");
        Assert.True(allowedToolsIndex >= 0);
        Assert.Equal(string.Empty, arguments[allowedToolsIndex + 1]);
    }

    [Fact]
    public async Task BuildSpec_OverrideNonJson_ThrowsLlmCliConfigurationException()
        => await AssertBadOverrideThrowsConfigExceptionAsync("not json");

    [Fact]
    public async Task BuildSpec_OverrideMissingPlaceholder_ThrowsLlmCliConfigurationException()
        => await AssertBadOverrideThrowsConfigExceptionAsync("[\"wsl.exe\",\"claude\",\"-p\"]");

    [Fact]
    public async Task BuildSpec_OverrideDuplicatePlaceholder_ThrowsLlmCliConfigurationException()
        => await AssertBadOverrideThrowsConfigExceptionAsync("[\"wsl.exe\",\"claude\",\"-p\",\"{instruction}\",\"{instruction}\"]");

    [Fact]
    public async Task BuildSpec_OverrideEmptyArray_ThrowsLlmCliConfigurationException()
        => await AssertBadOverrideThrowsConfigExceptionAsync("[]");

    private static CliLlmDistillationService CreateService(Queue<string> stdoutQueue, TimeSpan? timeout = null)
        => new(
            "claude",
            (_, _, _) => Task.FromResult(stdoutQueue.Dequeue()),
            timeout);

    private static async Task AssertBadOverrideThrowsConfigExceptionAsync(string overrideValue)
    {
        var invocations = 0;
        var service = new CliLlmDistillationService(
            "claude",
            (_, _, _) =>
            {
                invocations++;
                return Task.FromResult(ClaudeEnvelope("""{"summary":"Should not run."}"""));
            });

        var exception = await Assert.ThrowsAsync<LlmCliConfigurationException>(
            () => WithCommandOverrideAsync(overrideValue, () => service.SummarizeAsync("transcript")));

        Assert.Equal(0, invocations);
        Assert.Contains(CliLlmDistillationService.CliCommandEnvironmentKey, exception.Message, StringComparison.Ordinal);
    }

    private static string ClaudeEnvelope(string result, bool isError = false)
        => JsonSerializer.Serialize(new
        {
            type = "result",
            is_error = isError,
            result
        });

    private static string ValidTagsJson()
        => """{"archetype":["aristocrats"],"bracket":["Optimized"],"card_category":["draw","win-cons"]}""";

    private static async Task<T> WithCommandOverrideAsync<T>(
        string? overrideValue,
        Func<Task<T>> action)
    {
        var previous = Environment.GetEnvironmentVariable(CliLlmDistillationService.CliCommandEnvironmentKey);
        Environment.SetEnvironmentVariable(CliLlmDistillationService.CliCommandEnvironmentKey, overrideValue);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CliLlmDistillationService.CliCommandEnvironmentKey, previous);
        }
    }
}
