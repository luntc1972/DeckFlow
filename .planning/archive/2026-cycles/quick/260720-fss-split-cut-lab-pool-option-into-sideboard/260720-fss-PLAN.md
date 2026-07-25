---
quick_id: 260720-fss
slug: split-cut-lab-pool-option-into-sideboard
type: quick
date: 2026-07-20
description: Split Cut Lab pool option into independent Sideboard + Considering/Maybeboard toggles, show per-board counts, and give a size error that lists the counts
workstream: cut-lab
supersedes_ui_of: 260720-f3o
files_modified:
  - DeckFlow.Web/Models/CutLabRequest.cs
  - DeckFlow.Web/Models/CutLab/CutLabState.cs
  - DeckFlow.Web/Services/CutLab/CutLabPageService.cs
  - DeckFlow.Web/Services/CutLab/CutLabPoolValidator.cs
  - DeckFlow.Web/Controllers/CutLabController.cs
  - DeckFlow.Web/Models/CutLabViewModel.cs
  - DeckFlow.Web/Views/Deck/CutLab.cshtml
  - DeckFlow.Web.Tests/CutLabPageServiceTests.cs
  - DeckFlow.Web.Tests/CutLabPoolValidatorTests.cs
  - DeckFlow.Web/Help/cut-lab.md
  - README.md
---

## Description

Refine the just-shipped Cut Lab pool option (quick 260720-f3o) per user direction (2026-07-20):

1. **Split the single `IncludeSideboardAndMaybeboard` checkbox into TWO independent toggles** — one for the **sideboard**, one for the **considering/maybeboard** list — so the user can include either or both to land inside the 101–150 pool range.
2. **Show per-board counts** — Main (mainboard + commander), Sideboard, Considering/Maybe — after every import, near the pool-count line.
3. **Size error lists the counts** — when the selected boards push the non-commander pool over 150, reject with a Cut-Lab-specific message that lists each board's count so the user knows what to deselect.

**Terminology (user-specified):** Moxfield calls the third list **"Considering"**; Archidekt calls it **"Maybeboard"**. Both come through as internal `Board == "maybeboard"`. Label the control **"Considering / Maybeboard"** so both audiences recognize it. Sideboard is internal `Board == "sideboard"`.

## Context / grounding

- Current combined flag: `CutLabRequest.IncludeSideboardAndMaybeboard`; board-set switch at `CutLabPageService.cs:207-209` (`request.IncludeSideboardAndMaybeboard ? ExpandedAnalyzedBoards : AnalyzedBoards`); persisted in `CutLabIntent`; rehydrated in `CutLabController.Decide`; checkbox in `CutLab.cshtml`.
- Board values: importers emit `Board` = `commander` / `mainboard` / `sideboard` / `maybeboard` (`MoxfieldApiDeckImporter.AddBoardEntries`, `ArchidektApiDeckImporter` category→board). Moxfield "Considering" → API key `maybeboard`. Archidekt "Maybeboard" → `maybeboard`. Same internal value.
- Size validation: `CutLabPoolValidator.cs` — `MaxPoolCards = 150`; the too-many message at line 36 ("This pool has too many cards for Cut Lab (limit 150 plus commander)…"). The too-few message at line 31 is unchanged.
- Full board set is loaded before filtering (`load.Entries` has all boards), so per-board counts are computable from the loaded entries regardless of which are included.
- KNOWN, out of scope: a Moxfield URL import that Cloudflare-blocks falls back to Commander Spellbook, which returns NO sideboard/maybeboard — so those counts will legitimately be 0 for that path (the existing fallback notice already warns). Do NOT try to fix the fallback here; just report the real loaded counts.

## Tasks

### Task 1: Two independent flags + board-set + counts + size error

**Files:** `CutLabRequest.cs`, `CutLabState.cs`, `CutLabPageService.cs`, `CutLabPoolValidator.cs`, `CutLabController.cs`, `CutLabViewModel.cs`

**Action:**
1. `CutLabRequest`: replace `IncludeSideboardAndMaybeboard` with two bools `IncludeSideboard` and `IncludeMaybeboard` (both default false), each with XML doc. (Moxfield "Considering" == Archidekt "Maybeboard" == `IncludeMaybeboard`.)
2. `CutLabIntent` (`CutLabState.cs`): replace the persisted `IncludeSideboardAndMaybeboard` with `IncludeSideboard` + `IncludeMaybeboard`. **Back-compat:** a legacy serialized state carrying `IncludeSideboardAndMaybeboard: true` must deserialize such that BOTH new flags read true (the old combined meaning); absence → both false. Keep it simple — the feature is flag-gated OFF and unreleased, but honor any state saved this session.
3. `CutLabPageService`:
   - Build the analyzed board set per request from the two flags: always `{ mainboard, commander }`; add `"sideboard"` when `IncludeSideboard`; add `"maybeboard"` when `IncludeMaybeboard`. Do NOT mutate any static set — derive a local set. (Remove/replace the single-flag `ExpandedAnalyzedBoards` branch.)
   - Compute per-board counts from the FULL loaded entries (`load.Entries`, before the analyzed-board filter), quantity-weighted, as: **Main** = mainboard + commander, **Sideboard** = sideboard, **Considering** = maybeboard. Carry these onto the process result / view model so the view can always show them.
   - Carry both flags into the persisted `CutLabIntent`.
