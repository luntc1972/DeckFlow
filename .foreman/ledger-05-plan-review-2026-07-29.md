# Foreman Ledger — Cycle 21 Phase 5 plan review (2026-07-29)

**Run:** plan review only. No implementation code, no source edits.
**Worktree:** `/mnt/c/users/chrislunt/source/personal/deckflow-role-floors`
**Branch:** `gsd/cycle21-cut-lab`
**Baseline commit:** `2f9ab5ef` (docs(05): create archidekt bracket capture plans)
**Baseline dirt at start:** `.planning/workstreams/cycle21-cut-lab/config.json` modified (cross_ai model -> gpt-5.6-luna);
untracked `.foreman/*`, `.planning/.../04-functional-twins-detector/`, `_edhrec-brackets/`, `_role-floor-research/`.
**Mode:** Codex-boosted. LEAD = Opus 5 (FRONTIER). Codex consented by user this session; seat `gpt-5.6-luna` medium.

## Under review

`.planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-01-PLAN.md` (wave 1, BRKT-01)
`.planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-02-PLAN.md` (wave 2, BRKT-02/03)
`.planning/workstreams/cycle21-cut-lab/phases/05-archidekt-bracket-capture/05-03-PLAN.md` (wave 3, BRKT-01/02/03)

## LEAD pre-review findings (recorded BEFORE reviewers reported, so their evidence can be graded)

| ID | Severity guess | Claim |
|----|----------------|-------|
| L-1 | MED | 05-01 T2 wants the `ImportWithMetadataAsync` compatibility default to THROW; 05-03 T3 relies on "the default interface method should keep [fakes] source-compatible". Opposed assumptions. |
| L-2 | HIGH (factual) | 05-03 asserts `RunAsync` still calls `MarkDeckProcessedAsync` after an `Unchanged` return from `PersistDeckAsync`. If it early-`continue`s, metadata is never captured for unchanged decks and T2 is unsatisfiable. Verify in code. |
| L-3 | MED | 05-02 T2 says null metadata "leave all columns unchanged" for processed updates; T1 Test 4 only asserts a FRESH row stays null. A skip that WIPES prior capture would pass. Looser criterion masking the stricter truth. |
| L-4 | MED/HIGH | 05-03 changes admin commander extraction `Category=="Commander"` -> `Board=="commander"`. Maps to no BRKT id; mutates `commander_name`, the column the role-floor corpus groups on. Needs explicit ratification + statement about existing rows. |
| L-5 | HIGH | 05-02 T3 `<done>` permits recording Postgres coverage as SKIPPED. Postgres is production (Render). Precedent: F-51-PG-01 was PG-only and SQLite-green. |
| L-6 | MED (factual) | 05-02 DDL uses `TEXT NULL` for timestamps in BOTH dialects and forbids dialect branches. Repo history has `::timestamptz` dialect guards. Verify how `last_checked_utc` is actually typed/compared per dialect. |
| L-7 | LOW (factual) | 05-02 threat model says "parameterized Dapper commands". Verify DeckQueueRepository actually uses Dapper vs raw Microsoft.Data.Sqlite/Npgsql commands. |
| L-8 | LOW | `<automated>` steps invoke bare `dotnet.exe`. Known env fact: dotnet is `/mnt/c/Program Files/dotnet/dotnet.exe`. Verify resolvable on PATH for the executor. |
| L-9 | MED | Interface gains a member + `ImportAsync` delegates. Enumerate ALL `IArchidektDeckImporter` implementers/fakes; 05-01/05-03 write sets may omit some, breaking compile or throwing at runtime. |

## Tasks

