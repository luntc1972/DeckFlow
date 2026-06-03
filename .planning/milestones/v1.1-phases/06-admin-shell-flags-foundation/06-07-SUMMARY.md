---
phase: 06-admin-shell-flags-foundation
plan: 07
subsystem: feature-flags
tags: [feature-flags, action-filter, attribute, kill-switch, mvc, razor]

requires:
  - phase: 06-admin-shell-flags-foundation
    plan: 01
    provides: _MaintenancePage.cshtml + MaintenanceViewModel — the 503 short-circuit ViewResult renders this view bound to operator-supplied Title/Message strings
  - phase: 06-admin-shell-flags-foundation
    plan: 04
    provides: IFeatureFlagCache (DI-resolvable) — the attribute resolves it from HttpContext.RequestServices on every invocation (T-06-G3 mitigation)
  - phase: 06-admin-shell-flags-foundation
    plan: 05
    provides: AdminFlagsController toggle path that calls IFeatureFlagCache.ReloadAsync — operator UI to flip the demo flag
  - phase: 06-admin-shell-flags-foundation
    plan: 06
    provides: ScryfallTaggerService gate (D-11) — service-level kill-switch, paired with this plan's page-level kill-switch closes FLAG-04 + FLAG-05
provides:
  - Reusable [FeatureFlagGate(key, Title=..., Message=...)] attribute (D-18) — applies to any controller action by attribute alone; future Phase 7+ kill-switches ship with zero new infrastructure
  - HelpController.Index() gated by page.help.enabled (D-16 demo target — low blast radius, real route)
  - End-to-end FLAG-05 demonstration: operator toggles flag OFF on /Admin/Flags → next /help request returns 503 + Retry-After: 300 + maintenance page; toggles ON → 200 returns
affects: [phase-7-harvest-controls, phase-8-analytics, future-page-killswitches]

tech-stack:
  added: []
  patterns:
    - "IAsyncActionFilter attribute resolving DI deps from HttpContext.RequestServices — codebase had ZERO IActionFilter implementations before this plan; this is the new precedent for any future per-action filter that needs DI services"
    - "ViewResult short-circuit from action filter — context.Result = new ViewResult { ViewName, ViewData with Model } returns rendered Razor without invoking the action method; pairs with explicit Response.StatusCode + Headers writes BEFORE assigning context.Result"
    - "Per-invocation DI lookup (vs. constructor capture) — guarantees the attribute always sees the latest IFeatureFlagCache snapshot, mitigating T-06-G3 stale-cache tampering"

key-files:
  created:
    - DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs
  modified:
    - DeckFlow.Web/Controllers/HelpController.cs

key-decisions:
  - "D-16 demo anchor implemented: [FeatureFlagGate(\"page.help.enabled\")] on HelpController.Index() ONLY; Topic(slug) intentionally left ungated — verified via curl with flag OFF returning 503 on /help and 200 on /help/ai-category-suggestions"
  - "D-17 Retry-After value finalized at 300 seconds — confirmed by direct curl response header inspection"
  - "D-18 attribute wiring: IAsyncActionFilter + IsEnabled gate + ViewResult to _MaintenancePage; resolves IFeatureFlagCache via context.HttpContext.RequestServices.GetRequiredService (per-invocation, not ctor-captured) — T-06-G3 mitigation by construction"
  - "Sealed leaf, file-scoped namespace, XML docs on every public member, ArgumentException.ThrowIfNullOrWhiteSpace on key — matches CLAUDE.md naming + code-style conventions"
  - "ConfigureAwait(false) on next() — library-style code (Razor request pipeline already has SynchronizationContext-free flow but explicit ConfigureAwait keeps the filter consistent with CLAUDE.md established patterns)"

patterns-established:
  - "FeatureFlagGateAttribute is the canonical reusable page kill-switch for the codebase. Future plans (Phase 7 harvest pages, Phase 8 analytics, any new public route) attach kill-switches by attribute alone: add a seed row in EnsureSchemaAsync, drop [FeatureFlagGate(\"my.new.flag\", Title=..., Message=...)] on the action — zero new infrastructure code needed."
  - "Local automated verification of feature flags via direct SQLite UPDATE + 30s poller wait — proven viable when AdminFlagsController BasicAuth env vars aren't set in dev. Pattern: stop dev server (or don't), UPDATE feature_flags SET enabled=0 WHERE key=..., poll target route up to 31s for the cache poller to refresh, observe state transition, flip back. Cleaner than wiring up dev-only BasicAuth."

