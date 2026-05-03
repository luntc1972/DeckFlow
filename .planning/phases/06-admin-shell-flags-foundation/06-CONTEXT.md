# Phase 6: Admin Shell + Flags Foundation - Context

**Gathered:** 2026-05-02
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver a neutral-themed `/Admin` shell (sidebar nav: Feedback / Harvest / Analytics / Flags) that hosts existing `/Admin/feedback` unchanged and adds a Postgres-backed feature-flag system with hot reload. Phase ends when an operator can (a) reach all four sidebar sections behind BasicAuth with no guild theme leakage, (b) toggle the Tagger kill-switch from `/Admin/flags` and observe the change within seconds on a live card lookup, and (c) toggle a page kill-switch demo on `/help` and observe a 503 maintenance page.

This phase ships the **shell + flags plumbing only**. Harvest controls land in Phase 7, Analytics in Phase 8.

</domain>

<decisions>
## Implementation Decisions

### Admin shell visual + nav
- **D-01:** Palette is **dark slate** — `bg #0f172a`, `panel #1e293b`, `text #e2e8f0`, `accent #3b82f6` (blue-500), `border #334155`. Reads as "ops console" and contrasts cleanly with all 25 guild themes.
- **D-02:** Sidebar is **labels only** (vertical list: Feedback / Harvest / Analytics / Flags) — no icons, no font/CDN dependency.
- **D-03:** Active sidebar item gets a **left border bar (3-4px accent color) + bold label**; also `aria-current="page"`.
- **D-04:** Top chrome is a **thin top bar** carrying current section H1 on the left and a small build/version stamp on the right (e.g., `v1.1 · commit a38ad90`). No footer.
- **D-05:** Layout file is `_AdminLayout.cshtml` (sibling to existing `_Layout.cshtml`) and loads only an admin-specific CSS file (`wwwroot/css/admin.css`) — **must NOT include any of the 25 `site-*.css` guild themes**. `_AdminLayout` is the layout for every admin view, set via `Views/Admin/_ViewStart.cshtml` (or per-folder `_ViewStart.cshtml`).
- **D-06 (open):** "Logout" affordance under BasicAuth is browser-cached and cannot be cleared from a server response cleanly. Planner picks one: (a) omit Logout entirely, (b) include a "Sign out" link that returns 401 to force re-prompt, or (c) include a static note "close browser to sign out." Default to (a) unless planner finds a clean (b) pattern.

### Flag schema + seed + naming
- **D-07:** `feature_flags` table is **minimal** — `key TEXT PRIMARY KEY`, `enabled BOOLEAN NOT NULL DEFAULT TRUE`, `updated_at TIMESTAMPTZ NOT NULL DEFAULT now()`. Created via `EnsureSchemaAsync` alongside existing tables.
- **D-08:** Naming convention is **dotted namespace**: `system.subsystem.flag` form, e.g., `scryfall.tagger.enabled`, `harvest.cron.enabled`, `page.help.enabled`. Lowercase, dots only as separator.
- **D-09:** Seed list (all default `TRUE`, inserted by `EnsureSchemaAsync` on fresh DB):
  - `scryfall.tagger.enabled` — required by FLAG-04
  - `page.help.enabled` — required by FLAG-05 (kill-switch demo target)

  Seed uses `INSERT ... ON CONFLICT (key) DO NOTHING` so re-running the bootstrap on an existing DB never overwrites operator changes.
- **D-10:** Cache invalidation on admin write is **synchronous in-process reload** — admin write hits Postgres → reloads full flag dict from PG → returns 200. Operator sees effect within one request round-trip. The 30s `BackgroundService` poller (REQUIREMENTS.md) stays as a backstop.

### Flag check integration points
- **D-11:** Flag gate sits **at the top of `ScryfallTaggerService` public methods** (`GetTagsAsync` etc.) — short-circuit with `Array.Empty<TagResult>()` (or equivalent empty default) when off. Keeps controllers unaware of flags. Sets the precedent for future service-level gates.
- **D-12:** Call-site API is **stringly-typed** `IFeatureFlagCache.IsEnabled(string key)`, plus `IReadOnlyDictionary<string, bool> Snapshot()` for the `/Admin/flags` list view and `Task ReloadAsync(CancellationToken ct = default)` for the synchronous-invalidation path. No code-gen, no per-flag accessor.
- **D-13:** Missing-key fallback is **default-on (return `true`)** — aligns with FLAG-01's intent that fresh DB must not silently kill shipped features. Emit a WARN log when a missing key is queried, **de-duped per key** (log-once-per-process for each missing key) so logs don't flood.
- **D-14:** Startup behavior is **synchronous initial load before Kestrel binds** — `IFeatureFlagCache` registered as `IHostedService`; `StartAsync` performs the first PG load before the host reports ready. Avoids the cold-start window where every read defaults to `true`.

