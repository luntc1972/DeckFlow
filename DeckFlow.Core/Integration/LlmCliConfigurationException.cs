namespace DeckFlow.Core.Integration;

/// <summary>
/// Thrown when the CLI distillation service is misconfigured — for example,
/// <c>DECKFLOW_LLM_CLI_COMMAND</c> is not set on Windows, is not a valid JSON array,
/// or is missing the required <c>{instruction}</c> placeholder.
/// </summary>
/// <remarks>
/// This is a configuration error, not a per-video distillation failure. The orchestrator
/// converts it into a single run abort so the operator sees one clear message instead of N
/// "distill failed" lines.
/// </remarks>
public sealed class LlmCliConfigurationException : Exception
{
    /// <summary>
    /// Initializes a new instance with the specified error message.
    /// </summary>
    /// <param name="message">A message that describes the configuration error.</param>
    public LlmCliConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified error message and inner exception.
    /// </summary>
    /// <param name="message">A message that describes the configuration error.</param>
    /// <param name="inner">The exception that caused this configuration error.</param>
    public LlmCliConfigurationException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
