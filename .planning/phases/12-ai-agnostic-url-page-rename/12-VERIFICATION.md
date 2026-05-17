# Phase 12 Verification: AI-Agnostic URL + Page Rename

**Generated:** 2026-05-17 (after Plan 05 close)
**Phase:** 12-ai-agnostic-url-page-rename
**Status:** All 4 ROADMAP success criteria satisfied

This document aggregates the verification outputs from all 5 Phase 12 plans so the phase-level acceptance signals can be inspected from one place. Each success criterion (SC #1-4) below is sourced from the corresponding plan SUMMARY.

---

## SC #1 — 301 redirects (Plan 01)

**Criterion:** All 9 legacy `/chatgpt-*` paths (3 page-roots + 3 `/download` + 3 `/upload`) MUST 301-redirect to the new AI-agnostic slugs via the centralized `UseRewriter` block in `DeckFlow.Web/Program.cs`. Forwarded-headers middleware MUST precede `UseRewriter` so the 301 `Location` honors `X-Forwarded-Proto`.

**Source plan:** `12-01-SUMMARY.md`
**Commits:** `5598f9d` (feat — UseRewriter middleware), `38bb2f8` (feat — DeckController route attributes).

### 9 redirect paths (per D-04)

| Legacy path | Target slug | Status |
|---|---|:---:|
| `/chatgpt-packets` | `/deck-analysis` | 301 |
| `/chatgpt-packets/download` | `/deck-analysis/download` | 301 |
| `/chatgpt-packets/upload` | `/deck-analysis/upload` | 301 |
| `/chatgpt-deck-comparison` | `/deck-comparison` | 301 |
| `/chatgpt-deck-comparison/download` | `/deck-comparison/download` | 301 |
| `/chatgpt-deck-comparison/upload` | `/deck-comparison/upload` | 301 |
| `/chatgpt-cedh-meta-gap` | `/cedh-meta-gap` | 301 |
| `/chatgpt-cedh-meta-gap/download` | `/cedh-meta-gap/download` | 301 |
| `/chatgpt-cedh-meta-gap/upload` | `/cedh-meta-gap/upload` | 301 |

### Automated gates (from Plan 01 SUMMARY)

- `grep -cE 'Http(Get|Post)\("/chatgpt-' DeckFlow.Web/Controllers/DeckController.cs` → **0** (no remaining chatgpt- route attributes)
- `grep -cE 'Http(Get|Post)\("/(deck-analysis|deck-comparison|cedh-meta-gap)' DeckFlow.Web/Controllers/DeckController.cs` → **12** (3 page-roots × 4 verbs)
- `grep -c "AddRedirect.*chatgpt-" DeckFlow.Web/Program.cs` → **9**
- `grep -c "AddRedirect.*301" DeckFlow.Web/Program.cs` → **9**
- `grep -c "UseRewriter" DeckFlow.Web/Program.cs` → **1**
- Pipeline-order: `UseForwardedHeaders()` (L319) before `UseRewriter(` (L329) before `UseDeckFlowSecurityHeaders()` (L347) — D-05 invariant satisfied.
- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` → 0 warnings, 0 errors.

### Manual curl verification (deferred to user per `feedback_user_starts_server.md`)

User-launched dev server spot-check spec from Plan 01 verification block:

```text
curl -i http://localhost:5173/chatgpt-packets             → 301 Location: /deck-analysis
curl -i http://localhost:5173/chatgpt-deck-comparison     → 301 Location: /deck-comparison
curl -i http://localhost:5173/chatgpt-cedh-meta-gap       → 301 Location: /cedh-meta-gap
curl -i http://localhost:5173/deck-analysis               → 200
curl -i http://localhost:5173/deck-comparison             → 200
curl -i http://localhost:5173/cedh-meta-gap               → 200
```

Same pattern for the `/upload` and `/download` sub-routes — all 9 redirects MUST return 301; all 3 new page-roots MUST return 200.

**SC #1: PASSED (automated). Manual curl spot-check deferred to user.**

---

## SC #2 — Page labels + explainer lines (Plan 03)

**Criterion:** Page-1 H1 + browser `<title>` + nav-link text + hub-card title + hub-hero title all read `Deck Analysis` (was `ChatGPT Analysis`). All three AI workflow pages (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`) have a `<p class="page-lede">` explainer paragraph directly under the H1 with the exact Mock A copy. `.page-lede` CSS lives ONLY in `site-common.css` (CLAUDE.md D-07 invariant).

**Source plan:** `12-03-SUMMARY.md`
**Commits:** `6b3dbb8` (feat — `.page-lede` CSS + explainer paragraphs on 3 views), `208654b` (feat — Page-1 rebrand + 6 nav/home href flips).

### Changed labels (per D-06, D-07, D-09)

