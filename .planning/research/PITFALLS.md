# Pitfalls Research

**Domain:** Admin console additions (harvest controls, analytics middleware, feature flags, admin sidebar) on live ASP.NET 10 / Razor / Postgres / Render
**Researched:** 2026-05-02
**Confidence:** HIGH — based on direct codebase reading + v1.0 post-mortem evidence

---

## Critical Pitfalls

### A1: Guild theme CSS leaking into admin pages

**What goes wrong:**
Admin pages inherit the public-facing `_Layout.cshtml` or `site.css` / `site-{guild}.css` tokens. Every guild theme is a full standalone CSS fork, so admin views end up with guild-coloured panels, link colours, and focus rings — making the admin console look like a themed public page rather than a neutral ops tool.

**Why it happens:**
New controllers default to `_Layout.cshtml` unless a `Layout` directive or separate `_AdminLayout.cshtml` is explicitly declared. `site-common.css` ships layout variables to every page, so even a "clean" admin layout inherits `--panel`, `--line`, `--accent-strong`, and all guild-overridden tokens.

**How to avoid:**
Create `Views/Shared/_AdminLayout.cshtml` that loads only `site-common.css` plus a single `admin.css` (neutral palette, no guild variables). Every admin view must open with `@{ Layout = "_AdminLayout"; }`. Do not reference `site.css` in the admin layout at all. Admin-specific structural CSS goes in `site-common.css` only if it is genuinely shared; otherwise it belongs in `admin.css`.

**Warning signs:**
- Admin page uses the same coloured header bar as the public deck tools.
- `--accent-strong` resolves to guild-specific hue (red on Rakdos themes, blue on Dimir, etc.) on admin form buttons.
- `<link rel="stylesheet">` in admin page source points to `site-{anything}.css`.

**Phase to address:**
Sidebar Shell phase (first admin phase). Catch it by rendering `/Admin` against three different guild themes on a local build and confirming the colour palette stays neutral.

---

### A2: Sidebar links routing to unauthorised or non-existent actions

**What goes wrong:**
A sidebar link is added pointing to `/Admin/harvest`, `/Admin/analytics`, or `/Admin/flags` before the corresponding controller action exists. The `UseWhen` BasicAuth branch in `Program.cs` (line 330–332) fires for all `/Admin/*` paths — so the 404 comes back inside the auth challenge, leaking information about which admin routes exist vs. don't, and potentially returning a guild-themed 404 page through the admin shell.

**Why it happens:**
Sidebar is built in Phase 1 as a shell; the linked pages are built in subsequent phases. Stub actions (returning `NotFound`) are easy to forget, or the route convention returns a public 404 view instead of an admin-scoped one.

**How to avoid:**
At sidebar build time, every linked action must exist and return at least a minimal `View()` (placeholder "coming soon" is fine). Admin 404s must render through `_AdminLayout`, not the public exception handler. Add a smoke test: after each phase deploy, manually GET every sidebar link and confirm it returns 200 with admin chrome.

**Warning signs:**
- Sidebar link returns a guild-themed error page.
- 404 response lacks `WWW-Authenticate` header (means auth gate fired, passed, then fell through to public error handler).

**Phase to address:**
Sidebar Shell phase. Stub all phase-2+ routes as placeholder actions during Phase 1.

---

### A3: BasicAuth gate bypass via middleware order

**What goes wrong:**
`app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/Admin"), ...)` runs after `app.UseRouting()` and `app.UseRateLimiter()` (Program.cs lines 314, 328, 330). If a new analytics or flags API endpoint under `/Admin/api/` is added but returns JSON (not HTML), a developer might use `[AllowAnonymous]` or a different route prefix to avoid the 401 challenge loop — accidentally leaving it unguarded.

**Why it happens:**
The `UseWhen` branch protects all `/Admin` paths in the middleware pipeline, but attribute-level `[AllowAnonymous]` on a controller action bypasses it. The ASP.NET Core `UseAuthorization()` + `UseWhen` combo is not the same as `[Authorize]` — they are independent gates.

**How to avoid:**
Never apply `[AllowAnonymous]` to any action under an admin controller. Treat the path-based `UseWhen` as the only gate — it is. If an admin endpoint needs a different response format (JSON vs HTML), still let it flow through `BasicAuthMiddleware`. Add an integration smoke check: `curl -s http://localhost:5173/Admin/harvest` without auth headers must return 401.

**Warning signs:**
- Any admin controller action has `[AllowAnonymous]`.
- `curl -s localhost:5173/Admin/anything` returns 200 without credentials.

**Phase to address:**
Sidebar Shell phase. Add the curl-without-auth check to the phase verification checklist.

---

### B1: Orphaned background task after Render redeploy

**What goes wrong:**
A harvest job is running when a git push triggers a Render auto-deploy. Render sends `SIGTERM` to the old container. `BackgroundService.StopAsync` is called, which cancels `stoppingToken`. `ExecuteAsync` in `ArchidektCacheJobService` catches `OperationCanceledException when stoppingToken.IsCancellationRequested` and re-throws (line 133), causing a clean exit — BUT the in-progress `RunCacheSweepAsync` call may not honour the token if the underlying Archidekt HTTP calls have a separate timeout. If the HTTP client does not pass the cancellation token through, the old container process hangs past Render's 30-second drain window and is SIGKILL'd mid-write.

**Why it happens:**
`RunCacheSweepAsync` passes `stoppingToken` down the call chain, but `RestSharp` + `Polly` retry loops in the Archidekt importer have their own timeout budgets. If the retry policy is set to retry 3× at 2s wait, the loop can run 6+ seconds per card batch after `stoppingToken` fires, exceeding the drain window on a large sweep.

