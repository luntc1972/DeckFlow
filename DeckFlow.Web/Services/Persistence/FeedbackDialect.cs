using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services;

/// <summary>
/// Provides feedback-specific SQL fragments for SQLite and Postgres variants.
/// This is a Web-layer concern and intentionally does not live in the Core dialect.
/// </summary>
public sealed class FeedbackDialect
{
    /// <summary>Gets the SQL column definition for the feedback created-at timestamp.</summary>
    public string FeedbackCreatedUtcColumnType { get; }

    /// <summary>Gets the SQL ordering clause for feedback queries.</summary>
    public string FeedbackOrderByClause { get; }

    /// <summary>Gets the SQL fragment that inserts feedback and returns the generated identifier.</summary>
    public string FeedbackInsertReturningIdSql { get; }

    private FeedbackDialect(
        string feedbackCreatedUtcColumnType,
        string feedbackOrderByClause,
        string feedbackInsertReturningIdSql)
    {
        FeedbackCreatedUtcColumnType = feedbackCreatedUtcColumnType;
        FeedbackOrderByClause = feedbackOrderByClause;
        FeedbackInsertReturningIdSql = feedbackInsertReturningIdSql;
    }

    private static readonly FeedbackDialect SqliteInstance = new(
        feedbackCreatedUtcColumnType: "TEXT",
        feedbackOrderByClause: "datetime(created_utc) DESC, id DESC",
        feedbackInsertReturningIdSql: """
        INSERT INTO feedback (created_utc, type, message, email, page_url, user_agent, ip_hash, app_version, status)
        VALUES (@created, @type, @message, @email, @pageUrl, @userAgent, @ipHash, @appVersion, @status)
        RETURNING id;
        """);

    private static readonly FeedbackDialect PostgresInstance = new(
        feedbackCreatedUtcColumnType: "TIMESTAMPTZ",
        feedbackOrderByClause: "created_utc DESC, id DESC",
        feedbackInsertReturningIdSql: """
        INSERT INTO feedback (created_utc, type, message, email, page_url, user_agent, ip_hash, app_version, status)
        VALUES (@created, @type, @message, @email, @pageUrl, @userAgent, @ipHash, @appVersion, @status)
        RETURNING id;
        """);

    /// <summary>
    /// Returns the <see cref="FeedbackDialect"/> for the given database connection's provider.
    /// </summary>
    /// <param name="connection">The relational database connection whose provider is used to select the dialect.</param>
    /// <returns>The matching <see cref="FeedbackDialect"/> instance.</returns>
    public static FeedbackDialect For(RelationalDatabaseConnection connection)
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
