namespace DeckFlow.Core.Storage;

/// <summary>
/// Provides database-dialect-specific SQL fragments and column types for SQLite and Postgres.
/// </summary>
public interface IRelationalDialect
{
    /// <summary>
    /// Gets the SQL column definition for a surrogate auto-incrementing primary key.
    /// </summary>
    string SurrogateIdColumnType { get; }
    /// <summary>Gets the SQL column definition for the feedback created-at timestamp.</summary>
    string FeedbackCreatedUtcColumnType { get; }
    /// <summary>Gets the SQL ordering clause for feedback queries.</summary>
    string FeedbackOrderByClause { get; }
    /// <summary>Gets the SQL fragment that inserts feedback and returns the generated identifier.</summary>
    string FeedbackInsertReturningIdSql { get; }
}