### Existing /Admin/feedback migration + FLAG-05 demo
- **D-15:** Existing `AdminFeedbackController` migrates by **layout swap only** — keep route `Admin/Feedback`, keep `Views/AdminFeedback/` folder. Set `Layout = "_AdminLayout"` (via a new `Views/AdminFeedback/_ViewStart.cshtml` or by adding `Views/AdminFeedback/` to a shared `_ViewStart.cshtml` that resolves admin layout for any `Admin*` view). Zero route churn, zero broken bookmarks, ADMIN-04 "no regression" stays trivially true.
- **D-16:** FLAG-05 demo target is **`/help` (Help index)** — real user-facing page, low blast radius if accidentally disabled (no deck workflow blocked), proves the pattern under live traffic. Flag key: `page.help.enabled`.
- **D-17:** 503 response uses a **dedicated `_MaintenancePage` Razor view** (`Views/Shared/_MaintenancePage.cshtml`) bound to `MaintenanceViewModel { Title, Message }`. Returns HTTP 503 with `Retry-After` header (suggested value: 300s — planner finalizes). Same view reusable for any future page kill-switch.
- **D-18:** Wire-up is an **action filter / attribute** on the gated action method: `[FeatureFlagGate("page.help.enabled", title: "Help center", message: "...")]`. Filter resolves `IFeatureFlagCache` from DI, short-circuits with `MaintenanceViewModel` when off. Reusable on any future controller action by attribute alone.

### Claude's Discretion
- Exact `Retry-After` value on 503 maintenance response (default 300s unless planner finds a better fit).
- Whether `IFeatureFlagCache` registers as `IHostedService` for synchronous initial load OR via a `IHostApplicationLifetime`-hooked initializer — both achieve D-14, planner picks based on what slots cleanest into existing `Program.cs:50-189` registration block.
- Whether `_AdminLayout.cshtml` lives under `Views/Shared/` or `Views/Admin/` — planner decides based on view-resolution conventions already in the codebase.
- Whether the build/version stamp (D-04) reads from `IConfiguration["BuildInfo:Commit"]` populated at Docker build, from `Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()`, or from a startup-captured timestamp. Pick the one that doesn't add a Dockerfile change if possible.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + requirements
- `.planning/PROJECT.md` — milestone definition, constraints (RAM cap, public repo, no framework migration)
- `.planning/REQUIREMENTS.md` — REQ-IDs ADMIN-01..05 and FLAG-01..05 (locked)
- `.planning/ROADMAP.md` §"Phase 6" — goal, depends-on, success criteria (5 items)
- `.planning/phases/06-admin-shell-flags-foundation/06-CONTEXT.md` — this file

### Codebase patterns to follow
- `DeckFlow.Web/Program.cs:50-189` — DI registration block; new services slot in here
- `DeckFlow.Web/Program.cs:331-332` — existing `MapWhen("/Admin")` BasicAuth branch (D-01 / ADMIN-03 reuses verbatim)
- `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` — BasicAuth gate already in production (Phase 5)
- `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs` — existing admin controller; sets the `Controllers/Admin/` precedent and shows existing `[ValidateAntiForgeryToken]` use
- `DeckFlow.Web/Views/AdminFeedback/{Index,Detail}.cshtml` — existing admin views; D-15 swaps their Layout
- `DeckFlow.Web/Views/Shared/_Layout.cshtml` — public-site layout; **DO NOT use for admin pages**
- `DeckFlow.Web/Services/ScryfallTaggerService.cs` — D-11 gate added at top of public methods; FLAG-04 success criterion
- `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` — example of `EnsureSchemaAsync` pattern; new `feature_flags` table follows the same idiom
- `DeckFlow.Web/Services/Storage/` (or wherever `IRelationalDialect` lives) — pluggable SQLite/Postgres dialect; `feature_flags` schema must work on both

### Project conventions / build constraints
- `CLAUDE.md` §Constraints — tech stack pinned (ASP.NET 10 + Razor), no framework migration, public repo (no secrets), commits plain default-author (no Co-Authored-By trailer)
- `CLAUDE.md` §"HTTP / Resilience Conventions" — services follow ctor + internal-test-ctor pattern; new `IFeatureFlagCache` should match
- `feedback_sqlite_postgres_sql_divergence.md` (memory) — qualify upsert columns with table name, prefer `COUNT(1)` over `EXISTS`, run Postgres integration tests before shipping new storage SQL

