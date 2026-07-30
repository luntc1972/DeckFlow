# Foreman Ledger — plan 02-08 (wave 7) execute

- Run started: 2026-07-28
- Worktree: /mnt/c/users/chrislunt/source/personal/deckflow-role-floors
- Branch: gsd/cycle21-cut-lab
- Baseline commit: a490e8af (pushed; 0 unpushed at run start)
- Mode: Codex-boosted (Agent tool + real shell + consented Codex)
- Codex seats this session: gpt-5.4 medium (coding), gpt-5.5 medium (review/planning)
- Scope: Task 1 ONLY. Tasks 2 and 4 are blocking operator checkpoints; Task 3 needs an
  operator-exported credential. Do not execute them in this run.

## Preconditions verified before dispatch

- Env-var credential path (plan 02-04 D-07) present: RoleFloorResearchCommandRunner.cs:166 reads
  DECKFLOW_ROLE_FLOOR_CONNECTION_STRING; --connection-string is IsRequired = false.
  => the wrapper must NOT pass --connection-string, and the plan's STOP-and-report fallback is moot.
- EDHREC corpus in place (D-03): manifest cells_written 1525, cells_failed 0, cells_404 0,
  commanders_selected 305, fetch_ended_utc 2026-07-27T19:11:29Z; _edhrec-brackets/cells/ holds 1525 files.
- Scryfall cache present: _role-floor-research/cards_full.json, 8,220,503 bytes.
- No stale run artifacts in the phase folder (no run/smoke log, exit, FINDINGS or GRID files).

## Tasks

| ID | Task | Seat | Write set | Status |
|---|---|---|---|---|
| T1 | Write fail-closed run wrapper + prove harness read-only | Codex gpt-5.4 medium | scripts/edhrec-brackets/run-role-floor-research.sh | DONE (verified) |
| T2 | Operator checkpoint | developer | -- | ANSWERED (see decisions) |
| T3a | Grid arm (D-09), no credential needed | foreman inline | EDHREC-ROLE-GRID.{md,json} + run log/exit | DONE (verified, UNCOMMITTED) |
| T3b | Smoke run + real harness run | DEVELOPER's shell | run/smoke log + exit + FINDINGS | PENDING -- awaiting operator |
| T4 | Disposition + commit | developer checkpoint | -- | BLOCKED on T3b |

## T3b FAILED 2026-07-28 -- smoke run exit 1, criterion 3 UNPROVEN

Developer ran it from a WSL bash shell (not PowerShell), credential via `read -rsp` so it never
touched a command line or shell history. Length 127 -- a full Render external URL.

What succeeded: connection, IP allowlist, taxonomy drift guard, and the FULL membership load --
4,011/4,011 commanders in 26:20, commandersWithMembership=3,958, rawDecks=130,075.

What failed: card resolution. Final log line:
  `Scryfall card lookup (cards/collection) returned HTTP 429 during role-floor research.`
Exit code 1, NOT the required 2. No findings artifact (correct, but exit 1 proves nothing about the
guard). Credential grep over the smoke log: exit 1, zero matches. Log non-empty, 60 lines.

### Causal correction -- my first diagnosis was WRONG

I initially reported that 13 DFC fallback searches "burned the quota" and took down the batch
endpoint. The log disproves that. Line 26 is the FIRST Scryfall event in the entire run and it is
already `attempt 1/4` of a 429 -- no ramp-up. The collection batch immediately before it SUCCEEDED.
So the throttle was ALREADY in force before the first fallback fired; the fallbacks did not cause it.

Most likely cause: the grid arm's 18,654 lookups, which finished at 13:30. Card resolution began
~14:37. My ~8 diagnostic curl calls at ~14:55-15:05 got ZERO 429s, so the window had lifted by then.

The 429s are genuine: `ExecuteWithScryfall429RetryAsync` catches only
`when (ex.StatusCode == HttpStatusCode.TooManyRequests)`, so a not_found (404) can never be logged
as a 429.

Consequence: **the DFC defect and the 429 are INDEPENDENT.** Fixing DFC does NOT guarantee the smoke
run succeeds. It removes needless traffic; it does not restore spent quota.

## Plan 02-10 -- DFC collection identifiers (COMMITTED)

Discovered while diagnosing the above, then proven INDEPENDENTLY against the live Scryfall API rather
than inferred from the run.

