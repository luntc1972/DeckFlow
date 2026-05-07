---
phase: 260507-ner
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs
  - DeckFlow.Web/wwwroot/ts/admin-analytics.ts
  - DeckFlow.Web/Views/AdminAnalytics/Index.cshtml
autonomous: true
requirements:
  - QUICK-260507-ner

must_haves:
  truths:
    - "GET /Admin/Analytics/status returns 200 JSON { metricsRevision: \"<token>\" } for same-origin browser requests."
    - "GET /Admin/Analytics/status returns 403 with { Message } body for cross-origin requests (SameOriginRequestValidator gate)."
    - "Analytics page polls /Admin/Analytics/status every 15s once loaded; on token change full-page reloads (preserving ?range=)."
    - "Local-dev SQLite path returns a stable empty-ish token (\"|0\") and never throws — page does not loop-reload."
    - "Postgres SUM/MAX query failure is caught, logged, and yields a stable fallback token (\"|err\") so transient DB hiccups do not loop-reload."
    - "Noscript fallback meta refresh (60s) keeps the page eventually-consistent when JS is disabled."
  artifacts:
    - path: "DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs"
      provides: "Status endpoint + IMemoryCache injection + AnalyticsStatusPayload record"
      contains: "[HttpGet(\"status\")]"
    - path: "DeckFlow.Web/wwwroot/ts/admin-analytics.ts"
      provides: "Always-on 15s poller, full-page reload on metricsRevision change"
      contains: "metricsRevision"
    - path: "DeckFlow.Web/Views/AdminAnalytics/Index.cshtml"
      provides: "Noscript meta refresh + script tag"
      contains: "admin-analytics.js"
  key_links:
    - from: "Views/AdminAnalytics/Index.cshtml"
      to: "wwwroot/js/admin-analytics.js"
      via: "@section Scripts"
      pattern: "src=\"~/js/admin-analytics.js\""
    - from: "wwwroot/ts/admin-analytics.ts"
      to: "/Admin/Analytics/status"
      via: "fetch with credentials: 'same-origin'"
      pattern: "/Admin/Analytics/status"
    - from: "AdminAnalyticsController.Status"
      to: "request_metrics table"
      via: "Npgsql SUM(hit_count) + MAX(day_utc)"
      pattern: "SUM\\(hit_count\\)"
---

<objective>
Add /Admin/Analytics auto-refresh. Mirror the harvest revision-token pattern shipped in 260507-m8k. Backend: new `GET /Admin/Analytics/status` returns `{ metricsRevision: "<maxDay>|<sumHits>" }` behind SameOriginRequestValidator + 5s IMemoryCache. Frontend: always-on 15s poller; on token change full-page reload (preserves ?range= query string).

Purpose: Operators viewing /Admin/Analytics see fresh top-routes data without manually refreshing. Mirrors the harvest pattern user just confirmed works (commit 9698551).

Output: Status endpoint, TS poller module, view wiring (noscript fallback + script include).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@.planning/quick/260507-m8k-fix-admin-harvest-decks-counter-and-rece/260507-m8k-PLAN.md
@.planning/quick/260507-m8k-fix-admin-harvest-decks-counter-and-rece/260507-m8k-SUMMARY.md
@DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs
@DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs
@DeckFlow.Web/wwwroot/ts/admin-harvest.ts
@DeckFlow.Web/Views/AdminHarvest/Index.cshtml
@DeckFlow.Web/Views/AdminAnalytics/Index.cshtml
@DeckFlow.Web/Security/SameOriginRequestValidator.cs

<interfaces>
<!-- Key facts the executor needs without re-exploring the codebase. -->

The exemplar Status endpoint (AdminHarvestController.cs:97-125):
```csharp
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
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
        // ... build payload ...
        return new HarvestStatusPayload(...);
    }).ConfigureAwait(false);

    return Json(payload);
}
```

The current AdminAnalyticsController constructor (lines 28-34):
```csharp
public AdminAnalyticsController(IWebHostEnvironment environment, ILogger<AdminAnalyticsController> logger)
{
    ArgumentNullException.ThrowIfNull(environment);
    ArgumentNullException.ThrowIfNull(logger);
    _environment = environment;
    _logger = logger;
}
```
Needs IMemoryCache added (already DI-registered globally for AdminHarvestController; no Program.cs change).

