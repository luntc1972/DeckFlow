# Expert Context Selection — Design

**Date:** 2026-06-07
**Status:** Approved (user, brainstorm session 2026-06-07)
**Target:** v1.5 (new phase, runs after Phase 30 completes)
**Origin:** Phase 30 UAT — a KB video's clips produced strong deck insights; user follows and
trusts that creator and wants to choose whose advice enters the prompt.

## Problem

Phase 30 injection is fully automatic: relevance scoring picks up to 5 clips; the user
cannot pin a trusted video or creator into their analysis prompt. General-advice
("philosophy") videos only inject by accidental tag overlap.

## Decisions (locked during brainstorm)

| # | Decision | Choice |
|---|----------|--------|
| S-01 | Placement | Both surfaces: pin on /content-kb browse cards, editable chip area on the analysis form (mockup option C) |
| S-02 | Pinnables | Videos + creator follows; NO tag pinning (duplicates auto archetype matching) |
| S-03 | Pin lifetime | Video pins one-shot (clear after the analysis run); creator follows sticky until unfollowed |
| S-04 | Storage | Browser localStorage, per-device. User accounts noted as a future feature |
| S-05 | Merge mechanics | Layered fill (tiers below), NOT score-boost, NOT pins-exclusive |
| S-06 | Generic-advice videos | Artifact-level `IsEvergreen` admin flag; evergreen fills leftover slots (user picked "evergreen flag" option) |
| S-07 | Download persistence | Selection state saved in the packet zip (`33-expert-selection.json`) alongside the clip set (`32-expert-context.json`); re-upload restores both |

## User experience

- **/content-kb browse:** each video card gets "📌 Pin for next analysis"; each creator
  heading gets "★ Follow". A small tray shows current pins/follows.
- **/deck-analysis form:** Expert Context chip area shows carried-over pins + follows;
  chips removable; typeahead adds more (published KB entries / creators). Selection submits
  with the request via hidden fields.
- **Result page:** "What Experts Say" panel unchanged except a per-clip origin marker:
  📌 pinned / ★ followed / auto / evergreen.

## Selection mechanics (layered fill)

Within the existing hard caps (K=5 clips, ~4.5KB rendered budget):

| Tier | Source | Rule |
|------|--------|------|
| 1 | Pinned videos (max 3 pins) | Clips injected first, document order; no gate, no score threshold |
| 2 | Followed creators | Their artifacts' clips with >=1 scoring dimension hit (gate relaxed 2->1), score order |
| 3 | Auto-scored | Unchanged Phase 30 behavior (>=2 dims, score >= 2.0) |
| 4 | Evergreen artifacts | Fill remaining slots; max 1 evergreen clip per prompt |

Budget trim removes tier 4 first, then 3, then 2; tier 1 last. If a single pinned video
busts the budget alone, trim within it but keep at least 1 clip. A pin never silently
vanishes below an auto match.

## Data flow

localStorage -> hidden form fields -> `DeckAnalysisRequest.PinnedVideoIds` +
`FollowedCreators` -> relevance service selection parameter -> ONE merged, trimmed set ->
prompt variants + zip + result panel. The Phase 30 invariant (prompt == zip == panel by
construction) is preserved because merging happens before the single trimmed set exists.

Replay (HIGH-2 pattern): on zip re-upload, `33-expert-selection.json` restores the
selection and `32-expert-context.json` restores the clips; the form re-offers the pins.

## Components

| Layer | Change |
|------|--------|
| `ContentSiteIndexRow` + index store + seed loader | `IsEvergreen` boolean column (additive, migration-safe) |
| `ContentKbRelevanceService` | new `GetMergedClipsAsync(selection, ...)` implementing tiers; existing `GetRelevantClipsAsync`/`ScoreAllAsync` untouched |
| `DeckAnalysisRequest` | `PinnedVideoIds`, `FollowedCreators` round-tripped fields |
| `DeckAnalysisPacketService` | thread selection; extend replay-first logic |
| `PacketArtifactStore` | `33-expert-selection.json` allowlist + writer + reader (same-commit rule — `ReadEntries` throws on unlisted entries) |
| Views: analysis form, /content-kb browse | chip area, pin/follow buttons, tray |
| New TS `kb-selection.ts` | localStorage + chips + tray (progressive enhancement; form works without JS, selection just empty) |
| `_ContentKbPanel.cshtml` | origin markers |
| `/Admin/ContentKb` | Evergreen toggle per row (POST, SameOrigin-validated like SetVisibility) |

## Testing

- Tier-fill unit tests: pin-first order, follow gate-relax, evergreen filler + 1-clip cap,
  trim order (4->3->2->1), pin survives trim, pin-cap (3) enforced.
- Zip round-trip: selection JSON survives BuildZip -> LoadFromZip; corrupt selection entry
  degrades to empty selection (no throw).
- Controller: selection fields bind; replay restores selection.
- TS/localStorage: manual + human-verify checkpoint at 2 viewports.
- All records `{ get; init; }` + serialization round-trip tests (standing constraint).

## Risks

- **Prompt dilution** — irrelevant pins lower analysis quality. Mitigated: 3-pin cap,
  origin markers make provenance visible.
- **localStorage limits** — per-device, empty in private browsing. Accepted; accounts are
  the future fix.
- **Evergreen overuse** — every prompt padded with generic advice. Mitigated: tier 4
  trimmed first, max 1 evergreen clip.

## Future (out of scope)

- User accounts syncing pins/follows across devices (explicit user ask, deferred).
- Tag pinning (rejected: duplicates automatic archetype matching).

## Estimate

One phase, 3-4 plans: (1) schema/evergreen + relevance tiers, (2) request/packet/zip
wiring, (3) browse+form UI/TS, (4) admin toggle + panel markers + checkpoint.
