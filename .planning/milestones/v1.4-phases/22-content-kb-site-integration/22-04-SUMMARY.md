---
phase: 22-content-kb-site-integration
plan: 04
status: code-complete
implementer: claude
requirements: [KB-09]
commits:
  - 79c62f4 feat(content): admin KB curation controller + same-origin guard on AdminFlags.Toggle
  - 62ba4d7 feat(content): admin KB curation view, scoped CSS, sidebar link, confirm TS
---

# Plan 22-04 Summary — Admin Content KB Curation

## What shipped

Admin curation surface at `/Admin/ContentKb` (BasicAuth, `.admin-shell`):

- **AdminContentKbController** — `Index` (grid over ALL rows + status panel + per-source
  groups), `SetVisibility`, `BulkSetVisibility`, `ReloadSeed`. Every mutating POST is
  double-CSRF-guarded: `[ValidateAntiForgeryToken]` + `SameOriginRequestValidator.IsValid`
  as the first statement (returns 403 before any state change).
- **AdminFlagsController.Toggle CSRF fix (HIGH-4 / D-22E closed)** — added the same-origin
  guard as the first statement of `Toggle` (alongside its existing anti-forgery token).
  Insert-only; the key-snapshot validation + store write are unchanged.
- **AdminContentKbViewModel** — `KbIndexStatus` (TotalCount / PublishedCount / SourceCount /
  IndexGeneratedUtc / FlagEnabled), `KbSourceGroup`, `KbEntryRow`. All `{ get; init; }`.
- **Index.cshtml** — status panel (counts + "Index generated: …" honest label + flag toggle
  form + reload button), per-source bulk grid, per-entry publish/unpublish grid, success
  banner, empty state. Every mutating form carries `@Html.AntiForgeryToken()` (5 tokens).
- **_AdminLayout.cshtml** — "Content KB" sidebar link (insert-only).
- **admin-common.css / admin-mobile.css** — KB rules ALL scoped under `.admin-shell`
  (`.kb-status`, `.kb-status--published/--hidden`, `.kb-tag`, `.admin-kb-*`). Zero theme
  bleed; mobile card-stack title-wrap delta.

## D-22D honest label

Status timestamp = `rows.Max(r => r.IndexedUtc)`, exposed as `IndexGeneratedUtc`, labeled
"Index generated" in the view. No `LastLoaded` field/label anywhere (grep → 0). No new
schema/column/status-row.

## Acceptance gates (all green)

- `dotnet build DeckFlow.Web` — **Build succeeded**, 0 errors/warnings.
- CSRF combined gate **4/4/4**: AdminContentKb 3 `[HttpPost]` / 3 `[ValidateAntiForgeryToken]`
  / 3 `SameOriginRequestValidator.IsValid`; AdminFlags.Toggle now 1 + 1 (double-guarded).
- View anti-forgery tokens: 5 (≥4).
- Honest timestamp: "Index generated" present; "LastLoaded"/"Last loaded" → 0.
- Theme bleed: `.kb-` in site.css / admin.css → none; every KB rule `.admin-shell`-scoped.
- TypeScript compiled to `wwwroot/js/content-kb-admin.js` via the MSBuild target.

## DEVIATION — content-kb-admin.ts (added file, not in plan's allowed_files)

The plan's Task-2 allowed set listed no TS/JS file, but Surface 3 requires a reload-confirm
modal and a two-click "Hide All" confirm. The site CSP is `script-src 'self'` (no
`unsafe-inline`), so inline `<script>` is blocked — a compiled module is mandatory to deliver
those behaviors. Added `wwwroot/ts/content-kb-admin.ts` (mirrors `admin-feedback.ts`: IIFE,
`module: "none"`, reuses `window.DeckFlowAdminModal.showConfirm`). Without it the forms still
function (native submit, CSRF intact) but lose the confirmation UX the UI-SPEC specifies.
This is a necessary, scoped addition — flagged here per the scope-fence rule.

Compiled JS is gitignored (`.gitignore:13` ignores `wwwroot/js/*.js`); Docker compiles TS at
build (`CompileTypeScriptAssets` target + `dotnet publish`). The `.ts` is the tracked source
of truth — `.js` intentionally not committed.

## Implementer note

Per user override ("cancel codex and you finish coding"), Claude implemented this plan
directly (Codex had already completed 22-03 before the cancel). Independent cavecrew-reviewer
pass on the uncommitted diff returned **no issues** (CSRF guards pre-mutation, flag route
correct, required members set, TS loop-safe, CSS scoped).

## Pending — Task 3 human UAT checkpoint (blocking)

Requires the dev server (user-started). Verify: curation round-trip, CSRF-negative on all 4
POSTs incl. the flag toggle (HIGH-4), reload-preserves-curation (Pitfall 1 live), honest
timestamp, 375px card-stack + zero theme bleed. Combined with the deferred 22-03 Task-4 UAT.

## Postgres note (carryover MED, accepted)

is_visible bool path is covered by DDL substring checks in Plan 01 only (no Npgsql
read/write fixture; no Postgres test harness). Accepted per round-2 disposition.