4. `CutLabPoolValidator`: enhance the **too-many** path (line ~34-36) so the error message lists the board counts. Pass the three counts (main, sideboard, considering) into the validation call (extend the method signature or add an overload) and format: e.g. *"This pool has {total} non-commander cards — over Cut Lab's 150 max. Main {main} · Sideboard {sb} · Considering/Maybe {mb}. Deselect the sideboard or considering list to fit."* Keep the too-few message unchanged.
5. `CutLabController.Decide` (no-JS rehydration ~line 124): set both `request.IncludeSideboard` and `request.IncludeMaybeboard` from `state.Intent`.
6. `CutLabViewModel`: carry both flags (for checkbox re-render) and the three board counts (for the breakdown display). Ensure `CutLabViewModel.From` maps them.

**Verify:** `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -clp:ErrorsOnly` clean.

**Done:** two flags drive the board set independently; counts computed + carried; too-many error lists per-board counts; default (both off) = mainboard-only, unchanged.

### Task 2: View — two checkboxes + board breakdown

**Files:** `CutLab.cshtml`

**Action:**
1. Replace the single `IncludeSideboardAndMaybeboard` checkbox with two checkboxes bound to `IncludeSideboard` and `IncludeMaybeboard`, reusing the existing checkbox idiom (no new CSS). Labels: **"Include sideboard"** and **"Include considering / maybeboard"**. Helper (`.manabase-help`) on the considering one: "Moxfield calls this 'Considering'; Archidekt calls it 'Maybeboard'." Re-render each checkbox's checked state from `Model.Request`.
2. After a successful import, near the existing `[data-cut-lab-lock-count]` pool-count line, show the board breakdown from the view model: e.g. **"Main {n} · Sideboard {n} · Considering/Maybe {n}"**. Only render when counts are available (post-import). No new CSS class — reuse existing text/chip styling.

**Verify:** build clean; both checkboxes render + post; breakdown shows after import.

**Done:** independent selection + visible board counts.

### Task 3: Tests

**Files:** `CutLabPageServiceTests.cs`, `CutLabPoolValidatorTests.cs`

**Action:**
1. Update the existing 260720-f3o combined-flag tests to the two-flag model.
2. Add: **sideboard-only** (IncludeSideboard true, IncludeMaybeboard false) → pool includes sideboard entries, excludes maybeboard; **considering-only** → includes maybeboard, excludes sideboard; **both** → includes both; **neither (default)** → mainboard+commander only (regression).
3. Board counts: a loaded deck with known main/sideboard/maybeboard quantities exposes the correct three counts on the result/view model.
4. Too-many error: a selection whose non-commander total exceeds 150 produces the error message containing each board count (assert the numbers appear). Add/extend `CutLabPoolValidatorTests` for the new signature.
5. Back-compat: a legacy state JSON with `IncludeSideboardAndMaybeboard: true` deserializes to both new flags true; absent → both false; no throw.

**Verify:** `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab"` green.

**Done:** both toggles, counts, size error, and back-compat locked.

### Task 4: Docs

**Files:** `Help/cut-lab.md`, `README.md`

**Action:** Update the 260720-f3o wording (which described one combined option) to the two independent toggles + the Considering/Maybeboard terminology, and mention the per-board counts + size error. Keep concise, match existing voice, LF preserved.

**Verify:** wording accurate to shipped behavior.

**Done:** docs match the two-toggle design.

## Constraints

- Cut-Lab-scoped; do NOT modify shared importers, `DeckEntry`, or Core.
- Preserve per-file LF; `{ get; init; }` / `{ get; set; }` carve-outs; XML docs on new public members; no new CSS class; no inline `[Attribute]`.
- Default both-off = today's mainboard-only behavior, zero change until opted in.
- Do NOT change the 150 cap value or the too-few message.

## Out of scope

- Fixing the Moxfield URL fallback that drops sideboard/maybeboard (separate, known limitation).
- Two-step import flow (user chose "always show counts after import").
- Card images, per-card lock, plan-textbox copy.
