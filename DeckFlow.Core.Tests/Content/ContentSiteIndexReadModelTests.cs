using System;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Regression guards for the shared content-site-index read surface
/// (<see cref="ContentSiteIndexReadColumns"/> / <see cref="ContentSiteIndexRowMapper"/>).
/// These lock the cycle-16 drift fix: ProdContentReader and the store must read and map the
/// SAME column set, including <c>awaiting_confirm_utc</c>, which had silently dropped out of the
/// pull path.
/// </summary>
public sealed class ContentSiteIndexReadModelTests
{
    [Fact]
    public void SelectList_IncludesAwaitingConfirmAndSeedManaged()
    {
        Assert.Contains("awaiting_confirm_utc", ContentSiteIndexReadColumns.SelectList);
        Assert.Contains("seed_managed", ContentSiteIndexReadColumns.SelectList);
    }

    [Fact]
    public void ToRow_PreservesAwaitingConfirmUtc()
    {
        var awaiting = new DateTimeOffset(2026, 7, 11, 8, 0, 0, TimeSpan.Zero);
        var model = new ContentSiteIndexReadModel
        {
            Id = 1,
            Source = "edhrecast",
            Title = "Example",
            VideoUrl = "https://example.test/v",
            ArtifactPath = "content-kb/edhrecast/abc.md",
            ArchetypeTags = string.Empty,
            BracketTags = string.Empty,
            CardCategoryTags = string.Empty,
            NaturalKeyType = ContentSourceType.Youtube,
            NaturalKeyValue = "abc123",
            ApprovalStatus = "approved",
            AwaitingConfirmUtc = awaiting,
        };

        var row = ContentSiteIndexRowMapper.ToRow(model);

        Assert.Equal(awaiting, row.AwaitingConfirmUtc);
        Assert.Equal("abc123", row.YoutubeVideoId);
    }
}
