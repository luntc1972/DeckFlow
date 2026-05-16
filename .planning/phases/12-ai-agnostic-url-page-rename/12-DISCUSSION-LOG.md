# Phase 12: AI-Agnostic URL + Page Rename - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-16
**Phase:** 12-ai-agnostic-url-page-rename
**Areas discussed:** URL slug lock, 301 redirect pattern, Artifact filename term, View file rename scope

---

## URL slug lock

### cEDH meta-gap slug

| Option | Description | Selected |
|--------|-------------|----------|
| `/cedh-meta-gap` | Preserves cEDH specificity, matches Mock A, audience signal stays in URL, symmetric with H1 "cEDH Meta Gap". | ✓ |
| `/meta-gap` | Shorter, broader. Listed as candidate in REQUIREMENTS.md. Risks losing audience signal. | |

**User's choice:** `/cedh-meta-gap`
**Notes:** Aligns with brainstorm Mock A directly; nothing about "meta gap" alone signals the cEDH audience.

### Page 1 + Page 2 slug confirmation

| Option | Description | Selected |
|--------|-------------|----------|
| `/deck-analysis` + `/deck-comparison` | Mock A defaults. Page 1 H1 changes to "Deck Analysis"; Page 2 H1 stays "Deck Comparison". Evergreen, AI-agnostic. | ✓ |
| Different slugs (Mock B verb-form) | Propose `/analyze-deck`, `/compare-decks` instead. | |

**User's choice:** `/deck-analysis` + `/deck-comparison`
**Notes:** Mock B was considered + rejected in the original brainstorm — not re-litigated.

---

## 301 redirect pattern

### Implementation surface

| Option | Description | Selected |
|--------|-------------|----------|
| `UseRewriter` middleware | Single Program.cs block with `AddRedirect(...regex..., 301)`. Keeps DeckController clean. ~12 entries. | ✓ |
| Per-route `RedirectPermanent` actions | Each old route becomes a thin action returning `RedirectPermanent(...)`. 12+ extra DeckController actions, ~100+ lines. | |
| Attribute-based RedirectRoute filter | Custom ActionFilter mapping `[LegacyRoute(...)]`. Most DRY but adds new infrastructure. | |

**User's choice:** `UseRewriter` middleware
**Notes:** DeckController already at 1000+ lines; keeping redirect plumbing out of the controller pays off in readability.

### Sub-route coverage

| Option | Description | Selected |
|--------|-------------|----------|
| All 12 sub-routes (top-level + `/download` + `/upload` for each of 3 pages) | ROADMAP SC #1 requires this; browser-extension may post directly to `/upload`. | ✓ |
| Top-level only | Faster to ship but breaks any external POSTs to old `chatgpt-packets/upload`. | |

**User's choice:** All 12 sub-routes
**Notes:** ROADMAP SC #1 phrasing locks this — not a discretionary call.

---

## Artifact filename term

| Option | Description | Selected |
|--------|-------------|----------|
| Fix fallbacks + tighten mid-segments | `deckflow-packet` → `deck-analysis`; `compare2` → `comparison`; `cedh` → `cedh-meta-gap`. AI label fallback `"chatgpt"` STAYS (user-selector default). | ✓ |
| Fix fallbacks only | Just rename commander fallback `deckflow-packet` → `deck-analysis`; mid-segments untouched. Lowest churn but `compare2` reads odd. | |
| Mock A "prompts" framing | Match brainstorm Q2 prompt-first copy: `analysis-prompt` / `comparison-prompt` / `meta-gap-prompt`. Higher churn, adds word `prompt` to filename. | |

**User's choice:** Fix fallbacks + tighten mid-segments
**Notes:** Closes the `compare2` legacy oddity while keeping filenames terse. AI label segment intentionally retained from Phase 10 commit `00e5bdd`.

---

## View file rename scope

### Razor `.cshtml` filename rename

| Option | Description | Selected |
|--------|-------------|----------|
| Rename in Phase 12 | `ChatGptPackets.cshtml` → `DeckAnalysis.cshtml`, etc. Closes Phase-11 filename-mismatch note. Controller `return View()` calls updated. | ✓ |
| Defer to Phase 13 | Keep view filenames; combine with class rename in Phase 13. Less churn now but leaves view-name-vs-URL drift. | |

**User's choice:** Rename in Phase 12
**Notes:** Phase 11 verification log explicitly flagged "Phase 12 will rename it" for these views — honor that.

### `@model` directive scope

| Option | Description | Selected |
|--------|-------------|----------|
| Keep `@model` classes unchanged | View files renamed but `@model ChatGptDeckViewModel` etc untouched. Phase 13 owns C# class rename in one combined sweep. | ✓ |
| Update `@model` alongside view rename | Bleed Phase 13's CLASSRENAME-01 into Phase 12 for the three view models. | |

**User's choice:** Keep `@model` classes unchanged
**Notes:** Phase 12 stays "user-visible/URL layer only"; bleeding class renames invites scope creep into Phase 13's territory.

---

## Claude's Discretion

- Plan/commit sequencing inside Phase 12 (URL → views → labels → sanitizer → docs vs interleaved) — pick whichever produces clean atomic commits per CLAUDE.md "one logical change per commit".
- Whether `.page-lede` is a new CSS rule or extends an existing `.mode-note`-style rule — depends on what Phase 11 left in `site-common.css`.
- `<title>` element wording (e.g. `Deck Analysis - DeckFlow` vs `DeckFlow - Deck Analysis`) — match whatever pattern other DeckFlow pages already use.

## Deferred Ideas

- C# class renames (`ChatGpt*` types) — Phase 13.
- Razor `@model` directive sweep — Phase 13.
- Mock B verb-form rename — rejected in brainstorm.
- Mock C `AI Deck Brief` artifact term — rejected in brainstorm.
- Visual regression harness across 22 guild themes — v1.0 deferred list.
- Gemini paste-limit workaround — flag-gated, deferred from v1.3.
