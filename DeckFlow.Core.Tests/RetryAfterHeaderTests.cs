using DeckFlow.Core.Integration;
using RestSharp;

namespace DeckFlow.Core.Tests;

public sealed class RetryAfterHeaderTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData(" 12 ", 12)]
    public void Parse_DeltaSeconds_ReturnsDelay(string raw, int seconds)
    {
        var result = RetryAfterHeader.Parse(raw, () => DateTimeOffset.UnixEpoch);

        Assert.Equal(TimeSpan.FromSeconds(seconds), result);
    }

    [Fact]
    public void Parse_PastHttpDate_ReturnsZero()
    {
        var result = RetryAfterHeader.Parse("Wed, 21 Oct 2015 07:28:00 GMT", () => new DateTimeOffset(2015, 10, 21, 7, 29, 0, TimeSpan.Zero));

        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void Read_RetryAfterHeader_ReturnsHeaderValue()
    {
        var response = new RestResponse
        {
            Headers = [new HeaderParameter("Retry-After", "9", false)],
        };

        Assert.Equal("9", RetryAfterHeader.Read(response));
    }
}
