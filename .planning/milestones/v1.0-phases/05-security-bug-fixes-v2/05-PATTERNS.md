# Phase 5: Security & Bug Fixes v2 — Pattern Map

**Mapped:** 2026-05-02
**Files analyzed:** 8 (5 modified, 1 created, 2 modified-tests; 0 UI files in scope)
**Analogs found:** 8 / 8

## Phase-Wide Constraints

- **No UI files in scope.** This phase touches no `wwwroot/css/**`, no `wwwroot/ts/**`, no `Views/**`. Plan should NOT propose any visual changes. README operational note (per CONTEXT decision) is the only doc touched.
- **HTTP stack is pinned.** RestSharp 114 + Polly v8 named pipelines via `ResiliencePipelineProvider<string>`. Do NOT propose `Microsoft.Extensions.Http.Resilience`, do NOT propose direct `HttpClient`/`HttpClientHandler` usage in services. Cookie automation goes through the typed `ScryfallTaggerHttpClient`'s `SocketsHttpHandler`.
- **Postgres dialect uses `@param` prefix and `ON CONFLICT(...) DO UPDATE` UPSERT shape** — same as SQLite per dialect tables (see Shared Pattern: Relational Dialect Helpers below). No driver-specific parameter prefixes (e.g., `:` Npgsql, `?` Sqlite positional) — all DeckFlow stores use `@name`.

## File Classification

