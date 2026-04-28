using System.Data.Common;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace DeckFlow.Core.Storage;

public enum RelationalDatabaseProvider
{
    Sqlite,
    Postgres
}

public sealed record RelationalDatabaseConnection(RelationalDatabaseProvider Provider, string ConnectionString)
{
    public IRelationalDialect Dialect
        => Provider switch
        {
            RelationalDatabaseProvider.Sqlite => SqliteRelationalDialect.Instance,
            RelationalDatabaseProvider.Postgres => PostgresRelationalDialect.Instance,
            _ => throw new NotSupportedException($"Unsupported database provider '{Provider}'.")
        };

    public DbConnection CreateConnection()
        => Provider switch
        {
            RelationalDatabaseProvider.Sqlite => new SqliteConnection(ConnectionString),
            RelationalDatabaseProvider.Postgres => new NpgsqlConnection(ConnectionString),
            _ => throw new NotSupportedException($"Unsupported database provider '{Provider}'.")
        };

    public bool IsSqlite => Provider == RelationalDatabaseProvider.Sqlite;
    public bool IsPostgres => Provider == RelationalDatabaseProvider.Postgres;

    public static RelationalDatabaseConnection FromSqlitePath(string databasePath)
        => new(RelationalDatabaseProvider.Sqlite, $"Data Source={Path.GetFullPath(databasePath)}");
}
