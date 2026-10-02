using DeckFlow.Web.Services.Harvest;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class MountainTimeFormatterTests
{
    [Theory]
    [InlineData("2026-07-01T18:00:00Z", "2026-07-01 12:00:00 MDT")]
    [InlineData("2026-01-15T18:00:00Z", "2026-01-15 11:00:00 MST")]
    [InlineData("2026-03-08T09:30:00Z", "2026-03-08 03:30:00 MDT")]
    [InlineData("2026-03-08T08:30:00Z", "2026-03-08 01:30:00 MST")]
    [InlineData("2026-11-01T07:30:00Z", "2026-11-01 01:30:00 MDT")]
    [InlineData("2026-11-01T08:30:00Z", "2026-11-01 01:30:00 MST")]
    public void Format_Instant_UsesMountainTimeAndAbbreviation(string instant, string expected)
    {
        Assert.Equal(expected, MountainTimeFormatter.Format(DateTimeOffset.Parse(instant)));
    }
}
