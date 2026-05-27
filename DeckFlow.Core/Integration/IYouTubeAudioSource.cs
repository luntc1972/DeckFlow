namespace DeckFlow.Core.Integration;

/// <summary>
/// Downloads YouTube audio to a temporary file without retaining raw audio.
/// </summary>
public interface IYouTubeAudioSource
{
    /// <summary>
    /// Downloads a video's audio stream to a temporary file.
    /// </summary>
    /// <param name="videoUrlOrId">YouTube video URL or id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A temporary audio download result.</returns>
    Task<AudioDownloadResult> DownloadAudioAsync(string videoUrlOrId, CancellationToken ct = default);
}

/// <summary>
/// Temporary YouTube audio download result with a cleanup handle.
/// </summary>
public sealed record AudioDownloadResult : IDisposable
{
    /// <summary>
    /// Temporary audio file path.
    /// </summary>
    public required string TempFilePath { get; init; }

    /// <summary>
    /// Temporary audio filename, including the container extension.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Audio stream size in bytes.
    /// </summary>
    public required long SizeBytes { get; init; }

    /// <summary>
    /// Best-effort duration from video metadata; may be 0 when unavailable. The cap projection uses the max of this value and the lister-supplied knownDuration passed through TranscribeAsync.
    /// </summary>
    public required double DurationSeconds { get; init; }

    /// <summary>
    /// Deletes the temporary audio file and its source-created temp directory when possible.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (File.Exists(TempFilePath))
            {
                File.Delete(TempFilePath);
            }

            DeleteCreatedDirectory();
        }
        catch
        {
        }
    }

    private void DeleteCreatedDirectory()
    {
        var parent = Path.GetDirectoryName(TempFilePath);
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "deckflow-audio");
        var grandparent = Path.GetDirectoryName(parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.Equals(root, grandparent, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.Delete(parent, recursive: false);
    }
}
