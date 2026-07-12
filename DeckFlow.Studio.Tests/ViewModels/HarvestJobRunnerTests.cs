using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Studio.ViewModels;
using Xunit;

namespace DeckFlow.Studio.Tests.ViewModels;

public sealed class HarvestJobRunnerTests
{
    [Fact]
    public async Task RunAsync_SetsIsRunning_DuringWork_ClearsAfter()
    {
        var runner = new HarvestJobRunner();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = runner.RunAsync(
            HarvestJobKind.Harvest,
            async ct =>
            {
                started.TrySetResult(true);
                await release.Task.WaitAsync(ct);
                return 42;
            });

        await started.Task;
        Assert.True(runner.IsRunning);
        Assert.Equal(HarvestJobKind.Harvest, runner.CurrentKind);

        release.TrySetResult(true);

        Assert.Equal(42, await task);
        Assert.False(runner.IsRunning);
        Assert.Null(runner.CurrentKind);
    }

    [Fact]
    public async Task RunAsync_SecondConcurrentStart_Throws()
    {
        var runner = new HarvestJobRunner();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = runner.RunAsync(
            HarvestJobKind.Harvest,
            async ct =>
            {
                started.TrySetResult(true);
                await release.Task.WaitAsync(ct);
                return 1;
            });

        await started.Task;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(
                HarvestJobKind.LiveDistill,
                _ => Task.FromResult(2)));

        release.TrySetResult(true);
        await first;
    }

    [Fact]
    public async Task Cancel_CancelsTheRunningWorkToken()
    {
        var runner = new HarvestJobRunner();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = runner.RunAsync(
            HarvestJobKind.HarvestAndAutoDistill,
            async ct =>
            {
                started.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return 0;
            });

        await started.Task;
        runner.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.False(runner.IsRunning);
        Assert.Null(runner.CurrentKind);
    }

    [Fact]
    public async Task RunAsync_CompletesEvenIfCallerStopsAwaiting()
    {
        var runner = new HarvestJobRunner();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = runner.RunAsync(
            HarvestJobKind.LiveDistill,
            async ct =>
            {
                started.TrySetResult(true);
                await release.Task.WaitAsync(ct);
                completed.TrySetResult(7);
                return 7;
            });

        await started.Task;
        Assert.True(runner.IsRunning);

        release.TrySetResult(true);

        Assert.Equal(7, await completed.Task);

        var attempts = 0;
        while (runner.IsRunning && attempts++ < 50)
        {
            await Task.Delay(10);
        }

        Assert.False(runner.IsRunning);
        Assert.Null(runner.CurrentKind);
    }

    [Fact]
    public void AppendLog_RaisesChanged_AndAccumulates()
    {
        var runner = new HarvestJobRunner();
        var changedCount = 0;
        runner.Changed += _ => changedCount++;

        runner.AppendLog("line 1");
        runner.AppendLog("line 2");

        Assert.Equal(2, changedCount);
        Assert.Equal(new[] { "line 1", "line 2" }, runner.Log);
    }
}
