---
status: brainstorm
created: 2026-05-10
phase_candidate: v1.2 small phase OR v1.3 backlog
related: 10-AISEL-PLATFORM-DESIGN.md (v1.3 internal-symbol rename)
---

# AI-Agnostic Rename Brainstorm

## Why this exists

The three "ChatGPT" pages (`/chatgpt-packets`, `/chatgpt-deck-comparison`, `/chatgpt-cedh-meta-gap`) now target ChatGPT, Claude, **and** Gemini interchangeably as of Phase 9 (AI selector) and Phase 10 (per-AI prompt content). The "ChatGPT" prefix is now a misnomer. Two questions to resolve:

1. **What do we rename "ChatGPT Analysis" to?** (and the URLs)
2. **Are we creating "packets" or "prompts" for AI?** (artifact terminology)

---

## Q1: What to rename "ChatGPT Analysis"

Three rename angles, in order of how disruptive they are.

### Option A — Minimal: drop the brand only

| Surface | Today | Proposed |
|---|---|---|
| Page H1 | `ChatGPT Analysis` | `Deck Analysis` |
| Nav label | `ChatGPT Analysis` | `Deck Analysis` |
| Home hub card | `ChatGPT Analysis` | `Deck Analysis` |
| URL path | `/chatgpt-packets` | `/deck-analysis` |

**Pros:** matches the AI-agnostic reality; URLs become evergreen; least change for users coming back to the site.
**Cons:** loses the "this generates something for an AI" cue — first-time users may not realize they're not pasting into a chat box on the page.
**Mitigation:** add an explainer line under each H1 (see Mock A below).

### Option B — Active: frame what the user does

| Surface | Today | Proposed |
|---|---|---|
| Page H1 | `ChatGPT Analysis` | `Analyze Deck` |
| Nav label | `ChatGPT Analysis` | `Analyze Deck` |
| URL path | `/chatgpt-packets` | `/analyze-deck` |

Sister pages: `Compare Decks` (`/compare-decks`), `Find Meta Gaps` (`/find-meta-gaps`).

**Pros:** verb-first, clearer call to action; matches modern app UX patterns.
**Cons:** page titles become commands, which doesn't match nav patterns elsewhere on the site (Card Lookup, Mechanic Rules, Ask a Judge, Deck Sync, Convert Deck — all noun-form). Imposing verb-form on three pages and not the rest creates inconsistency.

### Option C — Anchored: name the artifact

| Surface | Today | Proposed |
|---|---|---|
| Page H1 | `ChatGPT Analysis` | `AI Deck Brief` |
| Nav label | `ChatGPT Analysis` | `AI Deck Brief` |
| URL path | `/chatgpt-packets` | `/ai-deck-brief` |

Sister pages: `AI Comparison Brief`, `AI Meta-Gap Brief`.

**Pros:** keeps the "this is for an AI" cue without picking a vendor; "brief" matches what the artifact actually is (a structured prompt with reference data).
**Cons:** invents a term users have to learn; "Brief" is jargon-y; harder to scan in nav.

### Recommendation: **Option A + explainer lines**

You get evergreen URLs without inventing jargon. The explainer (matches the existing `.mode-note` pattern for unclear UI from your global feedback memory) covers the discoverability gap. Detailed mock below.

---

## Q2: Packets vs Prompts

The current naming conflates two things that are actually distinct:

| Term | What it means | Where it fits |
|---|---|---|
| **Prompt** | A single text artifact the user copies into an AI chat | The thing they paste — singular, atomic |
| **Packet** | A bundle: prompt(s) + reference data + optional response — saved as the session zip | The full reusable kit |

Today on Page 1 ("ChatGPT Packets"), the page genuinely produces multiple prompts (analysis + set-upgrade + follow-up) plus a downloadable bundle. On Pages 2 and 3, there's really one prompt at a time — the "packet" framing overstates it.

### Option 1 — Prompt-first

- Page generates "an analysis prompt" (or "a comparison prompt", "a meta-gap prompt").
- The zip is "your session save" / "session zip".
- Multi-prompt pages (Page 1 only) call them "the prompts" plural.

