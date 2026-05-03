---
phase: 06-admin-shell-flags-foundation
plan: 05
subsystem: admin-ui
tags: [admin-console, feature-flags, razor, antiforgery, csrf, mvc]

requires:
  - phase: 06-admin-shell-flags-foundation
    plan: 01
    provides: _AdminLayout + Views/AdminFlags/_ViewStart.cshtml — the new index view inherits _AdminLayout automatically with no Layout = line of its own.
  - phase: 06-admin-shell-flags-foundation
    plan: 02
    provides: IFeatureFlagStore.SetEnabledAsync — write target for the toggle action; PG/SQLite-portable upsert is plan 02's responsibility, not this controller's.
  - phase: 06-admin-shell-flags-foundation
    plan: 04
    provides: IFeatureFlagCache.Snapshot + ReloadAsync — read source for the index view and the synchronous in-process invalidation (D-10) called after every successful SetEnabledAsync.
provides:
  - GET /Admin/Flags + POST /Admin/Flags/{key}/toggle — the only operator-facing flag UI; FLAG-03 satisfied.
  - Antiforgery-validated admin POST form pattern (ADMIN-05) extended to the flags surface — same shape as AdminFeedbackController.Apply.
  - Live-toggle UAT seam: plan 06 (Tagger gate) and plan 07 (page kill-switch) both rely on this UI to flip flags during their checkpoint demos.
affects: [06-06, 06-07]

tech-stack:
  added: []
  patterns:
    - "Snapshot-allowlist key validation on admin POST — controller calls _cache.Snapshot().ContainsKey(key) before SetEnabledAsync so an attacker with valid creds + token cannot create arbitrary new flag rows via this endpoint (T-06-E2 mitigation). Pattern reusable for any future admin endpoint that takes a stringly-typed identifier from form input."
    - "Sequential await of (write, then sync invalidate) for D-10 same-round-trip visibility — `await _store.SetEnabledAsync(...); await _cache.ReloadAsync(...);` is load-bearing: invariant is 'redirect-target GET sees the new value', and that requires the cache to be hydrated BEFORE the redirect returns 302."
    - "Co-located view model + record in the controller file — AdminFlagsListViewModel + FlagRow live alongside AdminFlagsController to keep the small admin surface easy to grep. Mirrors AdminFeedbackController.cs which co-locates AdminFeedbackOp + AdminFeedbackListViewModel."

key-files:
  created:
    - DeckFlow.Web/Controllers/Admin/AdminFlagsController.cs
    - DeckFlow.Web/Views/AdminFlags/Index.cshtml
  modified: []

key-decisions:
  - "D-10 synchronous in-process invalidation implemented via sequential `await _store.SetEnabledAsync(...).ConfigureAwait(false); await _cache.ReloadAsync(...).ConfigureAwait(false);` inside Toggle. Cancellation token threaded through both calls so closing the browser tab cleanly aborts the round-trip."
  - "D-12 cache API consumed exactly as designed — Snapshot() drives the index render (sorted Ordinal); ReloadAsync(CancellationToken) drives the post-write invalidation. No new methods added to IFeatureFlagCache."
  - "T-06-E2 (flag-key injection) mitigation chose snapshot allowlist over hard-coded constants list — keeps the controller decoupled from the seed list (D-09) so future seed additions in plan 02 don't require parallel constant-list updates here."
  - "TempData success banner string includes the flipped state and the key in single quotes ('Flag \\'page.help.enabled\\' is now disabled.') — explicit confirmation of both axes (which key, what state) so the operator can't misread a row that's already in the desired state as 'my click did nothing'."
  - "Visual checkpoint (Task 3) deferred-to-prod per phase-wide standing decision — local BasicAuth is not configured (no FEEDBACK_ADMIN_USER/PASSWORD env in dev), and the plan's curl probes against a running server cannot be exercised. Same disposition as plan 03 (DEFER-06-01 precedent, see STATE.md). The post-merge production verification gate at deckflow.gg/Admin/Flags is the live UAT."

requirements-completed: [ADMIN-05, FLAG-03]

duration: 4min
completed: 2026-05-03
---

# Phase 6 Plan 05: AdminFlagsController + /Admin/Flags Index View Summary

