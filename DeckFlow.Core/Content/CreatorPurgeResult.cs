namespace DeckFlow.Core.Content;

/// <summary>Aggregate result of a creator-scoped purge.</summary>
public sealed record CreatorPurgeResult(
    string CanonicalSlug,
    IReadOnlyList<CreatorPurgeStoreOutcome> Stores,
    IReadOnlyList<string> ArtifactFolders,
    bool Succeeded);
