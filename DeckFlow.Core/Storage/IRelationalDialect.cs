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
    string FeedbackCreatedUtcColumnType { get; }
    string FeedbackOrderByClause { get; }
    string FeedbackInsertReturningIdSql { get; }
}
