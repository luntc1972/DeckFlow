# Phase 02: Layout, Hierarchy & UX Copy - Context

**Gathered:** 2026-04-30
**Status:** Ready for planning

<domain>
## Phase Boundary

Make the home hub's primary action unmistakable, eliminate flagged inline `style=` attributes from feedback/admin views, and close copy + voice + busy-state gaps surfaced by the UI audit. Targets pillar lifts: Visuals 3/4, Copywriting 3/4, Experience Design 3/4.

In scope (from ROADMAP.md):
- UI-LH-01: Promote a primary focal action on the home hub (hero CTA + per-group `.hub-card--primary`)
- UI-LH-02: Move inline `style=` from `Feedback/Index.cshtml` and `AdminFeedback/{Index,Detail}.cshtml` into named CSS classes
- UX-01: Replace generic "Submit" verb in `_MoxfieldBulkEditHint.cshtml` with action-specific copy mirroring host page's submit button
- UX-02: Add submit busy-state to `/feedback` form (disable button + spinner; prevent double-submit)
- UX-03: Reconcile voice mismatch between page `<title>` and `<h1>` on the Feedback page

Out of scope: any new home-hub cards, any new feedback fields, any non-feedback busy-state work, any non-flagged inline styles elsewhere in the app.

</domain>

<decisions>
## Implementation Decisions

### Hub primary-action treatment (UI-LH-01)

- **D-01:** Two-tier promotion — single full-width hero band ABOVE the grid + `.hub-card--primary` modifier on exactly one card per group below. Both layers ship.
- **D-02:** Hero card content = ChatGPT Analysis (the headline workflow per ROADMAP). Per-group primaries: Analyze→Deck Comparison; Build→Deck Sync; Reference→Card Lookup. (ChatGPT Analysis still appears in the Analyze grid as a regular card — hero is additive, not a removal.)
- **D-03:** Single visual signal across all promoted elements (hero + 3 group primaries): accent border using `--cta-border` token from Phase 01. No size bump, no background tint, no badge. Uniform treatment keeps the page from looking like 4 unequal-weight CTAs.
- **D-04:** Hero is structurally distinct from cards — full-width band above the first `.hub-group`, not a card inside any group. CSS class for the hero element is open to planner choice (suggest `.hub-hero` or `.hub-hero-cta`).

### Voice convention (UX-01, UX-03)

- **D-05:** Verb-noun convention site-wide for `<title>`, `<h1>`, and CTA button labels. Drop noun-only labels going forward.
- **D-06:** Feedback page specifically: title and h1 both become "Send Feedback" (or close verb-noun phrasing of equal voice). Resolves UX-03.
- **D-07:** `_MoxfieldBulkEditHint.cshtml` accepts a verb model parameter — each call site passes the verb that matches its host page's submit button (e.g., "Run Compare" on Compare page, "Look Up" on Card Lookup, etc.). Resolves UX-01.

### Feedback busy-state mechanism (UX-02)

- **D-08:** Client-side TypeScript submit handler. Either a new module under `wwwroot/ts/` or a small addition to `site.ts` — planner's choice based on file-size hygiene. Browser performs the POST normally; the handler only owns the visual disable + spinner.
- **D-09:** Spinner = pure CSS ring (no icon font, no asset) + button text swaps to "Sending…" on submit. Theme-aware via existing tokens.
- **D-10:** Graceful fallback: form must remain functional with JS disabled — POST still completes; user just sees no spinner. No JS-only validation required.
- **D-11:** Double-submit prevention via the same client-side handler (button `disabled` flag flips on submit). No server-side synchronizer token in scope this phase.

### CSS class strategy for inline-style migration (UI-LH-02)

- **D-12:** Keep the ROADMAP class names exactly as written: `.feedback-panel`, `.admin-feedback-detail`, `.admin-action-form`. No shared `.panel` base, no generic `.inline-form` utility. Purpose-named, scoped to feature.
- **D-13:** Migration target stylesheet: `site-common.css` (per project constraint that layout CSS goes in site-common.css, not site.css).
- **D-14:** `.feedback-panel` and `.admin-feedback-detail` already exist as classes — only the inline `style=` overlays need to move into the rule body. `.admin-action-form` is new and replaces `style="display:inline"` on the 4 admin forms (Apply archive on Index; markRead/archive/delete on Detail).
- **D-15:** Verifier check: `grep -c 'style=' Feedback/Index.cshtml AdminFeedback/Index.cshtml AdminFeedback/Detail.cshtml` must equal 0 across all three files post-migration.

### Claude's Discretion

- Exact hero copy beyond "Run ChatGPT Analysis" (label, subtitle if any) — planner chooses
- Whether the busy-state TS lives in a new module vs `site.ts` — planner chooses based on size and reuse potential
- Specific verb passed to `_MoxfieldBulkEditHint` per page — planner reads each page's submit button label and picks the matching verb
- Specific verb-noun phrasing for the new Feedback `<title>`/`<h1>` (e.g., "Send Feedback" vs "Submit Feedback") — planner picks; both satisfy the convention

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Roadmap & requirements
- `.planning/ROADMAP.md` §"Phase 2: Layout, Hierarchy & UX Copy" — phase goal, depends-on, 5 success criteria
- `.planning/REQUIREMENTS.md` §"UI Layout & Hierarchy (UI-LH)" + §"Copy & UX (UX)" — requirement text for UI-LH-01/02 and UX-01/02/03
- `.planning/PROJECT.md` — core value (ChatGPT-paste-ready output), constraints (theme system, layout CSS in site-common.css)

