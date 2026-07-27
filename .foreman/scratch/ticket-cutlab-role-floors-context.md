TASK: UI-only addition to DeckFlow.Web/Views/Deck/CutLab.cshtml — show which commander and
bracket the Role floors defaults were derived from.

CONTEXT: The "Role floors" section (around line 722-730) has a heading and description
paragraph but never states WHICH commander/bracket/play-experience the shown defaults are
computed for, even though the per-row "Source" column already says things like
"Default for B4: 34" (bracket number only). The user wants the commander name and full
bracket/play-experience context visible at the top of this section.

Available on the view model already (no logic changes needed, purely a display addition):
- Model.Request.SelectedCommander (string) — resolved commander name, always populated by the
  time this section renders (HasResult is true).
- Model.Request.Bracket (int?) — 1-5. The same page already has a bracket label lookup table
  a few hundred lines earlier (around line 175-181): B1 Exhibition, B2 Core, B3 Upgraded,
  B4 Optimized, B5 cEDH. Reuse those exact labels for consistency (don't invent new wording).
- Model.Request.PlayExperience (string) — "Casual" | "Focused" | "cEDH".

MUST DO: In DeckFlow.Web/Views/Deck/CutLab.cshtml, inside the Role floors `<div class="panel-heading">`
block (right after the existing `<p>Minimum counts the finished 100 should keep...</p>` around
line 728), add one more line showing commander + full bracket label + play experience, e.g.
(exact wording your call, keep it terse and consistent with the existing paragraph's tone):

  <p class="cutlab-role-floors-context">Defaults shown for <strong>@Model.Request.SelectedCommander</strong> — Bracket @Model.Request.Bracket (<bracket label>) · @Model.Request.PlayExperience play.</p>

Look at how the existing bracket-label array is declared near line 175 and either factor it into
a small reusable local (if trivial) or just inline the same 5-value lookup — do NOT change the
existing radio-button markup near line 175-188. Escape/encode values normally (Razor `@` auto-encodes,
just don't use Html.Raw).

Add a `.cutlab-role-floors-context` rule to DeckFlow.Web/wwwroot/css/site-common.css near the
other `.cutlab-*` role-floor rules (grep for `.cutlab-role-group__help` for placement/style —
match its existing look: muted color, small margin) so it reads like the other help/context text
on this page, not a big new visual element.

MUST NOT:
- Touch any other section of CutLab.cshtml.
- Change any C# view-model/service code — SelectedCommander/Bracket/PlayExperience already exist
  and are already populated; this is display-only.
- Touch any e2e/vitest/xUnit test file.
- Change line endings on touched lines (LF, per .gitattributes) — preserve every other line
  byte-for-byte.

VERIFICATION REQUIRED:
- `dotnet build` from repo root, must be 0 errors/warnings.
- Start the app per scripts/run-web-test.sh (DECKFLOW_DISABLE_AUTO_BROWSER=true) and confirm via
  curl/grep on the rendered HTML (or a quick Playwright script) that after importing a pool with
  a bracket/commander selected, the new line renders with the right commander name, bracket
  label, and play experience — don't just eyeball the Razor, prove the render.
- Run the existing cut-lab e2e specs that touch the Role floors section
  (cut-lab-structure.spec.ts) to confirm nothing broke.

OUTPUT FORMAT: start final message with DONE / DONE_WITH_CONCERNS / NEEDS_CONTEXT / BLOCKED,
then the diff and verification results. Do not commit — leave changes staged/unstaged for review.

WRITE SET: DeckFlow.Web/Views/Deck/CutLab.cshtml, DeckFlow.Web/wwwroot/css/site-common.css