**Pros:** simplest, matches what the user actually copies. "Session" cleanly describes the zip without overloading "packet".
**Cons:** loses some accuracy on Page 1 where the bundle is more than just a single prompt.

### Option 2 — Packet-first (current)

- Keep "packet" as the umbrella, call individual files prompts internally.
- Matches Page 1 structure exactly.

**Pros:** no copy churn on Page 1.
**Cons:** reads as more technical; misleads users on Pages 2 and 3 where there's only one prompt.

### Option 3 — Brief / Session

- The artifact is a "brief".
- The saved file is a "session".

**Pros:** both terms are user-friendly, neither is technically loaded.
**Cons:** requires teaching two new words. Pairs naturally with Q1 Option C only.

### Recommendation: **Option 1 (Prompt-first) for outward-facing copy**

The Packets page can call its multi-file output "the packet" only on that page, where the term is justified by actually containing multiple prompts. Comparison and Meta-Gap pages drop "packet" entirely — their output is *a prompt*, not a packet.

---

## Mock A — Recommended (Q1 Option A + Q2 Option 1)

### User-facing labels (before → after)

| Surface | Today | Proposed |
|---|---|---|
| **Page 1 H1** | `ChatGPT Analysis` | `Deck Analysis` |
| **Page 1 explainer** | — | *Generate a prompt to paste into ChatGPT, Claude, or Gemini.* |
| **Page 1 nav label** | `ChatGPT Analysis` | `Deck Analysis` |
| **Page 1 home hub** | `ChatGPT Analysis` | `Deck Analysis` |
| **Page 1 URL** | `/chatgpt-packets` | `/deck-analysis` (301 from old) |
| **Page 2 H1** | `Deck Comparison` | `Deck Comparison` (no change) |
| **Page 2 explainer** | — | *Generate a prompt comparing two decks. Paste into ChatGPT, Claude, or Gemini.* |
| **Page 2 URL** | `/chatgpt-deck-comparison` | `/deck-comparison` (301 from old) |
| **Page 3 H1** | `cEDH Meta Gap` | `cEDH Meta Gap` (no change) |
| **Page 3 explainer** | — | *Generate a prompt analyzing your deck against current cEDH meta. Paste into ChatGPT, Claude, or Gemini.* |
| **Page 3 URL** | `/chatgpt-cedh-meta-gap` | `/cedh-meta-gap` (301 from old) |

### Artifact noun convention

| Surface | Today | Proposed |
|---|---|---|
| Page 1 intro copy | "ChatGPT packet" | "**analysis prompt**" — when multiple, "the **prompts**" plural |
| Page 2 intro copy | "comparison packet" | "**comparison prompt**" |
| Page 3 intro copy | "meta-gap packet" | "**meta-gap prompt**" |
| Sticky save bar | "Save your work in progress to a zip file you can re-import later." | "Save your **session** to a zip file you can re-import later." |
| Download button | `Download session (.zip)` etc. | **Keep as-is** — already says "session" |

---

## Mock B — Bolder rename (Q1 Option B + Q2 Option 1)

### User-facing labels

| Surface | Today | Proposed |
|---|---|---|
| Page 1 H1 / nav / hub | `ChatGPT Analysis` | `Analyze Deck` |
| Page 1 URL | `/chatgpt-packets` | `/analyze-deck` (301 from old) |
| Page 2 H1 / nav / hub | `Deck Comparison` | `Compare Decks` |
| Page 2 URL | `/chatgpt-deck-comparison` | `/compare-decks` (301 from old) |
| Page 3 H1 / nav / hub | `cEDH Meta Gap` | `Find Meta Gaps` |
| Page 3 URL | `/chatgpt-cedh-meta-gap` | `/find-meta-gaps` (301 from old) |

Same artifact convention as Mock A (prompt-first).

**Trade-off vs Mock A:** more disruptive — Pages 2 and 3 also get H1 changes. Inconsistent with rest of site (noun-form nav).

