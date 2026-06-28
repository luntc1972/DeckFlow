using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Single source of truth for constructing a standalone <see cref="ContentKbOrchestrator"/> over a
/// relational connection — the seven content-kb stores + spend ledgers + options graph. Used by the
/// CLI's per-command orchestrator builders (both the SQLite-path and the Postgres-connection paths)
/// so the store wiring is declared once (M4: de-duplicates the previously copy-pasted constructor
/// blocks in <c>ContentKbCommandRunners</c>).
/// <br/>
/// Studio composes the same orchestrator through DI (<c>AddContentKbOrchestrator</c>) instead, so
/// its pages and the orchestrator share one store instance per type — a requirement this
/// new-instance factory deliberately does not serve.
/// </summary>
public static class ContentKbOrchestratorFactory
{
    /// <summary>
    /// Builds a <see cref="ContentKbOrchestrator"/> whose stores all target
    /// <paramref name="connection"/>. Callers with a SQLite path pass
    /// <see cref="RelationalDatabaseConnection.FromSqlitePath(string)"/>.
    /// </summary>
    /// <param name="connection">Provider + connection string descriptor every store targets.</param>
    /// <param name="artifactRoot">Root directory for content-kb artifacts.</param>
    /// <param name="distiller">LLM distillation service (or a throwing stub for read-only paths).</param>
    /// <param name="lister">YouTube channel video lister (or a throwing stub).</param>
    /// <param name="transcriptSource">Transcript source (or a throwing stub).</param>
    /// <param name="chunker">FFmpeg audio chunker (or a throwing stub).</param>
    /// <returns>A fully wired orchestrator over the supplied connection.</returns>
    public static ContentKbOrchestrator Create(
        RelationalDatabaseConnection connection,
        string artifactRoot,
        ILlmDistillationService distiller,
        IYouTubeChannelVideoLister lister,
        ITranscriptSource transcriptSource,
        IFfmpegAudioChunker chunker)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        ArgumentNullException.ThrowIfNull(distiller);
        ArgumentNullException.ThrowIfNull(lister);
        ArgumentNullException.ThrowIfNull(transcriptSource);
        ArgumentNullException.ThrowIfNull(chunker);

        return new ContentKbOrchestrator(
            new ContentSourceStore(connection),
            new ContentVideoStore(connection),
            new ContentSiteIndexStore(connection),
            new BlockedVideoStore(connection),
            new ContentHarvestRunStore(connection),
            new LlmSpendLedger(connection),
            new WhisperSpendLedger(connection),
            distiller,
            lister,
            transcriptSource,
            chunker,
            () => DateTimeOffset.UtcNow,
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = artifactRoot,
            });
    }
}