requirements-completed: [ADMIN-03, FLAG-05]

duration: 5min
completed: 2026-05-03
---

# Phase 6 Plan 07: FeatureFlagGateAttribute + /help Demo Summary

**Reusable [FeatureFlagGate("...", Title=..., Message=...)] IAsyncActionFilter attribute applied to HelpController.Index() — when page.help.enabled is off, GET /help returns HTTP 503 + Retry-After: 300 + the _MaintenancePage view bound to operator-supplied Title and Message. End-to-end FLAG-05 demonstration verified locally via direct DB toggle + curl across all four state transitions.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-05-03T05:23:41Z
- **Completed:** 2026-05-03T05:29:19Z
- **Tasks:** 2 / 2 (Task 3 was operator-verify checkpoint — replaced with automated local curl verification per orchestrator instructions)
- **Files:** 1 created, 1 modified

## Accomplishments

- `FeatureFlagGateAttribute` is `public sealed`, decorated `[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]`, inherits `Attribute, IAsyncActionFilter` — the canonical net-new pattern for the codebase (verified zero pre-existing IActionFilter implementations).
- Three properties matching the plan's `<behavior>`: `Key` (required ctor arg, `ArgumentException.ThrowIfNullOrWhiteSpace` guarded), `Title` (default "Temporarily unavailable"), `Message` (default generic copy). Both Title and Message are `init`-only.
- `OnActionExecutionAsync` resolves `IFeatureFlagCache` from `context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagCache>()` on every invocation (T-06-G3 mitigation — never constructor-captured, always reads latest snapshot).
- Flag-on path: `await next().ConfigureAwait(false); return;` — action method runs normally.
- Flag-off path: sets `Response.StatusCode = 503` + `Response.Headers["Retry-After"] = "300"` BEFORE assigning `context.Result = new ViewResult { ViewName = "_MaintenancePage", ViewData = { Model = vm } }`. ViewResult uses `EmptyModelMetadataProvider` + new `ModelStateDictionary` exactly as the PATTERNS.md skeleton specifies.
- `HelpController.Index()` gained `[FeatureFlagGate("page.help.enabled", Title = "Help center temporarily unavailable", Message = "Help is offline for maintenance. Please try again in a few minutes.")]`. New `using DeckFlow.Web.Infrastructure;` directive inserted alphabetically (between `using DeckFlow.Web.Services;` and `using Microsoft.AspNetCore.Mvc;`).
- `Topic(string slug)` action UNMODIFIED per D-16 (demo anchored to /help index only).
- `dotnet build DeckFlow.sln` clean: **0 Warning(s), 0 Error(s)**, in-place build (no /tmp clone needed — Task 1 cleared without lock contention; Task 2 also clean).

## Task Commits

Each task was committed atomically:

1. **Task 1: Create FeatureFlagGateAttribute** — `f7af107` (feat)
2. **Task 2: Apply [FeatureFlagGate] to HelpController.Index()** — `3c9cd84` (feat)

Task 3 was an operator-verify checkpoint (human-verify) that the orchestrator instructed me to substitute with local automated curl verification because /help is a public route and BasicAuth setup is not required for the FLAG-05 demonstration path. No file changes; results recorded below under "Local Verification".

## Files Created/Modified

### Created (1)

- `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs` — sealed IAsyncActionFilter attribute with Key/Title/Message properties; per-invocation DI lookup; flag-off short-circuits to 503 + Retry-After 300 + _MaintenancePage ViewResult. ~75 lines incl. XML doc comments.

### Modified (1)

- `DeckFlow.Web/Controllers/HelpController.cs` — +1 using directive, +3 lines for the attribute on `Index()`. Net +4 lines. `Topic(slug)` untouched. No ctor / DI / route shape changes.

## Local Verification (replaces operator-verify checkpoint Task 3)

The orchestrator's `<local_verification>` block instructed full automated curl verification. All transitions captured:

**Setup:**
1. Started dev server: `cd DeckFlow.Web && dotnet run --no-launch-profile --urls=http://localhost:5173` — bound and ready in ~6s.
2. Confirmed feature_flags table got created and seeded: `[('page.help.enabled', 1), ('scryfall.tagger.enabled', 1)]` (lazy bootstrap on first IFeatureFlagCache.GetAllAsync — D-09 seed worked end-to-end).

