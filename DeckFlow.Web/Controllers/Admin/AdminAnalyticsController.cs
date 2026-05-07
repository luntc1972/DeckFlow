using DeckFlow.Core.Storage;
using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using System.Globalization;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// Operator UI for /Admin/Analytics. Renders the top-routes table for a chosen time
/// window (today / 7d / 30d / all-time) per ANLY-04, with each row showing hit count,
/// unique-IP count, error rate, and an inline SVG sparkline (ANLY-05).
/// BasicAuth gating is provided by the existing /Admin path branch in Program.cs
/// (ADMIN-03); no per-action [Authorize] attribute is needed.
/// </summary>
[Route("Admin/Analytics")]
public sealed class AdminAnalyticsController : Controller
{
    private const int TopRouteLimit = 50;   // D-17: top 50 by hit_count DESC
    private const int SparklineDays = 14;   // D-18: 14-day sparkline window
    private const string StatusCacheKey = "admin.analytics.status.v1";

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdminAnalyticsController> _logger;
    private readonly IMemoryCache _memoryCache;

    /// <summary>Initializes a new instance of <see cref="AdminAnalyticsController"/>.</summary>
    /// <param name="environment">Used by <c>DeckFlowDatabaseConnectionFactory</c> to resolve the analytics DB.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="memoryCache">5-second status payload cache.</param>
    public AdminAnalyticsController(IWebHostEnvironment environment, ILogger<AdminAnalyticsController> logger, IMemoryCache memoryCache)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(memoryCache);
        _environment = environment;
        _logger = logger;
        _memoryCache = memoryCache;
    }

    /// <summary>
    /// GET /Admin/Analytics — renders the top-routes analytics table for the selected window.
    /// Invalid <paramref name="range"/> values fall back to "7d" per D-16.
    /// </summary>
    /// <param name="range">Time window: today, 7d, 30d, or all.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("")]
    public async Task<IActionResult> Index(string range = "7d", CancellationToken cancellationToken = default)
    {
        var normalized = AdminAnalyticsViewModel.AllowedRanges.Contains(range) ? range : "7d";

        var rows = Array.Empty<AdminAnalyticsViewModel.RouteRow>();
        try
        {
            rows = await LoadRowsAsync(normalized, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminAnalyticsController.Index query failed for range {Range}.", normalized);
        }

        var vm = new AdminAnalyticsViewModel
        {
            Range = normalized,
            Routes = rows,
        };
        return View(vm);
    }

    /// <summary>
    /// GET /Admin/Analytics/status — JSON revision token used by the in-page poller for auto-refresh.
    /// Same-origin gated; results cached 5s in IMemoryCache to absorb fan-out from the 15s poller.
    /// Returns { metricsRevision: "&lt;maxDay&gt;|&lt;sumHits&gt;" }. SQLite local-dev returns "|0".
    /// On query failure returns a stable fallback "|err" so the poller does not loop on transient DB hiccups.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { Message = "This endpoint only accepts same-origin browser requests." });
        }

        var payload = await _memoryCache.GetOrCreateAsync(StatusCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5);
            var revision = await GetMetricsRevisionAsync(cancellationToken).ConfigureAwait(false);
            return new AnalyticsStatusPayload(revision);
        }).ConfigureAwait(false);

        return Json(payload);
    }

    private async Task<string> GetMetricsRevisionAsync(CancellationToken ct)
    {
        var connInfo = DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection(_environment);
        if (!connInfo.IsPostgres)
        {
            // Local-dev SQLite — analytics is Postgres-only (D-01). Stable token => no reload loop.
            return "|0";
        }

        try
        {
            var dbConn = connInfo.CreateConnection();
            await dbConn.OpenAsync(ct).ConfigureAwait(false);
            await using var conn = (NpgsqlConnection)dbConn;

            // Token: MAX(day_utc) flips at midnight UTC; SUM(hit_count) increments on every flushed batch.
            // Either change flips the revision string and triggers a single full-page reload in the poller.
            const string sql = "SELECT COALESCE(MAX(day_utc)::text, ''), COALESCE(SUM(hit_count), 0) FROM request_metrics;";
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return "|0";
            }

            var maxDay = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var sumHits = reader.IsDBNull(1) ? 0L : reader.GetInt64(1);
            return $"{maxDay}|{sumHits.ToString(CultureInfo.InvariantCulture)}";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminAnalyticsController.GetMetricsRevisionAsync failed.");
            return "|err";
        }
    }

    private async Task<AdminAnalyticsViewModel.RouteRow[]> LoadRowsAsync(string range, CancellationToken ct)
    {
        // Mirror RequestMetricsStore: acquire the same Postgres connection the store writes to.
        var connInfo = DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection(_environment);
        if (!connInfo.IsPostgres)
        {
            // Analytics is Postgres-only (D-01). Local-dev SQLite renders an empty page.
            return Array.Empty<AdminAnalyticsViewModel.RouteRow>();
        }

        // Build the WHERE clause from the whitelisted range value — no user input interpolated.
        var whereClause = range switch
        {
            "today" => "WHERE day_utc = CURRENT_DATE",
            "7d"    => "WHERE day_utc >= CURRENT_DATE - INTERVAL '6 days'",
            "30d"   => "WHERE day_utc >= CURRENT_DATE - INTERVAL '29 days'",
            _        => "",   // all-time — no filter
        };

        var dbConn = connInfo.CreateConnection();
        await dbConn.OpenAsync(ct).ConfigureAwait(false);
        await using var conn = (NpgsqlConnection)dbConn;

        // (1) Top-N routes by hits in the selected window + error counts for error_rate.
        var topRoutesSql = $"""
            SELECT route_key,
                   SUM(hit_count)::bigint   AS hits,
                   SUM(error_count)::bigint AS errors
              FROM request_metrics
              {whereClause}
             GROUP BY route_key
             ORDER BY hits DESC
             LIMIT @limit;
            """;

        var topRoutes = new List<(string RouteKey, long Hits, long Errors)>(TopRouteLimit);
        await using (var cmd = new NpgsqlCommand(topRoutesSql, conn))
        {
            cmd.Parameters.AddWithValue("limit", TopRouteLimit);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                topRoutes.Add((reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2)));
            }
        }

        if (topRoutes.Count == 0)
        {
            return Array.Empty<AdminAnalyticsViewModel.RouteRow>();
        }

        var routeKeys = topRoutes.Select(r => r.RouteKey).ToArray();

        // (2) Unique-IP counts per route_key over the selected window.
        // Switch over the whitelisted range value — no user input reaches SQL (T-08-18).
        var ipSql = range switch
        {
            "today" => "SELECT route_key, COUNT(DISTINCT ip_hash)::bigint FROM request_metric_ip_seen WHERE day_utc = CURRENT_DATE AND route_key = ANY(@routeKeys) GROUP BY route_key;",
            "7d"    => "SELECT route_key, COUNT(DISTINCT ip_hash)::bigint FROM request_metric_ip_seen WHERE day_utc >= CURRENT_DATE - INTERVAL '6 days' AND route_key = ANY(@routeKeys) GROUP BY route_key;",
            "30d"   => "SELECT route_key, COUNT(DISTINCT ip_hash)::bigint FROM request_metric_ip_seen WHERE day_utc >= CURRENT_DATE - INTERVAL '29 days' AND route_key = ANY(@routeKeys) GROUP BY route_key;",
            _        => "SELECT route_key, COUNT(DISTINCT ip_hash)::bigint FROM request_metric_ip_seen WHERE route_key = ANY(@routeKeys) GROUP BY route_key;",
        };

        var ipCounts = new Dictionary<string, long>(StringComparer.Ordinal);
        await using (var cmd = new NpgsqlCommand(ipSql, conn))
        {
            cmd.Parameters.AddWithValue("routeKeys", routeKeys);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                ipCounts[reader.GetString(0)] = reader.GetInt64(1);
            }
        }

        // (3) Per-day hit counts over the last 14 days for sparklines — always 14-day window
        // regardless of the selected range filter (D-18).
        const string sparkSql = """
            SELECT route_key, day_utc, SUM(hit_count)::bigint
              FROM request_metrics
             WHERE day_utc >= CURRENT_DATE - INTERVAL '13 days'
               AND route_key = ANY(@routeKeys)
             GROUP BY route_key, day_utc;
            """;

        var perDay = new Dictionary<string, int[]>(StringComparer.Ordinal);
        foreach (var key in routeKeys)
        {
            perDay[key] = new int[SparklineDays];
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using (var cmd = new NpgsqlCommand(sparkSql, conn))
        {
            cmd.Parameters.AddWithValue("routeKeys", routeKeys);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var key = reader.GetString(0);
                var day = DateOnly.FromDateTime(reader.GetDateTime(1));
                var count = (int)Math.Min(int.MaxValue, reader.GetInt64(2));
                // index 13 = today, index 0 = 13 days ago
                var dayIndex = SparklineDays - 1 - (today.DayNumber - day.DayNumber);
                if (dayIndex >= 0 && dayIndex < SparklineDays && perDay.TryGetValue(key, out var arr))
                {
                    arr[dayIndex] = count;
                }
            }
        }

        // Compose RouteRows preserving topRoutes ordering (hit_count DESC).
        var result = new AdminAnalyticsViewModel.RouteRow[topRoutes.Count];
        for (var i = 0; i < topRoutes.Count; i++)
        {
            var t = topRoutes[i];
            var errorRate = t.Hits > 0 ? (double)t.Errors / t.Hits : 0.0;
            ipCounts.TryGetValue(t.RouteKey, out var uniqueIps);
            var hitsByDay = perDay.TryGetValue(t.RouteKey, out var arr) ? arr : new int[SparklineDays];
            result[i] = new AdminAnalyticsViewModel.RouteRow(
                RouteKey: t.RouteKey,
                Hits: t.Hits,
                UniqueIps: uniqueIps,
                ErrorRate: errorRate,
                HitsByDay: hitsByDay);
        }

        return result;
    }

    private sealed record AnalyticsStatusPayload(string MetricsRevision);
}