**How to avoid:**
Use `CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, jobCancellationToken)` for every job execution so both host-stop and operator-cancel signals terminate the same loop. Verify that `ArchidektApiDeckImporter` passes the token all the way to the `RestSharp` `ExecuteAsync` call (it currently uses legacy Polly `AsyncRetryPolicy` directly — CLAUDE.md architecture note). Add a `5-second graceful drain` expectation to Render `render.yaml` via `stopCommand: sleep 5`.

**Warning signs:**
- Render deploy log shows `Process exited with code 137` (SIGKILL, not SIGTERM clean).
- Last job in `_jobs` dictionary is stuck in `Running` state after redeploy (visible via the harvest stats endpoint if it reads from the singleton).

**Phase to address:**
Harvest Controls phase. Must be verified by starting a job and pushing a deploy while the job runs; confirm job state transitions to `Failed` (not stuck `Running`) within 30s.

---

### B2: Double-run when cron schedule fires concurrent with run-now

**What goes wrong:**
The cron scheduler fires at the scheduled time. Simultaneously, the admin hits "Run Now". `EnqueueAsync` checks `_activeJobId` under a `lock(_sync)` and returns the existing job if one is already Queued or Running. But if the cron fires first AND the Channel write completes before the lock check in the admin POST handler sees `_activeJobId` set, two jobs are written to the channel.

**Why it happens:**
The current `EnqueueAsync` implementation (lines 65–89) has the dedup guard inside the lock, but the cron scheduler (not yet written) will call `EnqueueAsync` from a different thread. The lock protects the dictionary and channel write atomically, so a true race is avoided — but the cron implementation must call the same `EnqueueAsync` method and not bypass the lock by writing directly to `_queue.Writer`.

**How to avoid:**
Cron scheduler must always call `IArchidektCacheJobService.EnqueueAsync(duration)` — never write to `_queue.Writer` directly. The existing lock-guarded dedup in `EnqueueAsync` already handles the double-fire case correctly. Test: call `EnqueueAsync` twice concurrently from two threads; assert `StartedNewJob == false` on the second call.

**Warning signs:**
- Two `Running` entries appear in `_jobs` simultaneously (impossible with correct lock usage).
- Harvest stats page shows two concurrent `StartedUtc` values without a `CompletedUtc` between them.

**Phase to address:**
Harvest Controls phase (cron sub-task). Write the concurrency test before implementing cron.

---

### B3: Cancellation token not actually cancelling because HttpClient ignores it

**What goes wrong:**
The operator hits "Cancel Job" in the admin UI. The cancel handler signals a `CancellationToken`. The job loop picks it up within `ExecuteAsync`, but the active Archidekt HTTP call (via `ArchidektApiDeckImporter` using legacy `AsyncRetryPolicy` — CLAUDE.md note) does not pass the token to `RestSharp`'s `ExecuteAsync`. The current request completes. The next iteration checks cancellation and stops — but the user sees "Cancel" → 5-second delay → finally stopped. If a retry is in progress, the delay is longer.

**Why it happens:**
`ArchidektApiDeckImporter` uses legacy Polly `AsyncRetryPolicy` directly (CLAUDE.md: "DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs still uses legacy Polly AsyncRetryPolicy directly"). Legacy `AsyncRetryPolicy.ExecuteAsync` does accept a `CancellationToken` parameter; if the importer omits it, retries will complete even after cancellation is requested.

**How to avoid:**
Audit `ArchidektApiDeckImporter` to confirm the `CancellationToken` is threaded into both the `AsyncRetryPolicy.ExecuteAsync` call and the inner `RestClient.ExecuteAsync` call. If not, fix as part of the Harvest Controls phase. This is a pre-condition for the cancel button being reliable.

**Warning signs:**
- After hitting Cancel, the "Active Job" in the UI stays Running for more than the expected per-request timeout (>10s).
- `Serilog` request log shows Archidekt calls continuing after `stoppingToken` fires.

**Phase to address:**
Harvest Controls phase, specifically the cancel sub-task. Verify by starting a job and hitting cancel; the job must reach `Failed` or `Succeeded` state within one HTTP timeout budget.

---

### B4: Cron timezone foot-gun (UTC vs server local)

**What goes wrong:**
The admin enters a cron schedule like `0 2 * * *` expecting "2 AM local time". The server runs UTC. Render containers run UTC. The schedule fires at 2 AM UTC = 8 PM or 9 PM local (MDT). The operator is confused when the harvest runs at unexpected times; in winter months the offset shifts by an hour if the operator uses MDT/MST.

**Why it happens:**
`System.TimeZoneInfo` on Linux uses the TZ environment variable or `/etc/localtime`. Render containers default to UTC. A cron library that calls `DateTime.Now` instead of `DateTime.UtcNow` will use UTC regardless, but the operator thinks local.

**How to avoid:**
Treat all schedule input/output as UTC. Label every cron time field in the admin UI with "(UTC)" explicitly. Display "Next run: {time} UTC" on the harvest stats page. Log schedule fires with UTC timestamps. Use `DateTimeOffset.UtcNow` everywhere in the scheduler, never `DateTime.Now`.

**Warning signs:**
- Harvest stats "Next run" time matches an unexpected local clock time.
- Job history timestamps jump by 6–7 hours seasonally (DST artifact).

**Phase to address:**
Harvest Controls phase (cron sub-task). Verify by checking the "next run" display matches UTC expectation after setting a known schedule.

---

### B5: Schedule string injection from admin form

**What goes wrong:**
The cron schedule field is a freetext `<input>` that is stored in Postgres and later executed by the scheduler. If the stored value is not validated, an operator typo `0 2 * *` (only 4 fields) causes the scheduler to throw at runtime — or worse, a stored value `; DROP TABLE` causes a SQL injection if the value is interpolated into a query rather than parameterised.

