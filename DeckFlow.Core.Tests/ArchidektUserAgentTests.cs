using System.Text.RegularExpressions;
using DeckFlow.Core.Integration;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Pins HARV-12 honest User-Agent formatting and shape boundaries.
/// </summary>
public sealed class ArchidektUserAgentTests
{
    [Theory]
    [InlineData("2026.09.6+abc123", null, "DeckFlow/2026.09.6 (+https://www.deckflow.gg)")]
    [InlineData("2026.09.6", null, "DeckFlow/2026.09.6 (+https://www.deckflow.gg)")]
    [InlineData(null, "1.2.3.4", "DeckFlow/1.2.3.4 (+https://www.deckflow.gg)")]
    [InlineData("+abc123", "1.0.0.0", "DeckFlow/1.0.0.0 (+https://www.deckflow.gg)")]
    [InlineData("   ", null, "DeckFlow/unknown (+https://www.deckflow.gg)")]
    public void Format_BuildsHonestProductToken(string? informationalVersion, string? assemblyVersion, string expected)
    {
        var version = assemblyVersion is null ? null : Version.Parse(assemblyVersion);

        Assert.Equal(expected, ArchidektUserAgent.Format(informationalVersion, version));
    }

    [Fact]
    public void Value_HasTheHonestShape()
    {
        Assert.Matches(new Regex("^DeckFlow/[^ ]+ \\(\\+https://www\\.deckflow\\.gg\\)$"), ArchidektUserAgent.Value);
        Assert.DoesNotContain("Mozilla", ArchidektUserAgent.Value);
        Assert.DoesNotContain("Chrome", ArchidektUserAgent.Value);
    }
}
