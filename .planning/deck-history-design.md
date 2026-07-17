# Deck History (Deck Versioning) — Design Spec

**Date:** 2026-07-16
**Status:** Approved design, pending implementation plan
**Route:** `/deck-history`
**Feature flag:** `tool.deck-history.enabled` (seeded OFF)

## Problem

DeckFlow has no user accounts, so users cannot track how a deck has changed
over time. Deck-building sites cap or gate history (Archidekt exposes only the
last 100 changes; snapshots require an account). Serious Commander/cEDH players
want to see what they added/cut between versions, why, and to feed that
evolution into an AI analysis conversation.

## Solution summary

A standalone tool page where the user's deck history lives in a **downloadable
JSON file the user owns**. Each visit: upload the file (or start a new one),
import the current deck (URL or paste), add a note, and download the updated
file. The page renders a version timeline, an adds/cuts diff between any two
versions, and a ChatGPT-ready "how has this deck evolved" prompt artifact.

Storage model is **snapshot-per-version** (full decklist in every version), per
the research report: snapshots let any two versions diff directly with the
existing `DiffEngine`, survive hand-edits and truncation gracefully, and cost
~2–3 KB per version at Commander scale. Delta formats (RFC 6902 JSON Patch,
RFC 7396 Merge Patch) were evaluated and rejected as the stored representation;
per-version deltas appear in the file only as a **derived convenience block**
that DeckFlow recomputes on every upload and never trusts.

## Decisions (locked)

| Decision | Choice |
|---|---|
| Placement | New standalone tool page `/deck-history` |
| Storage | Downloadable JSON file, snapshot-per-version |
| Upload UX | File picker (`<input type="file">`, `IFormFile`) |
| Hand-edits | Tolerated: validate structure, recompute deltas, repair what is repairable, warn on problems. No hashes/checksums. |
| Unchanged deck | No new snapshot; warn "identical to latest"; still render timeline/diff/prompt |
| Divergent copies | No merge logic. The uploaded file is the truth; user manages copies. |
| Prompt shape | First + latest versions as full lists; intermediate versions as deltas + notes (token-lean) |
| Container | Single JSON document (not JSONL) — server regenerates whole file each download |

## File format

```json
{
  "format": "deckflow-history",
  "formatVersion": "1.0",
  "deckName": "Tivit Ad Nauseam",
  "source": { "site": "moxfield", "url": "https://moxfield.com/decks/..." },
  "versions": [
    {
      "id": 3,
      "date": "2026-07-16T22:04:11Z",
      "label": "post-ban",
      "notes": "Cut Dockside after ban; leaning harder into Ad Naus line.",
      "commander": ["Tivit, Seller of Secrets"],
      "cards": [ { "name": "Sol Ring", "qty": 1 } ],
      "delta": {
        "adds": [ { "name": "Mystic Remora", "qty": 1 } ],
        "cuts": [ { "name": "Dockside Extortionist", "qty": 1 } ],
        "qtyChanges": [ { "name": "Island", "from": 8, "to": 7 } ]
      }
    }
  ]
}
```

Format rules:

- **Serialization:** camelCase property names, `WriteIndented = true`.
- **Versioned header:** `format` must equal `deckflow-history`. `formatVersion`
  is `major.minor`. Unknown **major** → reject upload with an explicit message.
  Unknown **minor** → accept; unknown fields are ignored on read (System.Text.Json
  default) and **preserved on write** via `[JsonExtensionData]` on the file and
  version records, so an older DeckFlow never silently drops newer fields.
- **`versions`** is append-ordered; `id` is a monotonically increasing integer
  assigned by DeckFlow (repaired if a hand-edit broke monotonicity).
- **`date`** is ISO-8601 UTC, assigned at append time.
- **`label`** optional short name; **`notes`** free text (user-entered, may be
  hand-edited later — that is fine).
- **`cards`** = authoritative full snapshot: mainboard entries, `name` + `qty`
  only. **`commander`** is a separate array of card names. Maybeboard excluded.
  Sideboard excluded (Commander-focused tool).
