# Phase 51 — OPS-01 Results (Render deploy-branch flip + Cycle 8 branch base)

**Recorded:** 2026-06-17
**Plan:** 51-04
**Requirement:** OPS-01

## OPS-01 — Render deploy branch flip

**Result: PASS** (verify-then-deploy gate satisfied — see note; flip was already in place this session)

| Fact | Value |
|------|-------|
| Render service | `DeckFlow` (`srv-d7gmufkp3tds73a29m30`) |
| Deploy branch | **`main`** (confirmed via Render API `list_services`) |
| autoDeploy | yes (trigger: `checksPass`) |
| Service updatedAt | 2026-06-17T16:37:35Z (branch/config reconfigured today) |
| Latest deploy | `dep-d8pc7o57vvec73fsuerg` — status **live**, finished 2026-06-17T15:57:59Z, trigger `new_commit` |
| Deployed commit | `d1f120d3` ("merge: bring main hotfixes into v1.7 before squash-merge") |
| `origin/main` HEAD | `39e74d55` (v1.7 squash, tag `2026.06.3` + `v1.7`) |
| Live site | https://www.deckflow.gg/ → **HTTP 200** (0.53s); https://deckflow.onrender.com/ → **HTTP 200** |

**SHA-vs-tree note (not a defect):** the currently-live deploy commit `d1f120d3` is the
pre-squash merge commit, not `origin/main` HEAD `39e74d55`. They are **tree-identical**
(`git diff --quiet d1f120d3 39e74d55` → no diff), so prod ships byte-for-byte the v1.7
code that is on `main`. The SHA label differs only because the squash-merge collapsed the
same diff into a new commit. The next push that advances `origin/main` (e.g. landing the
Cycle 8 commits) will trigger a fresh `checksPass` deploy and realign the deployed SHA
label with main HEAD. No code gap; OPS-01's intent (prod tracks the v1.7-inclusive `main`)
is met.

**Verify-then-deploy gate:** the Render branch was already pointed at `main` before this
session (operator-confirmed: "main is already deployed to render"), so no flip action was
taken here. The Phase 51 UAT smokes (51-01/51-02) are being recorded in the same session;
no NEW prod deploy was triggered by this phase.

## OPS-01 — Cycle 8 branch base

**Result: PASS**

| Check | Result |
|-------|--------|
| `git merge-base --is-ancestor 39e74d5 origin/main` | exit 0 — v1.7 squash **is** ancestor of `origin/main` |
| `git tag --points-at 39e74d5` | `2026.06.3`, `v1.7` — CalVer + legacy tag on the v1.7 squash |
| `origin/main` HEAD | `39e74d55` |
| local `main` HEAD | `525c7d3` (origin/main + 4 Cycle 8 doc/plan commits, unpushed, ff-pending) |
| Cycle 8 base | Cycle 8 planning commits branch off local `main` which descends from `origin/main` (v1.7-inclusive) — verified base |

No `render.yaml` edits, no git push, no branch creation performed by the AI (CLAUDE.md do-not-modify + no-push rules honored).
