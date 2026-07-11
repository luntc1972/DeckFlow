using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Real HTTP implementation of <see cref="IDeployedBodyConfirmer"/>: GETs the Plan 90-07
/// deployed-body-hash endpoint with admin BasicAuth, bounded retries, and backoff. Reads its base
/// URL and admin credentials from the Studio deploy-confirm config keys (D-09 REVISED / D-10) via
/// the injected <see cref="IConfiguration"/> — never hardcoded, never logged. Uses the shared
/// Studio singleton <see cref="HttpClient"/> (already wrapped in <c>ResilientHttpHandler</c> for
/// transport-level retry); this type ALSO applies its own application-level poll loop because a
/// 404 or hash-mismatch is a valid "not deployed yet" outcome, not a transport error.
/// </summary>
public sealed class DeployedBodyConfirmer : IDeployedBodyConfirmer
{
    // Why: bounded so a permanently-missing/mismatched body (never deployed, or a redeploy that
    // legitimately failed) cannot hang the confirm flow forever (T-90-12) — after the budget is
    // exhausted the row stays durably awaiting-confirm (D-10, Plan 90-03) and is resumable via
    // Plan 90-06, never a false positive.
    private const int DefaultMaxAttempts = 5;
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(3);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly int _maxAttempts;
    private readonly TimeSpan _retryDelay;
    private readonly ILogger<DeployedBodyConfirmer> _logger;

    /// <summary>Creates the confirmer with the Studio shared HttpClient, configuration, and an optional logger.</summary>
    public DeployedBodyConfirmer(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<DeployedBodyConfirmer>? logger = null)
        : this(httpClient, configuration, DefaultMaxAttempts, DefaultRetryDelay, logger)
    {
    }

    // Test seam: a tiny retry delay + a small attempt cap so DeployedBodyConfirmerTests runs in
    // milliseconds, not tens of seconds, while exercising the same bounded-retry code path.
    internal DeployedBodyConfirmer(
        HttpClient httpClient,
        IConfiguration configuration,
        int maxAttempts,
        TimeSpan retryDelay,
        ILogger<DeployedBodyConfirmer>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        _httpClient = httpClient;
        _configuration = configuration;
        _maxAttempts = maxAttempts;
        _retryDelay = retryDelay;
        _logger = logger ?? NullLogger<DeployedBodyConfirmer>.Instance;
    }

    /// <inheritdoc />
    public async Task<bool> IsDeployedBodyConfirmedAsync(
        string naturalKeyType,
        string naturalKeyValue,
        string expectedBodySha256,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedBodySha256);

        // Why (D-09 REVISED/D-10): read presence-checked config values here, never cached at
        // construction — mirrors DirectPushCoordinator.CreateProdStore's per-call read pattern.
        // Values are never logged (SC5/D-07).
        var baseUrl = _configuration["Studio:PublicSiteBaseUrl"];
        var adminUser = _configuration["Studio:AdminUser"];
        var adminPassword = _configuration["Studio:AdminPassword"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(adminUser) || string.IsNullOrWhiteSpace(adminPassword))
        {
            // Why: never attempt a poll with missing creds — that is exactly the misconfigured-hang
            // class the Studio IsConfirmerConfigured badge/gate exists to prevent (T-90-12). Fail
            // fast with no wasted retries; the caller (DirectPushCoordinator) treats this as
            // not-confirmed, leaving the row durably awaiting-confirm.
            _logger.LogWarning(
                "Deploy-confirm skipped for {NaturalKeyType}/{NaturalKeyValue}: Studio deploy-confirm config is missing.",
                naturalKeyType,
                naturalKeyValue);
            return false;
        }

        var url = $"{baseUrl.TrimEnd('/')}/Admin/api/contentkb/deployed-body-hash" +
            $"?naturalKeyType={Uri.EscapeDataString(naturalKeyType)}&naturalKeyValue={Uri.EscapeDataString(naturalKeyValue)}";
        var authParameter = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{adminUser}:{adminPassword}"));

        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authParameter);

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var payload = JsonSerializer.Deserialize<DeployedBodyHashResponse>(body, JsonOptions);
                    if (payload?.BodySha256 is { } deployedHash
                        && string.Equals(deployedHash, expectedBodySha256, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    // 200 but the hash does not match — the old body is still deployed (un-deployed
                    // update race, D-09 REVISED). Keep polling.
                }

                // 404 (not yet at /app) or any other non-200 — keep polling.
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Why: a transient network failure is "not yet confirmed," not a hard error — the
                // confirm flow must never throw and abort a DirectPush batch on a single blip.
                _logger.LogWarning(
                    ex,
                    "Deploy-confirm poll attempt {Attempt}/{MaxAttempts} failed for {NaturalKeyType}/{NaturalKeyValue}.",
                    attempt,
                    _maxAttempts,
                    naturalKeyType,
                    naturalKeyValue);
            }

            if (attempt < _maxAttempts)
            {
                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    private sealed record DeployedBodyHashResponse(string? BodySha256);
}
