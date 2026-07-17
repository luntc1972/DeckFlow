using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services;

/// <summary>
/// Provides the manabase-baseline SQL fragments that differ by provider: the <c>computed_utc</c>
/// timestamp column type and the 8-byte floating-point column type for the averages. Upsert and
/// select SQL are portable (both engines support <c>ON CONFLICT ... DO UPDATE SET c = excluded.c</c>)
/// and live in the store.
/// </summary>
public sealed class ManabaseBaselineDialect
{
    /// <summary>Gets the SQL column type for the <c>computed_utc</c> timestamp.</summary>
    public string ComputedUtcColumnType { get; }

    /// <summary>
    /// Gets the SQL column type for the 8-byte floating-point averages. SQLite <c>REAL</c> is already
    /// 8-byte; Postgres <c>REAL</c> is float4, so Postgres must use <c>DOUBLE PRECISION</c> or values
    /// like 35.9 round-trip to a different <see cref="double"/> (plan-review MEDIUM).
    /// </summary>
    public string RealColumnType { get; }

    private ManabaseBaselineDialect(string computedUtcColumnType, string realColumnType)
    {
        ComputedUtcColumnType = computedUtcColumnType;
        RealColumnType = realColumnType;
    }

    private static readonly ManabaseBaselineDialect SqliteInstance = new("TEXT", "REAL");
    private static readonly ManabaseBaselineDialect PostgresInstance = new("TIMESTAMPTZ", "DOUBLE PRECISION");

    /// <summary>Returns the dialect helper for the connection's provider.</summary>
    /// <param name="connection">Connection whose provider selects the dialect.</param>
    public static ManabaseBaselineDialect For(RelationalDatabaseConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return connection.Provider switch
        {
            RelationalDatabaseProvider.Sqlite => SqliteInstance,
            RelationalDatabaseProvider.Postgres => PostgresInstance,
            _ => throw new NotSupportedException($"Unsupported database provider '{connection.Provider}'.")
        };
    }
}
