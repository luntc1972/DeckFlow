---
quick_id: 260612-e2e
slug: playwright-smoke
date: 2026-06-12
status: complete
---

# Quick Task 260612-e2e: Playwright E2E smoke suite — Summary

## Why

Site had unit (Vitest jsdom) + controller (xUnit) coverage but ZERO whole-site
UI/E2E tests. Every UI bug this session needed the real rendered page + compiled
JS + a click to catch. Added a minimal Playwright smoke wired into CI.

## What shipped

- `@playwright/test` 1.60.0 (chromium) devDependency + `e2e` / `e2e:install` scripts.
- `playwright.config.ts`: desktop (1280×900) + mobile (390×844) chromium projects;
  admin `httpCredentials` with `send:'always'`; `webServer` runs `dotnet run`
  (`reuseExistingServer` locally, launches in CI).
- `e2e/smoke.spec.ts`: 15 public routes × 2 viewports → HTTP ok, no console/page
  errors, no horizontal overflow.
- `e2e/scripts.spec.ts`: per-page `<script>` presence (catches the Phase 37
  dropped-include class). Admin asserts only its `_AdminLayout` section scripts.
- `e2e/interactions.spec.ts`: Primer controls present; admin filter narrows rows;
  admin Delete arms on first click without submitting (two-click safety).
- CI: Playwright stage after Vitest (install + `npm run e2e`, report on failure).
- `.gitignore`: playwright outputs.

## Verification

- Local: **68/68 green** at both viewports against the seeded artifacts DB
  (server started by Claude, suite reused it).
- tsc still 0 (e2e/ is outside the `wwwroot/ts/**`-scoped project tsconfig).

## Gotchas found + resolved (for next time)

1. **Admin uses `_AdminLayout`** — no public layout trio (site.js/df-select/
   df-typeahead). scripts.spec must assert only the admin section scripts.
2. **BasicAuth brute-force throttle** (`admin_brute_force_buckets` in feedback.db,
   15-min window): Playwright's default reactive auth fires a 401 challenge per
   admin request, and 401s count toward the throttle → parallel workers + repeated
   dev runs trip a 429. Fix: `httpCredentials.send:'always'` (proactive, no 401).
   The throttle is DB-persisted (survives server restart); clear with
   `DELETE FROM admin_brute_force_buckets` after stopping the server if locked.
3. Local WSL has no `dotnet` on PATH — start the server first (dotnet.exe) and let
   `reuseExistingServer` attach; CI launches `dotnet` directly.

## Delegation

Codex (gpt-5.4 medium) authored config + specs + CI; Claude planned, reviewed,
fixed the admin-layout script assertion + added `send:'always'`, and verified live.

## Follow-ups

- Interactions are data-light by design (no Scryfall/Moxfield generate flows). The
  copy/scroll/busy-overlay behaviors are covered by Vitest unit tests; consider a
  seeded fixture if deeper admin/browse E2E is wanted later.
- In CI the content KB grid may be empty (no seed) → the delete-arm test skips
  gracefully; seed a row if that coverage must run in CI.
