---
phase: mbgap-11-cedh-mulligan-keep
plan: 06
type: execute
wave: 6
depends_on: [MBGAP-11-05]
files_modified:
  - DeckFlow.Web/e2e/manabase-cedh-keep.spec.ts
  - DeckFlow.Web/e2e/manabase-mulligan.spec.ts
autonomous: false
requirements: [MBGAP-11-AC8, MBGAP-11-AC1, MBGAP-11-AC2, MBGAP-11-AC3]
must_haves:
  truths:
    - "A cEDH-mode e2e spec asserts two headline %s render and a turn->=5 payoff is not called workable"
    - "The new spec captures and restores the keep-shapes flag's pre-test state (no cross-spec contamination)"
    - "A commander-central fixture surfaces the commander as a representative opener line"
    - "The UI is human-verified across 3 themes x 2 viewports"
  artifacts:
    - path: "DeckFlow.Web/e2e/manabase-cedh-keep.spec.ts"
      provides: "cEDH keep-shapes live-UX smoke: two headlines, no turn->=5 workable line, commander-central opener, curve-coverage (casual)"
      contains: "analysis.manabase.keep-shapes"
  key_links:
    - from: "manabase-cedh-keep.spec.ts beforeEach/afterEach"
      to: "captureOriginalFlagEnabled / restoreFlagEnabled"
      via: "flag-restore hardening pattern (commit f8f58586)"
      pattern: "restoreFlagEnabled"
---

<objective>
Add Playwright coverage for the cEDH keep-shapes redesign and human-verify the UI across themes and
viewports. Reuse the just-landed flag-restore hardening (commit f8f58586) so the shared SQLite flag
store is never left contaminated.

Purpose: Acceptance #8 (Playwright e2e green across 3 themes x 2 viewports; EOL clean) plus live-UX
proof of #1 (turn->=5 payoff not workable), #2 (two headlines), #3 (commander-central opener). The
web-page-change rule (CLAUDE.md) requires Playwright + desktop/mobile + theme coverage in addition to
the xUnit coverage from plan 05.

Output: a new `manabase-cedh-keep.spec.ts`; any churn to `manabase-mulligan.spec.ts` if the panel
markup shifts LOW-8/9 selectors; a human-verify checkpoint over the rendered panel.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md

CODEX DISPATCH NOTE (line endings): MIXED LF/CRLF repo — preserve each touched file's existing line
endings exactly. The new `.ts` spec: match the existing e2e specs' EOL (they are LF). Change only
lines that change in `manabase-mulligan.spec.ts`.

TEST EXECUTION (CLAUDE.md): never open a browser on the Windows host. Start the app with
`scripts/run-web-test.sh` (sets DECKFLOW_DISABLE_AUTO_BROWSER=true), then drive Playwright headless
from WSL: `env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test <spec> --project=chromium-desktop --project=chromium-mobile`.
Result-dependent asserts must be `test.skip`-guarded when Scryfall is unreachable (same convention as
manabase-mulligan.spec.ts:1-20). Serialize admin specs (they share the SQLite flag store + admin
throttle).
</execution_context>

<context>
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-CONTEXT.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-PATTERNS.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-05-SUMMARY.md

<interfaces>
<!-- Verified e2e scaffolding to copy. -->

manabase-mulligan.spec.ts — the template: test.describe.configure({ mode: 'serial' }) (:43);
  beforeEach acquires admin lock + captureOriginalFlagEnabled (:45-49); afterEach
  restoreFlagEnabled + releaseAdminLockForTest (:51-58); helpers setFlagEnabled(page,key,bool)
  (:102), captureOriginalFlagEnabled (:119), restoreFlagEnabled (with 1 retry + warn) (:132);
  admin lock via ./support/admin-lock (:2); synthetic CF-Connecting-IP convention (:19-20);
  PASTE_DECK shape (:30-41); submitDeck() waits on .result-panel or .error-banner and returns bool.

