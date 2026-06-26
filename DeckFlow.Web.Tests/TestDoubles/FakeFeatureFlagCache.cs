using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Services.FeatureFlags;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Test fake for <see cref="IFeatureFlagCache"/>. Defaults all flags to enabled (matches
/// FLAG-01 / D-13 default-on contract); individual flags may be overridden via the dictionary
/// passed to the constructor or by mutating <see cref="Flags"/> mid-test.
/// </summary>
internal sealed class FakeFeatureFlagCache : IFeatureFlagCache
{
    public Dictionary<string, bool> Flags { get; }

    public int ReloadCallCount { get; private set; }

    public FakeFeatureFlagCache(IDictionary<string, bool>? initial = null)
    {
        Flags = initial is null
            ? new Dictionary<string, bool>()
            : new Dictionary<string, bool>(initial);
    }

    public bool IsEnabled(string key) => !Flags.TryGetValue(key, out var enabled) || enabled;

    public IReadOnlyDictionary<string, bool> Snapshot() => Flags;

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        ReloadCallCount++;
        return Task.CompletedTask;
    }
}
