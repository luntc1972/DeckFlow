using System.Text.Json;
using DeckFlow.Core.Content;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Validates the synthetic Content KB E2E fixture against the application's seed reader.</summary>
public sealed class ContentKbE2eFixtureSeedTests
{
    [Fact]
    public void Read_FixtureSeed_ParsesAndReferencesExistingArtifactBodies()
    {
        var seedPath = FindFixtureSeedPath();

        var seed = SeedIndexFileReader.Read(seedPath);
        Assert.True(seed.SeedAvailable);

        using var document = JsonDocument.Parse(File.ReadAllText(seedPath));
        var entries = document.RootElement.EnumerateArray().ToArray();
        Assert.NotEmpty(entries);
        Assert.Contains(entries, entry => entry.GetProperty("naturalKeyValue").GetString()?.StartsWith("e2e-visible-", StringComparison.Ordinal) == true);
        Assert.Contains(entries, entry => entry.GetProperty("naturalKeyValue").GetString()?.StartsWith("e2e-publish-", StringComparison.Ordinal) == true);

        var fixtureRoot = Directory.GetParent(Directory.GetParent(Directory.GetParent(seedPath)!.FullName)!.FullName)!.FullName;
        foreach (var entry in entries)
        {
            var artifactPath = entry.GetProperty("artifactPath").GetString();
            Assert.False(string.IsNullOrWhiteSpace(artifactPath));
            Assert.True(File.Exists(Path.Combine(fixtureRoot, artifactPath!)), $"Missing fixture artifact: {artifactPath}");
        }
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
