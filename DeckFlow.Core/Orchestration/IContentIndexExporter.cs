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
}
