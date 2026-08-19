# Deck analysis

Deck analysis workflows, question buckets, comparisons, and cEDH meta guidance.

## Deck Analysis Workflow

The Deck Analysis page (`/deck-analysis`) guides you through a 5-step workflow. Step 2 generates the analysis prompt, Step 3 parses and renders the returned `deck_profile` JSON, Step 4 optionally generates a set-upgrade prompt using that parsed profile, and Step 5 parses and renders the returned `set_upgrade_report` JSON.

### Workflow layout modes
Three layouts are available via the toolbar: **Guided**, **Focused**, and **Expert**. They present the same underlying steps with different amounts of context and guidance text.

### Step 1 — Deck Setup
Choose an **Input method** — the page opens on **public deck URL** — and provide either a **Moxfield**/**Archidekt** deck URL or pasted deck export text. The chosen mode round-trips with the form so it survives refreshes and workflow-step navigation. The service:
- Falls back to treating leading quantity-1 entries as the commander when no Commander section header is present (Moxfield plain-text exports), then validates the inferred commander against Scryfall before continuing.
- Rejects inferred commanders that are not legal by the workflow rules: legendary creature, legendary Vehicle, or a planeswalker whose oracle text says it can be your commander.

#### Cross-tool single-deck carry-over
Within the same browser tab, DeckFlow now carries deck input between `/deck-analysis`, `/manabase`, `/cedh-meta-gap`, `/convert`, and `/deck-primer` via client-side `sessionStorage`. If you navigate from one of those single-deck tools to another, the target page prefills the saved deck URL or pasted list only when its deck field is still empty, so a value already rendered or typed there is never overwritten. When a prefill happens, the tool now shows a small inline "Restored your last deck." notice with a `Clear` action that empties the current deck field and removes the carried deck from storage. The existing `Start Over` / `Clear` controls on those tools also clear the carried deck now, so the deck does not reappear on the next navigation. The data is per-tab, clears when the tab closes, and is not stored on the server. Two-deck tools such as `/deck-comparison` and `/sync` are not covered yet.

### Step 2 — Analysis
Configure the analysis:

| Setting | Purpose |
|---|---|
| **Target Commander Bracket** | Bracket 1–5. Your AI uses this when evaluating card quality, interaction density, and upgrade suggestions. |
| **Analysis questions** | Select one or more questions from the buckets below. |
| **Card name** | Required when card-specific questions are selected. |
| **Budget amount** | Required when the budget-upgrade question is selected. |
| **Decklist export format** | Moxfield or Archidekt — required when category questions are selected; optional for versioning questions. |
| **Include card versions** | When checked, the original deck's set code and collector number are sent so your AI can preserve the exact printing for retained cards. |
| **Preferred category names** | Shown when **Update categories** is selected. One name per line; your AI will prefer these over inventing new ones. |
| **Protected cards** | Cards that must appear in every generated deck version. |

Click **Generate Analysis Packet** to build the reference data and analysis prompt. The service:
- Resolves all deck cards via Scryfall (`POST /cards/collection` in batches of 75) to supply authoritative Oracle text.
- Fetches official mechanic rules text from the WOTC rules page for any keyword mechanics found on resolved cards.
- Fetches the Commander banned list.
- Queries the Commander Spellbook API if combo questions are selected.
- Fires the banned-list fetch, set-packet fetch, and Spellbook combo lookup concurrently to minimize wait time.
- Generates a suggested AI conversation title displayed in the UI with a copy button.

The generated prompt uses `##` section headings (TASK, EVIDENCE RULES, BRACKET GUIDANCE, ANALYSIS QUESTIONS, OUTPUT FORMAT, REFERENCE DATA, DECKLIST) to keep long prompts structured.

**Reference Oracle-text recency gate (optional, off by default).** By default every reference card carries its full Oracle text. Because well-known older cards are already in the target AI's training data, that text is mostly redundant tokens. The `analysis.reference.full-oracle-text` feature flag, when an operator **disables** it, drops Oracle text from cards released more than 12 months ago (keeping it for recent or undatable printings the model may not know yet) — roughly a 30% prompt-token reduction with no measured change to analysis verdicts in cEDH testing. The flag is fail-safe: its enabled state (the default, and the state assumed if the flag store is unreachable) always keeps the legacy full-Oracle output, so the gate only ever engages on an explicit operator opt-in.

### Step 3 — Analysis Results
Paste the fenced `deck_profile` JSON block or raw JSON payload returned from your AI. You can also paste a saved `deck_profile` JSON file here directly without filling out Steps 1 and 2 again. The page validates the payload, parses it into a strongly typed model, and renders a readable summary of:
- Format and commander
- Game plan, speed, primary axes, and synergy tags
- Strengths, weaknesses, deck needs, and weak slots
- Per-question answers with basis notes
- Full deck versions when versioning questions were requested

This step is local to the returned JSON. It does not regenerate the analysis packet or call upstream services again.

### Step 4 — Set Upgrade (optional)
Select one or more recent MTG sets, or paste a condensed set packet override. The page generates a set-upgrade prompt that references the parsed deck profile and asks your AI to evaluate new cards from each set as potential inclusions, with suggested cuts, bracket-fit notes, speculative tests, and traps called out per set. For Commander/precon-style sets (`commander`, `duel_deck`, `starter`), the packet is filtered to first-print cards only so reprints don't crowd out genuinely new candidates; standard expansions are unfiltered. The set dropdown loads asynchronously from `/api/set-options` so the page renders immediately. A deck in Step 1 is required; the parsed Step 3 deck profile is optional but strongly recommended — without it your AI gets an empty schema and produces generic recommendations. A standalone `/set-upgrade-analysis` landing page explains this step for search visitors and links back into the workflow.

### Step 5 — Set Upgrade Results (optional)
Paste the fenced `set_upgrade_report` JSON block or raw JSON payload returned from your AI. The page validates the payload, parses it into a strongly typed model, and renders a readable summary of:
- Per-set panels: top adds with suggested cuts and reasoning, traps, and speculative tests
- Final shortlist broken into must-test, optional, and skip columns

Each suggested card (top adds and shortlist must-test/optional entries) also shows the card's rules text inline so you can see what it does without a separate lookup. The text is the exact Scryfall oracle text pulled from the generated set packet when that packet is available for the session; otherwise it falls back to the card text echoed by your AI in the `card_text` field.

Like Step 3, this step is local to the returned JSON. You can paste a saved `set_upgrade_report` JSON file here directly without re-running the earlier steps — Step 5 runs standalone when no deck source is present.

### Prompt output-format rules
All AI prompts generated by this app (analysis, set-upgrade, deck comparison, meta-gap) explicitly instruct your AI to return JSON inside a fenced ```` ```json ```` code block. Raw JSON outside a code block is rejected by the wording.

The analysis, deck-comparison, follow-up, and set-upgrade prompts also ground the AI against fabrication: if it encounters a card name it does not recognize, it is told to treat the card as unknown and flag it rather than guess or invent its rules text. (Earlier wording asked the AI to look the card up on scryfall.com, which a pasted-in chat model cannot do and which invited made-up card text.)

### Print results to paper
The rendered results panels on **Deck Analysis** (Step 3 / Step 5), **Deck Comparison** (Step 3), **cEDH Meta Gap** (Step 3), and **Commander Mana Base Analyzer** each include a **Print results** button beside their Download button. It opens the browser print dialog (print or save-as-PDF) against a print stylesheet that strips all site chrome — header, nav, tool tabs, timing, layout picker, the input form, and the toolbar buttons themselves — leaving only the rendered analysis on the page. Page breaks are constrained so a content section, list item, score card, or combo block is not split across pages and a heading is not stranded at a page foot; color meters and status pills keep their ink. The print layout lives in `site-common.css` (`@media print`), so it applies across every guild theme.

### Artifact saving (local download / upload)
On the **Deck Analysis** page, the Step 3 and Step 5 result panels include a **Download session (.zip)** button. The zip contains every artifact for the current run: the input summary, request context, prompts, schemas, and response JSON blobs. Files are stored only on your machine; no copy is retained server-side.

To resume a saved run later, expand **Resume from a saved session (.zip)** at the top of the form, choose the previously downloaded zip, and the page rehydrates the response JSON into Step 3 or Step 5. The browser's busy indicator runs while the upload is processed.

Zip contents:
- **/deck-analysis**: `00-input-summary.txt`, `01-request-context.txt`, `30-reference.txt`, `31-analysis-prompt.txt`, `41-deck-profile-schema.json`, `50-set-upgrade-prompt.txt`, `40-deck-profile.json`, `51-set-upgrade-response.json`, `all-prompts.txt`, `all-responses.txt`