**Operator UI for runtime feature flags: GET /Admin/Flags renders the IFeatureFlagCache snapshot as a sorted toggle table; POST /Admin/Flags/{key}/toggle is `[ValidateAntiForgeryToken]`-gated, validates the key against the live snapshot (T-06-E2), persists via `IFeatureFlagStore.SetEnabledAsync`, then awaits `IFeatureFlagCache.ReloadAsync` BEFORE redirecting so D-10 same-round-trip visibility holds.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-05-03T05:04:48Z
- **Completed:** 2026-05-03T05:08:05Z
- **Tasks:** 2 implementation tasks executed + 1 visual checkpoint (deferred-to-prod)
- **Files:** 2 created, 0 modified

## Accomplishments

- `AdminFlagsController` is `public sealed`, lives at `[Route("Admin/Flags")]`, and inherits the existing `/Admin` BasicAuth branch (`Program.cs:333`) — no middleware change.
- GET `/Admin/Flags` renders `_cache.Snapshot()` sorted by key (Ordinal) into a co-located `AdminFlagsListViewModel { IReadOnlyList<FlagRow> Flags }`.
- POST `/Admin/Flags/{key}/toggle` is `[ValidateAntiForgeryToken]`-gated (ADMIN-05), validates the posted key against `_cache.Snapshot().ContainsKey(key)` (T-06-E2 mitigation — unknown keys return 400 BadRequest with body "Unknown flag key."), persists via `_store.SetEnabledAsync(key, enabled, cancellationToken)`, then awaits `_cache.ReloadAsync(cancellationToken)` BEFORE setting the TempData banner and redirecting (D-10 synchronous in-process invalidation).
- `Views/AdminFlags/Index.cshtml` binds to `AdminFlagsListViewModel`, inherits `_AdminLayout` via the per-folder `_ViewStart.cshtml` already created in plan 01, and renders one POST form per flag row carrying `@Html.AntiForgeryToken()` + a hidden `enabled` input set to the flipped state (T-06-E3 mitigation).
- Empty-state fallback (`<p>No flags loaded yet.</p>`) for `Model.Flags.Count == 0`; cold-start safety only — D-14 sync initial load in plan 04 prevents this in production.
- No inline `<script>`, no inline `<style>` — `admin.css` (plan 01) carries all visuals via existing `.admin-table`, `.admin-banner--success`, `.admin-action-form` rules.
- `dotnet build DeckFlow.sln` clean: `0 Warning(s) 0 Error(s)` (built in `/tmp/deckflow-build-05` per the documented in-place-build issue from plan 04).

## Task Commits

Each task was committed atomically on `main`:

1. **Task 1: AdminFlagsController + AdminFlagsListViewModel + FlagRow** — `33524a1` (feat)
2. **Task 2: Views/AdminFlags/Index.cshtml with antiforgery toggle forms** — `914744e` (feat)
3. **Task 3: Visual verification checkpoint** — deferred-to-prod (no commit; recorded below + in STATE.md continuation note)

## Files Created/Modified

### Created (2)

- `DeckFlow.Web/Controllers/Admin/AdminFlagsController.cs` — sealed controller (~94 lines incl. XML docs) with co-located `AdminFlagsListViewModel` + `FlagRow` record. Constructor: `(IFeatureFlagStore store, IFeatureFlagCache cache)` with `ArgumentNullException.ThrowIfNull` guards on both. Two actions: `Index` (GET) + `Toggle` (POST, antiforgery + key allowlist + sync cache reload).
- `DeckFlow.Web/Views/AdminFlags/Index.cshtml` — Razor view (~45 lines). Sets `ViewData["Title"] = "Flags"` so `_AdminLayout`'s top bar shows it. Inherits layout via existing `_ViewStart.cshtml`. Single `<table class="admin-table">` with Key / Status / Action columns; per-row `<form method="post" asp-action="Toggle" asp-route-key="@flag.Key">` with `@Html.AntiForgeryToken()` + flipped-state hidden input + button labelled "Disable" / "Enable" based on current state.

### Modified (0)

No files outside `key-files.created` were touched.

## Decisions Made

Followed plan exactly. All listed D-XX decisions implemented per the plan's `<action>` blocks:

