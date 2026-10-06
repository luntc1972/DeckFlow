namespace DeckFlow.Web.Services.Harvest;

/// <summary>Persisted Archidekt request-rate setting and most recent rate-limit trip marker.</summary>
public sealed record HarvestThrottleSnapshot(int MaxRequestsPerMinute, DateTimeOffset? RateLimitedUtc, DateTimeOffset UpdatedUtc);
