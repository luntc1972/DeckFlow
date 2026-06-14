using System.Globalization;
using Dapper;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// Verifies the active Dapper type handlers round-trip through SQLite and Postgres.
/// </summary>
public sealed class DapperTypeHandlerRoundTripTests : IClassFixture<PostgresContainerFixture>, IDisposable
{
    private readonly PostgresContainerFixture _fixture;
    private readonly List<string> _sqliteDirectories = new();

    public DapperTypeHandlerRoundTripTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DateTimeHandler_RoundTrips_OnSqlite_WithRawWritePathProof()
    {
        var connectionInfo = CreateSqliteConnection();
        var value = TrimToSecondUtc(DateTime.UtcNow);

        await using var connection = await connectionInfo.OpenConnectionAsync();
        await connection.ExecuteAsync("CREATE TABLE handler_roundtrip(value TEXT NOT NULL);");
        await connection.ExecuteAsync("INSERT INTO handler_roundtrip(value) VALUES (@value);", new { value });

        var raw = await ReadRawSqliteValueAsync(connectionInfo.ExtractSqlitePath(), "SELECT value FROM handler_roundtrip LIMIT 1;");
        Assert.Equal(typeof(string), raw.FieldType);
        Assert.Equal(value.ToString("O", CultureInfo.InvariantCulture), Assert.IsType<string>(raw.Value));

        var roundTrip = await connection.QuerySingleAsync<DateTime>("SELECT value FROM handler_roundtrip LIMIT 1;");
        Assert.Equal(DateTimeKind.Utc, roundTrip.Kind);
        Assert.Equal(value, roundTrip);
    }

    [PostgresFact]
    public async Task DateTimeHandler_RoundTrips_OnPostgres()
    {
        var connectionInfo = await CreatePostgresConnectionAsync();
        var value = TrimToSecondUtc(DateTime.UtcNow);

        await using var connection = await connectionInfo.OpenConnectionAsync();
        await connection.ExecuteAsync("CREATE TEMP TABLE handler_roundtrip(value TIMESTAMPTZ NOT NULL);");
        await connection.ExecuteAsync("INSERT INTO handler_roundtrip(value) VALUES (@value);", new { value });

        var roundTrip = await connection.QuerySingleAsync<DateTime>("SELECT value FROM handler_roundtrip LIMIT 1;");
        Assert.Equal(DateTimeKind.Utc, roundTrip.Kind);
        Assert.Equal(value, TrimToSecondUtc(roundTrip));
    }

    [Fact]
    public async Task DecimalHandler_RoundTrips_OnSqlite_WithRawWritePathProof()
    {
        var connectionInfo = CreateSqliteConnection();
        const decimal value = 12345.678901m;

        await using var connection = await connectionInfo.OpenConnectionAsync();
        await connection.ExecuteAsync("CREATE TABLE handler_roundtrip(value TEXT NOT NULL);");
        await connection.ExecuteAsync("INSERT INTO handler_roundtrip(value) VALUES (@value);", new { value });

        var raw = await ReadRawSqliteValueAsync(connectionInfo.ExtractSqlitePath(), "SELECT value FROM handler_roundtrip LIMIT 1;");
        Assert.Equal(typeof(string), raw.FieldType);
        Assert.Equal(value.ToString(CultureInfo.InvariantCulture), Assert.IsType<string>(raw.Value));

        var roundTrip = await connection.QuerySingleAsync<decimal>("SELECT value FROM handler_roundtrip LIMIT 1;");
        Assert.Equal(value, roundTrip);
    }

    [PostgresFact]
    public async Task DecimalHandler_RoundTrips_OnPostgres()
    {
        var connectionInfo = await CreatePostgresConnectionAsync();
        const decimal value = 12345.678901m;

        await using var connection = await connectionInfo.OpenConnectionAsync();
        await connection.ExecuteAsync("CREATE TEMP TABLE handler_roundtrip(value NUMERIC NOT NULL);");
        await connection.ExecuteAsync("INSERT INTO handler_roundtrip(value) VALUES (@value);", new { value });

        var roundTrip = await connection.QuerySingleAsync<decimal>("SELECT value FROM handler_roundtrip LIMIT 1;");
        Assert.Equal(value, roundTrip);
    }

    [Fact]
    public async Task BoolHandler_RoundTrips_OnSqlite_WithRawWritePathProof()
    {
        var trueConnectionInfo = CreateSqliteConnection();
        await AssertSqliteBoolRoundTripAsync(trueConnectionInfo, true, 1L);

        var falseConnectionInfo = CreateSqliteConnection();
        await AssertSqliteBoolRoundTripAsync(falseConnectionInfo, false, 0L);
    }

