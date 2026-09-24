namespace DeckFlow.Core.Content;

internal static class CreatorArtifactPathParser
{
    internal static string? GetFolder(string artifactPath)
    {
        var parts = artifactPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^2] : null;
    }
}
