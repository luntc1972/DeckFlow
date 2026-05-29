---
plan: 07-05
phase: 07
title: Live status AJAX + admin-harvest.ts polling
wave: 4
status: complete
shipped: 2026-05-03
requirements: [HARV-01, HARV-03]
---

# Plan 07-05 — Status AJAX + Browser Polling

## What shipped

`GET /Admin/Harvest/status` JSON endpoint and `admin-harvest.ts` browser module that polls it every 3 seconds. Together they deliver HARV-01's "operator sees live job state" requirement and HARV-03's "Stopping → Cancelled within 30s" feedback path.

## Files modified

| File | Change |
|------|--------|
| `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` | +1 ctor dep (`IMemoryCache`), +1 ctor dep (`SameOriginRequestValidator` if not already), +1 GET action `Status` with same-origin gate + 1s IMemoryCache lookup, +1 sealed record `HarvestStatusPayload` |
| `DeckFlow.Web/wwwroot/ts/admin-harvest.ts` | NEW — strict TS module, 3s `setTimeout` recursion, native `fetch` with `credentials: 'same-origin'`, `AbortController` 10s timeout, stops on terminal state and reloads page so recent-runs refreshes |
| `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` | +`<script src="~/js/admin-harvest.js" asp-append-version="true">`; status block exposes `data-state` for TS hydration; existing `<noscript>` meta-refresh fallback intact |

## Decisions honored

| ID | Decision | Where |
|----|----------|-------|
| D-08 | AJAX poll every 3s while state ∈ {Queued, Running, Stopping}; `<noscript>` fallback for no-JS users | `admin-harvest.ts` polling loop + `Index.cshtml` |
| RESEARCH discretion | Status GET uses 1s `IMemoryCache` TTL (RESEARCH Q resolved — anywhere from 0–2s; 1s picked) | `Status` action `entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1)` |
| RESEARCH discretion | Steady 3s poll cadence — no adaptive 1s during cancel | `admin-harvest.ts` |

## Security gates

- `SameOriginRequestValidator.IsValid(Request)` runs BEFORE any other work; returns 403 with `{ Message }` on failure (mirrors existing API pattern in `ArchidektCacheJobsController`).
- BasicAuth gate already wired via `Program.cs` `MapWhen("/Admin")` — unchanged.
- GET endpoint deliberately omits `[ValidateAntiForgeryToken]` (antiforgery is for POSTs).

## Acceptance gates (all pass)

```
✓ HttpGet("status") in controller
✓ same-origin gate present
✓ admin.harvest.status.v1 cache key
✓ TimeSpan.FromSeconds(1) TTL
✓ admin-harvest.ts file exists
✓ /Admin/Harvest/status fetched
✓ setTimeout used (recursion, not setInterval)
✓ fetch native API
✓ credentials: 'same-origin'
✓ Index.cshtml loads admin-harvest.js
```

`dotnet build DeckFlow.sln` → 0 Warning(s), 0 Error(s).

## Commits

- `a929ff8` feat(07-05): live status AJAX + admin-harvest.ts 3s poll (HARV-01, HARV-03)

## Routing

Code authored via Codex MCP at `gpt-5.4` (full) per the global model-selection rule. Main thread committed after clean build verification.
