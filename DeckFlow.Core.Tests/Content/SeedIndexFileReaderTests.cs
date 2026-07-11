using DeckFlow.Core.Content;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="SeedIndexFileReader.Read"/> — the shared 3-outcome
/// <c>index-seed.json</c> natural-key reader (Task 3, SYNC-17 foundation). Covers all three
/// outcomes: present-and-parsed with entries, present-and-parsed as a valid empty seed, and
/// absent/unreadable/parse-failed — proving the availability flag never masquerades an
/// unavailable seed as an empty one (T-91-03).
/// </summary>
public sealed class SeedIndexFileReaderTests : IDisposable
{
    private readonly string _tempDir;

    public SeedIndexFileReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"seed-index-reader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Read_TwoEntryFile_ReturnsSeedAvailableTrueWithExactKeySet()
    {
        var path = WriteSeedFile("""
            [
              {
                "naturalKeyType": "youtube_channel",
                "naturalKeyValue": "abc123"
              },
              {
                "naturalKeyType": "podcast_rss",
                "naturalKeyValue": "guid-xyz"
              }
            ]
            """);

        var result = SeedIndexFileReader.Read(path);

        Assert.True(result.SeedAvailable);
        Assert.Equal(2, result.NaturalKeys.Count);
        Assert.Contains("youtube_channel\u0000abc123", result.NaturalKeys);
        Assert.Contains("podcast_rss\u0000guid-xyz", result.NaturalKeys);
    }

    [Fact]
    public void Read_ValidEmptySeedFile_ReturnsSeedAvailableTrueWithEmptyKeySet()
    {
        var path = WriteSeedFile("[]");

        var result = SeedIndexFileReader.Read(path);

        Assert.True(result.SeedAvailable);
        Assert.Empty(result.NaturalKeys);
    }

    [Fact]
    public void Read_MissingFile_ReturnsSeedAvailableFalseWithEmptyKeySet_NoThrow()
    {
        var path = Path.Combine(_tempDir, "does-not-exist.json");

        var result = SeedIndexFileReader.Read(path);

        Assert.False(result.SeedAvailable);
        Assert.Empty(result.NaturalKeys);
    }

    [Fact]
    public void Read_MalformedJsonFile_ReturnsSeedAvailableFalseWithEmptyKeySet_NoThrow()
    {
        var path = WriteSeedFile("{ this is not valid json ][");

        var result = SeedIndexFileReader.Read(path);

        Assert.False(result.SeedAvailable);
        Assert.Empty(result.NaturalKeys);
    }

    [Fact]
    public void Read_KeySeparator_IsU0000NotAPrintableSeparator()
    {
        var path = WriteSeedFile("""
            [
              { "naturalKeyType": "youtube_channel", "naturalKeyValue": "abc123" }
            ]
            """);

        var result = SeedIndexFileReader.Read(path);

        var key = Assert.Single(result.NaturalKeys);
        Assert.Equal("youtube_channel\u0000abc123", key);
        Assert.DoesNotContain(' ', key);
        Assert.Contains('\u0000', key);
    }

    [Fact]
    public void Read_EntryMissingNaturalKeyFields_IsSkippedNotThrown()
    {
        var path = WriteSeedFile("""
            [
              { "naturalKeyType": "youtube_channel", "naturalKeyValue": "" },
              { "naturalKeyType": "youtube_channel", "naturalKeyValue": "kept-1" }
            ]
            """);

        var result = SeedIndexFileReader.Read(path);

        Assert.True(result.SeedAvailable);
        var key = Assert.Single(result.NaturalKeys);
        Assert.Equal("youtube_channel\u0000kept-1", key);
    }

    private string WriteSeedFile(string json)
    {
        var path = Path.Combine(_tempDir, $"index-seed-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