**Why it happens:**
Admin forms feel "safe" because they are behind BasicAuth. But the BasicAuth gate protects against external attackers, not against operator mistakes or a compromised credential.

**How to avoid:**
Validate cron strings against the 5-field POSIX cron grammar on form POST before saving. Use a cron parser library (e.g. `Cronos` on NuGet) to parse and reject invalid strings with a clear error message. Store via parameterised query. Never interpolate the cron string into SQL or shell commands.

**Warning signs:**
- Schedule form accepts `0 2 * *` (4 fields) without error.
- Schedule change silently breaks next-run calculation (no error, no next-run time shown).

**Phase to address:**
Harvest Controls phase (cron sub-task). Server-side validation must fire before DB write; confirm with an invalid-string POST returning a form error.

---

### C1: High-cardinality route table blow-up in analytics

**What goes wrong:**
The analytics middleware logs `request.Path` as-is. Routes like `/lookup?q=Atraxa+%2C+Praetors%27+Voice` or `/api/deck/diff` with GUIDs in the query string create a unique row per call. Within weeks, the `page_views` table has millions of rows with near-unique "route" values. Aggregation queries become slow; the Postgres Basic-256mb instance (which has limited query planning RAM) struggles.

**Why it happens:**
`HttpContext.Request.Path` returns the literal path segment. Parameterised routes like `/help/{topic}` become `/help/combat` and `/help/mana-base` as separate rows instead of a single `/help/{topic}` bucket.

**How to avoid:**
Use `HttpContext.GetRouteTemplate()` (via `IEndpointFeature`) as the bucket key, not `Request.Path`. This returns `{controller}/{action}` or the template string, collapsing all parameter variations into one row. If `GetRouteTemplate()` returns null (static files, unmatched routes), bucket as `static` or `unmatched`. Cap the route key length at 100 characters before insert.

**Warning signs:**
- `SELECT COUNT(DISTINCT route) FROM page_views` returns thousands of rows after a week.
- Admin analytics page is slow (>2s) to load.
- Postgres `pg_stat_user_tables` shows `page_views` as the table with the most live tuples.

**Phase to address:**
Analytics Middleware phase. Verify on day one by loading several deck URLs and confirming the `route` column shows template strings, not literal paths.

---

### C2: Per-request synchronous DB write killing p95 latency

**What goes wrong:**
The analytics middleware `await`s a `INSERT INTO page_views` on every request, including the hot deck sync and card lookup paths. On the Render Starter tier with a Basic-256mb Postgres over a shared network link, each insert adds 5–20ms round-trip latency. p95 latency for a deck lookup with 5 Scryfall calls becomes measurably worse.

**Why it happens:**
Inserting analytics inline in the middleware is the obvious implementation path. It feels "safe" because it is async. But it still blocks the request completion until the DB round-trip finishes.

