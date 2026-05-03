---
phase: 07-harvest-controls-stats
plan: 05
type: execute
wave: 4
depends_on: [04]
files_modified:
  - DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs
  - DeckFlow.Web/wwwroot/ts/admin-harvest.ts
autonomous: true
requirements: [HARV-01, HARV-03]
tags: [harvest, ajax, typescript, same-origin, live-status]

must_haves:
  truths:
    - "GET /Admin/Harvest/status returns JSON with the active run row (or null) under same-origin gate (D-08)"
    - "Status response is cached in IMemoryCache for 1 second to absorb tight polling under multiple browser tabs (D-01, D-08, RESEARCH Q#2)"
    - "admin-harvest.ts polls /Admin/Harvest/status every 3000ms while state ∈ {Queued, Running, Stopping}; stops on terminal states (D-08)"
    - "On non-OK response the TS module silently bails (the noscript meta-refresh handles it) (D-08)"
    - "Same-origin gate via SameOriginRequestValidator early-return 403 (S-2)"
  artifacts:
    - path: "DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs"
      provides: "Adds [HttpGet(\"status\")] action returning JSON with active-run snapshot, same-origin gated, IMemoryCache-cached for 1s"
      contains: "[HttpGet(\"status\")]"
    - path: "DeckFlow.Web/wwwroot/ts/admin-harvest.ts"
      provides: "Browser TS module: setTimeout-recursion poller against /Admin/Harvest/status; updates DOM in the data-harvest-status root"
      contains: "data-harvest-status"
  key_links:
    - from: "admin-harvest.ts poll loop"
      to: "GET /Admin/Harvest/status"
      via: "fetch + JSON + setTimeout(poll, 3000) while non-terminal"
      pattern: "fetch.*Admin/Harvest/status"
    - from: "AdminHarvestController.Status"
      to: "IHarvestRunStore.GetActiveAsync via IMemoryCache 1s TTL"
      via: "GetOrCreateAsync('admin.harvest.status.v1')"
      pattern: "admin.harvest.status.v1"
---

<objective>
Land HARV-01's "operator sees live job state" requirement and HARV-03's "Stopping → Cancelled within 30s" feedback path. Adds the AJAX status endpoint to `AdminHarvestController` plus the browser-side TS module that polls it every 3 seconds while the run is active.

Purpose: closes the loop on Plan 04's "data-harvest-status" hook. Without this plan, operators must manually refresh after clicking Run Now or Cancel; with it, the live state and deck count update inline within 3 seconds.

Output:
- New `[HttpGet("status")]` action on `AdminHarvestController` returning JSON with active-run snapshot. Same-origin gated. Cached 1 second in `IMemoryCache` under key `admin.harvest.status.v1`.
- New `wwwroot/ts/admin-harvest.ts` module that polls the endpoint and updates the `[data-harvest-status]` DOM block.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/phases/07-harvest-controls-stats/07-CONTEXT.md
@.planning/phases/07-harvest-controls-stats/07-PATTERNS.md
@.planning/phases/07-harvest-controls-stats/07-04-SUMMARY.md
@DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs
@DeckFlow.Web/Controllers/Api/ArchidektCacheJobsController.cs
@DeckFlow.Web/Security/SameOriginRequestValidator.cs
@DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs
@DeckFlow.Web/Services/Harvest/HarvestRunModels.cs
@DeckFlow.Web/wwwroot/ts/category-suggestions.ts
@DeckFlow.Web/Views/AdminHarvest/Index.cshtml
@DeckFlow.Web/tsconfig.json

<interfaces>
<!-- Wire-format the TS module consumes — keep stable. -->

JSON response shape from GET /Admin/Harvest/status:
```json
{
  "active": null  // when no active run
}
```
or:
```json
{
  "active": {
    "id": "guid-string",
    "kind": "Bulk",
    "state": "Running",
    "startedUtc": "2026-05-03T08:30:00Z",
    "decksProcessed": 42,
    "additionalDecksFound": 0,
    "errorMessage": null
  }
}
```

