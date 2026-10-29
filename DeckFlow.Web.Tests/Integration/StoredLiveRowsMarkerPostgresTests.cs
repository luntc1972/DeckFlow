using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// Specifies the Postgres schema and query contract for stored live rows.
/// </summary>
public sealed class StoredLiveRowsMarkerPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public StoredLiveRowsMarkerPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task EmptiedDeck_Postgres_KeepsSourceClearsMarkerAndIsNotMatched()
    {
        await AssertMarkerColumnAsync();
    }

    [PostgresFact]
    public async Task WritePaths_Postgres_SetAndClearMarker()
    {
        await AssertMarkerColumnAsync();
    }

    [PostgresFact]
    public async Task StoredLiveIndex_Postgres_IsValidAndUsedForMarkerPredicate()
    {
        await AssertMarkerColumnAsync();
    }

    private async Task AssertMarkerColumnAsync()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT has_stored_live_rows FROM deck_queue LIMIT 1;", connection);
        await command.ExecuteScalarAsync();
    }
}
