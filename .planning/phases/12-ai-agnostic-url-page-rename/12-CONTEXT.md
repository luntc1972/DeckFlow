# Phase 12: AI-Agnostic URL + Page Rename - Context

**Gathered:** 2026-05-16
**Status:** Ready for planning

<domain>
## Phase Boundary

Drop "ChatGPT" branding from the three multi-AI workflow pages at the URL + visible-label layer so the AI-agnostic reality of v1.2's per-AI dispatch (Phase 9 selector, Phase 10 per-AI prompt content) is reflected in what users see and bookmark.

**In scope:**
- Three new AI-agnostic URL slugs replace `chatgpt-` paths, with 301 redirects from old paths preserving inbound links + bookmarks.
- H1, top-nav, hub-card titles, `<title>`, explainer lines updated to AI-agnostic per `AI-AGNOSTIC-RENAME-BRAINSTORM.md` Mock A.
- Session zip filename sanitizer in `ChatGptPacketArtifactStore` updated for the new artifact terminology; AI-segment retained per Phase 10 commit `00e5bdd`.
- Razor view filenames (`.cshtml`) renamed to align with URL slugs.
- README, `DeckFlow.Web/Help/**/*.md`, and `browser-extensions/deckflow-bridge/` reference the new URLs.

**Out of scope (deferred to later phases):**
- C# class renames (`ChatGptDeckPacketService` etc) — Phase 13 (CLASSRENAME-01..03).
- Razor `@model` directives — kept on existing ChatGpt-prefixed view-model classes until Phase 13.
- Broader name-vs-behavior audit — Phase 14.
- `AiPlatform` value object refactor — Phase 15.

</domain>

<decisions>
## Implementation Decisions

### URL Slugs (RENAME-01)
- **D-01:** Final URL slugs locked from `AI-AGNOSTIC-RENAME-BRAINSTORM.md` Mock A:
  - `/chatgpt-packets` → `/deck-analysis`
  - `/chatgpt-deck-comparison` → `/deck-comparison`
  - `/chatgpt-cedh-meta-gap` → `/cedh-meta-gap`
- **D-02:** `cEDH` specificity preserved in slug (`/cedh-meta-gap` not `/meta-gap`) — keeps audience signal for cEDH players + symmetry with H1 "cEDH Meta Gap".

### 301 Redirect Pattern (RENAME-01)
- **D-03:** Centralized redirects via `app.UseRewriter(new RewriteOptions().AddRedirect(...))` in `DeckFlow.Web/Program.cs` (single block, ~12 entries). Old `chatgpt-` routes vanish from `DeckController` entirely — new routes get fresh `[HttpGet/Post("/deck-analysis")]` attributes; old paths handled centrally by middleware. Keeps DeckController from accumulating 12+ thin redirect actions.
- **D-04:** All 12 sub-routes covered: `/chatgpt-packets`, `/chatgpt-packets/download`, `/chatgpt-packets/upload`, plus the same triplet for `chatgpt-deck-comparison` and `chatgpt-cedh-meta-gap`. Per ROADMAP SC #1 (...plus their `/upload` / `/download` sub-routes).
- **D-05:** Forwarded-headers middleware (already mandatory before HTTPS redirect per `Program.cs:194-196`) must also precede `UseRewriter` so the redirect Location header carries the browser-visible scheme.

### Page Labels + Explainer Lines (RENAME-02)
- **D-06:** Mock A H1 changes — only Page 1 H1 changes (`ChatGPT Analysis` → `Deck Analysis`). Page 2 (`Deck Comparison`) and Page 3 (`cEDH Meta Gap`) H1 unchanged — already AI-agnostic.
- **D-07:** Each page gains a `<p class="page-lede">` explainer line under the H1, copy from `AI-AGNOSTIC-RENAME-BRAINSTORM.md` Mock A:
  - Page 1: *Generate a prompt to paste into ChatGPT, Claude, or Gemini.*
  - Page 2: *Generate a prompt comparing two decks. Paste into ChatGPT, Claude, or Gemini.*
  - Page 3: *Generate a prompt analyzing your deck against current cEDH meta. Paste into ChatGPT, Claude, or Gemini.*
- **D-08:** `.page-lede` CSS lives in `site-common.css` (cross-cutting per CLAUDE.md D-07; Phase 11 already established the section). If the class is not yet defined, Phase 12 adds it.
- **D-09:** Nav labels in `_DeckToolTabs.cshtml`, hub-card titles in `Views/Deck/Home.cshtml`, and per-page `<title>` element values updated alongside H1 — only the Page-1 strings change ("ChatGPT Analysis" → "Deck Analysis").

