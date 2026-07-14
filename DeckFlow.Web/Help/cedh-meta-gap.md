---
title: cEDH Meta Gap
summary: Compare your deck against recent EDH Top 16 lists for the same commander.
order: 30
requires_flag: tool.cedh-meta-gap.enabled
---

# cEDH Meta Gap

The cEDH Meta Gap page (`/cedh-meta-gap`) generates a structured AI workflow for comparing your deck against recent EDH Top 16 lists for the same commander.

## Step 1 — Load Deck And Fetch References

Paste a public Moxfield or Archidekt URL, or paste Moxfield, Archidekt, or MTG Arena deck export text directly. You can optionally override the commander name. The page then queries EDH Top 16 using:

- Time period
- Sort by (`TOP` or `NEW`)
- Minimum event size
- Maximum standing

The service parses the submitted deck, removes sideboard and maybeboard cards, resolves the commander, fetches matching EDH Top 16 entries, and sorts them newest-first before display.

## Step 2 — Generate Meta-Gap Prompt

Select 1 to 3 EDH Top 16 reference decks and generate the prompt. While building the prompt, the service:

- Resolves submitted-deck and reference-deck card names through Scryfall so alternate print names and reskins are converted to canonical Oracle names where possible.
- Normalizes split and multi-face names to the base/front name for prompt display.
- Queries Commander Spellbook for your deck and for each selected reference deck, then injects combo summaries into the prompt.
- Caps the reference-deck count at 3 to keep the prompt size reasonable once decklists and combo references are included.

Your AI is instructed to:

- Write a concise human-readable meta-gap summary first.
- Then return a fenced `json` block whose top-level object is `meta_gap`.
- Prefer the supplied Commander Spellbook combo evidence over weaker inferred combo reads when they conflict.
- Fill every field, using empty strings, zero values, `false`, or empty arrays when evidence is missing.
- Return the whole `meta_gap` object in one response, without splitting or refusing.

### If your AI splits the answer or says it's "too long"

On a full four-deck comparison, ChatGPT sometimes breaks the reply into parts, offers to "continue," or claims the response is too large to finish. A fully-populated `meta_gap` object is only a few kilobytes and comfortably fits one response — the prompt now tells the AI this directly, but if it still balks:

- Reply: **"Output only the complete `meta_gap` JSON in a single response — skip the prose, one short sentence per justification."** The same tip is shown on the page beside the generated prompt.
- If it still resists, add: **"That is incorrect — the JSON is only a few KB. Cap each list to its top 8 entries and return it now."**
- Or shrink the input: select fewer reference decks in Step 2. Fewer lists means a smaller answer.
- **Do not** paste a partial or split response into Step 3. The page needs one complete `meta_gap` object and cannot merge parts — a truncated block is rejected as invalid JSON. Get the whole block in ChatGPT first, then paste once.

Claude and Gemini rarely split this prompt, so switching the target AI in Step 2 is another option.

## Step 3 — Paste Returned JSON

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

## Artifact saving

Use **Download meta-gap session (.zip)** in the sticky bar at the top of the page (always available, regardless of step) or in the Step 3 results panel to save the current artifacts locally.

The zip can contain: `00-input-summary.txt`, `30-meta-gap-prompt.txt`, `31-meta-gap-schema.json`, and `40-meta-gap-response.json`.

Use **Resume from a saved session (.zip)** at the top of the page to upload the same zip later. Re-import only reads `40-meta-gap-response.json`; the other files remain in the archive for your records.
