using Bunit;
using DeckFlow.Studio.Shared;

namespace DeckFlow.Studio.Tests;

public sealed class HelpPanelTests : BunitContext
{
    [Fact]
    public void HelpPanel_RendersCollapsedByDefault()
    {
        var cut = Render<HelpPanel>(parameters => parameters
            .Add(p => p.Title, "Test panel")
            .AddChildContent("<p>Help body</p>"));

        var button = cut.Find("button");

        Assert.Equal("false", button.GetAttribute("aria-expanded"));
        Assert.DoesNotContain("Help body", cut.Markup);
        Assert.Empty(cut.FindAll(".card-body"));
    }

    [Fact]
    public void HelpPanel_ClickToggle_ShowsChildContentAndUpdatesAriaExpanded()
    {
        var cut = Render<HelpPanel>(parameters => parameters
            .Add(p => p.Title, "Test panel")
            .AddChildContent("<p>Help body</p>"));

        cut.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            var button = cut.Find("button");
            Assert.Equal("true", button.GetAttribute("aria-expanded"));
            Assert.Contains("Help body", cut.Markup);
            Assert.Single(cut.FindAll(".card-body"));
        });
    }
}
