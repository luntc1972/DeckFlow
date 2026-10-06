using DeckFlow.Web.Services.Harvest;
using Xunit;

namespace DeckFlow.Web.Tests.Services;

/// <summary>Cache facts for D-06 and the HARV-11 idempotency edge.</summary>
public sealed class HarvestUpdateScheduleCacheTests
{
    [Fact] public void Snapshot_BeforeFirstLoad_IsOffAndUnpaused() { var cache = new HarvestUpdateScheduleCache(new FakeHarvestUpdateScheduleStore()); Assert.Null(cache.Snapshot().IntervalMinutes); Assert.False(cache.Snapshot().Paused); }
    [Fact] public async Task ReloadAsync_PicksUpStoreValues() { var store = new FakeHarvestUpdateScheduleStore { Snapshot = new(15, true, Now) }; var cache = new HarvestUpdateScheduleCache(store); await cache.ReloadAsync(); Assert.Equal(store.Snapshot, cache.Snapshot()); }
    [Fact] public async Task ReloadAsync_StoreThrows_PreservesLastGoodSnapshot() { var store = new FakeHarvestUpdateScheduleStore { Snapshot = new(15, false, Now) }; var cache = new HarvestUpdateScheduleCache(store); await cache.ReloadAsync(); store.GetException = new InvalidOperationException(); await cache.ReloadAsync(); Assert.Equal(store.Snapshot, cache.Snapshot()); }
    [Fact] public async Task ReloadAsync_FirstLoadThrows_StaysOff() { var cache = new HarvestUpdateScheduleCache(new FakeHarvestUpdateScheduleStore { GetException = new InvalidOperationException() }); await cache.ReloadAsync(); Assert.Null(cache.Snapshot().IntervalMinutes); Assert.False(cache.Snapshot().Paused); }
    [Fact] public async Task ReloadAsync_CancelledToken_RethrowsOperationCanceled() { var store = new FakeHarvestUpdateScheduleStore { GetException = new OperationCanceledException() }; var cache = new HarvestUpdateScheduleCache(store); using var source = new CancellationTokenSource(); source.Cancel(); await Assert.ThrowsAsync<OperationCanceledException>(() => cache.ReloadAsync(source.Token)); }
    [Fact] public async Task StartAsync_LoadsBeforeReturning() { var store = new FakeHarvestUpdateScheduleStore { Snapshot = new(30, true, Now) }; var cache = new HarvestUpdateScheduleCache(store); using var source = new CancellationTokenSource(); await cache.StartAsync(source.Token); Assert.Equal(store.Snapshot, cache.Snapshot()); await cache.StopAsync(CancellationToken.None); }
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private sealed class FakeHarvestUpdateScheduleStore : IHarvestUpdateScheduleStore
    {
        public HarvestUpdateScheduleSnapshot Snapshot { get; set; } = new(null, false, DateTimeOffset.MinValue);
        public Exception? GetException { get; set; }
        public int GetCallCount { get; private set; }
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<HarvestUpdateScheduleSnapshot> GetAsync(CancellationToken cancellationToken = default) { GetCallCount++; return GetException is null ? Task.FromResult(Snapshot) : Task.FromException<HarvestUpdateScheduleSnapshot>(GetException); }
        public Task SaveAsync(int? intervalMinutes, bool paused, DateTimeOffset now, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
