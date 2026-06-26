---
phase: 66-admin-tool-visibility-toggles-tool-registry
plan: 04
wave: 2
status: completed
requirements: [TOGGLE-03]
---

# 66-04 Summary

Implemented registry-aligned help visibility via `requires_flag` front-matter headers.

## Changes

- Added `requires_flag` headers to the 10 tool help topics covered by the plan:
  - `deck-analysis.md` → `tool.deck-analysis.enabled`
  - `deck-comparison.md` → `tool.deck-comparison.enabled`
  - `cedh-meta-gap.md` → `tool.cedh-meta-gap.enabled`
  - `deck-sync.md` → `tool.deck-sync.enabled`
  - `deck-primer.md` → `tool.deck-primer.enabled`
  - `card-lookup.md` → `tool.card-lookup.enabled`
  - `ask-a-judge.md` → `tool.judge-questions.enabled`
  - `commander-categories.md` → `tool.commander-categories.enabled`
  - `content-kb.md` → `content.kb.enabled`
  - `category-suggestions.md` → `feature.categories.enabled`
- Left `manabase.md` unchanged because it already had the correct header.
- Left `convert` and `mechanic-lookup` unchanged because they have no help topic.
- Added `HelpFlagHeaderConsistencyTests` so every registry-gated tool with a help topic must declare a matching `requires_flag` header.

## Verification

- Help-header grep check passed for all 10 files.
- Targeted help-flag consistency test added for registry↔help drift detection.