Re-import only consumes `40-deck-profile.json` and `51-set-upgrade-response.json`; the rest rides along for your records or future AI context.

---

## Analysis Question Buckets

Questions are grouped into collapsible buckets. Buckets with pre-selected questions open automatically on page load.

| Bucket | Notable questions |
|---|---|
| **Core Deck Analysis** | Strengths/weaknesses, win condition, consistency, power level, best meta |
| **Deck Construction & Balance** | Mana curve, lands and ramp, card draw, interaction count, underperformers |
| **Strategy & Synergy** | Key synergies, anti-synergies, commander support, protect-cards, game plan |
| **Optimization & Upgrades** | Cuts for strength, budget upgrades (requires amount), missing staples, faster/competitive, board-wipe resilience |
| **Meta & Matchups** | Performance vs. archetypes, pod weaknesses, tech options, hate pieces |
| **Play Pattern & Decision Making** | Ideal opening hand, tutor priorities, when to cast the commander, common misplays |
| **Specific Card-Level Questions** | Card worth including and better alternatives can each target multiple card names, and every `[card]` question is emitted once per card you add; also includes weakest card and too many high-CMC cards |
| **Advanced / Expert-Level** | Turn clock, disruption vulnerability, keepable hand percentage, redundancy, mana-base optimization |
| **Combo Analysis (Commander Spellbook)** | Combos already in the deck, combos one card away within the color identity — both use live Commander Spellbook API data injected into the prompt |
| **Deck Versioning & Upgrade Paths** | Bracket 2/3/4/5 version, 3 named upgrade paths, assign categories, update categories |

