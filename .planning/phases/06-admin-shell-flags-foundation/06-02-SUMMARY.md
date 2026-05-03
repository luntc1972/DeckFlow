---
phase: 06-admin-shell-flags-foundation
plan: 02
subsystem: persistence
tags: [feature-flags, postgres, sqlite, dual-dialect, ensure-schema, upsert]

requires:
  - phase: 06-admin-shell-flags-foundation
    plan: 01
    provides: Admin shell + _MaintenancePage view (no direct dependency, but the persistence layer this plan ships becomes the data source for the admin Flags page in plan 05)
  - external: AdminBruteForceTrackerStore (Phase 5) — exact pattern analog for triple-ctor, EnsureSchemaAsync gate, and IsPostgres branching
provides:
  - IFeatureFlagStore contract (GetAllAsync / SetEnabledAsync / EnsureSchemaAsync)
  - Sealed FeatureFlagStore implementation (Postgres + SQLite, lazy schema bootstrap, default-on seed)
  - DeckFlowDatabaseConnectionFactory.CreateFeatureFlagConnection — single-logical-DB connection routing
  - feature_flags table schema (D-07) created on first call against either provider
  - Default-on seed for scryfall.tagger.enabled and page.help.enabled (D-09) preserved across restarts via ON CONFLICT (key) DO NOTHING (FLAG-01)
affects: [06-03, 06-04, 06-05]

tech-stack:
  added: []
  patterns:
    - "Triple-ctor test seam ((string sqlitePath) / (RelationalDatabaseConnection) / (IWebHostEnvironment)) — mirrors AdminBruteForceTrackerStore exactly so FeatureFlagStore can be unit-tested against in-memory SQLite or wired via DI in production."
    - "Dual-dialect SQL constants with IsPostgres branching — CREATE TABLE / seed / UPSERT each have a Postgres and a SQLite variant; runtime selector in command.CommandText assignment. No IRelationalDialect bump (kept feedback-specific)."
    - "Idempotent seed via ON CONFLICT (key) DO NOTHING — fresh-DB writes default-on rows; existing-DB re-bootstrap leaves operator-set values untouched, enforcing FLAG-01 'no default-off accidentally killing live behavior on fresh DB' at the schema layer."
    - "EXCLUDED-column UPSERT (works on both Postgres and SQLite) — ON CONFLICT (key) DO UPDATE SET enabled = EXCLUDED.enabled, updated_at = EXCLUDED.updated_at. Avoids the table-qualified column ambiguity warned about in feedback_sqlite_postgres_sql_divergence.md."
    - "Parameterized writes via RelationalDatabaseConnection.AddParameter — three params (@key, @enabled, @now) with provider-specific value coercion (BOOLEAN vs INTEGER 0/1, UtcDateTime vs ISO-8601 string). Zero string concatenation in any CommandText assignment (T-06-B1 mitigation)."

key-files:
  created:
    - DeckFlow.Web/Services/FeatureFlags/IFeatureFlagStore.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
  modified:
    - DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs

key-decisions:
  - "D-07 schema implemented verbatim — Postgres: key TEXT PRIMARY KEY, enabled BOOLEAN NOT NULL DEFAULT TRUE, updated_at TIMESTAMPTZ NOT NULL DEFAULT now(); SQLite: key TEXT PRIMARY KEY, enabled INTEGER NOT NULL DEFAULT 1, updated_at TEXT NOT NULL DEFAULT (datetime('now'))."
  - "D-08 dotted-namespace naming reflected in the seed list — both seed keys are lowercase + dots-only ('scryfall.tagger.enabled', 'page.help.enabled')."
  - "D-09 seed list shipped — exactly two rows, both default TRUE, inserted on schema bootstrap; DB-level ON CONFLICT preserves operator changes."
  - "Co-locate IFeatureFlagStore + FeatureFlagStore namespace at DeckFlow.Web.Services.FeatureFlags — but split into two files (per plan files_modified spec) so the interface is independently navigable. AdminBruteForceTrackerStore co-locates in one file; this plan deliberately splits because the interface signature is part of the public contract handed to plan 04 cache."
  - "Use EXCLUDED columns (not table-qualified columns) on the SET clause of the UPSERT — works on both Postgres and SQLite, avoids the ambiguity called out in feedback_sqlite_postgres_sql_divergence.md."
  - "No DI registration in this plan — plan 04 will register the store via AddDeckFlowFeatureFlags() once the cache exists; registering it here would be dead weight (no consumer until plan 04)."

