using DeckFlow.Core.Storage;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Knowledge;

/// <summary>Reads available SQLite-host disk space for retention pruning.</summary>
public sealed class DeckPruningDiskProbe
{
    private readonly RelationalDatabaseConnection _connection;
    private readonly Func<IReadOnlyList<(string RootDirectory, long AvailableFreeBytes)>> _drives;
    private readonly ILogger? _logger;

    /// <summary>Initializes a disk probe backed by system drive information.</summary>
    public DeckPruningDiskProbe(RelationalDatabaseConnection connection, ILogger? logger = null)
        : this(connection, static () => DriveInfo.GetDrives().Select(drive => (drive.RootDirectory.FullName, drive.AvailableFreeSpace)).ToArray(), logger)
    {
    }

    internal DeckPruningDiskProbe(RelationalDatabaseConnection connection, Func<IReadOnlyList<(string RootDirectory, long AvailableFreeBytes)>> drives, ILogger? logger = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _drives = drives ?? throw new ArgumentNullException(nameof(drives));
        _logger = logger;
    }

    /// <summary>Gets bytes free on the drive containing the SQLite database, when available.</summary>
    public Task<long?> GetFreeBytesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_connection.IsPostgres)
        {
            _logger?.LogWarning("Retention free-disk probe is unavailable for PostgreSQL.");
            return Task.FromResult<long?>(null);
        }

        try
        {
            var databasePath = _connection.ExtractSqlitePath();
            var drive = _drives()
                .Where(drive => databasePath.StartsWith(drive.RootDirectory, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(drive => drive.RootDirectory.Length)
                .FirstOrDefault();
            return Task.FromResult(drive.RootDirectory is null ? (long?)null : drive.AvailableFreeBytes);
        }
        catch (IOException exception)
        {
            _logger?.LogWarning(exception, "Retention free-disk probe failed for SQLite.");
            return Task.FromResult<long?>(null);
        }
    }
}
