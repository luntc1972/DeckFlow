namespace DeckFlow.Core.Integration;

/// <summary>
/// Splits local audio files into Whisper-sized chunks through a system ffmpeg executable.
/// </summary>
public interface IFfmpegAudioChunker
{
    /// <summary>
    /// Returns whether ffmpeg can be started from the current PATH.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when ffmpeg starts and exits successfully.</returns>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Splits the input file into ordered audio chunks.
    /// </summary>
    /// <param name="inputPath">Input audio path.</param>
    /// <param name="outputDirectory">Directory where chunk files should be written.</param>
    /// <param name="segmentSeconds">Segment length in seconds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered chunk file paths.</returns>
    Task<IReadOnlyList<string>> ChunkAsync(
        string inputPath,
        string outputDirectory,
        int segmentSeconds = 300,
        CancellationToken ct = default);
}
