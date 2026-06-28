using DeckFlow.Core.Orchestration;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="ContentKbOrchestratorFactory"/> — the single-source orchestrator graph the
/// CLI's SQLite-path and connection-path builders now both delegate to (M4 de-duplication).
/// </summary>
public sealed class ContentKbOrchestratorFactoryTests : IDisposable
{
    private readonly string _dbPath;

    public ContentKbOrchestratorFactoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "deckflow-ckb-factory-" + Guid.NewGuid().ToString("N") + ".db");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException)
        {
        }
    }

    private RelationalDatabaseConnection SqliteConnection() => RelationalDatabaseConnection.FromSqlitePath(_dbPath);

    [Fact]
    public void Create_WithValidArgs_ReturnsWiredOrchestrator()
    {
        var orchestrator = ContentKbOrchestratorFactory.Create(
            SqliteConnection(),
            artifactRoot: Path.Combine(Path.GetTempPath(), "content-kb"),
            new ThrowingLlmDistillationService(),
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker());

        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void Create_NullConnection_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ContentKbOrchestratorFactory.Create(
            connection: null!,
            artifactRoot: "content-kb",
            new ThrowingLlmDistillationService(),
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankArtifactRoot_Throws(string artifactRoot)
    {
        Assert.Throws<ArgumentException>(() => ContentKbOrchestratorFactory.Create(
            SqliteConnection(),
            artifactRoot,
            new ThrowingLlmDistillationService(),
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker()));
    }

    [Fact]
    public void Create_NullDistiller_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ContentKbOrchestratorFactory.Create(
            SqliteConnection(),
            artifactRoot: "content-kb",
            distiller: null!,
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker()));
    }
}
