# Foreman Ledger — Phase 03 commander-aware-floor-defaults, execute

BASELINE: `84a4d5f4` | working tree clean of tracked modifications; 11 untracked paths
(`.foreman/*` ledgers, `.foreman/scratch/`, `_edhrec-brackets/`, `_role-floor-research/`) | 2026-07-28

- Worktree: /mnt/c/users/chrislunt/source/personal/deckflow-role-floors
- Branch: `gsd/cycle21-cut-lab` (156 commits ahead of `origin/gsd/cycle21-cut-lab`, local only)
- Mode: Codex-boosted (Agent tool + real shell + consented Codex)
- Codex seats this session: `gpt-5.4` medium — coding; `gpt-5.4` medium — review/analysis
  (user confirmed "keep defaults" at run start)
- Scope authorized by user: all 6 waves. Plan 03-05 carries a blocking human-verify checkpoint.

## Plan

| Wave | Plan | Class | Objective (short) |
|---|---|---|---|
| 1 | 03-01 | WORKHORSE | Core snapshot contract + adoption filter + fail-closed drift check (3 tasks) |
| 1 | 03-07 | WORKHORSE | Headroom-ranked locked-overshoot advisory (3 tasks) |
| 2 | 03-02 | WORKHORSE | CLI `role-floor-baseline` generator + shipped `latest.json` (3 tasks) |
| 3 | 03-03 | WORKHORSE | Runtime fail-open provider + shared commander-key extraction (3 tasks) |
| 4 | 03-04 | WORKHORSE | `max(bracket, commander)` floor resolution wire-up (3 tasks) |
| 5 | 03-05 | WORKHORSE | Role-floors table 4→6 columns (4 tasks) — **non-autonomous, human verify** |
| 6 | 03-06 | WORKHORSE | Overlap-corrected feasibility advisory (3 tasks) |

## Routing

- Every plan → Codex `gpt-5.4` medium. Plans are exhaustively specified (file-level actions,
  named tests, explicit acceptance criteria), which is the WORKHORSE profile — no architecture
  judgment is delegated.
- Verification → Claude `foreman-verifier` (blind, fresh context, read-only). Cross-family by
  construction: Codex writes, Claude grades.
- Foreman (LEAD) does no implementation.

## Decisions

- **D-F1 — Wave 1 runs sequentially, not in parallel.** Write sets are provably disjoint
  (03-01: `DeckFlow.Core/**` + `DeckFlow.Core.Tests/**`; 03-07: `DeckFlow.Web/Services/CutLab/**`
  + `DeckFlow.Web.Tests/**`), so the GSD wave grouping is sound. But both plans' verify commands
  are `dotnet build DeckFlow.sln -c Release` / `dotnet test DeckFlow.sln`, and two concurrent
  MSBuild runs in one worktree contend on shared `obj/`/`bin/` locks. Worktree isolation would
  fix it but costs a full restore per worker plus the known `node_modules` junction staging
  hazard in this repo. Sequential is cheaper, rides prompt-cache warmth, and loses only
  wall-clock. Journaled as a deliberate deviation from the plan-index wave grouping.
- **D-F2 — No `--connection-string` anywhere in Phase 3.** 03-02 reads the committed
  `RESEARCH-FINDINGS.json` (5.6 MB) only; the threat model states the Postgres connection string
  is never read. No operator credential is needed at any point in this phase, unlike Phase 2 wave 7.
