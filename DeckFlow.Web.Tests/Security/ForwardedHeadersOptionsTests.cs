using System.Net;
using DeckFlow.Web;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DeckFlow.Web.Tests.Security;

/// <summary>
/// TD-04 invariant guard tests (Phase 3 SC #4 — UPDATED for Phase 5 CF-Connecting-IP rewrite).
/// The Phase 3 partition key was remote-peer based; Phase 5 BUG-02 rewrites
/// it to feedback:&lt;CF-Connecting-IP header&gt;. The X-Forwarded-For-ignored invariant
/// (TD-04 spoof resistance) is preserved across the rewrite — this file proves it.
/// </summary>
public sealed class ForwardedHeadersOptionsTests
{
    [Fact]
    public void DeriveFeedbackPartitionKey_IgnoresForwardedForHeader()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Forwarded-For"] = "1.2.3.4";
        ctx.Request.Headers["CF-Connecting-IP"] = "10.20.30.40";
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

        var key = Program.DeriveFeedbackPartitionKey(ctx);

        Assert.DoesNotContain("1.2.3.4", key);
        Assert.DoesNotContain("10.0.0.1", key);
        Assert.Equal("feedback:10.20.30.40", key);
        Assert.StartsWith("feedback:", key);
    }

    [Fact]
    public void DeriveFeedbackPartitionKey_WithCfConnectingIpHeader_ReturnsFeedbackPlusHeaderValue()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["CF-Connecting-IP"] = "203.0.113.42";

        var key = Program.DeriveFeedbackPartitionKey(ctx);

        Assert.Equal("feedback:203.0.113.42", key);
    }

    [Fact]
    public void DeriveFeedbackPartitionKey_WithoutCfConnectingIpHeader_FallsBackToUnknown()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

        var key = Program.DeriveFeedbackPartitionKey(ctx);

        Assert.Equal("feedback:unknown", key);
    }
}