The Postgres connection pattern (LoadRowsAsync, lines 71-90):
```csharp
var connInfo = DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection(_environment);
if (!connInfo.IsPostgres)
{
    return Array.Empty<AdminAnalyticsViewModel.RouteRow>();
}

var dbConn = connInfo.CreateConnection();
await dbConn.OpenAsync(ct).ConfigureAwait(false);
await using var conn = (NpgsqlConnection)dbConn;
```
Reuse this. For SQLite return `"|0"` instead of empty rows.

Schema fact (CRITICAL): `request_metrics` has NO `updated_utc`. Token must be derived as:
```sql
SELECT COALESCE(MAX(day_utc)::text, ''), COALESCE(SUM(hit_count), 0) FROM request_metrics;
```
Token format: `$"{maxDay}|{sumHits}"` (e.g., `"2026-05-07|125834"`). Either column flips the token on any flush.

The harvest TS poller (admin-harvest.ts) is a state-machine variant — analytics is simpler (no Active/Idle distinction, no DOM updates, just always-poll-and-reload-on-change).
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Backend — add Status endpoint to AdminAnalyticsController</name>
  <files>DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs</files>
  <action>
Edit `DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs`:

1. Add usings at the top (alongside existing): `using DeckFlow.Web.Security;` and `using Microsoft.Extensions.Caching.Memory;`. Existing `using Npgsql;` is already present.

2. Add a private const beneath `SparklineDays`:
```csharp
private const string StatusCacheKey = "admin.analytics.status.v1";
```

3. Add an `_memoryCache` field alongside `_environment`/`_logger`:
```csharp
private readonly IMemoryCache _memoryCache;
```

4. Update the constructor to accept `IMemoryCache memoryCache` (third parameter, between environment and logger or after logger — pick a stable order; recommended: after logger). Add `ArgumentNullException.ThrowIfNull(memoryCache);` and `_memoryCache = memoryCache;`. Update the XML doc to mention `<param name="memoryCache">5-second status payload cache.</param>`.
   IMemoryCache is already registered globally (used by AdminHarvestController) — no Program.cs change needed.

5. Add a new action method directly after the existing `Index` action and before `LoadRowsAsync`:
```csharp
/// <summary>
/// GET /Admin/Analytics/status — JSON revision token used by the in-page poller for auto-refresh.
/// Same-origin gated; results cached 5s in IMemoryCache to absorb fan-out from the 15s poller.
/// Returns { metricsRevision: "<maxDay>|<sumHits>" }. SQLite local-dev returns "|0".
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
        return $"{maxDay}|{sumHits}";
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

private sealed record AnalyticsStatusPayload(string MetricsRevision);
```

Notes:
- The new `[HttpGet("status")]` resolves to `/Admin/Analytics/status` — no route collision with the existing `[HttpGet("")]` Index (different action template).
- Reuse `DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection(_environment)` exactly as `LoadRowsAsync` does.
- Mirror the existing error-handling style of `Index`: catch `OperationCanceledException` first when `ct.IsCancellationRequested`, then a broad `catch (Exception ex)` with `_logger.LogError`.
- `AnalyticsStatusPayload` is a `private sealed record` mirroring the harvest controller's `HarvestStatusPayload` shape (single field).
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln /clp:ErrorsOnly</automated>
  </verify>
  <done>
- File compiles clean (0 errors; baseline NU1900 NuGet warnings only).
- `grep -n 'metricsRevision\|MetricsRevision\|StatusCacheKey\|AnalyticsStatusPayload\|HttpGet("status")' DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs` shows all five symbols.
- Constructor has 3 dependencies (env, logger, memoryCache) with `ArgumentNullException.ThrowIfNull` for each.
- The SQL string `SELECT COALESCE(MAX(day_utc)::text, ''), COALESCE(SUM(hit_count), 0) FROM request_metrics;` is present verbatim.
- Fallbacks `"|0"` (SQLite + zero-row) and `"|err"` (caught exception) are present.
  </done>
</task>

<task type="auto">
  <name>Task 2: Frontend — TS poller + view wiring</name>
  <files>DeckFlow.Web/wwwroot/ts/admin-analytics.ts, DeckFlow.Web/Views/AdminAnalytics/Index.cshtml</files>
  <action>
**Part A — Create new file `DeckFlow.Web/wwwroot/ts/admin-analytics.ts`:**