**How to avoid:**
Use a `Channel<AnalyticsEvent>` write-behind buffer (same pattern as `ArchidektCacheJobService`'s `_queue`). The middleware writes to the channel without awaiting. A singleton `IHostedService` drains the channel and batches inserts (e.g. every 5 seconds or 100 events). Cap the channel at 1000 unbuffered items to bound RAM (Render 512MB cap). Drop events on overflow rather than back-pressuring the request path.

**Warning signs:**
- `/sync` or `/lookup` p95 latency increases after analytics middleware is deployed.
- Serilog request log shows analytics-path requests are consistently 15–30ms slower than pre-analytics baseline.

**Phase to address:**
Analytics Middleware phase. Verify by measuring p95 locally with `bombardier` or similar before/after adding the middleware; channel-buffered version must not regress.

---

### C3: Not skipping static asset routes

**What goes wrong:**
The analytics middleware records a page view for every CSS file, JS file, favicon, and font request. `/wwwroot/css/site-common.css` generates dozens of "page view" events per page load. The `page_views` table fills rapidly with static asset noise; unique-IP counts are inflated; per-page counts are meaningless.

**Why it happens:**
`app.UseStaticFiles()` runs before `app.UseRouting()` (Program.cs line 313 vs 314) and short-circuits before reaching later middleware — BUT only for matched static files. If the analytics middleware is inserted after `UseStaticFiles`, it will never see static-file requests. If it is inserted before, it will see everything.

**How to avoid:**
Place the analytics middleware after `UseStaticFiles()` and after `UseRouting()` so only routed controller requests are counted. As a belt-and-suspenders guard, add an explicit path prefix check: skip any path starting with `/css/`, `/js/`, `/lib/`, `/extensions/`, or lacking a route template.

**Warning signs:**
- `SELECT route, COUNT(*) FROM page_views GROUP BY route ORDER BY 2 DESC LIMIT 10` returns `static` or CSS/JS paths in top rows.
- Page view count for `/sync` is 30× what Serilog request logs show for that action.

**Phase to address:**
Analytics Middleware phase. Verify by loading a single page and checking no static-asset rows appear in `page_views`.

---

### C4: Unique-IP count blowing up Postgres index (storing raw IPs)

**What goes wrong:**
The analytics schema stores raw `CF-Connecting-IP` values per event row to enable unique-IP counting. After a week of normal traffic, the `page_views` table has thousands of distinct IP strings. A query like `SELECT COUNT(DISTINCT ip) FROM page_views WHERE route = '/sync' AND day = '2026-05-02'` requires a sequential scan of all rows for that day. On Basic-256mb Postgres, this is slow and memory-intensive.

**Why it happens:**
Storing raw IPs for analytics is the obvious approach but creates both a privacy problem (PII) and a performance problem.

**How to avoid:**
Store a SHA-256 hash of `CF-Connecting-IP + daily_salt` (same pattern as `FeedbackStore.HashIpInternal`). This gives unique-IP cardinality estimates without storing PII. For the index, store `(route, day)` as a composite index — this supports the primary filter. For unique-IP count, use a `COUNT(DISTINCT ip_hash)` on the filtered result set; the `(route, day)` index makes the filtered set small.

**Warning signs:**
- Analytics slow-query log shows `page_views` unique-IP queries taking >500ms.
- Raw IP addresses visible in admin analytics view (should never be displayed, only hashed).

**Phase to address:**
Analytics Middleware phase. Schema design decision: use `ip_hash` column, not `ip`. Verify no raw IP is written or displayed.

---

### D1: Flag check on hot path doing a DB round-trip per request

**What goes wrong:**
`IFeatureFlagService.IsEnabledAsync("tagger-enabled")` is called from `ScryfallTaggerService.GetTagsAsync` on every card lookup. Each call hits Postgres. On a deck with 100 cards, that is 100 flag-check DB round-trips per lookup. The Render Starter + Basic-256mb Postgres network hop is 5–20ms; 100 × 15ms = 1.5s of pure flag-check overhead per lookup.

**Why it happens:**
The feature flag interface looks like a simple `IsEnabled(name)` call. Devs don't think of it as a DB query. The first implementation goes straight to the DB because it's the simplest thing that works.

**How to avoid:**
Cache all flags in `IMemoryCache` on first load with a 30-second sliding TTL. Flag reads go to cache; the admin write-path invalidates the cache entry for the changed flag. Never hit the DB inside a per-card or per-request inner loop. The `IMemoryCache` is already registered as a singleton in `Program.cs` (line 59).

**Warning signs:**
- Serilog shows `SELECT value FROM feature_flags WHERE name = 'tagger-enabled'` in the query log 100 times per deck lookup.
- Card lookup p95 regresses by 1–2 seconds after flags phase ships.

**Phase to address:**
Feature Flags phase. Cache must be part of the initial design, not a follow-up optimisation.

---

### D2: Stale cache after admin writes a new flag value

**What goes wrong:**
The admin sets `tagger-enabled = false` via `/Admin/flags`. The flag store writes to Postgres. The in-process `IMemoryCache` still holds the old `true` value for up to 30 seconds (or until TTL expires). The Tagger keeps running for half a minute after the operator thinks they killed it.

**Why it happens:**
Memory cache write-through invalidation is easy to forget. The write path (admin POST) and the read path (service call) are in different classes.

**How to avoid:**
After every flag write in the admin controller, call `_memoryCache.Remove(FlagCacheKey(name))` before returning. This forces the next read to go to Postgres and re-populate the cache. Do not wait for TTL expiry. Document this invariant in an inline comment on the flag store's `SetAsync` method.

**Warning signs:**
- After setting a flag to `false` in the admin UI, the affected feature still activates within the next 30 seconds.
- Flag cache key is not removed in the admin POST handler (visible in code review).

**Phase to address:**
Feature Flags phase. Verify by: (1) enabling a flag, (2) refreshing a public page to confirm it's on, (3) disabling via admin, (4) refreshing within 2 seconds — must be off immediately.

---

### D3: Accidental "default off" when flag table is empty

**What goes wrong:**
The `feature_flags` table is empty on first deploy (schema just created). `IsEnabled("tagger-enabled")` returns `false` because the row doesn't exist. The Tagger is now silently off in production despite the code being correct. The operator doesn't know to create the flag row.

**Why it happens:**
Flags are often designed as opt-in: missing row = disabled. This is safe for experimental features but destructive for features that shipped in a previous version (Tagger is live in v1.0).

**How to avoid:**
Distinguish between flag categories. For kill-switches on existing features, the default when the row is missing must be `true` (enabled). Only features gated as "beta" should default to `false` on missing row. Encode the default in the flag name or in a `default_value` column. On `EnsureSchemaAsync`, seed known kill-switch flags with `INSERT ... ON CONFLICT DO NOTHING` so the row always exists. Document the default clearly in the admin flags UI.

**Warning signs:**
- After a fresh deploy (e.g. new Postgres instance), Tagger returns empty tags for all cards.
- `/Admin/flags` shows no rows immediately after first deploy.

**Phase to address:**
Feature Flags phase. `EnsureSchemaAsync` must include seed rows for all kill-switch flags. Verify by running against a fresh DB and confirming Tagger still works without manual flag insertion.

---

### D4: Type confusion — bool stored as TEXT, read as string

**What goes wrong:**
Postgres schema stores flag values as `TEXT` (`'true'` / `'false'`). The flag reader does `reader.GetBoolean(0)` — this throws `InvalidCastException` on Postgres (which returns a `string`, not a `bool`). The app crashes on first flag read. Alternatively, the reader uses `bool.Parse(reader.GetString(0))` which throws if someone typed `'True'` or `'1'` in the admin UI.

**Why it happens:**
The project already uses `TEXT` for enum-like values (feedback status, type). Flags look similar. Developers mirror the feedback pattern but forget flags are consumed as booleans.

**How to avoid:**
Store flag enabled state as `BOOLEAN` in Postgres (which is a native type) and `INTEGER` in SQLite (which has no BOOLEAN). Use `IRelationalDialect` column-type substitution (same pattern as `__ID_COLUMN_TYPE__` in `FeedbackStore`). Read with `Convert.ToBoolean(reader.GetValue(0))` which handles both `bool` (Postgres) and `int` (SQLite). Never parse string representations.

**Warning signs:**
- Flag read throws `InvalidCastException` on first production deploy.
- Admin UI accepts `'yes'` or `'1'` as valid flag values.

**Phase to address:**
Feature Flags phase. Must test against both SQLite (local) and Postgres (push-and-watch CI) before marking the phase complete.

---

### D5: Flag flip mid-request causing partial execution

**What goes wrong:**
A long-running deck analysis request checks `IsEnabled("tagger-enabled")` at the start and gets `true`. Midway through fetching 100 card tags, the admin flips the flag to `false` and the cache is invalidated. The next iteration checks the flag again, gets `false`, and aborts the loop. The user gets a partial result — some cards have tags, most don't. No error is shown; the prompt artifact silently has incomplete tag data.

**Why it happens:**
Checking flags inside a loop (per-card iteration) means the flag state can change between iterations.

**How to avoid:**
Read the flag once at the start of the request or service call, store in a local variable, and use that variable for the entire operation. Never re-read the flag inside a loop. The cached value from `IMemoryCache` is consistent for the duration of the cache entry anyway, so a per-request read is also correct — but the pattern of checking inside the loop is the hazard.

**Warning signs:**
- Flag is checked inside `foreach (var card in cards)` rather than before the loop.
- Users report partial tag results on decks analysed during a flag change.

**Phase to address:**
Feature Flags phase. Code review check: no flag read inside a loop body.

---

### E1: EnsureSchemaAsync timing — new tables not created before startup validation

**What goes wrong:**
`ValidateDatabaseConnectionsAsync` in `Program.cs` (lines 421–438) runs at startup and calls `CountAsync` on `FeedbackStore` and `GetProcessedDeckCountAsync` on `CategoryKnowledgeStore`. Both trigger `EnsureSchemaAsync` which creates tables if missing. New stores (analytics, flags) added in v1.1 must also be included in this validation — or their schemas are only created on first request, creating a race window where the first real user request triggers the schema migration.

**Why it happens:**
New stores are registered in DI but not added to `ValidateDatabaseConnectionsAsync`. The schema-on-first-use pattern in `FeedbackStore` is lazy; startup validation turns it eager.

**How to avoid:**
For every new store added in v1.1, add a corresponding `EnsureSchemaAsync` call (or a minimal read call that triggers it) inside `ValidateDatabaseConnectionsAsync`. This ensures all schemas are created at deploy time, not on first request. Keep the pattern: non-Dev environments validate; Development environments skip (so local SQLite is still lazy).

**Warning signs:**
- First request after deploy to a fresh Postgres instance returns a 500 error.
- `ValidateDatabaseConnectionsAsync` log line appears but does not mention the new store.

**Phase to address:**
Whichever phase introduces the first new store (Analytics or Flags, whichever ships first). Must be in the startup validation call before the phase is marked complete.

---

### E2: SQLite-vs-Postgres divergence in new table SQL (known project pattern)

**What goes wrong:**
New tables for `page_views` or `feature_flags` use SQL patterns that SQLite accepts but Postgres rejects. The two confirmed project patterns (from `feedback_sqlite_postgres_sql_divergence.md`):
1. `SELECT EXISTS(...)` cast to long: Postgres returns `bool`, not `0/1`.
2. `INSERT ... ON CONFLICT DO UPDATE SET col = col + excluded.col`: Postgres rejects ambiguous bare column reference; must be `tablename.col + excluded.col`.

New SQL for analytics upserts (incrementing page view counts) will almost certainly use pattern 2.

**Why it happens:**
SQLite is used for local dev; the divergence only manifests on Postgres. Static analysis and `dotnet build` do not catch SQL syntax errors. VSTest is unreliable in WSL (project constraint), so SQL integration tests may not run before push.

**How to avoid:**
For every new upsert in analytics or flags schema: (1) qualify ambiguous columns with table name, (2) use `COUNT(1)` not `EXISTS()` for existence checks. Run the Postgres integration test suite (`DECKFLOW_POSTGRES_TESTS=1`) against the new SQL before merging. If Docker is not available locally, push to a feature branch and watch CI against the Render Postgres instance.

**Warning signs:**
- Local tests pass; Render deploy log shows `42702: column reference is ambiguous` or `42804: cannot convert bool to integer`.
- The new store's `EnsureSchemaAsync` succeeds locally (SQLite) but throws on first Render deploy.

**Phase to address:**
Every phase that introduces new SQL. Not a one-time fix — each SQL addition must be checked independently.

---

### E3: No rollback path when migration fails on Render

**What goes wrong:**
A new `ALTER TABLE` or `CREATE INDEX` fails on the live Render Postgres instance mid-deploy. The new code expects the new column; the old schema is still in place. `EnsureSchemaAsync` uses `CREATE TABLE IF NOT EXISTS` which is idempotent — but `ALTER TABLE ADD COLUMN` is not guarded in the same way. The deploy fails, the rollback deploys old code against a partially-migrated schema, and the old code doesn't know about the new column.

**Why it happens:**
v1.0 used only `CREATE TABLE IF NOT EXISTS` — fully idempotent. v1.1 may need to add columns to existing tables (e.g. adding `ip_hash` or a cron-schedule column). `ALTER TABLE ADD COLUMN IF NOT EXISTS` is Postgres 9.6+ supported but easy to forget the `IF NOT EXISTS` guard.

**How to avoid:**
Prefer new tables over `ALTER TABLE` wherever possible. When a column must be added to an existing table, always use `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` (Postgres supports this). Test the migration by running `EnsureSchemaAsync` twice against the same Postgres instance and confirming the second run is a no-op (no error). Never use `DROP COLUMN` or destructive DDL in `EnsureSchemaAsync`.

**Warning signs:**
- `EnsureSchemaAsync` throws `42701: column already exists` on redeploy.
- Render deploy log shows a startup crash with a Postgres DDL error.

**Phase to address:**
Every phase with schema changes. Idempotency of DDL must be explicitly verified in the phase plan.

---

### F1: Admin form CSRF — SameOriginRequestValidator is API-only, not applied to admin form POSTs

**What goes wrong:**
`SameOriginRequestValidator` is called manually in API controllers (`DeckSyncApiController`, `SuggestionsApiController`). Razor form POSTs to admin controllers (`/Admin/harvest/run`, `/Admin/flags/set`) do not go through `SameOriginRequestValidator` and have no CSRF token (`[ValidateAntiForgeryToken]` attribute or equivalent). A CSRF attack can trick the operator's browser into submitting a harvest run or disabling a flag.

**Why it happens:**
`SameOriginRequestValidator` is a static helper, not middleware. It must be called explicitly. Admin form POSTs are new in v1.1; the pattern was not extended.

**How to avoid:**
Use ASP.NET Core's built-in `[ValidateAntiForgeryToken]` attribute on every admin POST action (it works with the existing `AddControllersWithViews()` registration and Razor's `@Html.AntiForgeryToken()` / `asp-antiforgery` form tag helper). This is the correct tool for Razor form POSTs; `SameOriginRequestValidator` is for JSON API endpoints. Do not mix the two patterns.

