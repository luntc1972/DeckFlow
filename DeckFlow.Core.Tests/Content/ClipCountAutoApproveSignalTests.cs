using DeckFlow.Core.Content;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Behavior tests for <see cref="ClipCountAutoApproveSignal"/> — the clip-count auto-approve
/// heuristic (D-01) with the operator-set cutoff and the &gt;=5 default boundary (D-03).
/// </summary>
public sealed class ClipCountAutoApproveSignalTests
{
    private readonly ClipCountAutoApproveSignal _signal = new();

    [Fact]
    public void ShouldAutoApprove_ClipCountEqualsCutoff_ReturnsTrue()
    {
        // D-03 boundary: >= cutoff, so 5 clips at cutoff 5 approves.
        Assert.True(_signal.ShouldAutoApprove(clipCount: 5, cutoff: 5));
    }

    [Fact]
    public void ShouldAutoApprove_ClipCountBelowCutoff_ReturnsFalse()
    {
        // 3-4 clips hold for review at the default cutoff.
        Assert.False(_signal.ShouldAutoApprove(clipCount: 4, cutoff: 5));
    }

    [Fact]
    public void ShouldAutoApprove_ClipCountAboveCutoff_ReturnsTrue()
    {
        Assert.True(_signal.ShouldAutoApprove(clipCount: 8, cutoff: 5));
    }

    [Fact]
    public void ShouldAutoApprove_CutoffZero_AnyClipCountReturnsTrue()
    {
        // Operator set cutoff 0 → everything auto-approves.
        Assert.True(_signal.ShouldAutoApprove(clipCount: 0, cutoff: 0));
        Assert.True(_signal.ShouldAutoApprove(clipCount: 3, cutoff: 0));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public void ShouldAutoApprove_HighCutoff_RealisticClipCountsReturnFalse(int clipCount)
    {
        // A high operator cutoff (99) holds every realistic 3-8 clip distill.
        Assert.False(_signal.ShouldAutoApprove(clipCount, cutoff: 99));
    }

    [Fact]
    public void DefaultCutoff_IsFive()
    {
        // Single source of truth the Studio settings store (Plan 02) seeds from (D-03).
        Assert.Equal(5, ClipCountAutoApproveSignal.DefaultCutoff);
    }
}
