using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>Specifies free-disk probing for retention pruning.</summary>
public sealed class DeckPruningDiskProbeTests
{
    [Fact]
    public async Task GetFreeBytesAsync_Sqlite_LongestMatchingRootWins()
    {
        var tempPath = Path.GetTempPath();
        var probe = CreateSqliteProbe(Path.Combine(tempPath, "decks", "knowledge.db"), [(Path.GetPathRoot(tempPath)!, 10L), (tempPath, 20L)]);
        Assert.Equal(20L, await probe.GetFreeBytesAsync());
    }

    [Fact]
    public async Task GetFreeBytesAsync_Sqlite_NoMatchingRoot_ReturnsNull()
    {
        var probe = CreateSqliteProbe(Path.Combine(Path.GetTempPath(), "knowledge.db"), [(Path.Combine(Path.GetTempPath(), "other"), 20L)]);
        Assert.Null(await probe.GetFreeBytesAsync());
    }

    [Fact]
    public async Task GetFreeBytesAsync_Sqlite_DrivesThrowIOException_ReturnsNull()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(Path.Combine(Path.GetTempPath(), "knowledge.db"));
        var probe = new DeckPruningDiskProbe(connection, static () => throw new IOException());
        Assert.Null(await probe.GetFreeBytesAsync());
    }

    [Fact]
    public async Task GetFreeBytesAsync_Postgres_ReturnsNullWithoutConnecting()
    {
        var connection = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, "Host=192.0.2.1;Timeout=1");
        var probe = new DeckPruningDiskProbe(connection, static () => throw new InvalidOperationException());
        Assert.Null(await probe.GetFreeBytesAsync());
    }

    private static DeckPruningDiskProbe CreateSqliteProbe(string path, IReadOnlyList<(string RootDirectory, long AvailableFreeBytes)> drives)
        => new(RelationalDatabaseConnection.FromSqlitePath(path), () => drives);
}
