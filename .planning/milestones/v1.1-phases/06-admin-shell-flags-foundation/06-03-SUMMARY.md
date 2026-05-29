---
phase: 06-admin-shell-flags-foundation
plan: 03
subsystem: ui
tags: [admin-shell, layout, razor, viewstart, layout-swap, basicauth]

requires:
  - phase: 06-admin-shell-flags-foundation
    plan: 01
    provides: _AdminLayout.cshtml shell + ~/css/admin.css + sidebar nav with active-link highlighting
  - phase: 06-admin-shell-flags-foundation
    plan: 02
    provides: (no direct dependency — included for wave-2 context only)
provides:
  - AdminFeedback views render inside _AdminLayout (dark slate sidebar) instead of public-site _Layout (D-15 layout-swap-only enforced)
  - Per-folder _ViewStart pattern proven on existing/live admin route — establishes the template plans 04-07 will reuse for AdminLanding / AdminHarvest / AdminAnalytics / AdminFlags folders
  - DEFER-06-01 one-line Razor fix (`v@VersionService.GetVersion()` → `v@(VersionService.GetVersion())`) folded into closure commit so post-merge prod top bar renders correctly
affects: [06-04, 06-05, 06-06, 06-07]

tech-stack:
  added: []
  patterns:
    - "Per-folder _ViewStart override: any per-area view tree can opt into _AdminLayout by dropping a 3-line _ViewStart.cshtml at the folder root — no Layout = ... inside the views themselves, no controller-level [Layout] attribute, no extra DI."
    - "Verify-on-deploy fallback for blocked local-checkpoint paths: when local-dev cannot exercise an auth-gated visual gate, the `dotnet build` clean is the only pre-merge automated check; verification reduces to a single post-merge URL visit on prod (Render auto-deploys main). Justified only when surface area is bounded (e.g. 3-line _ViewStart) and the shell itself was already verified by an earlier plan."

key-files:
  created:
    - DeckFlow.Web/Views/AdminFeedback/_ViewStart.cshtml
  modified:
    - DeckFlow.Web/Views/Shared/_AdminLayout.cshtml

key-decisions:
  - "D-15 layout-swap-only enforced: zero edits to AdminFeedbackController.cs, Index.cshtml, Detail.cshtml. The single new file is a 3-line _ViewStart that sets Layout = \"_AdminLayout\". Verified by `git diff` on each."
  - "Verify-on-deploy chosen over implementing a local BasicAuth fallback: see 06-03-CHECKPOINT-FEEDBACK.md §Resolution. Options A (dev-only middleware fallback), B (README docs), C (launchSettings.json env vars) all rejected; risk justification recorded."
  - "DEFER-06-01 (literal-text Razor bug in _AdminLayout.cshtml:30) folded into this closure commit instead of spawning a separate trivial-fix plan. Single-line change `v@VersionService.GetVersion()` → `v@(VersionService.GetVersion())` — Razor parentheses force expression evaluation. Out-of-strict-scope per D-15 but operationally tied to the same post-merge verification gate."

patterns-established:
  - "Layout-swap closure with deferred verification: when a checkpoint:human-verify gate cannot be exercised locally for environmental reasons, the plan can still close cleanly if (a) the build is green, (b) the surface area is bounded and reviewable by `git diff`, (c) the post-merge re-test plan is recorded in CHECKPOINT-FEEDBACK.md §Resolution, and (d) any related deferred-items.md entries are folded into the closure commit so they ride the same prod verification."

threat-mitigations:
  - id: T-06-C1
    category: "Tampering / Repudiation (regression on existing live route)"
    component: "/Admin/Feedback inbox + mark-read POST"
    disposition: mitigated-pending-prod-verify
    mitigation: "D-15 zero-controller-edit constraint enforced — `git diff HEAD~ -- DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs DeckFlow.Web/Views/AdminFeedback/Index.cshtml DeckFlow.Web/Views/AdminFeedback/Detail.cshtml` returns empty. Antiforgery token in view body and [ValidateAntiForgeryToken] on the controller action are both untouched. Build clean. Post-merge prod visit (re-test plan in 06-03-CHECKPOINT-FEEDBACK.md §Resolution) is the live verification gate."
  - id: T-06-C2
    category: "Information Disclosure (theme leakage on admin page)"
    component: "/Admin/Feedback rendered output"
    disposition: mitigated
    mitigation: "Plan 06-01 already proved _AdminLayout.cshtml loads only ~/css/admin.css with zero references to site-*.css. By switching the AdminFeedback layout to _AdminLayout (this plan), AdminFeedback inherits that mitigation — the public-site theme picker, footer, and back-to-top button are no longer in the rendered DOM."
  - id: T-06-C3
    category: "Spoofing (BasicAuth coverage)"
    component: "/Admin/Feedback path"
    disposition: accept
    mitigation: "Path /Admin/Feedback unchanged. The MapWhen branch (`ctx.Request.Path.StartsWithSegments(\"/Admin\")` at Program.cs:330) still matches. No middleware reorder. Layout swap is purely a view-resolution change."

requirements-completed: [ADMIN-03, ADMIN-04]

