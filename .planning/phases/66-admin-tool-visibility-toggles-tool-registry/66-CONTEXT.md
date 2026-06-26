---
phase: 66-admin-tool-visibility-toggles-tool-registry
milestone: Cycle 11 — Security, Visibility Control & Creator-Lens
created: 2026-06-25
gates:
  research: skipped   # internal wiring over existing infra, no new integration
  ui_spec: skipped    # reuses existing admin-table + nav/tile patterns, no net-new visual design
---

# Phase 66 — Admin Tool-Visibility Toggles + Tool Registry

## Goal

Give the admin a single, friendly place to toggle each public tool on/off, backed by
a tool **registry** that unifies the today-scattered per-tool `@if` flag checks across
nav, home tiles, help, and route gating. Disabling a tool makes it fully unreachable
and removes every surface entry — no orphan tab/tile/help/route.

Requirements: TOGGLE-01..07 (see `.planning/REQUIREMENTS.md`).

## Locked Decisions (user, 2026-06-25)

1. **Disabled-route behavior → 404 NotFound.** A toggled-off tool's route returns 404
   (treat as if it doesn't exist), replacing today's 503 maintenance page. This CHANGES
   existing manabase / content-kb / categories gate behavior from 503 → 404. Update the
   gate (`FeatureFlagGateAttribute`) and the three existing gated tools accordingly, plus
   their tests (`ManabaseControllerFlagGateTests`, KB/categories gate tests).

2. **Admin UI → new dedicated `/Admin/Tools` page.** Registry-driven: friendly tool
   names, grouped by nav section (Analyze / Build / Reference / Categories), with a
   core-tool warning. Leave the existing raw-key `/Admin/Flags` page as-is for non-tool
   infra flags (tagger, harvest cron, analysis.reference.*). BasicAuth + anti-forgery +
   SameOrigin, mirror `AdminFlagsController` toggle pattern (calls `ReloadAsync` for
   same-round-trip visibility).

3. **Offline UX → hide everywhere.** When a tool is OFF, hide its home tile, its nav tab,
   AND its help topic together — no placeholder. Drop the categories "Temporarily offline"
   placeholder card and the nav "Suggestions offline" muted span. Close the help-header
   gaps: add `requires_flag` to `content-kb.md` and `category-suggestions.md` (today they
   stay visible when their tool is off).

4. **Core-tool warning → 3 Analyze tools, inline banner.** Mark Deck Analysis, Deck
   Comparison, and cEDH Meta Gap as `core` in the registry. On the `/Admin/Tools` page,
   disabling a core tool shows an inline warning banner (warn-not-block, no JS confirm) —
   the toggle still proceeds. Non-core tools toggle without warning.

## Tool Registry (new abstraction — the heart of this phase)

A single source of truth mapping each public tool to: stable key, friendly name, route,
nav section, feature-flag key, and `core` bool. Nav (`_DeckToolTabs.cshtml`), home tiles
(`Home.cshtml`), help visibility, route gating, and the new `/Admin/Tools` page all read
from this registry instead of hardcoded per-tool `@if` blocks. Reuse existing
`feature_flags` table + `FeatureFlagStore` / `FeatureFlagCache` for persistence and the
lock-free `IsEnabled(key)` read path (default TRUE, D-13). Do NOT rebuild flag infra.

**13 public tools (route | nav section | flag key | core):**
- Deck Analysis `/deck-analysis` Analyze | (new flag) | **core**
- Deck Comparison `/deck-comparison` Analyze | (new flag) | **core**
- cEDH Meta Gap `/cedh-meta-gap` Analyze | (new flag) | **core**
- Mana Base `/manabase` Analyze | `feature.manabase.enabled` | non-core (already gated)
- Deck Sync `/sync` Build | (new flag) | non-core
- Convert Deck `/convert` Build | (new flag) | non-core (no help topic — leave as-is)
- Deck Primer `/deck-primer` Build | (new flag) | non-core
- Card Lookup `/card-lookup` Reference | (new flag) | non-core
- Mechanic Rules `/mechanic-lookup` Reference | (new flag) | non-core (no help topic)
- Ask a Judge `/judge-questions` Reference | (new flag) | non-core
- Knowledge Base `/content-kb` Reference | `content.kb.enabled` | non-core (already gated)
- Category Suggestions `/suggest-categories` Categories | `feature.categories.enabled` | non-core (already gated)
- Category Reference `/commander-categories` Categories | (new flag) | non-core

New flag keys: dotted-lowercase, seeded via existing `ON CONFLICT DO NOTHING`, default
enabled. Planner to settle exact key naming (e.g. `tool.deck-analysis.enabled`) — keep
consistent with existing `feature.*` / `content.kb.*` convention; prefer a single
`tool.<slug>.enabled` namespace for the registry so the new `/Admin/Tools` page can filter
tool flags from infra flags cleanly.

## Scouted Wiring Map (preserved from discuss session — do NOT re-scout)

**Feature-flag infra (reuse):**
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — Postgres/SQLite `feature_flags`
  table (key/enabled/updated_at); seed `ON CONFLICT DO NOTHING`; existing keys:
  `scryfall.tagger.enabled`, `page.help.enabled`, `harvest.cron.enabled`,
  `feature.categories.enabled`, `content.kb.enabled`, `feature.manabase.enabled`,
  `analysis.reference.full-oracle-text`, `analysis.reference.deck-stats(FALSE)`.
- `IFeatureFlagStore`: `GetAllAsync`, `SetEnabledAsync`, `EnsureSchemaAsync`.
- `FeatureFlagCache` (Singleton + IHostedService): lock-free `IsEnabled(key)` → default
  TRUE (D-13); sync load in StartAsync; 30s poll; `ReloadAsync` for same-round-trip.
- `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs` — `[FeatureFlagGate(key,Title,
  Message)]`; **today OFF → 503 + Retry-After:300 + `_MaintenancePage.cshtml`**. Decision 1
  changes this to 404. Resolves cache per-invocation.

**Admin console (existing):**
- `Controllers/Admin/AdminFlagsController.cs` + `Views/AdminFlags/Index.cshtml` at
  `/Admin/Flags` (BasicAuth). POST `/Admin/Flags/{key}/toggle` (anti-forgery + SameOrigin);
  only toggles keys already in snapshot; calls `ReloadAsync`. KEEP for infra flags.

**Nav (hardcoded per-tool @if):** `Views/Shared/_DeckToolTabs.cshtml` — 4 groups. Flag
checks: manabase (hide), content.kb (hide), categories ("Suggestions offline" muted span →
remove per decision 3). Replace hardcoded @ifs with registry iteration.

**Home tiles (hardcoded per-tool @if):** `Views/Deck/Home.cshtml` — same 4 sections.
manabase/KB hide; categories "Temporarily offline" card → remove per decision 3.

**Help (markdown-derived):** `Services/HelpContentService.cs` scans `Help/*.md`;
`HelpTopic.RequiresFlag` optional header drives visibility; `HelpController` 404s hidden
topics. Only `manabase.md` has `requires_flag`. GAPS to close (decision 3): add header to
`content-kb.md` + `category-suggestions.md`. `convert` + `mechanic-lookup` have NO help
topic — leave as-is (out of scope).

**Test patterns to follow:** `FeatureFlagGateAttributeTests.cs`,
`AdminFlagsControllerToggleTests.cs`, `Manabase/ManabaseControllerFlagGateTests.cs`
(reflection lock on gate attrs); `TestDoubles/FakeFeatureFlagStore.cs`,
`FakeFeatureFlagCache.cs` (defaults enabled).

## Blocking Constraint

**Wrong-worktree planning.** Phase 66 exists ONLY on the `cycle11` worktree
(`/mnt/c/users/chrislunt/source/personal/deckflow-cycle11`). Run EVERY Phase 66 GSD command
with that worktree as cwd — `init.plan-phase 66` from main returns `phase_found:false`.

## Out of Scope

- New visual design (reuse existing admin-table + nav/tile/help patterns).
- Help topics for `convert` / `mechanic-lookup` (none exist; not adding here).
- Touching infra flags' raw `/Admin/Flags` page beyond leaving it intact.
