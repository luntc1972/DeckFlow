using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for content-hash deduplication in the Archidekt category cache.
/// </summary>
public sealed class ContentHashDedupTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _tempDirectory;

    public ContentHashDedupTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    [Fact]
    public void ComputeHash_OrderIndependent()
    {
        var original = new[]
        {
            CreateEntry("Sol Ring", "Ramp"),
            CreateEntry("Guardian Project", "Draw", board: "sideboard")
        };
        var reordered = new[]
        {
            CreateEntry("Guardian Project", "Draw", board: "sideboard"),
            CreateEntry("Sol Ring", "Ramp")
        };

        Assert.Equal(
            DeckCategoryCacheWriter.ComputeCanonicalHash(original),
            DeckCategoryCacheWriter.ComputeCanonicalHash(reordered));
    }

    [Fact]
    public void ComputeHash_DistinguishesContent()
    {
        var baseline = new[] { CreateEntry("Sol Ring", "Ramp", quantity: 1) };
        var differentName = new[] { CreateEntry("Arcane Signet", "Ramp", quantity: 1) };
        var differentCategory = new[] { CreateEntry("Sol Ring", "Draw", quantity: 1) };
        var differentBoard = new[] { CreateEntry("Sol Ring", "Ramp", quantity: 1, board: "sideboard") };
        var differentQuantity = new[] { CreateEntry("Sol Ring", "Ramp", quantity: 2) };
        var baselineHash = DeckCategoryCacheWriter.ComputeCanonicalHash(baseline);

        Assert.NotEqual(baselineHash, DeckCategoryCacheWriter.ComputeCanonicalHash(differentName));
        Assert.NotEqual(baselineHash, DeckCategoryCacheWriter.ComputeCanonicalHash(differentCategory));
        Assert.NotEqual(baselineHash, DeckCategoryCacheWriter.ComputeCanonicalHash(differentBoard));
        Assert.NotEqual(baselineHash, DeckCategoryCacheWriter.ComputeCanonicalHash(differentQuantity));
    }

    [Fact]
    public void ComputeHash_SplitsMultiCategory()
    {
        var combined = new[] { CreateEntry("Esper Sentinel", "Ramp,Draw", quantity: 2) };
        var split = new[]
        {
            CreateEntry("Esper Sentinel", "Ramp", quantity: 2),
            CreateEntry("Esper Sentinel", "Draw", quantity: 2)
        };

        Assert.Equal(
            DeckCategoryCacheWriter.ComputeCanonicalHash(combined),
            DeckCategoryCacheWriter.ComputeCanonicalHash(split));
    }

    [Fact]
    public void ComputeHash_AggregatesDuplicates()
    {
        var duplicates = new[]
        {
            CreateEntry("Mystic Remora", "Draw", quantity: 1),
            CreateEntry("Mystic Remora", "Draw", quantity: 2)
        };
        var aggregated = new[] { CreateEntry("Mystic Remora", "Draw", quantity: 3) };

        Assert.Equal(
            DeckCategoryCacheWriter.ComputeCanonicalHash(duplicates),
            DeckCategoryCacheWriter.ComputeCanonicalHash(aggregated));
    }

    [Fact]
    public void ComputeHash_UncategorizedCardChangesHash()
    {
        var baseline = new[] { CreateEntry("Sol Ring", "Ramp") };
        var withUncategorizedCard = new[]
        {
            CreateEntry("Sol Ring", "Ramp"),
            CreateEntry("Command Tower", string.Empty)
        };

        Assert.NotEqual(
            DeckCategoryCacheWriter.ComputeCanonicalHash(baseline),
            DeckCategoryCacheWriter.ComputeCanonicalHash(withUncategorizedCard));
    }

    [Fact]
    public void ComputeHash_BoardMoveChangesHash()
    {
        var mainboard = new[] { CreateEntry("Command Tower", string.Empty, board: "mainboard") };
        var sideboard = new[] { CreateEntry("Command Tower", string.Empty, board: "sideboard") };

        Assert.NotEqual(
            DeckCategoryCacheWriter.ComputeCanonicalHash(mainboard),
            DeckCategoryCacheWriter.ComputeCanonicalHash(sideboard));
    }

    [Fact]
    public void ComputeHash_DelimiterInjectionSafe()
    {
        var first = new[] { CreateEntry("A|B", "c") };
        var second = new[] { CreateEntry("A", "b|c") };

        Assert.NotEqual(
            DeckCategoryCacheWriter.ComputeCanonicalHash(first),
            DeckCategoryCacheWriter.ComputeCanonicalHash(second));
    }

    [Fact]
    public void ComputeHash_Deterministic()
    {
        var entries = new[] { CreateEntry("Rhystic Study", "Draw") };

        var first = DeckCategoryCacheWriter.ComputeCanonicalHash(entries);
        var second = DeckCategoryCacheWriter.ComputeCanonicalHash(entries);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.All(first, character => Assert.True(character is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }

    [Fact]
    public async Task GetContentHash_ReturnsNullWhenUnset()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { "100" });

        var hash = await repository.GetContentHashAsync("100");

        Assert.Null(hash);
    }

    [Fact]
    public async Task SetThenGetContentHash_RoundTrips()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { "101" });

        await repository.SetContentHashAsync("101", new string('a', 64));

        Assert.Equal(new string('a', 64), await repository.GetContentHashAsync("101"));
    }

    [Fact]
    public async Task SetContentHashNull_ClearsHash()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { "102" });
        await repository.SetContentHashAsync("102", new string('b', 64));

        await repository.SetContentHashAsync("102", null);

        Assert.Null(await repository.GetContentHashAsync("102"));
    }

    [Fact]
    public async Task EnsureSchema_IsIdempotentForContentHash()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);

        await repository.EnsureSchemaAsync();
        await repository.EnsureSchemaAsync();

        await repository.AddDeckIdsAsync(new[] { "103" });
        Assert.Null(await repository.GetContentHashAsync("103"));
    }

    private static DeckEntry CreateEntry(
        string cardName,
        string? category,
        int quantity = 1,
        string board = "mainboard") => new()
        {
            Name = cardName,
            NormalizedName = CardNormalizer.Normalize(cardName),
            Quantity = quantity,
            Board = board,
            Category = category
        };

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // ignored
        }
    }
}
