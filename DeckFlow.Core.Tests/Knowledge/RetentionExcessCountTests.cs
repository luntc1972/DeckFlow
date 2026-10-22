using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Tests.Knowledge;

public sealed class RetentionExcessCountTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public RetentionExcessCountTests()
    {
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "category-knowledge.db");
    }

    [Fact]
    public async Task CountRetentionExcessAsync_SumsDecksBeyondCapPerCommander()
    {
        var repository = new CategoryKnowledgeRepository(_path);
        await SeedAsync(repository, "Alpha", 3, 0);
        await SeedAsync(repository, "alpha", 2, 5);
        await SeedAsync(repository, "Beta", 2, 10);
        await SeedAsync(repository, "Gamma", 3, 20);

        Assert.Equal(4, await repository.CountRetentionExcessAsync(2));
    }

    [Fact]
    public async Task CountRetentionExcessAsync_IgnoresSkippedPendingAndUrlOnly()
    {
        var repository = new CategoryKnowledgeRepository(_path);
        await SeedAsync(repository, "Alpha", 3, 0);
        await repository.AddDeckIdsAsync(["pending", "url-only"]);
        await repository.MarkDeckProcessedAsync("pending", "Alpha", false, new ArchidektDeckMetadata(null, 3, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        await repository.MarkDeckProcessedAsync("url-only", "Alpha", false, new ArchidektDeckMetadata(null, 3, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        Assert.Equal(1, await repository.CountRetentionExcessAsync(2));
    }

    [Fact]
    public async Task CountRetentionExcessAsync_EqualsRetentionRemovals()
    {
        var repository = new CategoryKnowledgeRepository(_path);
        await SeedAsync(repository, "Alpha", 5, 0);
        await SeedAsync(repository, "Beta", 2, 10);
        await SeedAsync(repository, "Gamma", 3, 20);
        var count = await repository.CountRetentionExcessAsync(2);
        var service = new DeckPruningService(repository, new DeckPruningOptions { PerCommanderCap = 2, BatchSize = 10, Pause = TimeSpan.Zero }, _ => Task.FromResult<long?>(long.MaxValue));

        var result = await service.RunRetentionAsync();

        Assert.Equal(count, result.DecksRemoved);
        Assert.Equal(0, await repository.CountRetentionExcessAsync(2));
    }

    [Fact]
    public async Task CountRetentionExcessAsync_NegativeCap_Throws()
    {
        var repository = new CategoryKnowledgeRepository(_path);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.CountRetentionExcessAsync(-1));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }

    private static async Task SeedAsync(CategoryKnowledgeRepository repository, string commander, int count, int offset)
    {
        for (var index = 0; index < count; index++)
        {
            var id = $"{commander}-{offset + index:D3}";
            await repository.AddDeckIdsAsync([id]);
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + id, [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
            await repository.MarkDeckProcessedAsync(id, commander, false, new ArchidektDeckMetadata(null, 3, false, null, DateTimeOffset.UtcNow.AddDays(offset + index), DateTimeOffset.UtcNow));
        }
    }
}
