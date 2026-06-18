namespace DeckFlow.Core.Storage;

/// <summary>
/// SQLite-specific SQL fragments and column type definitions for the DeckFlow relational schema.
/// </summary>
public sealed class SqliteRelationalDialect : IRelationalDialect
{
    /// <summary>Shared reusable singleton instance of the SQLite dialect.</summary>
    public static readonly SqliteRelationalDialect Instance = new();

    private SqliteRelationalDialect()
    {
    }

    /// <inheritdoc />
    public string SurrogateIdColumnType => "INTEGER PRIMARY KEY AUTOINCREMENT";
}
