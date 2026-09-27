using System.Text.Json;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Validates the synthetic Content KB E2E fixture against the application's seed reader.</summary>
public sealed class ContentKbE2eFixtureSeedTests
{
    [Fact]
    public void Read_FixtureSeed_ParsesAndReferencesExistingArtifactBodies()
    {
        var seedPath = FindFixtureSeedPath();

        var entries = JsonSerializer.Deserialize<ContentKbSeedEntry[]>(File.ReadAllText(seedPath), JsonOptions)
            ?? Array.Empty<ContentKbSeedEntry>();
        Assert.NotEmpty(entries);
        Assert.Contains(entries, entry => entry.NaturalKeyValue.StartsWith("e2e-visible-", StringComparison.Ordinal));
        Assert.Contains(entries, entry => entry.NaturalKeyValue.StartsWith("e2e-publish-", StringComparison.Ordinal));

        var fixtureRoot = Directory.GetParent(Directory.GetParent(Directory.GetParent(seedPath)!.FullName)!.FullName)!.FullName;
        foreach (var entry in entries)
        {
            Assert.True(File.Exists(Path.Combine(fixtureRoot, entry.ArtifactPath)), $"Missing fixture artifact: {entry.ArtifactPath}");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

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
        public string? BodySha256 { get; init; }
    }

    private static string FindFixtureSeedPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "DeckFlow.Web", "e2e", "fixtures", "content-kb", "seed", "index-seed.json");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("Could not locate the Content KB E2E fixture seed.");
    }
}
