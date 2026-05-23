# Phase 6: Admin Shell + Flags Foundation - Pattern Map

**Mapped:** 2026-05-02
**Files analyzed:** 18 (13 create, 5 modify)
**Analogs found:** 17 / 18 (1 net-new pattern: action filter attribute)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml` (new) | Razor layout | template-render | `Views/Shared/_Layout.cshtml` | role-match (strip themes) |
| `DeckFlow.Web/Views/Admin/_ViewStart.cshtml` (new) | Razor viewstart | template-render | `Views/_ViewStart.cshtml` | exact |
| `DeckFlow.Web/Views/AdminFeedback/_ViewStart.cshtml` (new) | Razor viewstart | template-render | `Views/_ViewStart.cshtml` | exact |
| `DeckFlow.Web/wwwroot/css/admin.css` (new) | static CSS | template-render | `wwwroot/css/site-common.css` (shape only) | role-match |
| `DeckFlow.Web/Controllers/Admin/AdminFlagsController.cs` (new) | controller | request-response (CRUD-ish) | `Controllers/Admin/AdminFeedbackController.cs` | exact |
| `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` (new shell) | controller | request-response | `Controllers/Admin/AdminFeedbackController.cs` | exact |
| `DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs` (new shell) | controller | request-response | `Controllers/Admin/AdminFeedbackController.cs` | exact |
| `DeckFlow.Web/Views/AdminFlags/Index.cshtml` (new) | Razor view | template-render | `Views/AdminFeedback/Index.cshtml` | exact |
| `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` (new placeholder) | Razor view | template-render | `Views/AdminFeedback/Index.cshtml` | role-match |
| `DeckFlow.Web/Views/AdminAnalytics/Index.cshtml` (new placeholder) | Razor view | template-render | `Views/AdminFeedback/Index.cshtml` | role-match |
| `DeckFlow.Web/Views/Shared/_MaintenancePage.cshtml` (new) | Razor partial / view | template-render | `Views/Shared/_FormError.cshtml` (shape) | partial |
| `DeckFlow.Web/Models/Admin/MaintenanceViewModel.cs` (new) | view model | data-shape | `Controllers/Admin/AdminFeedbackController.cs` (`AdminFeedbackListViewModel`) | role-match |
| `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagStore.cs` + impl (new) | service / repository | CRUD (relational) | `Services/AdminBruteForceTrackerStore.cs` | exact (Postgres+SQLite, EnsureSchemaAsync, IWebHostEnvironment ctor) |
| `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagCache.cs` + impl (new) | service / hosted | event-driven (poll + invalidate) | `Services/ArchidektCacheJobService.cs` (BackgroundService + Singleton dual-reg) | role-match |
| `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs` (new) | action filter / attribute | request-response (gate) | **NO ANALOG** — codebase has zero `IActionFilter`/`ActionFilterAttribute`. Closest cousin = `Infrastructure/BasicAuthMiddleware.cs` (gate semantics, but middleware not filter) | partial |
| `DeckFlow.Web/Extensions/AddDeckFlowFeatureFlagsExtension.cs` (new) | DI extension | composition | `Services/Http/ResiliencePipelineFactory.cs` (`AddDeckFlowResiliencePipelines`) | exact |
| `DeckFlow.Web/Program.cs` (modify) | composition root | composition | self (lines 50-189, 178-180, 331-332) | self |
| `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs` (modify, layout swap only) | controller | request-response | self | self |
| `DeckFlow.Web/Views/AdminFeedback/{Index,Detail}.cshtml` (modify, layout swap only) | Razor view | template-render | self | self |
| `DeckFlow.Web/Services/ScryfallTaggerService.cs` (modify, flag gate at top of `LookupOracleTagsAsync`) | service | request-response (cache-first) | self (lines 84-95 short-circuit precedent) | self |
| `DeckFlow.Web/Controllers/HelpController.cs` (modify, attach `[FeatureFlagGate]`) | controller | request-response | self | self |
| `DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs` (modify, add `CreateFeatureFlagConnection`) | factory | composition | self (line 21 `CreateAdminThrottleConnection`) | self |

---

## Pattern Assignments

### `Services/FeatureFlags/FeatureFlagStore.cs` (service / repository, CRUD)

**Analog:** `DeckFlow.Web/Services/AdminBruteForceTrackerStore.cs` — newest (Phase 5) example of dialect-pluggable store, smaller surface than `FeedbackStore`, perfect template.

**Imports + namespace** (`AdminBruteForceTrackerStore.cs:1-6`):
```csharp
using System.Data;
using System.Data.Common;
using System.Globalization;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services;
```
For new file use `namespace DeckFlow.Web.Services.FeatureFlags;`.

**Interface + class header pattern** (`AdminBruteForceTrackerStore.cs:14-32, 40-48`):
```csharp
public interface IAdminBruteForceTrackerStore
{
    Task<(bool Throttled, int RetryAfterSeconds)> IsThrottledAsync(
        string partitionKey, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task RecordFailureAsync(
        string partitionKey, DateTimeOffset now, CancellationToken cancellationToken = default);
}

public sealed class AdminBruteForceTrackerStore : IAdminBruteForceTrackerStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;
```

**Triple constructor pattern (test seam + DI)** (`AdminBruteForceTrackerStore.cs:50-68`):
```csharp
public AdminBruteForceTrackerStore(string databasePath)
    : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

public AdminBruteForceTrackerStore(RelationalDatabaseConnection connectionInfo)
{
    ArgumentNullException.ThrowIfNull(connectionInfo);
    _connectionInfo = connectionInfo;
    if (_connectionInfo.IsSqlite)
    {
        var directory = Path.GetDirectoryName(_connectionInfo.ExtractSqlitePath());
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}

public AdminBruteForceTrackerStore(IWebHostEnvironment environment)
    : this(DeckFlowDatabaseConnectionFactory.CreateAdminThrottleConnection(environment)) { }
```
For `FeatureFlagStore` mirror exactly — replace `CreateAdminThrottleConnection` with new `CreateFeatureFlagConnection` factory method (or re-use the feedback Postgres connection like brute-force does — D-07 says "alongside existing tables", so reuse the feedback connection).

**`EnsureSchemaAsync` pattern** (`AdminBruteForceTrackerStore.cs:143-160`):
```csharp
private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
{
    if (_schemaReady) return;
    await _schemaGate.WaitAsync(cancellationToken);
    try
    {
        if (_schemaReady) return;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var create = connection.CreateCommand();
        create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
        await create.ExecuteNonQueryAsync(cancellationToken);
        _schemaReady = true;
    }
    finally
    {
        _schemaGate.Release();
    }
}
```
For `FeatureFlagStore`, after `await create.ExecuteNonQueryAsync` add the seed `INSERT ... ON CONFLICT (key) DO NOTHING` for `scryfall.tagger.enabled` and `page.help.enabled` (D-09). Both seeds in one batched command keeps the gate single-shot.

**Dual-dialect schema constants** (`AdminBruteForceTrackerStore.cs:162-176`):
```csharp
private const string PostgresCreateTableSql = """
    CREATE TABLE IF NOT EXISTS admin_brute_force_buckets (
      partition_key TEXT PRIMARY KEY,
      count         INT NOT NULL,
      window_start  TIMESTAMPTZ NOT NULL
    );
    """;

private const string SqliteCreateTableSql = """
    CREATE TABLE IF NOT EXISTS admin_brute_force_buckets (
      partition_key TEXT PRIMARY KEY,
      count         INTEGER NOT NULL,
      window_start  TEXT NOT NULL
    );
    """;
```
For `feature_flags` per D-07 use:
- Postgres: `key TEXT PRIMARY KEY, enabled BOOLEAN NOT NULL DEFAULT TRUE, updated_at TIMESTAMPTZ NOT NULL DEFAULT now()`
- SQLite: `key TEXT PRIMARY KEY, enabled INTEGER NOT NULL DEFAULT 1, updated_at TEXT NOT NULL DEFAULT (datetime('now'))`

**Dual-dialect upsert** (`AdminBruteForceTrackerStore.cs:178-206`):
```csharp
private const string PostgresUpsertSql = """
    INSERT INTO admin_brute_force_buckets (partition_key, count, window_start)
    VALUES (@key, 1, @now)
    ON CONFLICT(partition_key)
    DO UPDATE SET ...
    """;
```
Lessons-baked: qualify upsert columns with table name (per `feedback_sqlite_postgres_sql_divergence.md`). Apply same to `SetEnabledAsync(string key, bool enabled)`. For seed-on-create use `ON CONFLICT (key) DO NOTHING` (D-09).

**Param binding helper** (`AdminBruteForceTrackerStore.cs:115-121`):
```csharp
RelationalDatabaseConnection.AddParameter(command, "@key", partitionKey);
RelationalDatabaseConnection.AddParameter(
    command, "@now",
    _connectionInfo.IsPostgres
        ? (object)now.UtcDateTime
        : now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
```
Use `RelationalDatabaseConnection.AddParameter` (defined in `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs:48-54`) — never `command.Parameters.AddWithValue`.

**`IRelationalDialect` extension (if needed)** — `IRelationalDialect` (`DeckFlow.Core/Storage/IRelationalDialect.cs:3-9`) currently has only Feedback-specific properties. For Phase 6, `feature_flags` SQL is simple enough to keep in the store as `Postgres*Sql` / `Sqlite*Sql` constants (matching `AdminBruteForceTrackerStore`), avoiding an interface bump. Only extend `IRelationalDialect` if planner finds a third site that needs the same column-type abstraction.

---

### `Services/FeatureFlags/FeatureFlagCache.cs` (service / hosted, event-driven)

**Analog:** `DeckFlow.Web/Services/ArchidektCacheJobService.cs` — sets the dual `Singleton` + `IHostedService` registration precedent (`Program.cs:279-281`).

**Class header — singleton facade + `BackgroundService`** (`ArchidektCacheJobService.cs:36-51`):
```csharp
public sealed class ArchidektCacheJobService : BackgroundService, IArchidektCacheJobService
{
    private readonly Channel<ArchidektCacheJobStatus> _queue = Channel.CreateUnbounded<ArchidektCacheJobStatus>();
    private readonly ConcurrentDictionary<Guid, ArchidektCacheJobStatus> _jobs = new();
    private readonly ICategoryKnowledgeStore _knowledgeStore;
    private readonly ILogger<ArchidektCacheJobService> _logger;

    public ArchidektCacheJobService(
        ICategoryKnowledgeStore knowledgeStore,
        ILogger<ArchidektCacheJobService> logger)
    {
        _knowledgeStore = knowledgeStore;
        _logger = logger;
    }
```
For `FeatureFlagCache`:
- Inherit `BackgroundService` (gives you `StartAsync` via `IHostedService` interface for D-14 sync initial load + `ExecuteAsync` for the 30s poller).
- Hold flags in `volatile IReadOnlyDictionary<string, bool> _snapshot = new Dictionary<string,bool>(0);`.
- Hold missing-key dedupe set as `private readonly ConcurrentDictionary<string, byte> _warnedMissing = new();` (per `<code_context>` §Established Patterns).

**`ExecuteAsync` poll loop** (`ArchidektCacheJobService.cs:105-150`):
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await foreach (var queuedJob in _queue.Reader.ReadAllAsync(stoppingToken))
    {
        ...
        try { ... }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Archidekt cache background job {JobId} failed.", runningJob.JobId);
            ...
        }
    }
}
```
Adapt to a 30-second `PeriodicTimer` loop calling `ReloadAsync(stoppingToken)`; same try/catch shape with `OperationCanceledException` re-throw guard.

**Synchronous initial load before Kestrel binds (D-14)** — `BackgroundService.StartAsync` runs `ExecuteAsync` on a background task and returns immediately, so it does NOT block startup. To honor D-14, override `StartAsync` and `await ReloadAsync(cancellationToken)` BEFORE calling `await base.StartAsync(cancellationToken)`. Pattern:
```csharp
public override async Task StartAsync(CancellationToken cancellationToken)
{
    await ReloadAsync(cancellationToken).ConfigureAwait(false);  // D-14 sync first load
    await base.StartAsync(cancellationToken).ConfigureAwait(false);  // schedules ExecuteAsync
}
```
No analog in current codebase (`ArchidektCacheJobService` doesn't override `StartAsync`); planner adds this as net-new.

**DI registration in `Program.cs`** (`Program.cs:279-281`) — exact dual-registration precedent:
```csharp
builder.Services.AddSingleton<ArchidektCacheJobService>();
builder.Services.AddSingleton<IArchidektCacheJobService>(sp => sp.GetRequiredService<ArchidektCacheJobService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ArchidektCacheJobService>());
```
For new extension `AddDeckFlowFeatureFlags()` mirror exactly with `FeatureFlagCache` / `IFeatureFlagCache`.

**Public API surface (D-12)** — three members:
```csharp
public interface IFeatureFlagCache
{
    bool IsEnabled(string key);
    IReadOnlyDictionary<string, bool> Snapshot();
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
```

---

### `Extensions/AddDeckFlowFeatureFlagsExtension.cs` (DI extension, composition)

**Analog:** `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` — exact precedent for "group all related DI registrations into one extension method" pattern.

**Extension shape** (`ResiliencePipelineFactory.cs:21-32`):
```csharp
public static class ResiliencePipelineFactory
{
    public static IServiceCollection AddDeckFlowResiliencePipelines(this IServiceCollection services)
    {
        DeckFlowResiliencePipelineRegistry.AddResiliencePipeline<string, RestResponse>(services, "banlist", builder => BuildBanList(builder));
        ...
        return services;
    }
```
For new file:
```csharp
public static class FeatureFlagsServiceCollectionExtensions
{
    public static IServiceCollection AddDeckFlowFeatureFlags(this IServiceCollection services)
    {
        services.AddSingleton<IFeatureFlagStore, FeatureFlagStore>();
        services.AddSingleton<FeatureFlagCache>();
        services.AddSingleton<IFeatureFlagCache>(sp => sp.GetRequiredService<FeatureFlagCache>());
        services.AddHostedService(sp => sp.GetRequiredService<FeatureFlagCache>());
        return services;
    }
}
```
Call site in `Program.cs` (slot near line 156-158 next to other singleton stores, between `IFeedbackStore` and `IAdminBruteForceTrackerStore` registrations).

---

### `Services/DeckFlowDatabaseConnectionFactory.cs` (modify — add `CreateFeatureFlagConnection`)

**Analog:** self, lines 21-22 (`CreateAdminThrottleConnection` reuses `CreateFeedbackConnection`):
```csharp
public static RelationalDatabaseConnection CreateAdminThrottleConnection(IWebHostEnvironment environment)
    => CreateFeedbackConnection(environment);
```
Add at line ~23 (mirror exactly):
```csharp
/// <summary>
/// Returns the relational connection used by FeatureFlagStore (Phase 6, FLAG-01..05).
/// Shares the feedback Postgres connection in production (single logical DB; the
/// feature_flags table sits alongside feedback and admin_brute_force_buckets).
/// In local-dev SQLite, also shares the feedback.db file.
/// </summary>
public static RelationalDatabaseConnection CreateFeatureFlagConnection(IWebHostEnvironment environment)
    => CreateFeedbackConnection(environment);
```

---

### `Controllers/Admin/AdminFlagsController.cs` (controller, request-response/CRUD-ish)

**Analog:** `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs` — exact match for admin-route convention, BasicAuth-via-MapWhen reliance, `[ValidateAntiForgeryToken]` POST pattern.

**Class header + route attribute** (`AdminFeedbackController.cs:27-35`):
```csharp
[Route("Admin/Feedback")]
public sealed class AdminFeedbackController : Controller
{
    private readonly IFeedbackStore _store;

    public AdminFeedbackController(IFeedbackStore store)
    {
        _store = store;
    }
```
For `AdminFlagsController` use `[Route("Admin/Flags")]`, inject `IFeatureFlagCache _cache, IFeatureFlagStore _store`.

**GET Index pattern** (`AdminFeedbackController.cs:37-58`):
```csharp
[HttpGet("")]
public async Task<IActionResult> Index(...)
{
    ...
    var vm = new AdminFeedbackListViewModel { ... };
    return View(vm);
}
```
For Flags index: build VM from `_cache.Snapshot()` — render dotted-namespace list with toggle form per row.

**POST + antiforgery + TempData** (`AdminFeedbackController.cs:68-94`):
```csharp
[HttpPost("{id:long}/{op}")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Apply(long id, AdminFeedbackOp op)
{
    if (!ModelState.IsValid) return BadRequest();
    switch (op) { ... }
    TempData["AdminFeedbackAction"] = $"{op.ToString().ToLowerInvariant()} applied to #{id}";
    return RedirectToAction(nameof(Index));
}
```
For Flags toggle: `[HttpPost("{key}/toggle")]`, call `_store.SetEnabledAsync(key, enabled)`, then `await _cache.ReloadAsync(HttpContext.RequestAborted)` (D-10 synchronous in-process reload), then redirect with TempData success banner.

---

### `Controllers/Admin/AdminHarvestController.cs` + `AdminAnalyticsController.cs` (placeholder shells)

**Analog:** `AdminFeedbackController` — same shape, both shells render an empty view that says "coming in Phase 7/8":
```csharp
[Route("Admin/Harvest")]
public sealed class AdminHarvestController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
```
Sidebar nav target only; full controllers land in Phase 7 / 8.

---

### `Views/Shared/_AdminLayout.cshtml` (Razor layout)

**Analog:** `Views/Shared/_Layout.cshtml` — read for SHAPE only. **DO NOT carry over** the `themeOptions` array (lines 2-37), the theme-picker `<select>` (lines 57-65), the `site-common.css`/`site-mobile.css`/`site-{theme}.css` link tags (lines 44-46), or the public-site footer (lines 74-77).

**KEEP from `_Layout.cshtml`:**
- Doctype + `<head>` skeleton (lines 38-43): `<meta charset>`, viewport, `<title>@ViewData["Title"] - DeckFlow Admin</title>`.
- `@RenderBody()` placement (line 72) and `@RenderSection("Scripts", required: false)` (line 91) for per-page additions.
- Skip-link (line 49) for accessibility.

**REPLACE with admin chrome (D-01..D-04):**
- Single CSS link: `<link rel="stylesheet" href="~/css/admin.css" asp-append-version="true" />` — only one stylesheet (D-05 prohibits any `site-*.css`).
- Top bar with section H1 (`@ViewData["Title"]`) on left and version stamp on right. Pull stamp from injected `IVersionService` via `@inject`:
  ```cshtml
  @inject DeckFlow.Web.Services.IVersionService VersionService
  ...
  <span class="admin-topbar__version">v@VersionService.GetVersion()</span>
  ```
  `VersionService.cs:18-30` reads `AssemblyInformationalVersionAttribute` — no Dockerfile change needed (satisfies D-04 + Claude's Discretion bullet).
- Sidebar `<nav>` with four `<a>` links: Feedback, Harvest, Analytics, Flags. Active item gets `aria-current="page"` and a CSS class for the left-border accent (D-03). Compute active state from `ViewContext.RouteData.Values["controller"]`.
- No footer (D-04).
- No back-to-top button, no `df-select.js`/`df-typeahead.js`, no extension bridge attributes.

---

### `Views/Admin/_ViewStart.cshtml` + `Views/AdminFeedback/_ViewStart.cshtml`

**Analog:** `Views/_ViewStart.cshtml` (3 lines, complete file):
```cshtml
@{
    Layout = "_Layout";
}
```
Per-folder override for admin:
```cshtml
@{
    Layout = "_AdminLayout";
}
```
**Place the file under `Views/Shared/_AdminLayout.cshtml`** so Razor's view resolution finds it from any controller's view folder (location decision per D-05 Claude's Discretion). One `_ViewStart.cshtml` in each of `Views/Admin/`, `Views/AdminFeedback/`, `Views/AdminFlags/`, `Views/AdminHarvest/`, `Views/AdminAnalytics/`. (Razor doesn't have inheritance across sibling controller folders; one file per folder is the cheapest, most explicit option.)

---

### `Views/AdminFlags/Index.cshtml` (Razor view)

**Analog:** `Views/AdminFeedback/Index.cshtml:1-97`.

**TempData success banner pattern** (`AdminFeedback/Index.cshtml:4-14`):
```cshtml
@{
    ViewData["Title"] = "Admin — Feedback";
    var actionMessage = TempData["AdminFeedbackAction"] as string;
}
...
@if (!string.IsNullOrEmpty(actionMessage))
{
    <div class="feedback-banner feedback-banner--success">@actionMessage</div>
}
```
For Flags use `TempData["AdminFlagsAction"]` and a neutral admin banner class.

**Table + per-row antiforgery POST form** (`AdminFeedback/Index.cshtml:49-83`):
```cshtml
<table class="admin-feedback-table">
    <thead><tr><th>Created (UTC)</th><th>Type</th>...<th>Actions</th></tr></thead>
    <tbody>
    @foreach (var item in Model.Items)
    {
        <tr>
            <td>...</td>
            <td>
                <form method="post" asp-action="Apply" asp-route-id="@item.Id" asp-route-op="archive" class="admin-action-form">
                    @Html.AntiForgeryToken()
                    <button type="submit">Archive</button>
                </form>
            </td>
        </tr>
    }
    </tbody>
</table>
```
For Flags: rows are `(key, enabled)` pairs from `_cache.Snapshot()`. Each row has a single toggle form posting to `Admin/Flags/{key}/toggle` with the next-state encoded as a hidden input. `@Html.AntiForgeryToken()` is mandatory on every POST form (ADMIN-05).

---

### `Views/Shared/_MaintenancePage.cshtml` + `Models/Admin/MaintenanceViewModel.cs` (D-17)

**Analog (model):** `AdminFeedbackController.AdminFeedbackListViewModel` (`AdminFeedbackController.cs:14-25`):
```csharp
public sealed class AdminFeedbackListViewModel
{
    public IReadOnlyList<FeedbackItem> Items { get; init; } = Array.Empty<FeedbackItem>();
    public FeedbackStatus? StatusFilter { get; init; }
    ...
}
```
For new file (`Models/Admin/MaintenanceViewModel.cs`):
```csharp
namespace DeckFlow.Web.Models.Admin;

public sealed class MaintenanceViewModel
{
    public string Title { get; init; } = "Temporarily unavailable";
    public string Message { get; init; } = "This page is offline for maintenance. Please try again shortly.";
}
```

**Analog (view):** `Views/AdminFeedback/Detail.cshtml:1-45` for shape (`@model` + `<section>` + `<h1>` + content). Maintenance view does NOT use admin layout — it renders inside the user-facing `_Layout.cshtml` (the page's normal chrome stays; only the inner content swaps to the maintenance message). Header in view file:
```cshtml
@model DeckFlow.Web.Models.Admin.MaintenanceViewModel
@{
    ViewData["Title"] = Model.Title;
}
<section class="maintenance-page">
    <h1>@Model.Title</h1>
    <p>@Model.Message</p>
</section>
```

---

### `Infrastructure/FeatureFlagGateAttribute.cs` (action filter — NO ANALOG)

**No existing `IActionFilter` / `IAsyncActionFilter` / `ActionFilterAttribute` in `DeckFlow.Web` or `DeckFlow.Core`** (verified via `grep -r "ActionFilter\|IActionFilter\|IAsyncActionFilter\|FilterAttribute"`). This is a new pattern for the codebase.

**Closest cousin (gate semantics):** `Infrastructure/BasicAuthMiddleware.cs:33-52`:
```csharp
public async Task InvokeAsync(HttpContext context)
{
    var partitionKey = Program.DeriveAdminPartitionKey(context);
    var (throttled, retryAfter) = await _store.IsThrottledAsync(partitionKey, ...);
    if (throttled)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers["Retry-After"] = retryAfter.ToString(...);
        return;
    }
    ...
    await _next(context);
}
```
Same short-circuit shape, different abstraction. For the action filter use `Microsoft.AspNetCore.Mvc.Filters.IAsyncActionFilter`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Services.FeatureFlags;

namespace DeckFlow.Web.Infrastructure;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class FeatureFlagGateAttribute : Attribute, IAsyncActionFilter
{
    public string Key { get; }
    public string Title { get; init; } = "Temporarily unavailable";
    public string Message { get; init; } = "This page is offline for maintenance. Please try again shortly.";

    public FeatureFlagGateAttribute(string key) => Key = key;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cache = context.HttpContext.RequestServices
            .GetRequiredService<IFeatureFlagCache>();
        if (!cache.IsEnabled(Key))
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.HttpContext.Response.Headers["Retry-After"] = "300";
            var vm = new MaintenanceViewModel { Title = Title, Message = Message };
            context.Result = new ViewResult
            {
                ViewName = "_MaintenancePage",
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                    { Model = vm }
            };
            return;
        }
        await next();
    }
}
```
**Service resolution** uses `context.HttpContext.RequestServices.GetRequiredService<T>()` (per-request scope) — the canonical way to use DI inside an `Attribute` filter, since attribute constructors only accept compile-time constants.

**Naming convention** for attribute classes (project convention from `CardLookupService.cs` ctor + per `CLAUDE.md` §Code Style): `sealed class XxxAttribute`, file matches type name exactly (`FeatureFlagGateAttribute.cs`), `namespace DeckFlow.Web.Infrastructure;`.

---

### `Services/ScryfallTaggerService.cs` (modify — flag gate at top of `LookupOracleTagsAsync`, D-11)

**Analog:** self, lines 84-95 — the existing "set/number empty → return `[]`" short-circuit shows the gate-and-return pattern at the top of the public method:
```csharp
public async Task<IReadOnlyList<string>> LookupOracleTagsAsync(string cardName, CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(cardName);

    var stopwatch = Stopwatch.StartNew();
    var trimmedName = cardName.Trim();

    var (set, collectorNumber) = await ResolveCardPrintingAsync(trimmedName, cancellationToken).ConfigureAwait(false);
    if (string.IsNullOrEmpty(set) || string.IsNullOrEmpty(collectorNumber))
    {
        return [];
    }
    ...
}
```

**Required modifications** (D-11):
1. Add `IFeatureFlagCache _flagCache` field.
2. Inject in constructor (between existing deps and `pipelineProvider` at `ScryfallTaggerService.cs:63-68`):
   ```csharp
   public ScryfallTaggerService(
       IScryfallRestClientFactory scryfallRestClientFactory,
       IScryfallTaggerHttpClient taggerHttpClient,
       ITaggerSessionCache taggerSessionCache,
       ResiliencePipelineProvider<string> pipelineProvider,
       IFeatureFlagCache flagCache,                              // NEW
       ILogger<ScryfallTaggerService>? logger = null)
   {
       ArgumentNullException.ThrowIfNull(flagCache);
       ...
       _flagCache = flagCache;
   }
   ```
3. Add gate as the first statement after `ArgumentException.ThrowIfNullOrWhiteSpace`:
   ```csharp
   public async Task<IReadOnlyList<string>> LookupOracleTagsAsync(string cardName, CancellationToken cancellationToken = default)
   {
       ArgumentException.ThrowIfNullOrWhiteSpace(cardName);

       if (!_flagCache.IsEnabled("scryfall.tagger.enabled"))
       {
           return Array.Empty<string>();
       }
       ...
   }
   ```
   Use `Array.Empty<string>()` not `[]` to match the rest of the file (lines 254, 317, 332, 342) and avoid mid-method style drift.
4. Update `Program.cs:242` registration to inject `IFeatureFlagCache` — currently `AddSingleton<IScryfallTaggerService, ScryfallTaggerService>()` resolves all deps from DI automatically, so no change needed if `IFeatureFlagCache` is registered before this line.

---

### `Controllers/HelpController.cs` (modify — apply `[FeatureFlagGate]`, D-16, D-18)

**Analog:** self (entire 22-line file).

**Modification:** add the attribute to `Index()` only (D-16 says one user-facing page proves the pattern):
```csharp
[HttpGet("/help")]
[FeatureFlagGate("page.help.enabled",
    Title = "Help center temporarily unavailable",
    Message = "Help is offline for maintenance. Please try again in a few minutes.")]
public IActionResult Index() => View(_content.GetAll());
```
`Topic(string slug)` is left unflagged for v1.1 — D-16 explicitly anchors the demo to the index page only. (Planner can choose to flag both if they prefer; CONTEXT does not forbid it.)

---

### `Program.cs` (modify — DI registration + Tagger ctor wiring)

**Analog:** self.

**Insertions (in order at lines ~157-158, before existing `AddSingleton<IAdminBruteForceTrackerStore, ...>`):**
```csharp
builder.Services.AddDeckFlowFeatureFlags();
```
The extension internally registers store (singleton), cache (singleton + hosted), and is the single composition seam.

**No middleware change** — admin path branch at lines 330-332 (`UseWhen("/Admin")` + `BasicAuthMiddleware`) already covers the new admin controllers without modification (per `<code_context>` §Integration Points).

**No `EnsureSchemaAsync` call from `ValidateDatabaseConnectionsAsync` is needed** — the cache's `StartAsync` (D-14) calls `_store.GetAllAsync` which internally invokes `EnsureSchemaAsync` (lazy bootstrap pattern, same as `FeedbackStore` and `AdminBruteForceTrackerStore`). Skipping the explicit validation matches the existing services' contract.

---

### `Controllers/Admin/AdminFeedbackController.cs` + `Views/AdminFeedback/{Index,Detail}.cshtml` (modify — layout swap only, D-15)

**Analog:** self.

**Zero controller changes.** Per D-15, only Razor layout resolution changes:
- Create `Views/AdminFeedback/_ViewStart.cshtml` (3 lines, `Layout = "_AdminLayout";`).
- Existing `Views/AdminFeedback/Index.cshtml` and `Detail.cshtml` have NO `Layout = ...` line in the `@{}` block (verified via grep — empty result), so the new `_ViewStart.cshtml` takes effect with no edits to the views themselves.
- Routes, HTTP verbs, view models, antiforgery — all unchanged. Trivially satisfies ADMIN-04 "no regression."

---

## Shared Patterns

### File header / namespace convention

**Source:** every `.cs` file in the project (e.g. `AdminBruteForceTrackerStore.cs:1-6`, `ScryfallTaggerService.cs:14`, `BasicAuthMiddleware.cs:1-8`).

**Apply to:** every new `.cs` file.

```csharp
using System;
using <System.* sorted>;
using <Third-party>;
using DeckFlow.Core.* / DeckFlow.Web.*;

namespace DeckFlow.Web.<Folder>;   // file-scoped, never block-scoped

public sealed class XxxYyy : IXxxYyy   // `sealed` on every leaf type
{
    private readonly IDep _dep;        // _camelCase fields
    public XxxYyy(IDep dep)
    {
        ArgumentNullException.ThrowIfNull(dep);   // mandatory guard
        _dep = dep;
    }
}
```

XML doc comments (`/// <summary>`) on every public type / method (`<GenerateDocumentationFile>true</GenerateDocumentationFile>` is on per `CLAUDE.md` §Code Style).

---

### Public DI ctor + internal test ctor

**Source:** `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]` in `DeckFlow.Web/AssemblyInfo.cs:3`. Pattern used in `AdminBruteForceTrackerStore` (three public ctors: `(string)`, `(RelationalDatabaseConnection)`, `(IWebHostEnvironment)`), `FeedbackStore` (same three), and `CardLookupService` (per `CLAUDE.md` §HTTP Conventions, with `Func<...>` test seam delegate).

**Apply to:** `FeatureFlagStore`, `FeatureFlagCache`. For `FeatureFlagStore` use the same triple-ctor (string sqlite path / `RelationalDatabaseConnection` / `IWebHostEnvironment`) so tests can inject an in-memory SQLite path or a fake `RelationalDatabaseConnection`. For `FeatureFlagCache` add an `internal` ctor accepting an `IFeatureFlagStore` only (skip the hosted-service plumbing) so unit tests can drive `ReloadAsync`/`IsEnabled`/`Snapshot` synchronously.

---

### Antiforgery on all admin POST forms (ADMIN-05)

**Source:** `Views/AdminFeedback/Index.cshtml:74-77`, `Detail.cshtml:27-43`, controller decorator `AdminFeedbackController.cs:69`.

**Apply to:** every new admin POST form (`Views/AdminFlags/Index.cshtml` toggle form) AND every new admin controller POST action (`AdminFlagsController.Toggle`).

Form side:
```cshtml
<form method="post" asp-action="Toggle" asp-route-key="@flag.Key" class="admin-action-form">
    @Html.AntiForgeryToken()
    <input type="hidden" name="enabled" value="@(!flag.Enabled)" />
    <button type="submit">@(flag.Enabled ? "Disable" : "Enable")</button>
</form>
```

Controller side:
```csharp
[HttpPost("{key}/toggle")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Toggle(string key, bool enabled) { ... }
```

---

### Structured Serilog logging — named placeholders only

**Source:** `BasicAuthMiddleware.cs:45-47`, `ScryfallTaggerService.cs:151-153, 265-267`, `ArchidektCacheJobService.cs:138`.

**Apply to:** every new `_logger.Log*` call.

```csharp
_logger.LogWarning(
    "FeatureFlag.MissingKey {Key} queried; defaulting to enabled=true (D-13). First miss only - logged once per process.",
    key);
```
**Never** use string interpolation: `_logger.LogWarning($"...{key}...")` is forbidden by `CLAUDE.md` §Logging.

For D-13 dedupe-once-per-key, gate on `_warnedMissing.TryAdd(key, 0)` before logging.

---

### Constructor argument validation

**Source:** `BasicAuthMiddleware.cs:23-26`, `ScryfallTaggerService.cs:70-73`, `AdminBruteForceTrackerStore.cs:55`.

**Apply to:** every new service / middleware / filter constructor.

```csharp
public FeatureFlagCache(IFeatureFlagStore store, ILogger<FeatureFlagCache> logger)
{
    ArgumentNullException.ThrowIfNull(store);
    ArgumentNullException.ThrowIfNull(logger);
    _store = store;
    _logger = logger;
}
```

---

### Sealed records for immutable view-models / DTOs

**Source:** `AdminFeedbackController.cs:14-25` (`AdminFeedbackListViewModel` — `init` props), `ArchidektCacheJobService.cs:21-30` (`sealed record ArchidektCacheJobStatus`).

**Apply to:** `MaintenanceViewModel` (use `sealed class` with `init` props or `sealed record` — pick `sealed class` to mirror `AdminFeedbackListViewModel` shape so view models look uniform across admin).

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `Infrastructure/FeatureFlagGateAttribute.cs` | action filter / attribute | request-response (gate) | Codebase has zero `IActionFilter`, `IAsyncActionFilter`, or `ActionFilterAttribute` implementations. `BasicAuthMiddleware` uses the middleware pipeline (different abstraction). Planner uses the `IAsyncActionFilter` skeleton in this PATTERNS doc plus the `BasicAuthMiddleware` short-circuit semantics; everything else is net-new MVC plumbing. Recommend a unit test that mocks `IFeatureFlagCache` and asserts `context.Result` is set to a `ViewResult` with `ViewName = "_MaintenancePage"` when the flag is off. |

---

## Metadata

**Analog search scope:**
- `DeckFlow.Web/Controllers/`, `Controllers/Admin/`, `Controllers/Api/`
- `DeckFlow.Web/Services/`, `Services/Http/`, `Services/FeatureFlags/` (new)
- `DeckFlow.Web/Infrastructure/`, `DeckFlow.Web/Security/`
- `DeckFlow.Web/Views/Shared/`, `Views/AdminFeedback/`
- `DeckFlow.Core/Storage/`

**Files scanned:** 18 source files read in full or targeted; `find`/`grep` swept ~120 candidates.

**Pattern extraction date:** 2026-05-02