**Warning signs:**
- Admin POST actions lack `[ValidateAntiForgeryToken]`.
- Razor admin forms lack `@Html.AntiForgeryToken()` or `method="post"` with the `asp-antiforgery` tag helper.

**Phase to address:**
Sidebar Shell phase (establish the pattern in the first admin form). Every subsequent admin form POST inherits it.

---

### F2: Feature flag values as XSS vectors in admin display

**What goes wrong:**
Flag names and values are stored in Postgres and displayed in the `/Admin/flags` Razor view. If a flag name contains `<script>alert(1)</script>` (e.g. stored via a compromised admin credential), and the Razor view renders it with `@Html.Raw(flag.Name)`, the script executes in the admin's browser.

**Why it happens:**
Admin pages feel "trusted" so developers sometimes use `@Html.Raw()` for convenience, or forget that Razor's default `@flag.Name` encoding is the correct and sufficient protection.

**How to avoid:**
Never use `@Html.Raw()` for any user-supplied or database-sourced value in admin views. Razor's default `@` encoding is sufficient and correct. This is the same hardening principle already applied to the help content pipeline (`DisableHtml()` in Markdig).

**Warning signs:**
- Any Razor admin view contains `@Html.Raw(model.*)` on a DB-sourced field.
- Flag name or value field in the admin form does not sanitise on input.

