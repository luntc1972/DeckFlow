# Phase 32: Expert Context Selection - Context

**Gathered:** 2026-06-07
**Status:** Ready for planning
**Source:** PRD Express Path (.planning/specs/2026-06-07-expert-context-selection-design.md)

<domain>
## Phase Boundary

Users can pin trusted KB videos and follow creators so their advice is guaranteed a place
in the deck-analysis prompt — manual control layered over Phase 30's automatic relevance
selection. Covers: pin/follow UI on /content-kb browse and the analysis form, layered-fill
merge tiers in the relevance service, selection round-trip through the packet zip, an
artifact-level Evergreen admin flag, and per-clip origin markers on the What Experts Say
panel. Out of scope: user accounts, tag pinning.

</domain>

<decisions>
## Implementation Decisions

### Placement (S-01)
- Both surfaces: pin on /content-kb browse cards AND editable chip area on the analysis form (mockup option C, hybrid two-stage picker)
- /content-kb browse: each video card gets "📌 Pin for next analysis"; each creator heading gets "★ Follow"; a small tray shows current pins/follows
- /deck-analysis form: Expert Context chip area shows carried-over pins + follows; chips removable; typeahead adds more (published KB entries / creators); selection submits via hidden fields

### Pinnables (S-02)
- Videos + creator follows only
- NO tag pinning (rejected — duplicates auto archetype matching)

### Pin lifetime (S-03)
- Video pins are one-shot: clear after the analysis run
- Creator follows are sticky until unfollowed

### Storage (S-04)
- Browser localStorage, per-device
- User accounts noted as future feature (out of scope)

### Merge mechanics (S-05) — layered fill, NOT score-boost, NOT pins-exclusive
Within existing hard caps (K=5 clips, ~4.5KB rendered budget):
- Tier 1: Pinned videos (max 3 pins) — clips injected first, document order; no gate, no score threshold
- Tier 2: Followed creators — their artifacts' clips with ≥1 scoring dimension hit (gate relaxed 2→1), score order
- Tier 3: Auto-scored — unchanged Phase 30 behavior (≥2 dims, score ≥ 2.0)
- Tier 4: Evergreen artifacts — fill remaining slots; max 1 evergreen clip per prompt
- Budget trim removes tier 4 first, then 3, then 2; tier 1 last
- If a single pinned video busts the budget alone, trim within it but keep at least 1 clip
- A pin never silently vanishes below an auto match

### Generic-advice videos (S-06)
- Artifact-level `IsEvergreen` admin flag; evergreen fills leftover slots
- Max 1 evergreen clip per prompt

### Download persistence (S-07)
- Selection state saved in packet zip as `33-expert-selection.json` alongside the clip set (`32-expert-context.json`)
- Re-upload restores both clips AND selection state; the form re-offers the pins (HIGH-2 replay-first pattern)

### Data flow
- localStorage → hidden form fields → `DeckAnalysisRequest.PinnedVideoIds` + `FollowedCreators` → relevance service selection parameter → ONE merged, trimmed set → prompt variants + zip + result panel
- Phase 30 invariant (prompt == zip == panel by construction) preserved: merging happens before the single trimmed set exists

### Component changes (locked surface map)
- `ContentSiteIndexRow` + index store + seed loader: `IsEvergreen` boolean column (additive, migration-safe)
- `ContentKbRelevanceService`: new `GetMergedClipsAsync(selection, ...)` implementing tiers; existing `GetRelevantClipsAsync`/`ScoreAllAsync` untouched
- `DeckAnalysisRequest`: `PinnedVideoIds`, `FollowedCreators` round-tripped fields
- `DeckAnalysisPacketService`: thread selection; extend replay-first logic
- `PacketArtifactStore`: `33-expert-selection.json` allowlist + writer + reader (same-commit rule — `ReadEntries` throws on unlisted entries)
- Views (analysis form, /content-kb browse): chip area, pin/follow buttons, tray
- New TS `kb-selection.ts`: localStorage + chips + tray (progressive enhancement; form works without JS, selection just empty)
- `_ContentKbPanel.cshtml`: origin markers — 📌 pinned / ★ followed / auto / evergreen
- `/Admin/ContentKb`: Evergreen toggle per row (POST, SameOrigin-validated like SetVisibility)

### Testing (required by spec)
- Tier-fill unit tests: pin-first order, follow gate-relax, evergreen filler + 1-clip cap, trim order (4→3→2→1), pin survives trim, pin-cap (3) enforced
- Zip round-trip: selection JSON survives BuildZip → LoadFromZip; corrupt selection entry degrades to empty selection (no throw)
- Controller: selection fields bind; replay restores selection
- TS/localStorage: manual + human-verify checkpoint at 2 viewports
- All records `{ get; init; }` + serialization round-trip tests (standing constraint)

### Claude's Discretion
- Exact chip/tray markup, CSS class names, and typeahead implementation details (must follow site-common.css layout rule + per-theme token rule)
- Hidden-field encoding format for selection submit
- Internal shape of the selection parameter passed to the relevance service
- Plan/wave decomposition (ROADMAP estimates 4 plans: schema/tiers → request/packet/zip → browse+form UI/TS → admin toggle + markers + UI checkpoint)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Design spec (source of locked decisions)
- `.planning/specs/2026-06-07-expert-context-selection-design.md` — S-01..S-07 locked decisions, tier table, component map, test list

### Phase 30 foundation (this phase layers onto it)
- `.planning/phases/30-content-kb-integration/` — Phase 30 plans/summaries: relevance service, packet zip round-trip, panel, admin KB view
- `DeckFlow.Web/Services/ContentKbRelevanceService.cs` — auto-scoring behavior tier 3 must preserve
- `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` / packet pipeline — replay-first logic to extend
- `DeckFlow.Core` packet artifact store — `32-expert-context.json` allowlist pattern to mirror for `33-expert-selection.json`
- `DeckFlow.Web/Views/Shared/_ContentKbPanel.cshtml` — panel to mark with origins
- `DeckFlow.Web/Controllers/Admin/` ContentKb admin controller — SetVisibility POST pattern for the Evergreen toggle

</canonical_refs>

<specifics>
## Specific Ideas

- Origin marker glyphs: 📌 pinned / ★ followed / auto / evergreen
- Pin cap: 3 videos; clip cap K=5; rendered budget ~4.5KB (Phase 30 values, unchanged)
- Evergreen toggle must be SameOrigin-validated POST, modeled on the existing SetVisibility action
- Progressive enhancement requirement: analysis form must work as a plain form without JS (selection simply empty)

</specifics>

<deferred>
## Deferred Ideas

- User accounts syncing pins/follows across devices (explicit user ask, deferred to future milestone)
- Tag pinning (rejected outright — duplicates automatic archetype matching)

</deferred>

---

*Phase: 32-expert-context-selection*
*Context gathered: 2026-06-07 via PRD Express Path*
