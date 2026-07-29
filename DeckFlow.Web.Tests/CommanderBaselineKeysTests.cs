using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Verifies the shared commander-baseline key generation semantics.
/// </summary>
public sealed class CommanderBaselineKeysTests
{
    [Fact]
    public void Candidates_SingleName_YieldsThatNameOnly()
    {
        string[] candidates = CommanderBaselineKeys.Candidates(["The Ur-Dragon"]).ToArray();

        Assert.Equal(["The Ur-Dragon"], candidates);
    }

    [Fact]
    public void Candidates_TwoNames_YieldsBothPartnerOrders()
    {
        string[] candidates = CommanderBaselineKeys.Candidates(["A", "B"]).ToArray();

        Assert.Equal(["A / B", "B / A"], candidates);
    }

    [Fact]
    public void Candidates_DoubleFacedName_IsNeverSplit()
    {
        string[] candidates = CommanderBaselineKeys.Candidates(["Ojer Axonil // Temple of Power"]).ToArray();

        Assert.Equal(["Ojer Axonil // Temple of Power"], candidates);
    }

    [Fact]
    public void Candidates_EmptyList_YieldsNothing()
    {
        Assert.Empty(CommanderBaselineKeys.Candidates([]));
    }

    [Fact]
    public void Candidates_ThreeNames_YieldsNothing()
    {
        Assert.Empty(CommanderBaselineKeys.Candidates(["A", "B", "C"]));
    }
}