---

## Mock C — Anchored rename (Q1 Option C + Q2 Option 3)

### User-facing labels

| Surface | Today | Proposed |
|---|---|---|
| Page 1 H1 / nav / hub | `ChatGPT Analysis` | `AI Deck Brief` |
| Page 1 URL | `/chatgpt-packets` | `/ai-deck-brief` (301 from old) |
| Page 2 H1 / nav / hub | `Deck Comparison` | `AI Comparison Brief` |
| Page 2 URL | `/chatgpt-deck-comparison` | `/ai-comparison-brief` (301 from old) |
| Page 3 H1 / nav / hub | `cEDH Meta Gap` | `AI Meta-Gap Brief` |
| Page 3 URL | `/chatgpt-cedh-meta-gap` | `/ai-meta-gap-brief` (301 from old) |

### Artifact noun convention

| Surface | Today | Proposed |
|---|---|---|
| All pages intro copy | "packet" | "**brief**" |
| Sticky save bar | "session" zip | "session" zip (same) |
| Download button | `Download session (.zip)` | `Download session (.zip)` (same) |

**Trade-off:** "brief" feels formal/jargon-y; users have to learn the term. Stronger AI-cue than Mock A but less natural.

---

## Mock D — Do nothing structural; just add explainers

### User-facing labels

| Surface | Today | Proposed |
|---|---|---|
| All H1 / nav / hub / URLs | (current) | (no change) |
| Page 1 explainer | — | *Works with ChatGPT, Claude, and Gemini — pick one in Step 2.* |
| Page 2 explainer | — | (same) |
| Page 3 explainer | — | (same) |
| Artifact terminology | "packet" | (no change) |

**Trade-off:** zero migration cost; URLs and labels stay vendor-confused. The explainer is band-aid only.

---

## URL redirect strategy (applies to Mocks A, B, C)

Add three lines in `Program.cs` middleware (before `MapControllers`):

```csharp
app.MapGet("/chatgpt-packets",          ctx => { ctx.Response.Redirect("/deck-analysis", permanent: true); return Task.CompletedTask; });
app.MapGet("/chatgpt-deck-comparison",  ctx => { ctx.Response.Redirect("/deck-comparison", permanent: true); return Task.CompletedTask; });
app.MapGet("/chatgpt-cedh-meta-gap",    ctx => { ctx.Response.Redirect("/cedh-meta-gap", permanent: true); return Task.CompletedTask; });
```

(Substitute new paths per chosen Mock.) Also need to redirect `/chatgpt-packets/upload`, `/chatgpt-packets/download`, etc. — easier to use a single catch-all `MapWhen` on the `/chatgpt-` prefix and rewrite to the new prefix.

Existing bookmarks, the browser-extension's deep links, and any external blog/Discord links — all keep working.

---

## Internal naming (deferred — separate decision)

Controllers, view models, enums (`DeckPageTab.ChatGptPackets`), service classes (`ChatGptDeckPacketService`), test classes — **don't touch** in this rename.

- Internal-only "ChatGPT" naming has zero user impact.
- Renaming has 50+ file blast radius and a high regression risk (form `name` attributes, JavaScript `data-` selectors, etc.).
- Already on the v1.3 backlog alongside the AiPlatform value-object refactor (see `10-AISEL-PLATFORM-DESIGN.md`).

Capture as v1.3 backlog item: *"Rename `ChatGpt*` → `DeckAnalysis*` internal symbols (controllers, services, view models, enums, JS selectors, CSS classes)."*

---

## What this costs to actually ship (estimate per Mock)

| Mock | Files touched | Estimated effort |
|---|---|---|
| A (recommended) | 3 views (H1 + explainer), 1 partial (nav), 1 home view (hub cards), 3 controller route attributes, ~30 `Url.Content` references in views, `Program.cs` redirects, README | Half a phase / 1 day |
| B (action verbs) | Same as A but with sister-page H1 changes too — copy churn larger | 1 day |
| C (anchored "brief") | Same as A + replace "packet" with "brief" everywhere in copy | 1 day |
| D (explainers only) | 3 views (one new line each), README | 1-2 hours |

