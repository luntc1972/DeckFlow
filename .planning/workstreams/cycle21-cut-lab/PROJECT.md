# Project: DeckFlow — Cycle 21 Discriminating Cut Proposals (workstream)

**Full project history:** `.planning/PROJECT.md` on `main`/`feat/personal-tools` — this workstream is
scoped to one milestone and does not duplicate that file. Read the root `PROJECT.md` for prior
milestone context if needed.

## What This Is

DeckFlow is a Magic: The Gathering deck analysis tool for cEDH/Commander players
(deckflow.gg). Core value: every supported workflow produces output the user can paste into
ChatGPT/Claude/Gemini and get a useful answer in one round-trip.

## Current Milestone: Cycle 21 — Discriminating Cut Proposals

**Goal:** Make Cut Lab's cut proposals better in both directions — more accurate guardrails
(commander-aware role floors) and, for the first time, a finding that actually discriminates
*within* a role so the queue ranks cards rather than merely measuring the deck.

**Framing that drove the re-plan.** A review of Cut Lab against community cutting methodology
found the tool strongest on measurement and process discipline (Monte Carlo simulation,
deterministic round ordering, lock/defer psychology, commander-keyed land baselines) and weakest
on card-vs-card comparison — the question players actually verbalize at 108 cards. Crucially,
both floor-derived findings (`WeakFloorCase`, `RedundantFinishers`) sit in
`CutLabCutRoundEngine.ExcludedFindingKindsFromTally`, so **floor accuracy cannot change which
card is proposed next**. That exclusion is correct — a role-count finding attaches uniformly to
every member of the role — but it means the original 2-phase milestone would have shipped better
guardrails and an unchanged ranking. Phase 4 (functional twins) was added to close that.

**Target features:**
- Phase 1: split the merged `interaction` role into targeted and mass, from classifier calls that
  are already separate. Aligns to the 2025 Command Zone template and de-flattens the signal
  Phase 2 measures.
- Phase 2: repair and actually run the role-floor divergence harness against the real corpus.
- Phase 3 (conditional): commander-aware floor defaults with bracket and commander floors shown
  side by side.
- Phase 4 (independent): functional-twins detector — the cycle's only ranking change.
- Phase 5 (independent, non-gating): capture Archidekt bracket during the category harvest.

**Key context:**
- Isolated from the concurrently in-flight Cycle 20 "Personal Tools" milestone
  (`feat/personal-tools`) — different branch (`gsd/cycle21-cut-lab`), different worktree.
- Prior shipped, closed work: Phase 102 "Structural Analysis & Role Floors"
  (`.planning/milestones/ws-cut-lab-2026-07-23/phases/102-structural-analysis-role-floors`)
  shipped the *bracket+plan*-derived role floors this milestone extends. Additive only.
- Corpus available for research: Postgres `harvest_runs`/`deck_queue`/`card_deck_totals` via
  `CategoryKnowledgeRepository`.

## Out of Scope (this milestone)

- Commander × bracket joint floor derivation — bracket capture lands (Phase 5) but backfill
  latency keeps the commander floor bracket-agnostic this cycle.
- Any change to bracket+plan fallback behavior when no commander data exists.
- Land/ramp/draw floor logic — already commander-aware.
- Bracket legality / Game Changers compliance panel — real gap, deliberately deferred.
- Public-facing changes outside Cut Lab.

## Decisions Log

| Decision | Reasoning | Outcome |
|----------|-----------|---------|
| New standalone milestone/branch/worktree, not folded into Cycle 20 | Unrelated surface area (Cut Lab vs. creator-style port); avoids blocking either on the other | Kept |
| Validate against real C# classifiers before any implementation | A prior Python reimplementation may have drifted from `DeckStatClassifier`/`PlanRoleClassifier` | Kept — and reinforced, see below |
| Re-planned 2 phases → 5 | Original scope improved guardrails only; both floor-derived findings are excluded from the round tally, so ranking would not have changed. Added the interaction split (unblocks the research), functional twins (the only ranking change), and bracket capture (user requirement) | Adopted 2026-07-26 |
| Interaction split sequenced BEFORE the research run | `interaction` is one of the five roles the go/no-go hinges on; measuring a role about to be redefined wastes the run. Merging targeted removal with board wipes also inflates within-role variance, which suppresses the z and Cohen's d the bar reads | Adopted 2026-07-26 |
| Functional twins deliberately corpus-free | Ships regardless of whether the research returns go or no-go, so the cycle delivers a ranking improvement even in the no-go branch | Adopted 2026-07-26 |
| Bracket capture is non-gating; commander floor stays bracket-agnostic this cycle | Bracket cannot be derived retroactively — coverage only builds as new decks are crawled. Gating on backfill stalls the cycle on latency outside our control (user decision) | Adopted 2026-07-26 |
| RFLR-08 resolved to side-by-side display | REQUIREMENTS.md and ROADMAP.md carried contradictory wording (both numbers vs. single value + source tag). User confirmed: show bracket floor and commander floor, commander floor regardless of bracket | Resolved 2026-07-26 |
| Phase 2 findings artifacts to be deleted and regenerated, not amended | The committed `RESEARCH-FINDINGS.md`/`.json` are fixture output, not a run — see below | Adopted 2026-07-26 |

## Incident: synthetic research findings (2026-07-26)

Before the re-plan, `RESEARCH-FINDINGS.md` presented a complete go/no-go and `STATE.md` reported
Phase 1 as "planned and converged, not yet executed." Neither was accurate.

The findings were **fixture output**, not a corpus run:
- Commanders named Alpha/Beta/Gamma/Delta; per-commander statistics identical within each role.
- `ClearsBar` contradicted its own inputs — identical ratio, z, and Cohen's d yielding different
  verdicts, with DEDUPED N=41 against a stated threshold of 40.
- The exact constants appear hardcoded in `RoleFloorResearchCommandRunner.cs:884-888`
  (`BuildSyntheticRoleStat(18.0, 16.0, 1.5, 6.4, 2.0, …)`).
- `role-floor-research-run.log` and `.exit` were both 0 bytes.

Neither artifact was ever committed (both untracked), so nothing false entered git history.

**Structural remedy, not a process note.** RFLR-09 requires deleting the synthetic writer outright
rather than leaving it uncalled, requires run provenance stamped into every artifact (DB host,
counts, timestamp, harness SHA), and requires a non-zero exit when zero commanders qualify. The
principle: a harness that *can* emit a plausibly-shaped artifact without touching its data source
eventually will, and a well-formatted deliverable reads as evidence of a run. Make the fake
impossible rather than relying on review to catch it.

This is the second false-green in recent cycles — Cycle 20 waves 2-3 produced invented prompts from
missing manifest schemas. Same shape, same remedy.

## Note before starting Cycle 20's EDHREC track (C20-03)

Cycle 21 derives per-commander role counts from the **Postgres Archidekt harvest** via
`CategoryKnowledgeRepository`, classified by DeckFlow's own classifiers. C20-03 proposes deriving
per-commander card statistics from **EDHREC's `edhrec.csv` / `averages.csv`**. Both can answer
"what does this commander's deck normally look like," and they will disagree — different
populations, different categorization.

Decide the ownership boundary before C20-03 commits a provider interface. The clean split:
**Archidekt corpus owns role counts; EDHREC owns card-level inclusion.** Under that split the two
compose instead of competing, and Cycle 21 Phase 4 stays independent of both.

---
*Created: 2026-07-26*
*Re-planned: 2026-07-26*
