---
id: SEED-001
status: dormant
planted: 2026-06-17
planted_during: Cycle 8
trigger_when: Cycle 9 starts (Studio expansion / Content KB workflow milestone)
scope: large
---

# SEED-001: KB add/remove videos in Studio + unified publish-tracking (Studio & site)

Deferred from Cycle 8 (hardening-only scope; Studio expansion → Cycle 9+).
Scope **A+B+C**, approved 2026-06-17.

## Why This Matters

Today "published" is implicit across **three gates in two databases**:
`distilled` (Studio) → `approved` (Studio) → **DirectPush** (row now in prod, `is_visible=0`) → **site Publish All** (`is_visible=1`). Nothing records *"this row was pushed to prod on DATE"* or *"local distill is newer than what's live."*

That gap directly caused two incidents this cycle:
- **@salubrioussnail duplicate** — a re-added Studio source forked the creator into two grid rows on prod.
- **Based Deck Department never published** — 20 videos harvested + distilled but stuck `approval_status=pending`, with no surfaced indicator that they'd never reached prod.

Also: Studio has **no remove/block UI** — removing a bad/dup video means dropping to the CLI. The Core methods already exist; they're just unwired.

## When to Surface

**Trigger:** Cycle 9 kickoff (the Studio-expansion / Content-KB-workflow milestone). Surface during `/gsd:new-milestone` when scope mentions Studio, Content KB, harvest/publish workflow, or admin curation.

## Scope Estimate

**Large** — `content_site_index` migration + DirectPush/Publish changes + new Studio UI + derived-status display in two apps. ~Multi-phase.

### Requirements
- **REM-01** — Studio **Block** action: hard-delete video + artifacts AND add to `blocked_videos` (no re-harvest). Reuse Core `BlockVideoAsync`.
- **REM-02** — Studio **Blocked-list** view (`ListBlockedAsync`) + **Unblock** action (`UnblockVideoAsync`). (User wants to SEE blocked + unblock them.)
- **PUB-01** — add nullable `published_utc` to `content_site_index` (dialect-guarded idempotent migration, SQLite + Postgres). DirectPush + git Publish stamp it on each pushed row.
- **PUB-02** — single **derived publish-state** `{ Never published | Pushed-hidden | Published | Local-newer }` from `published_utc` + `is_visible` + max(index/distill time).
- **PUB-03** — show derived status in Studio (Review / Publish pages).
- **SITE-01** — show the same status column on `/Admin/ContentKb`.
- **ADD** — "add" largely covered by Harvest (channel browse + paste-URLs); confirm in planning whether direct add-single-video-by-URL needs polish.

Keep harvest/block Studio-only by design (harvest is local).

## Breadcrumbs

- Core (exists, CLI-only, Phase 37.6): `IContentMaintenanceOrchestrator.BlockVideoAsync/UnblockVideoAsync/ListBlockedAsync`, `IContentVideoStore.DeleteVideoByYoutubeIdAsync`, `BlockedVideoStore` + `blocked_videos` table.
- Schema: `DeckFlow.Core/Content/ContentSiteIndexStore.cs` (`content_site_index`: `approval_status`, `is_visible`, `is_hidden`, `indexed_utc`; NO `published_utc`).
- Studio pages: `DeckFlow.Studio/Pages/{Harvest,Review,Publish,DirectPush}.razor`.
- Site admin: `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` + `Views/AdminContentKb/Index.cshtml`.
- Export/publish source mapping: `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` (`Source = source.DisplayName` ~line 1302; `ExportIndexAsync`, `CopyApprovedArtifactsToRepoAsync`).

## Notes

Full research + decisions in auto-memory: `project_kb_addremove_publishtracking_research.md`. Related: `project_cycle8_contentkb_cleanup.md`, `project_phase52_executed.md`, `followup_harvest_suppress_blocklist.md`.
