---
quick_id: "260712-studio-harvest-job-survive-nav"
title: "Studio: keep a running Harvest job alive when switching pages"
status: complete
completed: 2026-07-12
---

# Summary: Harvest job survives page navigation

**Shipped** — commit `a110f04d` (`fix(studio): keep Harvest job running when switching pages`).

DeckFlow.Studio is classic Blazor Server, so navigating away from the Harvest page disposed the component and its `Dispose()` cancelled the in-flight `_cts`, killing long-running Harvest / Auto-distill / Live-Distill jobs. Fixed so those jobs are no longer bound to the component lifetime — they run to completion and persist to the content-kb DB even while the operator is on another page, and returning to Harvest reconnects to the running job's progress.

Retroactive SUMMARY added 2026-07-23 during the Cycle-18 planning sweep — work was verified shipped in git.
