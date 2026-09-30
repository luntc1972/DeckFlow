using DeckFlow.Core.Storage;
using Npgsql;

namespace DeckFlow.Core.Tests.Storage;

public sealed class DeadlockRetryTests
{
    [Fact]
    public async Task ExecuteAsync_DeadlockThenSuccess_RetriesWholeBody()
    {
        var attempts = 0;
        await DeadlockRetry.ExecuteAsync(() =>
        {
            if (++attempts < 3)
            {
                throw Error("40P01");
            }

            return Task.CompletedTask;
        }, logger: null);

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_ThreeDeadlocks_Rethrows()
    {
        var attempts = 0;
        await Assert.ThrowsAsync<PostgresException>(() => DeadlockRetry.ExecuteAsync(() =>
        {
            attempts++;
            throw Error("40P01");
        }, logger: null));

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_OtherSqlState_DoesNotRetry()
    {
        var attempts = 0;
        await Assert.ThrowsAsync<PostgresException>(() => DeadlockRetry.ExecuteAsync(() =>
        {
            attempts++;
            throw Error("55P03");
        }, logger: null));

        Assert.Equal(1, attempts);
    }

    private static PostgresException Error(string sqlState) => new("test", "ERROR", "ERROR", sqlState);
}
