# Foreman Ledger — Cut Lab Role-Floor Research milestone setup

**Run started:** 2026-07-26
**Mode:** Codex-boosted (Agent tool + real shell + consented Codex CLI 0.145.0, ChatGPT-account subscription login)
**LEAD:** Sonnet 5 (session default; user did not request a switch)
**Baseline commit:** `2da0bcc7` (local `main`, up to date with `origin/main`)
**Branch:** `research/cutlab-role-floors` (new worktree `../deckflow-role-floors`)
**Workstream:** `cutlab-role-floors` (`.planning/workstreams/cutlab-role-floors/`, isolated from the
in-flight Cycle 20 "Personal Tools" milestone on `feat/personal-tools`)
**Tracked tree at baseline:** clean

## User decisions (do not re-litigate)

1. Phase goal = research + build: (a) validate the per-commander role-floor empirical signal
   properly, produce a written findings doc + go/no-go, (b) if research supports it, design+build
   commander-aware role-floor defaults in Cut Lab.
2. New standalone milestone, isolated from Cycle 20 (own branch/worktree/workstream).
3. Codex routing: YES, use Codex per standing CLAUDE.md rule.
4. Codex models: keep session defaults — review/planning `gpt-5.5` medium, coding `gpt-5.4` medium
   (gpt-5.x-codex variants 400 on this ChatGPT-account login, do not dispatch them).

## Background (context for planning, not yet committed anywhere)

- Cut Lab's "Role floors" (interaction/protection/engines/payoffs/wincons) currently come from a
  pure bracket-wide `GetBracketBand` lookup — no commander awareness. Lands/ramp/draw already have
  a commander-aware priority chain (cedhBaseline -> bracket baseline -> fallback) — see
  `CutLabFloorDefaults.cs` and the three-layer `ManabaseBaseline` system
  (`EdhrecAveragesConverter` / `ManabaseBaselineProvider` / `CedhLandBaselineProvider`).
- An ad-hoc, uncommitted investigation the prior session (temp scratchpad, now gone) queried the
  Postgres corpus for 4 commanders (Sokka 229 decks, Edgar Markov 700, Krenko 734, Atraxa 596),
  reimplemented `DeckStatClassifier`/`PlanRoleClassifier` role logic in Python against Scryfall
  bulk data, and found apparent per-commander divergence from bracket floors (e.g. Edgar Markov
  ~5-7x higher payoffs than the other three). Never verified against the real C# classifiers,
  never reconciled with the existing baseline architecture, never written up.
- Prior shipped, closed phase: `.planning/milestones/ws-cut-lab-2026-07-23/phases/102-structural-analysis-role-floors`
  — read before planning so this doesn't duplicate already-decided ground.

## Tasks

| ID | Task | Seat | Status |
|----|------|------|--------|
| T1 | `/gsd-new-milestone` in the new workstream — questioning, requirements, roadmap for research-validation + (conditional) build phase | LEAD (Sonnet 5) | IN PROGRESS |
| T2 | Plan first phase (research validation) via `/gsd-plan-phase` | LEAD, Codex plan-review | PENDING |
| T3 | Report milestone + first phase plan back to user before any execution | LEAD | PENDING |
