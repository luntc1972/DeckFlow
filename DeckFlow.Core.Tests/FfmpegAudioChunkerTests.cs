using DeckFlow.Core.Integration;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for the ffmpeg shell-out chunker.
/// </summary>
public sealed class FfmpegAudioChunkerTests
{
    [Fact]
    public void BuildSegmentArguments_UsesSegmentCopyCommand()
    {
        var inputPath = Path.Combine("tmp", "source audio.webm");
        var outputPattern = Path.Combine("tmp", "chunks", "chunk_%04d.webm");

        var arguments = FfmpegAudioChunker.BuildSegmentArguments(inputPath, outputPattern, 300);

        Assert.Equal(
            $"-i \"{inputPath}\" -f segment -segment_time 300 -c copy -reset_timestamps 1 \"{outputPattern}\"",
            arguments);
    }

    [Fact]
    [Trait("Category", "Environment")]
    public async Task IsAvailableAsync_ReportsFalseWhenFfmpegIsAbsent()
    {
        var chunker = new FfmpegAudioChunker();

        var available = await chunker.IsAvailableAsync();

        if (IsFfmpegOnPath())
        {
            Assert.True(available);
        }
        else
        {
            Assert.False(available);
        }
    }

    private static bool IsFfmpegOnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Combine(directory, "ffmpeg")) || File.Exists(Path.Combine(directory, "ffmpeg.exe")));
    }
}
