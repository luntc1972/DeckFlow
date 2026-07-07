---
quick_id: 260707-clo
slug: fix-5-high-manabase-efficacy-findings-r2
status: complete
completed: 2026-07-07
branch: fix/manabase-efficacy-r2
review: "Codex gpt-5.5 medium, 4 rounds: BLOCK -> BLOCK -> BLOCK -> APPROVE-WITH-NITS"
---

# Summary: Fix 5 HIGH manabase efficacy findings (R2)

All five HIGH findings from `.planning/captures/manabase-efficacy-findings-r2.md` fixed on
`fix/manabase-efficacy-r2` (worktree `../deckflow-manabase-r2`), one atomic commit per fix plus
three review-driven refinements.

## Commits

| Commit | Fix |
|--------|-----|
| `6fcf6209` | H1 — `EntersTapped` matches post-Aug-2024 "enters tapped" wording (taplands were ALL classified untapped on live Scryfall data) |
| `6cf916ad` | H5 — `SixtyCardLandTarget` uses Karsten's real 60-card coefficients (19.59 + 1.90·MV, was 100-card-scaled 32.65 + 3.16·MV) |
| `b3b68d32` | H3 — swap prompt gains the three-way land note (`LandShortfallCoveredByRamp`), no longer asks the LLM for lands the page said are unnecessary |
| `8c88dfda` | H2 — `IsRockOrDork` requires a repeatable front-face `<cost>: Add` ability (reminder-stripped, sacrifice-excluded); Dockside/Lotus Petal/altars no longer permanent WUBRG sources |
| `49ea573c` | H4 — verdict consumes the health band's `ColorIssueFindings` + the page's `< -1` land threshold; "no changes needed" beside a Workable chip is impossible |
| `2a054efe` | H2 review r1 — quoted granted abilities (Paradise Mantle) are not the granter's own mana |
| `fb477c3b` | H2 review r2 — self pronoun grants (Honored Hierarch, Mul Daya Channelers) kept |
| `057b3e05` | H2 review r3 — type-aware self-inclusion for collective grants (Gemhide Sliver, Katilda, Citanul Hierophants) |

## Validation

- Core 1117/1117, Web 1225/1225 (+12 skipped) — full suites green via Windows dotnet.exe.
- Solution build 0 warnings / 0 errors; changed-lines format gate passed.
- Golden re-baselines (documented in `ManabaseHealthBandRegressionTests`): Meren Excellent→Solid,
  army-now Solid→Needs work — phantom optimism (untapped taplands, phantom Treasure sources) removed.
- Codex (gpt-5.5 medium) 4-round review → APPROVE-WITH-NITS (residual nit: hypothetical
  "Other creatures you control have" self-inclusion; no real card affected).

## Deliberate scope notes

- STATE.md untouched (no quick-task table in current format; avoids conflict with the active
  cycle-16 branch's STATE churn). This SUMMARY is the completion record.
- README untouched: internal accuracy fixes, no user-facing workflow change. The swap-prompt
  wording change is inside a generated artifact.
- MED/LOW findings from the R2 capture remain open — see the capture doc's recommended order
  (next: M1 mulligan bottoming, M2 tapped-fixer sequencing, M3 ramp-castability gate wiring,
  plus the live-oracle canary guard).
