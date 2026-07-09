using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using Microsoft.Extensions.Configuration;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Fast unit tests for <see cref="ReconcileCoordinator"/> — the SYNC-11 dry-run operator-action
/// coordinator (91-07) plus the SYNC-12 gated destructive Apply (91-08). Dry-run coverage: the
/// coordinator returns the orchestrator's <see cref="ReconcileDryRunResult"/> unchanged (including
/// an unavailable-seed result surfaced intact, T-91-28), the coordinator never writes to the local
/// discrepancy store directly (all persistence is the orchestrator's responsibility per 91-06), the
/// scope tag defaults to <see cref="ReconcileCoordinator.FullScopeTag"/> and is passed through
/// unchanged when supplied, and <see cref="ReconcileCoordinator.GetOpenDiscrepanciesAsync"/> passes
/// straight through to the store. Apply coverage: flag true/false/null gating, the independent
/// seed-unavailable refuse (precedes the stale-check), the seed_managed=true-only soft-hide (the
/// SYNC-17 core invariant — a seed_managed=false row is never hidden even when the discrepancy Kind
/// says seed-drift), stale-removal-set rejection, and a mixed-class dry-run that still applies its
/// seed-drift removals without a false stale-reject.
/// </summary>
public sealed class ReconcileCoordinatorTests
{
    private static readonly DateTimeOffset IndexedAt = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);

    private static ContentKbReconcileDiscrepancy PublishedOrphan(string key)
        => new(
            ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.PublishedOrphan, "youtube_channel", key, null),
            ContentKbReconcileKind.PublishedOrphan,
            "youtube_channel",
            key,
            $"content-kb/test-channel/{key}.md",
            "Title");

    private static ContentKbReconcileDiscrepancy SeedDrift(string key)
        => new(
            ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.SeedDrift, "youtube_channel", key, null),
            ContentKbReconcileKind.SeedDrift,
            "youtube_channel",
            key,
            $"content-kb/test-channel/{key}.md",
            "Title");

    private static ContentKbReconcileDiscrepancy FileOrphan(string path)
        => new(
            ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.FileOrphan, null, null, path),
            ContentKbReconcileKind.FileOrphan,
            null,
            null,
            path,
            null);

    private static ContentKbReconcileDiscrepancy BodyHashMismatch(string key)
        => new(
            ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.BodyHashMismatch, "youtube_channel", key, null),
            ContentKbReconcileKind.BodyHashMismatch,
            "youtube_channel",
            key,
            $"content-kb/test-channel/{key}.md",
            "Title");

    private static StoredReconcileDiscrepancy Stored(string id, string scopeTag)
        => new(
            id,
            ContentKbReconcileKind.FileOrphan,
            null,
            null,
            "content-kb/orphan.md",
            null,
            scopeTag,
            new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero),
            null);

    // Prod row carrying the SYNC-17 seed_managed marker — Apply's own defense-in-depth re-check
    // (T-91-20) reads this straight from a fresh prod store read, independent of the discrepancy's
    // own Kind.
    private static ContentSiteIndexRow ProdRow(string videoId, bool? seedManaged)
        => new()
        {
            Id = videoId.GetHashCode(),
            Source = "test-channel",
            Title = "Title",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            IndexedUtc = IndexedAt,
            ApprovalStatus = "approved",
            IsVisible = true,
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
            SeedManaged = seedManaged,
        };

    private static ReconcileCoordinator Build(
        FakeContentKbReconcileOrchestrator? orchestrator = null,
        FakeContentKbReconcileStore? store = null,
        FakeContentSiteIndexStore? prod = null,
        bool? flagValue = null,
        bool flagIndeterminate = false)
        => new(
            orchestrator ?? new FakeContentKbReconcileOrchestrator(),
            store ?? new FakeContentKbReconcileStore(),
            new FakeProdStoreFactory(prod ?? new FakeContentSiteIndexStore()),
            new FakeReconcileFlagReader { FlagValue = flagValue, FlagIndeterminate = flagIndeterminate },
            new ConfigurationBuilder().Build());

    // ── Dry-run (91-07, unchanged behavior) ────────────────────────────────

    [Fact]
    public async Task RunDryRunAsync_ReturnsOrchestratorResultUnchanged()
    {
        var discrepancies = new[] { PublishedOrphan("aaa"), SeedDrift("bbb") };
        var orchestrator = new FakeContentKbReconcileOrchestrator
        {
            Result = new ReconcileDryRunResult(true, discrepancies),
        };
        var coordinator = Build(orchestrator);

        var result = await coordinator.RunDryRunAsync("full");

        Assert.True(result.SeedAvailable);
        Assert.Equal(discrepancies, result.Discrepancies);
        Assert.Equal(1, orchestrator.CallCount);
        Assert.Equal("full", orchestrator.LastScopeTag);
    }

    [Fact]
    public async Task RunDryRunAsync_SeedUnavailable_IsSurfacedIntact()
    {
        // Why: T-91-28 / Codex BLOCK closure — an unavailable seed must be surfaced AS UNAVAILABLE,
        // never silently dropped or collapsed into an empty-but-available result.
        var discrepancies = new[] { PublishedOrphan("aaa") };
        var orchestrator = new FakeContentKbReconcileOrchestrator
        {
            Result = new ReconcileDryRunResult(false, discrepancies),
        };
        var coordinator = Build(orchestrator);

        var result = await coordinator.RunDryRunAsync();

        Assert.False(result.SeedAvailable);
        Assert.Equal(discrepancies, result.Discrepancies);
    }

    [Fact]
    public async Task RunDryRunAsync_NoScopeTagSupplied_DefaultsToFull()
    {
        var orchestrator = new FakeContentKbReconcileOrchestrator();
        var coordinator = Build(orchestrator);

        await coordinator.RunDryRunAsync();

        Assert.Equal(ReconcileCoordinator.FullScopeTag, orchestrator.LastScopeTag);
        Assert.Equal("full", orchestrator.LastScopeTag);
    }

    [Fact]
    public async Task RunDryRunAsync_NeverWritesToTheLocalStoreDirectly()
    {
        // Why: T-91-17 — the dry-run must perform no destructive write. All persistence is the
        // orchestrator's job (91-06); the coordinator itself must never call PersistRunAsync (or
        // EnsureSchemaAsync) on the store — it only reads via GetOpenDiscrepanciesAsync.
        var orchestrator = new FakeContentKbReconcileOrchestrator();
        var store = new FakeContentKbReconcileStore();
        var coordinator = Build(orchestrator, store);

        await coordinator.RunDryRunAsync();

        Assert.Equal(0, store.PersistRunCallCount);
        Assert.Equal(0, store.EnsureSchemaCallCount);
        Assert.Equal(0, store.GetOpenCallCount);
    }

    [Fact]
    public async Task GetOpenDiscrepanciesAsync_PassesThroughToStore()
    {
        var store = new FakeContentKbReconcileStore();
        store.OpenDiscrepancies.Add(Stored("id-1", "full"));
        store.OpenDiscrepancies.Add(Stored("id-2", "other-scope"));
        var coordinator = Build(store: store);

        var results = await coordinator.GetOpenDiscrepanciesAsync("full");

        Assert.Single(results);
        Assert.Equal("id-1", results[0].Id);
        Assert.Equal(1, store.GetOpenCallCount);
        Assert.Equal("full", store.LastGetOpenScopeTag);
    }

    [Fact]
    public void Constructor_NullOrchestrator_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ReconcileCoordinator(
            null!,
            new FakeContentKbReconcileStore(),
            new FakeProdStoreFactory(new FakeContentSiteIndexStore()),
            new FakeReconcileFlagReader(),
            new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void Constructor_NullStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ReconcileCoordinator(
            new FakeContentKbReconcileOrchestrator(),
            null!,
            new FakeProdStoreFactory(new FakeContentSiteIndexStore()),
            new FakeReconcileFlagReader(),
            new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void Constructor_NullProdStoreFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ReconcileCoordinator(
            new FakeContentKbReconcileOrchestrator(),
            new FakeContentKbReconcileStore(),
            null!,
            new FakeReconcileFlagReader(),
            new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void Constructor_NullProdReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ReconcileCoordinator(
            new FakeContentKbReconcileOrchestrator(),
            new FakeContentKbReconcileStore(),
            new FakeProdStoreFactory(new FakeContentSiteIndexStore()),
            null!,
            new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void Constructor_NullConfiguration_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ReconcileCoordinator(
            new FakeContentKbReconcileOrchestrator(),
            new FakeContentKbReconcileStore(),
            new FakeProdStoreFactory(new FakeContentSiteIndexStore()),
            new FakeReconcileFlagReader(),
            null!));
    }

    // ── ApplyRemovalsAsync (91-08) ──────────────────────────────────────────

    [Fact]
    public async Task ApplyRemovalsAsync_FlagTrue_HidesStillPresentSeedManagedDrift()
    {
        var drift = SeedDrift("aaa");
        var orchestrator = new FakeContentKbReconcileOrchestrator
        {
            Result = new ReconcileDryRunResult(true, new[] { drift }),
        };
        var prod = new FakeContentSiteIndexStore();
        prod.Rows.Add(ProdRow("aaa", seedManaged: true));
        var coordinator = Build(orchestrator, prod: prod, flagValue: true);

        var result = await coordinator.ApplyRemovalsAsync(new HashSet<string> { drift.Id });

        Assert.True(result.WasApplied);
        Assert.Equal(1, result.HiddenCount);
        // The destructive hide MUST route through the ownership-scoped HideSeedManagedAsync, never the
        // ownership-agnostic SetVisibilityAsync (Codex 91-08 HIGH — closes the TOCTOU).
        Assert.Empty(prod.VisibilityKeyCalls);
        Assert.Single(prod.HideSeedManagedKeyCalls);
        var keys = prod.HideSeedManagedKeyCalls[0];
        Assert.Contains(keys, k => k.Type == "youtube_channel" && k.Value == "aaa");
    }

    [Fact]
    public async Task ApplyRemovalsAsync_FlagFalse_Refuses_NoVisibilityWrite()
    {
        var drift = SeedDrift("aaa");
        var orchestrator = new FakeContentKbReconcileOrchestrator
        {
            Result = new ReconcileDryRunResult(true, new[] { drift }),
        };
        var prod = new FakeContentSiteIndexStore();
        prod.Rows.Add(ProdRow("aaa", seedManaged: true));
        var coordinator = Build(orchestrator, prod: prod, flagValue: false);

        var result = await coordinator.ApplyRemovalsAsync(new HashSet<string> { drift.Id });

        Assert.False(result.WasApplied);
        Assert.Equal(ReconcileApplyRefusalReason.FlagNotEnabled, result.RefusalReason);
        Assert.Null(result.HiddenCount);
        Assert.Empty(prod.HideSeedManagedKeyCalls);
        // Why: a false/null flag must refuse BEFORE the fresh re-run even runs — no dry-run call.
        Assert.Equal(0, orchestrator.CallCount);
    }

    [Fact]
    public async Task ApplyRemovalsAsync_FlagIndeterminate_Refuses_NoVisibilityWrite()
    {
        // Why: fail-safe-to-REFUSE — an indeterminate (null) tri-state read must refuse exactly like
        // a definitive false, never proceed as if it were confirmed true.
        var drift = SeedDrift("aaa");
        var orchestrator = new FakeContentKbReconcileOrchestrator
        {
            Result = new ReconcileDryRunResult(true, new[] { drift }),
        };
        var prod = new FakeContentSiteIndexStore();
        prod.Rows.Add(ProdRow("aaa", seedManaged: true));
        var coordinator = Build(orchestrator, prod: prod, flagIndeterminate: true);

        var result = await coordinator.ApplyRemovalsAsync(new HashSet<string> { drift.Id });

        Assert.False(result.WasApplied);
        Assert.Equal(ReconcileApplyRefusalReason.FlagNotEnabled, result.RefusalReason);
        Assert.Empty(prod.HideSeedManagedKeyCalls);
        Assert.Equal(0, orchestrator.CallCount);
    }

    [Fact]
    public async Task ApplyRemovalsAsync_SeedUnavailable_RefusesBeforeStaleCheck_EvenWithStaleReviewedSet()
    {
        // Why: Codex BLOCK / T-91-27 — the seed-unavailable refuse is independent of the discrepancy
        // list and must precede the stale-check. A non-empty (and here deliberately STALE — it does
        // not match the fresh, empty result) reviewed set must still produce zero hides, proving the
        // refuse order (unavailable-seed gate before stale-check).
        var orchestrator = new FakeContentKbReconcileOrchestrator
        {
            Result = new ReconcileDryRunResult(false, Array.Empty<ContentKbReconcileDiscrepancy>()),
        };
        var prod = new FakeContentSiteIndexStore();
        prod.Rows.Add(ProdRow("aaa", seedManaged: true));
        var coordinator = Build(orchestrator, prod: prod, flagValue: true);

        var staleReviewedSet = new HashSet<string> { SeedDrift("aaa").Id };
        var result = await coordinator.ApplyRemovalsAsync(staleReviewedSet);

        Assert.False(result.WasApplied);
        Assert.Equal(ReconcileApplyRefusalReason.SeedUnavailable, result.RefusalReason);
        Assert.Empty(prod.HideSeedManagedKeyCalls);
    }

    [Fact]
    public async Task ApplyRemovalsAsync_SeedManagedFalse_NeverHidden()
    {
        // Why: the SYNC-17 core invariant (T-91-20). Even though the discrepancy Kind claims
        // seed-drift (simulating a hypothetical future classifier regression that emitted one for a
        // prod-owned row), Apply's own fresh prod re-check must refuse to hide it because the
        // CURRENT prod row is seed_managed=false.
        var drift = SeedDrift("prod-owned");
        var orchestrator = new FakeContentKbReconcileOrchestrator
        {
            Result = new ReconcileDryRunResult(true, new[] { drift }),
        };
        var prod = new FakeContentSiteIndexStore();
        prod.Rows.Add(ProdRow("prod-owned", seedManaged: false));
        var coordinator = Build(orchestrator, prod: prod, flagValue: true);

        var result = await coordinator.ApplyRemovalsAsync(new HashSet<string> { drift.Id });

        Assert.True(result.WasApplied);
        Assert.Equal(0, result.HiddenCount);
        // The in-memory pre-filter drops the prod-owned key before any write, so the destructive
        // HideSeedManagedAsync path is never even reached (and its SQL predicate would refuse anyway).
        Assert.Empty(prod.HideSeedManagedKeyCalls);
    }

    [Fact]
    public async Task ApplyRemovalsAsync_StaleReviewedSet_RejectedWithNoWrite()
    {
        var freshDrift = SeedDrift("bbb");
        var orchestrator = new FakeContentKbReconcileOrchestrator
        {
            Result = new ReconcileDryRunResult(true, new[] { freshDrift }),
        };
        var prod = new FakeContentSiteIndexStore();
        prod.Rows.Add(ProdRow("bbb", seedManaged: true));
        var coordinator = Build(orchestrator, prod: prod, flagValue: true);

        // Reviewed set names a DIFFERENT (now-resolved) discrepancy id than what the fresh run found.
        var staleReviewedSet = new HashSet<string> { SeedDrift("aaa").Id };
        var result = await coordinator.ApplyRemovalsAsync(staleReviewedSet);

        Assert.False(result.WasApplied);
        Assert.Equal(ReconcileApplyRefusalReason.StaleReviewSet, result.RefusalReason);
        Assert.Empty(prod.HideSeedManagedKeyCalls);
    }

    [Fact]
    public async Task ApplyRemovalsAsync_MixedClassDryRun_DoesNotFalseReject_AppliesSeedDriftOnly()
    {
        // Why (T-91-24): a dry-run surfacing all four classes must not false-reject the seed-drift
        // Apply as stale — only seed-drift IDs enter the comparison on both sides.
        var drift = SeedDrift("ccc");
        var mixed = new ContentKbReconcileDiscrepancy[]
        {
            PublishedOrphan("ddd"),
            FileOrphan("content-kb/orphan.md"),
            drift,
            BodyHashMismatch("eee"),
        };
        var orchestrator = new FakeContentKbReconcileOrchestrator
        {
            Result = new ReconcileDryRunResult(true, mixed),
        };
        var prod = new FakeContentSiteIndexStore();
        prod.Rows.Add(ProdRow("ccc", seedManaged: true));
        var coordinator = Build(orchestrator, prod: prod, flagValue: true);

        var result = await coordinator.ApplyRemovalsAsync(new HashSet<string> { drift.Id });

        Assert.True(result.WasApplied);
        Assert.Equal(1, result.HiddenCount);
        Assert.Single(prod.HideSeedManagedKeyCalls);
        var keys = prod.HideSeedManagedKeyCalls[0];
        Assert.Contains(keys, k => k.Type == "youtube_channel" && k.Value == "ccc");
    }

    [Fact]
    public async Task ApplyRemovalsAsync_NoTimestampColumnWritten()
    {
        // Why (D-03/Pitfall 5/F-51-PG-01): the soft-hide uses HideSeedManagedAsync exclusively and
        // must never call StampPushedToProdAsync or any other timestamp-writing method.
        var drift = SeedDrift("fff");
        var orchestrator = new FakeContentKbReconcileOrchestrator
        {
            Result = new ReconcileDryRunResult(true, new[] { drift }),
        };
        var prod = new FakeContentSiteIndexStore();
        prod.Rows.Add(ProdRow("fff", seedManaged: true));
        var coordinator = Build(orchestrator, prod: prod, flagValue: true);

        await coordinator.ApplyRemovalsAsync(new HashSet<string> { drift.Id });

        Assert.Empty(prod.StampCalls);
    }

    [Fact]
    public async Task ApplyRemovalsAsync_EmptyReviewedSet_MatchingEmptyFreshResult_AppliesZero()
    {
        var orchestrator = new FakeContentKbReconcileOrchestrator
        {
            Result = new ReconcileDryRunResult(true, Array.Empty<ContentKbReconcileDiscrepancy>()),
        };
        var coordinator = Build(orchestrator, flagValue: true);

        var result = await coordinator.ApplyRemovalsAsync(new HashSet<string>());

        Assert.True(result.WasApplied);
        Assert.Equal(0, result.HiddenCount);
    }
}