    [PostgresFact]
    public async Task BoolHandler_RoundTrips_OnPostgres()
    {
        var connectionInfo = await CreatePostgresConnectionAsync();

        await using var connection = await connectionInfo.OpenConnectionAsync();
        await connection.ExecuteAsync("CREATE TEMP TABLE handler_roundtrip(value BOOLEAN NOT NULL);");
        await connection.ExecuteAsync("INSERT INTO handler_roundtrip(value) VALUES (@trueValue), (@falseValue);", new { trueValue = true, falseValue = false });

        var roundTrips = (await connection.QueryAsync<bool>("SELECT value FROM handler_roundtrip ORDER BY value DESC;")).ToList();
        Assert.Equal(new[] { true, false }, roundTrips);
    }

    [Fact]
    public async Task GuidHandler_RoundTrips_OnSqlite_WithRawWritePathProof()
    {
        var connectionInfo = CreateSqliteConnection();
        var value = Guid.NewGuid();

        await using var connection = await connectionInfo.OpenConnectionAsync();
        await connection.ExecuteAsync("CREATE TABLE handler_roundtrip(value TEXT NOT NULL);");
        await connection.ExecuteAsync("INSERT INTO handler_roundtrip(value) VALUES (@value);", new { value });

        var raw = await ReadRawSqliteValueAsync(connectionInfo.ExtractSqlitePath(), "SELECT value FROM handler_roundtrip LIMIT 1;");
        Assert.Equal(typeof(string), raw.FieldType);
        Assert.Equal(value.ToString(), Assert.IsType<string>(raw.Value));

        var roundTrip = await connection.QuerySingleAsync<Guid>("SELECT value FROM handler_roundtrip LIMIT 1;");
        Assert.Equal(value, roundTrip);
    }

    [PostgresFact]
    public async Task GuidHandler_RoundTrips_OnPostgres()
    {
        var connectionInfo = await CreatePostgresConnectionAsync();
        var value = Guid.NewGuid();

        await using var connection = await connectionInfo.OpenConnectionAsync();
        await connection.ExecuteAsync("CREATE TEMP TABLE handler_roundtrip(value UUID NOT NULL);");
        await connection.ExecuteAsync("INSERT INTO handler_roundtrip(value) VALUES (@value);", new { value });

        var roundTrip = await connection.QuerySingleAsync<Guid>("SELECT value FROM handler_roundtrip LIMIT 1;");
        Assert.Equal(value, roundTrip);
    }

    // 49-02: add DateTimeOffset SQLite and Postgres round-trip coverage when
    // DateTimeOffsetTypeHandler becomes the sanctioned fifth registered handler.

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        foreach (var directory in _sqliteDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static DateTime TrimToSecondUtc(DateTime value)
    {
        var utc = value.ToUniversalTime();
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Utc);
    }

    private RelationalDatabaseConnection CreateSqliteConnection()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _sqliteDirectories.Add(directory);
        return RelationalDatabaseConnection.FromSqlitePath(Path.Combine(directory, "handler-roundtrip.db"));
    }

    private async Task<RelationalDatabaseConnection> CreatePostgresConnectionAsync()
        => new(RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync());

    private static async Task AssertSqliteBoolRoundTripAsync(
        RelationalDatabaseConnection connectionInfo,
        bool value,
        long expectedRawValue)
    {
        await using var connection = await connectionInfo.OpenConnectionAsync();
        await connection.ExecuteAsync("CREATE TABLE handler_roundtrip(value INTEGER NOT NULL);");
        await connection.ExecuteAsync("INSERT INTO handler_roundtrip(value) VALUES (@value);", new { value });

        var raw = await ReadRawSqliteValueAsync(connectionInfo.ExtractSqlitePath(), "SELECT value FROM handler_roundtrip LIMIT 1;");
        Assert.Equal(typeof(long), raw.FieldType);
        Assert.Equal(expectedRawValue, Assert.IsType<long>(raw.Value));

        var roundTrip = await connection.QuerySingleAsync<bool>("SELECT value FROM handler_roundtrip LIMIT 1;");
        Assert.Equal(value, roundTrip);
    }

    private static async Task<RawSqliteValue> ReadRawSqliteValueAsync(string databasePath, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return new RawSqliteValue(reader.GetFieldType(0), reader.GetValue(0));
    }

    private sealed record RawSqliteValue(Type FieldType, object Value);
}
