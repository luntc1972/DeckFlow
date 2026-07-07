---
quick_id: 260707-clo
slug: fix-5-high-manabase-efficacy-findings-r2
type: quick
branch: fix/manabase-efficacy-r2
worktree: ../deckflow-manabase-r2
source: .planning/captures/manabase-efficacy-findings-r2.md
autonomous: true
must_haves:
  truths:
    - "Taplands with the live Scryfall wording 'enters tapped' classify as ETB-tapped (H1)."
    - "Treasure-makers / sac one-shots (Dockside, Lotus Petal, altars) no longer count as permanent color sources; genuine '{T}: Add' rocks/dorks still do (H2)."
    - "Swap prompt land line has the same three-way branch (on target / ramp-covered / add N) as page, .txt, and PrimaryFix (H3)."
    - "Plain-language verdict issue detection shares the health band's ColorSignals + thresholds — no 'no changes needed' beside a Workable/Needs-work chip (H4)."
    - "SixtyCardLandTarget uses Karsten's 60-card coefficients 19.59 + 1.90*MV (H5)."
  artifacts:
    - "xUnit regression tests per fix in DeckFlow.Core.Tests/Manabase"
---

# Quick Task 260707-clo: Fix 5 HIGH manabase efficacy findings (R2)

Findings source: `.planning/captures/manabase-efficacy-findings-r2.md` (all 5 verified against
source + live Scryfall + prod flags before this plan).

## Tasks (atomic commit each)

1. **H1 — tapland wording** (`ManabaseClassifier.EntersTapped`): also match "enters tapped";
   keep old string. Tests: new-wording land classifies tapped; old wording still works.
2. **H5 — 60-card coefficients** (`KarstenManabase.SixtyCardLandTarget`): 32.65→19.59,
   3.16→1.90. Test: avg MV 2.5 / no credits ≈ 24.3 (not 40.55).
3. **H3 — swap prompt land branch** (`ManabaseSwapPromptBuilder`): add
   `LandShortfallCoveredByRamp` three-way branch mirroring ReportTextBuilder. Test: ramp-covered
   deck prompt does NOT say "add ~N more land".
4. **H2 — one-shot/sac producers** (`ManabaseClassifier.IsRockOrDork`/`ProducesMana`): gate on
   reminder-stripped front-face repeatable "{T}: Add" mana ability; exclude sac-cost-only and
   trigger-only producers. Tests: Dockside/Lotus Petal/Ashnod's Altar excluded; Sol Ring/
   Birds of Paradise/Signet still count.
5. **H4 — verdict/health threshold share** (`ManabaseVerdictSynthesizer.CollectIssues`): derive
   from the same ColorSignals/thresholds as `Health` (land < -1 uncovered, color-starved count,
   sim-weakest). Test: Workable-band report never yields empty issue list.

Then: full Core+Web test suite via Windows dotnet.exe; Codex review (gpt-5.5 medium); SUMMARY.md
+ STATE.md quick-task table update.
