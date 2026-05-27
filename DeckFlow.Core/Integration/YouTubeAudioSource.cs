using Polly;
using Polly.Retry;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.Streams;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Downloads YouTube audio-only streams to temporary files.
/// </summary>
public sealed class YouTubeAudioSource : IYouTubeAudioSource
{
    private static readonly AsyncRetryPolicy RetryPolicy = Policy
        .Handle<HttpRequestException>()
        .Or<TaskCanceledException>()
        .Or<YoutubeExplodeException>()
        .WaitAndRetryAsync(
            retryCount: 6,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250)),
            onRetry: (exception, timespan, retryAttempt, context) => { });

    private readonly Func<string, CancellationToken, Task<AudioDownloadResult>> _executeAsync;

    /// <summary>
    /// Initializes a YouTube audio source with an injected HTTP client.
    /// </summary>
    /// <param name="httpClient">HTTP client used by YoutubeExplode.</param>
    public YouTubeAudioSource(HttpClient httpClient)
        : this(CreateExecuteAsync(httpClient))
    {
    }

    /// <summary>
    /// Initializes a YouTube audio source with a delegate seam for tests.
    /// </summary>
    /// <param name="executeAsync">Audio download delegate.</param>
    internal YouTubeAudioSource(Func<string, CancellationToken, Task<AudioDownloadResult>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
    }

    /// <summary>
    /// Downloads audio for a YouTube video id or URL.
    /// </summary>
    /// <param name="videoUrlOrId">YouTube video URL or id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A temporary audio download result.</returns>
    public Task<AudioDownloadResult> DownloadAudioAsync(string videoUrlOrId, CancellationToken ct = default)
        => _executeAsync(videoUrlOrId, ct);

    private static Func<string, CancellationToken, Task<AudioDownloadResult>> CreateExecuteAsync(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var youtube = new YoutubeClient(httpClient);

        return (videoUrlOrId, ct) => RetryPolicy.ExecuteAsync(
            token => DownloadWithClientAsync(youtube, videoUrlOrId, token),
            ct);
    }

    private static async Task<AudioDownloadResult> DownloadWithClientAsync(
        YoutubeClient youtube,
        string videoUrlOrId,
        CancellationToken ct)
    {
        var manifest = await youtube.Videos.Streams.GetManifestAsync(videoUrlOrId, ct).ConfigureAwait(false);
        var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        var tempPath = CreateTempPath(streamInfo.Container.Name);

        await youtube.Videos.Streams.DownloadAsync(streamInfo, tempPath, progress: null, ct).ConfigureAwait(false);

        return new AudioDownloadResult
        {
            TempFilePath = tempPath,
            FileName = Path.GetFileName(tempPath),
            SizeBytes = streamInfo.Size.Bytes,
            DurationSeconds = GetBestEffortDurationSeconds(streamInfo),
        };
    }

    private static string CreateTempPath(string containerName)
    {
        var extension = string.IsNullOrWhiteSpace(containerName)
            ? ".tmp"
            : "." + containerName.TrimStart('.');
        var tempDir = Path.Combine(Path.GetTempPath(), "deckflow-audio", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return Path.Combine(tempDir, "audio" + extension);
    }

    private static double GetBestEffortDurationSeconds(IStreamInfo streamInfo)
        => 0;
}
