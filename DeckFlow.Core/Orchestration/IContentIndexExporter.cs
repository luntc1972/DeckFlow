namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Exports published Content KB site-index rows for host-owned JSON serialization and file output.
/// </summary>
public interface IContentIndexExporter
{
    /// <summary>
    /// Exports published site-index rows.
    /// </summary>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured exported rows and status information.</returns>
    Task<ContentIndexExportResult> ExportIndexAsync(
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes the approved-only site-index rows to <paramref name="seedPath"/> with guaranteed
    /// LF line endings (no CRLF) and the same JSON byte-shape as the CLI export (camelCase,
    /// 2-space indent, trailing newline). Parent directories are created when absent.
    /// </summary>
    /// <param name="seedPath">Absolute path to the seed JSON file to write.</param>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="ContentIndexExportResult.Success"/> is <see langword="true"/> on success and
    /// the <see cref="ContentIndexExportResult.RowCount"/> reflects the approved row count;
    /// <see langword="false"/> with <see cref="ContentIndexExportResult.Message"/> on I/O failure.
    /// </returns>
    Task<ContentIndexExportResult> ExportIndexToFileAsync(
        string seedPath,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);
}
