using System.Reflection;
using System.Text.Json;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using OpenAI.Chat;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for the pure LLM distillation service.
/// </summary>
public sealed class LlmDistillationServiceTests
{
    [Fact]
    public async Task DistillationMethods_DeserializePayloadsAndAttachCompletionUsage()
    {
        using var httpClient = new HttpClient();
        var completions = new Queue<ChatCompletion>(
        [
            CreateCompletion("""{"summary":"Build around sacrifice payoffs."}""", inputTokens: 101, outputTokens: 11),
            CreateCompletion(
                """{"clips":[{"timestamp_seconds":120,"excerpt":"Use the commander as a draw engine."},{"timestamp_seconds":null,"excerpt":"Protect the combo turn."},{"timestamp_seconds":480,"excerpt":"Close with a sacrifice loop."}]}""",
                inputTokens: 202,
                outputTokens: 22),
            CreateCompletion(
                """{"archetype":["aristocrats"],"bracket":["Optimized"],"card_category":["draw","win-cons"]}""",
                inputTokens: 303,
                outputTokens: 33)
        ]);
        var service = CreateService(httpClient, completions);

        var summary = await service.SummarizeAsync("transcript");
        var clips = await service.ExtractClipsAsync("transcript");
        var tags = await service.InferTagsAsync("transcript");

        Assert.Equal("Build around sacrifice payoffs.", summary.Summary);
        Assert.Equal(new TokenUsage(101, 11), summary.Usage);
        Assert.Collection(
            clips.Clips,
            clip =>
            {
                Assert.Equal(120, clip.TimestampSeconds);
                Assert.Equal("Use the commander as a draw engine.", clip.Excerpt);
            },
            clip =>
            {
                Assert.Null(clip.TimestampSeconds);
                Assert.Equal("Protect the combo turn.", clip.Excerpt);
            },
            clip =>
            {
                Assert.Equal(480, clip.TimestampSeconds);
                Assert.Equal("Close with a sacrifice loop.", clip.Excerpt);
            });
        Assert.Equal(new TokenUsage(202, 22), clips.Usage);
        Assert.Equal(["aristocrats"], tags.Archetype);
        Assert.Equal(["Optimized"], tags.Bracket);
        Assert.Equal(["draw", "win-cons"], tags.CardCategory);
        Assert.Equal(new TokenUsage(303, 33), tags.Usage);
        Assert.Empty(completions);
    }

    [Fact]
    public async Task SummarizeAsync_ThrowsWhenCompletionRefuses()
    {
        using var httpClient = new HttpClient();
        var completions = new Queue<ChatCompletion>(
        [
            CreateCompletion("""{"summary":""}""", refusal: "refused")
        ]);
        var service = CreateService(httpClient, completions);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SummarizeAsync("transcript"));