- **D-F4 — 03-02's aggregate proof is already discharged.** Plan 03-01's acceptance criteria defer
  the binding count check to plan 03-02 Task 2 ("the real run must yield exactly 1463 adopted pairs
  across 678 commanders; 1481 would mean the `> 0` rule never ran; 1468 would mean it ran against
  the untruncated value"). The blind verifier ran `RoleFloorBaseline.Build` over the committed
  5.6 MB `RESEARCH-FINDINGS.json` and observed `SampleSize=841, adoptedPairs=1463, commanders=678`,
  roles exactly the six adopted keys. 03-02 must still reproduce this through the CLI path, but the
  Core filter itself is now proven against real data, not fixtures.
- **D-F5 — Plan 03-01 has a self-contradictory acceptance criterion (no action needed).** Task 1's
  action text mandates a `// Why:` comment stating that `Math.Round` is deliberately not used; its
  acceptance criterion says "the file contains no `Math.Round`". A literal grep can never pass both.
  The code is correct — `Math.Round` appears only inside that mandated comment at
  `RoleFloorBaseline.cs:61`. Recorded so a future audit does not read the grep miss as a defect.
- **D-F6 — One test-hardening fix wave accepted against a PASS_WITH_NOTES.** The verifier found zero
  code defects but three tests that would pass against a broken implementation (a `FromJson` test
  with no happy path, so a camelCase-policy regression ships green; a source-guard test using
  `"edhrec"`, which never pins `StringComparison.Ordinal`; a 100%-vs-90% one-sided test leaving
  `>=` vs `>` unpinned). Batched into a single test-only ticket rather than accepted, because each
  gap hides a live regression class. Production code fenced to zero diff.
- **D-F7 — The "0 warnings" baseline was wrong; the real gate is 0 errors / 9 warnings.** An
  incremental `dotnet build` reports 0 warnings from cache without recompiling `DeckFlow.Core.Tests`.
  A `--no-incremental` rebuild shows the truth: exactly 9 `CS8629` (nullable value type may be null)
  in `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`, lines 52/54/56/69/123/125/137/
  139/141. That file was last touched by `c624f1f2`, before this phase's baseline, and is untouched
  by Phase 3. Two separate Codex dispatches reported `DONE_WITH_CONCERNS` over these warnings; both
  were correct to flag them and correct to refuse to "fix" a file outside their write set. Every
  remaining wave gates on **0 errors / exactly 9 CS8629 in that one file**; a warning anywhere else
  is a defect.
- **D-F8 — Role-key normalization in `AdvisoryRoleFor` is a latent no-op (accepted, no action).**
  The verifier flagged that the new `AdvisoryRoleFor` returns `NormalizeRoleKey(...)` (lowercased)
  where the old returned the raw role string, so a non-lowercase producer would change the emitted
  group `RoleKey`. Traced it: role keys originate as lowercase constants in `CutLabRoleAssigner`
  (`RoleKeys`, `WinconsRole = "wincons"`, the display-name map keyed `["wincons"]`), and the grouping
  is `StringComparer.OrdinalIgnoreCase` on both sides. No producer emits mixed case, so the behavior
  is unchanged today. Recorded rather than fixed.
- **D-F9 — Authorized deviation from plan 03-07's specified test fixtures.** The verifier found that
  two fixtures the PLAN ITSELF specified cannot distinguish the new behavior from the old:
  `..._MultiRoleCard_IsAttributedToItsTightestRole` uses roles `["draw","engines"]` where engines is
  both the tighter role AND the earlier array entry (priority 2 vs 6), so the pre-change
  `roles.OrderBy(RolePriority).FirstOrDefault()` picks it too; the headroom-tie test has the same
  coincidence. Both pass against the old code. This is a spec defect, not an execution defect —
  Codex implemented the plan correctly. Deviation from the plan's fixture choice was pre-authorized
  in the fix ticket so Codex would not (rightly) refuse it, per this cycle's pattern of Codex
  blocking on spec defects and being right to.
- **D-F10 — Trailing-newline nit in the `--generated` regex: ACCEPTED, not fixed, and it is not ours.**
  The verifier found that .NET's `$` matches before a trailing `\n`, so `--generated $'2026-07-28\n'`
  is accepted and the raw newline is stamped into the artifact. Real, but: (a) it requires an operator
  to embed a literal newline in a CLI argument; (b) the pattern `^\d{4}-\d{2}-\d{2}$` is exactly what
  the plan told the implementer to mirror; and (c) the precedent it mirrors —
  `CedhBaselineCommandRunner.MonthLabelRegex` = `^\d{4}-\d{2}$`, shipping on `main` today — has the
  identical hole. Fixing only the new runner would diverge from the mirrored pattern while leaving the
  live instance unfixed. Logged as a cross-cutting `\z`-vs-`$` consistency nit for a future sweep
  across both runners; explicitly NOT a Phase 3 defect.
- **D-F11 — The shipped snapshot's provenance is proven, not asserted.** The verifier did not merely
  re-parse the emitted JSON; it reimplemented the adoption filter in python directly from the raw
  5.6 MB `RESEARCH-FINDINGS.json` (six adopted roles ∩ `source == postgres` ∩ `clearsBar` ∩
  `floor(p25) > 0`) and got an EXACT MATCH on the whole commanders block. Fractional spot-checks
  confirm truncation (`7.25 → 7`, `2.5 → 2`, `1.75 → 1`) and the p25∈(0,1) rows are absent. This
  closes the one question re-parsing the output can never answer: whether the shipped data actually
  came from the specified filter rather than from somewhere else.
- **D-F12 — `required` is satisfied by an explicit JSON `null`; the shipped lands provider has the
  same gap (follow-up, NOT fixed here).** The verifier probed `RoleFloorBaselineProvider` and found
  three well-formed-JSON shapes that escape the three-type catch set and NRE at the dereference:
  `{"commanders":null}`, `{"commanders":{"X":null}}`, `{"commanders":{"X":{"floors":null}}}`. C#'s
  `required` guarantees only that the JSON *mentioned* the property, not that its value is non-null —
  so no `JsonException` fires. User-visible effect is a 500 on Cut Lab requests (NOT a boot failure;
  startup was separately probed across nine malformed shapes and is safe). This violates plan 03-03's
  own binding truth, "a missing or corrupt snapshot file degrades to 'no commander data' and never
  throws (RFLR-06)", so it is fixed in the new provider via a post-deserialize SHAPE CHECK — the catch
  set stays exactly three types, because an unexpected exception must still surface.
  **`CedhLandBaselineProvider` has the identical gap** (`CedhLandBaselineSnapshot.Commanders` is
  likewise `required` non-nullable) and is deliberately NOT fixed here: it is live production code
  serving the shipped manabase feature, no Phase 3 requirement binds it, and touching it again after a
  behavior-preserving extraction would add risk without mandate. **Logged as a follow-up for a
  dedicated fix.** Note this means the two providers now differ in load robustness while remaining
  identical in key-matching semantics — D-10's "cannot drift apart" constraint is about key matching,
  which is still satisfied by the shared `CommanderBaselineKeys`.
- **D-F13 — Two trivial 03-03 notes accepted, no action.** (a) The startup warm-up sits one line below
  the position the plan named (after the cEDH warm-load log rather than immediately after the
  `EnsureLoaded()` call) — behaviorally identical. (b) Task 1's criterion "`CommanderBaselineKeys.
  Candidates` returns exactly one match" holds at commit `4881198b` but is 2 at HEAD, because Task 2
  adds the second consumer by design — a plan-internal sequencing artifact, not a defect.
- **D-F14 — Plan 03-04 Task 1 is internally impossible as written; resolved by moving the mechanical
  call-site update into Task 1.** The plan requires `ResolveDefaults` to take a NEW NON-OPTIONAL
  parameter ("leave it non-optional so no call site can silently forget it") AND requires
  `dotnet build DeckFlow.sln -c Release` to pass before the Task 1 commit. Both cannot hold: a
  non-optional parameter breaks every existing caller until later tasks update them. The plan
  compounded the error in its own acceptance criteria, asserting the build stays clean "because the
  property name is unchanged" — reasoning that covers CONSUMERS of `DefaultValue` but not CALLERS of
  `ResolveDefaults`. There are **8 call sites**: `CutLabPageService.cs:291` plus seven in
  `CutLabFloorDefaultsTests.cs` (15, 66, 89, 104, 119, 134, 168); the plan's Task 1 write set listed
  neither file.
  **Resolution (Codex's own option 1, chosen because it preserves the plan's intent):** Task 1 also
  passes a literal `null` at all eight sites. The parameter stays non-optional, so every call site
  still makes a visible conscious choice — which is exactly what the plan wanted — and every commit
  compiles. Task 2 then swaps the production `null` for the injected provider, as already planned.
  Passing `null` in the seven test call sites is semantically correct, not test-weakening: those tests
  assert the NO-COMMANDER-DATA behavior, so `null` is the right value and they must otherwise pass
  unchanged. Codex was told that a failure of their assertions is a real `max()` regression and a
  stop condition.
  **This is the 5th correct Codex block this cycle.** Every one has been a spec defect, not an
  execution failure. Treated as precedence row 1 (fault in the ticket/spec) — retry on the same seat,
  not counted against it.
- **D-F15 — Plan 03-05's CSS placement instruction was wrong; it shipped a mobile-layout defect that
  no test could catch (6th spec defect this phase).** The plan told the implementer to place the new
  column-sizing rule "OUTSIDE the existing mobile media query, so the stacked layout is unaffected".
  A rule outside a `max-width` query applies at ALL widths — the opposite of what the plan claimed.
  Confirmed independently, not merely accepted from the verifier: the role-floors table carries BOTH
  `data-prompt-cedh-reference-table` and `data-cut-lab-role-floors-table` (`CutLab.cshtml:779`), so
  inside `@media (max-width: 600px)` (opens `site-common.css:1044`) the generic stacked rule at
  `site-common.css:1116-1124` applies to it: `display: grid; grid-template-columns: 6.5rem 1fr`. That
  rule has higher specificity but declares NO `width`, so the new unmediated `width: 6rem` wins
  uncontested — and 6rem is narrower than the 6.5rem label track alone, collapsing the value track.
  Fix: wrap in `@media (min-width: 601px)` and rewrite the comment, which asserted the opposite of
  what the selector did.
  **Note the verifier's cited line number was for a different table's rule and I initially doubted the
  finding — the diagnosis was right and the doubt was wrong.** The table carrying two attributes is
  what makes the shared rule apply. Worth remembering: check attribute sets before dismissing a
  cross-table CSS interaction.
  **2,138 Web tests pass with this defect present.** xUnit cannot see a collapsed grid track; only
  reading the cascade found it. This is the argument for the Task 4 human-verify gate existing at all.
- **D-F3 — Codex consent.** Standing per project CLAUDE.md (Codex codes, Claude reviews) and
  re-confirmed by the user this session via the model-defaults question.

## Tasks

| ID | Lifecycle | Owned paths | Job |
|---|---|---|---|
| 03-01 | **VERIFIED** (+ hardening `be547f76`) | `DeckFlow.Core/Research/RoleFloorBaseline.cs`, `DeckFlow.Core/Research/RoleFloorBaselineDriftCheck.cs`, `DeckFlow.Core.Tests/RoleFloorBaselineTests.cs`, `DeckFlow.Core.Tests/RoleFloorBaselineDriftCheckTests.cs`, `03-01-SUMMARY.md` | — |
| 03-07 | **VERIFIED** (+ hardening `3bca68ac`) | `DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs`, `DeckFlow.Web.Tests/CutLabCutRoundEngineTests.cs`, `DeckFlow.Web.Tests/CutLabUiPatchBuilderTests.cs`, `03-07-SUMMARY.md` | — |
| 03-02 | **VERIFIED** | per plan frontmatter + **one carve-out**: the single `Content Update` entry in `DeckFlow.Web/DeckFlow.Web.csproj` | `bfq1cohjp` |
| 03-03 | **VERIFIED** (+ fail-open fix `ac7c667d`) | per plan frontmatter — ⚠ touched LIVE `CedhLandBaselineProvider`; extraction was 2 ins / 17 del, all 22 pre-existing manabase tests passed unedited | `bc1xqb2i2` |
| 03-04 | **VERIFIED** | per plan frontmatter + carve-out: `REQUIREMENTS.md` RFLR-05 text; r2 deviation added the 8 `null` call sites | `bjva8ncln` → `bho5c6uh7` |
| 03-05 | **Tasks 1-3 VERIFIED** (+ CSS fix `af8c7c6c`) — **Task 4 AWAITING DEVELOPER** | per plan frontmatter — **Task 4 is a blocking human-verify checkpoint, fenced OUT of the Codex ticket**; no server/browser/Playwright permitted | `bbzyaj70v` |
| 03-06 | PENDING | per plan frontmatter | — |

## Attempts

| Task | # | Seat + effort | Ticket rev | Outcome | Checks run | Evidence | When |
|---|---|---|---|---|---|---|---|
| 03-01 | 1 | Codex gpt-5.4 medium | ticket-03-01.txt r1 | `DONE` | foreman gates: build 0W/0E; Core 2007 pass/0 fail; fence grep clean; 0 CR bytes ×4 files; no `{ get; }` | `.foreman/scratch/out-03-01.log`; commits `08627b15`, `241e4f73`, `c90e44ea`, `671a0ed7` | 2026-07-28 21:50 |
| 03-01 | verify | Claude `foreman-verifier` (opus, blind) | — | `PASS_WITH_NOTES` | independently reran build + filtered tests (19/19); reconstructed inverted `Build` in scratch to prove ordering test is a real discriminator; ran `Build` over the real 5.6 MB artifact | agent `ad428da85a854ceac`; **1463 adopted pairs / 678 commanders** — 03-02's aggregate proof discharged early | 2026-07-28 21:56 |
| 03-01-fix | 1 | Codex gpt-5.4 medium | ticket-03-01-fix.txt r1 | `DONE_WITH_CONCERNS` → concern resolved | build 0E / 9 pre-existing CS8629; filtered tests 19 → 23; prod code zero diff | `.foreman/scratch/out-03-01-fix.log`; commit `be547f76` | 2026-07-28 22:01 |
| 03-01-fix | verify | Claude `foreman-verifier` (sonnet, blind) | — | `PASS` | reran build + filtered tests (23/23); graded each new test for real discrimination against its target regression | agent `acf1fd105a23e1dfb`; 86 insertions / 0 deletions, additive only | 2026-07-28 22:04 |
| 03-07 | 1 | Codex gpt-5.4 medium | ticket-03-07.txt r1 | `DONE_WITH_CONCERNS` → concern resolved (the 9 warnings are pre-existing) | foreman gates: fence held on DTO/patch-builder/controller/csproj; 0 CR bytes ×3; `--stat` == `--ignore-all-space --stat`; `LockedOvershootRoleOrder` retained | `.foreman/scratch/out-03-07.log`; commits `6503320e`, `edec873d`, `6ccda1b7`, `de2e3d6e`; full suite 4541 pass / 0 fail / 20 skip | 2026-07-28 22:13 |
| 03-07 | verify | Claude `foreman-verifier` (opus, blind) | — | `PASS_WITH_NOTES` | independent `--no-incremental` build (9 CS8629, sanctioned file only); Web 2104/16/2120; traced the null-data degradation path; graded Task 3 patch-order test | agent `aba69c02fa364b8bc`; 3 findings — 2 non-discriminating tests, 1 false summary line | 2026-07-28 22:18 |
| 03-07-fix | 1 | Codex gpt-5.4 medium | ticket-03-07-fix.txt r1 | `DONE` | `--no-incremental` build 0E / 9W sanctioned; Web 2104/16/2120; prod zero diff | `.foreman/scratch/out-03-07-fix.log`; commit `3bca68ac` | 2026-07-28 22:26 |
| 03-07-fix | verify | Claude `foreman-verifier` (sonnet, blind) | — | `PASS` | recomputed every fixture's headroom by hand and derived both old- and new-code orderings; confirmed they differ | agent `a3c3fd97d231cf252`; F1 wincons=100/lands=0 → old picks wincons, new picks lands; F2 old `[payoffs,engines,lands]` vs new `[lands,payoffs,engines]` | 2026-07-28 22:30 |
| 03-02 | 1 | Codex gpt-5.4 medium | ticket-03-02.txt r1 | `DONE` | foreman gates: csproj diff = 3 lines, 0 `PackageReference`; snapshot independently re-parsed (678/1463/1463/841, six roles only, all floors ≥ 1); EOL preserved per file; build 0E/9W; **full suite 4541 pass / 0 fail** | `.foreman/scratch/out-03-02.log`; commits `1aa35830`, `53514620`, `96c7cb95`; snapshot 51,694 B copies to `bin/Release/net10.0/` | 2026-07-28 22:39 |
| 03-02 | verify | Claude `foreman-verifier` (opus, blind) | — | `PASS_WITH_NOTES` | **reimplemented the adoption filter in python from the raw 5.6 MB artifact — recomputed commanders block is an EXACT MATCH to the shipped file**; probed the bootstrap path 4 ways (truncated / `{}` / `null` / 0-byte) — all exit 1, none wrote; exercised all 9 exit codes live; regenerated into scratch → `cmp`-identical | agent `aca28e91e17cd063d`; fractional spot-checks `Abdel Adrian/draw` 7.25→7, `Admiral Beckett Brass/interaction-targeted` 2.5→2, `Alesha/ramp` 1.75→1; p25∈(0,1) rows (`Electro/engines` 0.5, `Faldorn/engines` 0.5, `Taii Wakeen/engines` 0.75) all absent | 2026-07-28 22:47 |
| 03-03 | 1 | Codex gpt-5.4 medium | ticket-03-03.txt r1 | `DONE` | foreman gates: no pre-existing test edited; `CedhLandBaselineProvider` 2 ins / 17 del; catch filter byte-identical to the lands template; EOL preserved per file; **full suite 4555 pass / 0 fail** | `.foreman/scratch/out-03-03.log`; commits `4881198b`, `3e66bd3d`, `048a8893` | 2026-07-28 22:58 |
| 03-03 | verify | Claude `foreman-verifier` (opus, blind) | — | `PASS_WITH_NOTES` | **built a scratch probe against the real `DeckFlow.Core.dll` and enumerated 9 malformed-input shapes against `EnsureLoaded()`** — all land inside the 3-type catch set, so startup is provably safe; confirmed the extraction is byte-identical incl. iterator laziness and partner-key order | agent `a7bb4434b639b8794`; 1 real finding (explicit-`null` NRE → D-F12), 3 trivial | 2026-07-28 23:05 |
| 03-03-fix | 1 | Codex gpt-5.4 medium | ticket-03-03-fix.txt r1 | `DONE` | build 0E / 9W; Web 2118 → 2121; lands provider zero diff; catch set unchanged | `.foreman/scratch/out-03-03-fix.log`; commit `ac7c667d` | 2026-07-28 23:11 |
| 03-03-fix | verify | Claude `foreman-verifier` (sonnet, blind) | — | `PASS` | traced control flow to prove the reject path falls through to the shared `_cache.Set` (no early return); confirmed the per-entry null cases `continue` rather than abort the candidate loop | agent `a9b5af0b981c9cebc`; Web 2121 / 16 / 2137 | 2026-07-28 23:15 |
| 03-04 | 1 | Codex gpt-5.4 medium | ticket-03-04.txt r1 | `BLOCKED` — **correct block, spec defect (precedence row 1: ticket/spec fault, retry same seat, does NOT count against the seat)** | build failed `CS7036` at `CutLabPageService.cs:291` after Task 1 alone | `.foreman/scratch/out-03-04.log`; nothing committed; 2 files left modified → **reverted to `ac7c667d`, clean tree** before retry | 2026-07-28 23:18 |
| 03-04 | 2 | Codex gpt-5.4 medium | ticket-03-04.txt **r2** (amended: Task 1 also passes `null` at all 8 call sites) | `DONE` | foreman gates: REQUIREMENTS.md diff = RFLR-05 line only; `Math.Max` count 1; `24 - rampDefault` survives; **`AssertFloor` helper strengthened (+2 asserts, 0 removed)**; EOL clean ×4; **full suite 4567 pass / 0 fail** | `.foreman/scratch/out-03-04-r2.log`; commits `ad8ea33f`, `2c60de9d`, `35da959a`; Web 2121 → 2130 | 2026-07-28 23:29 |
| 03-04 | verify | Claude `foreman-verifier` (opus, blind) | — | `PASS_WITH_NOTES` — **zero defects** | compared **all 21** migrated `AssertFloor` calls against `ac7c667d` — every one preserved its original expected values with `bracketValue` == old `defaultValue`, `commanderValue: null`; confirmed the below-bracket fixture is genuinely below (payoffs band 6 vs commander 2) | agent `a5b1bf213b1d9a77e`; out-of-scope gate pinned behaviorally — fake stocked with `lands=40/interaction-mass=9/protection=8` (values that WOULD win) and asserted never queried | 2026-07-28 23:38 |

## Scratch

- `.foreman/scratch/` — Codex stdout captures, one file per dispatch.

## Task 4 — blocking human-verify checkpoint (03-05)

Status: **AWAITING DEVELOPER**. Tasks 1-3 are committed, gated, and blind-verified; `03-05-SUMMARY.md`
is deliberately NOT written yet — the plan writes it only after visual approval.

No server, browser, Playwright run, or gstack daemon has been started by any agent in this phase.
Standing project rule: ask the developer before opening a browser for verification.

What the code review could NOT establish, and therefore needs eyes:
1. **≤600px stacked layout** — the highest-risk area. `af8c7c6c` fixed a real collapse defect here
   (D-F15); the fix is correct by cascade analysis but has never been rendered.
2. **A commander-loses row** — commander < bracket must still show the commander NUMBER, not an em
   dash. Pinned by test, but never seen.
3. **`n/a` vs `—` on one screen** — the two empty states must read as different things to a human.
4. **The `title` tooltip on the Source cell** — a new hover surface that has never been rendered.
5. **Desktop crowding** — two 6rem columns were added to a table that was four columns wide; the
   Floor input may be squeezed at narrow desktop/tablet widths.

Breakpoint note (accepted, not fixed): `min-width: 601px` is the first of its kind in
`site-common.css`, which has five `max-width: 600px` blocks. At fractional widths (600 < w < 601,
reachable via browser zoom or fractional DPI) neither query matches, so the table renders as a normal
auto-width six-column table. Benign — unsized, not broken.

## Post-phase render check — 03-06 feasibility banner (2026-07-29)

The phase verifier flagged that the 03-06 banner had never been rendered by anyone: it landed AFTER
03-05's human-verify gate and carried none of its own — the same exposure class that produced the
D-F15 CSS collapse. Checked on developer instruction.

Method: headless server + headless Playwright, no browser window on the host. Forced infeasibility by
raising the `payoffs` floor to 40 via the UI and resubmitting.

Result: **renders correctly, copy is honest, arithmetic independently confirmed.**

- Feasible state (shipped defaults): banner count **0** — absence, not an empty banner.
- Infeasible state: `These floors need at least 81 nonland slots, but only 65 remain after 34 lands
  and the commander. Relax Targeted removal (raised by 3), Payoffs, Card draw first. This is a
  conservative estimate — roles overlap, every engine is also a draw spell and win conditions usually
  double as another role, so the real requirement is at least this large and may be larger.`
- Capacity checks out: `100 - 1 - 34 lands = 65`.
- Required checks out: `ramp 10 + max(draw 14, engines 6) + int-targeted 10 + int-mass 3 +
  protection 4 + payoffs 40 = 81`. Lands correctly excluded from the required sum.
- Relax ordering correct: *Targeted removal* leads on commander-raise (+3), then the zero-raise roles
  fall back to floor size (payoffs 40 before draw 14).
- Themes: Classic (light) amber-on-cream and Nyx (dark) light-on-deep-amber, both with the accent bar;
  contrast holds. Mobile 390px wraps cleanly with no overflow.

Screenshots (untracked): `.foreman/scratch/banner-shots/`.
Temp spec deleted; tree clean at `469dc9cf`.

**Both of the phase verifier's unrendered-UI exposures are now closed.** The one remaining open item
is the AJAX `floorByRole` gap (WARNING, pre-existing, no RFLR binds it) — surfaced to the developer
and NOT fixed, because fixing it expands scope beyond the authorized phase.
