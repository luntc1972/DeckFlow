# 31-06 SUMMARY — Deck Primer Controller + Page Wiring

**Status:** COMPLETE — all 3 tasks; human visual-verify APPROVED 2026-06-09
**Requirements:** PRM-02, PRM-03, PRM-04, PRM-10, PRM-11, PRM-12
**Wave:** 4 (`depends_on: ["31-03", "31-04", "31-05"]`)

## What shipped

- **Task 1 — controller + nav wiring**
  - `DeckController` now injects `IDeckPrimerPacketService` and exposes `GET /deck-primer`, `POST /deck-primer`, `POST /deck-primer/download`, and `POST /deck-primer/upload`.
  - The GET path defaults `TargetCommanderBracket` to the confirmed catalog key `Optimized`, so the no-JS page render has a non-empty preset on first load.
  - The POST render path shows the **selected** AI platform prompt via `PromptTextsByPlatform.GetValueOrDefault(AiPlatform.Normalize(request.TargetAiPlatform).Key)`.
  - The download path builds a primer zip with **all enabled variants** by passing the ChatGPT / Claude / Gemini prompt values separately into `PacketArtifactStore.BuildPrimerZip(...)`.
  - `_DeckToolTabs.cshtml` now includes **Deck Primer** as the 4th Analyze peer and marks Analyze active when `DeckPageTab.DeckPrimer` is selected.

- **Task 2 — page + TS + CSS**
  - Added `DeckPrimer.cshtml` with the import flow, shared `_AiSelector` reuse, bracket selector, 5 collapsible primer groups, per-section help text, prompt-size warning, and download/upload actions.
  - The bracket selector emits **per-bracket presets** via per-option `data-preset-ids`, plus `CedhOnlySectionIds` / `CasualOnlySectionIds` data for client gating.
  - Added `primer-selection.ts` as an IIFE strict-TS module that persists selections per bracket in `localStorage`, restores saved sets when present, applies the destination bracket's preset on first visit, enforces cEDH/casual gating, updates `N/M sections selected` badges, and injects hidden `SelectedSectionIds` fields on submit.
  - Added primer-specific layout and badge rules to `site-common.css` only.

## Deviations

- **Task 3 was intentionally not started.** Human visual verification remains pending per the execution fence.
- **No new automated tests were added in this wave.** The user scope fence limited code changes to the controller/view/TS/CSS files plus this summary file, so TDD-style test additions were not in-bounds.
- **Pre-existing unrelated worktree changes remain untouched.** The existing spike text modifications and listed untracked directories/files were left alone.

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -warnaserror:CS1591` → Build succeeded, 0 errors.
- `DeckFlow.Web/wwwroot/js/primer-selection.js` exists after build → **TS-COMPILED**.
- Acceptance greps passed for:
  - `IDeckPrimerPacketService`, `/deck-primer`, `PromptTextsByPlatform`, `BuildPrimerZip`, and `LoadPrimerFromZip` in `DeckController.cs`
  - `DeckPrimer` nav wiring in `_DeckToolTabs.cshtml`
  - `_AiSelector`, `PrimerSectionCatalog.Groups`, `primer-group__badge`, `HelpText`, and `data-preset-ids` in `DeckPrimer.cshtml`
  - `deckflow.primer.sections.`, `presetForBracket`, and `try/catch` localStorage guards in `primer-selection.ts`
  - `primer-group` CSS in `site-common.css`
- `git status` after the build showed no staged or tracked `wwwroot/js/*.js` output.

## Task 3 — human visual-verify (APPROVED 2026-06-09)

Claude-driven headless-browser UAT at desktop (1280px) + mobile (390px); user approved. All checkpoint items passed:

- Deck Primer tab active → `/deck-primer`; paste import accepted.
- `_AiSelector` shows ChatGPT + Claude (Gemini gated off; `DECKFLOW_GEMINI_ENABLED` unset).
- **D-3 per-bracket presets:** fresh profile → direct switch to unvisited cEDH applies cEDH's own preset (30 sections); → Core applies Core's preset (10). Gating: #24/#25 cEDH-only enabled under cEDH + disabled under Core; #26 casual-only the reverse.
- Per-bracket localStorage persistence: an edit under one bracket survives a switch-away-and-back (9 not re-seeded to 10); keys `deckflow.primer.sections.<bracket>` written.
- Group badges render "N/M sections selected" (PRM-11); per-section help text present (PRM-12).
- End-to-end build (bracket cEDH, paste deck): 13.2 KB ChatGPT primer with the D-2 combo ground-truth block, sourced from live Commander Spellbook + EdhTop16 `GetTopArchetypesAsync`.
- Download: `POST /deck-primer/download` → 200, 22,533 bytes, `application/zip` (all enabled variants).
- No console errors; desktop + mobile layouts clean (single-column mobile, no overflow). Screenshots captured.

### Bugs found by the visual-verify and fixed (post-31-06 commits)

- `9fd1c65` `fix(primer): html-encode per-bracket preset data attribute` — `data-preset-ids` used `@Html.Raw(JsonSerializer.Serialize(...))`; the JSON double-quotes terminated the double-quoted HTML attribute, so the client read an empty preset and cleared all checkboxes on load. Dropping `@Html.Raw` (Razor encodes, matching the gate attributes) fixed it.
- `779affe` `fix(primer): degrade gracefully when category knowledge lookup fails` — `GetCategoryRowsForCommanderAsync` threw uncaught (exposed by a stale-schema local `category-knowledge.db`), failing the whole build; now wrapped in try/catch like the EdhTop16 path (omit the block, continue). Added regression test `CategoryStoreThrows_OmitsBlock_BuildSucceeds`.
- `abbeedd` `fix(test): construct DeckController with the new primer service dependency` — the new `IDeckPrimerPacketService` ctor param broke `DeckControllerTests` compile (24 call sites); added `StubDeckPrimerPacketService` and threaded it through. (31-06 built only `DeckFlow.Web`, not the test project — the interface-change/test-project trap.)

Full `DeckFlow.Web.Tests`: **654 passed / 0 failed / 5 PG-skipped** after all fixes.
