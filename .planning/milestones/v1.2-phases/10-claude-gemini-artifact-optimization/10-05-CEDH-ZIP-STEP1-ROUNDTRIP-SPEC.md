# 10-05: cEDH Zip Step 1 Round-Trip — Spec

**Status:** Spec — brainstormed 2026-05-11
**Parent phase:** 10 (Claude + Gemini Artifact Optimization)
**Trigger:** v1.2 integration test T3 (cEDH meta-gap round-trip) revealed gap during 2026-05-11 retest session.

## Problem

The cEDH meta-gap session zip cannot restore Step 1 state. Today the zip stores three scalars in `01-request-context.txt` (`workflow_step`, `commander`, `target_ai_platform`) plus deck text + generated prompt + response JSON, but **does not** persist:

- `FetchedEntries` — the EDH Top 16 query result rows that populate Step 1's reference table
- `SelectedReferenceIndexes` — the 1-3 reference decks the user picked
- `TimePeriod`, `SortBy`, `MinEventSize`, `MaxStanding` — Step 1 filter knobs

On re-upload, the reference table renders empty, selection checkboxes don't exist, and "Generate Meta Gap Prompt" throws `Select at least 1 EDH Top 16 reference deck before generating the prompt.` Compounding this, the service in `ChatGptCedhMetaGapService.BuildAsync` always re-fetches edhtop16 on every Step 2 submit, so a rate-limited upstream (observed 2026-05-11) blocks regenerate even on a fresh flow.

## Use case

The user wants a **full Step 1 round-trip**: regenerate the prompt, see prior results, and optionally re-select different reference decks — all without re-hitting edhtop16. The zip becomes a true point-in-time snapshot of the session.

## Design

### 1. Zip artifact contract

Add one new artifact to `CedhAllowedNames`:

- `20-edh-top16-references.json` — JSON array of `EdhTop16Entry` objects. Serialized via `System.Text.Json` with camelCase property names. Includes the full entry shape: standing, wins/losses/draws, decklist URL, player name, tournament name/id, tournament date (`yyyy-MM-dd` ISO), tournament size, main deck card list (name + type).

Extend `01-request-context.txt` (`ChatGptCedhMetaGapService.BuildRequestContextText`) with:

```
time_period: <CedhMetaTimePeriod enum string>
sort_by: <CedhMetaSortBy enum string>
min_event_size: <int>
max_standing: <int>   (omitted entirely when null)
selected_reference_indexes:
- 0
- 2
- 5
```

Position indexes are preserved as-is because `20-edh-top16-references.json` round-trips entries in stable order.

### 2. Request model + service changes

`ChatGptCedhMetaGapRequest` adds:

```csharp
public string FetchedEntriesJson { get; set; } = string.Empty;
```

Hidden form field carrying the serialized `List<EdhTop16Entry>` between submits. Backed by an internal helper that deserializes on demand (returns empty list on null/empty/parse failure).

`ChatGptCedhMetaGapService.BuildAsync`:

- New behavior: when `request.FetchedEntriesJson` deserializes to a non-empty list **and** `request.WorkflowStep >= 2`, use those entries and **skip the edhtop16 re-fetch**.
- Else, fetch as today (fresh-flow path unchanged).
- Failure mode: if deserialize throws or returns empty, fall through to the live fetch (graceful degradation, no error surfaced).

`ChatGptCedhMetaGapService.BuildRequestContextText` emits the new scalars and the `selected_reference_indexes` list.

### 3. Parser changes

`ChatGptRequestContextParser` learns:

- 4 new scalar keys: `time_period`, `sort_by`, `min_event_size`, `max_standing`
- 1 new list key: `selected_reference_indexes` (added to `IsListKey`)

`ParsedRequestContext` record gains five matching nullable properties: `TimePeriod`, `SortBy`, `MinEventSize`, `MaxStanding`, `SelectedReferenceIndexes`.

### 4. Zip store changes

`ChatGptPacketArtifactStore`:

- `BuildCedhMetaGapZip(...)` signature gains `IReadOnlyList<EdhTop16Entry> fetchedEntries` param. When non-empty, serializes to JSON and adds as `20-edh-top16-references.json`. Empty/null → omit the artifact (backwards compatible with legacy callers).
- `LoadCedhMetaGapFromZip(...)`:
  - Reads `20-edh-top16-references.json` into a `List<EdhTop16Entry>`.
  - Reads filter scalars + selected-indexes list from the parsed request-context.
  - Populates `request.SelectedReferenceIndexes`, `request.TimePeriod`, `request.SortBy`, `request.MinEventSize`, `request.MaxStanding`.
  - Returns the entries via an extended `RestoredCedhMetaGapArtifacts` record (new field `FetchedEntries`).
- Add `20-edh-top16-references.json` to `CedhAllowedNames`.

### 5. Controller wiring

`DeckController.ChatGptCedhMetaGapDownload`:

- Pass `result.FetchedEntries` into `BuildCedhMetaGapZip`.

`DeckController.ChatGptCedhMetaGapUpload`:

