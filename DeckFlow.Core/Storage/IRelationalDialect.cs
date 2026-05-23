namespace DeckFlow.Core.Storage;

/// <summary>
/// Provides database-dialect-specific SQL fragments and column types for SQLite and Postgres.
/// </summary>
public interface IRelationalDialect
{
    string FeedbackIdColumnType { get; }
    string FeedbackCreatedUtcColumnType { get; }
    string FeedbackOrderByClause { get; }
    string FeedbackInsertReturningIdSql { get; }
}
