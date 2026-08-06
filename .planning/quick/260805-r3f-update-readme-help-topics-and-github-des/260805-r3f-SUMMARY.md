---
phase: quick-260805-r3f
plan: 01
subsystem: docs
tags: [seo, readme, help-topics, tool-registry, github-description]
dependency-graph:
  requires: [SEO ladder tile renames in ToolRegistry.cs]
  provides: [README/Help copy matching the shipped tile names, drafted GitHub repo description]
  affects: [README.md, DeckFlow.Web/Help/*.md, DeckFlow.Web.Tests/HelpContentServiceTests.cs]
tech-stack:
  added: []
  patterns: ["long tile title + short nav label in parentheses on first mention per section (D-B)"]
key-files:
  created: []
  modified:
    - README.md
    - DeckFlow.Web/Help/ai-methodology.md
    - DeckFlow.Web/Help/bracket.md
    - DeckFlow.Web/Help/browser-extension.md
    - DeckFlow.Web/Help/category-suggestions.md
    - DeckFlow.Web/Help/commander-categories.md
    - DeckFlow.Web/Help/convert.md
    - DeckFlow.Web/Help/cut-lab.md
    - DeckFlow.Web/Help/deck-history.md
    - DeckFlow.Web/Help/deck-sync.md
    - DeckFlow.Web/Help/manabase.md
    - DeckFlow.Web.Tests/HelpContentServiceTests.cs
decisions:
  - "D-A resolved as option A: renamed category-suggestions.md's title and updated the two coupled HelpContentServiceTests.cs assertions (lines 106-107) to match. All 6 HelpContentService tests pass."
  - "D-B (naming convention, resolved by user): on the first mention of a renamed tool within each section, pair the new long tile title with the existing short nav label in parentheses -- e.g. 'Commander Mana Base Analyzer (Mana Base)'. Subsequent mentions in that same section use the short name alone. Headings/frontmatter titles use the long name alone (no parenthetical -- a title isn't prose); the first body-prose mention in that section then carries the pairing."
  - "GitHub repository description (the other half of the plan's D-B) was NOT applied live: gh repo edit was explicitly withheld per the orchestrator's run-time constraint. README:8 was updated with the proposed text; see 'PROPOSED GH DESCRIPTION' below for the string awaiting explicit user approval before gh repo edit runs."
metrics:
  duration: ~90m
  completed: 2026-08-05
status: complete
actuals:
  tokens: 34000
  tasks: 2
  commits: 2
---

# Phase quick-260805-r3f Plan 01: Update README, help topics, and GitHub description Summary

Renamed all 7 SEO tile references across the live README (lines 1-809) and the ten
`DeckFlow.Web/Help/*.md` topics to match `ToolRegistry.cs`'s new `TileTitle` values, added
`/set-upgrade-analysis` landing-page coverage, added one Unreleased/SEO-fixes bullet documenting
the renames, and drafted (but did not yet apply live) a new GitHub repository description.

## What Was Built

- **README.md** (Task 1, commit `f22d94fe`): renamed every live tile/page/tool reference for the 7
  renamed tools, leaving all domain-term uses of the old names byte-identical. Renamed the drifted
  `## Commander Categories` heading to `## Commander Category Reference` (name-drift case, matches
  the new `TileTitle`). Added `/set-upgrade-analysis` coverage to the Step 4 "Set Upgrade" section.
  Added one bullet to `### Unreleased` / `SEO fixes:` (line 848) documenting the 7 renames -- the
  only permitted write into the frozen Release Notes region (lines 810-1026+), which is otherwise
  byte-identical to `HEAD`. Drafted the new repository description into README line 8.
- **DeckFlow.Web/Help/*.md** (Task 3 Part 1, commit `1cf2fbb8`): renamed frontmatter `title:`, the
  H1, and the first body-prose page reference in all ten help topics, applying the same
  long-name-with-short-name-parenthetical convention as README. Renamed `commander-categories.md`'s
  title (same name-drift case as README). Left the canonical trap alone --
  `deck-history.md:19`'s "Deck History JSON file" describes the file, not the page, and stays
  `Deck History`. Left `manabase.md`'s two quoted "Analyze Mana Base" button-label mentions (lines
  25, 48) unchanged after verifying the live button text in
  `DeckFlow.Web/Views/Deck/Manabase.cshtml:233` still reads `Analyze Mana Base` (the `ToolRegistry`
  `Label`, not `TileTitle`, drives that button).
- **DeckFlow.Web.Tests/HelpContentServiceTests.cs** (same commit, D-A option A): updated the two
  coupled assertions (`Assert.Equal`, `Assert.Contains`) from `"Category Suggestions"` to
  `"Commander Deck Tag Suggestions"` to match the renamed title. This is the plan's sole authorized
  `.cs` carve-out -- exactly the 2 lines named in the decision, no other test touched.

## PROPOSED GH DESCRIPTION

**Not yet applied.** Per the run's constraints, `gh repo edit` was intentionally NOT run. The text
below is drafted into `README.md:8` and is awaiting explicit user approval before anyone runs:

```
gh repo edit luntc1972/DeckFlow --description "<text below>"
```

**Proposed text (293 characters, `wc -m`, well under GitHub's 350-character cap):**

> DeckFlow unifies Moxfield/Archidekt decks with a Commander Mana Base Analyzer, Commander Bracket
> Checker, Moxfield–Archidekt Deck Sync, Deck Version Tracker, Cut Lab trimmer, and MTG Decklist
> Converter — plus paste-ready AI prompts, card/mechanic lookup, and Ask-a-Judge. Live at
> deckflow.gg.

Carries all 5 required keywords (Commander Mana Base Analyzer, Commander Bracket Checker, Deck
Version Tracker, Moxfield–Archidekt Deck Sync, MTG Decklist Converter), keeps "Live at deckflow.gg.",
and drops the long+short parenthetical convention used in body prose -- a 350-character tagline has
no room for it, and the "nav still shows the short name" concern that convention protects against
does not apply to a search-engine blurb.

**Original live GitHub description** (as of this run, unchanged): "...deterministic mana-base
analyzer, bracket check, and deck diffs — plus paste-ready AI prompts (analysis, primer, comparison,
cEDH meta-gap), card/mechanic lookup, Ask-a-Judge handoff, and a browsable MTG creator knowledge
base. Live at deckflow.gg." -- carries none of the new tile names.

## Judgment Ledger

Convention applied throughout (D-B): headings and frontmatter `title:` fields use the **long name
only**. The first body-prose reference to that tool within a section pairs
**"Long Tile Title (Short Nav Label)"**; later mentions of the *same* tool in the *same* section
revert to the short name alone, since the pairing was already established.

### README.md (live region, lines 1-809)

| # | File:line | Old text (quoted) | Verdict | New text / reason kept |
|---|---|---|---|---|
| 1 | README.md:3 | "a Deck History tool" | RENAMED | "a Deck Version Tracker (Deck History) tool" |
| 2 | README.md:3 | "a deterministic mana-base analyzer" | KEPT | Domain term -- describes the analysis method, not the tile |
| 3 | README.md:3 | "a local bracket classifier" | KEPT | Domain term -- descriptive, not the literal tile name "Bracket Check" |
| 4 | README.md:8 | repository description (lowercase "bracket check", "deck history" -- not Title Case, not part of the 7-name census) | RENAMED (separate action) | See PROPOSED GH DESCRIPTION above; drafted into the file, not yet applied live |
| 5 | README.md:136 | "`Deck History`" | RENAMED | "`Deck Version Tracker` (Deck History)" |
| 6 | README.md:136 | "...saved versions...the list evolved...that history..." | KEPT | Domain/file concept, same sentence |
| 7 | README.md:141 | "Bracket Check (`/bracket`, flag ...)" | RENAMED | "Commander Bracket Checker (Bracket Check; `/bracket`, flag ...)" |
| 8 | README.md:143 | "### Mana base analyzer" (heading, lowercase "base" -- not a census hit) | KEPT | Descriptive section title, matches the domain-phrase pattern from the worked example; no anchor points here |
| 9 | README.md:144 | "Deterministic mana-base check (`/manabase`, CLI `manabase`)" (lowercase -- not a census hit) | KEPT | Bolded lede describing the check, not the tile name |
| 10 | README.md:255 | "...processing deck sync, suggestion..." (lowercase -- not a census hit) | KEPT | Describes an API traffic category, not the page |
| 11 | README.md:278 | "Deck Sync, Deck Analysis, Mana Base, Deck Primer, and Cut Lab" | RENAMED | "Moxfield–Archidekt Deck Sync (Deck Sync), Deck Analysis, Commander Mana Base Analyzer (Mana Base), Deck Primer, and Cut Lab" |
| 12 | README.md:362 | "**Mana Base** each include a **Print results** button" | RENAMED | "**Commander Mana Base Analyzer (Mana Base)** each include..." |
| 13 | README.md:536 | "## Deck Sync" | RENAMED | "## Moxfield–Archidekt Deck Sync" |
| 14 | README.md:538 | "The Deck Sync page (`/sync`)..." | RENAMED | "The Moxfield–Archidekt Deck Sync (Deck Sync) page (`/sync`)..." |
| 15 | README.md:591 | "## Commander Categories" (name-drift -- not a literal row in the rename table) | RENAMED | "## Commander Category Reference" -- matches new commander-categories `TileTitle` |
| 16 | README.md:593 | "The Commander Categories page shows..." (name-drift) | RENAMED | "The Commander Category Reference (Category Reference) page shows..." |
| 17 | README.md:599 | "## Category Suggestions" | RENAMED | "## Commander Deck Tag Suggestions" |
| 18 | README.md:601 | "The Category Suggestions page supports..." | RENAMED | "The Commander Deck Tag Suggestions (Category Suggestions) page supports..." |
| 19 | README.md:739 | "\"Analyze Mana Base\"" (quoted button text) | KEPT | Quotes the live button label -- `Views/Deck/Manabase.cshtml:233` still renders `Analyze Mana Base` |
| 20 | README.md:739 | "...on Mana Base." | RENAMED | "...on Commander Mana Base Analyzer (Mana Base)." -- first *page-reference* mention of Mana Base in this section |
| 21 | README.md:741 | "Bracket and Mana Base had the carry mechanism" | KEPT | Second mention of Mana Base in this section -- short name only, per convention. "Bracket" (no "Check") is not a census hit and was left as informal shorthand |
| 22 | README.md:742 | "the printing-conflict resolution form on Deck Sync" | RENAMED | "...on Moxfield–Archidekt Deck Sync (Deck Sync)..." |
| 23 | README.md:739/741/742 | `deck-sync.ts` (filename, 3 mentions) | KEPT | Identifier -- never renamed |
| -- | README.md:345 | (no prior text) | ADDED | One sentence added to Step 4 pointing at the `/set-upgrade-analysis` landing page (coverage gap; not a rename) |
| -- | README.md:848 | (no prior text) | ADDED | One new bullet in the frozen `### Unreleased` / `SEO fixes:` group documenting the 7 renames -- the only permitted write into that region |

**README reconciliation (7-name census only, rows 1, 5, 7, 11-14, 17-18, 20-21; excludes row 4's
non-census description and rows 2-3, 8-10, 23's non-census/identifier items):** found = 14,
renamed = 12, kept = 2, `12 + 2 == 14`. ✓
**Name-drift "Commander Categories" case (rows 15-16, tracked separately since it is not a literal
row in the rename table):** found = 2, renamed = 2, kept = 0.

**Lowercase denominator** (case-insensitive minus case-sensitive hits, per name, live region):
Mana Base 5 ci vs 4 exact (1 lowercase, row 8); Bracket Check 2 ci vs 1 exact (1 lowercase, no
separate row -- generic "bracket" prose); Deck History 3 ci vs 2 exact (1 lowercase, within row 6's
sentence); Deck Sync 5 ci vs 4 exact (1 lowercase, row 10); Category Suggestions 3 ci vs 2 exact (1
lowercase, inside the mode-name bullets, not a page reference); Convert Deck and Category Reference:
0 ci either way.

### DeckFlow.Web/Help/*.md

| # | File:line | Old text (quoted) | Verdict | New text / reason kept |
|---|---|---|---|---|
| 24 | manabase.md:2 | `title: Mana Base` | RENAMED | `title: Commander Mana Base Analyzer` |
| 25 | manabase.md:8 | `# Mana Base` | RENAMED | `# Commander Mana Base Analyzer` |
| 26 | manabase.md:10 | "The Mana Base page (`/manabase`) scores..." | RENAMED | "The Commander Mana Base Analyzer (Mana Base) page (`/manabase`) scores..." |
| 27 | manabase.md:10 | "...whether the mana base holds up." | KEPT | Domain term, same sentence |
| 28 | manabase.md:25 | "press **Analyze Mana Base** again" | KEPT | Quotes the live button label (verified against `Manabase.cshtml:233`) |
| 29 | manabase.md:48 | "Then press **Analyze Mana Base**." | KEPT | Same button-label quote |
| 30 | manabase.md:54 | "grades the *mana base*, not the curve" | KEPT | Domain term, explicitly called out by the plan |
| 31 | manabase.md:56 | "a curve problem the mana base can't fix" | KEPT | Domain term |
| 32 | manabase.md:169 | "when the mana base already clears the important checks" | KEPT | Domain term |
| 33 | bracket.md:2 | `title: Bracket Check` | RENAMED | `title: Commander Bracket Checker` |
| 34 | bracket.md:8 | `# Bracket Check` | RENAMED | `# Commander Bracket Checker` |
| 35 | bracket.md:10 | "The Bracket Check page (`/bracket`) classifies..." | RENAMED | "The Commander Bracket Checker (Bracket Check) page (`/bracket`) classifies..." |
| 36 | deck-history.md:2 | `title: Deck History` | RENAMED | `title: Deck Version Tracker` |
| 37 | deck-history.md:8 | `# Deck History` | RENAMED | `# Deck Version Tracker` |
| 38 | deck-history.md:10 | "The Deck History page (`/deck-history`) turns..." | RENAMED | "The Deck Version Tracker (Deck History) page (`/deck-history`) turns..." |
| 39 | deck-history.md:19 | "Upload an existing Deck History JSON file" | KEPT | Canonical trap -- describes the file, not the page |
| 40 | deck-sync.md:2 | `title: Deck Sync` | RENAMED | `title: Moxfield–Archidekt Deck Sync` |
| 41 | deck-sync.md:8 | `# Deck Sync` | RENAMED | `# Moxfield–Archidekt Deck Sync` |
| 42 | deck-sync.md:10 | "The Deck Sync page (`/sync`) compares..." | RENAMED | "The Moxfield–Archidekt Deck Sync (Deck Sync) page (`/sync`) compares..." |
| 43 | convert.md:2 | `title: Convert Deck` | RENAMED | `title: MTG Decklist Converter` |
| 44 | convert.md:8 | `# Convert Deck` | RENAMED | `# MTG Decklist Converter` |
| 45 | convert.md:10 | "The Convert Deck page (`/convert`) reformats..." | RENAMED | "The MTG Decklist Converter (Convert Deck) page (`/convert`) reformats..." |
| 46 | convert.md:10 | "Unlike **Deck Sync**, there is no second deck" | RENAMED | "Unlike **Moxfield–Archidekt Deck Sync (Deck Sync)**, there is no second deck" |
| 47 | category-suggestions.md:2 | `title: Category Suggestions` | RENAMED (D-A option A) | `title: Commander Deck Tag Suggestions` |
| 48 | category-suggestions.md:8 | `# Category Suggestions` | RENAMED | `# Commander Deck Tag Suggestions` |
| 49 | category-suggestions.md:10 | "The Category Suggestions page supports..." | RENAMED | "The Commander Deck Tag Suggestions (Category Suggestions) page supports..." |
| 50 | commander-categories.md:2 | `title: Commander Categories` (name-drift) | RENAMED | `title: Commander Category Reference` |
| 51 | commander-categories.md:8 | `# Commander Categories` (name-drift) | RENAMED | `# Commander Category Reference` |
| 52 | commander-categories.md:10 | "The Commander Categories page shows..." (name-drift) | RENAMED | "The Commander Category Reference (Category Reference) page shows..." |
| 53 | ai-methodology.md:13 | "Mana Base, Bracket Check, Deck Sync, Convert Deck, Card Lookup, and Mechanic Rules" | RENAMED (all 4 renamed tiles) | "Commander Mana Base Analyzer (Mana Base), Commander Bracket Checker (Bracket Check), Moxfield–Archidekt Deck Sync (Deck Sync), MTG Decklist Converter (Convert Deck), Card Lookup, and Mechanic Rules" |
| 54 | ai-methodology.md:17 | "Deck History's evolution prompt" | RENAMED | "Deck Version Tracker's evolution prompt" -- possessive, no parenthetical per the plan's explicit worked example (grammatically awkward with one) |
| 55 | cut-lab.md:42 | "the Mana Base tool's mana-source math" | RENAMED -- overrides the plan's KEEP pointer | "the Commander Mana Base Analyzer (Mana Base) tool's mana-source math". The plan flagged this as an expected KEEP/domain-term case; on inspection it is a literal page/tool reference ("the ... tool's"), so it was renamed under the judgment rule instead |
| 56 | browser-extension.md:23 | "(Deck Sync, Deck Analysis, Mana Base, and Deck Primer)" | RENAMED -- overrides the plan's KEEP pointer | "(Moxfield–Archidekt Deck Sync (Deck Sync), Deck Analysis, Commander Mana Base Analyzer (Mana Base), and Deck Primer)". Same reasoning as row 55: this is a literal tool-name list, not a domain-term use |
| 57 | HelpContentServiceTests.cs:106 | `Assert.Equal("Category Suggestions", topic!.Title)` | UPDATED (D-A option A, authorized carve-out) | `Assert.Equal("Commander Deck Tag Suggestions", topic!.Title)` |
| 58 | HelpContentServiceTests.cs:107 | `Assert.Contains("Category Suggestions", topic.HtmlContent)` | UPDATED (D-A option A) | `Assert.Contains("Commander Deck Tag Suggestions", topic.HtmlContent)` |

**Help-files reconciliation (7-name census, rows 24-26, 28-29 excluded as button quotes, 30-32
excluded as domain, 33-38, 40-49, 53, 55-56; excludes rows 39, 50-52's file-mention/name-drift and
57-58's test-only rows):** found = 30, renamed = 27, kept = 3, `27 + 3 == 30`. ✓
**Name-drift "Commander Categories" case in commander-categories.md (rows 50-52, tracked
separately):** found = 3, renamed = 3, kept = 0.

### Grand reconciliation

- 7-name census (README + Help): found = 14 + 30 = **44**, renamed = 12 + 27 = **39**,
  kept = 2 + 3 = **5**. `39 + 5 == 44`. ✓
- Name-drift "Commander Categories" case (README + commander-categories.md): found = 2 + 3 = **5**,
  all renamed, `5 + 0 == 5`. ✓
- Two rows (55, 56) deliberately diverge from the plan's own per-file pointers after re-verifying
  the actual sentences against the judgment rule -- both explicitly said "tool"/were a literal
  tool-name list, not the domain-concept usage the plan guessed they'd be.

## Deviations from Plan

### Auto-fixed / judgment issues

**1. [Rule 1 - judgment correction] cut-lab.md:42 and browser-extension.md:23 renamed instead of
kept.** The plan's per-file pointer table predicted both would turn out to be domain terms or
already-correct names ("expected to be domain terms or already-correct names → KEEP"). On reading
the actual sentences, both are literal tool/page references (`"the Mana Base tool's..."` and a
parenthetical list of tool names), matching the RENAME branch of the judgment rule, not the KEEP
branch. Renamed both; logged as rows 55-56 above with the reasoning.

**2. [Rule 3 - minor commit-boundary deviation] README line 8 was committed in the Task 1 commit
rather than deferred to a dedicated Task 3 commit.** The plan explicitly excludes line 8 from Task
1 ("Task 3 owns it, so the README copy and the live GitHub description land together from the same
approved text"), intending two separate commits. Because this run's constraints changed Task 3's
Part 2 to *not* run `gh repo edit` (deferring live approval entirely, see PROPOSED GH DESCRIPTION),
the "land together" rationale no longer applies in the same way, and the edit was made in the same
file-editing pass as the rest of Task 1 before the commit boundary was drawn. No functional impact
-- the description text is still clearly marked as proposed/pending in this SUMMARY, and the file
content is identical either way.

None of the other deviation rules (2, 4) applied -- no missing critical functionality was
discovered, and no architectural change was needed.

## Auth Gates

None encountered.

## Known Stubs

None. No stub patterns (hardcoded empty values, "coming soon" placeholders, unwired data sources)
were introduced by this docs-only change.

## Threat Flags

None. This plan's own threat register (T-r3f-01, T-r3f-02, T-r3f-03) fully covers the surface
touched; no new surface was introduced beyond what the plan anticipated.

## Verification

- **GATE 1 (README frozen region additions-only):** PASS -- `diff` against `HEAD:README.md`'s
  `## Release Notes` .. `## License` span shows zero removed/modified lines, one added bullet.
- **GATE 2 (line endings):** PASS -- `grep -c $'\r'` returns 0 for README.md and all ten touched
  help files.
- **GATE 3 (anchor intact):** PASS -- `### Bracket classifier and balancer` still present verbatim.
- **GATE 4 (survivors have a KEPT row):** PASS -- every string the post-edit grep still finds in
  the live region and in `DeckFlow.Web/Help/` corresponds to a KEPT row above (button-label quotes,
  the deck-history.md file mention, short-name-in-parentheses pairings, and second-mention
  short-name reuse).
- **GATE 5 (title-coupled test):** PASS -- `dotnet test ... --filter
  "FullyQualifiedName~HelpContentService"` reports 6/6 passed (WSL VSTest ran successfully this
  time, no fallback needed). `dotnet build DeckFlow.sln` also succeeds with the pre-existing 9
  `CS8629` warnings in `ManabaseBaselineWeightingTests.cs` (unrelated to this change, out of scope
  per the deviation-rules scope boundary) and 0 new warnings/errors.
- **README:8 vs live GitHub description match:** **NOT run** -- `gh repo edit` was withheld per
  the orchestrator's constraint. This is an intentional, expected non-match pending user approval
  of the PROPOSED GH DESCRIPTION above.
- `git diff --stat` vs `git diff --ignore-all-space --stat`: identical on every touched file (no
  reflow churn).

## Self-Check

```
FOUND: README.md
FOUND: DeckFlow.Web/Help/ai-methodology.md
FOUND: DeckFlow.Web/Help/bracket.md
FOUND: DeckFlow.Web/Help/browser-extension.md
FOUND: DeckFlow.Web/Help/category-suggestions.md
FOUND: DeckFlow.Web/Help/commander-categories.md
FOUND: DeckFlow.Web/Help/convert.md
FOUND: DeckFlow.Web/Help/cut-lab.md
FOUND: DeckFlow.Web/Help/deck-history.md
FOUND: DeckFlow.Web/Help/deck-sync.md
FOUND: DeckFlow.Web/Help/manabase.md
FOUND: DeckFlow.Web.Tests/HelpContentServiceTests.cs
FOUND commit f22d94fe (docs(readme): rename tool references to the new SEO tile names)
FOUND commit 1cf2fbb8 (docs(help): rename tool references to the new SEO tile names)
```

## Self-Check: PASSED

## Commits

- `f22d94fe` -- docs(readme): rename tool references to the new SEO tile names
- `1cf2fbb8` -- docs(help): rename tool references to the new SEO tile names

Both commits are on `docs/seo-tile-renames`. Nothing has been pushed or fast-forwarded to main.

## Owed / Follow-ups

- **Owed: run `gh repo edit luntc1972/DeckFlow --description "<approved text>"`** once the user
  approves (or edits) the PROPOSED GH DESCRIPTION above. README:8 already carries the drafted text,
  so once approved this is a single command, no further doc edits needed.
