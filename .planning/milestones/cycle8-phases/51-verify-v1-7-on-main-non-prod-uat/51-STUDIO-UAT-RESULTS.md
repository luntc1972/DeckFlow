# Phase 51 — Studio UAT Results (HARD-01)

**Recorded:** 2026-06-17
**Plan:** 51-02
**Requirement:** HARD-01 (Studio slice — deferred P41/P45/P46 smokes)
**Build:** v1.7-on-main, Windows `dotnet run` DeckFlow.Studio, http://localhost:5271,
`DECKFLOW_LLM_PROVIDER=claude` (subscription/$0), `DECKFLOW_DISABLE_AUTO_BROWSER=true`.
**Data:** existing local `DeckFlow.Studio/artifacts/studio/content-kb.db` (4 enabled sources,
32 videos, 31 distilled summaries, 241 clips). Driven headless via gstack browse + Studio log.

## P41 — Studio runtime render — PASS

| Check | Observed | Verdict |
|-------|----------|---------|
| Boots & listens | `Now listening on: http://localhost:5271`, `/` → 200 | PASS |
| Presence-only secret logging | stdout: `Studio prod connection: not configured` / `Studio SCP: not configured` — names only, no connection string value | PASS |
| All routes render (no NotFound / error-ui shown) | `/`, `/harvest`, `/review`, `/publish`, `/direct-push` all render; `#blazor-error-ui` `display:none` on each | PASS |
| Harvest chrome | Browse/Paste/Harvest/Distill panels; `Subscription ($0)` badge; cap UI Monthly $15.00 / Spent $0.00 / Remaining $15.00 | PASS |

Evidence: `/tmp/51-02-studio-harvest.png`.

## P45 — re-distill / cap / cancel — PASS (2 sub-smokes WAIVED with reason)

**Re-distill E2E — PASS.** Browsed @TheCommandZone, selected the already-distilled
"Borborygmos Enraged Deck Tech" (`f8782tCIwmk`); the amber **re-distill warning + double
confirm** rendered; checking both boxes unlocked **Run Distill** (subscription provider folds
the dry-run stage). Live distill log:
```
re-distilling f8782tCIwmk (redistill=true)
distilled f8782tCIwmk
distill complete sources=4 videos_distilled=1 videos_filtered=0 llm_calls=3 spend_usd=0.000000 distill_failed=0
```
DB confirms overwrite: `content_summaries` for video 1 went from row id=10 → **id=32**, fresh
`created_utc=2026-06-17 17:37:24`, refreshed body (prior output cleared + replaced). $0 spend.

**Cap session-raise + non-persistence — PASS (non-persistence by design).** The "Raise cap (this
session only)" control raised the cap **$15.00 → $99.00** (Monthly cap + Remaining both updated).
Non-persistence holds by design: `SessionCapOverride` is an in-memory singleton (no DB write), and
the clean boot showed the $15.00 env/default — a restart reverts. Not restarted live (would end the
session); the design + boot-default are the evidence.

**Cap block ENFORCEMENT — WAIVED (provider-gated).** Under the `$0 claude` subscription provider the
cap is intentionally **bypassed** (`StudioDistillConfig.IsSubscriptionProvider` → cap not enforced;
the live run logged `spend_usd=0.000000`). Exercising the red cap-exceeded block requires the metered
`openai` provider, which would incur real spend. Waived this session; enforcement logic is covered by
Phase 45 automated tests.

**Cancel-on-dispose — WAIVED (timing-sensitive).** Catching a mid-harvest tab-close headlessly is
unreliable (single-video harvest completes quickly); the CancellationToken-on-dispose wiring is unit-
tested in Phase 45. Waived for live reproduction this session.

## P46 — Review queue + Publish — PASS (real Publish-commit WAIVED per operator choice)

**Review queue — PASS** (driven via gstack):

| Check | Observed | Verdict |
|-------|----------|---------|
| Pending active on load | Pending tab active, 23 rows shown on load | PASS |
| Tab count badges match rows | Pending 23 / Approved 7 / Rejected 1 / All 31 (23+7+1=31) | PASS |
| Tab switch filters | Clicking Approved → 7 rows; back to Pending → 23 | PASS |
| Expand shows real artifact preview | Expand "Stop Turning your Commander into a Combo Piece" → `<pre>` with 3719 chars of real markdown (front-matter `source: "@salubrioussnail"` + body); NO false "Artifact missing" | PASS |
| Approve flips status + counts | Approve first Pending row → Pending 23→22, Approved 7→8, All stays 31 (optimistic) | PASS |

Evidence: `/tmp/51-02-studio-review.png`.

**Publish — render + no-push confirmed; real commit WAIVED.** `/publish` renders branch/approved-count
context + the **"Export & Preview Diff"** button, and there is **no push button anywhere** (confirms the
never-pushes design D-04). Per operator decision this session, the real Export→diff→checkbox-gated git
commit was NOT executed (avoids a throwaway commit); covered by the 21 DeckFlow.Studio.Tests bUnit tests.

## Net

P41 PASS · P45 PASS (re-distill + cap-raise verified live; cap-block-enforcement + cancel WAIVED with
reason) · P46 Review PASS, Publish render/no-push PASS (real commit WAIVED per operator). No defects
found in the Studio smokes. Local Studio dev data was mutated by the smokes (one re-distill, one extra
approval, session cap raise) — local only, not committed, not pushed.