### Phase 01 outputs (token sources for this phase)
- `.planning/phases/01-visual-system-tokens/01-03-SUMMARY.md` — tokens shipped, theme propagation outcome, residual rem/hex backlog
- `.planning/phases/01-visual-system-tokens/VERIFICATION.md` — verifier ACCEPT 5/5; live deckflow.gg parity confirmed
- `DeckFlow.Web/wwwroot/css/site.css` `:root` block — `--cta-border`, `--link`, `--danger`, `--on-accent`, `--accent-default`, etc. consumed by this phase

### Codebase maps (scout reads)
- `.planning/codebase/STRUCTURE.md` — view layout, partial conventions
- `.planning/codebase/CONVENTIONS.md` — Razor view + TS patterns
- `.planning/codebase/ARCHITECTURE.md` — DeckFlow.Web/wwwroot/ts/ build pipeline (tsc MSBuild target)

### Target files this phase will touch
- `DeckFlow.Web/Views/Deck/Home.cshtml` — UI-LH-01 hero band + per-group `.hub-card--primary` markup
- `DeckFlow.Web/Views/Feedback/Index.cshtml` — UI-LH-02 inline-style removal; UX-02 busy state; UX-03 voice
- `DeckFlow.Web/Views/AdminFeedback/Index.cshtml` — UI-LH-02 inline-style removal (`.admin-action-form`)
- `DeckFlow.Web/Views/AdminFeedback/Detail.cshtml` — UI-LH-02 inline-style removal (panel + 4 admin-action-forms)
- `DeckFlow.Web/Views/Shared/_MoxfieldBulkEditHint.cshtml` — UX-01 parameterize with verb; update 5 call sites
- `DeckFlow.Web/Views/Deck/{ChatGptCedhMetaGap,ChatGptDeckComparison,ChatGptPackets,DeckConvert,DeckSync}.cshtml` — UX-01 pass verb arg
- `DeckFlow.Web/wwwroot/css/site-common.css` — new rules for `.feedback-panel`, `.admin-feedback-detail`, `.admin-action-form`, `.hub-card--primary`, `.hub-hero` (or planner's chosen name), feedback-button busy state spinner
- `DeckFlow.Web/wwwroot/ts/` — new busy-state handler (new file or extend `site.ts`)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Phase 01 tokens: `--cta-border`, `--link`, `--accent`, `--accent-default`, `--panel`, `--line`, `--danger`, `--on-accent` already declared in `site.css` `:root` and propagated to all 22 guild themes. UI-LH-01 directly consumes `--cta-border`.
- `wwwroot/ts/site.ts` exists as the global script — natural insertion point for a small busy-state handler if planner doesn't want a new module.
- `wwwroot/ts/` build pipeline: MSBuild `BeforeTargets="Build"` runs `tsc -p tsconfig.json`; output lands in `wwwroot/js/`. No extra wiring needed for new TS files.
- `.hub-card`, `.hub-card__title`, `.hub-card__description`, `.hub-grid`, `.hub-group`, `.hub-group__title` BEM classes are already established in `site-common.css`.

### Established Patterns
- One public type per `.cs` file (Razor partials follow same — one partial per `.cshtml`).
- Razor partials accept models via `Html.PartialAsync("_Name", modelObject)` — UX-01's verb parameterization fits this pattern cleanly.
- TS strict mode (`tsconfig.json`); existing modules export to `wwwroot/js/` and are loaded via `<script src>` in `_Layout.cshtml`.
- Theme system: layout CSS goes in `site-common.css`, not `site.css` — hard project constraint (memory: project_theme_css_architecture).

### Integration Points
- Home hub (`Home.cshtml`) already uses BEM `.hub-card`/`.hub-card__*` — `.hub-card--primary` modifier slots in cleanly.
- `_Layout.cshtml` includes `site.ts` compiled output for every page — busy-state handler attached on DOMContentLoaded can scope itself by selector (`form[asp-action="Submit"]` on /feedback or a class added to the form).
- `_MoxfieldBulkEditHint.cshtml` is included from 5 view paths — verb parameterization requires touching all 5.

</code_context>

<specifics>
## Specific Ideas

- Hero card is full-width, above all `.hub-group` sections, with ChatGPT Analysis as label.
- Per-group primaries (Comparison / Deck Sync / Card Lookup) use `.hub-card--primary` modifier; ChatGPT Analysis stays as a regular card in the Analyze grid even though it's also the hero (no removal from grid — duplicated entry-point is acceptable).
- Spinner = CSS ring + "Sending…" text swap. No icon font, no SVG asset.
- Feedback page voice landing example: `<title>Send Feedback — DeckFlow</title>` and `<h1>Send Feedback</h1>` — both verb-noun, identical phrasing acceptable.

</specifics>

<deferred>
## Deferred Ideas

- Server-side anti-double-submit token for `/feedback` (defense-in-depth) — D-11 chose client-only for now; a token mechanism is a Phase 4 (security) candidate if abuse emerges.
- Generic shared `.panel` base / `.inline-form` utility class — D-12 rejected for this phase; revisit if 3rd panel surface lands.
- Other inline `style=` instances elsewhere in the app — out of scope; UI-LH-02 is bounded to the three flagged files.
- Voice audit / rewrite of every page title and h1 — D-05 sets the convention but planner only applies it to Feedback in scope. Sweep of remaining pages is a follow-on, not this phase.
- Size bump / background tint / badge for promoted cards — D-03 rejected in favor of single-signal border; revisit if user data shows the border isn't strong enough.

</deferred>

---

*Phase: 02-layout-hierarchy-ux-copy*
*Context gathered: 2026-04-30*
