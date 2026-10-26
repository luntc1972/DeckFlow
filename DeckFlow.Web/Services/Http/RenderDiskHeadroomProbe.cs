using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Registry;
using RestSharp;

namespace DeckFlow.Web.Services.Http;

/// <summary>Reads Render PostgreSQL disk headroom without querying the database server.</summary>
public sealed class RenderDiskHeadroomProbe
{
    /// <summary>Render API base URI.</summary>
    public const string ApiBaseUri = "https://api.render.com/v1/";

    /// <summary>Render API-key configuration key.</summary>
    public const string ApiKeyVariable = "RENDER_API_KEY";

    /// <summary>Render PostgreSQL-resource configuration key.</summary>
    public const string PostgresIdVariable = "RENDER_POSTGRES_ID";

    /// <summary>Named HTTP client for the Render API.</summary>
    public const string HttpClientName = "render-api";

    /// <summary>Named resilience pipeline for the Render API.</summary>
    public const string PipelineName = "render";
    private static readonly TimeSpan CapacityCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan UsageCacheDuration = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SampleMaximumAge = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan SampleBucketDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SamplePollDelay = TimeSpan.FromSeconds(15);
    private const int SampleCallBudget = 10;
    private const int ResolutionSeconds = 30;
    private static readonly Regex PostgresIdPattern = new("^[A-Za-z0-9-]{1,100}$", RegexOptions.CultureInvariant);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResiliencePipeline<RestResponse> _pipeline;
    private readonly ILogger<RenderDiskHeadroomProbe> _logger;
    private readonly string? _apiKey;
    private readonly string? _postgresId;
    private readonly TimeProvider _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<DateTimeOffset> _sampleCalls = [];
    private long? _capacity;
    private DateTimeOffset _capacityReadAt;
    private UsageSample? _usage;
    private DateTimeOffset _usageReadAt;