### Artifact Filename Sanitizer (RENAME-03)
- **D-10:** Update `ChatGptPacketArtifactStore.cs:536-543`:
  - Commander fallback in `SuggestPacketZipFileName`: `"deckflow-packet"` → `"deck-analysis"`.
  - Mid-segments tightened to match URL slugs:
    - `analysis` stays (`SuggestPacketZipFileName`).
    - `compare2` → `comparison` (`SuggestComparisonZipFileName`).
    - `cedh` → `cedh-meta-gap` (`SuggestCedhMetaGapZipFileName`).
  - Commander fallbacks `"deck-comparison"` and `"cedh-meta-gap"` already AI-agnostic — leave.
  - AI label fallback parameter `"chatgpt"` STAYS — it's the AI-selector default (user-chosen segment), not artifact branding. ChatGPT remains a valid AI choice in the selector.
- **D-11:** `Content-Disposition` headers are auto-derived by ASP.NET from the `fileName` argument to `File(bytes,"application/zip",fileName)` in `DeckController` (lines 528-530, 710-724, 747-749, 959-983). No controller string edits needed — fixing the sanitizer fixes the header transitively.

### View File Renames
- **D-12:** Razor view filenames renamed in Phase 12 (closes the filename mismatch flagged in Phase 11 verification notes):
  - `Views/Deck/ChatGptPackets.cshtml` → `Views/Deck/DeckAnalysis.cshtml`
  - `Views/Deck/ChatGptDeckComparison.cshtml` → `Views/Deck/DeckComparison.cshtml`
  - `Views/Deck/ChatGptCedhMetaGap.cshtml` → `Views/Deck/CedhMetaGap.cshtml`
- **D-13:** Controller `return View()` calls updated to reference new view names. If actions used the default convention (action-name → view-name), update either the action name OR pass an explicit view-name string. Per CLAUDE.md "one logical change per commit", view rename ships in its own commit.
- **D-14:** Razor `@model` directives keep their existing `ChatGpt*ViewModel` class names — Phase 13 owns the C# class rename (CLASSRENAME-01). Phase 12 stays strictly the user-visible/URL layer; no `@model` edits except where a view file rename forces them.

### Documentation + Extension Sweep (ROADMAP SC #4)
- **D-15:** README.md, every `DeckFlow.Web/Help/**/*.md`, and `browser-extensions/deckflow-bridge/{background.js,deckflow-bridge.js}` swept for hardcoded `chatgpt-packets` / `chatgpt-deck-comparison` / `chatgpt-cedh-meta-gap` URLs and replaced with the new slugs. Acceptance: `grep -r "chatgpt-" --include="*.md" --include="*.js" .` returns hits only inside the 301-redirect registration block in `Program.cs`.
- **D-16:** Browser extension version bump if its routes change — confirm extension Manifest V3 version bump if `background.js` URL references change.

### Claude's Discretion
- File rename sequencing within Phase 12 (URL → views → labels → sanitizer → docs vs interleaved) — pick whichever produces the cleanest atomic commits; CLAUDE.md "one logical change per commit" applies.
- Whether `.page-lede` lands as a new rule or extends an existing `.mode-note`-style rule — depends on what Phase 11 left in `site-common.css`; pick whichever keeps cascade clean.
- Razor `<title>` element wording (e.g. `Deck Analysis - DeckFlow` vs `DeckFlow - Deck Analysis`) — match whatever pattern the other DeckFlow pages already use.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + design choice
- `.planning/AI-AGNOSTIC-RENAME-BRAINSTORM.md` — Mock A is the source of truth for URL slugs, H1/nav/hub label changes, and explainer-line copy.
- `.planning/ROADMAP.md` §"Phase 12: AI-Agnostic URL + Page Rename" — phase goal, success criteria, dependency on Phase 11.
- `.planning/REQUIREMENTS.md` RENAME-01, RENAME-02, RENAME-03 — requirement text + traceability table.

### Prior-phase context that constrains Phase 12
- `.planning/phases/11-web-design-guidelines-audit-fixes/11-CONTEXT.md` — Phase 11 established `site-common.css` cross-cutting rules section (CLAUDE.md D-07) that `.page-lede` should join.
- `.planning/milestones/v1.2-phases/10-claude-gemini-artifact-optimization/10-AISEL-PLATFORM-DESIGN.md` — Phase 10 commit `00e5bdd` introduced the AI-segment in artifact filenames; preserve that segment in Phase 12 sanitizer changes.
- `.planning/milestones/v1.2-MILESTONE-AUDIT.md` — full T1-T8 manual integration test spec; Phase 12 should not regress T1-T8 round-trips.

