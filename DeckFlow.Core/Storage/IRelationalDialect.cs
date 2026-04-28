namespace DeckFlow.Core.Storage;

public interface IRelationalDialect
{
    string ParameterPrefix { get; }
    string FeedbackIdColumnType { get; }
    string FeedbackCreatedUtcColumnType { get; }
    string FeedbackOrderByClause { get; }
    string FeedbackInsertReturningIdSql { get; }
}
