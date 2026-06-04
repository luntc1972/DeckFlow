---
type: bug
created: 2026-05-24
source: phase-25 execution (Wave 1 review)
priority: medium
---

# 13 AdminCssPhase1Tests fail — stale Phase-1 CSS markers

`DeckFlow.Web.Tests/AdminCssPhase1Tests.cs` `ReadPhase1Section()` cannot find the
`Phase 1` CSS start/end markers — `Assert.Contains() Failure: Sub-string not found`.
All 13 facts fail as a result. Pre-existing Phase 18 debt (likely the D-OPEN
reversal removed/renamed the marker block the tests pin). Unrelated to Phase 25
(data/UI), which touches no CSS.

Fix: realign the tests with the shipped CSS, or restore the marker block if the
removal was unintended. Candidate for the Phase 24 bug-fix block.


---
RESOLVED 2026-05-27 (commit 7cee9b0): test-only fix — repointed AdminCssPhase1Tests at admin-common.css (Phase 18 moved the block) + updated danger-color assertion for the --danger token. Full web suite 486/0.
