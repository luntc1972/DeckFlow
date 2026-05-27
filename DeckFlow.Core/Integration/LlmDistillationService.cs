using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using DeckFlow.Core.Knowledge;
using OpenAI;
using OpenAI.Chat;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Pure OpenAI chat adapter for transcript summary, clip, and tag distillation.
/// </summary>
public sealed class LlmDistillationService : ILlmDistillationService
{
    private const string Model = "gpt-4o-mini";
    private const int SummaryMaxOutputTokens = 400;
    private const int ClipsMaxOutputTokens = 1200;
    private const int TagsMaxOutputTokens = 200;
    private const int SummaryMaxWords = 200;
    private const int MinClipCount = 3;
    private const int MaxClipCount = 8;
    private const string OpenAiApiKeyEnvironmentKey = "OPENAI_API_KEY";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Func<ChatMessage[], ChatCompletionOptions, CancellationToken, Task<ChatCompletion>> _completeChatAsync;

    /// <summary>
    /// Initializes a distillation service with production OpenAI chat completion.
    /// </summary>
    /// <param name="httpClient">HTTP client used by the OpenAI SDK transport.</param>
    public LlmDistillationService(HttpClient httpClient)
        : this(httpClient, completeChatAsyncOverride: null)
    {
    }

    internal LlmDistillationService(
        HttpClient httpClient,
        Func<ChatMessage[], ChatCompletionOptions, CancellationToken, Task<ChatCompletion>>? completeChatAsyncOverride)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _completeChatAsync = completeChatAsyncOverride ?? CreateProductionCompleter(httpClient);
    }

    /// <inheritdoc />
    public async Task<SummaryResult> SummarizeAsync(
        string transcript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

        var extracted = await ExtractJsonAsync<SummaryPayload>(
            transcript,
            SummarySystemPrompt,
            "summary",
            DistillationSchemas.SummarySchema,
            SummaryMaxOutputTokens,
            cancellationToken).ConfigureAwait(false);
        ValidateSummary(extracted.Payload.Summary);

        return new SummaryResult(extracted.Payload.Summary, extracted.Usage);
    }

    /// <inheritdoc />
    public async Task<ClipsResult> ExtractClipsAsync(
        string transcript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

        var extracted = await ExtractJsonAsync<ClipsPayload>(
            transcript,
            ClipsSystemPrompt,
            "clips",
            DistillationSchemas.ClipsSchema,
            ClipsMaxOutputTokens,
            cancellationToken).ConfigureAwait(false);
        ValidateClips(extracted.Payload.Clips);

        return new ClipsResult(extracted.Payload.Clips, extracted.Usage);
    }

    /// <inheritdoc />
    public async Task<TagsResult> InferTagsAsync(
        string transcript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

        var extracted = await ExtractJsonAsync<TagsPayload>(
            transcript,
            TagsSystemPrompt,
            "tags",
            DistillationSchemas.TagsSchema,
            TagsMaxOutputTokens,
            cancellationToken).ConfigureAwait(false);

        return new TagsResult(
            extracted.Payload.Archetype,
            extracted.Payload.Bracket,
            extracted.Payload.CardCategory,
            extracted.Usage);
    }

    private async Task<(T Payload, TokenUsage Usage)> ExtractJsonAsync<T>(
        string transcript,
        string systemPrompt,
        string schemaName,
        string jsonSchema,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        var options = new ChatCompletionOptions
        {
            Temperature = 0f,
            MaxOutputTokenCount = maxOutputTokens,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: schemaName,
                jsonSchema: BinaryData.FromString(jsonSchema),
                jsonSchemaIsStrict: true),
        };
        ChatMessage[] messages = [new SystemChatMessage(systemPrompt), new UserChatMessage(transcript)];
        var completion = await _completeChatAsync(messages, options, cancellationToken).ConfigureAwait(false);
        var json = ReadJsonContent(completion);
        var payload = JsonSerializer.Deserialize<T>(json, JsonOpts)
            ?? throw new InvalidOperationException($"{schemaName} JSON deserialized to null.");

        return (payload, ReadUsage(completion));
    }

    private static string ReadJsonContent(ChatCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);

        if (!string.IsNullOrEmpty(completion.Refusal))
        {
            throw new InvalidOperationException($"Model refused: {completion.Refusal}");
        }

        if (completion.FinishReason == ChatFinishReason.Length)
        {
            throw new InvalidOperationException("Output truncated because MaxOutputTokenCount was reached.");
        }

        if (completion.FinishReason != ChatFinishReason.Stop)
        {
            throw new InvalidOperationException($"Unexpected finish reason: {completion.FinishReason}.");
        }

        if (completion.Content.Count == 0)
        {
            throw new InvalidOperationException("Model returned no JSON content.");
        }

        var text = completion.Content[0].Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Model returned empty JSON content.");
        }

        return text;
    }

    private static TokenUsage ReadUsage(ChatCompletion completion)
    {
        var usage = completion.Usage
            ?? throw new InvalidOperationException("Completion did not include token usage.");
        return new TokenUsage(usage.InputTokenCount, usage.OutputTokenCount);
    }

    private static Func<ChatMessage[], ChatCompletionOptions, CancellationToken, Task<ChatCompletion>> CreateProductionCompleter(
        HttpClient httpClient)
        => async (messages, options, cancellationToken) =>
        {
            var chatClient = CreateChatClient(httpClient, ReadApiKey());
            ClientResult<ChatCompletion> result = await chatClient
                .CompleteChatAsync(messages, options, cancellationToken)
                .ConfigureAwait(false);
            return result.Value;
        };

    private static ChatClient CreateChatClient(HttpClient httpClient, string apiKey)
    {
        var options = new OpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(httpClient),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            NetworkTimeout = Timeout.InfiniteTimeSpan,
        };

        return new ChatClient(Model, new ApiKeyCredential(apiKey), options);
    }

    private static string ReadApiKey()
    {
        var apiKey = Environment.GetEnvironmentVariable(OpenAiApiKeyEnvironmentKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"{OpenAiApiKeyEnvironmentKey} is not set.");
        }

        return apiKey;
    }

    private static void ValidateSummary(string summary)
    {
        if (CountWords(summary) > SummaryMaxWords)
        {
            throw new InvalidOperationException("Summary exceeded the 200-word limit.");
        }
    }

    private static int CountWords(string text)
        => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static void ValidateClips(IReadOnlyList<ClipItem> clips)
    {
        if (clips.Count is < MinClipCount or > MaxClipCount)
        {
            throw new InvalidOperationException("Clip extraction must return 3 to 8 clips.");
        }

        if (clips.Any(clip => clip.TimestampSeconds < 0))
        {
            throw new InvalidOperationException("Clip timestamps cannot be negative.");
        }
    }

    private static string FormatAllowlist(IReadOnlySet<string> values)
        => string.Join(", ", values);

    private static string SummarySystemPrompt { get; } = """
        You extract grounded strategy summaries from Magic: The Gathering video transcripts.
        Output only JSON matching the supplied schema.
        Keep the summary 200 words or fewer, plain prose, and grounded only in the transcript.
        """;

    private static string ClipsSystemPrompt { get; } = """
        You extract 3 to 8 useful key clips from Magic: The Gathering video transcripts.
        Output only JSON matching the supplied schema.
        Use timestamp_seconds only when the transcript provides a defensible time; otherwise use null.
        Excerpts must be grounded only in the transcript.
        """;

    private static string TagsSystemPrompt { get; } =
        "You infer candidate Content KB tags from Magic: The Gathering video transcripts. "
        + "Output only JSON matching the supplied schema. "
        + "Choose only from these allowlists. "
        + $"Archetype: {FormatAllowlist(ContentTagVocabulary.Archetypes)}. "
        + $"Bracket: {FormatAllowlist(ContentTagVocabulary.Brackets)}. "
        + $"Card category: {FormatAllowlist(ContentTagVocabulary.CardCategories)}.";

    private sealed record SummaryPayload(string Summary);

    private sealed record ClipsPayload(IReadOnlyList<ClipItem> Clips);

    private sealed record TagsPayload(
        IReadOnlyList<string> Archetype,
        IReadOnlyList<string> Bracket,
        IReadOnlyList<string> CardCategory);
}
