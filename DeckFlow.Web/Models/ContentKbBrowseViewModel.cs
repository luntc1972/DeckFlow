namespace DeckFlow.Web.Models;

/// <summary>
/// View model for the public Content KB browse page.
/// </summary>
public sealed record ContentKbBrowseViewModel
{
    /// <summary>Published entries available for browse.</summary>
    public required IReadOnlyList<Entry> Entries { get; init; }

    /// <summary>Distinct source filter options.</summary>
    public required IReadOnlyList<string> Sources { get; init; }

    /// <summary>Distinct archetype filter options.</summary>
    public required IReadOnlyList<string> Archetypes { get; init; }

    /// <summary>Distinct bracket filter options.</summary>
    public required IReadOnlyList<string> Brackets { get; init; }

    /// <summary>Distinct card-category filter options.</summary>
    public required IReadOnlyList<string> CardCategories { get; init; }

    /// <summary>
    /// Published Content KB entry shown in the hub-card grid.
    /// </summary>
    public sealed record Entry
    {
        /// <summary>Surrogate row identifier.</summary>
        public required long Id { get; init; }

        /// <summary>Entry title.</summary>
        public required string Title { get; init; }

        /// <summary>Stable id used for pinning — YoutubeVideoId or RssGuid.</summary>
        public required string VideoId { get; init; }

        /// <summary>Source display name.</summary>
        public required string Source { get; init; }

        /// <summary>Canonical source URL.</summary>
        public required string SourceUrl { get; init; }

        /// <summary>Detail-page URL.</summary>
        public required string DetailUrl { get; init; }

        /// <summary>Primary archetype shown in the card description.</summary>
        public required string Archetype { get; init; }

        /// <summary>Primary bracket shown in the card description.</summary>
        public required string Bracket { get; init; }

        /// <summary>Primary card category shown in filter metadata.</summary>
        public required string CardCategory { get; init; }

        /// <summary>All archetype filter tags.</summary>
        public required IReadOnlyList<string> ArchetypeTags { get; init; }

        /// <summary>All bracket filter tags.</summary>
        public required IReadOnlyList<string> BracketTags { get; init; }

        /// <summary>All card-category filter tags.</summary>
        public required IReadOnlyList<string> CardCategoryTags { get; init; }
    }
}
