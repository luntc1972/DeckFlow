using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CreatorStyle;
using Microsoft.Data.Sqlite;
using Polly.CircuitBreaker;
using Xunit;

namespace DeckFlow.Web.Tests.Services.CreatorStyle;

public sealed class CreatorDeckCategoryResolverTests
{
    private static void ClearPool(string path) => SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    [Fact]
    public async Task ResolveAsync_BrokenCircuitForOneCard_SkipsCardAndContinues()
    {
        var directory = Path.Combine(Path.GetTempPath(), "deckflow-category-resolver-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var repository = new CategoryKnowledgeRepository(Path.Combine(directory, "knowledge.db"));
            var resolver = new CreatorDeckCategoryResolver(
                repository,
                new ThrowingTaggerLookupService("Broken Card"));

            var result = await resolver.ResolveAsync([
                new CreatorDeckSample
                {
                    DeckId = "deck-1",
                    Entries =
                    [
                        CreateEntry("Broken Card"),
                        CreateEntry("Healthy Card")
                    ],
                    CardCount = 2,
                    ConfidenceMarker = "test"
                }]);

            Assert.DoesNotContain("Broken Card", result.Keys);
            Assert.Equal(["ramp"], result["Healthy Card"]);
        }
        finally
        {
            ClearPool(Path.Combine(directory, "knowledge.db"));
            Directory.Delete(directory, recursive: true);
        }
    }

    private static DeckEntry CreateEntry(string name)
        => new()
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = 1,
            Board = "mainboard"
        };

    private sealed class ThrowingTaggerLookupService(string brokenCard) : IScryfallTaggerLookupService
    {
        public Task<IReadOnlyList<string>> LookupOracleTagsAsync(
            string cardName,
            CancellationToken cancellationToken = default)
            => cardName == brokenCard
                ? throw new BrokenCircuitException("test circuit is open")
                : Task.FromResult<IReadOnlyList<string>>(["ramp"]);
    }
}