### Deck Versioning output format
When any versioning or category question is selected, the analysis prompt instructs your AI to:
- Output the **full, complete 100-card decklist** for each generated version — no truncation, no "fill with basics" shorthand.
- Count cards before responding to confirm the total reaches 100.
- Use the deck builder's inline format when an export format is chosen:
  - **Moxfield**: `1 CardName (SET) collectorNumber` — or with categories: `1 CardName (SET) collectorNumber #Category1 #Category2`
  - **Archidekt**: `1 CardName (SET) collectorNumber [Category1,Category2]` — commander line uses `[Commander]`
- Output a **Cards Added** and **Cards Cut** diff after each decklist, comparing against the original.
- Output a `deck_profile` JSON block for each generated deck version.
- When **Include card versions** is checked, preserve the original printing (set code + collector number) for every retained card.

### Category / tag questions
- **Assign categories** — Your AI assigns functional role categories to every card in the deck. Plain text export is not supported; Moxfield or Archidekt format is required.
- **Update categories** — Your AI updates or reassigns categories using the preferred category names you provide. Preferred names are injected into the prompt; your AI may add new categories only when none of the preferred names fit.
- Basic card types (Creature, Instant, Sorcery, Enchantment, Artifact, Planeswalker, Battle) are excluded as categories. Your AI is instructed to use functional role labels instead (Ramp, Card Draw, Removal, Wipe, Tutor, Win Condition, Protection, etc.).
- For category questions, the prompt explicitly requires the final decklist to be returned only inside a fenced `text` code block so it can be pasted directly into Moxfield or Archidekt bulk edit.

