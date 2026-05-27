using DeckFlow.Core.Integration;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for the YouTube audio source delegate seam and cleanup handle.
/// </summary>
public sealed class YouTubeAudioSourceTests
{
    [Fact]
    public async Task DownloadAudioAsync_UsesDelegateSeam()
    {
        var tempPath = CreateTempAudioFile();
        var source = new YouTubeAudioSource((videoUrlOrId, ct) =>
        {
            Assert.Equal("video-1", videoUrlOrId);
            return Task.FromResult(new AudioDownloadResult
            {
                TempFilePath = tempPath,
                FileName = "audio.webm",
                SizeBytes = 5,
                DurationSeconds = 0,
            });
        });

        using var result = await source.DownloadAudioAsync("video-1");

        Assert.Equal(tempPath, result.TempFilePath);
        Assert.Equal("audio.webm", result.FileName);
        Assert.Equal(5, result.SizeBytes);
        Assert.Equal(0, result.DurationSeconds);
    }

    [Fact]
    public void GetBestEffortDurationSeconds_ReturnsMetadataDurationSeconds()
    {
        var seconds = YouTubeAudioSource.GetBestEffortDurationSeconds(TimeSpan.FromMinutes(3.5));

        Assert.Equal(210d, seconds);
    }

    [Fact]
    public void Dispose_DeletesTempFileAndCreatedDirectory()
    {
        var tempPath = CreateTempAudioFile();
        var tempDir = Path.GetDirectoryName(tempPath);
        var result = new AudioDownloadResult
        {
            TempFilePath = tempPath,
            FileName = "audio.webm",
            SizeBytes = 5,
            DurationSeconds = 0,
        };

        result.Dispose();

        Assert.False(File.Exists(tempPath));
        Assert.NotNull(tempDir);
        Assert.False(Directory.Exists(tempDir));
    }

    private static string CreateTempAudioFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "deckflow-audio", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, "audio.webm");
        File.WriteAllText(tempPath, "audio");
        return tempPath;
    }
}