Measured rule, api.scryfall.com, 2026-07-28: `cards/collection` `name` identifiers match a SINGLE
FACE name (front OR back). The combined form NEVER matches. Verified across layouts transform,
modal_dfc, adventure, split, and across all three spacings (`A // B`, `A / B`, `A//B`).
`Insectile Aberration` and `Stomp` -- BACK faces -- both resolve, which is what pins the rule as
"face" rather than "front face".
The opposite holds for `cards/named?exact=` and `cards/search?q=name:"..."`, which DO accept the
combined form -- so NormalizeForScryfall stays correct at its :170/:196 call sites.
Known exception, accepted: `Who // What // When // Where // Why` resolves by its combined name while
bare `Who` does not (presumably ambiguity). Such cards keep using the existing fallback.

Four defective call sites, two of them PRODUCTION:
  DeckFlow.CLI/RoleFloorResearchCommandRunner.cs:722
  DeckFlow.CLI/EdhrecRoleGridCommandRunner.cs:375   (masked -- the grid ran before we were throttled,
                                                     so its DFCs resolved expensively via fallback and
                                                     reported UnresolvedCards=0, which read as health)
  DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs:110
  DeckFlow.Web/Services/Packets/ScryfallReferenceResolver.cs  -- worst case: it ran
    NormalizeForScryfall, converting " / " into " // ", i.e. turning one failing form into another,
    on a doc comment that claimed the opposite.

Commits: 0246fedf feat(02-10) Core helper + tests; f8c960a5 fix(02-10) all four call sites.
Build 0/0. Core 1766 (+14). Web 2097 (+2), 16 skipped. EOL zero churn on all six files.
CardNormalizer.Normalize untouched; match-back untouched (changing it is how the sibling cEDH DFC bug
discarded results via key mismatch).

NOTE ON PROCESS: the first Codex dispatch was SIGTERM'd at the 10-minute foreground timeout having
committed NOTHING -- a LOST worker. Reconciled against baseline before trusting anything: scope fence
had held, 02-08 evidence intact, caches intact, CardNormalizer untouched. Work was complete and
on-spec, so it was committed rather than re-dispatched. The plan's TDD requirement to OBSERVE the
failing test was NOT satisfied -- Codex died before reporting it. Recorded as unmet, not claimed.