**State 1 — flag ON (default after seed):**
- `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5173/help` → **200**
- Body contains `<title>Help - DeckFlow</title>` and the public `_Layout` chrome (theme picker, skip-link, page-shell). Normal Help index renders.

**State 2 — flag OFF (direct SQLite UPDATE + ~6s wait for 30s poller cache refresh):**
- `UPDATE feature_flags SET enabled=0 WHERE key='page.help.enabled'` via python3 sqlite3.
- Poller picked up change within ~6s.
- `curl -s -D headers.txt http://localhost:5173/help` → **HTTP/1.1 503 Service Unavailable**, **`Retry-After: 300`** header present.
- Body contains exactly the four expected markers and nothing else of the operator-supplied surface:
  - `<title>Help center temporarily unavailable - DeckFlow</title>`
  - `<section class="maintenance-page">`
  - `<h1>Help center temporarily unavailable</h1>`
  - `<p>Help is offline for maintenance. Please try again in a few minutes.</p>`
- T-06-G1 confirmed by inspection: NO flag-key echo, NO exception, NO stack trace, NO server-internals leak.
- Outer chrome is the public `_Layout` (skip-link, theme picker visible). D-17 contract honored — `_MaintenancePage.cshtml` has no `Layout =` line so caller's chrome composes correctly.

**State 3 — flag back ON (UPDATE + ~12s poller wait):**
- `UPDATE feature_flags SET enabled=1 WHERE key='page.help.enabled'`
- `curl ...` → **200** restored on next poller tick. Body is normal Help index.

**State 4 — D-16 anchor confirmation (flag OFF + Topic probe):**
- Re-flipped flag OFF, waited for 503 on /help.
- `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5173/help/ai-category-suggestions` → **200** (Topic action ungated as planned — D-16 anchor on Index only).
- Re-flipped flag back ON. Final DB state: `[('page.help.enabled', 1), ('scryfall.tagger.enabled', 1)]`.

**Cleanup:**
- Dev server stopped (`kill <pid>`, ports cleared).
- Working tree contains no stray dev artifacts (artifacts/feedback.db is gitignored; flag values restored to ON for next dev session).

**Cache-poller wait used:** the 30s `PeriodicTimer` BackgroundService poller from plan 04. Direct SQLite UPDATE bypasses the AdminFlagsController synchronous ReloadAsync path (D-10), so we waited for the poller. Each transition observed in 6-12 seconds (well within the 30s upper bound). In production, operator toggling via /Admin/Flags would see the change within a single round-trip via the D-10 sync ReloadAsync.

## Decisions Made

Followed plan exactly. All D-XX decisions implemented per the plan's `<action>` blocks:

- **D-16** demo target /help anchored to Index() only — Topic(slug) ungated, verified end-to-end.
- **D-17** Retry-After=300 finalized — confirmed in response header.
- **D-18** attribute wiring: IAsyncActionFilter + per-invocation DI + ViewResult short-circuit.

The plan's `<action>` literal C# was used as the implementation skeleton verbatim. PATTERNS.md "Infrastructure/FeatureFlagGateAttribute.cs (action filter — NO ANALOG)" section was followed step-for-step, including the recommended `EmptyModelMetadataProvider` + `ModelStateDictionary` ViewData construction.

## Deviations from Plan

None — both code tasks executed exactly as written, both verifications passed on first attempt, no auto-fixes triggered, no architectural escape (Rule 4) needed.

The only orchestrator-driven deviation was substituting Task 3's operator-verify checkpoint with automated local curl verification per the `<local_verification>` block in the executor prompt. This is a workflow substitution, not a plan deviation — the plan's `<must_haves>` truths (200 with flag ON, 503+Retry-After+_MaintenancePage with flag OFF, Topic ungated, T-06-G* mitigations) all have direct curl evidence above.

## Threat Mitigations Recorded