| Surface | Before | After |
|---|---|---|
| Page-1 H1 (`DeckAnalysis.cshtml:29`) | `ChatGPT Analysis` | `Deck Analysis` |
| Page-1 browser title (`DeckAnalysis.cshtml:3`) | `ChatGPT Analysis` | `Deck Analysis` |
| Nav-link Page 1 (`_DeckToolTabs.cshtml:18`) | `ChatGPT Analysis` | `Deck Analysis` |
| Hub-hero title (`Home.cshtml:13`) | `Analyze Your Deck with ChatGPT` | `Analyze Your Deck` |
| Hub-card 1 title (`Home.cshtml:21`) | `ChatGPT Analysis` | `Deck Analysis` |
| Page-2 H1 + Title | `Deck Comparison` | unchanged (already AI-agnostic per D-06) |
| Page-3 H1 + Title | `cEDH Meta Gap` | unchanged (already AI-agnostic per D-06) |

### Mock A explainer paragraphs (per D-07)

| View | Page-lede copy (exact, Mock A) |
|---|---|
| `DeckAnalysis.cshtml:30` | `Generate a prompt to paste into ChatGPT, Claude, or Gemini.` |
| `DeckComparison.cshtml:145` | `Generate a prompt comparing two decks. Paste into ChatGPT, Claude, or Gemini.` |
| `CedhMetaGap.cshtml:22` | `Generate a prompt analyzing your deck against current cEDH meta. Paste into ChatGPT, Claude, or Gemini.` |

### Href synchronization (6 flips per D-09)

3 nav-link hrefs in `_DeckToolTabs.cshtml` + 1 hub-hero + 3 hub-cards in `Home.cshtml` = 7 hrefs total all flipped to new slugs. `grep -c 'Url.Content("~/chatgpt-' _DeckToolTabs.cshtml Home.cshtml` → **0**.

### CSS invariant (per D-07 / D-08)

- `.page-lede` rule lives at `site-common.css:1393` (single source).
- `grep -rln "\.page-lede" DeckFlow.Web/wwwroot/css/ | grep -v site-common.css | wc -l` → **0** (not forked across any of the 22 guild themes — T-12-07 mitigated).

### Build

- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` → 0 warnings, 0 errors.
- `dotnet build DeckFlow.sln --configuration Release` → 0 warnings, 0 errors.

**SC #2: PASSED.**

---

## SC #3 — Artifact filenames (Plan 04 — RENAME-03)

**Criterion:** The three `Suggest*ZipFileName` helpers in `ChatGptPacketArtifactStore.cs` emit AI-agnostic artifact terminology (`deck-analysis`, `-comparison-`, `-cedh-meta-gap-`) while preserving the Phase 10 `"chatgpt"` AI-segment fallback invariant. The download/upload round-trip (T1/T4/T7) continues to work because `LoadFromZip` matches by zip CONTENT, not zip filename.

**Source plan:** `12-04-SUMMARY.md`
**Commits:** `c87ff5b` (feat — three sanitizer literal-string edits).

### Sanitizer changes (per D-10)

| Helper | Before | After |
|---|---|---|
| `SuggestPacketZipFileName` commander fallback | `"deckflow-packet"` | `"deck-analysis"` |
| `SuggestComparisonZipFileName` mid-segment | `"-compare2-"` | `"-comparison-"` |
| `SuggestCedhMetaGapZipFileName` mid-segment | `"-cedh-"` | `"-cedh-meta-gap-"` |
| All three AI-segment fallback | `"chatgpt"` | unchanged (D-10 invariant from Phase 10 commit `00e5bdd`) |

### Automated gates (from Plan 04 SUMMARY)

- `grep -c 'CreateSafePathSegment(commanderName, "deckflow-packet")' DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` → **0**
- `grep -c 'CreateSafePathSegment(commanderName, "deck-analysis")' DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` → **1**
- `grep -c "compare2" DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` → **0**
- `grep -c '"chatgpt"' DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` → **3** (one per helper — AI fallback preserved)
- Build clean (0 warnings, 0 errors).

### Round-trip checks (T1 / T4 / T7) — deferred to user

Per `feedback_user_starts_server.md` the executor does not auto-launch the dev server. The user-launched verification spec from Plan 04:

- **T1 (`/deck-analysis`):** Generate prompts, click Download. Expected filename pattern `{commander}-analysis-{ai}-{yyyymmdd-hhmmss}.zip`.
- **T4 (`/deck-comparison`):** Same. Expected `{commander}-comparison-{ai}-{ts}.zip` (was `compare2`).
- **T7 (`/cedh-meta-gap`):** Same. Expected `{commander}-cedh-meta-gap-{ai}-{ts}.zip` (was bare `cedh`).
- **Edge — empty commander on `/deck-analysis`:** Expected `deck-analysis-analysis-{ai}-{ts}.zip` (NOT `deckflow-packet-...`).
- **Edge — empty AI selector:** All three fall back to `chatgpt` segment (UNCHANGED — D-10 invariant).
- **Backward compatibility:** Old saved zips (pre-Phase-12 filenames) still load via Step-1 resume upload. `LoadFromZip` matches by zip content not filename.

**SC #3: PASSED (automated). Manual T1/T4/T7 round-trip deferred to user.**

---

## SC #4 — Docs + extension sweep (Plan 05)

**Criterion:** No hardcoded `/chatgpt-(packets|deck-comparison|cedh-meta-gap)` paths remain in any tracked `*.md`, `*.js`, or `*.json` file outside `.planning/` and outside the `UseRewriter` redirect block in `DeckFlow.Web/Program.cs`. The browser-extension package (`browser-extensions/deckflow-bridge/`) is verified clean of stale URL references; manifest version bumped iff actual JS edits occurred (D-16 conditional).

**Source plan:** `12-05-SUMMARY.md` (this plan)
**Commits:** `51bb902` (docs — README + Help + CLAUDE.md URL sweep; Task 2 was Case A NO-OP per D-16).

### Files updated (3 .md files)

| File | Hits before | Hits after | Prose update |
|---|:-:|:-:|---|
| `README.md` (lines 278, 365, 376, 437) | 4 | 0 | Line 376: "structured ChatGPT workflow" → "structured AI workflow" |
| `DeckFlow.Web/Help/cedh-meta-gap.md` (line 9) | 1 | 0 | Same prose update as README line 376 |
| `CLAUDE.md` (line 272 — Rule 3 addition) | 2 (`/chatgpt-packets`, `/chatgpt-cedh-meta-gap`) | 0 | URL strings only |

### Browser-extension state (D-16 conditional bump)

| File | Pre-edit hits | Action | Post-edit state |
|---|:-:|---|---|
| `background.js` | 0 | NO-OP (Case A) | unchanged |
| `deckflow-bridge.js` | 0 | NO-OP (Case A) | unchanged |
| `manifest.json` | 0 | version NOT bumped per D-16 | unchanged (`git diff` empty) |
| `wwwroot/extensions/deckflow-bridge.zip` | n/a | MSBuild re-zipped on rebuild | byte-identical contents |

### D-15 atomic acceptance grep gate

The phase-wide D-15 gate:

```bash
grep -rEn "/chatgpt-(packets|deck-comparison|cedh-meta-gap)" \
  --include="*.md" --include="*.js" --include="*.json" \
  --exclude-dir=".planning" --exclude-dir="node_modules" --exclude-dir="bin" --exclude-dir="obj" \
  .
```

**Output (recorded at Plan 05 completion):** *(empty — zero hits)*

The only remaining `chatgpt-(packets|deck-comparison|cedh-meta-gap)` URL strings anywhere in the repo are inside the `AddRedirect(...)` calls in `DeckFlow.Web/Program.cs`'s `UseRewriter` block — that file is `.cs` (structurally excluded from the `--include` set) and the strings are the legitimate redirect-source side of the 301 pattern.

### Build

- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` → 0 warnings, 0 errors.

**SC #4: PASSED.**

---

## Aggregate Phase 12 Status

| ROADMAP Success Criterion | Plan | Status |
|---|---|---|
| SC #1 — 301 redirects for 9 legacy paths | 01 | PASS |
| SC #2 — User-visible labels + Mock A explainer lines | 03 | PASS |
| SC #3 — AI-agnostic artifact filenames | 04 | PASS |
| SC #4 — Docs + extension sweep (D-15) | 05 | PASS |

Additional phase invariants:
- **D-05 (pipeline order):** `UseForwardedHeaders` before `UseRewriter` before security headers — verified at L319/L329/L347 of Program.cs.
- **D-12 (R100 view-file rename):** Three views renamed via `git mv` with rename detection preserving `git blame` / `git log --follow` continuity.
- **D-14 (Phase 13 surface preservation):** `ChatGpt*ViewModel` `@model` directives, action-method names (`ChatGptPackets`, `ChatGptDeckComparison`, `ChatGptCedhMetaGap`), and `DeckPageTab.*` enum values intentionally LEFT in place — those are Phase 13 (CLASSRENAME-01..03) scope.
- **D-16 (conditional manifest bump):** Honored — no JS edits to browser extension, no manifest version bump.
- **CLAUDE.md compliance:** Plain default-author commits (no `Co-Authored-By` trailer); one logical change per commit; README updated when behavior changes; no secrets in any commit; build clean.

**Phase 12 ready for merge. Phase 13 (C# class renames) unblocked.**

---
*Phase: 12-ai-agnostic-url-page-rename*
*Verification compiled: 2026-05-17 (post-Plan 05)*