patterns-established:
  - "Dual-dialect store pattern continues: every new shared-table store in v1.1+ follows AdminBruteForceTrackerStore + FeatureFlagStore precedent — IsPostgres branching on CommandText, no IRelationalDialect bump unless a third site demands it."
  - "Seed-on-bootstrap pattern: stores that ship default rows do it inside EnsureSchemaAsync, immediately after CREATE TABLE, in a separate ExecuteNonQueryAsync, with ON CONFLICT (key) DO NOTHING. Idempotent on every call."
  - "Boolean reader compatibility helper (ReadBool): handles bool / long / int / short / string '1'|'true' uniformly — covers Postgres BOOLEAN, SQLite INTEGER, and any future provider that hands back a string. Reusable shape for future bool columns."

threat-mitigations:
  - id: T-06-B1
    category: Tampering
    component: FeatureFlagStore.SetEnabledAsync
    disposition: mitigated
    mitigation: "Verified: every CommandText assignment in FeatureFlagStore.cs is a const string literal (PostgresCreateTableSql / SqliteCreateTableSql / PostgresSeedSql / SqliteSeedSql / PostgresUpsertSql / SqliteUpsertSql) plus one inline literal for SELECT. All variable values flow through RelationalDatabaseConnection.AddParameter (3 calls in SetEnabledAsync, all parameterized). Grep inside the SetEnabledAsync block returned 0 matches for $\", \" + key, \" + , or string.Format."
  - id: T-06-B2
    category: Tampering (concurrent admin writes)
    component: feature_flags table
    disposition: accept
    mitigation: "ON CONFLICT (key) DO UPDATE is atomic at the DB level on both Postgres and SQLite; near-simultaneous toggles by single-operator BasicAuth admin are last-write-wins by design. Audit log deferred to POLISH-02."
  - id: T-06-B3
    category: Denial of Service (schema gate)
    component: FeatureFlagStore.EnsureSchemaAsync
    disposition: accept
    mitigation: "SemaphoreSlim(1,1) gate + volatile _schemaReady double-checked-locking. Worst case: one extra round-trip on app cold-start. Matches AdminBruteForceTrackerStore precedent already in production."
  - id: T-06-B4
    category: Information Disclosure (default-off kills live behavior)
    component: seed insert
    disposition: mitigated
    mitigation: "Seed list inserts default-TRUE rows for both currently-shipped flag-gated features. ON CONFLICT (key) DO NOTHING means existing operator-set values survive re-bootstrap. Cache-side default-on for missing keys (D-13) lands in plan 04. Together they enforce FLAG-01."

requirements-completed: [FLAG-01]

duration: 3min
completed: 2026-05-03
---

# Phase 6 Plan 02: Feature Flag Persistence Summary

**Dual-dialect FeatureFlagStore (Postgres + SQLite) with feature_flags table, default-on seed for scryfall.tagger.enabled + page.help.enabled (D-09), and ON CONFLICT (key) DO NOTHING idempotency that enforces FLAG-01 'no default-off accidentally killing live behavior on fresh DB' at the schema layer.**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-05-03T02:59:40Z
- **Completed:** 2026-05-03T03:02:25Z
- **Tasks:** 2 / 2
- **Files:** 2 created, 1 modified

## Accomplishments

- `CreateFeatureFlagConnection(IWebHostEnvironment)` added to `DeckFlowDatabaseConnectionFactory` — delegates to `CreateFeedbackConnection` exactly like `CreateAdminThrottleConnection`. Single logical DB; the new `feature_flags` table sits alongside `feedback` and `admin_brute_force_buckets` (D-07).
- `IFeatureFlagStore` declares the three-method contract (`GetAllAsync`, `SetEnabledAsync`, `EnsureSchemaAsync`) that plan 04's cache and plan 05's controller will both consume.
- `FeatureFlagStore` is `sealed`, triple-ctor (`(string)` / `(RelationalDatabaseConnection)` / `(IWebHostEnvironment)`), with `SemaphoreSlim`-gated lazy schema bootstrap + idempotent seed.
- D-07 schema shipped on both providers — `BOOLEAN NOT NULL DEFAULT TRUE` on Postgres, `INTEGER NOT NULL DEFAULT 1` on SQLite — with `now()` / `datetime('now')` updated_at defaults.
- D-09 seed shipped — `scryfall.tagger.enabled` + `page.help.enabled` both default TRUE, inserted at bootstrap with `ON CONFLICT (key) DO NOTHING` so operator-set values are preserved across deployments and app restarts.
- T-06-B1 mitigated by construction — every SQL block is a const literal; every variable flows through `RelationalDatabaseConnection.AddParameter`; zero concatenation or interpolation inside any `CommandText` assignment (verified by grep).
- T-06-B4 mitigated by the seed itself — fresh-DB bootstraps with both flags ON, so a fresh Postgres instance never silently kills Tagger or Help.

