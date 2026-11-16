using DeckFlow.Web.Services.Harvest;
namespace DeckFlow.Web.Tests;

internal sealed class FakeHarvestMaintenanceRunStore : IHarvestMaintenanceRunStore
{
    private readonly object _lock = new();
    public List<HarvestMaintenanceStatus> Inserted { get; } = [];
    public List<HarvestMaintenanceStatus> Saved { get; } = [];
    public HarvestMaintenanceStatus? Latest { get; set; }
    public bool ThrowOnInsert { get; set; }
    public bool ThrowOnSave { get; set; }
    public bool ThrowOnGetLatest { get; set; }
    public TaskCompletionSource ProgressSaved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task? InsertGate { get; set; }
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public async Task InsertAsync(HarvestMaintenanceStatus status, CancellationToken cancellationToken = default)
    {
        if (InsertGate is not null) await InsertGate.WaitAsync(cancellationToken);
        if (ThrowOnInsert) throw new InvalidOperationException("insert failed");
        lock (_lock) { Inserted.Add(status); Latest = status; }
    }
    public Task SaveAsync(HarvestMaintenanceStatus status, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSave) throw new InvalidOperationException("save failed");
        lock (_lock) { Saved.Add(status); Latest = status; }
        if (status.State == HarvestMaintenanceRunState.Running && status.DecksAffected == 7) ProgressSaved.TrySetResult();
        return Task.CompletedTask;
    }
    public Task<HarvestMaintenanceStatus?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnGetLatest) throw new InvalidOperationException("read failed");
        return Task.FromResult(Latest);
    }
}
