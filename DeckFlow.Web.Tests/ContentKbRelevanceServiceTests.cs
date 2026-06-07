using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ContentKbRelevanceService"/>.
/// </summary>
public sealed class ContentKbRelevanceServiceTests
{
    [Fact]
    public async Task GetRelevantClipsAsync_FlagDisabled_ReturnsNullWithoutTouchingStores()
    {
        var row = CreateRow(1, "artifact-a.md", ["combo"], ["cEDH"]);
        var store = new TrackingContentSiteIndexStore([row]);
        var categoryStore = new TrackingCategoryKnowledgeStore();
        var flags = new TrackingFeatureFlagCache(new Dictionary<string, bool>
        {
            ["content.kb.enabled"] = false
        });
        var archetypeDeriver = new ContentKbArchetypeDeriver(categoryStore);
        var sut = CreateService(store, flags, archetypeDeriver, new Dictionary<string, string>());

        var result = await sut.GetRelevantClipsAsync("Tymna the Weaver", "cEDH");

        Assert.Null(result);
        Assert.Equal(0, store.PublishedRowsQueryCount);
        Assert.Equal(0, categoryStore.CommanderQueryCount);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_CommanderFoundOnlyInClipText_WithArchetypeOverlap_ReturnsClip()
    {
        var row = CreateRow(1, "artifact-a.md", ["combo"], []);
        var store = new TrackingContentSiteIndexStore([row]);
        var categoryStore = new TrackingCategoryKnowledgeStore
        {
            CommanderRows =
            [
                new CategoryKnowledgeRow("tutor", "Demonic Tutor", 8, 4),
                new CategoryKnowledgeRow("counter", "Counterspell", 7, 4),
            ]
        };
        var artifactText = BuildArtifact(
            "https://www.youtube.com/watch?v=abc123",
            "2026-06-05T12:34:56Z",
            "Neutral summary with no commander mention.",
            ("02:14", "Tymna the Weaver keeps the combo line compact and protected."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(categoryStore),
            new Dictionary<string, string> { [row.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync("Tymna the Weaver / Kraum, Ludevic's Opus", bracket: null);

        var clip = Assert.Single(result!);
        Assert.Equal("02:14", clip.TimestampLabel);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_SingleDimensionMatch_ReturnsNull()
    {
        var row = CreateRow(1, "artifact-a.md", ["value-engine"], []);
        var store = new TrackingContentSiteIndexStore([row]);
        var artifactText = BuildArtifact(
            "https://www.youtube.com/watch?v=abc123",
            "2026-06-05T12:34:56Z",
            "A summary about Tymna the Weaver.",
            ("02:14", "No second dimension here."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            new Dictionary<string, string> { [row.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync("Tymna the Weaver", bracket: null, deckArchetypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_CommanderAndBracketMatch_QualifyWithoutArchetypeOverlap()
    {
        var row = CreateRow(1, "artifact-a.md", ["value-engine"], ["cEDH"]);
        var store = new TrackingContentSiteIndexStore([row]);
        var artifactText = BuildArtifact(
            "https://www.youtube.com/watch?v=abc123",
            "2026-06-05T12:34:56Z",
            "Neutral summary.",
            ("02:14", "Kraum, Ludevic's Opus is the engine that closes the game."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            new Dictionary<string, string> { [row.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync(
            "Tymna the Weaver / Kraum, Ludevic's Opus",
            "cEDH",
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.Single(result!);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_MoreThanFiveQualifyingClips_ReturnsBestArtifactFirstInDocumentOrder()
    {
        var topRow = CreateRow(1, "artifact-top.md", ["combo"], ["cEDH"]);
        var nextRow = CreateRow(2, "artifact-next.md", ["combo"], []);
        var store = new TrackingContentSiteIndexStore([topRow, nextRow]);
        var artifacts = new Dictionary<string, string>
        {
            [topRow.ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=top123",
                "2026-06-05T12:34:56Z",
                "Strong summary.",
                ("00:10", "Tymna the Weaver opens the line."),
                ("00:20", "Second top clip for Tymna the Weaver."),
                ("00:30", "Third top clip for Tymna the Weaver."),
                ("00:40", "Fourth top clip for Tymna the Weaver.")),
            [nextRow.ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=next123",
                "2026-06-05T12:34:56Z",
                "Backup summary.",
                ("01:10", "Tymna the Weaver appears in this lower-ranked artifact."),
                ("01:20", "Second lower-ranked clip for Tymna the Weaver."),
                ("01:30", "Third lower-ranked clip for Tymna the Weaver."))
        };
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            artifacts);

        var result = await sut.GetRelevantClipsAsync(
            "Tymna the Weaver",
            "cEDH",
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(result);
        Assert.Equal(5, result!.Count);
        Assert.All(result.Take(4), clip => Assert.Equal(topRow.Title, clip.Title));
        Assert.Equal("00:10", result[0].TimestampLabel);
        Assert.Equal("00:40", result[3].TimestampLabel);
        Assert.Equal(nextRow.Title, result[4].Title);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_PartnerAwareCommanderMatch_AllowsEitherPartnerToQualify()
    {
        var row = CreateRow(1, "artifact-a.md", ["combo"], []);
        var store = new TrackingContentSiteIndexStore([row]);
        var artifactText = BuildArtifact(
            "https://www.youtube.com/watch?v=abc123",
            "2026-06-05T12:34:56Z",
            "Neutral summary.",
            ("02:14", "Kraum, Ludevic's Opus carries the wheel plan here."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            new Dictionary<string, string> { [row.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync(
            "Tymna the Weaver / Kraum, Ludevic's Opus",
            bracket: null,
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.Single(result!);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_FullCommanderName_MatchesShortNameMentionBeforeComma()
    {
        var row = CreateRow(1, "artifact-a.md", ["combo"], []);
        var store = new TrackingContentSiteIndexStore([row]);
        var artifactText = BuildArtifact(
            "https://www.youtube.com/watch?v=abc123",
            "2026-06-05T12:34:56Z",
            "Neutral summary.",
            ("02:14", "Kinnan powers the artifact combo turn here."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            new Dictionary<string, string> { [row.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync(
            "Kinnan, Bonder Prodigy",
            bracket: null,
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.Single(result!);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_PreCommaTokenShorterThanFourChars_DoesNotCreateCommanderHit()
    {
        var row = CreateRow(1, "artifact-a.md", ["combo"], []);
        var store = new TrackingContentSiteIndexStore([row]);
        var artifactText = BuildArtifact(
            "https://www.youtube.com/watch?v=abc123",
            "2026-06-05T12:34:56Z",
            "Neutral summary.",
            ("02:14", "Rin carries the combo turn here."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            new Dictionary<string, string> { [row.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync(
            "Rin, Test Commander",
            bracket: null,
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_TightBudget_TrimsLowestScoringClipsFirst()
    {
        var highRow = CreateRow(1, "artifact-high.md", ["combo"], ["cEDH"]);
        var lowRow = CreateRow(2, "artifact-low.md", ["combo"], []);
        var store = new TrackingContentSiteIndexStore([highRow, lowRow]);
        var artifacts = new Dictionary<string, string>
        {
            [highRow.ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=high123",
                "2026-06-05T12:34:56Z",
                "High summary.",
                ("00:10", "Tymna the Weaver top clip alpha."),
                ("00:20", "Tymna the Weaver top clip beta."),
                ("00:30", "Tymna the Weaver top clip gamma.")),
            [lowRow.ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=low123",
                "2026-06-05T12:34:56Z",
                "Low summary.",
                ("01:10", "Tymna the Weaver lower clip alpha."),
                ("01:20", "Tymna the Weaver lower clip beta."))
        };
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            artifacts);

        var result = await sut.GetRelevantClipsAsync(
            "Tymna the Weaver",
            "cEDH",
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase),
            maxRenderedChars: 420);

        Assert.NotNull(result);
        Assert.True(result!.Count is > 0 and < 5);
        Assert.All(result, clip => Assert.Equal(highRow.Title, clip.Title));
    }

    [Fact]
    public async Task ScoreAllAsync_ReturnsEveryVisibleRowIncludingZeroScores()
    {
        var matchRow = CreateRow(1, "artifact-match.md", ["combo"], ["cEDH"]);
        var zeroRow = CreateRow(2, "artifact-zero.md", ["lands"], []);
        var store = new TrackingContentSiteIndexStore([matchRow, zeroRow]);
        var artifacts = new Dictionary<string, string>
        {
            [matchRow.ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=match123",
                "2026-06-05T12:34:56Z",
                "Summary.",
                ("00:10", "Tymna the Weaver is named here.")),
            [zeroRow.ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=zero123",
                "2026-06-05T12:34:56Z",
                "Summary.",
                ("00:10", "No relevant commander text."))
        };
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            artifacts);

        var scored = await sut.ScoreAllAsync(
            "Tymna the Weaver",
            "cEDH",
            CancellationToken.None);

        Assert.Equal(2, scored.Count);
        Assert.Equal(matchRow.Id, scored[0].Row.Id);
        Assert.Contains(scored, item => item.Row.Id == zeroRow.Id && item.Score == 0d);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_FileReadFailureOnOneArtifact_ContinuesSelection()
    {
        var badRow = CreateRow(1, "artifact-bad.md", ["combo"], ["cEDH"]);
        var goodRow = CreateRow(2, "artifact-good.md", ["combo"], ["cEDH"]);
        var store = new TrackingContentSiteIndexStore([badRow, goodRow]);
        var artifacts = new Dictionary<string, string>
        {
            [goodRow.ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=good123",
                "2026-06-05T12:34:56Z",
                "Summary.",
                ("00:10", "Tymna the Weaver still qualifies here."))
        };
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            artifacts,
            throwOnRead: "artifact-bad.md");

        var result = await sut.GetRelevantClipsAsync(
            "Tymna the Weaver",
            "cEDH",
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        var clip = Assert.Single(result!);
        Assert.Equal(goodRow.Title, clip.Title);
    }

    private static ContentKbRelevanceService CreateService(
        TrackingContentSiteIndexStore store,
        IFeatureFlagCache flagCache,
        ContentKbArchetypeDeriver archetypeDeriver,
        IReadOnlyDictionary<string, string> artifacts,
        string? throwOnRead = null)
    {
        return new ContentKbRelevanceService(
            store,
            artifactPath => artifactPath,
            flagCache,
            archetypeDeriver,
            logger: null,
            readArtifactAsync: (artifactPath, cancellationToken) =>
            {
                if (string.Equals(artifactPath, throwOnRead, StringComparison.Ordinal))
                {
                    throw new IOException("boom");
                }

                if (!artifacts.TryGetValue(artifactPath, out var text))
                {
                    throw new FileNotFoundException("missing test artifact", artifactPath);
                }

                return Task.FromResult(text);
            });
    }

    private static ContentSiteIndexRow CreateRow(long id, string artifactPath, IReadOnlyList<string> archetypeTags, IReadOnlyList<string> bracketTags)
    {
        return new ContentSiteIndexRow
        {
            Id = id,
            Source = "EDHRECast",
            Title = $"Artifact {id}",
            VideoUrl = $"https://www.youtube.com/watch?v=video{id}",
            ArtifactPath = artifactPath,
            PublishedUtc = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero),
            IndexedUtc = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero),
            IsVisible = true,
            ArchetypeTags = archetypeTags,
            BracketTags = bracketTags,
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = $"video{id}",
            RssGuid = null
        };
    }

    private static string BuildArtifact(string sourceUrl, string generatedUtc, string summary, params (string Timestamp, string Excerpt)[] clips)
    {
        var clipLines = string.Join(
            Environment.NewLine,
            clips.Select(clip => $"- **[{clip.Timestamp}]** {clip.Excerpt}"));

        return $$"""
---
source: "EDHRECast"
title: "Test Artifact"
url: "{{sourceUrl}}"
generated_utc: "{{generatedUtc}}"
---

## Summary

{{summary}}

## Key Clips

{{clipLines}}

## Tags

ignored
""";
    }

    private sealed class TrackingFeatureFlagCache : IFeatureFlagCache
    {
        private readonly Dictionary<string, bool> _flags;

        public TrackingFeatureFlagCache(Dictionary<string, bool>? flags = null)
        {
            _flags = flags ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }

        public bool IsEnabled(string key) => !_flags.TryGetValue(key, out var enabled) || enabled;

        public IReadOnlyDictionary<string, bool> Snapshot() => _flags;

        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TrackingContentSiteIndexStore : IContentSiteIndexStore
    {
        private readonly IReadOnlyList<ContentSiteIndexRow> _rows;

        public TrackingContentSiteIndexStore(IReadOnlyList<ContentSiteIndexRow> rows)
        {
            _rows = rows;
        }

        public int PublishedRowsQueryCount { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default)
            => Task.FromResult<ContentSiteIndexRow?>(null);

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
        {
            PublishedRowsQueryCount++;
            return Task.FromResult(_rows);
        }

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_rows);

        public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(_rows.FirstOrDefault(row => row.Id == id));

        public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
