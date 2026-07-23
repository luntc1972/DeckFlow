---
slug: category-junk-rank
status: complete
date: 2026-07-15
completed: 2026-07-15
---

# Summary: Harden category junk filter + fix Suggest-Categories ranking

**Shipped** — commits `6fbf1ab6` (harder junk filter + source-authority ranking) and follow-up `93f1ad8d` (weighted table on Suggest Categories).

- `CategoryFilter.IsJunk` extended (display-time only, raw DB untouched): rejects any embedded ASCII digit, word count ≥5, and sentence punctuation (`,`/`;`/trailing `.`); existing rejections kept; hyphen/apostrophe/`&` still allowed.
- `CategorySuggestionReporter.Merge` adds a source-authority tie-break (Tagger/Exact=3, cached=2, EDHREC=1) so the merged Suggest list is ranked by agreement + authority instead of looking alphabetical.

Gates green at close (Core + Web category subset). Retroactive SUMMARY added 2026-07-23 during the Cycle-18 planning sweep — work was verified shipped in git.