**Phase to address:**
Feature Flags phase. Code review check: no `@Html.Raw` on flag name/value fields.

---

### F3: Brute-force throttle conflict — admin actions count toward the wrong partition

**What goes wrong:**
`BasicAuthMiddleware.ChallengeAsync` calls `_store.RecordFailureAsync(partitionKey, ...)` where `partitionKey` uses the `admin:` namespace from `DeriveAdminPartitionKey`. If analytics or flags endpoints are added under `/Admin` and make outbound HTTP calls (e.g. fetching metagame data for analytics), a 401 response from an upstream service does not count toward the admin brute-force bucket — but it could confuse log-based monitoring that treats all 401s on `/Admin/*` as brute-force signals.

**Why it happens:**
The brute-force throttle only increments on `BasicAuthMiddleware.ChallengeAsync`. Upstream 401s from Scryfall or Archidekt calls made inside admin actions are unrelated. The risk is not code correctness but operational confusion — an alert on "admin 401s" would fire falsely.

**How to avoid:**
This is a monitoring discipline issue, not a code correctness issue. Document in the admin analytics phase that "401 on `/Admin/*`" in Serilog means either (a) auth challenge or (b) upstream service 401 propagated through an admin action. Do not create a blanket 401 rate alert on the admin path without distinguishing the source.

**Warning signs:**
- Unexpected 429 responses on admin actions after a sustained Archidekt harvest (upstream rate-limit 401s accumulating in the wrong bucket) — this would only happen if `RecordFailureAsync` is called outside of `ChallengeAsync`, which the current code does not do.

**Phase to address:**
Analytics phase (document the distinction in the analytics schema). Not a code fix — an operational note.

---

### G1: "Passes static checks but fails live" — the Phase 4 trap

**What goes wrong:**
This is the exact failure mode from v1.0 Phase 4 (see `04-ABANDONED.md`). Phase 4 shipped two fixes that passed `dotnet build`, passed unit tests, and passed code review — but both were wrong in production. The Tagger fix failed because the manual cookie replay path was broken under RestSharp 114 + redirect-disabled config. The admin throttle fix failed because `Connection.RemoteIpAddress` fragmented under multi-proxy. Neither failure was detectable without prod traffic.

For v1.1, the same trap applies to:
- Analytics middleware: does it correctly read `CF-Connecting-IP` from headers on Render (behind Cloudflare) or does it fall back to "unknown" for every request?
- Feature flags cache invalidation: does the `IMemoryCache.Remove` call in the admin POST actually invalidate the in-process cache, or is a singleton scope issue causing the wrong instance?
- Harvest cancel: does the cancellation token actually propagate through the Archidekt importer's legacy Polly retry loop, or only through the outer `BackgroundService` loop?

**Why it happens:**
Static analysis and unit tests cover the code path but not the deployment environment (Cloudflare headers, multi-proxy chain, container restart, Postgres network latency).

**How to avoid:**
For every v1.1 feature, define a **live verification step** (not just a unit test) that must pass on the actual Render deployment before the phase is marked complete. Specific checks:

1. **Analytics IP:** Immediately after deploying analytics middleware, query `SELECT DISTINCT ip_hash FROM page_views LIMIT 5`. If all rows show the same hash (or the "unknown" fallback hash), CF-Connecting-IP is not being read. Compare with the expected hash of your own IP.

