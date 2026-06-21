using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeckFlow.Core.Content;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for <see cref="CreatorSourceStore"/> using a temporary SQLite content KB database.
/// </summary>
public sealed class CreatorSourceStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly CreatorSourceStore _store;

    public CreatorSourceStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"creator-source-test-{Guid.NewGuid():N}.db");
        _store = new CreatorSourceStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task AddAsync_PersistsCreator_VisibleFromNewStoreInstance()
    {
        await _store.AddAsync("The Command Zone", "https://youtube.com/@TheCommandZone");

        var reopened = new CreatorSourceStore(_dbPath);
        var creators = await reopened.ListAsync();

        var creator = Assert.Single(creators);
        Assert.Equal("The Command Zone", creator.DisplayName);
        Assert.Equal("https://youtube.com/@TheCommandZone", creator.ChannelRef);
        Assert.True(creator.Id > 0);
    }

    [Theory]
    [InlineData("https://youtube.com/@CZ", "https://youtube.com/@CZ")]
    [InlineData("https://youtube.com/@CZ", "  https://youtube.com/@CZ  ")]
    [InlineData("https://youtube.com/@CZ", "https://youtube.com/@cz")]
    public async Task AddAsync_DedupesOnNormalizedChannelRef(string first, string second)
    {
        await _store.AddAsync("Creator A", first);
        await _store.AddAsync("Creator A again", second);

        var creators = await _store.ListAsync();
        Assert.Single(creators);
    }

    [Fact]
    public async Task AddAsync_DistinctChannels_BothPersisted()
    {
        await _store.AddAsync("Creator A", "https://youtube.com/@A");
        await _store.AddAsync("Creator B", "https://youtube.com/@B");

        var creators = await _store.ListAsync();
        Assert.Equal(2, creators.Count);
    }

    [Fact]
    public async Task RemoveAsync_RemovesById_ReturnsWhetherRemoved()
    {
        await _store.AddAsync("Creator A", "https://youtube.com/@A");
        var creator = Assert.Single(await _store.ListAsync());

        Assert.True(await _store.RemoveAsync(creator.Id));
        Assert.Empty(await _store.ListAsync());
        Assert.False(await _store.RemoveAsync(creator.Id));
    }

    [Fact]
    public async Task ListAsync_OrdersByDisplayNameThenId()
    {
        await _store.AddAsync("Zebra", "https://youtube.com/@Z");
        await _store.AddAsync("Alpha", "https://youtube.com/@A");
        await _store.AddAsync("Mango", "https://youtube.com/@M");

        var names = (await _store.ListAsync()).Select(c => c.DisplayName).ToArray();
        Assert.Equal(new[] { "Alpha", "Mango", "Zebra" }, names);
    }

    [Fact]
    public async Task EnsureSchemaAsync_IsIdempotent()
    {
        await _store.EnsureSchemaAsync();
        await _store.EnsureSchemaAsync();

        await _store.AddAsync("Creator A", "https://youtube.com/@A");
        Assert.Single(await _store.ListAsync());
    }
}
