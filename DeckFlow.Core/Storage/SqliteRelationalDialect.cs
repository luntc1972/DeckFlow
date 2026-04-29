namespace DeckFlow.Core.Storage;

public sealed class SqliteRelationalDialect : IRelationalDialect
{
    public static readonly SqliteRelationalDialect Instance = new();

    private SqliteRelationalDialect()
    {
    }

    public string FeedbackIdColumnType => "INTEGER PRIMARY KEY AUTOINCREMENT";
    public string FeedbackCreatedUtcColumnType => "TEXT";
    public string FeedbackOrderByClause => "datetime(created_utc) DESC, id DESC";
    public string FeedbackInsertReturningIdSql => """
        INSERT INTO feedback (created_utc, type, message, email, page_url, user_agent, ip_hash, app_version, status)
        VALUES (@created, @type, @message, @email, @pageUrl, @userAgent, @ipHash, @appVersion, @status)
        RETURNING id;
        """;
}
