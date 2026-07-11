using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Minimal in-memory <see cref="IContentSiteIndexStore"/> test double exercising ONLY the two
/// members <see cref="SeedManagedBackfill"/> actually calls (<see cref="GetAllRowsAsync"/>,
/// <see cref="SetSeedManagedIfNullAsync"/>) — every other member throws, so a test that
/// accidentally exercises an unrelated store path fails loudly instead of silently no-opping.
/// </summary>
internal sealed class InMemorySeedManagedStore : IContentSiteIndexStore
{
    public List<ContentSiteIndexRow> Rows { get; } = [];

    /// <summary>Ids passed to <see cref="SetSeedManagedIfNullAsync"/> that actually wrote (row was still null).</summary>
    public List<long> ClassifiedIds { get; } = [];

    public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Rows.ToList());

    public Task<int> SetSeedManagedIfNullAsync(long id, bool seedManaged, CancellationToken cancellationToken = default)
    {
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Id == id && Rows[i].SeedManaged is null)
            {
                Rows[i] = Rows[i] with { SeedManaged = seedManaged };
                ClassifiedIds.Add(id);
                count++;
            }
        }

        return Task.FromResult(count);
    }

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ContentSiteIndexRow?> GetPublishedByIdAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> SetApprovalStatusAsync(string naturalKeyType, string naturalKeyValue, string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> SetApprovalStatusAsync(IReadOnlyList<(string Type, string Value)> keys, string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> StampPushedToProdAsync(IReadOnlyList<(string Type, string Value)> keys, DateTimeOffset pushedUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> SetVisibilityAsync(IReadOnlyList<(string Type, string Value)> keys, bool visible, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>
/// Fake <see cref="ISeedKeyMembershipSource"/> returning a canned <see cref="SeedIndexReadResult"/>,
/// or throwing a supplied exception, so tests never touch the filesystem or git.
/// </summary>
internal sealed class FakeSeedKeyMembershipSource : ISeedKeyMembershipSource
{
    private readonly SeedIndexReadResult? _result;
    private readonly Exception? _toThrow;

    public FakeSeedKeyMembershipSource(SeedIndexReadResult result) => _result = result;

    public FakeSeedKeyMembershipSource(Exception toThrow) => _toThrow = toThrow;

    public int CallCount { get; private set; }

    public SeedIndexReadResult GetSeedMembership()
    {
        CallCount++;
        if (_toThrow is not null)
        {
            throw _toThrow;
        }

        return _result!;
    }
}

/// <summary>
/// Behavior coverage for <see cref="SeedManagedBackfill"/> (D-02): a present-key row classifies
/// true, an absent-key row classifies false, an unavailable seed writes nothing and leaves rows
/// NULL (the Codex-HIGH T-91-07 gate), a valid empty seed still classifies (all -&gt; false), a
/// no-key row is skipped without crashing, a throwing membership source is caught, and a second
/// run is a no-op (idempotent).
/// </summary>
public sealed class SeedManagedBackfillTests
{
    [Fact]
    public async Task RunAsync_SeedAvailable_PresentKeyClassifiesTrueAbsentKeyClassifiesFalse()
    {
        var store = new InMemorySeedManagedStore();
        store.Rows.Add(CreateRow(1, youtubeVideoId: "in-seed", seedManaged: null));
        store.Rows.Add(CreateRow(2, youtubeVideoId: "not-in-seed", seedManaged: null));

        var membership = new FakeSeedKeyMembershipSource(
            new SeedIndexReadResult(true, new HashSet<string>(StringComparer.Ordinal) { $"{ContentSourceType.Youtube}\u0000in-seed" }));
        var backfill = new SeedManagedBackfill(store, membership, new FakeLogger<SeedManagedBackfill>());

        await backfill.RunAsync();

        Assert.True(store.Rows.Single(r => r.Id == 1).SeedManaged);
        Assert.False(store.Rows.Single(r => r.Id == 2).SeedManaged);
    }

    [Fact]
    public async Task RunAsync_SeedUnavailable_WritesZeroRowsAndLeavesAllNull()
    {
        var store = new InMemorySeedManagedStore();
        store.Rows.Add(CreateRow(1, youtubeVideoId: "row-one", seedManaged: null));
        store.Rows.Add(CreateRow(2, youtubeVideoId: "row-two", seedManaged: null));

        var membership = new FakeSeedKeyMembershipSource(
            new SeedIndexReadResult(false, new HashSet<string>(StringComparer.Ordinal)));
        var logger = new FakeLogger<SeedManagedBackfill>();
        var backfill = new SeedManagedBackfill(store, membership, logger);

        await backfill.RunAsync();

        Assert.Empty(store.ClassifiedIds);
        Assert.Null(store.Rows.Single(r => r.Id == 1).SeedManaged);
        Assert.Null(store.Rows.Single(r => r.Id == 2).SeedManaged);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_SeedAvailableButEmpty_ClassifiesAllNullRowsFalse()
    {
        var store = new InMemorySeedManagedStore();
        store.Rows.Add(CreateRow(1, youtubeVideoId: "row-one", seedManaged: null));
        store.Rows.Add(CreateRow(2, youtubeVideoId: "row-two", seedManaged: null));

        var membership = new FakeSeedKeyMembershipSource(
            new SeedIndexReadResult(true, new HashSet<string>(StringComparer.Ordinal)));
        var backfill = new SeedManagedBackfill(store, membership, new FakeLogger<SeedManagedBackfill>());

        await backfill.RunAsync();

        Assert.False(store.Rows.Single(r => r.Id == 1).SeedManaged);
        Assert.False(store.Rows.Single(r => r.Id == 2).SeedManaged);
        Assert.Equal(2, store.ClassifiedIds.Count);
    }

    [Fact]
    public async Task RunAsync_RowWithNoDerivableNaturalKey_IsSkippedWithoutThrowing()
    {
        var store = new InMemorySeedManagedStore();
        store.Rows.Add(CreateRow(1, youtubeVideoId: null, rssGuid: null, seedManaged: null));
        store.Rows.Add(CreateRow(2, youtubeVideoId: "keyed-row", seedManaged: null));

        var membership = new FakeSeedKeyMembershipSource(
            new SeedIndexReadResult(true, new HashSet<string>(StringComparer.Ordinal) { $"{ContentSourceType.Youtube}\u0000keyed-row" }));
        var logger = new FakeLogger<SeedManagedBackfill>();
        var backfill = new SeedManagedBackfill(store, membership, logger);

        var exception = await Record.ExceptionAsync(() => backfill.RunAsync());

        Assert.Null(exception);
        Assert.Null(store.Rows.Single(r => r.Id == 1).SeedManaged);
        Assert.True(store.Rows.Single(r => r.Id == 2).SeedManaged);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_MembershipSourceThrows_SkipsEntireRunLogsWarningAndDoesNotThrow()
    {
        var store = new InMemorySeedManagedStore();
        store.Rows.Add(CreateRow(1, youtubeVideoId: "row-one", seedManaged: null));

        var membership = new FakeSeedKeyMembershipSource(new IOException("seed file locked"));
        var logger = new FakeLogger<SeedManagedBackfill>();
        var backfill = new SeedManagedBackfill(store, membership, logger);

        var exception = await Record.ExceptionAsync(() => backfill.RunAsync());

        Assert.Null(exception);
        Assert.Empty(store.ClassifiedIds);
        Assert.Null(store.Rows.Single(r => r.Id == 1).SeedManaged);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task RunAsync_AlreadyClassifiedRow_IsNeverRewritten()
    {
        var store = new InMemorySeedManagedStore();
        store.Rows.Add(CreateRow(1, youtubeVideoId: "already-true", seedManaged: true));
        store.Rows.Add(CreateRow(2, youtubeVideoId: "already-false", seedManaged: false));

        // Membership would classify both differently if (wrongly) re-evaluated.
        var membership = new FakeSeedKeyMembershipSource(
            new SeedIndexReadResult(true, new HashSet<string>(StringComparer.Ordinal) { $"{ContentSourceType.Youtube}\u0000already-false" }));
        var backfill = new SeedManagedBackfill(store, membership, new FakeLogger<SeedManagedBackfill>());

        await backfill.RunAsync();

        Assert.Empty(store.ClassifiedIds);
        Assert.True(store.Rows.Single(r => r.Id == 1).SeedManaged);
        Assert.False(store.Rows.Single(r => r.Id == 2).SeedManaged);
    }

    [Fact]
    public async Task RunAsync_SecondRun_IsIdempotentAndWritesNothing()
    {
        var store = new InMemorySeedManagedStore();
        store.Rows.Add(CreateRow(1, youtubeVideoId: "in-seed", seedManaged: null));
        store.Rows.Add(CreateRow(2, youtubeVideoId: "not-in-seed", seedManaged: null));

        var membership = new FakeSeedKeyMembershipSource(
            new SeedIndexReadResult(true, new HashSet<string>(StringComparer.Ordinal) { $"{ContentSourceType.Youtube}\u0000in-seed" }));
        var backfill = new SeedManagedBackfill(store, membership, new FakeLogger<SeedManagedBackfill>());

        await backfill.RunAsync();
        Assert.Equal(2, store.ClassifiedIds.Count);

        store.ClassifiedIds.Clear();
        await backfill.RunAsync();

        Assert.Empty(store.ClassifiedIds);
        Assert.True(store.Rows.Single(r => r.Id == 1).SeedManaged);
        Assert.False(store.Rows.Single(r => r.Id == 2).SeedManaged);
    }

    private static ContentSiteIndexRow CreateRow(
        long id,
        string? youtubeVideoId,
        bool? seedManaged,
        string? rssGuid = null)
        => new()
        {
            Id = id,
            Source = "The Command Zone",
            Title = $"Video {id}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId ?? "none"}",
            ArtifactPath = $"content-kb/command-zone/{id}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = ["combo"],
            BracketTags = ["cEDH"],
            CardCategoryTags = ["win-cons"],
            YoutubeVideoId = youtubeVideoId,
            RssGuid = rssGuid,
            ApprovalStatus = "approved",
            SeedManaged = seedManaged,
        };
}
