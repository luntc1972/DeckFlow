using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services;

/// <summary>
/// Reads the committed Content KB seed JSON and upserts it without clobbering curation.
/// </summary>
public sealed class ContentKbSeedLoader : IContentKbSeedLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ContentKbArtifactPathResolver _resolver;
    private readonly IContentSiteIndexStore _store;
    private readonly ILogger<ContentKbSeedLoader> _logger;

    /// <summary>
    /// Creates a seed loader.
    /// </summary>
    /// <param name="resolver">Artifact path resolver.</param>
    /// <param name="store">Content site-index store.</param>
    /// <param name="logger">Logger.</param>
    public ContentKbSeedLoader(
        ContentKbArtifactPathResolver resolver,
        IContentSiteIndexStore store,
        ILogger<ContentKbSeedLoader> logger)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);

        _resolver = resolver;
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> LoadIfPresentAsync(CancellationToken cancellationToken = default)
    {
        var seedFilePath = _resolver.SeedFilePath;
        if (!File.Exists(seedFilePath))
        {
            _logger.LogInformation("Content KB seed file not found; skipping seed load.");
            return 0;
        }

        await using var stream = File.OpenRead(seedFilePath);
        var entries = await JsonSerializer
            .DeserializeAsync<ContentKbSeedEntry[]>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false)
            ?? Array.Empty<ContentKbSeedEntry>();

        foreach (var entry in entries)
        {
            var row = BuildRow(entry);
            await _store.UpsertRowPreservingVisibilityAsync(row, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Content KB seed load complete: {RowCount} rows.", entries.Length);
        return entries.Length;
    }

    private static ContentSiteIndexRow BuildRow(ContentKbSeedEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.NaturalKeyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.NaturalKeyValue);

        return new ContentSiteIndexRow
        {
            Id = 0,
            Source = entry.Source,
            Title = entry.Title,
            VideoUrl = entry.VideoUrl,
            ArtifactPath = entry.ArtifactPath,
            PublishedUtc = entry.PublishedUtc,
            IndexedUtc = entry.IndexedUtc,
            ArchetypeTags = entry.ArchetypeTags,
            BracketTags = entry.BracketTags,
            CardCategoryTags = entry.CardCategoryTags,
            YoutubeVideoId = entry.NaturalKeyType == ContentSourceType.Youtube ? entry.NaturalKeyValue : null,
            RssGuid = entry.NaturalKeyType == ContentSourceType.Podcast ? entry.NaturalKeyValue : null,
        };
    }

    private sealed record ContentKbSeedEntry
    {
        public required string NaturalKeyType { get; init; }

        public required string NaturalKeyValue { get; init; }

        public required string Source { get; init; }

        public required string Title { get; init; }

        public required string VideoUrl { get; init; }

        public required string ArtifactPath { get; init; }

        public DateTimeOffset? PublishedUtc { get; init; }

        public required DateTimeOffset IndexedUtc { get; init; }

        public required IReadOnlyList<string> ArchetypeTags { get; init; }

        public required IReadOnlyList<string> BracketTags { get; init; }

        public required IReadOnlyList<string> CardCategoryTags { get; init; }
    }
}