New flag key: 'analysis.manabase.keep-shapes' (seeded OFF — the spec must ENABLE it, then restore).
Mode selection: cEDH radio is `input[name="Mode"][value="Cedh"]` (Manabase.cshtml:168-169) — check it
  before clicking "Analyze Mana Base".
Opening-hand panel selectors: `.manabase-mulliganlens` (visible when mulligan-eval on);
  `#manabase-opening-hand`; openers `ul.manabase-mulliganlens-openers > li`. Plan 05 added the second
  headline + shape labels + curve-coverage line inside this block (confirm the exact text/selectors
  plan 05 emitted from MBGAP-11-05-SUMMARY.md before asserting).
Playwright projects: chromium-desktop (1280) + chromium-mobile (390) (playwright.config.ts:27,34).
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: cEDH keep-shapes e2e spec with flag-restore hardening</name>
  <files>DeckFlow.Web/e2e/manabase-cedh-keep.spec.ts</files>
  <action>
Create `manabase-cedh-keep.spec.ts` by copying the scaffolding of `manabase-mulligan.spec.ts`
verbatim for the shared machinery: `test.describe.configure({ mode: 'serial' })`, the admin-lock
beforeEach/afterEach, and the `setFlagEnabled` / `captureOriginalFlagEnabled` / `restoreFlagEnabled`
helpers (import or re-declare identically — do NOT hard-restore to false; capture the pre-test state
and restore THAT, per commit f8f58586). This spec must enable BOTH `analysis.manabase.mulligan-eval`
(so the panel renders) AND `analysis.manabase.keep-shapes`, and restore both to their captured states
in afterEach.

Add a commander-central cEDH `PASTE_DECK` fixture: a Winota-style shell — commander `1 Winota, Joiner
of Forces`, a fast-mana suite (Sol Ring, Arcane Signet, a couple of dorks/rituals), a low-MV payoff,
and enough lands to be keepable — so the commander is deployable ahead of curve (Shape A / commander-
central). Include a spell whose printed MV makes it a turn->=5 play with no acceleration, to prove it
is NOT surfaced as workable.

