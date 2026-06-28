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
    /// Whether the given <see cref="EnvironmentVariableName" /> value selects a subscription (flat-rate
    /// CLI) provider rather than the metered OpenAI default. A subscription provider is any non-empty
    /// value other than <c>openai</c> (an unset/blank value defaults to metered OpenAI, see
    /// <see cref="Resolve(string?, HttpClient)" />). Single source of truth for this rule, shared by
    /// the Studio host and the CLI so the resolved distiller and the metered/subscription spend flag
    /// can never disagree (HIGH-1 / D-01).
    /// </summary>
    /// <param name="providerEnvValue">Provider value read from <see cref="EnvironmentVariableName" />.</param>
    /// <returns><see langword="true" /> when the provider is a subscription provider.</returns>
    public static bool IsSubscriptionProvider(string? providerEnvValue)
        => !string.IsNullOrWhiteSpace(providerEnvValue)
            && !string.Equals(providerEnvValue.Trim(), OpenAiProvider, StringComparison.OrdinalIgnoreCase);

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