duration: ~25min (across two execution slices: e9adbb2 layout swap + 6195047 checkpoint capture + closure commit)
completed: 2026-05-02
---

# Phase 6 Plan 03: AdminFeedback Layout Swap to `_AdminLayout` Summary

**Per-folder `_ViewStart.cshtml` swaps `/Admin/Feedback` rendering from the public-site `_Layout` (with guild themes and theme picker) to the new `_AdminLayout` (dark slate sidebar + admin.css only) with zero controller and zero view-body edits — D-15 layout-swap-only constraint enforced by `git diff`. DEFER-06-01 Razor literal-text bug in the layout's version stamp folded into the same closure commit. Visual verification deferred to post-merge prod because local-dev BasicAuth env vars are not configured (verify-on-deploy strategy recorded in 06-03-CHECKPOINT-FEEDBACK.md §Resolution).**

## Performance

- **Duration:** ~25 min total (executor slice 1: ~10 min for Task 1; checkpoint capture: ~5 min; resolution + DEFER-06-01 + closure: ~10 min)
- **Started:** 2026-05-03T03:02:25Z (continuation from 06-02 completion)
- **Completed:** 2026-05-03T04:47:00Z (closure commit)
- **Tasks:** 1 / 2 fully done; 1 / 2 deferred-to-prod
- **Files:** 1 created, 1 modified

## Accomplishments

- `Views/AdminFeedback/_ViewStart.cshtml` created (3 lines, sets `Layout = "_AdminLayout"`). Per-folder override wins over root `Views/_ViewStart.cshtml`, so `/Admin/Feedback/Index` and `/Admin/Feedback/Detail` both render inside the new admin shell — no edits to either view body needed (their `@{}` blocks have no `Layout = ...` line, confirmed in 06-PATTERNS.md and verified by `git diff`).
- D-15 enforced by construction: `AdminFeedbackController.cs`, `Index.cshtml`, `Detail.cshtml` all have empty `git diff` output across the entire plan.
- DEFER-06-01 folded into closure: `_AdminLayout.cshtml:30` literal-text Razor bug fixed (`v@VersionService.GetVersion()` → `v@(VersionService.GetVersion())`) so the build version stamp will render as `v1.x.y` on the first post-merge prod visit instead of the literal Razor source string. Build verified 0 warnings / 0 errors after the change.
- Verify-on-deploy strategy recorded in `06-03-CHECKPOINT-FEEDBACK.md §Resolution`: local-dev BasicAuth not configured (env vars unset), prod env vars set in Render dashboard, post-merge URL visit on `https://www.deckflow.gg/Admin/Feedback` is the live gate. Risk acknowledged + bounded by 3-line surface area + already-verified `_AdminLayout.cshtml` shell from Plan 06-01.

## Task Commits

1. **Task 1: Create Views/AdminFeedback/_ViewStart.cshtml (3-line layout override)** — `e9adbb2` (feat)
2. **Task 2: Operator verifies /Admin/Feedback inbox + mark-read flow inside new shell** — **deferred-to-prod**. Originally a `checkpoint:human-verify` gate; FAILED locally on 2026-05-02 (HTTP 503 "Admin not configured" — see `6195047` for capture). User decision: skip local fallback, verify on prod after merge. See `06-03-CHECKPOINT-FEEDBACK.md §Resolution` for full reasoning, risk acknowledgment, and post-merge re-test plan.

**Checkpoint capture commit:** `6195047 docs(06-03): capture checkpoint failure — admin BasicAuth not configured for local dev`

**Plan metadata + DEFER-06-01 fix commit:** (to be recorded after this commit lands)

## Files Created/Modified

### Created

- `DeckFlow.Web/Views/AdminFeedback/_ViewStart.cshtml` — 3-line per-folder layout override: `@{ Layout = "_AdminLayout"; }`.

### Modified

- `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml` — DEFER-06-01 fix on line 30 only: wrap version-service call in parentheses so Razor evaluates the expression instead of treating it as literal text. Single-character net change (`@(...)`).

## Decisions Made

- **Verify-on-deploy over local fallback** — Options A (`BasicAuthMiddleware` dev-only fallback), B (README docs), C (`launchSettings.json` env vars) all rejected for the reasons in `06-03-CHECKPOINT-FEEDBACK.md §Resolution`. Operator preference: keep `BasicAuthMiddleware` security surface untouched and rely on prod verification.
- **Fold DEFER-06-01 into 06-03 closure** — instead of spawning a separate trivial-fix plan or pushing it to 06-04. Rationale: same prod verification gate (the version stamp renders on every admin page, including AdminFeedback in the new shell), keeps the post-merge re-test single-pass.

## Deviations from Plan

### Deferred Work

**1. Task 2 deferred-to-prod**

