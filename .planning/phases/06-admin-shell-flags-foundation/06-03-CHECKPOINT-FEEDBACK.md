# Plan 06-03 Checkpoint Feedback

**Plan:** 06-03 — AdminFeedback layout swap to `_AdminLayout`
**Checkpoint Task:** Task 2 (`type="checkpoint:human-verify"`, gate: blocking)
**Verdict:** **FAILED**
**Date:** 2026-05-02
**Commit at time of checkpoint:** `e9adbb2` (Task 1 only — `_ViewStart.cshtml` created and committed)

---

## Operator Feedback (verbatim)

> http://localhost:5173/admin/feedback — admin not configured

---

## Root Cause Analysis

The checkpoint procedure (Plan 06-03 §`<how-to-verify>` step 2) tells the operator to:

> Authenticate with the local FEEDBACK_ADMIN_USER / FEEDBACK_ADMIN_PASSWORD env vars.

But `BasicAuthMiddleware.InvokeAsync` (`DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs:53-63`) does this:

```csharp
var user = Environment.GetEnvironmentVariable("FEEDBACK_ADMIN_USER");
var password = Environment.GetEnvironmentVariable("FEEDBACK_ADMIN_PASSWORD");

if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
{
    // Misconfigured admin — DO NOT count toward throttle (env-var path is operator
    // error, not a brute-force attempt). Phase 4-01 invariant preserved.
    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    await context.Response.WriteAsync("Admin not configured.");
    return;
}
```

When the operator launches `DeckFlow.Web` locally with no admin env vars, every `/Admin/*` request returns **HTTP 503 "Admin not configured."** before the BasicAuth challenge is even issued. The operator cannot reach the layout-swapped page to visually verify the dark-slate `_AdminLayout` shell — verification gate is blocked.

**Why the prior agent's curl test passed but the user's browser session did not:**
The prior agent (e9adbb2) ran a verification command in a subprocess that exported the env vars itself (e.g., `FEEDBACK_ADMIN_USER=admin FEEDBACK_ADMIN_PASSWORD=devpass dotnet run ...`). Those exports were scoped to the agent's shell — they did NOT propagate to the operator's later `dotnet run` invocation. The plan assumed local admin auth "just worked" without specifying setup steps; that assumption is wrong.

**Production note:** This is not a production regression — Render's dashboard sets both env vars with `sync: false`, so production `/Admin/Feedback` works. The gap is purely in the **local-dev story**.

**Scope note:** This is not a Plan 06-03 implementation defect. The `_ViewStart.cshtml` change in commit `e9adbb2` is correct and minimal (D-15 layout-swap-only). The blocker is environmental — operator cannot exercise the verification path without an admin-auth dev story.

---

## Recommended Remediation Options

The operator chooses one (or combines them) before this plan can be re-attempted. **None implemented in this commit** — capture-only.

### Option A — Dev-only auth fallback in `BasicAuthMiddleware`

Add a guard so that when `ASPNETCORE_ENVIRONMENT == "Development"` AND both env vars are unset, the middleware uses a hardcoded `admin` / `devpass` (or similar). Production behavior unchanged because env vars are always set there.

**Pros:** Zero operator setup, works on any clone, matches the muscle-memory of "just run the app."
**Cons:** New code path with security-sensitive surface. Must be carefully gated on `IHostEnvironment.IsDevelopment()` and never in `Production`. Adds a small surprise factor (`admin/devpass` is not obvious without docs).
**Risk:** If the env check ever inverts (Dev vs Production), a backdoor leaks. Mitigation: explicit unit test asserting `Production` env never falls through.
**Effort:** ~15 lines in `BasicAuthMiddleware.cs` + 2-3 tests.

### Option B — Document local-dev setup in `README.md` / `CLAUDE.md`

Add a "Local admin access" subsection telling the operator to set the two env vars before `dotnet run` (with a one-line bash export and a PowerShell equivalent). Plan 06-03 checkpoint gets updated to reference these docs.

**Pros:** Zero code change. Most conservative — security surface untouched.
**Cons:** Operator must remember every session, cross-shell command friction, easy to forget after fresh clone.
**Effort:** ~10 lines in `README.md`. Update `06-03-PLAN.md` Task 2 `<how-to-verify>` to reference README.

### Option C — `launchSettings.json` env override for the `https`/`http` profile

Inject `FEEDBACK_ADMIN_USER` and `FEEDBACK_ADMIN_PASSWORD` into the `environmentVariables` block of `DeckFlow.Web/Properties/launchSettings.json` profiles `http` and `https`. `dotnet run` and Visual Studio F5 both pick these up automatically.

**Pros:** No code change, no doc-reading required. Env vars only apply when launching via `dotnet run` with a profile (`launchSettings.json` is git-tracked but ASPNETCORE ignores it in published builds, so production is unaffected).
**Cons:** Embeds dev creds in a tracked file (currently no creds in tracked files — sets a small precedent). Pinned to `Development` profile by ASP.NET Core's profile selection logic, so prod truly is safe — but the file IS in the public GitHub repo. Mitigation: choose deliberately-fake creds like `admin` / `devpass` so leakage is operationally meaningless.
**Effort:** ~6 lines in `launchSettings.json`.

