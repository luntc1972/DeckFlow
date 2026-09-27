using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Fast unit tests for <see cref="DirectPushCoordinator"/> — the DirectPush orchestration extracted
/// from the page code-behind (H1 split). These exercise the content-diff classification and the
/// prod read/write sequences directly with fakes, without the bUnit render the logic previously
/// required.
/// </summary>
public sealed class DirectPushCoordinatorTests
{
    // Fixed timestamps so content signatures are deterministic across rows (no UtcNow drift).
    private static readonly DateTimeOffset IndexedAt = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PublishedAt = new(2026, 5, 30, 8, 0, 0, TimeSpan.Zero);

    private static ContentSiteIndexRow Youtube(long id, string videoId, string title = "Title")
        => new()
        {
            Id = id,
            Source = "test-channel",
            Title = title,
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            PublishedUtc = PublishedAt,
            IndexedUtc = IndexedAt,
            ApprovalStatus = "approved",
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
        };

    private static ContentSiteIndexRow Podcast(long id, string guid, string title = "Title")
        => new()
        {
            Id = id,
            Source = "test-podcast",
            Title = title,
            VideoUrl = $"https://pod.example/{guid}",
            ArtifactPath = $"content-kb/test-podcast/{guid}.md",
            PublishedUtc = PublishedAt,
            IndexedUtc = IndexedAt,
            ApprovalStatus = "approved",
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            RssGuid = guid,
        };

