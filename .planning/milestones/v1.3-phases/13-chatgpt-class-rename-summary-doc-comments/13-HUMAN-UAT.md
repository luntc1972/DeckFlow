# Phase 13: ChatGpt* Class Rename — Human UAT Checklist

**Per CLAUDE.md "VSTest unreliable in WSL":** automated `dotnet test` is NOT part of the verifier gate. Build-clean is the gate; behavioral parity is verified by the manual T1-T8 round-trip below (per D-09 SC4 + D-10).

**Per CLAUDE.md memory:** the user starts the dev server. Do NOT auto-launch DeckFlow web — ask the user to start it manually.

## Setup
- [ ] User starts `dotnet run --project DeckFlow.Web` locally (or `scripts/run-web.ps1` / `scripts/run-web.sh`)
- [ ] User confirms dev server is listening on http://localhost:5173

## T1: Deck-Analysis page renders + form submits
Source: `.planning/milestones/v1.2-MILESTONE-AUDIT.md` T1
- [ ] Visit http://localhost:5173/deck-analysis — page renders with AI-agnostic H1 + page-lede (Phase 12 layout intact)
- [ ] Paste a small valid deck export (10-30 cards + a commander) → click "Generate analysis prompt" → page advances to Step 2 with no errors
- [ ] Step 5 follow-up form submits, page returns to Step-5 view with no errors

## T2: Deck-Analysis zip download produces valid artifact
- [ ] On the deck-analysis page after Step 5, click "Download session zip"
- [ ] Open the downloaded `.zip` (e.g., `deck-analysis-<commander>-<timestamp>-chatgpt.zip`)
- [ ] Confirm `01-request-context.txt`, `40-deck-analysis-response.json` (or equivalent), prompt files, and `00-readme.md` (if present) are inside
- [ ] Confirm `target_ai_platform: ChatGPT` line is present in `01-request-context.txt` (D-07 #1 preservation)

## T3: Deck-Analysis zip upload round-trips byte-identical
- [ ] On the deck-analysis page, click "Upload session zip" and select the file from T2
- [ ] Page restores to Step 5 state with all form fields and prompt artifacts re-hydrated
- [ ] Re-download the zip and `diff` (or compare sizes / inner JSON byte-identical with the T2 download)

## T4: Deck-Comparison page renders + form submits + zip round-trip
- [ ] Repeat T1-T3 against http://localhost:5173/deck-comparison
- [ ] Confirm zip filename is `deck-comparison-*.zip` (Phase 12 filename)
- [ ] Confirm `40-deck-comparison-response.json` (or equivalent) inside zip

## T5: cEDH Meta-Gap page renders + form submits + zip round-trip
- [ ] Repeat T1-T3 against http://localhost:5173/cedh-meta-gap
- [ ] Confirm zip filename uses the cedh-meta-gap term (Phase 12)
- [ ] Confirm `40-meta-gap-response.json` (or equivalent) inside zip

## T6: AI selector dispatches across ChatGPT/Claude/Gemini
- [ ] On any of the 3 pages, change the AI radio to "Claude" → re-generate prompt → confirm output mentions Claude-specific instructions
- [ ] Repeat with "Gemini" if `DECKFLOW_GEMINI_ENABLED` is set
- [ ] Confirm `_AiSelector.cshtml` still renders `id="ai-chatgpt"`, `value="ChatGPT"` (Phase 13 preservation — Pitfall 4)

## T7: Permanent 301 redirects from legacy ChatGPT URLs (Phase 12 invariant)
- [ ] Visit http://localhost:5173/chatgpt-packets → confirm 301 → /deck-analysis
- [ ] Visit http://localhost:5173/chatgpt-deck-comparison → confirm 301 → /deck-comparison
- [ ] Visit http://localhost:5173/chatgpt-cedh-meta-gap → confirm 301 → /cedh-meta-gap

## T8: Pre-Phase-13 saved zips load successfully
- [ ] If the user has any zips saved BEFORE Phase 13 work (with the old `target_ai_platform` field name conventions but Phase-12 filenames), upload one of each (deck-analysis / deck-comparison / cedh-meta-gap)
- [ ] Confirm all three load and restore state without errors (PacketArtifactStore deserialization unchanged — D-07 #4 preserved the AI-segment fallback)

## Sign-off
- [ ] All T1-T8 pass — zero user-visible behavior change verified
- [ ] User types "approved" or pastes any specific failure description for the planner to triage as a gap

**Resume signal:** When user types "approved", proceed to plan 13-04 SUMMARY emission. If user reports any failure, surface it as a gap closure candidate (`/gsd:plan-phase 13 --gaps`).
