using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.FeatureFlags;

/// <summary>
/// Default <see cref="IFeatureFlagCache"/> backed by <see cref="IFeatureFlagStore"/>.
/// Registered as Singleton + IHostedService (see <c>FeatureFlagsServiceCollectionExtensions</c>).
/// Inherits BackgroundService for the 30s poller and overrides StartAsync to perform a
/// synchronous initial load before the host reports ready (D-14).
/// </summary>
public sealed class FeatureFlagCache : BackgroundService, IFeatureFlagCache
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IFeatureFlagStore _store;
    private readonly ILogger<FeatureFlagCache> _logger;
    private readonly ConcurrentDictionary<string, byte> _warnedMissing = new(StringComparer.Ordinal);

    /// <summary>Atomically replaced by ReloadAsync; reads are lock-free.</summary>
    private volatile IReadOnlyDictionary<string, bool> _snapshot =
        new Dictionary<string, bool>(0, StringComparer.Ordinal);

    /// <summary>
    /// DI constructor. Registered as a singleton and as an IHostedService (see
    /// FeatureFlagsServiceCollectionExtensions.AddDeckFlowFeatureFlags).
    /// </summary>
    /// <param name="store">Feature flag persistence store (Postgres or SQLite).</param>
    /// <param name="logger">Logger for poll failures and missing-key warnings.</param>
    public FeatureFlagCache(IFeatureFlagStore store, ILogger<FeatureFlagCache> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _logger = logger;
    }

    /// <summary>Test seam — bypasses logging plumbing for unit tests that drive the cache directly.</summary>
    /// <param name="store">Feature flag persistence store fake or stub.</param>
    internal FeatureFlagCache(IFeatureFlagStore store)
        : this(store, NullLogger<FeatureFlagCache>.Instance) { }

    /// <inheritdoc />
    public bool IsEnabled(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var snapshot = _snapshot;
        if (snapshot.TryGetValue(key, out var enabled))
        {
            return enabled;
        }
        WarnMissingKeyOnce(key);
        return true; // D-13 default-on
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, bool> Snapshot() => _snapshot;

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var fresh = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);
            _snapshot = fresh;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // T-06-D1: never replace a good snapshot with an empty one on transient PG failure.
            _logger.LogError(exception,
                "FeatureFlag.ReloadFailure could not refresh feature_flags snapshot; existing snapshot preserved (count={Count}).",
                _snapshot.Count);
        }
    }

    /// <summary>
    /// D-14: synchronous initial load before the host reports ready, so the very first
    /// request sees a populated snapshot (not the empty-default cold-start window).
    /// </summary>
    /// <param name="cancellationToken">Host startup cancellation token.</param>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>30s poller backstop (FLAG-02). ReloadAsync swallows non-cancellation exceptions internally.</summary>
    /// <param name="stoppingToken">Cancellation token signaled when the host is shutting down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await ReloadAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown path.
        }
    }

    private void WarnMissingKeyOnce(string key)
    {
        if (_warnedMissing.TryAdd(key, 0))
        {
            // D-13: first miss only — logged once per process for each missing key.
            _logger.LogWarning(
                "FeatureFlag.MissingKey {Key} queried; defaulting to enabled=true. Suppressing further warnings for this key.",
                key);
        }
    }
}