2. **Flags cache invalidation:** Deploy flags, enable a flag, load a public page (confirm feature is on), then disable the flag via admin — reload the public page within 2 seconds and confirm the feature is off. A 30-second wait does not prove invalidation; an immediate reload does.

3. **Harvest cancel:** Start a harvest job, let it run for 10 seconds, hit cancel. Watch Serilog on Render dashboard. Confirm the job transitions to `Failed` within one HTTP timeout budget (not 30–60 seconds).

4. **Cron schedule:** Set a schedule 2 minutes in the future (UTC), wait, confirm the harvest fires within 60 seconds of the scheduled time.

These four checks cannot be replaced by unit tests because they depend on Cloudflare, Render container behaviour, and in-process singleton state under real traffic.

**Warning signs:**
- Phase plan has no live-verification step (only `dotnet build clean` + unit test check).
- Live verification is listed as "post-deploy UAT" rather than a hard success criterion.

**Phase to address:**
Every phase. Live verification is a mandatory success criterion, not an optional UAT. The lesson from Phase 4: if the live check is not defined before coding starts, it will not be done after coding ends.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Inline analytics DB write per request | Simple, no extra service | p95 latency regression on hot paths; back-pressure on Postgres | Never — always use write-behind channel |
| Flag check via DB on every call | No cache to invalidate | 100× DB round-trips per deck lookup | Never on a hot path |
| Raw IP in analytics table | Easy to query unique-IPs | PII storage; Postgres index bloat | Never — always hash |
| Guild theme in admin layout | Zero extra CSS work | Admin looks like a public page; confusion for operator | Never |
| `[AllowAnonymous]` on admin action | Avoids 401 loop during dev | Admin endpoint unguarded in prod | Never — use test credentials |
| `ALTER TABLE` without `IF NOT EXISTS` | Simpler DDL | Idempotency broken; deploy crash on redeploy | Never in `EnsureSchemaAsync` |
| `@Html.Raw()` on DB-sourced flag names | Renders HTML decorations | XSS via compromised flag row | Never |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Cloudflare + analytics middleware | Read `HttpContext.Connection.RemoteIpAddress` for unique-IP | Read `CF-Connecting-IP` header (same as throttle + feedback patterns) |
| Postgres + feature flags bool | `GetBoolean()` on a TEXT column | Store as native BOOLEAN; read with `Convert.ToBoolean(GetValue(0))` |
| ArchidektCacheJobService cancel | Signal job-local CancellationToken, assume it propagates | Audit Archidekt importer's legacy Polly `AsyncRetryPolicy` to confirm token threading |
| IMemoryCache + flag invalidation | Rely on TTL expiry for admin-write visibility | Explicit `Remove(key)` on admin POST before returning |
| Cron schedule input | Accept any string, validate at fire time | Parse with `Cronos` on POST, reject before DB write |
| SameOriginRequestValidator + admin forms | Apply to Razor form POSTs | Use `[ValidateAntiForgeryToken]` for form POSTs; SameOriginRequestValidator is for JSON APIs only |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Per-request analytics INSERT | p95 latency increase ≥5ms | Write-behind Channel + batched insert | Immediately under any real traffic |
| Per-call flag DB read | Card lookup 1–2s slower per 100-card deck | Cache in IMemoryCache, 30s TTL | Any deck with >20 cards |
| High-cardinality route key | Analytics query >2s; table row count in millions | Use `GetRouteTemplate()` not `Request.Path` | After ~1 week of traffic |
| Harvest job history unbounded in-memory | `_jobs` ConcurrentDictionary grows forever | Cap at last N jobs (e.g. 100); evict oldest on insert | After ~1000 harvest runs |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| No antiforgery on admin form POSTs | CSRF — attacker triggers harvest or flag change via operator's browser | `[ValidateAntiForgeryToken]` on every admin POST action |
| `@Html.Raw()` on flag name/value | Stored XSS via compromised admin credential | Razor default `@` encoding; never Raw on DB-sourced fields |
| Raw IP in analytics store | PII violation; EU GDPR if any EU users | Hash with daily salt before storage |
| Secrets in cron schedule field | Operator types API key into schedule field thinking it is a different form | Clear label + cron grammar validation on POST |
| New admin route without auth | Any route added under `/Admin/*` is protected by path-prefix `UseWhen` — but a new controller mounted at `/AdminApi/*` would bypass it | All admin controllers must be under the `/Admin` path prefix |

---

## "Looks Done But Isn't" Checklist