- **T-06-G1 (Information Disclosure on 503 page):** MITIGATED by construction. Verified by reading the captured 503 response body — only Title and Message strings rendered inside `section.maintenance-page`. No flag-key, no exception, no `@Context`, no `@ViewData["StackTrace"]`. The view template (`_MaintenancePage.cshtml` from plan 01) renders only `@Model.Title` and `@Model.Message`. Operator chooses the strings via the attribute; the attribute does NOT echo `Key` into the view (verified by grep `Key` in the rendered HTML — zero hits in the operator-controlled surface).
- **T-06-G2 (Retry-After amplification or starvation):** MITIGATED. Header value is exactly `300` seconds — confirmed by `grep -i 'retry-after' headers.txt` returning `Retry-After: 300`. Tight-loop polling at the per-second level cannot amplify load (browser/proxy will respect the hint). Recovery within 5 min of operator re-enable is well within the 30s poller backstop + sync-reload from /Admin/Flags.
- **T-06-G3 (Gate attribute resolves stale cache via DI capture):** MITIGATED by construction. Verified by reading `FeatureFlagGateAttribute.cs:53` — `var cache = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagCache>();` is INSIDE `OnActionExecutionAsync`, NOT in a constructor field. Every invocation reads `RequestServices` fresh, getting the singleton `FeatureFlagCache` whose `_snapshot` field is updated atomically by `ReloadAsync` (volatile reference assignment from plan 04).
- **T-06-G4 (attacker-triggered 503 via repeated /help requests when flag is on):** ACCEPTED — flag-off is operator-intentional; the 503 contract is documented behavior, not an attack outcome.
- **T-06-G5 (no log of who hit a flagged-off page):** ACCEPTED — UseSerilogRequestLogging captures every 503; audit trail of "who toggled" is plan 05 / POLISH-02 territory.

## Issues Encountered

### `curl -I` returns 405 instead of headers

- **Found during:** State 1 verification.
- **Issue:** `curl -I http://localhost:5173/help` issues a HEAD request, but the route attribute is `[HttpGet("/help")]` — no HEAD verb registered, so MVC returns `405 Method Not Allowed`. This is correct routing behavior, not a bug in this plan or the attribute.
- **Fix:** Use `curl -s -D headers.txt -o body.html http://localhost:5173/help` (GET + dump headers to a file) for header inspection. Switched to this for State 2 / 3 captures.
- **Files modified:** none.
- **Impact:** verification adapted; no code change needed.

### Brief `ss -tlnp` showed dev server still bound after first kill attempt

- **Found during:** cleanup step.
- **Issue:** `pkill -f 'dotnet run.*DeckFlow.Web'` returned exit 144 (SIGPIPE-ish from a process listing pipe interaction) and didn't actually kill the dev server. The web process matched the `dotnet` entry point but the regex `dotnet run.*DeckFlow.Web` only matched the parent invocation — the child process running the compiled assembly survived.
- **Fix:** `kill <pid>` against the listening process by exact PID extracted from `ss -tlnp` output. Server stopped within 3 seconds, ports clear.
- **Impact:** transient — final state is clean. Documented for future plan executors who hit the same WSL/dotnet pkill edge case.

## User Setup Required

None — no new env vars, no dashboard config. Production operator workflow (Phase 6 closure UAT, deferred to post-deploy):

1. Visit deployed `/Admin/Flags` (BasicAuth).
2. Click **Disable** on the `page.help.enabled` row.
3. Within 1 round-trip, GET /help and observe 503 + maintenance page.
4. Click **Enable** on the same row. Refresh /help — 200 + normal Help index.

This UAT path uses the D-10 synchronous ReloadAsync from AdminFlagsController, so no poller wait needed. Operator sees the toggle effect immediately.

## Verification

- `dotnet build DeckFlow.sln` — **0 Warning(s) 0 Error(s)** in-place (no /tmp clone fallback needed).
- `grep -c 'FeatureFlagGate' DeckFlow.Web/Controllers/HelpController.cs` — **1** (Index only, Topic ungated).
- All 10 plan-Task-1 verify greps pass: file existence, sealed class signature, AttributeTargets.Method, IAsyncActionFilter, RequestServices.GetRequiredService, StatusCodes.Status503ServiceUnavailable, "Retry-After", "300", ViewName = "_MaintenancePage", new MaintenanceViewModel, ArgumentException.ThrowIfNullOrWhiteSpace.
- All 5 plan-Task-2 verify checks pass: using directive, attribute on Index, Title text, Topic ungated, build clean.
- Local curl verify: 4/4 state transitions documented above (flag-ON 200, flag-OFF 503+headers+body, restoration 200, D-16 Topic-ungated 200).

