using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for source-name slug generation used by content source seeding.
/// </summary>
public sealed class SlugifySourceNameTests
{
    [Fact]
    public void Slugify_LowercasesAsciiNames()
    {
        Assert.Equal("mtggoldfish", SlugifySourceName.Slugify("MTGGoldfish"));
    }

    [Fact]
    public void Slugify_CollapsesPunctuationRunsAndTrimsDashes()
    {
        Assert.Equal("the-command-zone", SlugifySourceName.Slugify("The Command Zone!!"));
    }

    [Fact]
    public void Slugify_EmptyNameFallsBackToDeterministicSlug()
    {
        var slug = SlugifySourceName.Slugify("");

        Assert.Equal("source", slug);
        Assert.False(string.IsNullOrWhiteSpace(slug));
    }

    [Fact]
    public void Slugify_NonAsciiOnlyNameFallsBackToAsciiSlug()
    {
        var slug = SlugifySourceName.Slugify("日本語");

        Assert.Equal("source", slug);
        Assert.False(string.IsNullOrWhiteSpace(slug));
        Assert.All(slug, character => Assert.InRange(character, 'a', 'z'));
    }

    [Fact]
    public async Task ListEnabledSourcesAsync_ReturnsSeededContentSource()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"content-source-list-{Guid.NewGuid():N}.db");
        var store = new ContentSourceStore(dbPath);

        try
        {
            var id = await store.InsertSourceAsync(
                SlugifySourceName.Slugify("MTGGoldfish"),
                "MTGGoldfish",
                ContentSourceType.Youtube,
                "https://www.youtube.com/@MTGGoldfish");

            var sources = await store.ListEnabledSourcesAsync();

            var source = Assert.Single(sources);
            Assert.Equal(id, source.Id);
            Assert.Equal("mtggoldfish", source.SourceSlug);
            Assert.Equal("https://www.youtube.com/@MTGGoldfish", source.SourceUrl);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.Delete(dbPath);
            }
        }
    }
}
