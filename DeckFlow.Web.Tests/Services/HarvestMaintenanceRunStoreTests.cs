using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace DeckFlow.Web.Tests;

public sealed class HarvestMaintenanceRunStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"maintenance-{Guid.NewGuid():N}.db");
    internal static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);
    private HarvestMaintenanceRunStore Store() => new(_path, NullLogger<HarvestMaintenanceRunStore>.Instance);
    internal static HarvestMaintenanceStatus Row(HarvestMaintenanceRunState state = HarvestMaintenanceRunState.Starting) => new()
    {
        RunId = Guid.NewGuid(),
        Operation = HarvestMaintenanceOperation.Drain,
        State = state,
        StartedUtc = Now,
        LastProgressUtc = Now.AddMinutes(1),
        EndedUtc = Now.AddMinutes(2),
        DecksAffected = 7,
        Batches = 2,
        StopRequested = true,
        StopReason = DeckPruningStopReason.Completed,
        RefusalReason = "purge_pending",
        PreviewAtStart = new(3, 4, 5),
        PreviewAsOfUtc = Now
    };
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(_path)) File.Delete(_path); }
    [Fact]
    public async Task EnsureSchemaAsync_FreshDatabase_CreatesTableAndIsIdempotent()
    {
        var s = Store(); await s.EnsureSchemaAsync(); await s.EnsureSchemaAsync(); Assert.Null(await s.GetLatestAsync());
    }
    [Fact]
    public async Task InsertAsync_ThenGetLatestAsync_RoundTripsEveryColumn()
    {
        var s = Store(); var row = Row(); await s.InsertAsync(row); Assert.Equal(row, await s.GetLatestAsync());
    }
    [Fact]
    public async Task GetLatestAsync_EmptyTable_ReturnsNull()
    {
        Assert.Null(await Store().GetLatestAsync());
    }
    [Fact]
    public async Task GetLatestAsync_TwoRuns_ReturnsNewestStarted()
    {
        var s = Store(); var newer = Row() with { StartedUtc = Now.AddHours(1) }; await s.InsertAsync(newer); await s.InsertAsync(Row()); Assert.Equal(newer, await s.GetLatestAsync());
    }
    [Fact]
    public async Task EnsureSchemaAsync_RunningRowFromPriorBoot_BecomesInterrupted()
    {
        var s = Store(); await s.InsertAsync(Row()); await s.InsertAsync(Row(HarvestMaintenanceRunState.Running) with { StartedUtc = Now.AddHours(1) }); var next = new HarvestMaintenanceRunStore(_path, NullLogger<HarvestMaintenanceRunStore>.Instance, () => Now); await next.EnsureSchemaAsync(); var row = await next.GetLatestAsync(); Assert.Equal(HarvestMaintenanceRunState.Interrupted, row!.State); Assert.Equal(Now, row.EndedUtc);
    }
    [Fact]
    public async Task EnsureSchemaAsync_TerminalRows_AreNotReaped()
    {
        var s = Store(); var row = Row(HarvestMaintenanceRunState.Finished); await s.InsertAsync(row); Assert.Equal(row, await Store().GetLatestAsync());
    }
    [Fact]
    public async Task SaveAsync_RunningRow_WritesAllColumns()
    {
        var s = Store(); var row = Row(); await s.InsertAsync(row); var changed = row with { State = HarvestMaintenanceRunState.Running, DecksAffected = 99, Batches = 9, StopRequested = false, PreviewAtStart = new(8, 9, 10) }; await s.SaveAsync(changed); Assert.Equal(changed, await s.GetLatestAsync());
    }
    [Fact]
    public async Task SaveAsync_InterruptedRow_TakesTerminalStateAndProgress()
    {
        var s = Store(); var row = Row(HarvestMaintenanceRunState.Interrupted); await s.InsertAsync(row); var changed = row with { State = HarvestMaintenanceRunState.Finished, DecksAffected = 99 }; await s.SaveAsync(changed); Assert.Equal(changed, await s.GetLatestAsync());
    }
    [Fact]
    public async Task SaveAsync_InterruptedRow_RunningWriteKeepsInterrupted()
    {
        var s = Store(); var row = Row(HarvestMaintenanceRunState.Interrupted); await s.InsertAsync(row); await s.SaveAsync(row with { State = HarvestMaintenanceRunState.Running, EndedUtc = null, DecksAffected = 99 }); Assert.Equal(row with { DecksAffected = 99 }, await s.GetLatestAsync());
    }
    [Fact]
    public async Task SaveAsync_FinalRow_IgnoresLaterWrites()
    {
        var s = Store(); var row = Row(HarvestMaintenanceRunState.Finished); await s.InsertAsync(row); await s.SaveAsync(row with { State = HarvestMaintenanceRunState.Running, DecksAffected = 99 }); Assert.Equal(row, await s.GetLatestAsync()); await s.SaveAsync(row with { State = HarvestMaintenanceRunState.Failed }); Assert.Equal(row, await s.GetLatestAsync());
    }
}