```typescript
// Always-on 15s poller for /Admin/Analytics. On metricsRevision change → full-page reload.
// window.location.reload() preserves the URL including ?range=today|7d|30d|all query string,
// so the operator's selected window survives auto-refresh.
((): void => {
  'use strict';

  const POLL_INTERVAL_MS = 15000;
  const FETCH_TIMEOUT_MS = 10000;

  type AnalyticsStatusPayload = {
    metricsRevision: string;
  };

  const fetchStatus = async (): Promise<AnalyticsStatusPayload | null> => {
    const abortController = new AbortController();
    const timeoutId = window.setTimeout(() => abortController.abort(), FETCH_TIMEOUT_MS);

    try {
      const response = await fetch('/Admin/Analytics/status', {
        credentials: 'same-origin',
        headers: { Accept: 'application/json' },
        signal: abortController.signal
      });

      if (!response.ok) {
        return null;
      }

      return await response.json() as AnalyticsStatusPayload;
    } finally {
      window.clearTimeout(timeoutId);
    }
  };

  document.addEventListener('DOMContentLoaded', () => {
    let stopped = false;
    let reloaded = false;
    let timerId: number | null = null;
    let lastRevision: string | null = null;

    const stopPolling = (): void => {
      stopped = true;
      if (timerId !== null) {
        window.clearTimeout(timerId);
        timerId = null;
      }
    };

    const schedulePoll = (): void => {
      if (stopped) {
        return;
      }
      timerId = window.setTimeout(() => { void poll(); }, POLL_INTERVAL_MS);
    };

    const poll = async (): Promise<void> => {
      try {
        const payload = await fetchStatus();
        if (payload === null) {
          // Soft failure (3xx/4xx/5xx) — keep polling. Analytics may be transiently unavailable during a deploy.
          schedulePoll();
          return;
        }

        if (lastRevision === null) {
          // First successful poll — capture baseline; do NOT reload.
          lastRevision = payload.metricsRevision;
          schedulePoll();
          return;
        }

        if (payload.metricsRevision !== lastRevision) {
          stopPolling();
          if (!reloaded) {
            reloaded = true;
            window.location.reload();
          }
          return;
        }

        schedulePoll();
      } catch {
        // Hard fetch error (network drop, abort, etc.) — stop permanently. Mirrors admin-harvest.ts behavior.
        stopPolling();
      }
    };

    schedulePoll();
  });
})();
```

Style notes mirroring `admin-harvest.ts`:
- IIFE wrapper with `'use strict'`.
- `type` (not `interface`) for the payload, with `metricsRevision: string`.
- AbortController + 10s timeout.
- `schedulePoll()` re-uses a single 15s cadence (no Active/Idle split — analytics has no state distinction).
- Soft errors (non-OK response) keep polling; hard errors (caught fetch exception) stop.
- `lastRevision === null` is the baseline-capture sentinel; do NOT reload on first poll.

**Part B — Edit `DeckFlow.Web/Views/AdminAnalytics/Index.cshtml`:**

1. Insert the noscript fallback **immediately after the `@{ ViewData["Title"] = "Analytics"; }` block** (between line 4 and the opening `<section>`):
```cshtml
<noscript><meta http-equiv="refresh" content="60" /></noscript>
```
60s noscript cadence is intentional — slower than harvest's 5s because metrics flush every 5s and the page is high-overview, not real-time-critical.

2. Append a `@section Scripts` block at the very end of the file, after the closing `</section>` (mirror `Views/AdminHarvest/Index.cshtml:197-200`):
```cshtml

@section Scripts {
    <script src="~/js/admin-analytics.js" asp-append-version="true"></script>
}
```

The TS file compiles to `wwwroot/js/admin-analytics.js` automatically via the existing `CompileTypeScriptAssets` MSBuild target — no `tsconfig.json`, csproj, or `_Layout.cshtml` change needed.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln /clp:ErrorsOnly &amp;&amp; test -f DeckFlow.Web/wwwroot/js/admin-analytics.js &amp;&amp; grep -q 'metricsRevision' DeckFlow.Web/wwwroot/js/admin-analytics.js &amp;&amp; grep -q 'metricsRevision' DeckFlow.Web/wwwroot/ts/admin-analytics.ts &amp;&amp; grep -q 'http-equiv="refresh" content="60"' DeckFlow.Web/Views/AdminAnalytics/Index.cshtml &amp;&amp; grep -q 'src="~/js/admin-analytics.js"' DeckFlow.Web/Views/AdminAnalytics/Index.cshtml</automated>
  </verify>
  <done>
