using System.Diagnostics;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

public sealed class CategoryLookupTimeoutPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public CategoryLookupTimeoutPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task GetCategoriesForNamesAsync_TableLockedPastTimeout_ThrowsNonCancellationWithinTimeout()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var repository = new CategoryKnowledgeRepository(
            new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));

        await repository.GetCategoriesForNamesAsync(new[] { "Sol Ring" });

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var lockAcquired = false;
            var lookupTask = repository.GetCategoriesForNamesAsync(
                new[] { "Sol Ring" },
                (_, _, _) =>
                {
                    using var lockCommand = new NpgsqlCommand(
                        "LOCK TABLE cards, card_category_qualified IN ACCESS EXCLUSIVE MODE",
                        connection,
                        transaction);
                    lockCommand.ExecuteNonQuery();
                    lockAcquired = true;
                });
            var completedTask = await Task.WhenAny(lookupTask, Task.Delay(TimeSpan.FromSeconds(15)));
            stopwatch.Stop();

            Assert.True(lockAcquired);
            Assert.Same(lookupTask, completedTask);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
                $"Lookup took {stopwatch.Elapsed}.");

            var exception = Assert.ThrowsAny<Exception>(() => lookupTask.GetAwaiter().GetResult());
            var baseException = exception is AggregateException aggregate
                ? aggregate.GetBaseException()
                : exception;
            // Why: fail-open rethrows any OperationCanceledException subtype (e.g. TaskCanceledException).
            Assert.False(baseException is OperationCanceledException, $"Unexpected {baseException.GetType()}.");
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }
}