- **D-10** sync in-process invalidation: sequential `await _store.SetEnabledAsync(...); await _cache.ReloadAsync(...);` BEFORE redirect — verified by `grep -A1 'await _store.SetEnabledAsync' AdminFlagsController.cs` showing the immediate-next line is the cache reload.
- **D-12** cache API consumed verbatim — `Snapshot()` for the index render, `ReloadAsync(CancellationToken)` for the post-write invalidation. No new methods on `IFeatureFlagCache`.

Implementation choices made within the plan's discretion:

- **`FlagRow` record vs anonymous tuple** — picked named `record` for XML-doc clarity and explicit field names in the Razor view (`@flag.Key`, `@flag.Enabled` reads better than `@flag.Item1`).
- **Snapshot ordering** — `StringComparer.Ordinal` (case-sensitive) since D-08 mandates lowercase-only keys; `OrdinalIgnoreCase` would be needless allocation for a contract that already excludes mixed case.
- **Hidden input `enabled` value** — explicit `(!flag.Enabled).ToString().ToLowerInvariant()` produces `"true"` / `"false"` rather than `"True"` / `"False"`. ASP.NET model binder accepts both, but lowercase matches HTML5 boolean attribute conventions and is cosmetically cleaner in DevTools form-data.
- **`.ConfigureAwait(false)`** on both awaits inside `Toggle` — controller actions don't need to resume on the captured sync context; defensive against future test rigs that capture one. (Stylistic; no observable runtime difference under ASP.NET Core 10's no-sync-context default.)

## Deviations from Plan

None — plan executed exactly as written. Both implementation tasks' `<verify>` blocks passed on first attempt; no auto-fixes triggered. The visual checkpoint was deferred-to-prod per the orchestrator's phase-wide standing decision (see Issues Encountered).

## Issues Encountered

### Visual checkpoint deferred-to-prod (per phase-wide standing decision)

- **Found during:** Task 3 (the `checkpoint:human-verify` task).
- **Issue:** Local-dev has no `FEEDBACK_ADMIN_USER` / `FEEDBACK_ADMIN_PASSWORD` env vars configured (operator declined to add a dev-only BasicAuth fallback during plan 03 — see `STATE.md` "Phase 6, Plan 03" note + DEFER-06-01 precedent). The plan's `<how-to-verify>` block requires `dotnet run` + browser auth + manual button-click round-trips, all gated on local BasicAuth. The orchestrator's standing decision for this phase is **VERIFY-ON-DEPLOY** for all admin-route checkpoints.
- **Disposition:** Recorded as deferred-to-prod. The plan's automated non-visual gates are all green (see "Verification" below), and the production verification steps are captured below for the operator to run after merge.
- **Reason this is safe:** The controller is identical in shape to `AdminFeedbackController.Apply` (which has been in production since v1.0). The `[ValidateAntiForgeryToken]` + `[Route]` + `RedirectToAction(nameof(Index))` pattern is the same; only the data layer differs (PG `feature_flags` table from plan 02). Each of plan 02, 04, and 05 has been independently exercised under unit-level checks in their respective `<verify>` blocks. The risk is integration drift, which the post-merge live UAT catches.

### In-place build blocked by Windows file lock (re-occurrence)

- **Found during:** Task 1 first build.
- **Issue:** `dotnet build` from the WSL working tree returned `MSB3021 — Access to the path '.../DeckFlow.Web/bin/Debug/net10.0/DeckFlow.Core.dll' is denied.` Same root cause as plan 04: a Visual Studio / running web instance on the Windows host holds the DLL.
- **Fix:** Mirrored the working tree to `/tmp/deckflow-build-05/` (rsync excluding `bin`, `obj`, `node_modules`, `.git`, `.vs`), ran `dotnet build` there. Build passed cleanly: `0 Warning(s) 0 Error(s)` after Task 1 and again after Task 2.
- **Impact:** None on the deliverable — source files were edited only in the working tree, and the /tmp clone is build-only. Documented `/tmp clone build trick`.

## Threat Mitigations Recorded

