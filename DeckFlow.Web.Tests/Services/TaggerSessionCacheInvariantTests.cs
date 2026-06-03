using System;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the HIGH-2 invariant: the Tagger session cache TTL must stay at least the rotation
/// safety margin below the SocketsHttpHandler lifetime, so a request arriving just before
/// handler rotation cannot replay a stale cookie+token against a freshly rotated handler.
/// Program.cs sets both PooledConnectionLifetime and SetHandlerLifetime from
/// <see cref="TaggerSessionCache.HandlerLifetime"/>, so these constants are the single source
/// of truth this test protects against silent drift.
/// </summary>
public sealed class TaggerSessionCacheInvariantTests
{
    [Fact]
    public void SessionCacheTtl_StaysAtLeastSafetyMarginBelowHandlerLifetime()
    {
        Assert.True(
            TaggerSessionCache.SessionCacheTtl
                <= TaggerSessionCache.HandlerLifetime - TaggerSessionCache.RotationSafetyMargin,
            $"Tagger cache TTL ({TaggerSessionCache.SessionCacheTtl}) must be <= HandlerLifetime "
                + $"({TaggerSessionCache.HandlerLifetime}) minus the "
                + $"{TaggerSessionCache.RotationSafetyMargin} rotation margin (HIGH-2).");
    }

    [Fact]
    public void SessionRefreshAge_IsBelowSessionCacheTtl()
    {
        // Proactive background refresh must fire while the entry is still cached, not after it
        // has already expired (otherwise the refresh path is dead code).
        Assert.True(
            TaggerSessionCache.SessionRefreshAge < TaggerSessionCache.SessionCacheTtl,
            $"Refresh age ({TaggerSessionCache.SessionRefreshAge}) must be < cache TTL "
                + $"({TaggerSessionCache.SessionCacheTtl}).");
    }
}
