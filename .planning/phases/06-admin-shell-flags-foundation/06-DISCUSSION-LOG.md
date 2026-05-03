# Phase 6: Admin Shell + Flags Foundation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-02
**Phase:** 6-admin-shell-flags-foundation
**Areas discussed:** Admin shell visual + nav, Flag schema + seed + naming, Flag check integration points, Existing feedback + kill-switch demo

---

## Admin shell visual + nav

### Palette

| Option | Description | Selected |
|--------|-------------|----------|
| Dark slate | bg #0f172a, panel #1e293b, text #e2e8f0, accent #3b82f6 (blue-500), border #334155 | ✓ |
| Light gray (admin paper) | bg #f5f5f5, panel white, dark text, indigo accent | |
| Mono (zinc + system fonts) | Pure zinc grayscale, no chromatic accent | |

### Sidebar

| Option | Description | Selected |
|--------|-------------|----------|
| Labels only | Plain text labels, no icons; vertical list of 4 links | ✓ |
| Icon + label | SVG icons + labels | |
| Sectioned w/ headers | Group by Ops / Insight / Config | |

### Active indicator

| Option | Description | Selected |
|--------|-------------|----------|
| Left border bar + bold | 3-4px accent border on left edge of active link, label bold | ✓ |
| Filled background pill | Filled rounded background block | |
| Just bold + accent text color | Minimal — bold + accent color | |

### Chrome

| Option | Description | Selected |
|--------|-------------|----------|
| Thin top bar | Top bar with section H1 + small build/version stamp on right; no footer | ✓ |
| No top bar, no footer | Pure sidebar + content area | |
| Top bar + footer with link to public site | Top bar + tiny footer with 'Back to deckflow.gg' | |

**Notes:** "Logout" appeared in the sidebar preview but BasicAuth has no clean server-driven logout — captured as open item D-06 for the planner.

---

## Flag schema + seed + naming

### Schema

| Option | Description | Selected |
|--------|-------------|----------|
| Minimal: key + bool + updated_at | 3 columns; description deferred to code-side catalog | ✓ |
| Add description column | description TEXT NOT NULL DEFAULT '' | |
| Description + updated_by | adds updated_by TEXT — requires multi-user auth to be useful | |

### Naming

| Option | Description | Selected |
|--------|-------------|----------|
| Dotted namespace | scryfall.tagger.enabled, harvest.cron.enabled, page.help.enabled | ✓ |
| Snake_case flat | tagger_enabled, harvest_cron_enabled | |
| Kebab-case dotted | scryfall.tagger-enabled (mixes separators) | |

### Seed

| Option | Description | Selected |
|--------|-------------|----------|
| scryfall.tagger.enabled | Required by FLAG-04 | ✓ |
| scryfall.spellbook.enabled | Companion kill-switch | |
| harvest.cron.enabled | Pre-seed for Phase 7 | |
| page.kill_switch_demo.enabled | Demo flag for FLAG-05 | |

**Notes:** Seed list expanded after area 4 to also include `page.help.enabled` (FLAG-05 demo target → seed default-on so admin page lists it day-1).

### Refresh / invalidation

| Option | Description | Selected |
|--------|-------------|----------|
| In-process: write → invalidate cache immediately | Sync reload from PG before admin write returns 200 | ✓ |
| Trigger flag on row write, poller picks up next tick | Adds latency up to poller tick | |
| PG NOTIFY/LISTEN | Multi-instance future-proof; overkill on single-instance Render | |

---

## Flag check integration points

### Call site

| Option | Description | Selected |
|--------|-------------|----------|
| Top of service public methods | Each public method short-circuits with empty result when off | ✓ |
| Decorator wrapping the service | Cleaner separation but more DI moving parts | |
| Controller / caller-side check | Most explicit but requires every caller to remember | |

### API shape

| Option | Description | Selected |
|--------|-------------|----------|
| Stringly-typed IsEnabled(name) | _flags.IsEnabled("scryfall.tagger.enabled") | ✓ |
| Static FlagKeys constants + IsEnabled(name) | Catches typos, central registry | |
| Strongly-typed accessor per flag | Most type-safe but breaks "add flag from admin without ship" | |

### Fallback

| Option | Description | Selected |
|--------|-------------|----------|
| Default-on (return true) | Aligns with FLAG-01 — missing flag means shipped feature stays on | ✓ |
| Default-off (return false) | Conservative; bad fit for FLAG-01's intent | |
| Throw KeyNotFoundException | Forces explicit handling everywhere | |

### Cache load

| Option | Description | Selected |
|--------|-------------|----------|
| Synchronous initial load before app starts serving | Blocking load in StartAsync before Kestrel binds | ✓ |
| Lazy: first read triggers a sync load | First user pays small extra latency | |
| Async init + default-on fallback covers the gap | Cache populates on first poll tick | |

**Notes:** WARN-on-missing-key (D-13) must be de-duped per key (log-once-per-process) to avoid log flood under hot traffic.

---

## Existing feedback + kill-switch demo

### Feedback migration

| Option | Description | Selected |
|--------|-------------|----------|
| Layout swap only | Keep route + folder; just set Layout = '_AdminLayout' | ✓ |
| Move views to Views/Admin/Feedback/ | Tidier structure but cosmetic | |
| Rename controller AdminFeedback → FeedbackAdminController | Most invasive, breaks bookmarks | |

### Demo target

| Option | Description | Selected |
|--------|-------------|----------|
| /help | Real user-facing page, low blast radius. Flag: page.help.enabled | ✓ |
| /about | Even lower-stakes; weaker demo | |
| Card lookup (/lookup) | Highest-stakes; risky if accidentally flipped | |
| Dedicated demo route /Admin/flag-demo | Cleanest separation but doesn't prove on real page | |

### 503 shape

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated _MaintenancePage view | Reusable Razor partial with title + message; returns 503 | ✓ |
| Plain 503 with default text | ASP.NET default page; reads as "site broken" | |
| JSON 503 + client-side render | Inconsistent with MVC site | |

### Wiring

| Option | Description | Selected |
|--------|-------------|----------|
| Action filter / attribute on action method | [FeatureFlagGate("page.help.enabled", title: "...")] reusable | ✓ |
| Inline check at top of action method | Explicit but copy-paste boilerplate | |
| Middleware mapping route→flag | Heaviest; route-flag config drift risk | |

---

## Claude's Discretion

- Exact `Retry-After` value on 503 maintenance response (default 300s).
- Whether `IFeatureFlagCache` registers as `IHostedService` for synchronous initial load OR via `IHostApplicationLifetime`-hooked initializer.
- Whether `_AdminLayout.cshtml` lives under `Views/Shared/` or `Views/Admin/`.
- Source of build/version stamp (IConfiguration / Assembly attribute / startup timestamp) — pick the one that doesn't add a Dockerfile change.

## Deferred Ideas

- Sidebar status badges (POLISH-01)
- Feature-flag audit log (POLISH-02)
- Non-bool flag types (POLISH-03)
- Sidebar collapse / breadcrumb / mobile admin nav (POLISH-04)
- Multiple gated pages beyond `/help`
- PG NOTIFY/LISTEN for multi-instance cache invalidation
- Per-flag description column in admin UI (use code-side `FlagCatalog` if needed)
