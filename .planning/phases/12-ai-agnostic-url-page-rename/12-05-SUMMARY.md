---
phase: 12-ai-agnostic-url-page-rename
plan: 05
subsystem: documentation
tags: [docs, readme, help, browser-extension, url-rename, ai-agnostic, d-15-gate]

# Dependency graph
requires:
  - phase: 12-ai-agnostic-url-page-rename
    plan: 01
    provides: New /deck-analysis, /deck-comparison, /cedh-meta-gap routes live on DeckController; legacy chatgpt-* paths 301-redirect to new slugs.
  - phase: 12-ai-agnostic-url-page-rename
    plan: 02
    provides: Razor view files renamed to AI-agnostic names — Plan 05 only references the URL slugs, not view filenames.
  - phase: 12-ai-agnostic-url-page-rename
    plan: 03
    provides: H1, top-nav, hub-card labels, browser title strings all swapped to AI-agnostic copy — Plan 05 docs reference the new slugs and prose terminology.
provides:
  - "README.md references the new AI-agnostic URL slugs in all four artifact-list bullets + the cEDH Meta Gap intro paragraph"
  - "DeckFlow.Web/Help/cedh-meta-gap.md (in-app Help page) intro paragraph updated to new slug + 'AI workflow' prose"
  - "CLAUDE.md Entry Points listing updated to new slugs so repo-wide D-15 grep gate passes"
  - "Browser-extension package (browser-extensions/deckflow-bridge/) verified clean — zero hardcoded chatgpt- URL references; D-16 conditional manifest version bump correctly suppressed (no JS edits)"
  - "Phase 12 ROADMAP success criterion #4 closed (no hardcoded chatgpt- paths remain outside permanent redirect registration)"
affects: [13-class-rename]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "D-15 atomic doc-sweep — every hardcoded URL slug in *.md, *.js, *.json across the repo (outside .planning/ and the Program.cs rewriter block) replaced in a single logical commit so partial replacement cannot leave the repo in a half-renamed state"
    - "D-16 conditional version bump — manifest version is bumped iff actual JS edits occurred; the per-D-16 verification gate (no JS edits → manifest UNCHANGED) is structurally enforced"

key-files:
  created: []
  modified:
    - README.md
    - DeckFlow.Web/Help/cedh-meta-gap.md
    - CLAUDE.md
  unchanged_verified:
    - browser-extensions/deckflow-bridge/background.js
    - browser-extensions/deckflow-bridge/deckflow-bridge.js
    - browser-extensions/deckflow-bridge/manifest.json

key-decisions:
  - "CLAUDE.md line 272 added to commit scope (Rule 3 deviation) — file is not listed in the plan's `files_modified` array, but it contains `/chatgpt-packets` and `/chatgpt-cedh-meta-gap` string literals in its Entry Points listing. The D-15 acceptance grep does not exclude CLAUDE.md, so leaving it unmodified would fail the phase-wide gate. Edit is narrow: swap the three URL strings only; surrounding listing intact."
  - "README line 376 (and Help/cedh-meta-gap.md line 9) prose rewritten — 'structured ChatGPT workflow' → 'structured AI workflow' per plan task 1 step 3. Other 'ChatGPT' references in the same files (e.g., 'paste into ChatGPT', section headers like '## ChatGPT cEDH Meta Gap', the line 600 Scryfall-usage 'ChatGPT workflow uses the same batch endpoint') intentionally NOT updated — they fall outside the plan's narrow URL-grep-hit scope, or the surrounding sentence refers to the actual AI tool (vendor name preserved per plan step 3)."
  - "Browser-extension verified as Case A NO-OP per D-16 — `grep -rn chatgpt- browser-extensions/` returned zero hits. Per D-16's conditional-bump rule, manifest.json `version` field was NOT incremented. `git diff browser-extensions/deckflow-bridge/manifest.json` is empty (post-edit invariant satisfied)."

patterns-established:
  - "Pattern: Repo-wide D-15 acceptance grep MUST cover CLAUDE.md (root) in addition to README + Help — auto-generated developer-docs files at the repo root are inside the grep search path and therefore inside the gate."
  - "Pattern: Conservative prose-update scope — only update 'ChatGPT workflow' / 'ChatGPT packet' when the surrounding sentence is on (or directly above) a grep hit. Avoids unintended rebranding of legitimate vendor-name references."

