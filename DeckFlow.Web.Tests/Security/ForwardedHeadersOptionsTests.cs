using System.Net;
using DeckFlow.Web;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DeckFlow.Web.Tests.Security;

public sealed class ForwardedHeadersOptionsTests
{
    [Fact]
    public void DeriveFeedbackPartitionKey_IgnoresForwardedForHeader()
    {
        // Arrange - forged X-Forwarded-For with immediate-peer set to a different value.
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Forwarded-For"] = "1.2.3.4";
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

        // Act
        var key = Program.DeriveFeedbackPartitionKey(ctx);

        // Assert - forged header value MUST NOT appear; immediate-peer IP DOES appear.
        Assert.DoesNotContain("1.2.3.4", key);
        Assert.Contains("10.0.0.1", key);
        Assert.StartsWith("peer:", key);
    }

    [Fact]
    public void DeriveFeedbackPartitionKey_FallsBackToUnknownWhenPeerMissing()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = null;

        var key = Program.DeriveFeedbackPartitionKey(ctx);

        Assert.Equal("peer:unknown", key);
    }
}
