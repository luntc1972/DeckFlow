using System.Data.Common;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace DeckFlow.Core.Storage;

/// <summary>
/// Identifies the relational database backend in use.
/// </summary>
public enum RelationalDatabaseProvider
{
    /// <summary>Uses a SQLite database.</summary>
    Sqlite,
    /// <summary>Uses a PostgreSQL database.</summary>
    Postgres
}

/// <summary>
/// Holds the database provider and connection string, and exposes dialect-specific helpers for opening connections.
/// </summary>
public sealed record RelationalDatabaseConnection(RelationalDatabaseProvider Provider, string ConnectionString)
{
    static RelationalDatabaseConnection()
    {
        // Why: register handlers at the earliest shared chokepoint before any connection opens.
        DapperTypeHandlers.EnsureRegistered();
    }

    /// <summary>Gets the SQL dialect helpers for the configured provider.</summary>
    public IRelationalDialect Dialect
        => Provider switch
        {
            RelationalDatabaseProvider.Sqlite => SqliteRelationalDialect.Instance,
            RelationalDatabaseProvider.Postgres => PostgresRelationalDialect.Instance,
            _ => throw new NotSupportedException($"Unsupported database provider '{Provider}'.")
        };

    /// <summary>
    /// Creates a new unopened connection for the configured provider.
    /// </summary>
    /// <remarks>
    /// This does not open the connection and does not apply SQLite foreign-key enforcement.
    /// Callers that need foreign-key enforcement must use <see cref="OpenConnectionAsync(CancellationToken)"/>
    /// or the content factory methods instead of calling this method and opening the connection directly.
    /// </remarks>
    public DbConnection CreateConnection()
        => Provider switch
        {
            RelationalDatabaseProvider.Sqlite => new SqliteConnection(ConnectionString),
            RelationalDatabaseProvider.Postgres => new NpgsqlConnection(ConnectionString),
            _ => throw new NotSupportedException($"Unsupported database provider '{Provider}'.")
        };

    /// <summary>
    /// Opens a new connection to the configured database, applying SQLite foreign-key enforcement when needed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An open <see cref="DbConnection"/>.</returns>
    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = CreateConnection();

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (IsSqlite)
            {
                // Why: SQLite enforces FK ON DELETE CASCADE only with this pragma per
                // connection. Dispose on failures after open so failed pragma commands do not leak.
                await using var pragma = connection.CreateCommand();
                pragma.CommandText = "PRAGMA foreign_keys=ON;";
                await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return connection;
    }

    /// <summary>Gets a value indicating whether the configured provider is SQLite.</summary>
    public bool IsSqlite => Provider == RelationalDatabaseProvider.Sqlite;
    /// <summary>Gets a value indicating whether the configured provider is PostgreSQL.</summary>
    public bool IsPostgres => Provider == RelationalDatabaseProvider.Postgres;

    /// <summary>
    /// Creates a SQLite connection descriptor using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    /// <returns>A SQLite connection descriptor rooted at the supplied path.</returns>
    public static RelationalDatabaseConnection FromSqlitePath(string databasePath)
        => new(RelationalDatabaseProvider.Sqlite, $"Data Source={Path.GetFullPath(databasePath)}");

    /// <summary>
    /// Extracts the normalized SQLite file path from the current connection string.
    /// </summary>
    /// <returns>The absolute SQLite file path.</returns>
    public string ExtractSqlitePath()
    {
        if (Provider != RelationalDatabaseProvider.Sqlite)
        {
            throw new InvalidOperationException("SQLite path is only available for SQLite connections.");
        }

        var builder = new SqliteConnectionStringBuilder(ConnectionString);
        return Path.GetFullPath(builder.DataSource);
    }

    /// <summary>
    /// Adds a parameter to the supplied database command.
    /// </summary>
    /// <param name="command">Command that will receive the parameter.</param>
    /// <param name="name">Parameter name.</param>
    /// <param name="value">Parameter value, or <see langword="null"/> to store <see cref="DBNull.Value"/>.</param>
    public static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
