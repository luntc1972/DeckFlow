using DeckFlow.Core.Analysis;
using DeckFlow.Web.Services.CutLab;

using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Checks the conditional, additive enabler role pass.</summary>
public sealed class CutLabEnablersPassTests
{
    [Theory]
    [InlineData(true, "You gain 1 life.", "wincons", "wincons,enablers")]
    [InlineData(true, "You gain 1 life.", "other", "enablers")]
    [InlineData(true, "Draw a card.", "draw", "draw")]
    [InlineData(false, "You gain 1 life.", "other", "other")]
    [InlineData(true, "{T}: You gain 1 life.", "lands", "lands,enablers")]
    [InlineData(true, null, "other", "other")]
    [InlineData(true, "You gain 1 life.", "enablers", "enablers")]
    public void Apply_CardAndFamilyInputs_ReturnsExpectedRoles(bool active, string? text, string role, string expected)
    {
        IReadOnlyList<string> families = active ? [TriggerFamilyCatalog.Lifegain] : [];

        Assert.Equal(expected.Split(','), CutLabEnablersPass.Apply(families, text, [role]));
    }
}