- **Found during:** Task 2 (`checkpoint:human-verify`).
- **Issue:** Local launch of `DeckFlow.Web` has no `FEEDBACK_ADMIN_USER` / `FEEDBACK_ADMIN_PASSWORD` env vars set. `BasicAuthMiddleware.InvokeAsync` returns HTTP 503 "Admin not configured." for every `/Admin/*` path before issuing a BasicAuth challenge — operator cannot reach the page to verify the layout swap visually. Not a Plan 06-03 implementation defect; the `_ViewStart.cshtml` change in `e9adbb2` is correct.
- **Resolution:** User opted not to add a local-dev auth fallback. Verification deferred to post-merge prod visit — see `06-03-CHECKPOINT-FEEDBACK.md §Resolution`.
- **Files modified:** `06-03-CHECKPOINT-FEEDBACK.md` (Resolution section appended).

### Rule 2 (Auto-add critical functionality) — folded from DEFER-06-01

**1. [Rule 2 — Bug] DEFER-06-01: literal-text Razor bug in `_AdminLayout.cshtml:30`**

- **Found during:** Plan 06-03 prior-agent verification slice (curl of `/Admin/Feedback` after layout swap, before checkpoint).
- **Issue:** Top bar build stamp rendered as the literal string `v@VersionService.GetVersion()` instead of the evaluated version (`v1.x.y`). Razor parser ambiguity: leading `v` is not a tag/whitespace boundary, so Razor did not switch into code context for the `@VersionService` expression.
- **Why folded here:** the bug rides the same post-merge prod-verification gate as the layout swap itself. Deferring it would mean either (a) opening a separate one-line plan that produces a separate prod re-verification cycle or (b) carrying a known-broken version stamp in the admin shell until 06-04 lands. Folding into the closure commit eliminates both costs.
- **Fix:** `_AdminLayout.cshtml:30` line changed from `v@VersionService.GetVersion()` to `v@(VersionService.GetVersion())` — parentheses force Razor to parse the call as an expression. Single-line change, scope-bounded.
- **Files modified:** `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml`.
- **Verification:** `dotnet build DeckFlow.sln` exits 0 with 0 warnings and 0 errors (verified in cloned tree at `/tmp/deckflow-build` to bypass Windows-side file-handle locks on the live `DeckFlow.Web/bin` directory; original tree is unchanged except for the single `.cshtml` line).
- **Committed in:** the closure commit (this plan's metadata commit).

---

**Total deviations:** 1 deferred-to-prod (Task 2) + 1 Rule 2 fold (DEFER-06-01).
**Impact on plan:** Both deviations net zero scope creep. Task 2 verification still happens — just on prod, single visit. DEFER-06-01 is one line and was already triaged in `deferred-items.md`; folding here saves a future cycle without expanding the change set.

## Issues Encountered

- **Local BasicAuth not configured** — root cause and remediation options recorded in full in `06-03-CHECKPOINT-FEEDBACK.md`. Resolved by deferring to prod verification (operator decision).
- **Windows-side file-handle locks during build verification** — operator's interactive `DeckFlow.Web.exe` (PID 60460) and `dotnet.exe` (PID 34112) held `bin/Debug/net10.0/DeckFlow.Core.dll` open, so `dotnet build DeckFlow.sln` against the original tree returned `MSB3021` (file copy denied). Worked around by `rsync`-cloning the source tree to `/tmp/deckflow-build`, copying `node_modules`, and building there — the cloned build succeeded with 0 warnings / 0 errors, confirming the Razor fix is syntactically and semantically valid. The original tree's source files are unchanged except for the single intended edits.

## User Setup Required

None for this plan. Post-merge prod verification (operator visits `https://www.deckflow.gg/Admin/Feedback` and walks the 8 checks in `06-03-PLAN.md §<how-to-verify>`) is recorded in `06-03-CHECKPOINT-FEEDBACK.md §Resolution`.

## Next Phase Readiness

- Plan 06-03 is closed (1 of 7 → 3 of 7 plans done in Phase 6 when counting 06-01 + 06-02 + 06-03).
- Plan 06-04 (FeatureFlagCache + DI extension) unblocked — no dependency on 06-03 visual verification.
- Per-folder `_ViewStart` pattern proven on a live route; plans 06-04 / 06-05 / 06-06 / 06-07 can drop equivalent 3-line files into AdminLanding / AdminFlags folders without further ceremony.

## Self-Check: PASSED

- File: `.planning/phases/06-admin-shell-flags-foundation/06-03-SUMMARY.md` — FOUND on disk (this file).
- File: `DeckFlow.Web/Views/AdminFeedback/_ViewStart.cshtml` — FOUND on disk (committed in `e9adbb2`).
- File: `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml` — modified one line (DEFER-06-01 fix), staged for closure commit.
- Commits: `e9adbb2` (Task 1), `6195047` (checkpoint capture) — both FOUND in `git log`.
- Build: `dotnet build DeckFlow.sln` clean (0 warnings, 0 errors) — verified in cloned tree at `/tmp/deckflow-build`.
- Scope: only the four files in the closure-commit set are touched (`_AdminLayout.cshtml`, `06-03-CHECKPOINT-FEEDBACK.md`, `06-03-SUMMARY.md`, `STATE.md`, `ROADMAP.md`). No unintentional file deletions.

---
*Phase: 06-admin-shell-flags-foundation*
*Completed: 2026-05-02*
