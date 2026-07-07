using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Focused tests for the publish-boundary stamp contract: content-only upserts do not stamp,
/// and the dedicated stamp method records one shared instant for the approved-key batch.
/// </summary>
public sealed class ContentPublishStampTests
{
    [Fact]
    public async Task ContentOnlyUpsert_DoesNotStampPushedToProdUtc()
    {
        var store = new RecordingContentSiteIndexStore();
        var row = CreateRow("vid-null");

        await store.UpsertContentColumnsOnlyAsync(row);

        var stored = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "vid-null");

        Assert.NotNull(stored);
        Assert.Null(stored!.PushedToProdUtc);
        Assert.Empty(store.StampCalls);
    }

    [Fact]
    public async Task StampPushedToProdAsync_RecordsApprovedKeys_AndSharedInstant()
    {
        var store = new RecordingContentSiteIndexStore();
        await store.UpsertContentColumnsOnlyAsync(CreateRow("vid-one"));
        await store.UpsertContentColumnsOnlyAsync(CreateRow("vid-two"));
        var pushedUtc = DateTimeOffset.Parse("2026-06-18T23:59:58+00:00");

        await store.StampPushedToProdAsync(
            [(ContentSourceType.Youtube, "vid-one"), (ContentSourceType.Youtube, "vid-two")],
            pushedUtc);

        Assert.Single(store.StampCalls);
        Assert.Equal(pushedUtc, store.StampCalls[0].PushedUtc);

        var first = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "vid-one");
        var second = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "vid-two");
        Assert.Equal(pushedUtc, first!.PushedToProdUtc);
        Assert.Equal(pushedUtc, second!.PushedToProdUtc);
    }

    private static ContentSiteIndexRow CreateRow(string youtubeVideoId)
        => new()
        {
            Id = 0,
            Source = "test-source",
            Title = $"Video {youtubeVideoId}",
            VideoUrl = $"https://youtu.be/{youtubeVideoId}",
            ArtifactPath = $"content-kb/test-source/{youtubeVideoId}.md",
            PublishedUtc = null,
            IndexedUtc = DateTimeOffset.Parse("2026-06-18T22:00:00Z"),
            ArchetypeTags = [],
            BracketTags = [],
            CardCategoryTags = [],
            ApprovalStatus = "approved",
            YoutubeVideoId = youtubeVideoId,
        };

    private sealed class RecordingContentSiteIndexStore : IContentSiteIndexStore
    {
        private readonly List<ContentSiteIndexRow> _rows = [];

        public List<(IReadOnlyList<(string Type, string Value)> Keys, DateTimeOffset PushedUtc)> StampCalls { get; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
        {
            var index = _rows.FindIndex(existing => existing.YoutubeVideoId == row.YoutubeVideoId && existing.RssGuid == row.RssGuid);
            if (index >= 0)
            {
                _rows[index] = row with { PushedToProdUtc = _rows[index].PushedToProdUtc };
            }
            else
            {
                _rows.Add(row);
            }

            return Task.CompletedTask;
        }

        public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default)
            => Task.FromResult(_rows.FirstOrDefault(row => row.YoutubeVideoId == naturalKeyValue));

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ContentSiteIndexRow?> GetPublishedByIdAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> DeleteAllRowsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> SetApprovalStatusAsync(string naturalKeyType, string naturalKeyValue, string status, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> SetApprovalStatusAsync(IReadOnlyList<(string Type, string Value)> keys, string status, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> StampPushedToProdAsync(IReadOnlyList<(string Type, string Value)> keys, DateTimeOffset pushedUtc, CancellationToken cancellationToken = default)
        {
            StampCalls.Add((keys, pushedUtc));
            var count = 0;
            for (var i = 0; i < _rows.Count; i++)
            {
                var match = keys.Any(key => key.Type == ContentSourceType.Youtube && _rows[i].YoutubeVideoId == key.Value);
                if (!match)
                {
                    continue;
                }

                _rows[i] = _rows[i] with { PushedToProdUtc = pushedUtc };
                count++;
            }

            return Task.FromResult(count);
        }

        public Task<int> SetVisibilityAsync(IReadOnlyList<(string Type, string Value)> keys, bool visible, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
