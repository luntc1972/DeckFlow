using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Services.FeatureFlags;

namespace DeckFlow.Web.Tests;

/// <summary>
/// In-memory test fake for <see cref="IFeatureFlagStore"/>. Records the last
/// <see cref="SetEnabledAsync"/> call so the admin flag-toggle path can be asserted.
/// </summary>
internal sealed class FakeFeatureFlagStore : IFeatureFlagStore
{
    private readonly Dictionary<string, bool> _flags = new();

    /// <summary>Key of the most recent SetEnabledAsync call, or null if never called.</summary>
    public string? LastSetKey { get; private set; }

    /// <summary>Enabled value of the most recent SetEnabledAsync call.</summary>
    public bool LastSetEnabled { get; private set; }

    /// <summary>Number of SetEnabledAsync calls.</summary>
    public int SetCallCount { get; private set; }

    public Task<IReadOnlyDictionary<string, bool>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<string, bool>>(new Dictionary<string, bool>(_flags));

    public Task SetEnabledAsync(string key, bool enabled, CancellationToken cancellationToken = default)
    {
        SetCallCount++;
        LastSetKey = key;
        LastSetEnabled = enabled;
        _flags[key] = enabled;
        return Task.CompletedTask;
    }

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