## Files

### Created

- `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagStore.cs` — three-method interface, XML-docced.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — `sealed` impl, ~210 LOC including doc comments and the six dual-dialect SQL constants.

### Modified

- `DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs` — added `CreateFeatureFlagConnection`, +9 lines.

## Verification

- `dotnet build DeckFlow.sln` exits 0 — 0 warnings, 0 errors (verified after each task commit).
- Per-task automated checks (file existence, contract grep counts) all green:
  - Triple-ctor present (3 hits for `public FeatureFlagStore(`).
  - 3 `RelationalDatabaseConnection.AddParameter` calls in `SetEnabledAsync`.
  - 0 concat/interpolation matches inside the `SetEnabledAsync` block.
  - 4 `ON CONFLICT (key) DO NOTHING` hits across seed SQL constants.
  - 2 `EXCLUDED.enabled` / `excluded.enabled` hits in upsert constants.
- **Postgres SQL not exercised at runtime in this plan** — Postgres is not running locally and this plan ships persistence-layer only. The dual-dialect SQL is reviewed against the `AdminBruteForceTrackerStore` precedent (which IS running on Postgres in production via Render) and against `feedback_sqlite_postgres_sql_divergence.md` lessons. Plan 04's cache wires the store and exercises `GetAllAsync` against the actual deployed Postgres on first request after merge — that is the live verification gate.

## Deviations from Plan

None. The plan was executed exactly as written. Two minor planner-discretion points were resolved as documented:

1. **File split** — plan said "default ONE file `FeatureFlagStore.cs` (matching the analog), and create `IFeatureFlagStore.cs` as a separate minimal file containing only the interface declaration so the `files_modified` frontmatter holds." Done exactly that way. Interface lives alone in its own file; impl + dialect constants live alone in theirs. Frontmatter holds.
2. **Boolean reader** — added a private `ReadBool` helper to handle the Postgres `BOOLEAN`-vs-SQLite `INTEGER` discrepancy in `GetAllAsync`. Pattern matches `ReadTimestamp` in `AdminBruteForceTrackerStore`. Not strictly required by the plan, but `GetAllAsync` reads `enabled` from both providers, and a uniform reader is the safe path. Counted as Rule 2 (auto-add critical functionality — without it `GetAllAsync` would crash on the first call against SQLite when reading `INTEGER`).

## Hand-off Notes

### For plan 04 (Cache + DI extension)

- Wire registration in a new `AddDeckFlowFeatureFlags()` extension method (per PATTERNS §"Extensions/AddDeckFlowFeatureFlagsExtension.cs"):
  - `services.AddSingleton<IFeatureFlagStore, FeatureFlagStore>()` — DI ctor `FeatureFlagStore(IWebHostEnvironment)` resolves automatically.
  - `services.AddSingleton<FeatureFlagCache>()` then `services.AddSingleton<IFeatureFlagCache>(sp => sp.GetRequiredService<FeatureFlagCache>())` and `services.AddHostedService(sp => sp.GetRequiredService<FeatureFlagCache>())`.
- The cache's `BackgroundService.StartAsync` should `await store.GetAllAsync(cancellationToken)` BEFORE `await base.StartAsync` to honor D-14 sync-first-load.
- The store's `GetAllAsync` is the single read path; it lazy-bootstraps schema + seed on first call. Cache does not need to call `EnsureSchemaAsync` separately.

### For plan 05 (AdminFlagsController)

- `POST /Admin/Flags/{key}/toggle` calls `_store.SetEnabledAsync(key, enabled)`, then `await _cache.ReloadAsync(HttpContext.RequestAborted)` for D-10 synchronous in-process reload.
- The store throws `ArgumentException` on null/whitespace `key` — controller must validate before calling, or catch and return `BadRequest`.
- All POST forms must carry `@Html.AntiForgeryToken()` (ADMIN-05).

### For plan 06 (Tagger gate) and 07 (Help gate)

- Stringly-typed lookup via `IFeatureFlagCache.IsEnabled("scryfall.tagger.enabled")` / `IsEnabled("page.help.enabled")`. Both keys exist in the seed; missing-key fallback (D-13) only applies to typo'd or future keys.

## Self-Check: PASSED

- Files: `IFeatureFlagStore.cs`, `FeatureFlagStore.cs`, `DeckFlowDatabaseConnectionFactory.cs` — all FOUND on disk.
- Commits: `0820b2e` (Task 1), `53df16d` (Task 2) — both FOUND in `git log`.
- Build: `dotnet build DeckFlow.sln` clean (0 warnings, 0 errors).
- Scope: only the three files in `files_modified` were touched.
- No unintentional file deletions.
