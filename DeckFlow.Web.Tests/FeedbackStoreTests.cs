using System.IO;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class FeedbackStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FeedbackStore _store;

    public FeedbackStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"feedback-test-{Guid.NewGuid():N}.db");
        _store = new FeedbackStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    private static FeedbackSubmission SampleSubmission(FeedbackType type = FeedbackType.Bug, string message = "Something is broken please help.") =>
        new() { Type = type, Message = message, Email = "user@example.com" };

    private static FeedbackRequestContext SampleContext(string ip = "203.0.113.7") =>
        new(ip, "UA/1.0", "https://decksync.test/deck/1", "1.2.3");

    [Fact]
    public async Task AddAsync_PersistsItem_AndReturnsNewId()
    {
        var id = await _store.AddAsync(SampleSubmission(), SampleContext());

        Assert.True(id > 0);
        var item = await _store.GetAsync(id);
        Assert.NotNull(item);
        Assert.Equal(FeedbackType.Bug, item!.Type);
        Assert.Equal("Something is broken please help.", item.Message);
        Assert.Equal("user@example.com", item.Email);
        Assert.Equal("UA/1.0", item.UserAgent);
        Assert.Equal("https://decksync.test/deck/1", item.PageUrl);
        Assert.Equal("1.2.3", item.AppVersion);
        Assert.Equal(FeedbackStatus.New, item.Status);
        Assert.False(string.IsNullOrWhiteSpace(item.IpHash));
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        var item = await _store.GetAsync(9999);
        Assert.Null(item);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatus()
    {
        var id1 = await _store.AddAsync(SampleSubmission(FeedbackType.Bug, "First message, ten plus chars."), SampleContext());
        var id2 = await _store.AddAsync(SampleSubmission(FeedbackType.Suggestion, "Second message, ten plus chars."), SampleContext());
        await _store.UpdateStatusAsync(id2, FeedbackStatus.Read);

        var newItems = await _store.ListAsync(new FeedbackListQuery { Status = FeedbackStatus.New });
        Assert.Single(newItems);
        Assert.Equal(id1, newItems[0].Id);

        var readItems = await _store.ListAsync(new FeedbackListQuery { Status = FeedbackStatus.Read });
        Assert.Single(readItems);
        Assert.Equal(id2, readItems[0].Id);
    }

    [Fact]
    public async Task ListAsync_FiltersByType()
    {
        await _store.AddAsync(SampleSubmission(FeedbackType.Bug, "Bug report message text."), SampleContext());
        await _store.AddAsync(SampleSubmission(FeedbackType.Suggestion, "Suggestion idea message here."), SampleContext());

        var bugs = await _store.ListAsync(new FeedbackListQuery { Status = null, Type = FeedbackType.Bug });
        Assert.Single(bugs);
        Assert.Equal(FeedbackType.Bug, bugs[0].Type);
    }

    [Fact]
    public async Task ListAsync_OrdersByCreatedDesc()
    {
        var first = await _store.AddAsync(SampleSubmission(FeedbackType.Comment, "Older message text here."), SampleContext());
        await Task.Delay(50);
        var second = await _store.AddAsync(SampleSubmission(FeedbackType.Comment, "Newer message text here."), SampleContext());

        var items = await _store.ListAsync(new FeedbackListQuery { Status = null });
        Assert.Equal(second, items[0].Id);
        Assert.Equal(first, items[1].Id);
    }

    [Fact]
    public async Task ListAsync_Pagination_ReturnsRequestedSlice()
    {
        for (int i = 0; i < 5; i++)
        {
            await _store.AddAsync(SampleSubmission(FeedbackType.Comment, $"Message number {i} of five."), SampleContext());
        }

        var page1 = await _store.ListAsync(new FeedbackListQuery { Status = null, Page = 1, PageSize = 2 });
        var page2 = await _store.ListAsync(new FeedbackListQuery { Status = null, Page = 2, PageSize = 2 });
        var page3 = await _store.ListAsync(new FeedbackListQuery { Status = null, Page = 3, PageSize = 2 });

        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.Single(page3);
    }

    [Fact]
    public async Task UpdateStatusAsync_TransitionsStatus()
    {
        var id = await _store.AddAsync(SampleSubmission(), SampleContext());
        await _store.UpdateStatusAsync(id, FeedbackStatus.Archived);
        var item = await _store.GetAsync(id);
        Assert.Equal(FeedbackStatus.Archived, item!.Status);
    }

    [Fact]
    public async Task DeleteAsync_RemovesItem()
    {
        var id = await _store.AddAsync(SampleSubmission(), SampleContext());
        await _store.DeleteAsync(id);
        Assert.Null(await _store.GetAsync(id));
    }

    [Fact]
    public async Task CountsByStatusAsync_ReturnsCorrectTotals()
    {
        var a = await _store.AddAsync(SampleSubmission(), SampleContext());
        var b = await _store.AddAsync(SampleSubmission(), SampleContext());
        var c = await _store.AddAsync(SampleSubmission(), SampleContext());
        await _store.UpdateStatusAsync(b, FeedbackStatus.Read);
        await _store.UpdateStatusAsync(c, FeedbackStatus.Archived);

        var counts = await _store.CountsByStatusAsync();
        Assert.Equal(1, counts[FeedbackStatus.New]);
        Assert.Equal(1, counts[FeedbackStatus.Read]);
        Assert.Equal(1, counts[FeedbackStatus.Archived]);
    }

    [Fact]
    public async Task HashIp_SameInput_ProducesSameHash()
    {
        _ = await _store.AddAsync(SampleSubmission(), SampleContext("10.0.0.1"));

        var h1 = _store.HashIp("198.51.100.9");
        var h2 = _store.HashIp("198.51.100.9");
        var h3 = _store.HashIp("198.51.100.10");

        Assert.Equal(h1, h2);
        Assert.NotEqual(h1, h3);
    }

    [Fact]
    public async Task HashIp_DifferentSalts_ProduceDifferentHashes()
    {
        _ = await _store.AddAsync(SampleSubmission(), SampleContext("10.0.0.1"));
        var firstHash = _store.HashIp("203.0.113.50");

        var otherDb = Path.Combine(Path.GetTempPath(), $"feedback-test-{Guid.NewGuid():N}.db");
        try
        {
            var other = new FeedbackStore(otherDb);
            _ = await other.AddAsync(SampleSubmission(), SampleContext("10.0.0.1"));
            var otherHash = other.HashIp("203.0.113.50");
            Assert.NotEqual(firstHash, otherHash);
        }
        finally
        {
            if (File.Exists(otherDb)) File.Delete(otherDb);
        }
    }
}