- [ ] **Analytics middleware:** Verify `route` column contains template strings (`Deck/Index`), not literal paths (`/sync?q=...`). Check via `SELECT DISTINCT route FROM page_views LIMIT 20` after 5 minutes of use.
- [ ] **Analytics IP:** Verify `ip_hash` is not the hash of `"unknown"` (CF-Connecting-IP missing) for all rows. Query: `SELECT COUNT(*) FROM page_views WHERE ip_hash = '<hash_of_unknown>'`.
- [ ] **Feature flags:** Verify flag table has seed rows for all kill-switch flags on first fresh-DB deploy. Check by wiping the flags table and restarting the app.
- [ ] **Feature flag cache invalidation:** Verify disabling a flag via admin takes effect within 2 seconds on a public page (not 30 seconds). Manual browser test required.
- [ ] **Harvest cancel:** Verify job transitions to `Failed` within one HTTP timeout budget after operator cancels. Watch Render dashboard logs.
- [ ] **Admin layout:** Verify no guild CSS token is present in the admin page source. Check: load `/Admin` with a Rakdos-themed session; confirm `--accent-strong` is not a red hue.
- [ ] **Antiforgery:** Verify every admin form POST action has `[ValidateAntiForgeryToken]` and every admin Razor form has `asp-antiforgery="true"` (or `@Html.AntiForgeryToken()`).
- [ ] **Sidebar links:** Verify every sidebar link returns 200 (not 404) immediately after the Sidebar Shell phase deploy.
- [ ] **Static assets excluded from analytics:** Verify no CSS/JS/font rows in `page_views` after a page load. Query: `SELECT route FROM page_views WHERE route LIKE '%css%' OR route LIKE '%js%'`.
- [ ] **Schema idempotency:** Verify running `EnsureSchemaAsync` twice against the same Postgres instance produces no error and no duplicate rows.

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Guild CSS leaks into admin | LOW | Add `_AdminLayout.cshtml` with clean CSS; update all admin view `Layout` directives |
| Analytics table high-cardinality blow-up | MEDIUM | `TRUNCATE page_views`; redeploy with route-template bucketing fix; data loss is acceptable (analytics, not business data) |
| Feature flag default-off kills live feature | LOW | Insert flag row via Postgres admin console with `true` value; or add seed row in schema and redeploy |
| Harvest job stuck Running after cancel | LOW | Restart the Render service (triggers `stoppingToken`; job transitions to `Failed` on restart) |
| Schema migration fails on Render | MEDIUM | Connect to Render Postgres console; run the failed DDL manually with `IF NOT EXISTS` guard; redeploy |
| CSRF on admin form | MEDIUM | Add `[ValidateAntiForgeryToken]` + antiforgery token to form; redeploy; no DB changes needed |
| Phase 4 trap — static checks pass, live fails | HIGH | Follow the post-mortem pattern: define live verification criteria before coding; if live fails, abandon and replan (do not press forward as in Phase 4) |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| A1: Guild CSS leaks into admin | Sidebar Shell | Load `/Admin` across 3 guild themes; confirm neutral palette |
| A2: Sidebar links to non-existent routes | Sidebar Shell | GET every sidebar link; confirm 200 with admin chrome |
| A3: BasicAuth gate bypass | Sidebar Shell | `curl` every admin route without credentials; must return 401 |
| F1: Admin form CSRF | Sidebar Shell (first form) | Attempt cross-origin POST; confirm 400 AntiForgery error |
| B1: Orphaned task on redeploy | Harvest Controls | Start job, trigger deploy, confirm clean job exit |
| B2: Double-run race | Harvest Controls (cron) | Concurrent `EnqueueAsync` test; assert second returns false |
| B3: Cancel not propagating | Harvest Controls (cancel) | Start job, cancel, confirm transition within timeout |
| B4: Cron UTC foot-gun | Harvest Controls (cron) | Set schedule, verify "Next run" shows UTC; verify job fires at UTC time |
| B5: Schedule string injection | Harvest Controls (cron) | POST invalid cron string; confirm form error, no DB write |
| C1: High-cardinality route keys | Analytics Middleware | Query DISTINCT route after 5 min; confirm template strings |
| C2: Synchronous DB write per request | Analytics Middleware | p95 latency benchmark before/after; must not regress |
| C3: Static assets in analytics | Analytics Middleware | Check page_views after page load; confirm no CSS/JS rows |
| C4: Raw IP / PII in analytics | Analytics Middleware | Confirm ip_hash column, not ip; no raw IP in any query or view |
| D1: Flag check per-request DB hit | Feature Flags | Serilog query log; confirm single flag read per request group, not per card |
| D2: Stale cache after flag write | Feature Flags | Disable flag, reload public page within 2s; confirm off |
| D3: Default-off kills live feature | Feature Flags | Fresh DB test; confirm Tagger still works without manual row insert |
| D4: Bool/TEXT type confusion | Feature Flags | Deploy to Postgres (push-and-watch CI); confirm no cast exception |
| D5: Flag flip mid-request | Feature Flags | Code review: no flag read inside per-card loop |
| E1: New stores missing from startup validation | First new store phase | Startup log shows new store validated; no first-request 500 |
| E2: SQLite/Postgres SQL divergence | Every SQL phase | Run Postgres integration tests before merge |
| E3: No rollback path for migrations | Every DDL phase | Run `EnsureSchemaAsync` twice; confirm no error on second run |
| F2: XSS via flag names in admin view | Feature Flags | Code review: no `@Html.Raw` on DB-sourced fields |
| F3: Brute-force partition confusion | Analytics (documentation) | Operational note in phase plan |
| G1: Static checks pass, live fails | Every phase | Live verification step defined before coding; executed before phase close |

---

## Sources

- Direct codebase reading: `DeckFlow.Web/Program.cs`, `BasicAuthMiddleware.cs`, `SameOriginRequestValidator.cs`, `ArchidektCacheJobService.cs`, `FeedbackStore.cs`
- v1.0 post-mortem: `MILESTONES.md` Phase 4 abandonment record, `PROJECT.md` Key Decisions table
- Project memory: `feedback_sqlite_postgres_sql_divergence.md` (confirmed `EXISTS` cast + ambiguous upsert column patterns)
- Project memory observation 4026: "ArchidektCacheJobService: cancel and cron missing from IArchidektCacheJobService interface" — confirms cancel/cron gaps
- Project memory observation 3812: "ASP.NET Core RateLimiter Cannot Throttle on Auth Outcomes" — confirms auth throttle must live in middleware, not rate-limiter
- CLAUDE.md architecture note: "ArchidektApiDeckImporter.cs still uses legacy Polly AsyncRetryPolicy directly" — B3 cancellation risk source

---
*Pitfalls research for: v1.1 Admin Console additions on DeckFlow (ASP.NET 10 / Render / Postgres)*
*Researched: 2026-05-02*
