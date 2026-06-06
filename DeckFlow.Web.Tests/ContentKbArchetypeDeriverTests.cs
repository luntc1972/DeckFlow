using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ContentKbArchetypeDeriver"/>.
/// </summary>
public sealed class ContentKbArchetypeDeriverTests
{
    [Fact]
    public async Task DeriveAsync_TutorAndCounterHeavyRows_EmitsComboOrControl()
    {
        var store = new TrackingCategoryKnowledgeStore
        {
            CommanderRows =
            [
                new CategoryKnowledgeRow("tutor", "Demonic Tutor", 8, 4),
                new CategoryKnowledgeRow("counter", "Counterspell", 7, 4),
                new CategoryKnowledgeRow("draw", "Brainstorm", 2, 1),
            ]
        };
        var sut = new ContentKbArchetypeDeriver(store);

        var archetypes = await sut.DeriveAsync("Kess, Dissident Mage");

        Assert.Contains(archetypes, tag => string.Equals(tag, "combo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tag, "control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeriveAsync_NoRows_ReturnsEmptySetWithoutThrowing()
    {
        var sut = new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore());

        var archetypes = await sut.DeriveAsync("Unknown Commander");

        Assert.NotNull(archetypes);
        Assert.Empty(archetypes);
    }

    [Fact]
    public async Task DeriveAsync_OnlyReturnsAllowlistedArchetypes()
    {
        var store = new TrackingCategoryKnowledgeStore
        {
            CommanderRows =
            [
                new CategoryKnowledgeRow("ramp", "Cultivate", 10, 5),
                new CategoryKnowledgeRow("sacrifice", "Viscera Seer", 9, 5),
                new CategoryKnowledgeRow("made-up-tag", "Mystery Card", 50, 10),
            ]
        };
        var sut = new ContentKbArchetypeDeriver(store);

        var archetypes = await sut.DeriveAsync("Korvold, Fae-Cursed King");

        Assert.NotEmpty(archetypes);
        Assert.All(archetypes, tag => Assert.Contains(tag, ContentTagVocabulary.Archetypes));
    }

    private sealed class TrackingCategoryKnowledgeStore : ICategoryKnowledgeStore
    {
        public IReadOnlyList<CategoryKnowledgeRow> CommanderRows { get; init; } = Array.Empty<CategoryKnowledgeRow>();

        public int CommanderQueryCount { get; private set; }

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
        {
            CommanderQueryCount++;
            return Task.FromResult(CommanderRows);
        }

        public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null) => Task.FromResult(0);

        public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopCommanderRow>>(Array.Empty<TopCommanderRow>());

        public Task<IReadOnlyList<HarvestedCommanderRow>> GetPagedProcessedCommandersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestedCommanderRow>>(Array.Empty<HarvestedCommanderRow>());

        public Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default) => Task.FromResult<long?>(null);

        public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult(CardDeckTotals.Empty);
    }
}