        Assert.Contains("refused", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractClipsAsync_ThrowsWhenCompletionIsTruncated()
    {
        using var httpClient = new HttpClient();
        var completions = new Queue<ChatCompletion>(
        [
            CreateCompletion("""{"clips":[""", finishReason: ChatFinishReason.Length)
        ]);
        var service = CreateService(httpClient, completions);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExtractClipsAsync("transcript"));

        Assert.Contains("truncated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InferTagsAsync_ThrowsWhenJsonIsGarbage()
    {
        using var httpClient = new HttpClient();
        var completions = new Queue<ChatCompletion>(
        [
            CreateCompletion("not json")
        ]);
        var service = CreateService(httpClient, completions);

        await Assert.ThrowsAsync<JsonException>(() => service.InferTagsAsync("transcript"));
    }

    [Fact]
    public async Task InferTagsAsync_ThrowsWhenJsonDeserializesToNull()
    {
        using var httpClient = new HttpClient();
        var completions = new Queue<ChatCompletion>(
        [
            CreateCompletion("null")
        ]);
        var service = CreateService(httpClient, completions);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.InferTagsAsync("transcript"));

        Assert.Contains("deserialized to null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SummarizeAsync_TruncatesLongSummary()
    {
        using var httpClient = new HttpClient();
        var longSummary = string.Join(" ", Enumerable.Range(1, 205).Select(index => $"word{index}"));
        var completions = new Queue<ChatCompletion>(
        [
            CreateCompletion($$"""{"summary":"{{longSummary}}"}""")
        ]);
        var service = CreateService(httpClient, completions);

        var result = await service.SummarizeAsync("transcript");

        Assert.Equal(200, DistillationValidation.CountWords(result.Summary));
        Assert.DoesNotContain("word201", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractClipsAsync_SanitizesNegativeTimestampAndOverlongList()
    {
        using var httpClient = new HttpClient();
        var completions = new Queue<ChatCompletion>(
        [
            CreateCompletion(
                """{"clips":[{"timestamp_seconds":-5,"excerpt":"drop"},{"timestamp_seconds":10,"excerpt":"1"},{"timestamp_seconds":20,"excerpt":"2"},{"timestamp_seconds":30,"excerpt":"3"},{"timestamp_seconds":40,"excerpt":"4"},{"timestamp_seconds":50,"excerpt":"5"},{"timestamp_seconds":60,"excerpt":"6"},{"timestamp_seconds":70,"excerpt":"7"},{"timestamp_seconds":80,"excerpt":"8"},{"timestamp_seconds":90,"excerpt":"9"}]}""")
        ]);
        var service = CreateService(httpClient, completions);

        var result = await service.ExtractClipsAsync("transcript");

        Assert.Equal(8, result.Clips.Count);
        Assert.Equal([10, 20, 30, 40, 50, 60, 70, 80], result.Clips.Select(clip => clip.TimestampSeconds).ToArray());
    }

    [Fact]
    public async Task InferTagsAsync_SanitizesUnknownDuplicateAndNullArrays()
    {
        using var httpClient = new HttpClient();
        var completions = new Queue<ChatCompletion>(
        [
            CreateCompletion("""{"archetype":["banana","Aristocrats","ARISTOCRATS","tokens"],"bracket":null,"card_category":["artifacts","draw","DRAW","ramp"]}""")
        ]);
        var service = CreateService(httpClient, completions);

        var result = await service.InferTagsAsync("transcript");

        Assert.Equal(["aristocrats", "tokens"], result.Archetype);
        Assert.Empty(result.Bracket);
        Assert.Equal(["draw", "ramp"], result.CardCategory);
    }

    [Fact]
    public void ConstructorSurface_IsPureAdapterWithoutPersistenceDependencies()
    {
        var publicConstructor = Assert.Single(typeof(LlmDistillationService).GetConstructors());
        var parameter = Assert.Single(publicConstructor.GetParameters());

        Assert.Equal(typeof(HttpClient), parameter.ParameterType);
        Assert.DoesNotContain(
            typeof(LlmDistillationService).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(constructor => constructor.GetParameters()),
            parameterInfo => parameterInfo.ParameterType.Name.Contains("Ledger", StringComparison.Ordinal)
                || parameterInfo.ParameterType.Name.Contains("Store", StringComparison.Ordinal));
    }

    private static LlmDistillationService CreateService(HttpClient httpClient, Queue<ChatCompletion> completions)
        => new(httpClient, (messages, options, cancellationToken) => Task.FromResult(completions.Dequeue()));

#pragma warning disable OPENAI001
    private static ChatCompletion CreateCompletion(
        string text,
        ChatFinishReason finishReason = ChatFinishReason.Stop,
        string? refusal = null,
        int inputTokens = 10,
        int outputTokens = 5)
        => OpenAIChatModelFactory.ChatCompletion(
            finishReason: finishReason,
            content: new ChatMessageContent(text),
            refusal: refusal,
            role: ChatMessageRole.Assistant,
            model: "gpt-4o-mini",
            usage: OpenAIChatModelFactory.ChatTokenUsage(
                outputTokenCount: outputTokens,
                inputTokenCount: inputTokens,
                totalTokenCount: inputTokens + outputTokens));
#pragma warning restore OPENAI001
}