- `dotnet build DeckFlow.sln` is clean (0 errors).
- `wwwroot/js/admin-analytics.js` exists (compiled by MSBuild target).
- `metricsRevision` symbol present in both `wwwroot/ts/admin-analytics.ts` and the compiled `wwwroot/js/admin-analytics.js`.
- `<meta http-equiv="refresh" content="60" />` present inside `<noscript>` in `Views/AdminAnalytics/Index.cshtml`.
- `<script src="~/js/admin-analytics.js" asp-append-version="true"></script>` present in `Views/AdminAnalytics/Index.cshtml`.
- `@section Scripts {` block present in `Views/AdminAnalytics/Index.cshtml`.
  </done>
</task>

</tasks>

<verification>
**Pass 1 — Build:**
- `dotnet build DeckFlow.sln /clp:ErrorsOnly` exits 0.

**Pass 2 — Static contract checks:**
- `grep -c 'metricsRevision\|MetricsRevision' DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs` ≥ 1.
- `grep -c 'metricsRevision' DeckFlow.Web/wwwroot/ts/admin-analytics.ts` ≥ 1.
- `grep -c 'metricsRevision' DeckFlow.Web/wwwroot/js/admin-analytics.js` ≥ 1.
- `grep -c 'http-equiv="refresh" content="60"' DeckFlow.Web/Views/AdminAnalytics/Index.cshtml` == 1.
- `grep -c 'src="~/js/admin-analytics.js"' DeckFlow.Web/Views/AdminAnalytics/Index.cshtml` == 1.
- `grep -c '\[HttpGet("status")\]' DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs` == 1.
- `grep -c 'SameOriginRequestValidator' DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs` ≥ 1.
- `grep -c 'IMemoryCache' DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs` ≥ 2 (field + ctor param).

**Pass 3 — Route resolution sanity:**
- The controller has `[Route("Admin/Analytics")]` (line 16). The new `[HttpGet("status")]` action resolves to `/Admin/Analytics/status`. The existing `[HttpGet("")]` Index resolves to `/Admin/Analytics`. No template collision (different action templates under same controller route prefix is supported by attribute routing).

**Live verification (deferred to operator post-deploy):**
- Visit https://www.deckflow.gg/Admin/Analytics, then hit any other route (e.g., /sync) to generate a request that flows through the metrics buffer/flusher.
- Within ~20s the analytics page should auto-reload (15s poll + 5s flush window).
- Top Routes hit counts should tick up.
- Selected `?range=` (e.g., `?range=today`) should be preserved across the auto-reload.
- Browser DevTools Network tab should show `GET /Admin/Analytics/status` returning 200 with `{ "metricsRevision": "..." }` every 15s.
</verification>

<success_criteria>
- Backend: `/Admin/Analytics/status` returns 200 JSON `{ metricsRevision: "<token>" }` for same-origin requests; 403 for cross-origin.
- Token derives from `MAX(day_utc) || '|' || SUM(hit_count)` over `request_metrics` (no schema change).
- 5s IMemoryCache wraps the SUM/MAX query (mirrors harvest's 1s wrapper, but tuned for the 15s poller cadence).
- SQLite local-dev returns `"|0"` and never queries Postgres.
- Postgres errors are logged + return `"|err"` — poller does not loop-reload on transient failures.
- Frontend: `admin-analytics.ts` compiles to `admin-analytics.js`; the view loads it.
- Poller cadence: 15s. On token change → exactly one `window.location.reload()` (guarded by `reloaded` flag), preserving `?range=` query string.
- Noscript users get a 60s meta refresh fallback.
- No changes to `IRequestMetricsStore`, `Program.cs`, or `tsconfig.json`.
- Plain commit (no Co-Authored-By trailer) by the orchestrator after the executor finishes.
</success_criteria>

<output>
After completion, create `.planning/quick/260507-ner-add-admin-analytics-auto-refresh-via-met/260507-ner-SUMMARY.md` documenting:
- Files changed (3) with brief description.
- Token format chosen and why (`MAX(day_utc)|SUM(hit_count)` — no `updated_utc` column exists).
- The two stable fallbacks (`"|0"` for SQLite, `"|err"` for query failure) and why each prevents reload-loops.
- Build verification result.
- Deferred live-verification checklist for the operator.
</output>
