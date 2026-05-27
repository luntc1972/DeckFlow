namespace DeckFlow.Core.Integration;

/// <summary>
/// Resolves the configured YouTube transcript provider.
/// </summary>
public static class TranscriptProviderFactory
{
    /// <summary>
    /// Environment variable used to select the YouTube transcript provider.
    /// </summary>
    public const string EnvironmentVariableName = "DECKFLOW_YOUTUBE_TRANSCRIPT_PROVIDER";

    private const string DirectProvider = "direct";

    /// <summary>
    /// Resolves a YouTube transcript fetcher from the environment toggle.
    /// </summary>
    /// <param name="httpClient">HTTP client for the resolved provider.</param>
    /// <returns>The configured YouTube transcript fetcher.</returns>
    public static IYouTubeTranscriptFetcher Resolve(HttpClient httpClient)
        => Resolve(Environment.GetEnvironmentVariable(EnvironmentVariableName), httpClient);

    /// <summary>
    /// Resolves a YouTube transcript fetcher from a provider value.
    /// </summary>
    /// <param name="providerEnvValue">Provider value read from <see cref="EnvironmentVariableName"/>.</param>
    /// <param name="httpClient">HTTP client for the resolved provider.</param>
    /// <returns>The configured YouTube transcript fetcher.</returns>
    /// <exception cref="NotSupportedException">Thrown when the provider value is unsupported.</exception>
    public static IYouTubeTranscriptFetcher Resolve(string? providerEnvValue, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var provider = string.IsNullOrWhiteSpace(providerEnvValue)
            ? DirectProvider
            : providerEnvValue.Trim();

        if (string.Equals(provider, DirectProvider, StringComparison.OrdinalIgnoreCase))
        {
            return new YouTubeTranscriptFetcher(httpClient);
        }

        throw new NotSupportedException(
            $"Unsupported {EnvironmentVariableName} '{provider}'. Supported: direct.");
    }
}
