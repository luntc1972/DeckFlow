namespace DeckFlow.Core.Storage;

/// <summary>
/// SQLite-specific SQL fragments and column type definitions for the DeckFlow relational schema.
/// </summary>
public sealed class SqliteRelationalDialect : IRelationalDialect
{
    public static readonly SqliteRelationalDialect Instance = new();

    private SqliteRelationalDialect()
    {
    }

    /// <inheritdoc />
    public string SurrogateIdColumnType => "INTEGER PRIMARY KEY AUTOINCREMENT";
    /// <inheritdoc />
    public string FeedbackCreatedUtcColumnType => "TEXT";
    /// <inheritdoc />
    public string FeedbackOrderByClause => "datetime(created_utc) DESC, id DESC";
    /// <inheritdoc />
    public string FeedbackInsertReturningIdSql => """
        INSERT INTO feedback (created_utc, type, message, email, page_url, user_agent, ip_hash, app_version, status)
        VALUES (@created, @type, @message, @email, @pageUrl, @userAgent, @ipHash, @appVersion, @status)
        RETURNING id;
        """;
}