- Propagate `restored.FetchedEntries` to view model `FetchedEntries`.
- Serialize entries into `request.FetchedEntriesJson` so the next form submit carries them.
- Update WorkflowStep heuristic: if entries restored **and** no response JSON → land on Step 2 (today's logic forces Step 1).

`DeckController.ChatGptCedhMetaGap (POST)`:

- No special handling needed — the service reads `request.FetchedEntriesJson` itself.

### 6. View changes

`Views/Deck/ChatGptCedhMetaGap.cshtml`:

- Add hidden `<input>` bound to `FetchedEntriesJson` (Step 2 panel). Carries the serialized entries on every Step 2 submit.
- Filter dropdowns already bind to `TimePeriod`/`SortBy`/`MinEventSize`/`MaxStanding` — no change.

### 7. Tests

Unit tests added to `DeckFlow.Web.Tests` (existing fixtures patterns):

- `BuildCedhMetaGapZip_includes_fetched_entries_artifact_when_provided`
- `BuildCedhMetaGapZip_omits_fetched_entries_artifact_when_empty` (back-compat)
- `LoadCedhMetaGapFromZip_restores_fetched_entries_from_artifact`
- `LoadCedhMetaGapFromZip_restores_filter_scalars_and_selected_indexes`
- `LoadCedhMetaGapFromZip_lands_on_step_2_when_entries_present_no_response`
- `BuildRequestContextText_emits_new_scalars_and_list`
- `ChatGptRequestContextParser_parses_new_scalars_and_list`
- `ChatGptCedhMetaGapService_uses_fetched_entries_override_when_present` (mock `IEdhTop16Client` to assert it is not called)
- `ChatGptCedhMetaGapService_falls_back_to_fetch_when_FetchedEntriesJson_corrupt`
- Existing Phase 10 round-trip tests updated to include FetchedEntries assertions.

Integration test (manual — T3 retest):

1. Generate a fresh cEDH session with Claude AI selector. Download zip.
2. Inspect zip: confirm `20-edh-top16-references.json` present + `01-request-context.txt` has new scalars + indexes.
3. Upload zip. Confirm: reference table renders, prior selections checked, filter dropdowns reflect zip values, AI selector shows Claude.
4. Click "Generate Meta Gap Prompt" without changing anything. Confirm prompt regenerates without an edhtop16 call (verify via dev tools network tab, or by knowing edhtop16 is rate-limited and the call would otherwise fail).
5. Confirm Step 2 prompt details panel auto-opens (per `eccc1f9`) and contains the regenerated prompt.

## Backwards compatibility

- Old zips (no `20-edh-top16-references.json`, no new scalars/indexes in request-context) load fine. `LoadCedhMetaGapFromZip` returns empty `FetchedEntries`, no selected indexes, default filter values. Controller falls back to today's Step-1-landing behavior.
- Old `BuildCedhMetaGapZip` callers without the new param: not applicable — only `DeckController.ChatGptCedhMetaGapDownload` calls this method. Single call site updated alongside.

## Out of scope

- Switching `SelectedReferenceIndexes` from positional to identity-based references. Positional is sufficient now that entries round-trip in stable order.
- Cross-session server-side caching of fetched entries. Hidden form field is sufficient for single-session round-trip.
- Anti-tamper signing of the hidden form field. EDH Top 16 data is public; tampering offers no security-relevant gain.
- Schema versioning for `20-edh-top16-references.json`. If `EdhTop16Entry` shape evolves, `System.Text.Json` deserialization with `PropertyNameCaseInsensitive` + ignoring unknown properties handles forward-compatible changes.

## Risks

- **Form payload size:** 48 entries × ~100 cards × ~50 bytes ≈ 240KB JSON in a hidden input. Acceptable for desktop browsers; well below default ASP.NET request size limits.
- **Stale fetched entries:** if the user uploads a zip from weeks ago and regenerates, the analysis is based on possibly-outdated tournament data. Acceptable — that's the intended snapshot semantics. The user explicitly chose to reuse the saved session.
- **Concurrent download/upload mismatch:** if the user downloads a zip without re-running Step 1 first, the in-memory `result.FetchedEntries` from the most recent BuildAsync drives what gets serialized. Already true for prompt+response artifacts today.

## Touched files (estimate)

- `DeckFlow.Web/Models/ChatGptCedhMetaGapRequest.cs` — add `FetchedEntriesJson` property + helper
- `DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs` — `BuildAsync` override branch + `BuildRequestContextText` emits new scalars/list
- `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` — `BuildCedhMetaGapZip` signature + body, `LoadCedhMetaGapFromZip` parses new state, allowlist add, `RestoredCedhMetaGapArtifacts` adds `FetchedEntries`
- `DeckFlow.Web/Services/ChatGptRequestContextParser.cs` — 4 new scalar keys, 1 new list key, record properties
- `DeckFlow.Web/Controllers/DeckController.cs` — `ChatGptCedhMetaGapDownload` passes entries, `ChatGptCedhMetaGapUpload` propagates + serializes + step heuristic
- `DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml` — hidden input for FetchedEntriesJson
- `DeckFlow.Web.Tests/...` — 9 new unit tests + existing round-trip test updates

Estimated diff: ~250–350 LOC source + ~150–250 LOC tests.