| New / Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Web/Services/ScryfallTaggerService.cs` (MODIFY) | service | request-response | `4db8b8a^:DeckFlow.Web/Services/ScryfallTaggerService.cs` (pre-migration) | exact |
| `DeckFlow.Web/Services/ScryfallTaggerHttpClient.cs` (MODIFY — 1-line touch in Program.cs handler config) | config | n/a | current `Program.cs:88-99` typed-client block | exact |
| `DeckFlow.Web/Services/AdminBruteForceTrackerStore.cs` (CREATE) | service / store | CRUD (UPSERT + SELECT) | `DeckFlow.Web/Services/FeedbackStore.cs` + `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` | role + flow match |
| `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` (MODIFY) | middleware | request-response | `50849e9:DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` (Phase 4-01, reverted) | exact |
| `DeckFlow.Web/Program.cs` (MODIFY — partition keys + 1-line UseCookies flip) | config / composition | n/a | current `Program.cs:88-99` (handler) + `Program.cs:128-149, 349-350` (forwarded headers + partition key) | exact |
| `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs` (MODIFY) | test | n/a | current file (drop expired tests, keep cards/named shape) + `4db8b8a^` reference (file did not exist pre-migration) | role-match |
| `DeckFlow.Web.Tests/Security/AdminBruteForceTrackerStoreTests.cs` (CREATE) | test | n/a | `7e08d8c:DeckFlow.Web.Tests/Security/AdminBruteForceTrackerTests.cs` (Phase 4-01, reverted) + `DeckFlow.Web.Tests/FeedbackStoreTests.cs` | role-match (combine both) |
| `README.md` (MODIFY — operational blurb) | docs | n/a | Phase 4 reverted blurb in commit `aed9ead` | exact |

---

## Pattern Assignments

### `DeckFlow.Web/Services/ScryfallTaggerService.cs` (MODIFY — service, request-response)

**Goal:** Restore the pre-migration cookie/printing-resolution shape but keep the typed-client + Polly + ScryfallThrottle + IMemoryCache + AsyncLocal-guarded 403 retry that the current file already does correctly. Drop manual cookie-replay (`BuildCookieHeader`/`StripCookieAttributes`/`AddHeader("Cookie", …)`); rely on the typed client's automatic `CookieContainer`.

**Primary analog:** `4db8b8a^:DeckFlow.Web/Services/ScryfallTaggerService.cs` (definitive pre-migration working shape — 167 LOC, working in production from project rename until 2026-04-27).

**Analog: `ResolveCardPrintingAsync` — single-printing cards/named shape (pre-migration `4db8b8a^:67-88`):**
```csharp
private async Task<(string Set, string CollectorNumber)> ResolveCardPrintingAsync(string cardName, CancellationToken cancellationToken)
{
    var request = new RestRequest("cards/named", Method.Get);
    request.AddQueryParameter("exact", cardName);

    var response = await ScryfallThrottle.ExecuteAsync(
        token => _scryfallClient.ExecuteAsync(request, token),
        cancellationToken);
    if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
    {
        _logger.LogWarning("Scryfall card lookup failed for {CardName}: {Status}", cardName, response.StatusCode);
        return (string.Empty, string.Empty);
    }

    using var document = JsonDocument.Parse(response.Content);
    var root = document.RootElement;

    var set = root.TryGetProperty("set", out var setProp) ? setProp.GetString() ?? string.Empty : string.Empty;
    var number = root.TryGetProperty("collector_number", out var numProp) ? numProp.GetString() ?? string.Empty : string.Empty;

    return (set, number);
}
```

**NOTE FOR PLANNER:** The *current* file (`bcc1693`) at `DeckFlow.Web/Services/ScryfallTaggerService.cs:123-148` already implements this shape correctly (single `cards/named?exact=` call, no iterate-printings loop, no sort by `released_at`). The Phase 4-02/4-03 iterate-printings code was reverted in `b3a8a5b`. **No change needed to `ResolveCardPrintingAsync` itself** — this file is already at the desired shape post-revert. Plan must only verify the iterate-printings + sort-ASC + negative-cache code is in fact gone (it is).

**Analog: cookie-replay code currently in service that needs DELETING (current `bcc1693:179-193`):**
```csharp
private static string BuildCookieHeader(RestResponse response)
{
    var setCookies = response.Headers?
        .Where(h => h.Name is not null && h.Name.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
        .Select(h => StripCookieAttributes(h.Value?.ToString() ?? string.Empty))
        .Where(v => !string.IsNullOrEmpty(v))
        .ToArray();
    return setCookies is { Length: > 0 } ? string.Join("; ", setCookies) : string.Empty;
}

private static string StripCookieAttributes(string setCookieValue)
{
    var semicolon = setCookieValue.IndexOf(';');
    return semicolon < 0 ? setCookieValue : setCookieValue[..semicolon];
}
```

**These two helpers go away.** They paper over `UseCookies = false`. Once the handler is flipped to `UseCookies = true`, .NET's `CookieContainer` keeps `_scryfall_tagger_session` between the page-GET and the GraphQL-POST automatically, and `graphqlRequest.AddHeader("Cookie", session.CookieHeader)` becomes both unnecessary and wrong (it would clobber the auto-managed cookie).

**Analog: `TaggerSession` record shape — drop `CookieHeader` field.** Current type (in `TaggerSessionCache.cs`) is `(CsrfToken, CookieHeader, CachedAt)`. Plan should reduce to `(CsrfToken, CachedAt)` — the cookie store lives in the handler now, not in the session record. (Planner: confirm `TaggerSessionCache.cs` in scope — only the type carrying these fields needs the prune.)

**Analog: post-fix `FetchTaggerSessionAsync` shape (synthesized from pre-migration `4db8b8a^:97-119` + current Polly wrapping `bcc1693:155-177`):**
```csharp
// After flipping UseCookies=true on the typed client's SocketsHttpHandler:
private async Task<TaggerSession?> FetchTaggerSessionAsync(string set, string collectorNumber, CancellationToken cancellationToken)
{
    var taggerRestClient = new RestClient(_taggerHttpClient.Inner);
    var pageRequest = new RestRequest($"card/{set}/{collectorNumber}", Method.Get);

    var pageResponse = await _taggerPipeline.ExecuteAsync(
        async ct => await taggerRestClient.ExecuteAsync(pageRequest, ct).ConfigureAwait(false),
        cancellationToken).ConfigureAwait(false);

    // ... existing logging templates per CONTEXT (Tagger.SessionFetch failed) ...

    var token = ScryfallTaggerParsers.TryExtractCsrfToken(pageResponse.Content);
    if (string.IsNullOrEmpty(token)) return null;

    // No cookie capture — handler's CookieContainer holds the cookie automatically and
    // replays it on the subsequent graphql POST through the same typed client.
    return new TaggerSession(token, DateTimeOffset.UtcNow);
}
```

**Analog: post-fix `ExecuteTaggerPostAsync` (current `bcc1693:227-248` minus the manual Cookie header):**
```csharp
private async Task<RestResponse> ExecuteTaggerPostAsync(
    string set, string collectorNumber, TaggerSession session, CancellationToken cancellationToken)
{
    var taggerRestClient = new RestClient(_taggerHttpClient.Inner);
    var graphqlRequest = new RestRequest("graphql", Method.Post);
    // DROP THIS LINE — handler now manages cookies automatically:
    //   graphqlRequest.AddHeader("Cookie", session.CookieHeader);
    graphqlRequest.AddHeader("X-CSRF-Token", session.CsrfToken);

    var payload = JsonSerializer.Serialize(new
    {
        query = TaggerQuery,
        variables = new { set, number = collectorNumber }
    });
    graphqlRequest.AddStringBody(payload, ContentType.Json);

    return await _taggerPostPipeline.ExecuteAsync(
        async ct => await taggerRestClient.ExecuteAsync(graphqlRequest, ct).ConfigureAwait(false),
        cancellationToken).ConfigureAwait(false);
}
```

**Keep unchanged from current file:**
- `LookupOracleTagsAsync` outer flow (lines 74-118)
- `_taggerSessionCache` cache-first / approaching-expiry branch
- `RefreshSessionAndRetryAsync` 403-retry path with `_attemptedRefresh` AsyncLocal guard (lines 256-296) — do NOT delete; CONTEXT keeps the 403 path and gives it its own log template.

**Logging templates to add (CONTEXT-mandated five templates, replacing the lump-warning at line 91):**
- `LogWarning("Tagger.Resolve failed for {CardName}: HTTP {StatusCode} in {ElapsedMs}ms", cardName, status, ms)`
- `LogWarning("Tagger.SessionFetch failed for {CardName} ({Set}/{Number}): HTTP {StatusCode} in {ElapsedMs}ms; csrf={CsrfPresent} cookies={CookieCount}", ...)` — note `cookies={CookieCount}` reads from the handler's `CookieContainer.Count` for the URI, not from the response; planner to wire correctly.
- `LogWarning("Tagger.GraphQlPost failed for {CardName} ({Set}/{Number}): HTTP {StatusCode} in {ElapsedMs}ms", ...)`
- `LogWarning("Tagger.Parse failed for {CardName}: {Reason}", cardName, reason)`
- `LogInformation("Tagger.Lookup succeeded for {CardName} in {ElapsedMs}ms returning {TagCount} tags", cardName, ms, tagCount)`
- Plus refresh-retry path: `LogWarning("Tagger.RefreshAndRetry triggered for {CardName} ({Set}/{Number}) after 403", ...)` — planner picks exact shape.

Use `Stopwatch.StartNew()` per LookupOracleTagsAsync invocation; record per-step elapsed for the four LogWarning sites. Use Serilog structured property names (PascalCase tokens) per project convention.

---

### `DeckFlow.Web/Services/ScryfallTaggerHttpClient.cs` (MODIFY — config / typed client wrapper)

**Goal:** No code change in this file. The handler config lives in `Program.cs`. The class itself is a thin `Inner` accessor and stays as-is.

**Analog: current `bcc1693:1-33` — keep verbatim.** The flip happens in Program.cs:88-99 (see below).

---

### `DeckFlow.Web/Program.cs` (MODIFY — composition root, two surgical edits)

**Goal:** (a) Flip `UseCookies = false` → `UseCookies = true`, `AllowAutoRedirect = false` → `true` on the Tagger handler. (b) Replace `DeriveFeedbackPartitionKey` (`Program.cs:349-350`) with shared `DeriveCloudflareClientIp(HttpContext)` helper used by both feedback-submit limiter and BasicAuth middleware.

**Analog (current state, line 88-99) — typed client handler config:**
```csharp
// Typed client for Tagger - cookie-disabled SocketsHttpHandler per D-06.
// HandlerLifetime = 5 min. TaggerSessionCache TTL = 270s (30s below HandlerLifetime)
// so session expiry races handler rotation with a safety margin (HIGH-2 fix).
builder.Services.AddHttpClient<ScryfallTaggerHttpClient>(c =>
{
    c.BaseAddress = new Uri("https://tagger.scryfall.com/");
    c.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    UseCookies = false,           // <— flip to true
    AllowAutoRedirect = false,    // <— flip to true
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
})
.SetHandlerLifetime(TimeSpan.FromMinutes(5));
```

**Update the comment to explain the cookie-handling decision:** "Typed client for Tagger - automatic cookie handling via CookieContainer per Phase 5 BUG-01 fix. The session+CSRF cookie set by GET /card/{set}/{num} is replayed automatically on the subsequent POST /graphql by the same handler, removing the need for manual Cookie header construction." Cite Phase 5 / BUG-01.

**Invariant to preserve (`Program.cs:86-87` comment + cache-config in `TaggerSessionCache`):**
- `TaggerSessionCache` TTL **270s** MUST remain strictly below `SetHandlerLifetime(5 min)`. The cookie-cycle now matters more, not less, after the flip — a stale session (CSRF+cookie pair) becomes invisible if the cookie outlives the handler. This is already correct, plan must NOT touch.

**Analog (current state, lines 128-149) — feedback rate limiter and partition key call site:**
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("feedback-submit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            DeriveFeedbackPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
```

**Analog (current state, lines 341-350) — partition key helper to REPLACE:**
```csharp
/// <summary>
/// Partition key for the feedback-submit rate limiter (TD-04 / Phase 03 SC #4,
/// retrieved 2026-04-30). Reads the immediate-peer IP directly. Render's edge collapses
/// all production traffic to a single partition - acceptable at DeckFlow's
/// expected feedback volume (well under 5/hr globally). Forwarded-header spoofing
/// cannot rotate this key. See DeckFlow.Web.Tests/Security/ForwardedHeadersOptionsTests.cs
/// for the invariant.
/// </summary>
internal static string DeriveFeedbackPartitionKey(HttpContext context)
    => "peer:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
```

**Replacement shape (CONTEXT-mandated):**
```csharp
/// <summary>
/// Reads the Cloudflare-injected real client IP. Configured-Render-only via
/// Inbound IP Rules allow-list (Cloudflare CIDRs); see README "Admin throttle"
/// section for spoof-prevention prerequisite. Returns "unknown" if header missing.
/// </summary>
internal static string DeriveCloudflareClientIp(HttpContext context)
{
    var raw = context.Request.Headers["CF-Connecting-IP"].ToString();
    if (string.IsNullOrWhiteSpace(raw))
    {
        // Log once per missing-header occurrence — log throttling left to planner.
        return "unknown";
    }
    return raw.Trim();
}

internal static string DeriveFeedbackPartitionKey(HttpContext context)
    => "feedback:" + DeriveCloudflareClientIp(context);

internal static string DeriveAdminPartitionKey(HttpContext context)
    => "admin:" + DeriveCloudflareClientIp(context);
```

The `peer:` prefix becomes `feedback:` to make the partition namespace explicit and disjoint from `admin:` — same store would NOT collide if reused, but the bucket prefix is the readable invariant.

**Wiring:** `BasicAuthMiddleware` registration at `Program.cs:288-290` is currently `branch.UseMiddleware<BasicAuthMiddleware>("DeckFlow Admin")`. Plan adds DI registration of `IAdminBruteForceTrackerStore` (singleton, like `IFeedbackStore`) at `Program.cs:115` area, and the middleware constructor takes the store + a function/HttpContext partition-key resolver. Keep the realm string positional arg.

---

### `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` (MODIFY — middleware, request-response)

**Goal:** Re-add throttle gate (429 with Retry-After before any auth parsing) and `RecordFailure` on `Challenge` 401, but persist via `IAdminBruteForceTrackerStore` (Postgres-backed) instead of the in-memory `IAdminBruteForceTracker` from Phase 4-01.

**Primary analog:** `50849e9:DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` (Phase 4-01 version, reverted in `b3a8a5b`).

**Analog: throttle gate at top of `InvokeAsync` (Phase 4-01 lines 31-46):**
```csharp
public async Task InvokeAsync(HttpContext context)
{
    // BUG-02 / D-02 — throttle gate before any auth parsing.
    var partitionKey = "admin:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    var now = DateTimeOffset.UtcNow;
    var (throttled, retryAfter) = _tracker.IsThrottled(partitionKey, now);
    if (throttled)
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        _logger.LogWarning(
            "Admin basic-auth throttled: {RemoteIp} retry after {RetryAfterSeconds}s",
            remoteIp, retryAfter);
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers["Retry-After"] = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return;
    }

    var user = Environment.GetEnvironmentVariable("FEEDBACK_ADMIN_USER");
    // ... rest as in current bcc1693 file ...
}
```

**Phase 5 update:** Replace `context.Connection.RemoteIpAddress?.ToString() ?? "unknown"` with a call to the shared `DeriveCloudflareClientIp(HttpContext)` (defined in Program.cs). The middleware needs that function reachable — either via an injected `Func<HttpContext, string>` or via making `Program.DeriveAdminPartitionKey` public-static and calling directly. Planner picks the shape.

`IsThrottled` and `RecordFailure` become **async** because the new store is Postgres-backed:
```csharp
var (throttled, retryAfter) = await _store.IsThrottledAsync(partitionKey, now, cancellationToken);
// ...
await _store.RecordFailureAsync(partitionKey, DateTimeOffset.UtcNow, cancellationToken);
```

The Phase 4-01 in-memory contract was synchronous `(bool, int) IsThrottled`; the Postgres-backed contract MUST be async. Update both the interface and the call sites accordingly. CONTEXT.md uses "(bool, int) IsThrottled(key, now)" loosely — interpret as "the same shape, but async because the store is Postgres".

**Analog: RecordFailure-on-Challenge wiring (Phase 4-01 lines 95-104):**
```csharp
private void Challenge(HttpContext context, string reason)
{
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    _logger.LogWarning("Admin basic-auth challenge issued: {Reason} from {RemoteIp}", reason, remoteIp);
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    context.Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{_realm}\", charset=\"UTF-8\"";
    // BUG-02 / D-01 — count only Challenge-emitted 401s (env-var 503 path bypasses this).
    var partitionKey = "admin:" + remoteIp;
    _tracker.RecordFailure(partitionKey, DateTimeOffset.UtcNow);
}
```

**KEY INVARIANT (do not regress):** `Challenge` is the single 401 emission point. `RecordFailure` is wired here, not at the env-var-503 path. The successful-auth path falls through to `_next(context)` and never calls `RecordFailure`. Phase 4-01's `SuccessfulAuthDoesNotCountTowardThrottle` test (line 108) asserts this — Phase 5 test must port that assertion.

**Phase 5 changes to Phase 4-01's Challenge:**
- `Challenge` becomes `async Task Challenge` (since `RecordFailureAsync` is async). Both call sites must `await Challenge(...)` then `return`.
- Use `DeriveCloudflareClientIp(context)` for the partition key, not `context.Connection.RemoteIpAddress`.

**Analog: `FixedTimeEquals` and rest of file (current `bcc1693:78-90` and Phase 4-01 `:106-117`)** — keep unchanged.

---

### `DeckFlow.Web/Services/AdminBruteForceTrackerStore.cs` (CREATE NEW — service / store, CRUD)

**Goal:** Postgres-backed throttle store with the contract `Task<(bool Throttled, int RetryAfterSeconds)> IsThrottledAsync(key, now, ct)` + `Task RecordFailureAsync(key, now, ct)`. Schema: single table `admin_brute_force_buckets(partition_key TEXT PRIMARY KEY, count INT NOT NULL, window_start TIMESTAMPTZ NOT NULL)`. Fixed window 10 attempts / 15 min, lazy expiry.

**Primary analogs:**
1. `DeckFlow.Web/Services/FeedbackStore.cs` — connection lifecycle, schema-init pattern, parameterized SQL via `RelationalDatabaseConnection.AddParameter`. Closest match for an ASP.NET-y store-with-injected-conn.
2. `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — UPSERT shape (`ON CONFLICT(...) DO UPDATE`), transaction-per-write pattern.

**Analog: ctor + connection wiring (FeedbackStore.cs:10-38):**
```csharp
public sealed class FeedbackStore : IFeedbackStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    public FeedbackStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath))
    {
    }

    public FeedbackStore(RelationalDatabaseConnection connectionInfo)
    {
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

    public FeedbackStore(IWebHostEnvironment environment)
        : this(DeckFlowDatabaseConnectionFactory.CreateFeedbackConnection(environment))
    {
    }
```

**Apply same triple-ctor pattern.** New store mirrors: `(string sqlitePath)` → `(RelationalDatabaseConnection)` → `(IWebHostEnvironment)` (using a new `DeckFlowDatabaseConnectionFactory.CreateAdminThrottleConnection(env)` factory if needed, or reuse the feedback connection factory — planner decides; CONTEXT defers Postgres dialect SQL to Claude's discretion).

**Analog: schema-init lazy gate (FeedbackStore.cs:236-286):**
```csharp
private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
{
    if (_schemaReady) return;
    await _schemaGate.WaitAsync(cancellationToken);
    try
    {
        if (_schemaReady) return;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using (var create = connection.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS feedback (
                  id           __ID_COLUMN_TYPE__,
                  ...
                );
                """;
            create.CommandText = create.CommandText
                .Replace("__ID_COLUMN_TYPE__", _connectionInfo.Dialect.FeedbackIdColumnType, StringComparison.Ordinal)
                .Replace("__CREATED_UTC_COLUMN_TYPE__", _connectionInfo.Dialect.FeedbackCreatedUtcColumnType, StringComparison.Ordinal);
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        _schemaReady = true;
    }
    finally
    {
        _schemaGate.Release();
    }
}
```

**Apply same gate.** For the `admin_brute_force_buckets` table, the schema is simpler — no autoincrement id, just a TEXT PK + INT + timestamp. The `__CREATED_UTC_COLUMN_TYPE__` placeholder pattern (`TIMESTAMPTZ` Postgres / `TEXT` Sqlite) reapplies if the planner wants Sqlite parity, OR the planner can skip Sqlite entirely (Render production is Postgres; SQLite path is only used in tests). Planner's choice — CONTEXT.md "Claude's Discretion" allows.

**Analog: UPSERT with `ON CONFLICT(...) DO UPDATE` (CategoryKnowledgeRepository.cs:355-377):**
```csharp
command.CommandText = """
    INSERT INTO card_category_observations (source, card_name, normalized_card_name, category, board, deck_count, count, last_seen_utc)
    VALUES (@source, @cardName, @normalizedCardName, @category, @board, @deckCount, @quantity, @lastSeenUtc)
    ON CONFLICT(source, normalized_card_name, category, board)
    DO UPDATE SET
        count = card_category_observations.count + excluded.count,
        deck_count = card_category_observations.deck_count + excluded.deck_count,
        card_name = excluded.card_name,
        last_seen_utc = excluded.last_seen_utc
    """;
RelationalDatabaseConnection.AddParameter(command, "@source", source);
// etc.
```

**Apply same shape for `RecordFailureAsync`:**
```csharp
INSERT INTO admin_brute_force_buckets (partition_key, count, window_start)
VALUES (@key, 1, @now)
ON CONFLICT(partition_key)
DO UPDATE SET
    count = CASE
        WHEN @now - admin_brute_force_buckets.window_start >= INTERVAL '15 minutes'
            THEN 1
        ELSE admin_brute_force_buckets.count + 1
    END,
    window_start = CASE
        WHEN @now - admin_brute_force_buckets.window_start >= INTERVAL '15 minutes'
            THEN @now
        ELSE admin_brute_force_buckets.window_start
    END;
```

**Postgres-vs-Sqlite divergence WARNING (per global memory feedback_sqlite_postgres_sql_divergence):**
- `INTERVAL '15 minutes'` is Postgres-only. SQLite path needs `(julianday(@now) - julianday(window_start)) * 86400 >= 900` or similar.
- Qualify upsert columns with table name (`admin_brute_force_buckets.window_start`), not bare `window_start`, to avoid the SQLite ambiguity that bit Phase 03's storage refactor.
- Prefer `COUNT(1)` over `EXISTS` per project memory.

**If planner chooses Postgres-only (acceptable per CONTEXT discretion):** add the `IRelationalDialect.AdminThrottleUpsertSql` method on the dialect interface (mirroring `FeedbackInsertReturningIdSql`), implement only on `PostgresRelationalDialect`, and have the SQLite implementation throw `NotSupportedException`. This matches the existing dialect pattern.

**Analog: read shape — IsThrottledAsync (FeedbackStore.cs:114-123 COUNT pattern):**
```csharp
command.CommandText = "SELECT count, window_start FROM admin_brute_force_buckets WHERE partition_key = @key";
RelationalDatabaseConnection.AddParameter(command, "@key", partitionKey);
await using var reader = await command.ExecuteReaderAsync(cancellationToken);
if (!await reader.ReadAsync(cancellationToken))
{
    return (false, 0);
}
var count = reader.GetInt32(0);
var windowStart = reader.GetFieldValue<DateTimeOffset>(1);
// ... apply Phase 4-01 in-memory tracker logic (lines 33-49):
//   if (now - windowStart >= 15min) return (false, 0);
//   if (count >= 10) return (true, remaining seconds);
//   return (false, 0);
```

The arithmetic block ports from Phase 4-01 `AdminBruteForceTracker.IsThrottled` (50849e9 lines 34-49) verbatim — only the storage swap differs.

**Lazy expiry in `RecordFailureAsync`:** The CASE expressions in the UPSERT do this atomically — if `now - window_start >= 15min`, the row resets to count=1, window_start=@now. No separate expiry SELECT needed.

**Type mapping — DateTimeOffset/TIMESTAMPTZ (FeedbackStore.cs:51 pattern):**
```csharp
RelationalDatabaseConnection.AddParameter(command, "@created", _connectionInfo.IsPostgres ? DateTime.UtcNow : DateTime.UtcNow.ToString("O"));
```
Postgres takes a `DateTime` (TIMESTAMPTZ binds via Npgsql), SQLite takes ISO string. Apply same dispatch for `@now` and `window_start`.

---

### `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs` (MODIFY — test, drop dead Phase 4-02 tests + repoint to single-printing flow)

**Goal:** The current file is already aligned with the cards/named single-printing flow (the iterate-printings tests were reverted with `b3a8a5b`). Plan must (a) verify no iterate-printings test survived, (b) add tests asserting the cookie-replay code is gone (i.e., `BuildCookieHeader`/`StripCookieAttributes` are no longer present), (c) keep the four current behaviors covered: cold-flow, warm-cache, csrf-expired, graphql-fails.

**Analog: current file `bcc1693:1-204` — already mostly correct.** The four existing tests stay. Plan changes:
- The `TaggerCsrfHtml` fixture and `Set-Cookie` header on the mocked CSRF route stay — the typed handler's `CookieContainer` will pick the cookie up from the response. Verify with MockHttp that the GraphQL POST mock receives the cookie back automatically. (MockHttpMessageHandler does NOT integrate with `CookieContainer` because it bypasses `SocketsHttpHandler` entirely, so the test must NOT assert cookie replay — it tests the service code paths only. Live UAT covers the actual cookie automation.)
- Plan should add **explicit assertion** that the GraphQL POST request is sent without `Cookie` header (i.e., the service no longer writes it manually). Use `MockHttpMessageHandler.When(...).WithHeaders` or capture-and-inspect via `.Respond(req => { ... assert no Cookie header ... return ...; })`.
- Drop `LookupOracleTagsAsync_CsrfExpired_RefetchesSession` if its semantics changed under the new shape; replace with `LookupOracleTagsAsync_GraphQl403_TriggersRefreshAndRetry` (current `RefreshSessionAndRetryAsync` path).

**Pre-migration test analog UNAVAILABLE:** `git show 4db8b8a^:DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs` returned `fatal: path ... exists on disk, but not in '4db8b8a^'`. The file was created post-migration (in the Phase 4 era). The test patterns are project-internal — use the current file and the `CommanderSpellbookServiceTests` (sibling) as MockHttp references.

**Sibling analog (DeckFlow.Web.Tests/Services/CommanderSpellbookServiceTests.cs):** Use for any cookie-header-absence assertion idioms (planner: open if needed). Same MockHttp shape as ScryfallTaggerServiceTests.

---

### `DeckFlow.Web.Tests/Security/AdminBruteForceTrackerStoreTests.cs` (CREATE NEW — test)

**Goal:** Cover the new Postgres-backed store: 10/15min window, lazy expiry, partition key isolation, plus middleware-integration test (11-burst → 10×401 + 1×429 with Retry-After). Mirror Phase 4-01's reverted test file shape, swapped to async + Sqlite/Testcontainers fixture.

**Primary analog 1 — pure tracker tests:** `7e08d8c:DeckFlow.Web.Tests/Security/AdminBruteForceTrackerTests.cs` (Phase 4-01, reverted in `b3a8a5b`).

**Analog: pure tracker test cases (lines 21-74):**
```csharp
[Fact]
public void RecordFailure_TenTimesUnderSameKey_EleventhCheckReturnsThrottled()
{
    var tracker = new AdminBruteForceTracker();
    var now = DateTimeOffset.UtcNow;
    for (var i = 0; i < 10; i++) tracker.RecordFailure("admin:10.0.0.1", now);
    var (throttled, retryAfter) = tracker.IsThrottled("admin:10.0.0.1", now);
    Assert.True(throttled);
    Assert.InRange(retryAfter, 1, 900);
}

[Fact]
public void IsThrottled_NinthFailure_StillNotThrottled()
{
    var tracker = new AdminBruteForceTracker();
    var now = DateTimeOffset.UtcNow;
    for (var i = 0; i < 9; i++) tracker.RecordFailure("admin:10.0.0.1", now);
    var (throttled, _) = tracker.IsThrottled("admin:10.0.0.1", now);
    Assert.False(throttled);
}

[Fact]
public void IsThrottled_DifferentKeys_DoNotInterfere()
{
    var tracker = new AdminBruteForceTracker();
    var now = DateTimeOffset.UtcNow;
    for (var i = 0; i < 10; i++) tracker.RecordFailure("admin:10.0.0.1", now);
    var (throttled, _) = tracker.IsThrottled("admin:10.0.0.2", now);
    Assert.False(throttled);
}

[Fact]
public void RecordFailure_AfterWindowExpiry_ResetsBucket()
{
    var tracker = new AdminBruteForceTracker();
    var t0 = DateTimeOffset.UtcNow;
    for (var i = 0; i < 10; i++) tracker.RecordFailure("admin:10.0.0.1", t0);
    var future = t0.AddMinutes(16);
    tracker.RecordFailure("admin:10.0.0.1", future);
    var (throttled, _) = tracker.IsThrottled("admin:10.0.0.1", future);
    Assert.False(throttled);
}

[Fact]
public void IsThrottled_ReturnsRemainingSecondsInWindow()
{
    var tracker = new AdminBruteForceTracker();
    var t0 = DateTimeOffset.UtcNow;
    for (var i = 0; i < 10; i++) tracker.RecordFailure("admin:10.0.0.1", t0);
    var t0Plus5 = t0.AddMinutes(5);
    var (throttled, retryAfter) = tracker.IsThrottled("admin:10.0.0.1", t0Plus5);
    Assert.True(throttled);
    Assert.InRange(retryAfter, 599, 601);
}
```

**Phase 5 changes:** All five tests port verbatim, but
- Type changes: `AdminBruteForceTracker` → `AdminBruteForceTrackerStore`
- All return values become `Task` and tests become `async Task`
- `tracker.RecordFailure(key, now)` → `await store.RecordFailureAsync(key, now)`
- `tracker.IsThrottled(...)` → `await store.IsThrottledAsync(key, now)`
- Tests need a real DB per test — use temp SQLite path (mirror `FeedbackStoreTests` ctor / Dispose pattern, see analog 2 below). Postgres dialect tests can be skipped in-process (no Testcontainers wiring in test project — see project memory `feedback_sqlite_postgres_sql_divergence`: Postgres integration tests are not yet runnable without infra additions, currently CONTEXT defers this).
  - Per CONTEXT "Claude's Discretion": planner picks Testcontainers.PostgreSql vs in-memory SQLite vs in-process FakeStore. Recommendation: **start with SQLite** (matches FeedbackStoreTests) AND defer Postgres-specific UPSERT verification to manual deploy + smoke test, since the project has shipped this exact way for FeedbackStore.

**Primary analog 2 — temp-DB lifecycle for relational test fixture (FeedbackStoreTests.cs:8-25):**
```csharp
public sealed class FeedbackStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FeedbackStore _store;

    public FeedbackStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"feedback-test-{Guid.NewGuid():N}.db");
        _store = new FeedbackStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
```

**Apply same `IDisposable` pattern.** Note project memory observation 2378 — SQLite file-handle release timing was an issue in the past; FeedbackStoreTests works around it. Plan should mirror.

**Analog: middleware integration test — 11-burst (7e08d8c lines 76-105):**
```csharp
[Fact]
public async System.Threading.Tasks.Task ElevenFailedAuthsFromSameIp_TenthReturns401_EleventhReturns429()
{
    using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
    var tracker = new AdminBruteForceTracker();
    var middleware = new BasicAuthMiddleware(
        _ => System.Threading.Tasks.Task.CompletedTask,
        NullLogger<BasicAuthMiddleware>.Instance,
        "DeckFlow Admin",
        tracker);
    var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:wrong"));

    int lastStatus = 0;
    string lastRetryAfter = string.Empty;
    string lastWwwAuthenticate = string.Empty;
    for (var i = 0; i < 11; i++)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.20.30.40");
        ctx.Request.Headers["Authorization"] = $"Basic {encoded}";
        await middleware.InvokeAsync(ctx);
        lastStatus = ctx.Response.StatusCode;
        lastRetryAfter = ctx.Response.Headers["Retry-After"].ToString();
        lastWwwAuthenticate = ctx.Response.Headers["WWW-Authenticate"].ToString();
        if (i < 10) Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
    }
    Assert.Equal(StatusCodes.Status429TooManyRequests, lastStatus);
    Assert.NotEmpty(lastRetryAfter);
    Assert.Empty(lastWwwAuthenticate);
}
```

**Phase 5 changes:**
- The IP source switches from `ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.20.30.40")` to `ctx.Request.Headers["CF-Connecting-IP"] = "10.20.30.40"` (matches the new `DeriveCloudflareClientIp` source).
- Test that with no `CF-Connecting-IP` header, traffic shares the `unknown` bucket (CONTEXT decision: fail-closed single-bucket fallback).
- Port `SuccessfulAuthDoesNotCountTowardThrottle` (lines 107-131) — the invariant matters: only `Challenge`-emitted 401s call `RecordFailure`, never the env-var-503 path or successful auth.

**Reuse `EnvScope` helper** (Phase 4-01 lines 133-163) — pure utility, ports verbatim.

---

### `README.md` (MODIFY — operational blurb, single section)

**Goal:** Restore the Phase 4 admin-throttle blurb plus document the Cloudflare inbound-rules prerequisite.

**Primary analog:** Commit `aed9ead` ("docs(04-01): note admin throttle in README (BUG-02)").

**Analog (extract via `git show aed9ead -- README.md`):** Planner runs `git show aed9ead:README.md` to retrieve the exact blurb text and ports it forward, with the addition of:
- Lockout window: 10 attempts / 15 min
- Retry-After header behavior
- Cloudflare CIDR Inbound-Rules requirement on Render dashboard with link to https://render.com/docs/inbound-ip-rules
- Note: blurb format follows project convention "README updated when behavior changes" (CLAUDE.md constraints + project memory `feedback_readme_updates`).

---

## Shared Patterns

### Relational Dialect Helpers
**Source:** `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs:48-54` and `DeckFlow.Core/Storage/IRelationalDialect.cs`

**Apply to:** `AdminBruteForceTrackerStore.cs`

**Parameter prefix:** Always `@name` regardless of provider. The `RelationalDatabaseConnection.AddParameter(command, "@name", value)` helper is the canonical surface.
```csharp
public static void AddParameter(DbCommand command, string name, object? value)
{
    var parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.Value = value ?? DBNull.Value;
    command.Parameters.Add(parameter);
}
```

**Connection factory:** `_connectionInfo.CreateConnection()` returns `SqliteConnection` or `NpgsqlConnection` based on provider enum. Use `await connection.OpenAsync(cancellationToken)` always.

**Type mapping:** `_connectionInfo.IsPostgres` and `_connectionInfo.IsSqlite` switch ISO-string vs native `DateTime`/`DateTimeOffset` for timestamps. See `FeedbackStore.cs:51` for canonical pattern.

**Schema add to dialect interface:** If the planner chooses to add `AdminThrottleUpsertSql` and `AdminThrottleWindowSeconds` to `IRelationalDialect`, follow the existing `FeedbackInsertReturningIdSql` shape (`DeckFlow.Core/Storage/PostgresRelationalDialect.cs:14-18` and `SqliteRelationalDialect.cs:14-18`). Adding new dialect properties requires touching three files: `IRelationalDialect.cs`, `PostgresRelationalDialect.cs`, `SqliteRelationalDialect.cs`. Phase planner decides whether to take this hit or just inline raw SQL with `_connectionInfo.IsPostgres ? PG_SQL : SQLITE_SQL` switching at the call site (less abstract, matches `CategoryKnowledgeRepository.cs:712-732` pattern for column-discovery split).

### Polly v8 Pipeline Resolution
**Source:** Current `ScryfallTaggerService.cs:67-69`

**Apply to:** Modified `ScryfallTaggerService.cs` — KEEP UNCHANGED.
```csharp
_scryfallPipeline = pipelineProvider.GetPipeline<RestResponse>("scryfall");
_taggerPipeline = pipelineProvider.GetPipeline<RestResponse>("tagger");
_taggerPostPipeline = pipelineProvider.GetPipeline<RestResponse>("tagger-post");
```

Polly pipelines `tagger`, `tagger-post`, `scryfall` are registered in `ResiliencePipelineFactory.cs` and resolved via `ResiliencePipelineProvider<string>`. Plan must NOT touch the registration. Project memory `feedback_http_resilience_pattern` confirms this is the canonical pattern — direct Polly v8, not MS standard handler.

### Service DI Registration
**Source:** `Program.cs:115` (`builder.Services.AddSingleton<IFeedbackStore, FeedbackStore>();`)

**Apply to:** `AdminBruteForceTrackerStore`
```csharp
builder.Services.AddSingleton<IAdminBruteForceTrackerStore, AdminBruteForceTrackerStore>();
```
Singleton lifetime matches FeedbackStore. The `IWebHostEnvironment`-overload constructor is what gets resolved at startup; the connection factory determines provider via env var.

### Logging — Serilog Structured Templates
**Source:** Current `ScryfallTaggerService.cs:91, 137, 166, 216` and `BasicAuthMiddleware.cs:73`

**Apply to:** All five Tagger logging additions + middleware throttle log + new partition-key-fallback warning.

Use named placeholders (PascalCase tokens), not interpolation:
```csharp
_logger.LogWarning("Tagger.SessionFetch failed for {CardName} ({Set}/{Number}): HTTP {StatusCode} in {ElapsedMs}ms; csrf={CsrfPresent} cookies={CookieCount}",
    cardName, set, number, status, elapsedMs, csrfPresent, cookieCount);
```

CONTEXT.md provides the exact five templates; planner must use these property names verbatim so log scraping in Render works.

### MockHttp Test Pattern
**Source:** `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs:27-48` and `CommanderSpellbookServiceTests.cs` (sibling)

**Apply to:** Modified ScryfallTaggerServiceTests — keep `CreateService` factory shape.

`MockHttpMessageHandler.When(...).Respond(...)` pattern is project-standard (`RichardSzalay.MockHttp 7.0.0` in `DeckFlow.Web.Tests.csproj`). Service constructors take real `HttpClient` instances backed by mock handlers, plus `FakeScryfallRestClientFactory` and `FakeResiliencePipelineProvider` test doubles.

**KEY LIMITATION (project memory observation 2831):** MockHttp does NOT integrate with `SocketsHttpHandler`/`CookieContainer` because it replaces the handler chain entirely. So tests cannot directly verify cookie automation works — they verify the service no longer writes a manual `Cookie` header. Live UAT (per `04-ABANDONED.md` "Common" lessons) is the gate for the cookie-automation behavior.

### Test Project — No Testcontainers Yet
**Source:** Project memory observation 2988 — "DeckFlow.Web.Tests.csproj has no Testcontainers reference — Postgres integration tests cannot run yet"

**Apply to:** `AdminBruteForceTrackerStoreTests` — use SQLite temp-file fixture pattern, NOT Postgres. Plan should NOT add Testcontainers.PostgreSQL (out of scope per CONTEXT "Not in scope: integration test framework changes").

If the planner chooses Postgres-only UPSERT SQL with no SQLite implementation, then the unit tests must skip the Postgres-only path (e.g., `[Fact(Skip = "Requires Postgres")]` or `Assert.Throws<NotSupportedException>` on the SQLite path). Live UAT is the gate.

---

## No Analog Found

None. Every Phase 5 file has at least one strong codebase analog. The git-archaeology pulls (Phase 4-01 reverted code) are the closest matches for the new throttle-aware middleware and store.

---

## Files Explicitly NOT in Scope (Belt-and-Suspenders Confirmation)

Per CONTEXT.md "Not in scope" + project boundary check:

| Surface | Confirmation |
|---|---|
| `DeckFlow.Web/wwwroot/css/**` | NOT touched. No theme/layout work. |
| `DeckFlow.Web/wwwroot/ts/**` | NOT touched. No client-side scripts. |
| `DeckFlow.Web/Views/**` | NOT touched. No Razor view changes. |
| `browser-extensions/**` | NOT touched. |
| `DeckFlow.Core/Storage/IRelationalDialect.cs` | OPTIONAL touch — only if planner chooses to add `AdminThrottleUpsertSql` to the interface. CONTEXT defers to Claude's discretion. |
| `tasks/UI-REVIEW.md` | NOT touched. UI score does not change. |
| Other HTTP-dependent services (BanList, Spellbook, Scryfall) | NOT touched — observability rollout to those services is in Deferred per CONTEXT. |

---

## Metadata

**Analog search scope:**
- `DeckFlow.Web/Services/` (FeedbackStore, ScryfallTaggerService, ScryfallTaggerHttpClient)
- `DeckFlow.Web/Infrastructure/` (BasicAuthMiddleware)
- `DeckFlow.Web/` (Program.cs)
- `DeckFlow.Core/Knowledge/` (CategoryKnowledgeRepository)
- `DeckFlow.Core/Storage/` (RelationalDatabaseConnection, IRelationalDialect, Sqlite/Postgres dialects)
- `DeckFlow.Web.Tests/` (FeedbackStoreTests, BasicAuthMiddlewareTests, ScryfallTaggerServiceTests)
- Git history: `4db8b8a^` (pre-migration Tagger), `50849e9` (Phase 4-01 middleware + tracker), `7e08d8c` (Phase 4-01 tests), `aed9ead` (Phase 4-01 README blurb)

**Files scanned:** 13 production sources + 3 test sources + 4 git-history snapshots = 20

**Pattern extraction date:** 2026-05-02
