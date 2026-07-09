using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="ContentIndexExportRow.From"/>'s SYNC-17/D-01 seed-ownership stamping: every
/// exported row carries <c>seedManaged=true</c>, hardcoded regardless of the source row's own
/// classification (Pitfall 4), and the field survives a JSON round-trip under the camelCase naming
/// policy the seed file uses.
/// </summary>
public sealed class ContentIndexExportRowTests
{
    private static readonly DateTimeOffset IndexedAt = DateTimeOffset.Parse("2026-06-16T00:00:00Z");

    private static ContentSiteIndexRow BuildRow(bool? seedManaged)
        => new()
        {
            Id = 1,
            YoutubeVideoId = "vid-1",
            Source = "test-source",
            Title = "Test Title",
            VideoUrl = "https://youtube.com/watch?v=vid-1",
            ArtifactPath = "content-kb/test-source/vid-1.md",
            IndexedUtc = IndexedAt,
            ArchetypeTags = [],
            BracketTags = [],
            CardCategoryTags = [],
            SeedManaged = seedManaged,
        };

    [Fact]
    public void From_UnclassifiedSourceRow_SetsSeedManagedTrue()
    {
        var row = BuildRow(seedManaged: null);

        var exportRow = ContentIndexExportRow.From(row);

        Assert.True(exportRow.SeedManaged);
    }

    [Fact]
    public void From_ProdOwnedSourceRow_StillSetsSeedManagedTrue_HardcodedNotPassthrough()
    {
        // Pitfall 4: a row classified prod-owned (seed_managed=false) that nonetheless reaches the
        // seed export must still be stamped true — export membership itself proves seed-managed. A
        // regression to `row.SeedManaged` (passthrough) would leak `false` into the seed JSON.
        var row = BuildRow(seedManaged: false);

        var exportRow = ContentIndexExportRow.From(row);

        Assert.True(exportRow.SeedManaged);
    }

    [Fact]
    public void From_SeedManagedTrueSourceRow_SetsSeedManagedTrue()
    {
        var row = BuildRow(seedManaged: true);

        var exportRow = ContentIndexExportRow.From(row);

        Assert.True(exportRow.SeedManaged);
    }

    [Fact]
    public void SeedManaged_RoundTripsThroughCamelCaseJson()
    {
        var exportRow = ContentIndexExportRow.From(BuildRow(seedManaged: null));
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var json = JsonSerializer.Serialize(exportRow, options);
        var roundTripped = JsonSerializer.Deserialize<ContentIndexExportRow>(json, options);

        Assert.Contains("\"seedManaged\":true", json, StringComparison.Ordinal);
        Assert.NotNull(roundTripped);
        Assert.True(roundTripped!.SeedManaged);
    }
}