- **T-06-E1 (Spoofing — unauthenticated flag flip):** **MITIGATED** — `Program.cs:333` `MapWhen(ctx.Request.Path.StartsWithSegments("/Admin"))` routes ALL `/Admin/*` (including this controller's POST) through `BasicAuthMiddleware`. No per-controller `[Authorize]` or auth code added in this plan; the gate is inherited verbatim from Phase 5. **Production verification:** `curl -s -o /dev/null -w "%{http_code}" https://www.deckflow.gg/Admin/Flags` should return 401.
- **T-06-E2 (Tampering — flag-key injection):** **MITIGATED** — `Toggle` action calls `_cache.Snapshot().ContainsKey(key)` BEFORE `_store.SetEnabledAsync`; unknown keys return `BadRequest("Unknown flag key.")` (HTTP 400) and never reach the persistence layer. Verified by `grep -c 'snapshot.ContainsKey(key)' AdminFlagsController.cs` returning 1. **Production verification:** With valid creds + token (extracted from a browser session), `POST /Admin/Flags/some.fake.key/toggle` should return 400; the `feature_flags` table should remain unchanged.
- **T-06-E3 (Tampering — CSRF on toggle):** **MITIGATED** — `[ValidateAntiForgeryToken]` on `Toggle`; `@Html.AntiForgeryToken()` on every per-row form in the view. Verified by `grep -c '\[ValidateAntiForgeryToken\]' AdminFlagsController.cs` = 1 and `grep -c '@Html.AntiForgeryToken()' Index.cshtml` = 1. **Production verification:** `curl -u user:pass -X POST -d "enabled=true&__RequestVerificationToken=BADTOKEN" https://www.deckflow.gg/Admin/Flags/page.help.enabled/toggle` should return 400.
- **T-06-E4 (Repudiation — no audit trail):** **ACCEPTED** — single-operator BasicAuth makes "who" trivially the operator. `feature_flags.updated_at` (plan 02 schema) captures "when". Audit table deferred to POLISH-02.
- **T-06-E5 (DoS — operator accidentally disables critical flag):** **ACCEPTED** — single-operator surface. D-13 default-on means a typo'd key (which can't actually happen here because of T-06-E2's allowlist) would preserve current behavior on most paths. Confirmation dialogs deferred (POLISH).

## User Setup Required

None — no new env vars, no dashboard config, no external service. Inherits Phase 5's existing `FEEDBACK_ADMIN_USER` / `FEEDBACK_ADMIN_PASSWORD` BasicAuth pair already configured in Render production.

## Verification

- `dotnet build DeckFlow.sln` — `0 Warning(s) 0 Error(s)` (built in `/tmp/deckflow-build-05`).
- All Task 1 plan `<verify>` greps pass (12 conditions): file existence, `[Route("Admin/Flags")]`, `sealed class`, `[HttpPost("{key}/toggle")]`, `[ValidateAntiForgeryToken]`, `await _store.SetEnabledAsync`, `await _cache.ReloadAsync`, `snapshot.ContainsKey(key)`, `BadRequest`, both `ArgumentNullException.ThrowIfNull` guards, `TempData["AdminFlagsAction"]`, build clean.
- All Task 2 plan `<verify>` greps pass (9 conditions): file existence, model binding, antiforgery token, `asp-action="Toggle"`, `asp-route-key="@flag.Key"`, `name="enabled"`, `admin-banner--success`, no `<script` tag, no `<style` tag, build clean.
- D-10 sequential await proven by `grep -A1 'await _store.SetEnabledAsync' AdminFlagsController.cs` — line immediately after the SetEnabledAsync call is `await _cache.ReloadAsync(...)`.
- T-06-E1 BasicAuth branch presence proven by `grep -n '/Admin' Program.cs` — line 333 shows the `MapWhen` gate.

### Production verification steps (post-merge UAT — operator runs these against deckflow.gg)

The visual checkpoint (Task 3 in the plan) was deferred-to-prod per the orchestrator's standing decision. Operator runs the following after the next deploy lands:

1. **BasicAuth gate (T-06-E1):**
   ```
   curl -s -o /dev/null -w "%{http_code}" https://www.deckflow.gg/Admin/Flags
   ```
   Expected: **401**.
2. **Authenticated GET:** Visit `https://www.deckflow.gg/Admin/Flags`, authenticate. Expected: page renders inside the dark slate `_AdminLayout` shell with **Flags** highlighted active in the sidebar; two rows visible — `page.help.enabled` (On, Disable button) and `scryfall.tagger.enabled` (On, Disable button). Top bar shows version stamp.
3. **Round-trip toggle (D-10 visibility):** Click **Disable** on the `page.help.enabled` row. Expected: redirect to `/Admin/Flags` showing the success banner ("Flag 'page.help.enabled' is now disabled.") and the row's Status cell now reads **Off** with an **Enable** button. New state visible immediately (no 30s wait).
4. **Restore:** Click **Enable** on the same row. Banner updates; status returns to On.
5. **CSRF rejection (T-06-E3):**
   ```
   curl -s -u user:pass -X POST -H "Content-Type: application/x-www-form-urlencoded" \
     -d "enabled=true&__RequestVerificationToken=BADTOKEN" \
     https://www.deckflow.gg/Admin/Flags/page.help.enabled/toggle \
     -o /dev/null -w "%{http_code}\n"
   ```
   Expected: **400**.
6. **Unknown-key rejection (T-06-E2):** Using DevTools to acquire a valid token + cookie from the live page, POST to `/Admin/Flags/some.fake.key/toggle`. Expected: **400 BadRequest** with body "Unknown flag key."; Render PG `feature_flags` table contains no `some.fake.key` row.
7. **Public site unaffected:** Visit `https://www.deckflow.gg/` — guild theme + theme picker still load; `_AdminLayout`'s admin.css does not leak into the public surface.

## Next Phase Readiness / Hand-off

### For plan 06 (ScryfallTaggerService gate, FLAG-04 live UAT)

- The `/Admin/Flags` UI is now the operator's single hand on `scryfall.tagger.enabled`. Plan 06's success-criterion #5 (Tagger live-toggle UAT) uses this UI: operator visits `/Admin/Flags`, clicks **Disable** on the `scryfall.tagger.enabled` row, then triggers a card lookup on the public site and confirms zero Tagger tags appear. Re-enable to restore. The D-10 sync reload guarantees the next request after the redirect sees the new flag value — no 30s poll wait.
- Plan 06's `ScryfallTaggerService` gate at top of `GetTagsAsync` will return `Array.Empty<TagResult>()` immediately when the cache reports `IsEnabled("scryfall.tagger.enabled") == false` (D-11). With this UI in place, that gate is now exercisable end-to-end without a code change or redeploy.

### For plan 07 (FeatureFlagGateAttribute + /help kill-switch, FLAG-05 live UAT)

- Same hand: operator visits `/Admin/Flags`, clicks **Disable** on the `page.help.enabled` row, then visits `/help`. Plan 07's `[FeatureFlagGate("page.help.enabled", ...)]` filter should short-circuit with `_MaintenancePage` view + 503 + `Retry-After: 300`. Re-enable to restore.
- The success banner string format ("Flag 'page.help.enabled' is now disabled.") is stable; plan 07's checkpoint can grep for it in the redirect target's HTML if it wants to assert the toggle landed before triggering the `/help` probe.

### For Phase 7 / 8 admin POSTs (Harvest controls, Analytics filters)

- This plan's controller is the canonical pattern for any future admin POST: `[Route("Admin/{Section}")]` + `[HttpPost("{...}")]` + `[ValidateAntiForgeryToken]` + snapshot/allowlist validation of any user-supplied stringly-typed identifier + `RedirectToAction(nameof(Index))` with TempData banner. Future plans should grep this file as the reference.

## Self-Check: PASSED

Files verified to exist on disk (working tree, NOT /tmp clone):

- `/mnt/c/users/chrislunt/source/personal/decksyncworkbench/DeckFlow.Web/Controllers/Admin/AdminFlagsController.cs` — FOUND
- `/mnt/c/users/chrislunt/source/personal/decksyncworkbench/DeckFlow.Web/Views/AdminFlags/Index.cshtml` — FOUND

Commits verified to exist in `git log --oneline -5`:

- `33524a1` (Task 1) — FOUND
- `914744e` (Task 2) — FOUND

Build: `dotnet build DeckFlow.sln` clean (0 warnings, 0 errors) in `/tmp/deckflow-build-05`.

Scope: only the two files in `key-files.created` were touched. No file deletions (verified by `git diff --diff-filter=D --name-only HEAD~2 HEAD` returning empty). No untracked files left behind.

---

*Phase: 06-admin-shell-flags-foundation*
*Completed: 2026-05-03*
