using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the harvest backlog warning treatment in the admin stylesheet.
/// </summary>
public sealed class AdminHarvestCssTests
{
    [Fact]
    public void HarvestBacklogWarning_DefinesTokenAndVisibleBadgeBorder()
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
            "admin-common.css"));

        Assert.Matches(new Regex(@"--warning:\s*#facc15", RegexOptions.IgnoreCase), content);
        Assert.Matches(
            new Regex(
                @"\.admin-harvest__health-badge\s*\{[^}]*border:\s*1px\s+solid\s+currentColor",
                RegexOptions.Singleline),
            content);
    }
}