### Recommendation

**Option A is the most operator-friendly and matches the project's "just run the app" ergonomic.** It can be combined with a one-line README note (Option B) for discoverability. Option C is simplest but commits literal "credentials" to a public repo, which conflicts with `CLAUDE.md` constraint *"public repo: no secrets in commits ever."* (Even fake creds set a precedent.)

If Option A is chosen, follow-up work should:
1. Add a *new* Plan 06-03b (or fold into Plan 06-04 before its checkpoint) that implements the dev-only fallback + tests.
2. Update Plan 06-03 `<how-to-verify>` step 2 to read: *"Authenticate with admin / devpass (dev-only fallback applies when ASPNETCORE_ENVIRONMENT=Development and FEEDBACK_ADMIN_USER/PASSWORD are unset)."*
3. Re-run the Plan 06-03 checkpoint (Task 2 only — Task 1 commit `e9adbb2` stays).

If Option B is chosen, no code change — just docs + a plan-text edit, then re-run the checkpoint.

---

## Plan State on Failure

- ✅ Task 1 complete and committed: `e9adbb2 feat(06-03): swap AdminFeedback views to _AdminLayout via per-folder _ViewStart`
- ❌ Task 2 (checkpoint:human-verify) **failed** — visual verification could not be performed
- ⏸️ Plan 06-03 **NOT marked complete**. No `06-03-SUMMARY.md` written. STATE.md / ROADMAP.md / REQUIREMENTS.md unchanged.
- 🟢 Background dev server: not running (verified: no `dotnet`/`Kestrel` processes, ports 5173/7173 not listening).

---

## Next Step for Operator

1. Pick remediation option (A / B / C, or a combination).
2. Either expand a new sub-plan or fold the fix into the next plan that has BasicAuth checkpoint surface (Plan 06-04 onward).
3. Re-invoke `/gsd-execute-phase 6 --wave 2` to retry the Plan 06-03 checkpoint (Task 2 only).

---

## Resolution (2026-05-02)

**Verdict:** **deferred-to-prod** (verify-on-deploy).

**Reason:** Local BasicAuth is not configured (no `FEEDBACK_ADMIN_USER` / `FEEDBACK_ADMIN_PASSWORD` env vars), and fixing the local-dev auth story is out of scope for Plan 06-03. Render production already has both env vars set with `sync: false`, so the layout-swap will be exercised live on `https://www.deckflow.gg/Admin/Feedback` after the v1.1 admin shell merges. None of remediation options A / B / C is being implemented at this time:

- **Option A** (dev-only middleware fallback) deferred — adds new security-sensitive surface area; not justified for a single checkpoint.
- **Option B** (README docs) deferred — would still require operator setup each session; verifying once on prod is cheaper.
- **Option C** (`launchSettings.json` env vars) rejected — conflicts with `CLAUDE.md` "no secrets in commits ever" precedent (the repo is public).

**Risk acknowledged:**

- No local visual gate before merge — `dotnet build DeckFlow.sln` (0 warnings, 0 errors) is the only pre-merge automated check.
- Razor-parser bugs (e.g. literal `v@VersionService.GetVersion()` from DEFER-06-01) only surface when the page actually renders in a browser, not at build time.
- A second Razor-parser regression in the layout-swapped views would not be caught until a post-merge prod visit.

**Mitigation for THIS plan specifically:** the layout-swap surface area is the smallest possible change — a single 3-line `_ViewStart.cshtml` (`Layout = "_AdminLayout"`) with zero controller edits and zero view-body edits (D-15 enforced by `git diff`). The `_AdminLayout.cshtml` shell itself was already verified by curl in Plan 06-01 (renders with no guild theme leakage, BasicAuth gate intact). The combined risk surface is bounded to "did per-folder `_ViewStart` correctly override the root layout?" — answerable in a single post-merge URL visit.

**Closure actions taken in this commit:**

1. The DEFER-06-01 one-line Razor fix in `_AdminLayout.cshtml:30` (`v@VersionService.GetVersion()` → `v@(VersionService.GetVersion())`) is folded into the closure commit, so the version stamp will render correctly on the first post-merge prod visit.
2. `06-03-SUMMARY.md` written; Task 2 recorded as `deferred-to-prod` with link back to this document.
3. STATE.md and ROADMAP.md updated to mark Plan 06-03 complete (3 of 7 plans done in Phase 6).

**Re-test plan (post-merge):** after Render auto-deploys `main`, operator visits `https://www.deckflow.gg/Admin/Feedback`, authenticates via prod BasicAuth, and confirms the eight checks in 06-03-PLAN.md `<how-to-verify>` (sidebar present, Feedback link active, no guild theme leakage, mark-read POST round-trip works, version stamp renders correctly). Failure of any check would be filed as a fast-follow bug, not a re-open of Plan 06-03.
