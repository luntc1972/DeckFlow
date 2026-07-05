# Phase 85: `chatgpt-*` Naming Cleanup — Context

**Gathered:** 2026-07-05
**Status:** Ready for planning
**Source:** Interactive scope + naming decisions (inline, no discuss-phase)

<domain>
## Phase Boundary

Rename the `chatgpt-*` / `ChatGpt*` identifiers that name the **model-agnostic
prompt-artifact builder** to AI-agnostic equivalents, with byte-identical rendered
output and no behavior change. The tool builds prompt packets the user pastes into
ChatGPT / Claude / Gemini — so the generic "chatgpt" branding in identifiers is a
misnomer to clean up.

**In scope (user chose the BROADEST scope — "Everything incl C#"):**
1. Kebab `chatgpt-*` CSS class names across the 25 theme forks + `site-common.css`
   + `site.css` (+ `site-commander-table.css`, `site-mobile.css`, `site-theme-overrides.css`).
2. Kebab `chatgpt-*` `data-*` attribute NAMES (e.g. `data-chatgpt-download-submit`,
   `data-chatgpt-print`, `data-chatgpt-cedh-*`) and `chatgpt-*` string-literal
   class/attribute selectors in TS + Razor views.
3. camelCase / PascalCase `ChatGpt*` TypeScript symbols (`ChatGptUiMode`,
   `parseChatGptStep`, `clearChatGptPacketsState`, `registerChatGptDownloadHandler`, …).
4. C# `ChatGpt*` types, interfaces, DI registrations, view models, and tests
   (`ChatGptDeckPacketService`, `IChatGptDeckPacketService`, and every `ChatGpt*`
   symbol — 38 refs across ~14 `.cs` files). The prompt-artifact core value rides
   this service; behavior MUST stay identical.
</domain>

<decisions>
## Implementation Decisions (LOCKED)

### D1 — Naming convention
- Kebab identifiers: `chatgpt-<stem>` → `prompt-<stem>` (stem preserved: `chatgpt-packets-form`
  → `prompt-packets-form`, `chatgpt-step-tab` → `prompt-step-tab`, `data-chatgpt-print`
  → `data-prompt-print`).
- camelCase/PascalCase code symbols: `ChatGpt` → `Prompt` (`ChatGptUiMode` → `PromptUiMode`,
  `ChatGptDeckPacketService` → `PromptDeckPacketService`, `IChatGptDeckPacketService` →
  `IPromptDeckPacketService`). File names follow the type (one public type per file =>
  rename the `.cs`/`.ts` file too where the type name drives the filename).

### D2 — Scope = EVERYTHING (kebab + camelCase TS + C#)
User explicitly chose the broadest scope. AICLEAN-03's grep `chatgpt-*` → 0 covers the
kebab set; the camelCase/C# `ChatGpt*` renames go beyond that grep (they are not
`chatgpt-*`-hyphen matches) but are in scope by user decision for full consistency.
Extend the grep-clean acceptance to also assert zero `ChatGpt` (any case) in `css/`,
`ts/`, `Views/`, AND the `.cs` sources — EXCEPT the D3 keep-list below.

### D3 — CRITICAL: do NOT rename genuine ChatGPT-MODEL references (keep-list)
The prompt tool targets a **ChatGPT / Claude / Gemini trio**, and per project memory the
three prompt variants are intentionally decoupled/duplicated. Any `ChatGpt*` symbol,
class, attribute VALUE, label, or copy that genuinely denotes **the ChatGPT model as one
of that trio** (vs. generic tool branding) MUST be kept — renaming it to `Prompt*` would
be semantically wrong and collapse the three-way distinction. The researcher MUST classify
every `chatgpt`/`ChatGpt` occurrence as either **(a) generic-branding → rename** or
**(b) ChatGPT-model-variant → KEEP**, and the plan carries an explicit keep-list. When in
doubt, KEEP and flag for review — a wrong rename here breaks the core prompt-artifact value.

**D3 keep-list confirmed by research + user sign-off (2026-07-05):** KEEP all of —
the 7 `ChatGpt{Domain}PromptVariant` classes (+ their `Claude*`/`Gemini*` siblings),
`AiPlatform.ChatGpt`, the `*-chatgpt-prompt.txt` zip-artifact filenames (trio siblings),
AND all user-visible "ChatGPT" COPY/text (Manabase `ChatGptSwapPrompt` emitted copy,
JudgeQuestions "ChatGPT prompt generator" heading, ContentKb button label, Core doc
comment, FeatureFlagCatalog/ToolRegistry description strings). Rationale: genuine ChatGPT
product references, and changing rendered text would break D4 byte-identical render. Phase
85 stays a pure IDENTIFIER cleanup — no user-visible copy changes.

**Corrected counts (research):** CSS 1555/25 files, TS 268/4, Views 329/15, C# 368/88 —
but the C# total is dominated by KEEP (model-trio); real C# RENAME surface ≈ a dozen files.

### D4 — Byte-identical render + green everything
- Rendered HTML/CSS output byte-identical to pre-rename (pure identifier swap; no
  selector reordering, no specificity change, no declaration edits).