Submit via: goto /manabase, select PasteText, fill the deck, CHECK the cEDH radio
(`input[name="Mode"][value="Cedh"]`), click "Analyze Mana Base", wait on `.result-panel` or
`.error-banner` (reuse submitDeck's race), and `test.skip` when the result never appears (Scryfall
unreachable). Assertions (guarded):
  - `.manabase-mulliganlens` visible; contains the mana-keepable headline AND the new plan-keepable
    headline text (Acceptance #2 — two headlines; use the exact copy plan 05 shipped).
  - No opener `li` presents a turn->=5 spell as a workable/explosive keep (Acceptance #1): assert the
    openers list does NOT contain a "(turn 5)"/"(turn 6)" read tagged workable/keep — assert the
    "no plan by turn 4 - mulligan" copy appears for the slow line instead.
  - The commander name ("Winota") appears as/within a representative opener line (Acceptance #3,
    commander-central). Keep this assertion resilient (contains-text), skip-guarded on result.
  - No-horizontal-scroll invariant at both viewports (mirror manabase-mulligan.spec.ts:89-91).
Run under chromium-desktop and chromium-mobile (the config projects) — no per-test project override
needed; the harness runs both.
  </action>
  <verify>
    <automated>cd DeckFlow.Web && env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test e2e/manabase-cedh-keep.spec.ts --project=chromium-desktop 2>&1 | tail -20</automated>
  </verify>
  <done>Spec runs (passes or cleanly skips when Scryfall is unreachable), captures+restores both flags, and asserts the two headlines / no-turn>=5-workable / commander-central opener. Flag store left in its pre-test state.</done>
</task>

<task type="auto">
  <name>Task 2: Reconcile existing opening-hand specs with the new panel markup</name>
  <files>DeckFlow.Web/e2e/manabase-mulligan.spec.ts</files>
  <action>
Run the existing opening-hand specs against the plan-05 markup. If the second headline / shape-label
/ curve-coverage additions shifted any selector or text the LOW-8/9 specs assert on
(`manabase-mulligan.spec.ts`), update those assertions minimally to match — but keep them testing the
FLAG-OFF-equivalent / mulligan-eval behavior (the keep-shapes flag defaults OFF, so the existing specs
run with keep-shapes off and must still see today's copy). Only touch assertions that actually broke;
do not restructure the spec. If nothing broke, record "no churn needed" in the SUMMARY.
  </action>
  <verify>
    <automated>cd DeckFlow.Web && env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test e2e/manabase-mulligan.spec.ts --project=chromium-desktop --project=chromium-mobile 2>&1 | tail -20</automated>
  </verify>
  <done>manabase-mulligan.spec.ts green (or cleanly skipped) at both viewports; any churn is assertion-only and its flag restored.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <what-built>
The cEDH keep-shapes opening-hand panel: two headline %s (mana-keepable + plan-keepable),
shape-labeled representative openers, the commander surfaced for a commander-central deck, the
turn->=5 payoff shown as "no plan by turn 4 - mulligan" (not workable), and the casual curve-coverage
line — all behind the keep-shapes flag (default OFF).
  </what-built>
  <how-to-verify>
1. Start the app headless: `scripts/run-web-test.sh` (DECKFLOW_DISABLE_AUTO_BROWSER=true).
2. In /Admin/Flags enable `analysis.manabase.mulligan-eval` and `analysis.manabase.keep-shapes`.
3. Analyze a commander-central cEDH deck (Winota fixture) in cEDH mode; then a casual deck.
4. Capture screenshots at desktop 1280 + mobile 390 across THREE themes (per the UI-review rule —
   e.g. classic, nyx, azorius). Confirm for cEDH: two headlines side by side and legible; plan-keepable
   <= mana-keepable; openers carry shape labels; the slow payoff reads "no plan by turn 4 - mulligan";
   the commander appears as an opener line. For casual: the "plays a spell on ~N of first 5 turns"
   line renders. Confirm no horizontal scroll and no layout breakage on mobile or in any theme.
5. Toggle keep-shapes OFF and confirm the panel returns to today's single-headline layout (byte-
   identity backstopped by the plan-05 excision test).
  </how-to-verify>
  <resume-signal>Type "approved" or describe the visual/layout issues to fix.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| e2e admin toggle -> shared SQLite flag store | Test mutates a shared flag; the capture/restore + serial + admin-lock pattern prevents cross-spec contamination (the f8f58586 incident). |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap11-10 | Tampering | Shared flag store left dirty | mitigate | captureOriginalFlagEnabled + restoreFlagEnabled (1 retry, warn-and-continue) restore BOTH toggled flags; serial mode + admin lock serialize access. Never hard-restore to false. |
| T-mbgap11-SC | Tampering | package installs | n/a | No package installs (uses --no-install playwright already in repo). |
</threat_model>

<verification>
- `npx --no-install playwright test e2e/manabase-cedh-keep.spec.ts e2e/manabase-mulligan.spec.ts
  --project=chromium-desktop --project=chromium-mobile` green or cleanly skipped.
- Flag store returns to its pre-test state after the run (both flags restored).
- Human-verify checkpoint approved across 3 themes x 2 viewports.
- EOL: new `.ts` is LF; `git diff --stat` == `git diff --ignore-all-space --stat` for the spec files.
</verification>

<success_criteria>
- cEDH keep-shapes e2e asserts AC1/AC2/AC3 live; both flags captured+restored (no contamination).
- Existing opening-hand specs reconciled and green at both viewports.
- UI human-verified across 3 themes x 2 viewports (AC8).
</success_criteria>

<output>
Create `.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-06-SUMMARY.md` when done.
</output>
