# Phase 107: Cut Lab Tech-Debt Cleanup - Context

**Gathered:** 2026-07-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Retire the tracked Cut Lab quality/tech-debt identified across Cycle 18 (Phases 101–106).
**No new user requirements — quality only.** The six ROADMAP cleanup items are the fixed
scope. Each must be either fixed or explicitly closed-with-reason; full suite + e2e green;
no behavior regression.

The six items (from ROADMAP.md Phase 107):
1. Dead `_spellbook` + `_categoryKnowledge` fields in `CutLabPageService` — remove/justify.
2. Pool-status chip: two sites disagree (total vs non-commander count) — reconcile.
3. Dark-theme delta contrast: only Nyx has `--cutlab-delta-up/down` overrides; other dark
   guild themes inherit sub-AA global success/danger — add overrides.
4. 101-VERIFICATION open items: validator xmldoc garble; Manabase castability-copy leaking
   onto Cut Lab; Nyx-mobile badge overlap; Lock-all-lands contrast; mobile pool-row
   "Package assignment" label truncation.
5. 104-simplify notes: cacheKey→data-attr, route path-base safety, shared pluralizer
   (server + JS).
6. Structural-analysis table isn't live-patched on JS decide (server-render refresh only) —
   live-patch or keep documented.

</domain>

<decisions>
## Implementation Decisions

### Item 1 — Dead DI fields (CutLabPageService)
- **D-01:** REMOVE the unused `_spellbook` + `_categoryKnowledge` fields and their ctor
  params from `CutLabPageService` (DeckFlow.Web/Services/CutLab/CutLabPageService.cs:105-106).
  Cleaner surface. NOTE: these two dependencies ARE genuinely used in
  `CutLabAnalysisContextBuilder` (:81-82, :499-546) — do NOT touch those; scope the removal
  to `CutLabPageService` only. If any test DI-probes the removed params, update the test.

### Item 6 — Structural-analysis table live-patch
- **D-02:** IMPLEMENT live-patch. On JS decide/accept, live-update the structural-analysis
  table client-side (match the existing count-chip / working-list live-patch behavior in
  cut-lab.ts) instead of relying on a server-render refresh. Requires new client DOM logic
  + e2e coverage asserting the table updates without full round-trip. This is the heaviest
  item — planner should isolate it in its own plan/wave so it can be verified independently.

### Item 3 — Dark-theme delta contrast
- **D-03:** Add `--cutlab-delta-up` / `--cutlab-delta-down` overrides to ALL dark guild
  themes (not just the measured failures), so none inherits the sub-AA global success/danger.
  Token seam already exists (defined in site-common.css, only overridden in site-nyx.css).
  Planner: enumerate the dark themes among site-*.css (dark guild themes + planeswalker-dark),
  contrast-check each delta color against its panel background, set AA-passing overrides.
  Keep layout CSS in site-common.css per project constraint; token overrides go in each
  theme file's `:root`.

### Batching
- **D-04:** All six items land in ONE phase (107), multi-plan/wave. Closes the whole
  Cycle-18 tech-debt tail at once. Isolate item 6 (live-patch, the only behavior-adjacent
  item) in its own plan for independent verification; the other five are mechanical.

### Claude's Discretion
- The purely mechanical fixes — item 4 (xmldoc garble, Manabase copy leak, Nyx badge
  overlap, Lock-all-lands contrast, mobile label truncation), item 5 (cacheKey→data-attr,
  path-base safety, shared pluralizer), and item 2 (pool-status chip reconciliation) — are
  Claude-decidable in planning. Pick the concrete fix per what the code shows; no user
  decision pending on these.
- Item 2 reconciliation direction (which count is canonical — total vs non-commander) is
  Claude's call: choose the count that matches what the user-facing chip label claims, and
  make all sites agree with it.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase debt sources (the authoritative item lists)
- `.planning/workstreams/cut-lab/ROADMAP.md` §"Phase 107: Cut Lab Tech-Debt Cleanup"
  (lines ~127-137) — the six-item scope, goal, success criteria.
- `.planning/workstreams/cut-lab/phases/101-intake-protection-foundation/101-VERIFICATION.md`
  §"Open Items (recorded, non-blocking)" — items 1-5 detail (dead prop / copy triplication,
  path-base assumption, Manabase-verbatim copy, xmldoc garble, cosmetic Nyx/Lock-all-lands).

### Target source files (confirmed during scout)
- `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` — item 1 dead fields (:105-106).
- `DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs` — DO-NOT-TOUCH: legitimately
  uses `_spellbook`/`_categoryKnowledge` (:81-82, :499-546).
- `DeckFlow.Web/wwwroot/css/site-common.css` + `site-nyx.css` — item 3 `--cutlab-delta-*`
  token seam (defined common, overridden nyx only).
- `DeckFlow.Web/wwwroot/ts/cut-lab.ts` — item 5 path-base (`form[action="/cut-lab"]` ~:103),
  pluralizer, cacheKey; item 6 live-patch target (existing count-chip live-patch pattern).
- `DeckFlow.Web/Models/CutLabViewModel.cs` — pool-status chip string (item 2), dead
  `PoolStatusText` prop (item 4 consolidation).
- `DeckFlow.Web/Views/**/CutLab.cshtml` — chip render (:128), Manabase copy leak (:100),
  structural-analysis table markup (item 6).
- `DeckFlow.Web/Services/CutLab/CutLabPoolValidator.cs` — xmldoc garble (:26).

### Project constraints that gate this phase
- Root `CLAUDE.md` §Constraints — theme CSS forks: layout in site-common.css, token
  additions in each theme's `:root`; changed-lines-only format gate; LF endings; five
  editorconfig carve-outs (raw-string reindent, `{get;init;}`, etc.).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `--cutlab-delta-up/down` token seam (site-common.css) — item 3 rides the existing seam;
  just add per-theme `:root` overrides.
- cut-lab.ts count-chip / working-list live-patch — item 6's live-patch mirrors this proven
  client-DOM update pattern (e2e already asserts the count chip; extend to the table).
- Existing e2e harness (playwright, headless via scripts/run-web-test.sh) — item 6 verification.

### Established Patterns
- Cut Lab services live under `DeckFlow.Web/Services/CutLab/` (one type per file).
- ViewModel-computed strings surface via `CutLabViewModel`; keep the chip string single-source
  (item 2 + item-4 triplication were flagged — consolidate, don't add a 4th copy).

### Integration Points
- Item 1 removal touches the `CutLabPageService` ctor signature → check DI registration in
  Program.cs and any test that news it up / DI-probes the params.
- Item 6 adds a client render path; server-render refresh stays as the no-JS fallback (do
  not remove it — progressive enhancement).

</code_context>

<specifics>
## Specific Ideas

- Item 6: live-patch must NOT replace the server-render refresh — it augments it, so the
  no-JS path still works (Cut Lab has form-fallback throughout Cycle 18).
- Item 3: "all dark guild themes" = every dark site-*.css (guild darks + planeswalker-dark),
  not the light themes; contrast-check confirms which actually need the override but all dark
  themes get one for consistency.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. Partial-Copy Cuts "Option B" is already
promoted/closed in Phase 106; the ROADMAP backlog note is historical, not 107 scope.

</deferred>

---

*Phase: 107-cut-lab-tech-debt-cleanup*
*Context gathered: 2026-07-22*
