---
phase: manabase-research-gap-closure
plan: 10
type: execute
wave: 10
depends_on: ["09"]
files_modified:
  - DeckFlow.Web/Views/Deck/Manabase.cshtml
  - DeckFlow.Web/Models/ManabaseDisplay.cs
  - DeckFlow.Web/wwwroot/css/site-common.css
  - DeckFlow.Web/e2e/manabase-ux-polish.spec.ts
  - README.md
autonomous: false
requirements: [MBGAP-UX]
must_haves:
  truths:
    - "The castability table shows only the worst/actionable rows by default with an explicit expander revealing all rows"
    - "Long card names on mobile castability cards wrap or ellipsize with the full name still reachable (no silent hard clip)"
    - "The verdict card's castability-table cross-reference is mode-aware: cEDH mode never references a table that does not render"
    - "The 'Reading your deck' plain-language issues render adjacent to (or inside) the verdict summary card, not ~800px below it"
    - "Every result section has a real semantic heading and an 'On this page' anchor list renders when results are present"
    - "The result header carries a persistent mode indicator (Casual/cEDH chip) and the cEDH two-lens row is not lopsided"
  artifacts:
    - path: "DeckFlow.Web/Views/Deck/Manabase.cshtml"
      provides: "Capped castability table + merged verdict narrative + anchor nav + mode chip + mode-aware copy"
    - path: "DeckFlow.Web/e2e/manabase-ux-polish.spec.ts"
      provides: "Playwright guards: default row cap + expander, cEDH no-dangling-ref, anchor nav present, mobile name no-clip"
  key_links:
    - from: "Manabase.cshtml castability table"
      to: "ManabaseDisplay helper (row-cap threshold logic)"
      via: "default-visible row subset + details/expander for the rest"
      pattern: "progressive disclosure"
---

<objective>
UX polish of the manabase results page, closing the 2026-07-12 UX research findings
(.planning/ui-design/manabase-ux-research.md — HIGH 1-3 and MED 4-7; LOW items 8-10
are explicitly out of scope / backlog). Measured problem: casual result page is
5,661px desktop / 15,674px mobile, driven almost entirely by an unbounded 65-row
castability table where ~50 rows carry no decision value; plus a cEDH dangling
cross-reference, duplicated verdict narrative, near-zero semantic headings, and a
mode indicator buried mid-page.

Decision already made by research (do NOT relitigate): the page stays ONE page for
both audiences — no route split. All fixes are in-place progressive disclosure and
copy/structure fixes.

Purpose: shrink the page to its decision-relevant core, especially on mobile, and
remove the cEDH copy/layout defects.
Output: view + display-helper changes, CSS, an e2e guard spec, human visual sign-off.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/ui-design/manabase-ux-research.md
@.planning/phases/manabase-research-gap-closure/RESEARCH.md

<interfaces>
- DeckFlow.Web/Views/Deck/Manabase.cshtml — 839-line single view; castability table ~lines 668-714 (Casual only); verdict summary card ~229-285; "Reading your deck" plain-language block ~494-512 (flag ShowPlainLanguage); cEDH placeholder note ~715-718; context metadata "Mode:" line ~479-486. Line numbers approximate — plans 04/06 touch this file in earlier waves; re-locate at execution time.
- DeckFlow.Web/Models/ManabaseDisplay.cs — static presentation helper; put row-cap/threshold logic here (unit-testable), not inline Razor.
- Layout CSS goes in site-common.css, NEVER site.css (guild themes are standalone forks; token additions go in :root of each theme file — avoid needing new tokens; reuse existing chip/panel classes).
- Mobile tables use the manabase-table--card pattern with data-label cells — the name-clip fix must work inside that pattern.
- e2e conventions: reuse manabase spec setup (paste-mode deck submit); server via scripts/run-web-test.sh; drive with env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test; headless only, never a Windows host browser.
- Anchor nav: plain in-page anchor links + h3 headings; no new JS framework. A no-JS <details> expander for the capped table is acceptable and preferred over new TS.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Cap castability table + fix mobile name clipping</name>
  <read_first>
    - .planning/ui-design/manabase-ux-research.md (HIGH-1, HIGH-2)
    - DeckFlow.Web/Views/Deck/Manabase.cshtml (castability table block)
    - DeckFlow.Web/Models/ManabaseDisplay.cs
  </read_first>
  <action>
    Default-visible rows = spells below a "good" threshold (reuse the existing low/ok/good chip
    tiering: show low+ok rows, minimum 10 / maximum ~20 visible), with a summary line
    ("Showing the N hardest casts — M more at 92%+ are fine") and a no-JS expander
    (<details> or equivalent) revealing the full list. Threshold/subset selection lives in
    ManabaseDisplay (unit-testable). Fix mobile hard-clip of long card names inside
    manabase-table--card: allow wrapping (preferred) or CSS ellipsis with the full name
    rendered accessibly (e.g. wrap in an element whose full text remains in the DOM and
    is not visually clipped when wrapped). CSS changes in site-common.css only.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln 2>&1 | tail -3; dotnet test DeckFlow.Web.Tests --filter ManabaseDisplay 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - Casual result renders ≤ ~20 castability rows by default; expander reveals all
    - ManabaseDisplay subset logic unit-tested (boundary: all-good deck shows minimum rows; all-bad deck caps at max)
    - Long names (e.g. double-faced "A // B") readable on 390px viewport — no mid-word hard clip
  </acceptance_criteria>
  <done>Table capped with expander; mobile names readable; unit tests green.</done>