### Memory / prior-decision context
- v1.0 BasicAuth + Postgres-backed throttle pattern (commits c72610d era) — `feature_flags` table follows the same persistence shape as `admin_throttle`
- v1.0 `CF-Connecting-IP` partition key — not directly relevant to Phase 6 but admin pages must keep behaving correctly behind it

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`BasicAuthMiddleware`** (`DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs`) — already protects `/Admin/*` via `MapWhen` branch in `Program.cs:331-332`. ADMIN-03 reuses verbatim; no new auth code.
- **`AdminFeedbackController`** — sets the precedent for `Controllers/Admin/` placement and shows `[ValidateAntiForgeryToken]` on POST. New `FlagsAdminController` (or whatever name) follows this pattern.
- **`RelationalDatabaseConnection` + `IRelationalDialect`** (SQLite + Postgres) — `feature_flags` table goes through the same dialect abstraction as existing tables. Schema must work on both providers.
- **`EnsureSchemaAsync` idiom** in `CategoryKnowledgeStore` and `FeedbackStore` — `IFeatureFlagStore.EnsureSchemaAsync` follows the same pattern (CREATE TABLE IF NOT EXISTS + idempotent seed).
- **`ArchidektCacheJobService`** registered as both `Singleton` and `IHostedService` (`Program.cs:178-180`) — same dual-registration pattern fits `IFeatureFlagCache` (singleton facade + hosted service for poller + initial load).
- **Antiforgery (`[ValidateAntiForgeryToken]`)** already wired and proven in `AdminFeedbackController` — ADMIN-05 just extends to the new admin POST forms.

### Established Patterns
- **One service interface + sealed implementation per file** — follow for `IFeatureFlagCache` / `FeatureFlagCache`, `IFeatureFlagStore` / `PostgresFeatureFlagStore`.
- **Public DI ctor + internal test ctor** with `[InternalsVisibleTo("DeckFlow.Web.Tests")]` — apply to new services so tests can inject fakes/stubs without touching DI.
- **Razor views per controller folder, named PascalCase `.cshtml`** — `Views/{ControllerName}/Index.cshtml`. Shared partials prefixed `_`.
- **Structured Serilog logging** — named placeholders only (`{Key}`, not interpolation). De-duped WARN log (D-13) uses a `ConcurrentDictionary<string, byte>` sentinel to track already-logged keys.
- **Render Inbound IP Rules + Cloudflare CIDR allow-list** is the spoof guarantee for IP-partitioned features — flags don't need this since they're not IP-keyed, but the BasicAuth gate already inherits it.

### Integration Points
- `Program.cs:50-189` — register `IFeatureFlagStore`, `IFeatureFlagCache` (Singleton + HostedService), and gate filter (`FeatureFlagGateAttribute`) here.
- `Program.cs:331-332` — admin path branch already in place; new admin controllers slot in without middleware changes.
- `ScryfallTaggerService` ctor — accepts `IFeatureFlagCache`. Service's existing internal test ctor extends to inject a fake flag cache.
- `EnsureSchemaAsync` call site at startup — `IFeatureFlagStore.EnsureSchemaAsync` runs alongside other stores' bootstrap.
- `ServiceCollection` extension `AddDeckFlowResiliencePipelines()` is the precedent for grouping registrations — `AddDeckFlowFeatureFlags()` extension method captures the dual registration neatly.

</code_context>

<specifics>
## Specific Ideas

- **"Reads as ops console, not deck-builder"** — dark slate palette intentionally distances admin from public guild-themed surface; operator should never confuse the two.
- **"Build stamp on top bar"** — explicit user request: needs to confirm a deploy actually landed. Render dashboard lag and BasicAuth-cached page sometimes trick eyes into seeing old code.
- **Layout-swap-only for feedback migration** — explicit ADMIN-04 risk reduction. Operator has muscle-memory for `/Admin/Feedback` URL and inbox flow; nothing should change visually except the chrome.
- **Default-on missing-flag is a deliberate FLAG-01 alignment** — REQUIREMENTS.md FLAG-01 calls out "no default-off accidentally killing live behavior on fresh DB"; D-13 implements that contract at the cache level so even a typo'd key never silently kills production.
- **Tagger as the FLAG-04 anchor** is operator-driven: Cloudflare BIC has burned this app twice (Phase 4 abandoned, Phase 5 BUG-01) — being able to cut Tagger from the browser without a deploy is the highest-value flag in the system.
- **Help as the FLAG-05 anchor** is risk-balanced: real user-facing page that proves the pattern but won't block any deck workflow if accidentally toggled off.

</specifics>

<deferred>
## Deferred Ideas

- **Sidebar status badges** (job running, unread feedback count) — POLISH-01 in REQUIREMENTS.md; not in v1.1 scope.
- **Feature-flag audit log** (`flag_audit_log` table tracking who toggled what when) — POLISH-02; defer until multi-user admin auth lands.
- **Non-bool flag types** (string / int / json values) — POLISH-03; bool-only is sufficient for kill switches in v1.1.
- **Sidebar collapse / breadcrumb / mobile admin nav** — POLISH-04; single-operator desktop-only is fine for v1.1.
- **Multiple gated pages beyond `/help`** — D-16 ships exactly one demo page; future kill-switches add by attribute alone.
- **PG NOTIFY/LISTEN for cross-instance cache invalidation** — single-instance Render Starter doesn't need it; revisit if/when multi-instance scale arrives.
- **Per-flag description / help text in admin UI** — minimal schema (D-07) skips description column; if `/Admin/flags` page feels opaque, planner can add a code-side `static class FlagCatalog` for human descriptions without a schema change.

</deferred>

---

*Phase: 6-admin-shell-flags-foundation*
*Context gathered: 2026-05-02*