The zip filename pattern (`{commander}-{ai}-{ts}.zip`) is unaffected by all options — no segment depends on URL.

---

## Backward-compat checklist (for Mocks A–C)

- [ ] 301 redirects for old URLs (page + `/upload` + `/download` per page)
- [ ] Browser extension (`browser-extensions/deckflow-bridge/`) — verify it doesn't hardcode old paths; if it does, ship updated extension zip
- [ ] README links updated
- [ ] Help content (`DeckFlow.Web/Help/**/*.md`) — search for `chatgpt-packets` etc.
- [ ] Sitemap / robots.txt (if present)
- [ ] Any external links/docs the user controls — Discord, social, blog posts

---

## Phase placement options

1. **Tack onto v1.2 as Phase 11** — ship before merging `v1.2` → `main`. Keeps the brand-rename in the v1.2 multi-AI story.
2. **Defer to v1.3** — bundle with the internal-symbol rename. Single coherent rename milestone.
3. **Ship as a hotfix on v1.2 after Phase 10 closes** — smaller scope, faster turnaround, but spreads the rename over two milestones.

**My pick:** Option 1 if you want the v1.2 launch to feel "AI-agnostic" cleanly; Option 2 if you want one big rename done well.

---

## UI / Visual Design Considerations

The rename is mostly copy, but the new explainer line is the only *new visual element* on three highly-trafficked pages — getting its placement and typography right matters more than the wording. Below: where the explainer goes, how it fits the existing theme system, and what changes (if anything) for nav and mobile.

### Explainer line — placement and typography

The explainer is a **page subtitle** (sometimes called a "lede"), not a section heading. It sits directly under `<h1>` and tells the user, in one line, what the page does.

**HTML pattern:**

```cshtml
<h1>Deck Analysis</h1>
<p class="page-lede">Generate a prompt to paste into ChatGPT, Claude, or Gemini.</p>
```

**Why `<p>` not `<h2>`:** screen readers walk heading hierarchy. The next semantic heading on the page is the Step 1/2/3 region, which should be `<h2>`. Slotting an `<h2>` before that for the explainer breaks hierarchy (UX skill: medium-severity heading-hierarchy issue). `<p class="page-lede">` is the right primitive.

**CSS (goes in `site-common.css`, not per-theme files):**

```css
.page-lede {
  margin: -0.25rem 0 1.5rem;     /* tighter to h1 above; full breathing room below */
  font-size: 1.125rem;            /* ~18px — larger than body, smaller than h2 */
  line-height: 1.5;               /* per Typography rule: 1.5-1.75 */
  max-width: 60ch;                /* per Typography rule: line-length 65-75 chars */
  color: var(--text-muted);       /* token; each theme defines its own muted shade */
  font-weight: 400;
}
```

**Theme additions:** each guild theme file (`site-azorius.css`, `site-rakdos.css`, etc.) only needs to define `--text-muted` if it doesn't already. No layout CSS in per-theme files (project rule).

### AI-selector callout pattern

The `_AiSelector.cshtml` partial is currently rendered at the top of Step 2 on all three pages (per Phase 9 placement decision). The rename doesn't move it — but the new H1 explainer makes the AI selector feel less buried, because it now references "ChatGPT, Claude, or Gemini" up at page-top, then the user picks one in Step 2.

**Recommendation:** **don't duplicate** the AI selector at page top. Mentioning AI choice in the explainer + the actual control in Step 2 is the right one-touchpoint pattern. Adding a second selector instance creates two surfaces of truth for the same setting.

**Optional mini-callout (defer — not in this scope):** if first-time users still don't notice the Step 2 selector, a lightweight inline pill near the explainer ("Currently set to: **Claude** · change in Step 2") would solve it without the duplicate-control problem. Capture as v1.3 polish.

### Page-heading visual hierarchy (existing → with explainer)