</task>

<task type="auto">
  <name>Task 2: Verdict narrative merge + mode-aware copy + mode chip + cEDH row fix</name>
  <read_first>
    - .planning/ui-design/manabase-ux-research.md (HIGH-3, MED-4, MED-6, MED-7)
    - Manabase.cshtml verdict card, "Reading your deck" block, two-lens row, cEDH placeholder
  </read_first>
  <action>
    (a) Move the plain-language "Reading your deck" issues (when ShowPlainLanguage) to render
    directly beneath the verdict summary card content — one contiguous verdict block; remove the
    later duplicate placement. (b) Make the verdict card's "Full list in the castability table
    below" sentence mode-aware: Casual keeps it; cEDH replaces it with copy that references the
    color-findings table (which does render) or drops the reference. (c) Add a persistent mode
    chip to the Result header ("Casual analysis" / "cEDH analysis") next to the verdict chip.
    (d) Fix the cEDH lopsided two-lens row: when Simulated cast rate is absent, let the Karsten
    card span the full row (CSS grid/flex change in site-common.css) or place the cEDH meta-range
    panel in the second slot.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln 2>&1 | tail -3</automated>
  </verify>
  <acceptance_criteria>
    - cEDH result contains NO reference to a castability table that is not rendered
    - Plain-language issues adjacent to verdict card (single narrative block)
    - Mode chip visible in Result header in both modes
    - cEDH two-lens row has no empty half-width hole
  </acceptance_criteria>
  <done>Copy mode-aware; verdict unified; mode chip present; cEDH row balanced.</done>
</task>

<task type="auto">
  <name>Task 3: Semantic headings + "On this page" anchor nav + e2e guards</name>
  <read_first>
    - .planning/ui-design/manabase-ux-research.md (MED-5)
    - An existing manabase e2e spec (setup reuse); 09's manabase-lens-visual.spec.ts if present
  </read_first>
  <action>
    Give every result section a real heading (h3, styled like current section labels — visual
    parity, semantic gain): Karsten source check, Simulated cast rate, Untapped sources, Opening
    hand, Reading your deck, Ramp & draw, Command-zone castability, Color findings, Castability.
    Add ids to each and render a compact "On this page" anchor list at the top of the Result
    panel (results state only). The footer blocks below the castability table (unsupported-
    interactions disclosure, download form, swap-suggestion prompt, "How the analysis works",
    "This deck's numbers") stay EXACTLY as they are — collapsed details at the bottom, no
    restructuring — but get a shared anchor id and a final "Details" link in the anchor list
    so they are reachable without blind scrolling. Then create DeckFlow.Web/e2e/manabase-ux-polish.spec.ts asserting:
    (1) default castability row count ≤ cap and expander reveals all; (2) cEDH result has no
    "castability table below" text; (3) anchor nav present with working fragment links;
    (4) 390px viewport: a long known card name is fully readable (not clipped) in the table;
    (5) mode chip visible in both modes. Run full e2e for the manabase specs. Update README
    (analysis page behavior changed).
  </action>
  <verify>
    <automated>scripts/run-web-test.sh &amp; sleep 8; env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test manabase 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - Result sections have h3 headings + ids; anchor list renders with results
    - manabase-ux-polish.spec.ts green headless; existing manabase specs still green
    - README updated
  </acceptance_criteria>
  <done>Headings + anchor nav shipped; all manabase e2e green.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <what-built>Manabase results page UX polish: capped castability table with expander, unified verdict narrative, mode-aware cEDH copy, mode chip, balanced cEDH lens row, semantic headings + anchor nav, mobile name readability.</what-built>
  <how-to-verify>
    1. Review desktop + mobile screenshots (Casual and cEDH results) captured after the change — compare against ux-shots/manabase-audit/ baselines (casual mobile was 15,674px; expect a large reduction).
    2. Confirm: verdict block reads as one narrative; cEDH has no dangling table reference; anchor links jump correctly; long card names readable on mobile; at least 2 guild themes spot-checked.
  </how-to-verify>
  <resume-signal>Type "approved" or describe visual issues to fix.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| rendered page → user | Presentation-only change; no new input surface, no new endpoints |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgapux-01 | Information disclosure | capped table hides a genuinely weak card | mitigate | cap is tier-based (all low/ok rows always shown), expander always available, summary line states what is hidden |
| T-mbgapux-SC | Tampering | NuGet installs | accept | No packages; view/CSS/e2e only |
</threat_model>

<verification>
- ManabaseDisplay subset unit tests green; full manabase e2e suite green headless.
- Human approves 2-viewport, 2-mode screenshots (blocking checkpoint).
</verification>

<success_criteria>
HIGH 1-3 + MED 4-7 from the UX research closed: page length driven by the capped table drops sharply on mobile, cEDH copy/layout defects gone, verdict unified, page navigable via anchors, mode always visible. LOW 8-10 remain backlog.
</success_criteria>

<output>
Create `.planning/phases/manabase-research-gap-closure/10-SUMMARY.md` when done.
</output>
