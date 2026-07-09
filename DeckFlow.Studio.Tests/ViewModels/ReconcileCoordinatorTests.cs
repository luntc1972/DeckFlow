using DeckFlow.Core.Content;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Fast unit tests for <see cref="ReconcileCoordinator"/> — the SYNC-11 dry-run operator-action
/// coordinator (91-07). Covers: the coordinator returns the orchestrator's
/// <see cref="ReconcileDryRunResult"/> unchanged (including an unavailable-seed result surfaced
/// intact, T-91-28), the coordinator never writes to the local discrepancy store directly (all
/// persistence is the orchestrator's responsibility per 91-06), the scope tag defaults to
/// <see cref="ReconcileCoordinator.FullScopeTag"/> and is passed through unchanged when supplied,
/// and <see cref="ReconcileCoordinator.GetOpenDiscrepanciesAsync"/> passes straight through to the
/// store.
/// </summary>
public sealed class ReconcileCoordinatorTests
{
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

    [Fact]
    public async Task RunDryRunAsync_ReturnsOrchestratorResultUnchanged()
    {
        var discrepancies = new[] { PublishedOrphan("aaa"), SeedDrift("bbb") };
        var orchestrator = new FakeContentKbReconcileOrchestrator
        {
            Result = new ReconcileDryRunResult(true, discrepancies),
        };
        var store = new FakeContentKbReconcileStore();
        var coordinator = new ReconcileCoordinator(orchestrator, store);

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
        var store = new FakeContentKbReconcileStore();
        var coordinator = new ReconcileCoordinator(orchestrator, store);

        var result = await coordinator.RunDryRunAsync();

        Assert.False(result.SeedAvailable);
        Assert.Equal(discrepancies, result.Discrepancies);
    }

    [Fact]
    public async Task RunDryRunAsync_NoScopeTagSupplied_DefaultsToFull()
    {
        var orchestrator = new FakeContentKbReconcileOrchestrator();
        var store = new FakeContentKbReconcileStore();
        var coordinator = new ReconcileCoordinator(orchestrator, store);

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
        var coordinator = new ReconcileCoordinator(orchestrator, store);

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
        var orchestrator = new FakeContentKbReconcileOrchestrator();
        var coordinator = new ReconcileCoordinator(orchestrator, store);

        var results = await coordinator.GetOpenDiscrepanciesAsync("full");

        Assert.Single(results);
        Assert.Equal("id-1", results[0].Id);
        Assert.Equal(1, store.GetOpenCallCount);
        Assert.Equal("full", store.LastGetOpenScopeTag);
    }

    [Fact]
    public void Constructor_NullOrchestrator_Throws()
    {
        var store = new FakeContentKbReconcileStore();
        Assert.Throws<ArgumentNullException>(() => new ReconcileCoordinator(null!, store));
    }

    [Fact]
    public void Constructor_NullStore_Throws()
    {
        var orchestrator = new FakeContentKbReconcileOrchestrator();
        Assert.Throws<ArgumentNullException>(() => new ReconcileCoordinator(orchestrator, null!));
    }
}