requirements-completed: [RENAME-01]

# Metrics
duration: ~5min
started: 2026-05-17T01:48:00Z
completed: 2026-05-17T01:52:00Z
tasks: 2
files_modified: 3
files_unchanged_verified: 3
---

# Phase 12 Plan 05: Documentation + Extension Sweep Summary

**Closed Phase 12 ROADMAP success criterion #4 (no hardcoded chatgpt- paths outside permanent-redirect registrations) by sweeping README.md (4 URL bullets + 1 prose line), DeckFlow.Web/Help/cedh-meta-gap.md (1 URL + 1 prose line), and CLAUDE.md (1 URL listing) to AI-agnostic slugs, and verifying the deckflow-bridge browser extension has zero hardcoded chatgpt- URL references (Case A NO-OP per D-16 — no edits, no manifest version bump). Phase-wide D-15 acceptance grep across *.md / *.js / *.json (excluding .planning/ and the Program.cs rewriter block) returns ZERO hits. Build clean against `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` (0 warnings, 0 errors).**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-05-17T01:48:00Z
- **Completed:** 2026-05-17T01:52:00Z
- **Tasks:** 2 (Task 1 = commit; Task 2 = Case A NO-OP)
- **Files modified:** 3 (README.md, DeckFlow.Web/Help/cedh-meta-gap.md, CLAUDE.md)
- **Files unchanged but verified:** 3 (browser-extensions/deckflow-bridge/{background.js, deckflow-bridge.js, manifest.json})

## Accomplishments

- **README.md (4 URL bullets + 1 prose update):**
  - Line 278: `**/chatgpt-packets**:` → `**/deck-analysis**:` (zip-contents bullet under "ChatGPT Analysis Workflow" section).
  - Line 365: `**/chatgpt-deck-comparison**:` → `**/deck-comparison**:` (zip-contents bullet under "ChatGPT Deck Comparison" section).
  - Line 376: `The cEDH Meta Gap page (\`/chatgpt-cedh-meta-gap\`) generates a structured ChatGPT workflow for comparing your deck...` → `The cEDH Meta Gap page (\`/cedh-meta-gap\`) generates a structured AI workflow for comparing your deck...` — URL swap + prose update per plan task 1 step 3.
  - Line 437: `**/chatgpt-cedh-meta-gap**:` → `**/cedh-meta-gap**:` (zip-contents bullet under "ChatGPT cEDH Meta Gap" section).
- **DeckFlow.Web/Help/cedh-meta-gap.md (1 URL + 1 prose update):**
  - Line 9: `The cEDH Meta Gap page (\`/chatgpt-cedh-meta-gap\`) generates a structured ChatGPT workflow...` → `The cEDH Meta Gap page (\`/cedh-meta-gap\`) generates a structured AI workflow...`
- **CLAUDE.md (Rule 3 — required for phase-wide gate):**
  - Line 272 Entry Points listing: replaced `/chatgpt-packets`, `/chatgpt-comparison`, `/chatgpt-cedh-meta-gap` literals with `/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`. File is not in `plan.files_modified` but the D-15 grep does not exclude root-level CLAUDE.md — see Deviations below.
- **Browser extension (Case A NO-OP per D-16):**
  - `grep -rn chatgpt- browser-extensions/` → ZERO hits. No JS file edits required.
  - `manifest.json` `version` field deliberately NOT incremented per D-16 conditional bump rule. `git diff browser-extensions/deckflow-bridge/manifest.json` is empty.
  - MSBuild `ZipDeckFlowBridge` re-zipped `wwwroot/extensions/deckflow-bridge.zip` on the post-edit build (timestamp updated, contents byte-identical to pre-edit zip).
- **Build verification:**
  - `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` → 0 warnings, 0 errors.
  - `wwwroot/extensions/deckflow-bridge.zip` exists, size 5930 bytes.
