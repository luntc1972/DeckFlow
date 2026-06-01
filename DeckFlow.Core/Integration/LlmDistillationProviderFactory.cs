namespace DeckFlow.Core.Integration;

/// <summary>
/// Resolves the configured LLM distillation provider.
/// </summary>
public static class LlmDistillationProviderFactory
{
    /// <summary>
    /// Environment variable used to select the LLM distillation provider.
    /// </summary>
    public const string EnvironmentVariableName = "DECKFLOW_LLM_PROVIDER";

    private const string OpenAiProvider = "openai";
    private const string ClaudeProvider = "claude";
    private const string CodexProvider = "codex";

    /// <summary>
    /// Resolves an LLM distillation service from the environment toggle.
    /// </summary>
    /// <param name="httpClient">HTTP client for the OpenAI provider.</param>
    /// <returns>The configured LLM distillation service.</returns>
    public static ILlmDistillationService Resolve(HttpClient httpClient)
        => Resolve(Environment.GetEnvironmentVariable(EnvironmentVariableName), httpClient);

    /// <summary>
    /// Resolves an LLM distillation service from a provider value.
    /// </summary>
    /// <param name="providerEnvValue">Provider value read from <see cref="EnvironmentVariableName" />.</param>
    /// <param name="httpClient">HTTP client for the OpenAI provider.</param>
    /// <returns>The configured LLM distillation service.</returns>
    /// <exception cref="NotSupportedException">Thrown when the provider value is unsupported.</exception>
    public static ILlmDistillationService Resolve(string? providerEnvValue, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var provider = string.IsNullOrWhiteSpace(providerEnvValue)
            ? OpenAiProvider
            : providerEnvValue.Trim();

        if (string.Equals(provider, OpenAiProvider, StringComparison.OrdinalIgnoreCase))
        {
            return new LlmDistillationService(httpClient);
        }

        if (string.Equals(provider, ClaudeProvider, StringComparison.OrdinalIgnoreCase))
        {
            return new CliLlmDistillationService(ClaudeProvider);
        }

        if (string.Equals(provider, CodexProvider, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                "The codex LLM distillation provider is deferred to Phase 21.3 / KB-12 and is not yet supported.");
        }

        throw new NotSupportedException(
            $"Unsupported {EnvironmentVariableName} '{provider}'. Supported: openai, claude.");
    }
}
