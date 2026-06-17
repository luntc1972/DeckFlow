namespace DeckFlow.Studio;

/// <summary>
/// Indicates whether the production Studio connection and SCP artifact-transport have been
/// configured (presence-only; never carries the underlying connection string or SSH values).
/// </summary>
public sealed record StudioConfig(bool IsProdConfigured, bool IsScpConfigured);