    internal bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_postgresId) && PostgresIdPattern.IsMatch(_postgresId);

    /// <summary>Registers the process-wide Render disk probe.</summary>
    public static IServiceCollection AddTo(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<RenderDiskHeadroomProbe>();
        return services;
    }

    /// <summary>Initializes a probe using Render configuration values.</summary>
    public RenderDiskHeadroomProbe(IHttpClientFactory httpClientFactory, ResiliencePipelineProvider<string> pipelineProvider, IConfiguration configuration, ILogger<RenderDiskHeadroomProbe>? logger = null)
        : this(httpClientFactory, pipelineProvider, logger, configuration[ApiKeyVariable], configuration[PostgresIdVariable], TimeProvider.System, Task.Delay)
    {
        ArgumentNullException.ThrowIfNull(configuration);
    }

    internal RenderDiskHeadroomProbe(IHttpClientFactory httpClientFactory, ResiliencePipelineProvider<string> pipelineProvider, ILogger<RenderDiskHeadroomProbe>? logger, string? apiKey, string? postgresId, TimeProvider clock, Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _pipeline = (pipelineProvider ?? throw new ArgumentNullException(nameof(pipelineProvider))).GetPipeline<RestResponse>(PipelineName);
        _logger = logger ?? NullLogger<RenderDiskHeadroomProbe>.Instance;
        _apiKey = apiKey;
        _postgresId = postgresId;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
    }

    /// <summary>Gets free Render disk bytes, or null when the measurement is unsafe.</summary>
    public async Task<long?> GetFreeBytesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsConfigured)
        {
            LogFailure("render-probe-unconfigured");
            return null;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var capacity = await GetCapacityAsync(cancellationToken).ConfigureAwait(false);
            if (capacity is null) return null;
            var usage = await GetUsageAsync(cancellationToken).ConfigureAwait(false);
            if (usage is null) return null;
            try { return checked(capacity.Value - usage.Bytes); }
            catch (OverflowException)
            {
                LogFailure("render-metric-invalid");
                return null;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // Why: the key controls dashboard access and the id identifies production storage.
            LogFailure("render-http-failure", exceptionType: ex.GetType().Name);
            return null;
        }
        finally { _gate.Release(); }
    }

    private async Task<long?> GetCapacityAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        if (_capacity is not null && now - _capacityReadAt < CapacityCacheDuration) return _capacity;
        var response = await SendAsync($"postgres/{_postgresId}", "postgres", cancellationToken).ConfigureAwait(false);
        if (response is null) return null;
        using var document = JsonDocument.Parse(response.Content ?? string.Empty);
        if (!document.RootElement.TryGetProperty("diskSizeGB", out var size) || !size.TryGetDouble(out var gigabytes) || !TryBytes(gigabytes, "GB", out var bytes) || bytes <= 0)
        {
            LogFailure("render-metric-invalid");
            return null;
        }
        _capacity = bytes;
        _capacityReadAt = now;
        return bytes;
    }

    private async Task<UsageSample?> GetUsageAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        if (_usage is not null && now - _usageReadAt < UsageCacheDuration && HasBudget(_usage.Timestamp, now))
        {
            RecordAnsweredCall(now);
            return _usage;
        }
        UsageSample? sample;
        if (_usage is not null && !HasBudget(_usage.Timestamp, now))
        {
            sample = _usage;
        }
        else
        {
            sample = await FetchUsageAsync(cancellationToken).ConfigureAwait(false);
            if (sample is null) return null;
        }
        while (!HasBudget(sample.Timestamp, _clock.GetUtcNow()))
        {
            if (IsStale(sample.Timestamp, _clock.GetUtcNow()))
            {
                LogFailure("render-metric-stale");
                return null;
            }
            await _delayAsync(SamplePollDelay, cancellationToken).ConfigureAwait(false);
            var newer = await FetchUsageAsync(cancellationToken).ConfigureAwait(false);
            if (newer is null) return null;
            if (newer.Timestamp > sample.Timestamp) sample = newer;
        }
        _usage = sample;
        _usageReadAt = _clock.GetUtcNow();
        RecordAnsweredCall(_usageReadAt);
        return sample;
    }

    private async Task<UsageSample?> FetchUsageAsync(CancellationToken cancellationToken)
    {
        var response = await SendAsync($"metrics/disk-usage?resource={Uri.EscapeDataString(_postgresId!)}&resolutionSeconds={ResolutionSeconds}", "disk-usage", cancellationToken).ConfigureAwait(false);
        if (response is null) return null;
        using var document = JsonDocument.Parse(response.Content ?? string.Empty);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() != 1)
        {
            LogFailure("render-response-invalid");
            return null;
        }
        var series = document.RootElement[0];
        if (!series.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array || values.GetArrayLength() == 0)
        {
            LogFailure("render-response-invalid");
            return null;
        }
        UsageSample? newest = null;
        foreach (var value in values.EnumerateArray())
        {
            if (!value.TryGetProperty("timestamp", out var timestamp) || !DateTimeOffset.TryParse(timestamp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var observedAt) || !value.TryGetProperty("value", out var valueElement) || !valueElement.TryGetDouble(out var observedValue))
            {
                LogFailure("render-response-invalid");
                return null;
            }
            var unit = value.TryGetProperty("unit", out var unitValue) ? unitValue.GetString() : series.TryGetProperty("unit", out var seriesUnit) ? seriesUnit.GetString() : null;
            if (!TryBytes(observedValue, unit, out var bytes))
            {
                LogFailure("render-metric-invalid");
                return null;
            }
            var candidate = new UsageSample(observedAt, bytes);
            if (newest is null || candidate.Timestamp > newest.Timestamp) newest = candidate;
        }
        var now = _clock.GetUtcNow();
        if (newest is null)
        {
            LogFailure("render-response-invalid");
            return null;
        }
        if (newest.Timestamp > now.AddSeconds(30))
        {
            LogFailure("render-metric-invalid");
            return null;
        }
        if (IsStale(newest.Timestamp, now))
        {
            LogFailure("render-metric-stale");
            return null;
        }
        return newest;
    }

    private async Task<RestResponse?> SendAsync(string resource, string endpoint, CancellationToken cancellationToken)
    {
        var client = new RestClient(_httpClientFactory.CreateClient(HttpClientName));
        var request = new RestRequest(new Uri(new Uri(ApiBaseUri), resource), Method.Get);
        request.AddHeader("Authorization", $"Bearer {_apiKey}");
        var response = await _pipeline.ExecuteAsync(async ct => await client.ExecuteAsync(request, ct).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (response.IsSuccessful) return response;
        LogFailure("render-http-failure", endpoint, (int)response.StatusCode);
        return null;
    }

    private bool HasBudget(DateTimeOffset timestamp, DateTimeOffset now)
    {
        _sampleCalls.RemoveAll(call => now - call >= CapacityCacheDuration);
        return _sampleCalls.Count(call => call >= timestamp - SampleBucketDuration) < SampleCallBudget;
    }

    private void RecordAnsweredCall(DateTimeOffset now) => _sampleCalls.Add(now);
    private static bool IsStale(DateTimeOffset timestamp, DateTimeOffset now) => now - timestamp > SampleMaximumAge;

    private static bool TryBytes(double value, string? unit, out long bytes)
    {
        bytes = 0;
        var multiplier = unit switch
        {
            "B" => 1d,
            "bytes" => 1d,
            "KB" => 1_000d,
            "MB" => 1_000_000d,
            "GB" => 1_000_000_000d,
            "TB" => 1_000_000_000_000d,
            "KiB" => 1_024d,
            "MiB" => 1_048_576d,
            "GiB" => 1_073_741_824d,
            "TiB" => 1_099_511_627_776d,
            _ => 0d,
        };
        var scaled = value * multiplier;
        if (!double.IsFinite(value) || value < 0 || multiplier == 0 || !double.IsFinite(scaled) || scaled > long.MaxValue) return false;
        bytes = (long)Math.Ceiling(scaled);
        return true;
    }

    private void LogFailure(string reason, string? endpoint = null, int? statusCode = null, string? exceptionType = null)
        => _logger.LogWarning("Render disk headroom probe failed: {Reason} {Endpoint} {StatusCode} {ExceptionType}", reason, endpoint, statusCode, exceptionType);

    private sealed record UsageSample(DateTimeOffset Timestamp, long Bytes);
}