| ID | Task | Seat | Status |
|----|------|------|--------|
| R1 | gsd-plan-checker over Phase 5 (goal-backward, text-vs-goal) | Claude gsd-plan-checker | DISPATCHED |
| R2 | Codex authoritative plan review (claim-vs-code, read-only) | Codex gpt-5.6-luna medium | DISPATCHED |
| R2b | Claim-vs-code review (Codex substitute, user-authorized, REDUCED assurance) | Claude general-purpose, fresh ctx | **DONE** — CHANGES REQUIRED, 3 BLOCK / 3 HIGH |
| R3 | Fold findings into plans | LEAD (planning is Claude's lane) | BLOCKED on 2 user decisions |
| R4 | Codex re-review until CONVERGED | Codex gpt-5.6-luna medium | **OWED** — cannot run, no credits |
| R5 | Write REVIEWS.md | LEAD | **DONE** — `05-REVIEWS.md` |

## Results

- **R1 DONE** — `gsd-plan-checker`: **FAIL**, 3 BLOCK + 3 HIGH + 7 MED + 3 LOW.
- **R2b DONE** — fresh-context Claude claim-vs-code: **CHANGES REQUIRED**, 3 BLOCK + 3 HIGH + 6 MED + 4 LOW.
- Deduped total in `05-REVIEWS.md`: **3 BLOCK, 4 HIGH, 8 MEDIUM, 5 LOW**.
- LEAD pre-review scorecard: L-1 CONFIRMED+escalated (B1) · **L-2 REFUTED — plans were RIGHT about
  `RunAsync`; `ArchidektDeckCacheSession.cs:110-121` has no `continue`/early return, falls through to `:125`** ·
  L-3 CONFIRMED+escalated to BLOCK (R-B4) · L-4 CONFIRMED+escalated (R-M5) · L-5 CONFIRMED (R-H1) ·
  L-6 CONFIRMED (R-M4) · **L-7 REFUTED — Dapper IS the idiom, `DeckQueueRepository.cs:2`** ·
  **L-8 REFUTED — `dotnet.exe` resolves, all 8 test classes exist** · L-9 CONFIRMED+quantified (12 of 14 unlisted).
- Reviewer-only findings LEAD missed: R-H3 (must_haves proxies already true — 5th of this class in the
  cycle), R-H4 (wave 1 never compiles Web.Tests), R-M1 (parse throws reach USER-FACING import paths,
  `FormatException` escapes the harvest catch filter), R-M2 (`CapturedUtc` on any parsed JSON = permanent
  false "captured, absent"), R-M6 (cited legacy-migration test patterns DO NOT EXIST), R-M8, R-L2, R-L3.
- 3 cross-reviewer conflicts arbitrated by LEAD (recorded in `05-REVIEWS.md`): `CapturedUtc` semantics
  (R-M1 vs R-M2), `COALESCE` on timestamps (R-M3 vs R-H2), interface option A vs B (R-B1).

## Attempts (append-only, continued)

## Attempts (append-only)

- 2026-07-29 ~16:55 — R1 dispatched (gsd-plan-checker, background). RUNNING.
- 2026-07-29 ~16:55 — R2 dispatched (codex exec, read-only sandbox, background).
- 2026-07-29 ~16:57 — R2 **FAILED**: `ERROR: Your workspace is out of credits. Ask your workspace owner to refill in order to continue.`
  Zero bytes of review produced (`.foreman/scratch/codex-05-plan-review-r1.md` holds only the prompt echo + the error).
- 2026-07-29 ~16:58 — R2 **retry 1 FAILED**, verbatim same error on a low-effort 5-token probe. Deterministic (billing), not transient.
  Per CLAUDE.md "Cross-AI dispatch failures": surfaced to user; NOT silently falling back to Claude. Awaiting authorization.
  Attempt count for the Codex seat on this input: 2. Do not attempt a third on unchanged input.
- 2026-07-29 ~17:00 — R2b dispatched (fresh-context Claude, claim-vs-code, user-authorized Codex substitute).
- 2026-07-29 ~17:02 — R1 returned **FAIL** (3 BLOCK / 3 HIGH / 7 MED / 3 LOW).
- 2026-07-29 ~17:08 — R2b returned **CHANGES REQUIRED** (3 BLOCK / 3 HIGH / 6 MED / 4 LOW). Confirmed the
  load-bearing `RunAsync` claim is CORRECT, refuting LEAD's L-2.
- 2026-07-29 ~17:12 — `05-REVIEWS.md` written. LEAD arbitrated 3 cross-reviewer conflicts.
- 2026-07-29 ~17:15 — **USER DECISIONS**: R-M5 = RATIFY the commander-extraction fix into Phase 5 as D-09
  (no backfill of pre-existing URL rows). R-H1/R-H2 = Core parameter-type test only;
  **`.github/workflows/ci.yml` MUST NOT be modified**; Postgres recorded as NOT VERIFIED + open prod risk.
- 2026-07-29 ~17:16 — R3 dispatched: one worker folds all 20 findings into the 5 planning docs.
  Write set fenced to `05-CONTEXT.md`, `05-01/02/03-PLAN.md`, `05-VALIDATION.md`. No source, no ci.yml,
  no git mutation. LEAD verifies the diff against each finding ID on return.
- 2026-07-29 ~17:28 — R3 returned **DONE**, all 20 findings APPLIED. **LEAD-VERIFIED independently:**
  exactly the 5 write-set files modified (+ pre-existing `config.json`); `git diff --stat` 164/72 is
  **byte-identical** to `--ignore-all-space` → zero whitespace churn; `CR=0` on all five (LF held);
  all four replaced `must_haves` proxies measured **grep-0 at HEAD** so they can no longer pass
  pre-change; 05-01 option A deleted and the throwing default mandated with the 14-implementer
  rationale; `ContentHashDedupTests.cs` in `files_modified`; D-09 present and worded as a decision with
  the unreachability proof; `ImportUrl`→`SubmitUrl` corrected; VALIDATION map exactly 8 rows
  `05-01-01`..`05-03-03`; `format-check-changed.sh` in all three plans; **`.github/workflows/ci.yml`
  untouched** as required. R3 = **ACCEPTED**.
- Worker judgment calls surfaced for the user, not silently accepted: (a) `README.md` added to 05-03's
  write set because ratifying D-09 makes admin banner text user-visible — reversible if unwanted;
  (b) R-H2's extraction landed as a labelled "Step A" inside 05-02 T2 rather than a 4th task, because
  the mandated 8-row map fixes plan 02 at 3 tasks; (c) `ArchidektDeckImportResult.Metadata` became
  **nullable** — the only way to withhold `CapturedUtc` without making `CapturedUtc` itself nullable and
  weakening D-03's three-state guarantee; (d) `05-VALIDATION.md`'s "Quick run command" row still omits
  `ArchidektDeckMetadataParametersTests`.
- **STILL OWED: R4, the Codex authoritative re-review.** Cannot run — no credits. `05-REVIEWS.md` carries
  the assurance disclosure in writing so this round cannot be mistaken for cross-AI verified.
- **Nothing committed.** Plan-doc commits are the user's to trigger.
