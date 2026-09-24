using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class IntakeStateCssTests
{
    [Fact]
    public void EmptyIntakeState_HidesItsSummary()
    {
        string content = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "wwwroot",
            "css",
            "site-common.css"));

        Assert.Matches(new Regex(@"\.cutlab-intake\[data-intake-state=""empty""\]\s*>\s*\.cutlab-intake-summary\s*\{[^}]*display:\s*none;"), content);
    }
}