`state` is one of: `"Queued"`, `"Running"`, `"Stopping"`, `"Succeeded"`, `"Failed"`, `"Cancelled"` (matches `HarvestRunState.ToString()`). Terminal states stop the poll.
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Add [HttpGet("status")] action to AdminHarvestController with same-origin gate + 1s IMemoryCache</name>
  <files>DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs</files>
  <behavior>
    - Inject `IMemoryCache` into the controller (DI already registered in Program.cs via `AddMemoryCache()`).
    - New action `[HttpGet("status")]` returning `Task<IActionResult>`. Same-origin gate first (early 403 on miss). Cache key `admin.harvest.status.v1`. TTL `TimeSpan.FromSeconds(1)`.
    - Cache value is the projection (anonymous-typed-friendly POCO) so we don't accidentally cache a `HarvestRunRow` reference whose downstream JSON property names break the contract.
    - Returns `Json(new { active = ... })`. Field names use camelCase (System.Text.Json default) — keep them stable: `id`, `kind`, `state`, `startedUtc`, `decksProcessed`, `additionalDecksFound`, `errorMessage`.
    - Action accepts `CancellationToken cancellationToken`; passes through to `_runStore.GetActiveAsync`.
  </behavior>
  <action>
    Open `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` and:
    1. Add `using DeckFlow.Web.Security;` and `using Microsoft.Extensions.Caching.Memory;` to the imports.
    2. Add a private field `private readonly IMemoryCache _memoryCache;` and update the constructor signature to accept `IMemoryCache memoryCache`. `ArgumentNullException.ThrowIfNull(memoryCache);` and assign. Update the existing DI registration in tests (none needed — DI handles it).
    3. Add a private const `private const string StatusCacheKey = "admin.harvest.status.v1";` near the top of the class.
    4. Add the new action immediately after `Index`:
       ```csharp
       [HttpGet("status")]
       public async Task<IActionResult> Status(CancellationToken cancellationToken)
       {
           if (!SameOriginRequestValidator.IsValid(Request))
           {
               return StatusCode(StatusCodes.Status403Forbidden,
                   new { Message = SameOriginRequestValidator.GetForbiddenMessage() });
           }

           var payload = await _memoryCache.GetOrCreateAsync(StatusCacheKey, async entry =>
           {
               entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
               var active = await _runStore.GetActiveAsync(cancellationToken);
               if (active is null)
               {
                   return new HarvestStatusPayload(Active: null);
               }
               return new HarvestStatusPayload(new HarvestStatusActive(
                   Id: active.Id.ToString("D"),
                   Kind: active.Kind.ToString(),
                   State: active.State.ToString(),
                   StartedUtc: active.StartedUtc?.ToString("u"),
                   DecksProcessed: active.DecksProcessed,
                   AdditionalDecksFound: active.AdditionalDecksFound,
                   ErrorMessage: active.ErrorMessage));
           });

           return Json(payload);
       }

       private sealed record HarvestStatusPayload(HarvestStatusActive? Active);
       private sealed record HarvestStatusActive(
           string Id,
           string Kind,
           string State,
           string? StartedUtc,
           int DecksProcessed,
           int AdditionalDecksFound,
           string? ErrorMessage);
       ```
    5. Verify the existing controller actions still compile.

    Notes:
    - `ASP.NET Core` System.Text.Json serializer lowercases first letter for record properties tagged with `init` properties — confirm by inspecting the JSON output via a smoke test, or explicitly add `[JsonPropertyName("active")]` etc. if the default mapping is uppercase. The default `JsonOptions` registered in `Program.cs` should yield camelCase; if the build emits PascalCase, add `[System.Text.Json.Serialization.JsonPropertyName(...)]` attributes to each property to force camelCase.
    - The `DurationSeconds` field is intentionally omitted from the JSON to keep the wire payload small; the TS module doesn't display it.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -q "[HttpGet(\"status\")]" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q "SameOriginRequestValidator.IsValid(Request)" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q "admin.harvest.status.v1" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q "TimeSpan.FromSeconds(1)" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs</automated>
  </verify>
  <done>Build exits 0; status route registered; same-origin gate in place; cache key literal `admin.harvest.status.v1` present; 1-second TTL set.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: TypeScript module wwwroot/ts/admin-harvest.ts — 3s setTimeout poll loop</name>
  <files>DeckFlow.Web/wwwroot/ts/admin-harvest.ts</files>
  <behavior>
    - IIFE module, strict mode. No imports (matches `category-suggestions.ts` pattern; `module: "none"` in tsconfig).
    - Constants: `POLL_INTERVAL_MS = 3000`, `TERMINAL_STATES = new Set(['Succeeded', 'Failed', 'Cancelled'])`.
    - On `DOMContentLoaded`: query `[data-harvest-status]`. If absent, return. Read `dataset.state`; if state is non-terminal (not in TERMINAL_STATES, including the `Idle` placeholder which won't trigger because Idle is not a real state), schedule the first poll.
    - `poll()` async: fetch `/Admin/Harvest/status` GET; on `!res.ok` silently return (noscript meta-refresh covers it). Parse JSON. If `data.active` is null → render Idle, stop polling. Else render state + decksProcessed + startedUtc into the existing DOM children, then if `!TERMINAL_STATES.has(data.active.state)` schedule another poll via `window.setTimeout(poll, POLL_INTERVAL_MS)`.
    - Render strategy: read existing child spans inside `[data-harvest-status]` by class (`.admin-harvest__state`, `.admin-harvest__decks`, `.admin-harvest__started`) and update their textContent. If the children are missing (defensive), set the root's textContent to a minimal one-line summary.
    - All errors caught (`try/catch`) and ignored — the `noscript` meta-refresh handles full failure modes.
    - File compiles to `wwwroot/js/admin-harvest.js` automatically via the existing `CompileTypeScriptAssets` MSBuild target. No csproj edit.
  </behavior>
  <action>
    Create `DeckFlow.Web/wwwroot/ts/admin-harvest.ts`:
    ```typescript
    ((): void => {
        'use strict';

        const POLL_INTERVAL_MS = 3000;
        const TERMINAL_STATES = new Set<string>(['Succeeded', 'Failed', 'Cancelled']);

        type HarvestStatusActive = {
            id: string;
            kind: string;
            state: string;
            startedUtc: string | null;
            decksProcessed: number;
            additionalDecksFound: number;
            errorMessage: string | null;
        };

        type HarvestStatusPayload = { active: HarvestStatusActive | null };

        const setText = (root: HTMLElement, selector: string, value: string): void => {
            const el = root.querySelector<HTMLElement>(selector);
            if (el) {
                el.textContent = value;
            }
        };

        const render = (root: HTMLElement, payload: HarvestStatusPayload): void => {
            if (payload.active === null) {
                root.dataset.state = 'Idle';
                setText(root, '.admin-harvest__state', 'Idle');
                setText(root, '.admin-harvest__decks', '');
                setText(root, '.admin-harvest__started', '');
                return;
            }
            const a = payload.active;
            root.dataset.state = a.state;
            setText(root, '.admin-harvest__state', a.state);
            setText(root, '.admin-harvest__decks', `decks=${a.decksProcessed}`);
            setText(root, '.admin-harvest__started', `started=${a.startedUtc ?? '—'}`);
        };

        const poll = async (root: HTMLElement): Promise<void> => {
            try {
                const res = await fetch('/Admin/Harvest/status', { method: 'GET' });
                if (!res.ok) {
                    return; // noscript meta-refresh covers full-page recovery.
                }
                const data = (await res.json()) as HarvestStatusPayload;
                render(root, data);
                const stillActive = data.active !== null && !TERMINAL_STATES.has(data.active.state);
                if (stillActive) {
                    window.setTimeout(() => { void poll(root); }, POLL_INTERVAL_MS);
                }
            } catch {
                // Network glitch — let the noscript meta-refresh handle long-term failure.
            }
        };

        document.addEventListener('DOMContentLoaded', () => {
            const root = document.querySelector<HTMLElement>('[data-harvest-status]');
            if (!root) {
                return;
            }
            const initial = root.dataset.state ?? 'Idle';
            if (initial !== 'Idle' && !TERMINAL_STATES.has(initial)) {
                window.setTimeout(() => { void poll(root); }, POLL_INTERVAL_MS);
            }
        });
    })();
    ```

    Build runs `tsc -p tsconfig.json` automatically via the `CompileTypeScriptAssets` target. After `dotnet build`, verify `wwwroot/js/admin-harvest.js` exists.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && test -f DeckFlow.Web/wwwroot/ts/admin-harvest.ts && test -f DeckFlow.Web/wwwroot/js/admin-harvest.js && grep -q "POLL_INTERVAL_MS = 3000" DeckFlow.Web/wwwroot/ts/admin-harvest.ts && grep -q "/Admin/Harvest/status" DeckFlow.Web/wwwroot/ts/admin-harvest.ts && grep -q "TERMINAL_STATES" DeckFlow.Web/wwwroot/ts/admin-harvest.ts && grep -q "data-harvest-status" DeckFlow.Web/wwwroot/ts/admin-harvest.ts</automated>
  </verify>
  <done>Build exits 0; both `.ts` source and `.js` compiled output exist; constants and selectors wire to the Plan 04 view's `[data-harvest-status]` element; terminal-state set is `{Succeeded, Failed, Cancelled}`.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Browser → /Admin/Harvest/status | BasicAuth-gated by /Admin path branch; same-origin gate further blocks cross-origin XHR. |
| Controller → IMemoryCache | In-process; cache value is a sealed POCO copy of the run row, no reference leakage. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-07-22 | Information disclosure | Status JSON includes errorMessage | accept | Operator-only after BasicAuth + same-origin; admin debugging requires the message. |
| T-07-23 | CSRF | GET /Admin/Harvest/status | mitigate | Same-origin gate (Origin/Referer must match host) returns 403 to cross-origin XHR. |
| T-07-24 | Denial of service | Tight polling under multiple tabs | mitigate | 1-second IMemoryCache TTL absorbs concurrent requests; PG sees ≤ 1 query/sec regardless of tab count. |
| T-07-25 | XSS via TS rendering | render(root, payload) | mitigate | All DOM updates use `textContent`, never `innerHTML`; `state` and `decksProcessed` are server-issued enum/int values. |
| T-07-26 | Spoofing | TS module assumes server JSON shape | accept | Controller and TS share the wire format; type assertion is a compile-time safety net only. |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` exits 0.
- `grep -c "[HttpGet(\"status\")]" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` ≥ 1.
- `grep -c "admin.harvest.status.v1" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` ≥ 1.
- `grep -c "TimeSpan.FromSeconds(1)" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` ≥ 1.
- `test -f DeckFlow.Web/wwwroot/js/admin-harvest.js` (compiled output exists post-build).
- `grep -c "POLL_INTERVAL_MS = 3000" DeckFlow.Web/wwwroot/ts/admin-harvest.ts` ≥ 1.
- `grep -c "/Admin/Harvest/status" DeckFlow.Web/wwwroot/ts/admin-harvest.ts` ≥ 1.
</verification>

<success_criteria>
- Operator clicking Run Now sees the live state + deck count update within 3 seconds without a page reload.
- Operator clicking Cancel sees state transition to `Stopping` within 1 second (Plan 04 writes the row synchronously) and to `Cancelled` within 30 seconds (next deck-loop OCE landing — ROADMAP SC #3).
- Status endpoint returns 403 to cross-origin requests.
- No-JS users still see live state via the `<noscript>` meta-refresh fallback (every 5 seconds, slightly slower than the JS path).
</success_criteria>

<output>
After completion, create `.planning/phases/07-harvest-controls-stats/07-05-SUMMARY.md` covering: the JSON payload shape (exact field names), confirmation the TS file compiled to JS, and any property-naming attribute additions if camelCase needed enforcing.
</output>