```
Today                          Proposed (Mock A)
─────────                      ─────────────────
[H1] ChatGPT Analysis          [H1] Deck Analysis
                               [P]  Generate a prompt to paste into
[Step 1 region]                     ChatGPT, Claude, or Gemini.
[Step 2 region]                
[Step 3 region]                [Step 1 region]
                               [Step 2 region]
                               [Step 3 region]
```

H1 stays dominant (largest, theme accent color). Lede sits one notch below in size and weight, muted color, max ~60ch wide so it doesn't sprawl on desktop. No new visual weight introduced — just one extra muted line.

### Nav consistency

`_DeckToolTabs.cshtml` has 3 ChatGPT-page entries. After rename:

| Today | Mock A |
|---|---|
| `ChatGPT Analysis` | `Deck Analysis` |
| `Deck Comparison` | `Deck Comparison` |
| `cEDH Meta Gap` | `cEDH Meta Gap` |

**Visual impact:** label width changes by 4 chars on Page 1 only. The existing `tool-nav__link` styling is fluid — no width assumption to break.

**Active-state CSS:** unchanged (`is-active` class wires the same).

**Hub cards on `Home.cshtml`:** label change on Page 1 hub card; descriptions in those cards may also reference "ChatGPT" — audit and update so the rename is internally consistent (a card titled "Deck Analysis" with copy "Generate a ChatGPT packet" reads as a bug).

### Mobile considerations

Three risks to verify at 375px viewport:

1. **Explainer wrap** — `max-width: 60ch` plus the page's own padding leaves ample room; should wrap to 2-3 lines at 375px and look clean. Typography rule: minimum 16px body text on mobile (we're at 18px — ✓).
2. **Nav labels** — current nav already wraps or scrolls at narrow widths; adding 0 chars net (rename labels are same length or shorter) means no regression. Verify on real device.
3. **Step-tab strip width** — unaffected; this is only an H1+P change above the form.

**No new media queries needed** if the existing layout uses fluid widths and `clamp()`-style headings (likely; check `site-common.css`).

### Theme-integration checklist

- [ ] Add `--text-muted` token to each theme file (`site-azorius.css`, etc.) if not already defined. Default fallback in `:root` of `site-common.css`.
- [ ] No theme-file edits beyond the token. Layout CSS for `.page-lede` lives only in `site-common.css`.
- [ ] Verify contrast: `--text-muted` against the page background must clear 4.5:1 (Accessibility: critical color-contrast rule). Particularly important for darker themes (e.g., Rakdos) where muted shades skew low-contrast.
- [ ] Visual smoke check on all 6+ guild themes; the explainer should look "obviously second-tier" but never illegible.

### Accessibility checklist (for the new explainer)

- [ ] **Color contrast** ≥ 4.5:1 across every theme (CRITICAL).
- [ ] **Heading hierarchy** preserved: H1 → P → next region's H2. Don't tempt yourself into making the explainer an `<h2>` for visual weight.
- [ ] **Reduced motion**: no animation on this element, no concern.
- [ ] **Screen-reader order**: H1 → lede → first form control reads naturally; no aria changes needed.

### What does NOT need design work

- Download buttons, AI selector partial, sticky save bar — all unchanged by the rename.
- Form layout, step navigation, error banner placement — unchanged.
- The "session" zip filename pattern — unchanged (already AI-segment-aware via Phase 10 commit `00e5bdd`).

### Visual design effort estimate

- **CSS:** ~10 lines in `site-common.css` (the `.page-lede` rule), plus a `--text-muted` token per theme file (~6 themes × 1 line = 6 lines).
- **HTML:** 3 lines per page × 3 pages = 9 lines (one new `<p class="page-lede">` per page).
- **QA:** visual pass on each theme + mobile breakpoint.

**Total visual design effort:** ~30 minutes. The brand/copy effort (Mock A's 1-day estimate above) dwarfs it.

---

## Decision capture

(Fill in when decided.)

- **Chosen mock:** _____
- **Phase placement:** _____
- **Internal-symbol rename:** v1.3 backlog (locked)
- **Decided on:** _____
- **Decided by:** _____