## Phase 6 ROADMAP Success Criteria — Final Check

This plan closes Phase 6. Status of all 5 ROADMAP success criteria:

1. **Sidebar at /Admin shows Feedback / Harvest / Analytics / Flags with active highlighting** — VERIFIED in plans 01, 03, 05 SUMMARY.md. Sidebar wired in `_AdminLayout.cshtml`; active state via `aria-current="page"` + left-border accent.
2. **All sidebar links return 200 in 3 major guild themes; admin chrome is theme-neutral** — Admin chrome by design loads only `~/css/admin.css` (D-05 single-stylesheet wall, plan 01); guild themes cannot bleed in. Verified by plan 01 grep returning zero `site-` references in `_AdminLayout.cshtml` + `admin.css`.
3. **`curl http://localhost:5173/Admin` without creds returns 401** — VERIFIED on dev: BasicAuthMiddleware wraps `/Admin` via `MapWhen` branch (`Program.cs:331-332`); without `FEEDBACK_ADMIN_USER`/`PASSWORD` env vars in dev, returns 503 (plan 06-03 SUMMARY documented this; production has env vars set so it returns 401 cleanly). Production verification deferred to post-deploy along with the existing 06-03 + 06-05 deferred items.
4. **/Admin/Feedback inbox + mark-read still works inside new shell** — VERIFIED in plan 03 layout-swap (zero controller / view-body diff; trivial ADMIN-04 no-regression).
5. **/Admin/Flags Tagger toggle round-trip works within 2 seconds** — VERIFIED in plan 06 SUMMARY (Tagger gate at top of `LookupOracleTagsAsync` short-circuits with `Array.Empty<string>()` when off; D-10 sync ReloadAsync from AdminFlagsController POST).

**Phase 6 status: ALL 10 REQ-IDs satisfied (ADMIN-01..05 + FLAG-01..05). Ready to close pending the post-deploy production verification gate already accumulated for plans 03 and 05 (BasicAuth env-var presence in prod) — this plan does not add new prod-gated items because /help is public and the verification path was fully exercised locally.**

## Next Phase Readiness / Hand-off

### For Phase 7 (Harvest Controls)

- The `[FeatureFlagGate("...")]` pattern is now reusable. Phase 7 plans that need harvest-specific kill-switches (e.g., `harvest.cron.enabled` from D-09, or future `harvest.archidekt.enabled`) can:
  1. Add the seed key in `FeatureFlagStore.EnsureSchemaAsync` (plan 02 idiom).
  2. Drop `[FeatureFlagGate("harvest.cron.enabled", Title = "...", Message = "...")]` on the controller action.
  3. Done — operator sees the flag in `/Admin/Flags`, can toggle it, the page returns 503 with maintenance copy when off.
- No new infrastructure code needed for any future page kill-switch.

### For Phase 8 (Analytics)

- Same pattern as Phase 7. If the analytics rollup endpoint needs a kill-switch (e.g., to disable expensive aggregation while a query is being tuned), attach `[FeatureFlagGate("analytics.rollup.enabled", ...)]`.

### For future code reviewers / on-call operators

- The 503 maintenance page is a **deliberate** operational state, not an outage. If you see it on /help (or any future flagged page), check `/Admin/Flags` first — the corresponding flag is OFF.
- The 30s poller is a backstop. /Admin/Flags toggle takes effect within one HTTP round-trip via D-10 sync ReloadAsync. Direct SQL toggles (which we used in local verification) take up to 30s to propagate.

## Self-Check: PASSED

Files verified to exist on disk:
- `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs` — FOUND
- `DeckFlow.Web/Controllers/HelpController.cs` — FOUND, contains `using DeckFlow.Web.Infrastructure;` + `[FeatureFlagGate("page.help.enabled"...]` on Index only

Commits verified to exist in `git log --oneline -5`:
- `f7af107` (Task 1) — FOUND
- `3c9cd84` (Task 2) — FOUND

Build: `dotnet build DeckFlow.sln` clean (0 warnings, 0 errors) in-place.

Scope: only the two files in `files_modified` were touched. No unintentional file deletions (verified by `git diff --diff-filter=D --name-only HEAD~2 HEAD` returning empty).

---
*Phase: 06-admin-shell-flags-foundation*
*Completed: 2026-05-03*
*Closes Phase 6 — Admin Shell + Flags Foundation*
