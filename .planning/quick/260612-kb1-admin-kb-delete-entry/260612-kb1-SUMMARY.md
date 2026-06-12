---
quick_id: 260612-kb1
slug: admin-kb-delete-entry
date: 2026-06-12
status: complete
---

# Quick Task 260612-kb1: Admin KB per-entry Delete — Summary

## Ask

User: no UI to delete videos from the KB; admin KB page is noisy (too many
videos to read through when enabling/disabling). Decisions (this session):
block+delete intent, per-entry, quick build.

## What shipped

1. **Per-entry Delete** on `/Admin/ContentKb`: a `DeleteEntry` POST action
   (anti-forgery + SameOrigin double guard, mirrors `SetVisibility`) calling
   `IContentSiteIndexStore.DeleteByIdAsync`, plus a danger two-click Delete button.

2. **Phase 37 regression fix (discovered mid-task):** the retire commit `0e91a29`
   deleted `wwwroot/ts/content-kb-admin.ts` and dropped its `<script>` include,
   leaving only a gitignored orphan `.js`. On prod `/Admin/ContentKb` this had
   silently broken: the **entry filter** (the user's actual pain), the two-click
   confirms (Hide All + Delete), the reload-from-seed confirm, the toast, and
   scroll restore — the view still had every hook but nothing wired them.
   Recreated `content-kb-admin.ts` (strict-TS port of the orphan, minus the retired
   commander-preview helpers) and re-added the include.

## Architecture note (locked decision)

Web admin curates `content-site-index.db` (`content_site_index`) only. The CLI
block + hard-delete (Phase 37.6) operate on a DIFFERENT db (`content-kb.db`:
content_videos/clips/blocked_videos) the deployed web never sees. So a web
"block (never re-harvest)" is NOT reachable — the web Delete removes the
site-index row only; permanent re-harvest suppression stays the CLI `block-video`.
A republish/local-reharvest can re-add a deleted row.

## Files

- `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` — DeleteEntry action
- `DeckFlow.Web/Views/AdminContentKb/Index.cshtml` — Delete button + script include
- `DeckFlow.Web/wwwroot/ts/content-kb-admin.ts` — recreated (filter/confirms/toast/scroll)
- `DeckFlow.Web/ts-tests/content-kb-admin-twoclick.test.ts` — new (2 tests)
- `DeckFlow.Web.Tests/AdminContentKbControllerTests.cs` — +2 DeleteEntry tests
- `DeckFlow.Web.Tests/TestDoubles/FakeContentSiteIndexStore.cs` — DeletedIds tracking

## Verification

- `dotnet build` (web + tests) → 0/0; `tsc --noEmit` → 0
- xUnit AdminContentKbControllerTests → 11/11; Vitest → 15/15
- Live (gstack, desktop): filter narrows (budget → 3 of 51); Delete arms on 1st
  click (no-op) then deletes on 2nd (banner "Entry deleted."); reload modal
  confirms + restored the test-deleted row. No console errors.

## Delegation

Codex (gpt-5.4 medium) implemented across 3 dispatches; Claude planned, reviewed,
caught the missing `data-confirm-label` + the Phase 37 regression, and verified live.

## Follow-ups

- Delete button is functional but other admin pages may have lost behaviors in the
  same Phase 37 retire — not audited here.
- Consider a CLI-parity "block from web" only if the harvest DB is ever co-located.