    private static DirectPushCoordinator Build(
        FakeContentSiteIndexStore local,
        FakeContentSiteIndexStore prod,
        FakeSshArtifactUploader? uploader = null,
        string artifactRoot = "/data/content-kb",
        FakeContentKbOrchestrator? orchestrator = null,
        IProdContentReader? prodReader = null,
        CreatorSuppressionRowFilter? filter = null,
        IPrivateKbRootProvider? privateKbRootProvider = null)
        => new(
            local,
            uploader ?? new FakeSshArtifactUploader(),
            new FakeProdStoreFactory(prod),
            new StudioProdConnectionSource(new ConfigurationBuilder().Build()),
            new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot },
            privateKbRootProvider ?? new StudioPrivateKbRootProvider(null, Path.GetTempPath()),
            orchestrator ?? new FakeContentKbOrchestrator(),
            // D-05: flag OFF by default — [skip render] behavior stays byte-identical unless a test
            // explicitly turns the flag on (see TestDoubles/FakeDirectPushFlagReader.cs).
            prodReader ?? new FakeDirectPushFlagReader(),
            filter ?? new CreatorSuppressionRowFilter(new FakeCreatorSuppressionStore(), new FakeCreatorIdentityResolver()));

    [Fact]
    public async Task UploadArtifactsAsync_PrivateRoot_FlagOn_RefusesBeforeUpload()
    {
        var uploader = new FakeSshArtifactUploader();
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), uploader: uploader,
            prodReader: new FakeDirectPushFlagReader { FlagValue = true });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.UploadArtifactsAsync(
            new[] { Youtube(1, "flag-on") }, "/data", new Progress<SshUploadResult>(), CancellationToken.None));

        Assert.Contains("sync.directpush-gitbody to stay OFF", exception.Message, StringComparison.Ordinal);
        Assert.Empty(uploader.UploadedFiles);
    }

    [Fact]
    public async Task WriteContentAsync_PrivateRoot_FlagUnknown_RefusesBeforeUpsert()
    {
        var prod = new FakeContentSiteIndexStore();
        var coordinator = Build(new FakeContentSiteIndexStore(), prod,
            prodReader: new FakeDirectPushFlagReader { FlagReadIndeterminate = true });

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.WriteContentAsync(
            new[] { Youtube(1, "flag-unknown") }, CancellationToken.None));

        Assert.Empty(prod.BatchUpsertCalls);
    }

    [Fact]
    public async Task ExportBodiesToPrivateKbRootAsync_FlagOff_WritesPrivateRootWithoutGit()
    {
        var orchestrator = new FakeContentKbOrchestrator();
        var privateRoot = Path.Combine(Path.GetTempPath(), $"direct-push-private-{Guid.NewGuid():N}");
        Directory.CreateDirectory(privateRoot);
        try
        {
            var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(),
                orchestrator: orchestrator,
                privateKbRootProvider: new StudioPrivateKbRootProvider(null, privateRoot));

            var result = await coordinator.ExportBodiesToPrivateKbRootAsync(new[] { Youtube(1, "private-root") }, "/data", CancellationToken.None);

            Assert.Equal(Path.Combine(privateRoot, "content-kb", "seed", "index-seed.json"), orchestrator.ExportToFilePaths.Single());
            Assert.Equal(1, result);
        }
        finally
        {
            Directory.Delete(privateRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExportBodiesToPrivateKbRootAsync_UnsetRoot_FailsBeforeCopyOrGit()
    {
        var orchestrator = new FakeContentKbOrchestrator();
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(),
            orchestrator: orchestrator,
            privateKbRootProvider: new StudioPrivateKbRootProvider(null, null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ExportBodiesToPrivateKbRootAsync(new[] { Youtube(1, "unset-root") }, "/data", CancellationToken.None));

        Assert.Contains("DECKFLOW_KB_ROOT", exception.Message, StringComparison.Ordinal);
        Assert.Empty(orchestrator.CopyArtifactsCalls);
    }

    // ── ClassifyDiff (pure) ─────────────────────────────────────────────────

    [Fact]
    public void ClassifyDiff_RowNotInProd_IsNew_AndInPublishSet()
    {
        var local = new[] { Youtube(1, "aaa") };
        var prod = Array.Empty<ContentSiteIndexRow>();

        var diff = DirectPushCoordinator.ClassifyDiff(local, prod);

        Assert.Equal(1, diff.NewCount);
        Assert.Equal(0, diff.UpdatedCount);
        Assert.Equal(0, diff.UnchangedCount);
        Assert.Single(diff.PublishRows);
        Assert.True(diff.DiffRows[0].IsNew);
    }

    [Fact]
    public void ClassifyDiff_SameKeyDifferentContent_IsUpdated_AndInPublishSet()
    {
        var local = new[] { Youtube(1, "aaa", title: "New Title") };
        var prod = new[] { Youtube(99, "aaa", title: "Old Title") };

        var diff = DirectPushCoordinator.ClassifyDiff(local, prod);

        Assert.Equal(0, diff.NewCount);
        Assert.Equal(1, diff.UpdatedCount);
        Assert.Equal(0, diff.UnchangedCount);
        Assert.Single(diff.PublishRows);
        Assert.False(diff.DiffRows[0].IsNew);
    }

    [Fact]
    public void ClassifyDiff_SameKeyIdenticalContent_IsUnchanged_AndExcludedFromPublish()
    {
        var local = new[] { Youtube(1, "aaa", title: "Same") };
        var prod = new[] { Youtube(99, "aaa", title: "Same") };

        var diff = DirectPushCoordinator.ClassifyDiff(local, prod);

        Assert.Equal(0, diff.NewCount);
        Assert.Equal(0, diff.UpdatedCount);
        Assert.Equal(1, diff.UnchangedCount);
        Assert.Empty(diff.PublishRows);
        Assert.Empty(diff.DiffRows);
    }

    [Fact]
    public void ClassifyDiff_YoutubeAndPodcastShareKeyValue_DoNotCollide()
    {
        // Why: the composite-key data-loss regression (Codex MED). A local youtube row and a prod
        // podcast row sharing the same natural-key VALUE must NOT match — otherwise the local row
        // could be misclassified Unchanged and silently skip its publish.
        var local = new[] { Youtube(1, "shared") };
        var prod = new[] { Podcast(99, "shared") };

        var diff = DirectPushCoordinator.ClassifyDiff(local, prod);

        Assert.Equal(1, diff.NewCount);
        Assert.Equal(0, diff.UnchangedCount);
        Assert.Single(diff.PublishRows);
        // The diff now carries the stored vocabulary discriminator (D-07), not the short "youtube".
        Assert.Equal(ContentSourceType.Youtube, diff.DiffRows[0].KeyType);
    }

    [Fact]
    public void ClassifyDiff_RowWithNoNaturalKey_IsSkipped_AndWarns_WhenLoggerSupplied()
    {
        // D-08 (Codex MED-3): a local row with neither a YouTube id nor an RSS guid is skipped, and a
        // structured warning naming the row is logged when a logger is supplied.
        var orphan = Youtube(1, "keyed") with { YoutubeVideoId = null, RssGuid = null, Title = "Orphan row" };
        var logger = new RecordingTestLogger();

        var diff = DirectPushCoordinator.ClassifyDiff(new[] { orphan }, Array.Empty<ContentSiteIndexRow>(), logger);

        Assert.Equal(0, diff.NewCount);
        Assert.Empty(diff.PublishRows);
        var warning = Assert.Single(logger.Warnings);
        Assert.Contains("Orphan row", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void ProdStoreFactory_Create_BuildsSchemaEnsureDisabledStore_NoDdlOnDeadConnection()
    {
        // D-10 wiring proof: the factory builds a schema-ensure-DISABLED store, so EnsureSchemaAsync
        // early-returns without ever opening a connection. Against an unreachable-but-well-formed prod
        // connection string, that completes without throwing; a regression to schema-ensure-ON would
        // attempt the dead connection and throw. (The zero-DDL invariant itself is locked by 88-01's
        // recording-connection test.)
        var store = new ProdStoreFactory().Create(
            "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1");

        // Should NOT throw — the switch is off, so no connection is opened.
        var ex = Record.Exception(() => store.EnsureSchemaAsync().GetAwaiter().GetResult());
        Assert.Null(ex);
    }

    [Fact]
    public void ClassifyDiff_MixedSet_CountsEachBucket()
    {
        var local = new[]
        {
            Youtube(1, "new1"),
            Youtube(2, "upd", title: "Local"),
            Youtube(3, "same", title: "Same"),
        };
        var prod = new[]
        {
            Youtube(20, "upd", title: "Prod"),
            Youtube(30, "same", title: "Same"),
        };

        var diff = DirectPushCoordinator.ClassifyDiff(local, prod);

        Assert.Equal(1, diff.NewCount);
        Assert.Equal(1, diff.UpdatedCount);
        Assert.Equal(1, diff.UnchangedCount);
        Assert.Equal(2, diff.PublishRows.Count);
    }

    // ── LoadInitDataAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task LoadInitDataAsync_ReturnsApprovedCount_AndDataRootParentOfArtifactRoot()
    {
        var local = new FakeContentSiteIndexStore();
        local.Rows.Add(Youtube(1, "aaa"));
        local.Rows.Add(Youtube(2, "bbb"));
        local.Rows.Add(Youtube(3, "ccc") with { ApprovalStatus = "pending" });
        var coordinator = Build(local, new FakeContentSiteIndexStore(), artifactRoot: "/data/content-kb");

        var init = await coordinator.LoadInitDataAsync(CancellationToken.None);

        Assert.Equal(2, init.ApprovedCount);
        Assert.Equal(Path.GetDirectoryName("/data/content-kb"), init.DataRoot);
    }

    // ── ComputeDiffAsync (read + classify) ──────────────────────────────────

    [Fact]
    public async Task ComputeDiffAsync_ReadsApprovedLocalAndAllProd_AndClassifies()
    {
        var local = new FakeContentSiteIndexStore();
        local.Rows.Add(Youtube(1, "new1"));
        local.Rows.Add(Youtube(2, "same", title: "Same"));
        local.Rows.Add(Youtube(3, "skip") with { ApprovalStatus = "pending" });
        var prod = new FakeContentSiteIndexStore();
        prod.Rows.Add(Youtube(20, "same", title: "Same"));
        var coordinator = Build(local, prod);

        var diff = await coordinator.ComputeDiffAsync(CancellationToken.None);

        Assert.Equal(1, diff.NewCount);
        Assert.Equal(1, diff.UnchangedCount);
        Assert.Equal(0, prod.EnsureSchemaCallCount); // H3: diff issues no DDL on prod
    }

    // ── WriteContentAsync (content-only batch + awaiting-confirm marker, D-06/D-07) ──────────

    [Fact]
    public async Task WriteContentAsync_HappyPath_UsesContentColumnsOnlyBatch_SetsMarker_NoStampOrVisibility()
    {
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow>
        {
            Youtube(1, "aaa") with { ApprovalStatus = "approved" },
            Youtube(2, "bbb") with { ApprovalStatus = "approved" },
        };
        // Seed prod so the local marker-set pass has rows to match.
        prod.Rows.Add(Youtube(1, "aaa"));
        prod.Rows.Add(Youtube(2, "bbb"));
        var coordinator = Build(local, prod);

        await coordinator.WriteContentAsync(publish, CancellationToken.None);

        // SC3 / D-08: only the content-columns-only BATCH upsert ran on prod — never a full-row upsert.
        Assert.Equal(new[] { "UpsertContentColumnsOnlyBatchAsync" }, prod.UpsertMethodCalls);
        Assert.Single(prod.BatchUpsertCalls);
        // D-03: approval_status mirrored via the content-only upsert (P88 approval mirror preserved
        // by the split — this method still calls UpsertContentColumnsOnlyBatchAsync).
        Assert.All(prod.BatchUpsertCalls[0], r => Assert.Equal("approved", r.ApprovalStatus));
        // D-06/D-07: neither store is stamped or made visible by the content-only write.
        Assert.Empty(prod.StampCalls);
        Assert.Empty(prod.VisibilityKeyCalls);
        Assert.Empty(local.StampCalls);
        Assert.Empty(local.VisibilityKeyCalls);
        // D-10: the local awaiting-confirm marker was set for the pushed keys.
        Assert.Single(local.SetAwaitingConfirmCalls);
        Assert.Equal(2, local.SetAwaitingConfirmCalls[0].Keys.Count);
    }

    [Fact]
    public async Task WriteContentAsync_StampsSeedManagedTrue_OnEveryBatchRow()
    {
        // SYNC-17/D-01: every row DirectPush pushes to prod enters the seed-managed set — the batch
        // upsert must receive rows stamped seed_managed=true, hardcoded regardless of the incoming
        // row's own (possibly null/false) classification (Pitfall 4).
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow>
        {
            Youtube(1, "aaa") with { ApprovalStatus = "approved", SeedManaged = null },
            Youtube(2, "bbb") with { ApprovalStatus = "approved", SeedManaged = false },
        };
        prod.Rows.Add(Youtube(1, "aaa"));
        prod.Rows.Add(Youtube(2, "bbb"));
        var coordinator = Build(local, prod);

        await coordinator.WriteContentAsync(publish, CancellationToken.None);

        var batch = Assert.Single(prod.BatchUpsertCalls);
        Assert.All(batch, r => Assert.True(r.SeedManaged));
    }

    [Fact]
    public async Task WriteContentAsync_BatchRollback_Throws_AndDoesNotSetMarker()
    {
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa"), Youtube(2, "boom") };
        prod.KeysToFailOnUpsert.Add("boom");
        var coordinator = Build(local, prod);

        await Assert.ThrowsAsync<ContentSiteIndexBatchUpsertException>(
            () => coordinator.WriteContentAsync(publish, CancellationToken.None));

        // PUB-01: nothing was stamped/made-visible, and the marker was never set — the whole batch
        // rolled back before the marker-set call is reached.
        Assert.Empty(prod.StampCalls);
        Assert.Empty(prod.VisibilityKeyCalls);
        Assert.Empty(local.StampCalls);
        Assert.Empty(local.VisibilityKeyCalls);
        Assert.Empty(local.SetAwaitingConfirmCalls);
    }

    // ── ConfirmAndPublishAsync (post-confirm stamp/visibility + marker clear, D-06/D-07/D-10) ──

    [Fact]
    public async Task ConfirmAndPublishAsync_StampsAndFlipsVisible_ProdAndLocal_ClearsMarker()
    {
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa"), Youtube(2, "bbb") };
        prod.Rows.Add(Youtube(1, "aaa"));
        prod.Rows.Add(Youtube(2, "bbb"));
        var coordinator = Build(local, prod);

        await coordinator.ConfirmAndPublishAsync(publish, CancellationToken.None);

        // PUB-01/HIGH-3: prod stamped + made visible, then local mirrors the same (order preserved
        // from the pre-split WritePublishAsync across the Task 1 split).
        Assert.Single(prod.StampCalls);
        Assert.Single(prod.VisibilityKeyCalls);
        Assert.Single(local.StampCalls);
        Assert.Single(local.VisibilityKeyCalls);
        // D-10: the local awaiting-confirm marker is cleared once the row is fully published.
        Assert.Single(local.ClearAwaitingConfirmCalls);
        Assert.Equal(2, local.ClearAwaitingConfirmCalls[0].Count);
    }

    [Fact]
    public async Task ConfirmAndPublishAsync_ExcludesSuppressedCreator()
    {
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var blocked = Youtube(1, "blocked") with { Source = "blocked-creator" };
        var control = Youtube(2, "control") with { Source = "allowed-creator" };
        prod.Rows.Add(blocked); prod.Rows.Add(control);
        var suppressed = new FakeCreatorSuppressionStore(); suppressed.Suppressed.Add("blocked-creator");
        var coordinator = Build(local, prod, filter: new CreatorSuppressionRowFilter(suppressed, new FakeCreatorIdentityResolver()));

        await coordinator.ConfirmAndPublishAsync(new[] { blocked, control }, CancellationToken.None);

        var stamped = Assert.Single(prod.StampCalls).Keys;
        Assert.Single(stamped);
        Assert.Equal("control", stamped[0].Value);
    }

    // ── GetAwaitingConfirmRowsAsync (D-10 resume support, Plan 90-06) ────────

    [Fact]
    public async Task GetAwaitingConfirmRowsAsync_ReturnsOnlyApprovedRowsWithMarkerSet()
    {
        var local = new FakeContentSiteIndexStore();
        local.Rows.Add(Youtube(1, "marked") with { AwaitingConfirmUtc = DateTimeOffset.UtcNow, ApprovalStatus = "approved" });
        local.Rows.Add(Youtube(2, "unmarked") with { AwaitingConfirmUtc = null, ApprovalStatus = "approved" });
        local.Rows.Add(Youtube(3, "marked-not-approved") with { AwaitingConfirmUtc = DateTimeOffset.UtcNow, ApprovalStatus = "pending" });
        var coordinator = Build(local, new FakeContentSiteIndexStore());

        var result = await coordinator.GetAwaitingConfirmRowsAsync(CancellationToken.None);

        var row = Assert.Single(result);
        Assert.Equal("marked", row.YoutubeVideoId);
    }

    [Fact]
    public async Task GetAwaitingConfirmRowsAsync_NoMarkedRows_ReturnsEmpty()
    {
        var local = new FakeContentSiteIndexStore();
        local.Rows.Add(Youtube(1, "aaa"));
        var coordinator = Build(local, new FakeContentSiteIndexStore());

        var result = await coordinator.GetAwaitingConfirmRowsAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    // ── UploadArtifactsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task UploadArtifactsAsync_BuildsRequestsFromPublishRows_AndDataRoot()
    {
        var uploader = new FakeSshArtifactUploader();
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), uploader);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var results = await coordinator.UploadArtifactsAsync(
            publish, "/data", progress: null!, CancellationToken.None);

        Assert.Single(results);
        Assert.True(results[0].Success);
        var req = Assert.Single(uploader.UploadedFiles);
        Assert.Equal("content-kb/test-channel/aaa.md", req.RemoteRelativePath);
        Assert.Equal(Path.GetFullPath(Path.Combine("/data", "content-kb/test-channel/aaa.md")), req.LocalPath);
    }


    [Fact]
    public async Task ExportBodiesToPrivateKbRootAsync_SeedExportFailure_ThrowsWithoutGit()
    {
        var orchestrator = new FakeContentKbOrchestrator
        {
            CannedExportResult = new ContentIndexExportResult { Success = false, Message = "disk full" },
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), orchestrator: orchestrator);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ExportBodiesToPrivateKbRootAsync(new[] { Youtube(1, "failure") }, "/data", CancellationToken.None));

        Assert.Contains("disk full", exception.Message, StringComparison.Ordinal);
        Assert.Empty(orchestrator.CopyArtifactsCalls);
    }

    [Fact]
    public async Task ExportBodiesToPrivateKbRootAsync_SuppressedCreator_IsNotCopied()
    {
        var orchestrator = new FakeContentKbOrchestrator();
        var suppressions = new FakeCreatorSuppressionStore();
        suppressions.Suppressed.Add("blocked-creator");
        var filter = new CreatorSuppressionRowFilter(suppressions, new FakeCreatorIdentityResolver());
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), orchestrator: orchestrator, filter: filter);
        var suppressed = Youtube(1, "blocked") with { Source = "blocked-creator" };
        var control = Youtube(2, "control") with { Source = "allowed-creator" };

        var result = await coordinator.ExportBodiesToPrivateKbRootAsync(new[] { suppressed, control }, "/data", CancellationToken.None);

        Assert.Equal(new[] { control.ArtifactPath }, Assert.Single(orchestrator.CopyArtifactsCalls));
    }

    [Fact]
    public async Task ExportBodiesToPrivateKbRootAsync_ContentEqualRows_AreCopiedWithoutGit()
    {
        var orchestrator = new FakeContentKbOrchestrator();
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), orchestrator: orchestrator);

        var result = await coordinator.ExportBodiesToPrivateKbRootAsync(new[] { Youtube(1, "equal") }, "/data", CancellationToken.None);

        Assert.Equal(1, result);
        Assert.Equal(new[] { "content-kb/test-channel/equal.md" }, Assert.Single(orchestrator.CopyArtifactsCalls));
    }

    [Fact]
    public async Task ExportBodiesToPrivateKbRootAsync_Cancellation_Propagates()
    {
        var orchestrator = new FakeContentKbOrchestrator { ThrowOnCopy = new OperationCanceledException() };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), orchestrator: orchestrator);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            coordinator.ExportBodiesToPrivateKbRootAsync(new[] { Youtube(1, "cancelled") }, "/data", CancellationToken.None));
    }


    [Fact]
    public async Task StudioDirectpush_SuppressedCreator_IsExcludedFromDiffAndDirectWrite()
    {
        var local = new FakeContentSiteIndexStore(); var prod = new FakeContentSiteIndexStore();
        var blocked = Youtube(1, "blocked") with { Source = "blocked-creator" };
        var control = Youtube(2, "control") with { Source = "allowed-creator" };
        local.Rows.Add(blocked); local.Rows.Add(control);
        var suppressed = new FakeCreatorSuppressionStore(); suppressed.Suppressed.Add("blocked-creator");
        var coordinator = Build(local, prod, filter: new CreatorSuppressionRowFilter(suppressed, new FakeCreatorIdentityResolver()));

        var diff = await coordinator.ComputeDiffAsync(CancellationToken.None);
        await coordinator.WriteContentAsync([blocked, control], CancellationToken.None);

        Assert.Equal(1, diff.NewCount); Assert.Single(prod.BatchUpsertCalls); Assert.Single(prod.BatchUpsertCalls[0]);
        Assert.Equal("control", prod.BatchUpsertCalls[0][0].YoutubeVideoId);
    }

    [Fact]
    public async Task FailclosedStudioDirectpush_ThrowingStore_RefusesWritesAfterReadableStoreSucceeds()
    {
        var local = new FakeContentSiteIndexStore(); var prod = new FakeContentSiteIndexStore(); var row = Youtube(1, "control");
        await Build(local, prod).WriteContentAsync([row], CancellationToken.None);
        Assert.Single(prod.BatchUpsertCalls); prod.BatchUpsertCalls.Clear();
        var coordinator = Build(local, prod, filter: new CreatorSuppressionRowFilter(new ThrowingCreatorSuppressionStore(), new FakeCreatorIdentityResolver()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.WriteContentAsync([row], CancellationToken.None));
        Assert.Empty(prod.BatchUpsertCalls);
    }

    // Minimal recording logger: captures formatted Warning messages for D-08 skip-log assertions.
    private sealed class RecordingTestLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
