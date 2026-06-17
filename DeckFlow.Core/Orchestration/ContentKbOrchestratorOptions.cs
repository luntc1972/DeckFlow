namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Carries host-resolved Content KB orchestration settings into the orchestrator without requiring
/// a bare string constructor parameter in DI wiring.
/// </summary>
public sealed record ContentKbOrchestratorOptions
{
    /// <summary>
    /// Gets the artifact root path resolved by the host, such as CLI path helpers or the Studio data directory.
    /// This record only transports the resolved value.
    /// </summary>
    public required string ArtifactRoot { get; init; }
}