FOLLOW-UP (not fixed, outside 02-10's write set): `normalizeForScryfall` is now a DEAD parameter on
ScryfallReferenceResolver.ResolveBatchAsync -- never read, yet four production callers still pass it
(DeckHistoryPageService and DeckAnalysisPacketService pass true; DeckComparisonService and
MetaGapService pass false). Removing it touches four more files.

## Plan 02-11 -- cheap criterion-3 smoke run (DISPATCHED, background)

Root problem: the exit-2 guard sits at :493, AFTER ResolveCardsAsync at :326, so proving criterion 3
costs a full membership load plus a full Scryfall pass. `--min-decks 999999` does not shrink it,
because qualification is evaluated at :429.

Fix: `--limit N` capping commanders paged in LoadCommanderRowsAsync, defaulting to unlimited.
`MIN_DECKS=999999 LIMIT=50` then reaches the guard in seconds and -- with cards_full.json at
20.29 MB -- likely with zero Scryfall calls, removing the rate limit from the critical path.
D-03 requires a limited run be LABELLED in provenance so it can never pass as a full run.
Dispatched to Codex gpt-5.4 medium in the BACKGROUND, to avoid a second timeout kill.

## Task 2 checkpoint -- developer decisions, recorded 2026-07-28

1. Schema-ensure DDL: "Verify first via Render MCP" -- CLEARED BY EVIDENCE, see below.
2. Run path for the credential-bearing half: the DEVELOPER runs it in their own terminal.
   The agent never receives the connection string. This supersedes the plan's assumption that the
   agent runs the wrapper; the wrapper's contract is unchanged.
3. D-06 exit-2 smoke run: APPROVED, to run before the real run.
4. Plan 02-09 grid arm (D-09): APPROVED, run first.
5. Assumed and stated to the developer, not contradicted: EDHREC bracket corpus USED (so criterion 8
   and decision D-A remain in scope, not waived), and --min-decks 40.

### DDL escalation CLOSED by live read-only evidence

Read-only catalog query via Render MCP against dpg-d7oj8iugvqtc73fso0g0-a (no write, no credential
handled by the agent, and NOT used as a connection-string source for the run):

- `deck_queue.content_hash` column EXISTS -> the guarded ALTER TABLE ADD COLUMN is a no-op.
- `ix_deck_queue_processed_commander` ABSENT -> DROP INDEX IF EXISTS is a no-op.
- `ix_deck_queue_processed_commander_lower` ABSENT -> DROP INDEX IF EXISTS is a no-op.
- `ix_deck_queue_commander_lower_processed` PRESENT -> the replacement index already exists.

Conclusion: the index-replacement migration has already run in production, so EnsureSchemaAsync is a
complete no-op against this database and NO DDL will actually execute during the harness run. The
Task 1 concern is resolved with evidence rather than inference. Threat row T-02-08-C's "read-only"
claim should be reworded in a future plan to "SELECTs plus a schema-ensure that is a verified no-op
against this instance" -- recorded as a plan-doc defect, not fixed here (no .cs or plan edit in scope).

## Task 2 automated gate (plan requires green suite)

- `dotnet build DeckFlow.sln`: 0 errors, 0 warnings.
- DeckFlow.Core.Tests: 1752 passed, 0 failed, 0 skipped.
- DeckFlow.Web.Tests: 2095 passed, 0 failed, 16 skipped.
Matches the wave 6 baseline exactly, as expected -- Task 1 changed no .cs file.

## T3a -- grid arm result (D-09), NOT YET COMMITTED

Invocation used Windows paths, NOT the plan's literal /mnt/c/... paths:
  --edhrec-csv 'C:\users\...\deckflow\artifacts\edhrec\data-jul26-uigloqve\edhrec.csv'
  --averages   'C:\users\...\deckflow\artifacts\edhrec\averages-jul26-m5o50xfj\averages.csv'
  --cards-cache '_role-floor-research/cards_full.json'

PLAN-DOC DEFECT: 02-08-PLAN.md lines 692-693 specify /mnt/c/... paths. dotnet.exe is a WINDOWS
process and cannot resolve them. A --dry-run preflight caught this before spending any Scryfall
traffic -- which is only possible because wave 6 fixed the dry-run ordering bug (validation now
precedes the dry-run exit). Surface in 02-08-SUMMARY.md; do not hand-edit the plan.

Outcome: exit 0. All 249/249 Scryfall batches completed.
  RowsRead=14,150,219  DistinctCards=31,788  Commanders=3,226
  DenominatorMismatches=146  MissingDenominators=6  UnresolvedCards=0

Cross-checks:
- RowsRead and DistinctCards match wave 6's INDEPENDENT quote-aware parse to the digit
  (14,150,219 rows / 31,788 distinct cards). Strong evidence the reader is correct.
- Commander accounting reconciles exactly: 3,378 true commanders - 146 mismatches - 6 missing
  denominators = 3,226 accumulated. No unexplained loss.

Worst five denominator mismatches (all partner/Background commanders, confirming the plan's
solo-vs-partner join hypothesis; excluded rather than clamped, which is the correct behavior):
  Haldan, Avid Arcanist   | Island        | count=7942  | denom=1  | ratio=7942
  Atreus, Impulsive Son   | Mountain      | count=11328 | denom=5  | ratio=2265.6
  Jenny Flint             | Forest        | count=2140  | denom=3  | ratio=713.333
  Dargo, the Shipwrecker  | Sol Ring      | count=7830  | denom=11 | ratio=711.818
  Kraum, Ludevic's Opus   | Command Tower | count=14618 | denom=22 | ratio=664.455

Structural gates on the produced artifact (not merely on the emitter):
- Credential grep over .md, .json and the run log: exit 1, ZERO matches. NOTE: the first attempt at
  this check was DEFECTIVE -- `grep ... | head -5` then `$?` reports head's status, not grep's,
  so it would have reported "clean" unconditionally. This is the FIFTH defective grep criterion this
  phase. Re-run without the pipeline before being believed.
- Exactly ONE markdown table exists in the file; its header is
  `| Source | Commander | Role | Expected count | Decks (denominator) | Rows | Max card inclusion |`
  -- Source present as the leading column. The other two sections (Commander totals, Known gaps) are
  bullet lists, not figure tables.
- No percentile column in any table header (checked over header rows only, exit 1). The single
  "percentile" string in the file is the prose prohibition "This is a mean-style expected count, not
  a percentile", which is the structural guarantee being DOCUMENTED, not violated.
- Shared Scryfall cache grew 8,220,503 -> 19,279,683 bytes, warming it for the harness run.
- _edhrec-brackets/cells/ still 1525 files; no request was made to json.edhrec.com.

Artifacts are UNSTAGED and UNCOMMITTED, per plan Task 3 step F -- committing is Task 4's business,
after the developer dispositions the harness run.

## Attempts (append-only)

- T1 attempt 1: dispatched to Codex gpt-5.4 medium, sandbox danger-full-access, approval never.
  Reported DONE_WITH_CONCERNS at 6fc30354. Concern: EnsureSchemaAsync DDL is not purely
  CREATE ... IF NOT EXISTS. See ESCALATION below.
- T1 blind verification: fable-foreman:foreman-verifier, fresh context, given the plan's Task 1
  verbatim and never the worker's restatement. Verdict PASS_WITH_NOTES, 13/13 criteria PASS.
  Proved by stub-pipeline simulation that harness exit 2 survives `set -e`, reaches the .exit file,
  and is PIPESTATUS[0] rather than tee's status. Proved `${EDHREC_DATA+x}` keeps unset and
  explicitly-empty distinguishable. Proved WSLENV is load-bearing for the env var to cross the
  WSL -> Windows dotnet.exe boundary. HEAD and git status unchanged by verification.
  Four non-blocking findings.
- T1 fix batch (one worker for the whole findings list, per budget discipline): Codex gpt-5.4 medium,
  commit a01d0968. Applied findings 1 (stored mode 100644 -> 100755), 2 (positional args now loudly
  refused with a shell-history warning, without echoing the argument's value -- verified the fake
  credential 'p@h' does not appear in the output) and 3 (harness-rejection diagnostic moved to stderr
  so it survives into the log, and reworded to name BOTH the missing-02-04-D-07 cause and the WSLENV
  propagation cause). Finding 4 (the wrapper duplicating the harness's `cedh` --mode default as a
  literal) accepted as-is: explicit is defensible and it is documented.
- Scope gate after T1: `git diff --name-only a490e8af HEAD` lists only the wrapper script; no tag at
  HEAD; _edhrec-brackets/cells/ still 1525 files; cards_full.json still 8,220,503 bytes; both caches
  still untracked.

## ESCALATION -> Task 2 checkpoint (plan Task 1 requires this be raised, not decided by the agent)

Plan Task 1 says: state whether EnsureSchemaAsync's DDL is idempotent `IF NOT EXISTS`, and confirm
the target database already has the schema -- "If you cannot confirm that, escalate at the Task 2
checkpoint rather than proceeding."

Findings, read directly from DeckFlow.Core/Knowledge/CategoryCacheSchema.cs:

- The harness DOES reach EnsureSchemaAsync. DeckQueueRepository calls it at the head of every method
  (:35, :66, :93, :120, :175, ... 16 call sites), and role-floor-research calls
  GetPagedProcessedCommanderRowsAsync through that repository. So threat row T-02-08-C's flat claim
  that "the harness is read-only" is too strong: it issues SELECTs plus a schema-ensure that CAN
  emit DDL.
- Idempotency, statement by statement:
  - `CREATE TABLE IF NOT EXISTS` / `CREATE [UNIQUE] INDEX IF NOT EXISTS` -- idempotent by SQL form.
  - `ALTER TABLE deck_queue ADD COLUMN content_hash TEXT NULL;` (:65) -- NOT idempotent by SQL form,
    but guarded application-side by `if (!deckQueueColumns.Contains("content_hash"))` (:62), which
    reads the live column list first. Idempotent in effect.
  - `DROP INDEX IF EXISTS ix_deck_queue_processed_commander;` and
    `..._processed_commander_lower;` (:91-92) -- these are the sharp edge. They will not error, but
    if those two legacy indexes still exist on the live database, running the harness DROPS them.
    That is a real, permanent write against a production-adjacent database. The in-code comment
    (:89) shows this is a deliberate index-replacement migration: the replacement
    `ix_deck_queue_commander_lower_processed` is created first in the same batch, and the batch runs
    inside a try/catch that swallows index-creation failures so the drops cannot execute if the
    replacement failed.
- What is NOT confirmable without touching the live database: whether prod has already run this
  migration. It almost certainly has -- the live web app runs this same code path against this same
  instance -- in which case every statement is a no-op. But that is inference, not evidence.
- CLAUDE.md requires explicit per-task user authorization for any DDL against a shared database.
  So this is the developer's call at Task 2, not the agent's.

## 02-11 (--limit) — ACCEPTED 2026-07-28 15:54

Commits: `5a10ce78` test, `15958f3d` feat. Codex gpt-5.4 medium, background, status DONE, no deviation.

Gates (foreman-run, independent of worker report):
- build 0 warnings / 0 errors
- Core 1775 (+9 from 1766), Web 2097 (+0, 16 skipped)
- EOL: all 5 files LF, worktree == base; `git diff --stat` == `git diff --ignore-all-space --stat` (zero churn)
- wrapper: `bash -n` OK; secret unset -> exit 1; positional arg -> exit 1
- JSON equivalence proved out-of-band (scratch console app, real JsonOptions): anonymous-object -> Dictionary swap is BYTE-IDENTICAL when no limit is set

Blind verifier (foreman-verifier, fresh context, given 02-11-PLAN.md verbatim): **PASS_WITH_NOTES**, 8/8 criteria PASS.

LOW findings recorded, none actioned:
1. `FormatCommanderLimit`'s `"full corpus"` arm is unreachable (only call site is inside `HasValue`).
2. `break` precedes the `page % 5` log, so a limit landing exactly on page 5/10/... loses one progress line. Unlimited path unaffected (4 log lines, unchanged).
3. Markdown warning selected by `StartsWith("limited run:")` ~420 lines from its producer, no test either side. Sound today, fragile.
4. No test pins the JSON `methodology` shape. Equivalence is empirical only; a future `DictionaryKeyPolicy` addition would rename every key silently.
5. PROCESS: commit `5a10ce78` ("test:") contains the implementation too, so no repo state exists where the tests fail. The CS0117 failing output WAS observed in the Codex transcript this time, but the commit split does not preserve it.
6. OPERATOR TRAP: `LIMIT=0` or negative silently runs the FULL multi-hour corpus and prints no "limit in effect" line. Spec-conformant (predicate treats <=0 as no limit by design) but the wrapper should refuse it loudly. QUEUED, not fixed -- fixing it now would race the operator's smoke run.

NOT verified (do not claim otherwise):
- The `<success_criteria>` end-to-end run. Requires the live secret; forbidden to the agent. Criterion 3 REMAINS UNPROVEN.
- `ResolveCardsAsync` (:333) still precedes the guard (:500), so a 50-commander run still hits Scryfall for any card missing from `cards_full.json`. Cache is warm (19.3 MB) but coverage of an arbitrary 50 is not guaranteed.
- format-gate and CI not run.

PUSH ANOMALY: `origin/gsd/cycle21-cut-lab` advanced to `5a10ce78` between 15:31 and 15:36, during the Codex run. NOT Codex -- its transcript contains `git push` only twice, both as quoted fence text, never executed. No hook contains a push. Same unexplained mechanism seen on `cycle20-personal-tools` this morning. Local HEAD `15958f3d` is one ahead of the remote.

## 02-08 Task 3a — SMOKE RUN PASSED 2026-07-28 15:58

Command (operator's terminal, WSL bash): `MIN_DECKS=999999 LIMIT=50 bash scripts/edhrec-brackets/run-role-floor-research.sh`
Wall clock: 15:56 -> 15:58, ~2 minutes (membership load 00:01:43). Prior attempt at this cost ~26 min and died on Scryfall 429.

Result: **exit 2**, no artifact. ROADMAP criterion 3 PROVEN EXECUTABLY.

Log:
    Commander limit in effect: 50 (--limit 50).
    Loaded commander memberships 50/50 in 00:01:43 (commandersWithMembership=50, rawDecks=22941).
    Zero commanders met the minimum deduped deck count of 999999.
    Commander rows enumerated: 50.
    ThresholdCounts: 15..100 all = 50
    NO findings artifact was written.

Foreman-verified independently of the harness's own claim:
- `cat role-floor-research-smoke.exit` -> `2`
- `ls ROLE-FLOOR*` -> no such file; no findings artifact exists in the phase dir at all
- `find . -maxdepth 1 -mmin -10 -type f` -> ONLY role-floor-research-smoke.{log,exit}. Nothing else written.
- EDHREC-ROLE-GRID.{md,json} untouched (still 13:30).

What this proves that no unit test could: the exit-2 guard's PLACEMENT. `HasNoQualifyingCommanders` returns true, `return 2` fires at :517, and control never reaches BuildGoNoGo/WriteFindingsFiles at :569-570. A misplaced guard would have written a bogus artifact and still exited 2.

Also proven live: `--limit` parses, threads Program.cs -> RunAsync -> LoadCommanderRowsAsync, trims to exactly 50 (log says 50/50, not 200), and the paging stopped at page 1 of 21.

NOTE: no Scryfall traffic appeared in the log. ResolveCardsAsync (:333) still precedes the guard, so it ran -- the warm 19.3 MB cards_full.json cache evidently served all 50 commanders' cards with zero network calls. The 429 exposure flagged pre-run did not materialise. Do NOT generalise this to the full run, which touches far more distinct cards.

REMAINING on 02-08: Task 3b (the real run, MIN_DECKS=40 MODE=cedh, no LIMIT) and Task 4 (blocking developer disposition + commit of the grid artifacts).

## LIMIT guard — commit `e21fdd78` 2026-07-28 16:07

`fix(02-11): refuse a non-positive LIMIT before starting a full run`. Codex gpt-5.4 medium, background, DONE, no deviation. Wrapper only, +17/-1.

Implementation: `case "${limit}" in *[!0-9]*)` rejects any non-digit, then `[ "$((10#${limit}))" -le 0 ]` rejects zero. `10#` forces base 10 -- without it bash reads `050` as OCTAL 40. Verified: `$((050))=40` vs `$((10#050))=50`.

Foreman-verified independently, 7 inputs, dummy placeholder secret `x` (never a real credential):
  LIMIT=0 -> exit 1;  -1 -> exit 1;  abc -> exit 1;  1.5 -> exit 1;  " 50" -> exit 1;  "+5" -> exit 1
  LIMIT=0050 -> ACCEPTED, parsed to 50 (correct: leading zeros are a plausible typo, not an error)
Secret check still fires FIRST (unset secret + LIMIT=0 -> secret error, not limit error).
Invariants intact (fixed-string grep): EDHREC_DATA+x, set +e, PIPESTATUS[0], set -euo pipefail, export WSLENV, no-positional-args, 999999 routing, `--connection-string` still never on argv.
EOL: LF, 0 CR both sides. `git diff --stat` == `--ignore-all-space --stat`, zero churn.

### TWO FOREMAN ERRORS THIS STEP — recorded, both mine

1. DEFECTIVE GREP CRITERIA (#6 and #7 this phase). Used `grep -cE 'EDHREC_DATA+x'` and `grep -cE 'set +e'`.
   In ERE `+` is a quantifier, so both patterns tested for something else entirely and returned count=0,
   which I nearly read as "invariant deleted". Re-ran with `grep -cF`; both count=1, intact.
   The standing lesson keeps repeating: assert on the CONSTRUCT, and use -F for anything containing regex metachars.

2. EVIDENCE CONTAMINATION, self-inflicted. My `LIMIT=0050` probe correctly PASSED the new guard and
   therefore LAUNCHED the harness. With MIN_DECKS defaulting to 40 it took the real-run file routing and
   wrote `role-floor-research-run.{log,exit}` -- the REAL run's evidence filenames -- at 16:07.
   Contents: `Commander limit in effect: 50 (--limit 50).` then
   `Format of the initialization string does not conform to specification starting at index 0.`, exit 1.
   The dummy string `x` failed at connection-string FORMAT PARSING, so NO connection was attempted and
   prod was never touched. Files were untracked and never staged; git lost nothing.
   REMOVED both, because leaving them would let Task 4 mistake a 137-byte failed probe for the real run.
   Lesson: a probe that exercises the ACCEPT path of a launcher launches the thing. Probe the reject path
   only, or stub the launch.

Side effect worth keeping: that accident proved end-to-end that `0050` reaches the CLI as 50
(`Commander limit in effect: 50`), i.e. the base-10 handling works through the whole chain, not just in the guard.

WRAPPER IS NOW CLEAN. The real run (`MIN_DECKS=40 MODE=cedh`, no LIMIT) is unaffected by this change --
LIMIT-unset arg-building is byte-identical, replayed and confirmed.

## 02-08 Task 3b — REAL RUN COMPLETE 2026-07-28 16:37, EXIT 0

Wall clock 16:10 -> 16:37, ~27 min. Membership load 26:24 for 4011 commanders. NO Scryfall 429s at any point.

Artifacts written: `RESEARCH-FINDINGS.md` (1,614,675 B) and `RESEARCH-FINDINGS.json` (5,624,602 B), both 16:37:38.

Headline: RawDecks=130075, DedupedDecks=128407, Commanders=4011, commandersWithMembership=3958, **QualifyingCommanders=841** at DEDUPED N>=40.
- GO (7): lands, ramp, draw, interaction-targeted, engines, payoffs, wincons
- Signal present but insufficient breadth (2): interaction-mass, protection
Clearing-commander counts: lands 411, engines 379, ramp 277, draw 275, interaction-targeted 273, wincons 153, payoffs 124.
Scryfall: 14 unresolved (all not_found), **0 rate_limited_after_retry**. The 02-10 DFC fix plus the warm cache held.

### FOREMAN ERROR — wrong artifact glob (#8 defective criterion this phase)
I checked for artifacts with `ls ROLE-FLOOR*`. That filename is my invention; the harness writes
`RESEARCH-FINDINGS.{md,json}`. Both the smoke-run watcher and the real-run watcher printed
"NO ROLE-FLOOR* artifact written" -- FALSE for this run, which wrote 7.2 MB of artifacts.

The criterion-3 conclusion nevertheless STANDS, and not by luck: the load-bearing smoke-run evidence was
`find . -maxdepth 1 -mmin -10 -type f`, which is glob-independent and returned ONLY
role-floor-research-smoke.{log,exit}. RESEARCH-FINDINGS.md is stamped 16:37 (this run); nothing was
written at 15:58. Criterion 3 remains proven. The redundant check is what saved the conclusion --
keep pairing a named-pattern check with a mtime sweep.

### TWO ITEMS FOR THE TASK 4 CHECKPOINT

1. **PROVENANCE DEGRADED — the artifact cannot be tied to a code state.**
   `| Harness Commit SHA | unknown |` plus the warning line. Diagnosed: `DescribeHarnessCommitSha`
   (RoleFloorResearchCommandRunner.cs:604) shells `git rev-parse --short HEAD`, but the harness runs under
   WINDOWS dotnet.exe while this worktree's `.git` is a pointer FILE containing a WSL path
   (`gitdir: /mnt/c/users/chrislunt/source/personal/deckflow/.git/worktrees/deckflow-role-floors`).
   Windows git cannot resolve a /mnt/c path, so rev-parse fails and the value falls back to "unknown".
   From WSL bash the same command works (`e21fdd78`). This is a WSL-worktree + Windows-dotnet interaction,
   not a code defect in the harness. A research artifact that cannot name its own code state is weak
   evidence; recommend re-stamping or re-running before these findings are cited.

2. **THE CALIBRATION CONTROL CAME BACK GO -- lands, 411 clearing commanders.**
   The ROADMAP set lands deliberately as a control against the 2026-07-16 study whose verdict was
   "commander identity barely moves land count; bracket is the only driver". This run contradicts that.
   Two readings, and the checkpoint must pick one deliberately:
     (a) the within-commander P25 method detects real structure that EDHREC point estimates could not
         measure -- which is exactly the stated reason for the hybrid corpus; or
     (b) the bar is too permissive and lands is a false positive, which would put the other six GOs
         under the same doubt.
   Internal tension worth weighing: the run's own casual-bias metric gives lands the LOWEST lower-tail
   spread ratio of all nine roles (0.899, vs 1.128 mean) yet lands has the MOST clearing commanders (411).
   Do not wave this through on the strength of six other GOs.

Other checkpoint inputs: EDHREC cEDH bracket is THIN (40 qualifying cells) and exhibition is NOT REPORTED
(1 cell) -- bracket-5 conclusions are weakly supported. `protection` is explicitly PROVISIONAL pending the
deferred Phase 01.2 and its floors are a stated LOWER BOUND.

STILL UNCOMMITTED by design, pending Task 4 disposition: RESEARCH-FINDINGS.{md,json},
role-floor-research-run.{log,exit}, role-floor-research-smoke.{log,exit}, EDHREC-ROLE-GRID.{md,json},
edhrec-role-grid-run.{log,exit}.

## LANDS GO IS AN ARTIFACT — measured, not inferred, 2026-07-28 16:52

Hypothesis (raised from the per-commander means, then TESTED rather than asserted): the Postgres arm's
"lands" figure is DISTINCT LAND NAMES, not land count, because findings line 82 states Postgres decks are
classified as singleton card sets while EDHREC preserves real quantities. `8x Island` therefore counts as 1.
Corpus mean 18.35 vs a real Commander deck's ~36 is consistent with that.

Test: joined all 841 qualifying commanders from RESEARCH-FINDINGS.json against `color_identity` in
_role-floor-research/cards_full.json. 841/841 matched, 0 unmatched (front-face fallback for DFC names).
Script: scratchpad/colour_test.py. Read-only; no repo file written.

Pearson r, colour count vs role mean:
  lands                 r=0.734  r^2=0.539   <-- colour count explains 54% of the variance
  ramp                  r=0.034  r^2=0.001
  draw                  r=-0.109 r^2=0.012
  interaction-targeted  r=-0.130 r^2=0.017
  engines               r=-0.043 r^2=0.002
  payoffs               r=0.053  r^2=0.003
  wincons               r=0.043  r^2=0.002

Mean "lands" by colour count: 1c=10.06, 2c=17.09, 3c=22.13, 4c=24.14, 5c=25.73. Monotonic.

Clearing DIRECTION by colour count is the decisive evidence:
  1 colour (n=155): 118 clear LOW,   3 clear HIGH
  2 colours(n=347):  14 clear LOW,  31 clear HIGH   (only 13.0% clear at all -- 2c sits at the corpus median
                                                     and fails the 1.5x/0.667x ratio gate from the middle)
  3 colours(n=271):   0 clear LOW, 191 clear HIGH
  4 colours(n=11) :   0 clear LOW,  10 clear HIGH
  5 colours(n=46) :   0 clear LOW,  39 clear HIGH
Perfect monotonic ordering with a clean sign flip. The lands "signal" is a colour-identity gradient.

CONSEQUENCES
- The lands GO must be PULLED before Phase 3 consumes it. A commander-aware land floor built from this
  would encode "how many colours you play" as if it were "how many lands you need".
- The six nonland GOs are UNAFFECTED: r^2 <= 0.017 for every one. Singleton-set treatment is correct for
  them because Commander is singleton outside lands, which is exactly what line 82 says.
- The apparent contradiction with the 2026-07-16 land study DISSOLVES. That study measured TOTAL lands;
  this run measured DISTINCT land names. Different quantities, so this never contradicted it. The
  calibration control did not fail -- it was miscalibrated, because the singleton assumption that is right
  for six roles is wrong for the seventh.
- Fix options for a future pass: carry quantities for lands, or restrict the lands role to nonbasics, or
  drop lands from the Postgres arm and take land floors solely from the EDHREC arm (which preserves
  quantities) plus the already-shipped ManabaseBaselineSnapshot path.

## 02-08 Task 4 — DISPOSITIONED AND COMMITTED 2026-07-28 17:00. WAVE 7 / PHASE 2 CLOSED.

Developer decision at the blocking checkpoint: GO on six roles, LANDS PULLED.

Commits:
  b062b944 docs(02-08): commit the role-floor research findings and run evidence
  6afb0bb7 docs(02-08): record the wave-7 summary and the lands disposition
  0ed373d3 docs(02-08): mark phase 2 complete in the roadmap progress table

Pre-staging gates, all run before anything was staged:
- CREDENTIAL GREP, unpiped so the exit status is the actual check (the piped-into-head mistake made
  earlier in this phase is not repeated): all ten evidence files CLEAN, zero matches for
  postgres://, password=, pwd=, user:pass@host, sslmode=.
- D-08 SOURCE ATTRIBUTION on the GENERATED markdown: 26 tables parsed, 18 carry a Source column,
  21,294 data rows under them, ZERO empty source values. The 8 tables without a Source column are all
  provenance/coverage/baseline summaries carrying no per-commander figures. PASS.
- No CalVer bump, no tag, no scripts/release.* invocation.

Artifact integrity: RESEARCH-FINDINGS.{md,json} committed EXACTLY as emitted. They still list `lands`
in rolesInScopeForPhase3. This is deliberate and correct per plan D-08 -- a disagreement with a
generated artifact is recorded in 02-08-SUMMARY.md, NEVER by hand-editing the artifact. The summary is
the authority on the disposition; the artifact is the authority on what was computed.

D-05 / .gitignore CONFLICT, resolved without violating either rule: D-05 requires the run log committed
as evidence, but `.gitignore:7` ignores `*.log`, and .gitignore is on CLAUDE.md's Do-Not-Modify list
with D-05 additionally forbidding the edit under any checkpoint authorization. Used `git add -f` on the
three run logs -- commits the evidence, does not touch the ignore file. Recorded as a deviation in the
summary. VERIFIED after commit: `git log -- .gitignore` shows no new commit; the caches
`_role-floor-research/` and `_edhrec-brackets/` remain untracked.

Final state: working tree clean apart from the untracked caches and these ledgers. 93 commits ahead of
main. origin still at 5a10ce78, so the last 5 commits are local only -- pushing is the user's call.

Follow-ups recorded in 02-08-SUMMARY.md, none actioned: gitignore question for the two cache dirs;
~19 MB of generated artifacts now in a public repo; dead `normalizeForScryfall` parameter; plan 02-08's
own wrong `/mnt/c/...` operator paths at lines 692-693; harness SHA detection broken for WSL worktrees
under Windows dotnet.