### Project-wide constraints
- `CLAUDE.md` — tech-stack pins, public-repo rule, RestSharp+Polly handler pattern, plain commits (no Co-Authored-By trailer), "one logical change per commit".

### Code anchors (read on demand during planning)
- `DeckFlow.Web/Controllers/DeckController.cs:151-1009` — all six `chatgpt-` HTTP attribute routes + the `File()` filename hand-off sites.
- `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs:536-543` — the three `Suggest*ZipFileName` helpers.
- `DeckFlow.Web/Program.cs:194-200` — forwarded-headers + HTTPS redirect order; `UseRewriter` must slot into this pipeline.
- `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` — nav-label source for the Phase-1 swap.
- `DeckFlow.Web/Views/Deck/Home.cshtml:11-28` — hub-card href + label.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`UseRewriter` middleware** is built into ASP.NET Core — `Microsoft.AspNetCore.Rewrite` namespace ships with `Microsoft.AspNetCore.App`. No new NuGet dependency required.
- **`File(bytes, "application/zip", fileName)`** in controllers already drives `Content-Disposition` from the fileName argument — fixing `ChatGptPacketArtifactStore` propagates automatically.
- **`.mode-note` CSS pattern** in `site-common.css` is the closest analog to what `.page-lede` will become — explainer-line CSS, small muted text under a heading.

### Established Patterns
- **One logical change per commit** (CLAUDE.md) — Phase 12 plans should be split per layer: (1) UseRewriter + new route attrs, (2) view file rename + label swap + explainer lines, (3) sanitizer update, (4) docs + extension sweep.
- **Cross-cutting CSS goes in `site-common.css`** (CLAUDE.md D-07) — `.page-lede` belongs there, not in `site.css` or any guild fork.
- **`ChatGptThemeService`/`ChatGptDeckPacketService` etc class names stay until Phase 13** — Phase 12 only touches user-visible/URL surface area.

### Integration Points
- **Forwarded-headers ordering invariant** (CLAUDE.md): `app.UseForwardedHeaders()` MUST run before `UseRewriter` so 301 Location headers honor X-Forwarded-Proto. Same constraint that already protects HTTPS redirect + security headers.
- **Browser-extension bridge** (`browser-extensions/deckflow-bridge/`) posts to DeckFlow URLs from a Manifest-V3 extension. Old URLs must continue to function via 301 indefinitely; the extension itself should be updated to call new URLs directly so it doesn't depend on the redirect.
- **MSBuild zip target** (`DeckFlow.Web.csproj` `ZipDeckFlowBridge`) re-packages the browser-extension on every Build — extension URL edits propagate to the served `wwwroot/extensions/deckflow-bridge.zip` automatically.

</code_context>

<specifics>
## Specific Ideas

- Brainstorm Mock A is the user-approved direction — do not revisit Mock B (verb-form) or Mock C (`AI Deck Brief`); they were considered + rejected.
- Explainer-line copy is exact from Mock A — do not paraphrase.
- Sticky save-bar download buttons today read `Download session (.zip)` — Mock A says "Keep as-is" so no edit there.
- Phase 11 verification note: "plans referenced `Views/Deck/CedhMetaGap.cshtml` but the file on disk is `ChatGptCedhMetaGap.cshtml` — Phase 12 will rename it" — that rename is now part of D-12.

</specifics>

<deferred>
## Deferred Ideas

- **C# class renames** (`ChatGptDeckRequest`, `ChatGptDeckPacketService`, `ChatGptDeckViewModel`, etc) — explicitly Phase 13 (CLASSRENAME-01..03). Phase 12 does not edit `.cs` class names.
- **Razor `@model` directive sweep** — bleeds into Phase 13 class rename; out of Phase 12 scope.
- **Verb-form rebrand** (Mock B: `Analyze Deck`, `Compare Decks`, `Find Meta Gaps`) — rejected during brainstorm; out of scope.
- **`AI Deck Brief` artifact term** (Mock C) — rejected during brainstorm; out of scope.
- **Visual regression harness across 22 guild themes** — not Phase-12 concern; tracked in v1.0 deferred list.
- **Gemini paste-limit workaround** — flag-gated, deferred from v1.3 entirely.

</deferred>

---

*Phase: 12-ai-agnostic-url-page-rename*
*Context gathered: 2026-05-16*
