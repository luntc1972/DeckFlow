using System;
using System.Collections.Concurrent;

namespace DeckFlow.Web.Infrastructure;

/// <summary>
/// One bucket of failed-attempt counts within a fixed 15-minute window. (BUG-02 / D-07.)
/// </summary>
public sealed record BucketEntry(int Count, DateTimeOffset WindowStart);

/// <summary>
/// In-memory IP-partitioned brute-force counter for /Admin basic-auth (BUG-02 / D-02).
/// </summary>
public interface IAdminBruteForceTracker
{
    /// <summary>Returns (true, retryAfterSeconds) if the partition is currently throttled.</summary>
    (bool Throttled, int RetryAfterSeconds) IsThrottled(string partitionKey, DateTimeOffset now);

    /// <summary>Atomically increments the bucket count for partitionKey; resets window if expired.</summary>
    void RecordFailure(string partitionKey, DateTimeOffset now);
}

/// <summary>
/// Default implementation backed by ConcurrentDictionary. Lazy expiry on access (D-08).
/// </summary>
public sealed class AdminBruteForceTracker : IAdminBruteForceTracker
{
    private const int PermitLimit = 10;                                     // D-06
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);     // D-06

    private readonly ConcurrentDictionary<string, BucketEntry> _buckets = new();

    /// <inheritdoc />
    public (bool Throttled, int RetryAfterSeconds) IsThrottled(string partitionKey, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(partitionKey);
        if (!_buckets.TryGetValue(partitionKey, out var entry)) return (false, 0);
        if (now - entry.WindowStart >= Window)
        {
            _buckets.TryRemove(partitionKey, out _);  // lazy expiry (D-08)
            return (false, 0);
        }
        if (entry.Count >= PermitLimit)
        {
            var remaining = (int)(Window - (now - entry.WindowStart)).TotalSeconds;
            return (true, Math.Max(remaining, 1));
        }
        return (false, 0);
    }

    /// <inheritdoc />
    public void RecordFailure(string partitionKey, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(partitionKey);
        _buckets.AddOrUpdate(
            partitionKey,
            _ => new BucketEntry(1, now),
            (_, existing) => (now - existing.WindowStart >= Window)
                ? new BucketEntry(1, now)
                : existing with { Count = existing.Count + 1 });
    }
}
