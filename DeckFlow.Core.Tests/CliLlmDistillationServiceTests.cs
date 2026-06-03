using System.Text.Json;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CliLlmDistillationServiceTests
{
    private const string ValidOverride = "[\"wsl.exe\",\"claude\",\"-p\",\"{instruction}\",\"--output-format\",\"json\",\"--allowedTools\",\"\"]";

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
    public async Task Summarize_MissingRequiredField_RetriesThenThrows()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope("""{"not_summary":"x"}"""),
            ClaudeEnvelope("""{"not_summary":"x"}"""),
            ClaudeEnvelope("""{"not_summary":"x"}""")
        ]);
        var service = CreateService(stdout);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WithCommandOverrideAsync(ValidOverride, () => service.SummarizeAsync("transcript")));

        Assert.Contains("failed after 3 attempts", exception.Message, StringComparison.OrdinalIgnoreCase);
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
    public async Task InferTags_VocabInvalidThenValid_RetriesAndSucceeds()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope("""{"archetype":["banana"],"bracket":["Optimized"],"card_category":["draw"]}"""),
            ClaudeEnvelope(ValidTagsJson())
        ]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.InferTagsAsync("transcript"));

        Assert.Equal(["aristocrats"], result.Archetype);
        Assert.Empty(stdout);
    }

    [Fact]
    public async Task InferTags_PersistentVocabInvalid_ThrowsNoSilentEmpty()
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope("""{"archetype":["banana"],"bracket":["Optimized"],"card_category":["draw"]}"""),
            ClaudeEnvelope("""{"archetype":["banana"],"bracket":["Optimized"],"card_category":["draw"]}"""),
            ClaudeEnvelope("""{"archetype":["banana"],"bracket":["Optimized"],"card_category":["draw"]}""")
        ]);
        var service = CreateService(stdout);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WithCommandOverrideAsync(ValidOverride, () => service.InferTagsAsync("transcript")));

        Assert.Contains("failed after 3 attempts", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(stdout);
    }

    [Theory]
    [InlineData("""{"archetype":["aristocrats","ARISTOCRATS"],"bracket":["Optimized"],"card_category":["draw"]}""")]
    [InlineData("""{"archetype":null,"bracket":["Optimized"],"card_category":["draw"]}""")]
    public async Task InferTags_DuplicateOrNullArray_Retries(string invalidJson)
    {
        var stdout = new Queue<string>(
        [
            ClaudeEnvelope(invalidJson),
            ClaudeEnvelope(ValidTagsJson())
        ]);
        var service = CreateService(stdout);

        var result = await WithCommandOverrideAsync(
            ValidOverride,
            () => service.InferTagsAsync("transcript"));

        Assert.Equal(["aristocrats"], result.Archetype);
        Assert.Empty(stdout);
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
    public async Task BuildSpec_OverrideNonJson_ThrowsFastRunnerNotInvoked()
        => await AssertBadOverrideThrowsFastAsync("not json");

    [Fact]
    public async Task BuildSpec_OverrideMissingPlaceholder_ThrowsFastRunnerNotInvoked()
        => await AssertBadOverrideThrowsFastAsync("[\"wsl.exe\",\"claude\",\"-p\"]");

    [Fact]
    public async Task BuildSpec_OverrideDuplicatePlaceholder_ThrowsFastRunnerNotInvoked()
        => await AssertBadOverrideThrowsFastAsync("[\"wsl.exe\",\"claude\",\"-p\",\"{instruction}\",\"{instruction}\"]");

    [Fact]
    public async Task BuildSpec_OverrideEmptyArray_ThrowsFastRunnerNotInvoked()
        => await AssertBadOverrideThrowsFastAsync("[]");

    private static CliLlmDistillationService CreateService(Queue<string> stdoutQueue, TimeSpan? timeout = null)
        => new(
            "claude",
            (_, _, _) => Task.FromResult(stdoutQueue.Dequeue()),
            timeout);

    private static async Task AssertBadOverrideThrowsFastAsync(string overrideValue)
    {
        var invocations = 0;
        var service = new CliLlmDistillationService(
            "claude",
            (_, _, _) =>
            {
                invocations++;
                return Task.FromResult(ClaudeEnvelope("""{"summary":"Should not run."}"""));
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
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
