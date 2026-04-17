namespace DeckFlow.Web.Services;

public interface IChatGptArtifactsDirectory
{
    /// <summary>
    /// Absolute path to the ChatGPT Analysis artifacts root (honors MTG_DATA_DIR when set).
    /// </summary>
    string RootPath { get; }

    /// <summary>
    /// Enumerates saved analysis folders as commander/timestamp tuples, newest first.
    /// </summary>
    IReadOnlyList<SavedSession> EnumerateSessions();
}

public sealed record SavedSession(string Commander, string Timestamp, string RelativePath, DateTime CreatedUtc);

public sealed class ChatGptArtifactsDirectory : IChatGptArtifactsDirectory
{
    public string RootPath { get; }

    public ChatGptArtifactsDirectory()
    {
        RootPath = ResolveRoot();
    }

    public IReadOnlyList<SavedSession> EnumerateSessions()
    {
        if (!Directory.Exists(RootPath))
        {
            return Array.Empty<SavedSession>();
        }

        var sessions = new List<SavedSession>();
        foreach (var commanderDir in Directory.EnumerateDirectories(RootPath))
        {
            var commander = Path.GetFileName(commanderDir);
            foreach (var timestampDir in Directory.EnumerateDirectories(commanderDir))
            {
                var timestamp = Path.GetFileName(timestampDir);
                var relative = Path.Combine(commander, timestamp);
                var info = new DirectoryInfo(timestampDir);
                sessions.Add(new SavedSession(commander, timestamp, relative, info.CreationTimeUtc));
            }
        }

        return sessions
            .OrderByDescending(session => session.CreatedUtc)
            .ToList();
    }

    private static string ResolveRoot()
    {
        var dataDir = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataDir))
        {
            return Path.Combine(Path.GetFullPath(dataDir), "ChatGPT Analysis");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MTG Deck Studio",
            "ChatGPT Analysis");
    }
}