### Commander Spellbook combo lookup
When either combo question is selected, the service calls the Commander Spellbook `find-my-combos` API before building the prompt:
- Returns up to 20 **included combos** (all pieces are in the deck) and up to 15 **almost-included combos** (exactly one card missing, within the deck's color identity).
- Each combo entry lists the card names, results, and up to 300 characters of instructions.
- Results are injected as a reference block in the prompt. Your AI is told to treat this data as authoritative.
- Results are cached for 30 minutes keyed by the sorted deck card list.
- API failures degrade gracefully — the analysis continues without combo data rather than failing.

---

## Deck Comparison

The Deck Comparison page (`/deck-comparison`) generates structured AI prompts for comparing two Commander decklists side by side. It lives alongside the Deck Analysis page in the Deck Tools tabs.

### Step 1 — Deck Setup
For each deck, choose an **Input method** (public deck URL or paste text — each deck toggles independently) and provide a **Moxfield**/**Archidekt** deck URL or plain-text export, then select a Commander Bracket. Optionally name each deck — the service falls back to the commander name if left blank.

### Step 2 — Generate Comparison Packet
The service:
- Parses both decklists, resolving cards via Scryfall `POST /cards/collection` in batches of 75.
- Falls back to per-card Scryfall search when a submitted name is an alternate-art or Universes Beyond printing that does not round-trip through the collection endpoint cleanly, then labels rendered decklists as `resolved name [printed as: submitted name]`.
- Queries Commander Spellbook for combos in each deck.
- Builds a comparison context document with bracket definitions, role counts (ramp, draw, interaction, wipes, recursion, closing power), mana curves, color identity, category overlap, and combo gaps.
- Generates a structured comparison prompt with `## TASK`, `## RULES`, `## COMPARISON AXES`, `## OUTPUT FORMAT`, deck sections, and comparison context. The prompt instructs your AI to produce both a human-readable comparison and a fenced `json` block matching a `deck_comparison` schema.
- Generates a follow-up prompt for iterative refinement of the comparison.

Comparison axes include: commander role and game plan, speed and setup tempo, ramp, draw, spot interaction, sweepers, recursion, closing power (including combos), resilience, consistency, mana stability, commander dependence, table fit, major overlap/differences, and five concrete cards or packages that best explain the gap.

### Step 3 — Review Results
Paste your AI's JSON response back into the form. The page parses the `deck_comparison` JSON and renders a formatted view with:
- Game plans and bracket labels for each deck
- Strengths and weaknesses per deck
- Key combos per deck
- Verdict panel: speed, resilience, interaction, mana consistency, closing power, and combo comparisons
- Shared themes and major differences
- Key gap cards or packages
- Recommended-for notes per deck
- Confidence notes (when your AI flags uncertainty)

If you continue asking follow-up questions in the same AI thread, use `32-comparison-follow-up-prompt.txt` to have your AI revise the readable comparison and regenerate the full `deck_comparison` JSON block.

### Artifact saving (local download / upload)
On the **Deck Comparison** page, the Step 3 result panel includes a **Download comparison session (.zip)** button. The zip contains every artifact for the current run: the input summary, both normalized decklists, combo summaries, context, prompts, schema, and response JSON. Files are stored only on your machine; no copy is retained server-side.

To resume a saved run later, expand **Resume from a saved session (.zip)** at the top of the form, choose the previously downloaded zip, and the page rehydrates the response JSON into Step 3. The browser's busy indicator runs while the upload is processed.

Zip contents:
- **/deck-comparison**: `00-comparison-input-summary.txt`, `10-deck-a-list.txt`, `11-deck-b-list.txt`, `12-deck-a-combos.txt`, `13-deck-b-combos.txt`, `20-comparison-context.txt`, `30-comparison-prompt.txt`, `31-comparison-schema.json`, `32-comparison-follow-up-prompt.txt`, `40-deck-comparison-response.json`

Re-import only consumes `40-deck-comparison-response.json`; the rest rides along for your records or future AI context.

### Prompt templates
The `prompt-templates/deck-comparison/` directory contains reference templates for compact and JSON-structured comparison prompts: all-in-one, competitive meta, matchup, quick verdict, JSON matchup, JSON strict return, and JSON tuning variants. See `docs/deck-comparison-prompt-cheat-sheet.md` for usage guidance.

---

## cEDH Meta Gap

The cEDH Meta Gap page (`/cedh-meta-gap`) generates a structured AI workflow for comparing your deck against recent EDH Top 16 lists for the same commander.

### Step 1 — Load Deck And Fetch References
Choose an **Input method** (public deck URL or paste text) and provide a public **Moxfield**/**Archidekt** URL or deck export text. You can optionally override the commander name. The page then queries EDH Top 16 using:

- Time period
- Sort by (`TOP` or `NEW`)
- Minimum event size
- Maximum standing

The service parses the submitted deck, removes sideboard and maybeboard cards, resolves the commander, fetches matching EDH Top 16 entries, and sorts them newest-first before display.

### Step 2 — Generate Meta-Gap Prompt
Select 1 to 3 EDH Top 16 reference decks and generate the prompt. The service builds:

- `30-meta-gap-prompt.txt`
- `31-meta-gap-schema.json`

While building the prompt, the service also:

- Resolves submitted-deck and reference-deck card names through Scryfall so alternate print names and reskins are converted to canonical Oracle names where possible.
- Normalizes split and multi-face names to the base/front name for prompt display.
- Queries Commander Spellbook for your deck and for each selected reference deck, then injects combo summaries into the prompt.
- Ranks the injected combo reference by popularity (most-played first), breaking ties by lowest mana value needed to assemble, so the highest-impact combos lead the list; combos lacking ranking data keep their original API order.
- Caps the reference-deck count at 3 to keep the prompt size reasonable once decklists and combo references are included.

The prompt is structured with clear sections:

- `ROLE`
- `EVIDENCE PRIORITY`
- `RULES`
- `INPUT DATA`
- `ANALYSIS TASK`
- `OUTPUT CONTRACT`
- `JSON SHAPE`

Your AI is instructed to:

- Write a concise human-readable meta-gap summary first.
- Then return a fenced `json` block whose top-level object is `meta_gap`.
- Prefer the supplied Commander Spellbook combo evidence over weaker inferred combo reads when they conflict.
- Fill every field, using empty strings, zero values, `false`, or empty arrays when evidence is missing.

### Step 3 — Paste Returned JSON
Paste the raw JSON or fenced `json` block back into the page. The shared JSON extractor accepts fenced responses and ignores surrounding prose or extra trailing fence noise before parsing the payload. The page renders:

- Overview and readiness score
- Win lines
- Interaction
- Speed
- Mana efficiency
- Core convergence
- Missing staples
- Potential cuts
- Top 10 adds and cuts

### Artifact saving (local download / upload)
On the **cEDH Meta Gap** page, the Step 3 result panel includes a **Download meta-gap session (.zip)** button. The zip contains every artifact for the current run: the input summary, prompt, schema, and response JSON. Files are stored only on your machine; no copy is retained server-side.

To resume a saved run later, expand **Resume from a saved session (.zip)** at the top of the form, choose the previously downloaded zip, and the page rehydrates the response JSON into Step 3. The browser's busy indicator runs while the upload is processed.

Zip contents:
- **/cedh-meta-gap**: `00-input-summary.txt`, `30-meta-gap-prompt.txt`, `31-meta-gap-schema.json`, `40-meta-gap-response.json`

Re-import only consumes `40-meta-gap-response.json`; the rest rides along for your records or future AI context.

---

