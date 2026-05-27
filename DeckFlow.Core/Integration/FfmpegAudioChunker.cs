using System.Diagnostics;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Shells out to ffmpeg to split large audio files into fixed-duration chunks.
/// </summary>
public sealed class FfmpegAudioChunker : IFfmpegAudioChunker
{
    private const string FfmpegExecutable = "ffmpeg";
    private const int ErrorTailLength = 800;

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo(FfmpegExecutable)
            {
                Arguments = "-version",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ChunkAsync(
        string inputPath,
        string outputDirectory,
        int segmentSeconds = 300,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(segmentSeconds, 0);

        Directory.CreateDirectory(outputDirectory);
        var extension = Path.GetExtension(inputPath);
        var outputPattern = Path.Combine(outputDirectory, "chunk_%04d" + extension);
        var startInfo = new ProcessStartInfo(FfmpegExecutable)
        {
            Arguments = BuildSegmentArguments(inputPath, outputPattern, segmentSeconds),
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("ffmpeg process failed to start.");
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}: {Tail(stderr)}");
        }

        return Directory.GetFiles(outputDirectory, "chunk_*" + extension)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    internal static string BuildSegmentArguments(string inputPath, string outputPattern, int segmentSeconds)
        => $"-i \"{inputPath}\" -f segment -segment_time {segmentSeconds} -c copy -reset_timestamps 1 \"{outputPattern}\"";

    private static string Tail(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= ErrorTailLength)
        {
            return text;
        }

        return text[^ErrorTailLength..];
    }
}
