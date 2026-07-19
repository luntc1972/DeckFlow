using System.Text.Json;
using DeckFlow.CLI;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for creator-style seed serialization and export assembly.
/// </summary>
public sealed class CreatorStyleSeedSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void SerializeCreatorStyleExport_RoundTripsProfilesAndDeckCacheRows_WithCamelCaseAndTrailingNewline()
    {
        var profiles = new[]
        {
            CreatorStyleProfileTestData.CreateFullProfile("alpha")
        };
        var deckCacheRows = new[]
        {
            CreateDeckCacheEntry("alpha", "deck-a")
        };

        var profilesJson = CreatorStyleCommandRunners.SerializeCreatorStyleExport(profiles);
        var deckCacheJson = CreatorStyleCommandRunners.SerializeCreatorStyleExport(deckCacheRows);

        Assert.EndsWith("\n", profilesJson, StringComparison.Ordinal);
        Assert.EndsWith("\n", deckCacheJson, StringComparison.Ordinal);
        Assert.Contains("\"slug\": \"alpha\"", profilesJson, StringComparison.Ordinal);
        Assert.Contains("\"creatorSlug\": \"alpha\"", deckCacheJson, StringComparison.Ordinal);

        var roundTrippedProfiles = JsonSerializer.Deserialize<CreatorStyleProfile[]>(profilesJson, JsonOptions);
        var roundTrippedDeckCache = JsonSerializer.Deserialize<CreatorDeckCacheEntry[]>(deckCacheJson, JsonOptions);

        var profile = Assert.Single(roundTrippedProfiles!);
        CreatorStyleProfileTestData.AssertProfilesEqual(profiles[0], profile);

        var deckCache = Assert.Single(roundTrippedDeckCache!);
        Assert.Equal(deckCacheRows[0].CreatorSlug, deckCache.CreatorSlug);
        Assert.Equal(deckCacheRows[0].DeckId, deckCache.DeckId);
        Assert.Equal(deckCacheRows[0].ContentHash, deckCache.ContentHash);
        Assert.Equal(deckCacheRows[0].FolderId, deckCache.FolderId);
        Assert.Equal(deckCacheRows[0].FolderName, deckCache.FolderName);
        Assert.Equal(deckCacheRows[0].Size, deckCache.Size);
        Assert.Equal(deckCacheRows[0].ConfidenceMarker, deckCache.ConfidenceMarker);
        Assert.Equal(deckCacheRows[0].CachedUtc, deckCache.CachedUtc);
        Assert.Equal(deckCacheRows[0].Entries, deckCache.Entries);
    }

    [Fact]
    public void SerializeCreatorStyleExport_ReturnsExactEmptyArrayWithTrailingNewline_ForEmptyList()
    {
        var json = CreatorStyleCommandRunners.SerializeCreatorStyleExport(Array.Empty<CreatorStyleProfile>());

        Assert.Equal("[]\n", json);
    }

    [Fact]
    public async Task BuildCreatorStyleSeedExportAsync_IncludesDeckCacheRows_ForEveryExportedProfileSlug()
    {
        var profiles = new[]
        {
            CreatorStyleProfileTestData.CreateFullProfile("alpha"),
            CreatorStyleProfileTestData.CreateFullProfile("beta")
        };
        var deckCacheRowsBySlug = new Dictionary<string, IReadOnlyList<CreatorDeckCacheEntry>>(StringComparer.Ordinal)
        {
            ["alpha"] = new[]
            {
                CreateDeckCacheEntry("alpha", "deck-a")
            },
            ["beta"] = new[]
            {
                CreateDeckCacheEntry("beta", "deck-b")
            }
        };
        var profileStore = new FakeCreatorStyleProfileStore(profiles);
        var deckCacheStore = new FakeCreatorDeckCacheStore(deckCacheRowsBySlug);

        var export = await CreatorStyleCommandRunners.BuildCreatorStyleSeedExportAsync(
            profileStore,
            deckCacheStore,
            new[] { "alpha", "beta" });

        Assert.Collection(
            export.Profiles,
            profile => Assert.Equal("alpha", profile.Slug),
            profile => Assert.Equal("beta", profile.Slug));
        Assert.Equal(
            export.Profiles.Select(profile => profile.Slug).OrderBy(slug => slug, StringComparer.Ordinal),
            export.DeckCacheRows.Select(entry => entry.CreatorSlug).Distinct(StringComparer.Ordinal).OrderBy(slug => slug, StringComparer.Ordinal));
        Assert.Equal(new[] { "alpha", "beta" }, deckCacheStore.RequestedSlugs);
    }

    private static CreatorDeckCacheEntry CreateDeckCacheEntry(string creatorSlug, string deckId)
        => new()
        {
            CreatorSlug = creatorSlug,
            DeckId = deckId,
            ContentHash = $"hash-{deckId}",
            FolderId = null,
            FolderName = null,
            Size = 100,
            ConfidenceMarker = "exact",
            Entries =
            [
                new DeckEntry
                {
                    Name = "Sol Ring",
                    NormalizedName = "sol ring",
                    Quantity = 1,
                    Board = "mainboard"
                }
            ],
            CachedUtc = DateTimeOffset.Parse("2026-07-18T00:00:00Z")
        };

    private sealed class FakeCreatorStyleProfileStore : ICreatorStyleProfileStore
    {
        private readonly IReadOnlyDictionary<string, CreatorStyleProfile> _profilesBySlug;

        public FakeCreatorStyleProfileStore(IReadOnlyList<CreatorStyleProfile> profiles)
        {
            _profilesBySlug = profiles.ToDictionary(profile => profile.Slug, StringComparer.Ordinal);
        }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult(_profilesBySlug.TryGetValue(slug, out var profile) ? profile : null);

        public Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeCreatorDeckCacheStore : ICreatorDeckCacheStore
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<CreatorDeckCacheEntry>> _rowsBySlug;

        public FakeCreatorDeckCacheStore(IReadOnlyDictionary<string, IReadOnlyList<CreatorDeckCacheEntry>> rowsBySlug)
        {
            _rowsBySlug = rowsBySlug;
        }

        public List<string> RequestedSlugs { get; } = new();

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> GetContentHashAsync(string creatorSlug, string deckId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<IReadOnlyList<CreatorDeckCacheEntry>> GetByCreatorAsync(string creatorSlug, CancellationToken cancellationToken = default)
        {
            RequestedSlugs.Add(creatorSlug);
            return Task.FromResult(_rowsBySlug.TryGetValue(creatorSlug, out var rows) ? rows : Array.Empty<CreatorDeckCacheEntry>());
        }

        public Task UpsertAsync(CreatorDeckCacheEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
