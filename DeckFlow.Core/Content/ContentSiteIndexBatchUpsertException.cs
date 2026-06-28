namespace DeckFlow.Core.Content;

/// <summary>
/// Thrown when <see cref="IContentSiteIndexStore.UpsertContentColumnsOnlyBatchAsync"/> aborts
/// mid-batch and rolls back the transaction. Carries only non-secret row identity (title and
/// natural key) so callers can surface a human-readable failure message without touching the
/// underlying DB exception.
/// </summary>
/// <remarks>
/// Why: the DB exception (e.g. Npgsql, SQLite) can carry connection host, database name, and
/// credentials in its Message; it must stay in <see cref="Exception.InnerException"/> for the
/// log sink only — never surfaced to the UI (D-07 / SC5 / T-qyc-02).
/// </remarks>
public sealed class ContentSiteIndexBatchUpsertException : Exception
{
    /// <summary>
    /// Gets the title of the row whose upsert caused the batch to abort.
    /// </summary>
    public string FailedRowTitle { get; }

    /// <summary>
    /// Gets the natural key type of the failing row (e.g. <c>youtube_channel</c> or
    /// <c>podcast_rss</c>), derived before the exception was thrown.
    /// </summary>
    public string FailedKeyType { get; }

    /// <summary>
    /// Gets the natural key value of the failing row (e.g. a YouTube video ID or RSS GUID),
    /// derived before the exception was thrown.
    /// </summary>
    public string FailedKeyValue { get; }

    /// <summary>
    /// Initializes a new <see cref="ContentSiteIndexBatchUpsertException"/>.
    /// </summary>
    /// <param name="failedRowTitle">Title of the row that caused the abort.</param>
    /// <param name="failedKeyType">Natural key type of the failing row.</param>
    /// <param name="failedKeyValue">Natural key value of the failing row.</param>
    /// <param name="message">Human-readable message describing the failure.</param>
    /// <param name="innerException">
    /// The underlying DB exception; kept here for the log sink and MUST NOT be surfaced to the UI.
    /// </param>
    public ContentSiteIndexBatchUpsertException(
        string failedRowTitle,
        string failedKeyType,
        string failedKeyValue,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        FailedRowTitle = failedRowTitle;
        FailedKeyType = failedKeyType;
        FailedKeyValue = failedKeyValue;
    }
}
