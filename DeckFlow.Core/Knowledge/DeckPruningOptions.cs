namespace DeckFlow.Core.Knowledge;

/// <summary>Configures per-commander stored-deck retention.</summary>
public sealed record DeckPruningOptions
{
    /// <summary>Gets the maximum number of retained decks for each commander.</summary>
    public int PerCommanderCap { get; init; } = 100;

    /// <summary>Gets the maximum number of removals in one transaction.</summary>
    public int BatchSize { get; init; } = 25;

    /// <summary>Gets the delay between non-final removal batches.</summary>
    public TimeSpan Pause { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets the free-disk floor below which no removal starts.</summary>
    public long MinFreeDiskBytes { get; init; } = 1_073_741_824L;

    /// <summary>Gets the optional maximum age retained independently of cap.</summary>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>Initializes default retention options.</summary>
    public DeckPruningOptions() { }

    /// <summary>Initializes validated retention options.</summary>
    public DeckPruningOptions(int perCommanderCap, int batchSize, TimeSpan pause, long minFreeDiskBytes, TimeSpan? maxAge = null)
    {
        PerCommanderCap = ValidateCap(perCommanderCap);
        BatchSize = ValidateBatch(batchSize);
        Pause = ValidatePause(pause);
        MinFreeDiskBytes = ValidateFloor(minFreeDiskBytes);
        MaxAge = ValidateMaxAge(maxAge);
    }

    private static int ValidateCap(int value) => value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static int ValidateBatch(int value) => value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static TimeSpan ValidatePause(TimeSpan value) => value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static long ValidateFloor(long value) => value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static TimeSpan? ValidateMaxAge(TimeSpan? value) => !value.HasValue || value.Value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
}
