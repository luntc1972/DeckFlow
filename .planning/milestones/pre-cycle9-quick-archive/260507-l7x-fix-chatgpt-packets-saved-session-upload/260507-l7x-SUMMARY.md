---
quick_id: 260507-l7x
slug: fix-chatgpt-packets-saved-session-upload
status: complete
date: 2026-05-07
implementation: codex
plan: 260507-l7x-PLAN.md
---

# Quick Task 260507-l7x Summary

## What shipped

Fixed the saved-session upload regression on the ChatGPT workflow pages where the upload action could be blocked before the POST by validation on unrelated required fields. The production symptom was the browser error:

`An invalid form control with name='TargetCommanderBracket' is not focusable.`

This happened on `/chatgpt-packets` because the resume-upload action lived inside the main workflow form, while the required bracket select belongs to Step 2 and can be hidden when the user is just trying to re-import a saved zip.

## Changes

- Added `formnovalidate` to the resume-upload submit buttons on:
  - `DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml`
  - `DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml`
  - `DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml`
- Added a shared `data-chatgpt-upload-submit` marker to those upload buttons.
- Updated `DeckFlow.Web/wwwroot/ts/deck-sync.ts` so each ChatGPT workflow submit handler exits early for the resume-upload submitter instead of running normal step validation.

## Why this is correct

- Native browser validation must be bypassed for the upload action because the upload endpoint only needs `zipFile`, not the rest of the workflow form state.
- Client-side workflow validation must also be bypassed because the initial blank-page state does not satisfy step requirements such as deck input or bracket selection, but those are irrelevant to re-importing a saved session.
- Normal packet-generation and render-summary submits still follow the existing validation paths.

## Verification

- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj`
  - Result: success
  - Warnings: 0
  - Errors: 0

## Commit

- `29e2733` — `fix(chatgpt): bypass resume upload validation`
