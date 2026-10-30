using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabRoleEditServiceTests
{
    internal static CutLabState State() => new()
    {
        Commander = "Commander",
        Pool = [new() { Name = "Commander", IsCommander = true }, new() { Name = "Arcane Signet" }],
        BaselineSnapshot = new(),
    };

    [Fact]
    public async Task ApplyAsync_ChangedEdit_RebuildsBaselineOnceAtInLoopTrials()
    {
        var fake = new CountingSimulationService();
        var state = State();
        var result = await new CutLabRoleEditService(fake, NullLogger<CutLabRoleEditService>.Instance)
            .ApplyAsync(state, "Arcane Signet", "draw", CutLabRoleOverrideOperation.Add);
        Assert.True(result.Changed);
        Assert.Null(result.Error);
        var call = Assert.Single(fake.Calls);
        Assert.Same(state.Pool, call.Pool);
        Assert.Equal(ICutLabSimulationService.InLoopTrials, call.Trials);
        Assert.Null(call.Key);
        Assert.Same(fake.Result.Snapshot, result.State.BaselineSnapshot);
        Assert.Equal(fake.Result.ActualLands, result.State.BaselineActualLands);
        Assert.Equal(fake.Result.TargetLands, result.State.BaselineTargetLands);
        Assert.Equal(["draw"], Assert.Single(result.State.RoleOverrides).Added);
    }

    [Fact]
    public async Task ApplyAsync_RefusedEdit_ReturnsErrorWithoutSimulating()
    {
        var fake = new CountingSimulationService();
        var state = State();
        var result = await new CutLabRoleEditService(fake, NullLogger<CutLabRoleEditService>.Instance)
            .ApplyAsync(state, "Commander", "draw", CutLabRoleOverrideOperation.Add);
        Assert.Equal(CutLabMessages.RoleOverrideCommanderMessage, result.Error);
        Assert.False(result.Changed);
        Assert.Same(state, result.State);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task ApplyAsync_NoOpEdit_SkipsSimulationAndKeepsBaseline()
    {
        var fake = new CountingSimulationService();
        var state = State();
        var result = await new CutLabRoleEditService(fake, NullLogger<CutLabRoleEditService>.Instance)
            .ApplyAsync(state, "Arcane Signet", null, CutLabRoleOverrideOperation.Reset);
        Assert.False(result.Changed);
        Assert.Null(result.Error);
        Assert.Same(state, result.State);
        Assert.NotNull(result.State.BaselineSnapshot);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task ApplyAsync_BaselineBuildThrows_ReturnsChangedStateWithoutBaseline()
    {
        var logger = new CapturingLogger<CutLabRoleEditService>();
        var result = await new CutLabRoleEditService(new CountingSimulationService { Failure = new InvalidOperationException() }, logger)
            .ApplyAsync(State(), "Arcane Signet", "draw", CutLabRoleOverrideOperation.Add);
        Assert.True(result.Changed);
        Assert.Null(result.Error);
        Assert.Null(result.State.BaselineSnapshot);
        Assert.Equal(["draw"], Assert.Single(result.State.RoleOverrides).Added);
        Assert.Contains(Assert.Single(logger.Entries), item => item.Key == "{OriginalFormat}" && Equals(item.Value,
            "Cut Lab role edit baseline rebuild failed after {ElapsedMs} ms; saving the edit without a baseline"));
    }

    [Fact]
    public async Task ApplyAsync_Cancelled_PropagatesOperationCanceledException()
    {
        var service = new CutLabRoleEditService(new CountingSimulationService { Failure = new OperationCanceledException() }, NullLogger<CutLabRoleEditService>.Instance);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ApplyAsync(State(), "Arcane Signet", "draw", CutLabRoleOverrideOperation.Add));
    }

    internal sealed class CountingSimulationService : ICutLabSimulationService
    {
        internal List<(IReadOnlyList<CutLabPoolCard> Pool, int? Trials, string? Key)> Calls { get; } = [];
        internal Exception? Failure { get; init; }
        internal CutLabSimulationResult Result { get; } = new()
        {
            Snapshot = new() { Metrics = [new() { Kind = CutLabMetricKind.KeepableHand, Value = 75 }] },
            ActualLands = 30,
            TargetLands = 29,
        };

        public Task<CutLabSimulationResult> BuildSnapshotResult(IReadOnlyList<CutLabPoolCard> workingList, string? playExperience, int? trialsOverride = 4000, string? poolKey = null, CutLabGoalSettings? goals = null, IReadOnlyList<CutLabRoleOverride>? roleOverrides = null, CancellationToken cancellationToken = default)
        {
            Calls.Add((workingList, trialsOverride, poolKey));
            if (Failure is not null) throw Failure;
            return Task.FromResult(Result);
        }

        public Task<CutLabMetricSnapshot> BuildSnapshot(IReadOnlyList<CutLabPoolCard> workingList, string? playExperience, int? trialsOverride = 4000, string? poolKey = null, CutLabGoalSettings? goals = null, IReadOnlyList<CutLabRoleOverride>? roleOverrides = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CutLabProposalDeltas> ComputeProposalDeltas(IReadOnlyList<CutLabPoolCard> currentWorkingList, string candidateCardName, string? playExperience, int? trialsOverride = 4000, string? poolKey = null, CutLabGoalSettings? goals = null, IReadOnlyList<CutLabRoleOverride>? roleOverrides = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