- Full Playwright e2e suite unchanged/green; `dotnet build DeckFlow.sln` 0/0; all xUnit
  tests pass (C# rename touches tests — update in lockstep).
- Acceptance grep: zero `chatgpt-*` in css/ts/Views; zero `ChatGpt`/`chatgpt` outside the
  D3 keep-list across css/ts/Views/*.cs.

### D5 — Attribute/string VALUES that are behavioral CONTRACTS
Some `chatgpt-*` occurrences are not just cosmetic identifiers but runtime contracts:
`data-cache-key="chatgpt-packets|chatgpt-deck-comparison|chatgpt-cedh-meta-gap"`,
`data-sync-panel="chatgpt-deck-url|chatgpt-deck-text"`, any sessionStorage/localStorage
keys, Content-Disposition download filenames, and any client↔server key the C# side emits
or the JS reads. These MUST be renamed in **lockstep on both sides** (server value +
client selector), OR explicitly KEPT if they cross into persisted / prod / cross-tool
state that a rename would break (cf. Phase 74 cross-tool sessionStorage persistence).
The researcher maps every such value; the plan states rename-lockstep vs keep per value.
No silent client-only rename that desyncs a contract.

### D6 — Format & commit discipline
Changed-lines-only, LF endings, no unrelated reflow (CSS carve-outs + changed-lines gate
per CLAUDE.md). Commit per logical group (e.g. CSS forks, shared CSS, TS, C# service, C#
tests, views). Plain default-author commits, no Co-Authored-By trailer. Update README only
if behavior/user-facing text changes (a pure rename should not).

### Claude's Discretion
- Plan wave/sequencing (interface-first: rename the C# type + TS symbols before/after the
  kebab CSS as the planner sees safest for byte-identical proof).
- Whether to capture a pre-rename computed-style / rendered-HTML baseline (à la Phase 84
  Task 0) to prove byte-identical, vs. rely on git-diff-is-rename-only + e2e. Recommend a
  cheap rendered-output baseline given the 25-fork blast radius.
- How to prove "byte-identical render" concretely (baseline snapshot diff vs. structural
  git-diff assertion that only identifiers changed).
</decisions>

<canonical_refs>
## Canonical References

- `.planning/REQUIREMENTS.md` — AICLEAN-01/02/03 (authoritative acceptance).
- `.planning/ROADMAP.md` (Phase 85 section) — goal + success criteria + counts.
- `CLAUDE.md` — theme CSS fork model, changed-lines format gate, CSS carve-outs,
  prompt-variant decoupling, commit rules, byte-identical constraint.
- `.planning/phases/84-theme-semantic-token-migration/` — Phase 84 established the
  25-fork edit discipline + the headless baseline-capture pattern (run-web-test.sh,
  `DECKFLOW_DISABLE_AUTO_BROWSER=true`, `npx --no-install playwright test`).
- `.planning/phases/85-chatgpt-naming-cleanup/85-RESEARCH.md` — authoritative inventory,
  D3 KEEP-LIST vs RENAME tables, contract-value map, ordering hazards, Validation Architecture.
- NOTE (research correction 2026-07-05): `ChatGptDeckPacketService` / `IChatGptDeckPacketService`
  named in old CLAUDE.md architecture **no longer exists** — Phase 83's PKTSVC split replaced it
  with 4 services that carry NO "ChatGpt" naming. Ignore that stale ref; use RESEARCH.md's real
  surface (~a dozen generic C# files after excluding the model-trio keep-list).
</canonical_refs>

<specifics>
## Specific Ideas / Known Surfaces (from inventory 2026-07-05)

- CSS: 25 files carry `chatgpt-*` (all theme forks + site.css/site-common.css/
  commander-table/mobile/theme-overrides), ~1553 class refs. Top stems: `chatgpt-ui-mode`,
  `chatgpt-packets-form`, `chatgpt-question-bucket`, `chatgpt-context-note`,
  `chatgpt-instructions`, `chatgpt-layout-picker`, `chatgpt-step-*` family,
  `chatgpt-helper-panel`, `chatgpt-cedh-reference-table`, `chatgpt-sticky-download`.
- TS: `busy-indicator.ts`, `deck-sync.ts`, `moxfield-extension-bridge.ts` — 72 kebab +
  142 camelCase `ChatGpt*` refs. `deck-sync.ts` holds the bulk: `ChatGptUiMode` type,
  `parse/getDefault/setChatGpt*` helpers, `data-chatgpt-*` query selectors,
  `data-sync-panel="chatgpt-deck-*"`, `data-chatgpt-download-submit`, `data-chatgpt-print`.
- Views (8): `DeckPrimer`, `DeckAnalysis`, `DeckComparison`, `CedhMetaGap`, `Manabase`,
  `ContentKb/Detail`, `Shared/_FormError`, `Shared/_WorkflowStepTabs` — 291 refs.
- C#: 38 `ChatGpt*` refs across ~14 `.cs` files (service, interface, view models, DI, tests).
- Contract-bearing VALUES to classify (D5): `data-cache-key="chatgpt-*"`,
  `data-sync-panel="chatgpt-deck-url|deck-text"`, download filenames, any storage keys.
</specifics>

<deferred>
## Deferred / Out of Scope
- Any functional, behavioral, layout, or COLOR change (byte-identical only).
- Typography / `font-size` → `var(--fs-*)` migration → Phase 86 (D3 handoff from Phase 84).
- Genuine ChatGPT-model-variant identifiers (D3 keep-list) — intentionally retained.
- UI-SPEC design contract — N/A: a byte-identical rename introduces no design change,
  so this phase runs `--skip-ui` (no UI-SPEC gate).
</deferred>

<scope_fence>
## Scope Fence
ALLOWED: identifier renames (chatgpt→prompt / ChatGpt→Prompt) in CSS classes, data-*
attribute names, TS/Razor string selectors, TS symbols, C# types/interfaces/DI/tests/
filenames, and lockstep client+server renames of contract VALUES per D5.
FORBIDDEN: any declaration/value/color/layout edit; reordering selectors; renaming a
genuine ChatGPT-model-variant reference (D3); touching font-size (Phase 86); mass-reflow
of unchanged lines; new dependencies; behavior changes.
</scope_fence>

---

*Phase: 85-chatgpt-naming-cleanup*
*Context gathered: 2026-07-05 (interactive scope + naming decisions)*