- **D-15 acceptance gate satisfaction:**
  - `grep -rEn "/chatgpt-(packets|deck-comparison|cedh-meta-gap)" --include="*.md" --include="*.js" --include="*.json" --exclude-dir=".planning" --exclude-dir="node_modules" --exclude-dir="bin" --exclude-dir="obj" .` → ZERO hits.
  - System reminder D-15 gate (excluding `.planning/` and `DeckFlow.Web/Program.cs`) → ZERO hits.

## Task Commits

Per CLAUDE.md "one logical change per commit", the plan ships as ONE commit (Task 1) — Task 2 is a Case A NO-OP per D-16 and therefore has no commit:

1. **Task 1: Sweep README.md + Help/cedh-meta-gap.md + CLAUDE.md for chatgpt- URL references** — `51bb902` (docs)
   - 3 files changed, 6 insertions(+), 6 deletions(-).
   - Single atomic commit so the D-15 grep gate is satisfied at HEAD (partial replacement would leave the repo half-renamed).
2. **Task 2: Verify browser-extension URL state (Case A NO-OP per D-16)** — *no commit*
   - `grep -rn chatgpt- browser-extensions/` returned zero hits; no edits made; manifest.json deliberately unchanged.

## Files Created/Modified

- `README.md` — 4 URL string swaps + 1 prose rewrite ("structured ChatGPT workflow" → "structured AI workflow" at line 376). Section headers `## ChatGPT Analysis Workflow`, `## ChatGPT Deck Comparison`, `## ChatGPT cEDH Meta Gap` deliberately left intact (out of plan's narrow grep-hit scope).
- `DeckFlow.Web/Help/cedh-meta-gap.md` — 1 URL swap + 1 prose rewrite (same as README line 376). Help-page H1 `# ChatGPT cEDH Meta Gap` deliberately left intact (Phase 13 / Phase 14 surface).
- `CLAUDE.md` — 1 URL listing swap on line 272 only. No edits to any other CLAUDE.md content. Auto-generated developer-profile section also untouched.

## Files Verified Unchanged (Case A per D-16)

- `browser-extensions/deckflow-bridge/background.js` — 0 hits in pre-edit grep, no edits made.
- `browser-extensions/deckflow-bridge/deckflow-bridge.js` — 0 hits in pre-edit grep, no edits made.
- `browser-extensions/deckflow-bridge/manifest.json` — version field deliberately NOT bumped per D-16 conditional rule; `git diff` empty post-commit.

## Decisions Made

- **CLAUDE.md added to commit scope (Rule 3 — blocking issue).** The plan's `files_modified` array lists README + Help + extension files but not CLAUDE.md. Pre-commit grep showed CLAUDE.md line 272 contains `/chatgpt-packets` and `/chatgpt-cedh-meta-gap` in its Entry Points listing. The D-15 acceptance grep (`grep -rE "/chatgpt-(packets|deck-comparison|cedh-meta-gap)" --include="*.md" --exclude-dir=".planning" .`) does not exclude root-level CLAUDE.md, so leaving the file untouched would fail the phase-wide gate. Per executor deviation Rule 3 ("auto-fix blocking issues"), I added the minimal CLAUDE.md edit to the same logical commit. The edit is narrowly scoped: swap 3 URL literal strings in the Entry Points list bullet only; no other CLAUDE.md content touched.
- **Section headers (README + Help H1) deliberately untouched.** The plan's `<interfaces>` block lists exactly 4 README occurrences and 1 Help occurrence — all are URL string sites or directly surrounding prose. The README section headers (`## ChatGPT Analysis Workflow`, `## ChatGPT Deck Comparison`, `## ChatGPT cEDH Meta Gap`) and the Help-page H1 (`# ChatGPT cEDH Meta Gap`) are not in the grep-hit list and are not exact-matches for "ChatGPT workflow" / "ChatGPT packet" prose; updating them goes beyond the plan's narrow scope. Phase 14 (broader name-vs-behavior audit) is the canonical home for those edits.
- **Line 600 (Scryfall usage) NOT updated.** The README line 600 reads "The ChatGPT workflow uses the same batch endpoint to resolve authoritative Oracle text for all deck cards." This is a Scryfall-integration description, not a page reference, and the line is not a grep hit. Plan task 1 step 3 explicitly preserves "any phrase where 'ChatGPT' refers to the actual AI tool" — the surrounding context here is "Scryfall usage", so the phrase stays.
- **Conservative prose-update scope.** The plan task 1 step 3 instruction is to inspect the surrounding sentence at each grep hit. Lines 278, 365, 437 are bullet-list entries that begin with the URL slug — no surrounding "ChatGPT workflow" prose to update. Only line 376 (README) and line 9 (Help) had prose to update. Both received the same treatment per the plan's worked example.

## Deviations from Plan

### Rule 3 - Blocking issue: CLAUDE.md not in plan.files_modified but needed for D-15 gate

- **Found during:** Task 1 enumerative grep (the first `grep -rnE` step of the task action).
- **Issue:** Plan task 1's enumerative grep (`grep -rnE "/chatgpt-(packets|deck-comparison|cedh-meta-gap)" --include="*.md" --exclude-dir=".planning" .`) returned a hit at `CLAUDE.md:272`, but the plan's `files_modified` array did not list CLAUDE.md. The D-15 acceptance gate (which the plan defines as the success criterion) is repo-wide and does not exclude root-level CLAUDE.md.
- **Fix:** Added the CLAUDE.md line 272 edit to the same Task 1 commit. Edit scope: 3 URL literal-string swaps inside the existing Entry Points bullet only. No other CLAUDE.md content modified. This satisfies the D-15 gate without introducing new behavior or new docs.
- **Why Rule 3 not Rule 4:** This is a "wrong list" / blocking-issue category — the plan's enumerative grep would have caught this hit, so the omission from `files_modified` is a transcription gap not an architectural choice. No new architectural decision required; the mapping (chatgpt-* → new slugs) is already locked by D-01.
- **Files modified:** CLAUDE.md (in addition to README.md + Help/cedh-meta-gap.md).
- **Commit:** `51bb902` (same as Task 1's main commit — the CLAUDE.md edit is logically part of the same atomic doc sweep per D-15).

No other deviations. The plan otherwise executed exactly as written:
- Browser-extension Case A NO-OP confirmed by pre-edit grep (`grep -rn chatgpt- browser-extensions/` → 0 hits). No JS file edits made.
- D-16 conditional bump rule honored: `manifest.json` version field deliberately unchanged; `git diff` empty post-commit.
- D-15 atomic gate satisfied at HEAD: 0 hits across *.md / *.js / *.json (excluding .planning/, node_modules/, bin/, obj/).

## Issues Encountered

- **`dotnet` CLI not on `PATH` inside the WSL worktree shell** — used the Windows-side `/mnt/c/Program Files/dotnet/dotnet.exe` per Plan 01-04 SUMMARY guidance. No commits or workspace changes resulted.
- **`DeckFlow.Web/node_modules/` missing on worktree spawn** — copied from the main repo per Plan 01-04 SUMMARY guidance. `node_modules` is gitignored; `git status --short` confirmed no contamination of the commit set (only the 3 expected files staged).

## Manual Smoke-Test Status

Deferred to user — per `feedback_user_starts_server.md` memory the executor does not auto-launch the dev server. The plan's `<verification>` block specifies the following spot checks for the user to run after starting the dev server (`scripts/run-web.sh` / `run-web.ps1`):

- Open `README.md` in a GitHub or local Markdown renderer; visually confirm:
  - Line 278: artifact-list bullet starts with `**/deck-analysis**:` (was `**/chatgpt-packets**:`).
  - Line 365: artifact-list bullet starts with `**/deck-comparison**:` (was `**/chatgpt-deck-comparison**:`).
  - Lines 376 + 437: cEDH Meta Gap page reference reads `/cedh-meta-gap` (was `/chatgpt-cedh-meta-gap`); the intro sentence at line 376 reads "structured AI workflow" (was "structured ChatGPT workflow").
- Open `DeckFlow.Web/Help/cedh-meta-gap.md` in a renderer — confirm URL references `/cedh-meta-gap` and prose reads "structured AI workflow".
- In-app Help check (visit `/help/cedh-meta-gap` on the running dev server) — confirm rendered page reflects the new slug + prose. The Help page H1 `# ChatGPT cEDH Meta Gap` is intentionally unchanged (Phase 14 scope).
- Phase-wide D-15 grep (run from repo root, recorded in this summary):
  ```bash
  grep -rEn "/chatgpt-(packets|deck-comparison|cedh-meta-gap)" \
    --include="*.md" --include="*.js" --include="*.json" \
    --exclude-dir=".planning" --exclude-dir="node_modules" --exclude-dir="bin" --exclude-dir="obj" \
    .
  ```
  Recorded output: empty (zero hits). Proof of D-15 gate satisfaction at plan-completion HEAD.

## Defer Notes

- **README section headers** (`## ChatGPT Analysis Workflow`, `## ChatGPT Deck Comparison`, `## ChatGPT cEDH Meta Gap`) — out of plan scope (not grep-hits). Phase 14 (broader name-vs-behavior audit) is the canonical home for those edits.
- **Help-page H1** (`# ChatGPT cEDH Meta Gap`) — out of plan scope (not a grep-hit; D-06 said only Page 1 H1 changes for the live web pages). Phase 14 / 15 surface.
- **C# class renames** (`ChatGptDeckPacketService`, `ChatGptPacketArtifactStore`, etc.) — Phase 13 (CLASSRENAME-01..03) per D-14.
- **`AiPlatform` value object refactor** — Phase 15.

## Threat Surface Scan

No new attack surface introduced beyond the plan's `<threat_model>`:

- **T-12-12 (stale-doc social-engineering)** — mitigated; every grep-detectable stale `/chatgpt-*` URL in *.md files (outside `.planning/`) replaced with new slug.
- **T-12-13 (outdated browser-extension talking to wrong endpoint)** — mitigated by Phase 12 Plan 01's 301 redirect block; the extension verified to have zero stale URLs, so the 301 safety net is unused but in place for any future drift.
- **T-12-14 (information disclosure via PR diff)** — accept; public repo (`luntc1972/DeckFlow`); no secrets in any edited file. Verified `git diff` for all three edited files contains only the documented URL/prose changes.

No new threat flags raised. All edits flow through documentation surfaces with no trust-boundary changes.

## Next Phase Readiness

- **Phase 12 ROADMAP success criterion #4 closed** — "no hardcoded `chatgpt-` paths remain in any tracked file outside of permanent-redirect registrations". D-15 grep confirms.
- **Phase 13 (C# class renames)** unblocked — Phase 12 user-visible + URL + filename surface is fully AI-agnostic. Phase 13 may proceed with `ChatGptDeckPacketService` → `DeckPacketService` etc. class renames without disturbing user-visible state.
- **Phase 14 (broader name-vs-behavior audit)** has a clean baseline — the remaining "ChatGPT" prose in README section headers, Help-page H1, and Scryfall-usage line 600 are documented in Defer Notes for Phase 14 to evaluate.

## Self-Check: PASSED

- File `README.md` exists; `grep -cE "/(deck-analysis|deck-comparison|cedh-meta-gap)" README.md` returns 5 (≥1 of each).
- File `DeckFlow.Web/Help/cedh-meta-gap.md` exists; contains `/cedh-meta-gap` (count: 1).
- File `CLAUDE.md` exists; line 272 references new slugs only (no remaining `/chatgpt-(packets|deck-comparison|cedh-meta-gap)` strings).
- Files `browser-extensions/deckflow-bridge/{background.js,deckflow-bridge.js,manifest.json}` exist; `grep -rn chatgpt- browser-extensions/` returns 0 hits; `git diff browser-extensions/deckflow-bridge/manifest.json` is empty.
- Commit `51bb902` present in `git log --oneline`.
- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` returns 0 warnings + 0 errors.
- D-15 acceptance grep across `--include="*.md" --include="*.js" --include="*.json"` (excluding `.planning/`, `node_modules/`, `bin/`, `obj/`): 0 hits.
- System reminder D-15 gate (excluding `.planning/` and `DeckFlow.Web/Program.cs`): 0 hits.

---
*Phase: 12-ai-agnostic-url-page-rename*
*Plan: 05*
*Completed: 2026-05-17*