- **`delta`** = derived changes vs the previous version: `adds`/`cuts` are
  `{name, qty}` entries; `qtyChanges` covers quantity shifts of a card present
  in both versions (basic lands are the common Commander case). Recomputed by
  DeckFlow on every upload from the snapshots; any hand-edited delta is
  overwritten. First version has an empty delta.
- Upload size cap ~1 MB (hundreds of versions of headroom).

## Architecture

### DeckFlow.Core (`DeckFlow.Core/History/`) — pure, no I/O

| Unit | Responsibility |
|---|---|
| `DeckHistoryFile`, `DeckSnapshot`, `SnapshotCard`, `SnapshotDelta` | `sealed record` types mirroring the format. `{ get; init; }` (never `{ get; }` — STJ carve-out). `[JsonExtensionData]` on `DeckHistoryFile` and `DeckSnapshot`. |
| `DeckHistorySerializer` | `Parse(string json)` → file or structured failure (bad JSON, wrong `format`, major-version mismatch, cap exceeded). Normalizes/repairs: missing ids reassigned, versions re-sorted by date when ids are broken, null collections → empty. `Serialize(DeckHistoryFile)` → indented camelCase JSON. |
| `DeckHistoryAppender` | Builds a `DeckSnapshot` from `List<DeckEntry>` (commander = entries with `Board == "commander"`, mainboard → cards); detects "identical to latest" (normalized name + qty set equality); assigns next id + UTC date; recomputes **all** deltas via `VersionDiffProjector`. |
| `VersionDiffProjector` | Compares two snapshots directly via `CardNormalizer`-keyed maps and returns `VersionDiff(Adds, Cuts, QuantityChanges)`. (Amended pre-execution from the original `DiffEngine.Compare` adaptation: snapshots are name+qty only, so `DiffEngine`'s board/printing machinery added conversion cost without signal, and `DeckDiff` splits quantity deltas across two lists that would need dictionary reassembly anyway. Same normalized-name matching semantics.) |

`CardNormalizer` provides the comparison key, consistent with the rest of the
site.

### DeckFlow.Web

| Unit | Responsibility |
|---|---|
| `DeckHistoryController` | `GET /deck-history` (`[FeatureFlagGate("tool.deck-history.enabled")]`) renders the form. `POST` process action accepts `IFormFile? historyFile`, the standard split deck input (`DeckInputSource` + URL + text, reconciled via `DeckInputReconciler`, loaded via `IDeckEntryLoader`), `notes`, optional `label`, and optional pair-diff selection. `POST` download action returns the updated JSON as `File(..., "application/json; charset=utf-8", fileName)` with `X-DeckFlow-Filename`. |
| `IDeckHistoryPageService` + impl | Orchestrates: parse upload → load deck (if provided) → append → project pair diff → build prompt → serialize updated JSON. Keeps the controller thin. Scoped registration. |
| `DeckHistoryViewModel` | `ActiveTab`, input echo fields, timeline rows (id, date, label, notes, count, adds/cuts summary), pair-diff selection + result, prompt text, serialized updated JSON (hidden field), warnings, `ErrorMessage`. |
| `Views/Deck/DeckHistory.cshtml` | Form + result sections. Uses `_DeckToolTabs`, `_AiSelector`, existing split-input markup conventions. Layout CSS in `site-common.css`, theme tokens only. |
| `PromptBuilders/Evolution/` | `IEvolutionPromptVariant`, `EvolutionPromptVariantRegistry`, `ChatGptEvolutionPromptVariant`, `ClaudeEvolutionPromptVariant`, `GeminiEvolutionPromptVariant`. Three hand-written variants (ADR-0001 decoupling; raw string literals never re-indented). |

### Request/data flow

1. `GET /deck-history` → form: file picker + split deck input + notes + label.
2. `POST` process:
   - **file + deck** → parse, load deck, append (or warn-unchanged), recompute deltas.
   - **deck only** → new history file started, version 1.
   - **file only** → inspect mode: timeline + pair diff, no append.
3. Result view renders timeline, pair diff (default: latest vs previous), AI
   prompt copy box, and a download button. The updated serialized JSON travels
   in a hidden field (same pattern as packet `FetchedEntriesJson`). Selecting a
   different version pair re-POSTs with that hidden JSON as the state source —
   no re-upload of the file input needed.
4. Download `POST` echoes the hidden JSON back as a file download. The button
   is marked `data-prompt-download-submit` so `deck-sync.js` intercepts it as
   a fetch + blob download (mobile-refresh-safe; reads `X-DeckFlow-Filename`).
5. Filename: `deck-history-<slug(deckName)>-<yyyyMMdd>.json`.

### Prompt artifact ("how has this deck evolved")

- Header: deck name, commander, version count, date span.
- Version 1: full decklist (plain text lines, house style).
- Intermediate versions: date + label + notes + adds/cuts lines only.
- Latest version: full decklist.
- Analysis instructions follow the house EXECUTE NOW / one-round-trip style;
  per-platform differences live in the three variant classes.
- Rendered as plain text in a copy box — never raw JSON (token efficiency,
  15–60% overhead of repeated JSON keys per research).

## Error handling

| Failure | Behavior |
|---|---|
| Unparseable JSON / wrong `format` marker | On-page `ErrorMessage`: "not a DeckFlow history file" |
| Major version ahead of app | Explicit message: file was created by a newer DeckFlow |
| File over size cap | Friendly reject |
| Deck import failure (Moxfield/Archidekt/parse) | Existing `UpstreamErrorMessageBuilder` / `DeckParseException` copy, house pattern |
| Deck not exactly 100 cards | Non-blocking warning ("Deck has N cards — Commander decks run 100."); snapshot still saved. Matches Bracket/Primer's lenient stance (only Deck Sync hard-enforces 100) and the tool's hand-edit-tolerant philosophy — mid-brew lists are trackable. |
| Timeout | `OperationCanceledException` → existing timeout copy |
| Repairable hand-edit damage (bad ids, missing arrays, stale deltas) | Repair silently or with a non-blocking warning list on the result view |

All failures re-render the page with `ErrorMessage`; no bare 500s.

## Wiring checklist

- `DeckPageTab.DeckHistory` enum value + `_DeckToolTabs` entry.
- `SeoPaths.Indexable` + `SeoPaths.Tools` (+ sitemap, JSON-LD, share bar follow automatically).
- `FeatureFlagCatalog.Descriptions` + `FeatureFlagStore` seed (OFF) — `FeatureFlagCatalogTests` enforces the pair.
- DI registrations in `Program.cs` (page service scoped; prompt variants + registry singletons) — not `PacketServiceCollectionExtensions`, since this is not a packet service.
- Help doc `DeckFlow.Web/Help/deck-history.md` + Help index entry.
- README feature section.

## Testing

- **Core xUnit** (`DeckFlow.Core.Tests`): serializer round-trip incl. unknown-field
  preservation and repair cases; major-version reject; appender identical-deck
  detection, id/date assignment, delta recomputation; projector add/cut/qty
  semantics (incl. commander change).
- **Web xUnit** (`DeckFlow.Web.Tests`): controller modes (file+deck / deck only /
  file only / neither), download headers + filename, oversized upload, flag
  gate; one test per prompt variant (golden-fragment assertions).
- **Playwright e2e**: happy path new-history → download; re-upload → append →
  pair diff; screenshots at 2 viewports; theme spot-check per house rule.
- Build via Windows `dotnet.exe`; no browser auto-launch (`scripts/run-web-test.sh`).

## Out of scope (this milestone)

- Cross-tool history integration (other tools reading/appending the file) — phase 2 candidate.
- Merge of divergent file copies (CRDT-style) — explicitly rejected for now.
- Per-card printing/category metadata in snapshots — name+qty only until a concrete need.
- localStorage caching of the file between visits.

## Research grounding

Format and storage-model choices follow the 2026-07-16 deep-research report
(23 claims verified): snapshot-per-version over delta storage (Git model;
direct any-pair diffing; corruption isolation), JSON Merge Patch unable to
express array edits (RFC 7396), JSON Patch positional fragility (RFC 6902/6901),
additive-minor versioned header with unknown-field round-trip, Archidekt
snapshots as shipped prior art with its 100-change history cap as the gap this
feature fills, and tabular/plain-text prompt rendering over raw JSON.
