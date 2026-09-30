namespace DeckFlow.Core.Content;

/// <summary>Extracts creator folder slugs from artifact paths so identity and visibility updates target the same creator.</summary>
internal static class CreatorArtifactPathParser
{
    internal static string? GetFolder(string artifactPath)
    {
        var parts = artifactPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^2] : null;
    }
}
