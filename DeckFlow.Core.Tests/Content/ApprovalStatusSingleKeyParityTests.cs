using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class ApprovalStatusSingleKeyParityTests
{
    [Fact]
    public async Task SetApprovalStatusAsync_OneKeyBulk_SameReturnAndStatusAsSingle()
    {
        var (single, bulk, paths) = await CreateStoresAsync();
        try
        {
            var singleResult = await single.SetApprovalStatusAsync(ContentSourceType.Youtube, "parity", "approved");
            var bulkResult = await bulk.SetApprovalStatusAsync([(ContentSourceType.Youtube, "parity")], "approved");

            Assert.Equal(singleResult, bulkResult);
            Assert.Equal((await single.GetByNaturalKeyAsync(ContentSourceType.Youtube, "parity"))!.ApprovalStatus,
                (await bulk.GetByNaturalKeyAsync(ContentSourceType.Youtube, "parity"))!.ApprovalStatus);
        }
        finally
        {
            Delete(paths);
        }
    }

    [Fact]
    public async Task SetApprovalStatusAsync_OneKeyBulk_SameRevisionBumpAsSingle()
    {
        var (single, bulk, paths) = await CreateStoresAsync();
        try
        {
            await single.SetApprovalStatusAsync(ContentSourceType.Youtube, "parity", "approved");
            await bulk.SetApprovalStatusAsync([(ContentSourceType.Youtube, "parity")], "approved");

            var singleRow = await single.GetByNaturalKeyAsync(ContentSourceType.Youtube, "parity");
            var bulkRow = await bulk.GetByNaturalKeyAsync(ContentSourceType.Youtube, "parity");
            Assert.Equal(singleRow, bulkRow);
        }
        finally
        {
            Delete(paths);
        }
    }

    [Fact]
    public async Task SetApprovalStatusAsync_OneKeyBulk_SameExceptionForUnknownKeyAsSingle()
    {
        var (single, bulk, paths) = await CreateStoresAsync();
        try
        {
            var singleValue = 0;
            var bulkValue = 0;
            var singleResult = await Record.ExceptionAsync(async () => singleValue = await single.SetApprovalStatusAsync(ContentSourceType.Youtube, "unknown", "approved"));
            var bulkResult = await Record.ExceptionAsync(async () => bulkValue = await bulk.SetApprovalStatusAsync([(ContentSourceType.Youtube, "unknown")], "approved"));

            Assert.Equal(singleResult?.GetType(), bulkResult?.GetType());
            if (singleResult is null && bulkResult is null)
            {
                Assert.Equal(singleValue, bulkValue);
            }
        }
        finally
        {
            Delete(paths);
        }
    }

    private static async Task<(ContentSiteIndexStore Single, ContentSiteIndexStore Bulk, (string Single, string Bulk) Paths)> CreateStoresAsync()
    {
        var paths = (Path.Combine(Path.GetTempPath(), $"parity-single-{Guid.NewGuid():N}.db"), Path.Combine(Path.GetTempPath(), $"parity-bulk-{Guid.NewGuid():N}.db"));
        var single = new ContentSiteIndexStore(paths.Item1);
        var bulk = new ContentSiteIndexStore(paths.Item2);
        var row = new ContentSiteIndexRow
        {
            Id = 0,
            Source = "source",
            Title = "title",
            VideoUrl = "https://example.test/parity",
            ArtifactPath = "parity.md",
            PublishedUtc = DateTimeOffset.UtcNow,
            IndexedUtc = DateTimeOffset.UtcNow,
            YoutubeVideoId = "parity",
            ApprovalStatus = "pending",
            ArchetypeTags = [],
            BracketTags = [],
            CardCategoryTags = []
        };
        await single.UpsertContentColumnsOnlyAsync(row);
        await bulk.UpsertContentColumnsOnlyAsync(row);
        return (single, bulk, (paths.Item1, paths.Item2));
    }

    private static void Delete((string Single, string Bulk) paths)
    {
        foreach (var path in new[] { paths.Single, paths.Bulk })
        {
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
