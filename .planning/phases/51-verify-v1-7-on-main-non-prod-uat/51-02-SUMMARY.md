# 51-02 Summary — Studio smokes P41/P45/P46 (HARD-01)

**Status:** PASS (2 P45 sub-smokes WAIVED with reason; P46 real-commit WAIVED per operator) · **Date:** 2026-06-17

Ran the deferred Studio smokes on the v1.7-on-main build (Windows `dotnet run` DeckFlow.Studio, :5271,
`DECKFLOW_LLM_PROVIDER=claude`), driven headless via gstack against existing local data (4 sources,
31 distilled summaries).

- **P41 render — PASS:** all 5 routes render, presence-only secret logging (`prod connection: not configured`, no value leaked).
- **P45 re-distill — PASS:** live re-distill of `f8782tCIwmk` logged `redistill=true` → `videos_distilled=1 spend_usd=0.000000`; DB summary overwritten (row id 10→32, fresh timestamp). Cap session-raise PASS ($15→$99; non-persistence by-design in-memory override). Cap-block enforcement WAIVED (bypassed under $0 claude provider — needs metered openai). Cancel-on-dispose WAIVED (timing-sensitive; unit-tested).
- **P46 Review — PASS:** tabs/counts consistent (23/7/1/31), Pending active on load, tab-switch filters, expand shows real 3719-char artifact `<pre>` (no false "Artifact missing"), Approve flips status + counts (Pending 23→22, Approved 7→8). Publish renders Export & Preview Diff with no push button (never-push design confirmed); real commit WAIVED per operator choice.

No Studio product files modified. No defects. Local dev data mutated by smokes (not committed/pushed).
Full evidence: `51-STUDIO-UAT-RESULTS.md`.
