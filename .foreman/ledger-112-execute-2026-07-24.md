# Foreman Ledger — Phase 112 Execute (Cycle 17 Code Port)

**Run start:** 2026-07-24
**Mode:** Codex-boosted (Agent + real shell + consented Codex gpt-5.4 medium, ChatGPT-sub login)
**Baseline commit:** 1f1a33185038a95123df858da7972ff61fd7727e
**Branch:** feat/personal-tools (branching_strategy=none; no new branch)
**Port source (READ-ONLY):** plan/cycle-17-creator-style @ 6da5eb420b6403b68804bdbd3f2e51d7213ab33c
**Roles:** Codex codes each wave (gpt-5.4 @ medium, user confirmed defaults this session); LEAD (Opus 5) verifies (EOL + scope fence + independent build/test + blind foreman-verifier). Cross-family verify default.
**User decision:** run all 6 waves end-to-end; stop only on a failed gate.

## Config facts
- `workflow.use_worktrees=false` → sequential on the main working tree, no worktree isolation.
- `workflow.cross_ai_execution=true`, `cross_ai_command` = `codex exec -m gpt-5.4 -c model_reasoning_effort="medium" -s danger-full-access -c approval_policy="never" --color never -`
- dotnet = `"/mnt/c/Program Files/dotnet/dotnet.exe"`; do NOT set `MTG_DATA_DIR` (pollutes ContentKbArtifactPathResolverTests).

## Dependency graph (strictly linear — no parallelism possible)
- Wave 1 = Plan 112-01 (baseline capture + drift preflight, doc-only) — depends_on: []
- Wave 2 = Plan 112-02 (Core engine + Core tests via path-allowlist checkout; 3 LLM-distillation M-hunks) — depends_on: [112-01]
- Wave 3 = Plan 112-03 (6 remaining Core M-hunks; Core gates; **Commit 1**) — depends_on: [112-02]
- Wave 4 = Plan 112-04 (Web services, Scryfall helpers, seed loader, Web tests, 4 Web M-hunks + archidekt pipeline) — depends_on: [112-03]
- Wave 5 = Plan 112-05 (AddDeckFlowCreatorStyle extension + 2 Program.cs edits) — depends_on: [112-04]
- Wave 6 = Plan 112-06 (whole-graph DI-resolution test incl. real ArchidektOwnerClient; **Commit 2**) — depends_on: [112-05]

D-07: the six plans collapse into exactly TWO production commits (Commit 1 at 112-03, Commit 2 at 112-06). Doc/tracking commits are separate and do not count.

## Task rows
| ID | Plan | Wave | Write set | Status |
|----|------|------|-----------|--------|
| T1 | 112-01 | 1 | .planning/phases/112-cycle-17-code-port/112-BASELINE.md, 112-01-SUMMARY.md | PENDING |
| T2 | 112-02 | 2 | DeckFlow.Core/{Content,Knowledge,Integration}/** (allowlist), DeckFlow.Core.Tests/** (allowlist) | PENDING |
| T3 | 112-03 | 3 | 6 Core M-files (ContentKbPaths, AssemblyInfo, ContentTagVocabulary, CommanderInference, CategoryKnowledgeRepository, CardCategoryRepository) | PENDING |
| T4 | 112-04 | 4 | DeckFlow.Web/Services/{CreatorStyle,Scryfall,Content}/**, DeckFlow.Web.Tests/**, content-kb/seed/*.json, 5 Web M-files | PENDING |
| T5 | 112-05 | 5 | DeckFlow.Web/Extensions/CreatorStyleServiceCollectionExtensions.cs, DeckFlow.Web/Program.cs | PENDING |
| T6 | 112-06 | 6 | DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStyleDiRegistrationTests.cs | PENDING |

## Standing gates applied to EVERY wave (LEAD-side, post-dispatch)
1. **EOL churn:** `git diff --stat` vs `git diff --ignore-all-space --stat`; per touched file compare `grep -c $'\r'` against `git show HEAD:<path>`. Repo is `.gitattributes` LF-enforced but verify per file.
2. **Scope fence:** `git diff --name-status main` must list only that plan's declared paths (plus `.planning/`).
3. **Contamination grep:** none of D-12's never-port strings arrived (D-08/D-12/D-18).
4. **Independent gates:** LEAD re-runs `dotnet build DeckFlow.sln` and the plan's test filter itself — never trusts the worker's transcript.
5. **Blind foreman-verifier** on the diff with the original plan text, no worker narration.

## Attempts (append-only)

### T1 / Plan 112-01 — Wave 1
- Attempt 1: Codex gpt-5.4 medium (bg b5vwtclao, ~113k tok) → STATUS **DONE_WITH_CONCERNS**.
- **Recorded baseline (authoritative for plans 03 + 06 deltas):**
  - Build @ 1f1a3318: **0 errors, 9 warnings**, all `CS8629`, all in `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`.
  - `DeckFlow.Core.Tests`: Failed 0 / Passed **1613** / Skipped 0 / Total 1613 (1m51s).
  - `DeckFlow.Web.Tests`: Failed 0 / Passed **2013** / Skipped 16 / Total **2029** (2m28s).
  - Zero pre-existing failures in either suite.
  - RESEARCH's static-attribute proxy (1334/1576) was an UNDERCOUNT — superseded by these runner numbers.
- Drift preflight: **VERDICT: GO — manifest matches HEAD**.
- LEAD verify (independent re-run, not narrative grading):
  - Commit scope OK — 76fa3fd4 touches exactly 2 files (112-BASELINE.md 125 lines, 112-01-SUMMARY.md 79 lines), 204 insertions, 0 deletions. Zero production files.
  - EOL OK — both files CR count 0 (LF).
  - Check A re-run: `plan/cycle-17-creator-style` = 6da5eb42…, `5709f37c` resolves. ✓
  - Check B re-run: extracted 64 + 38 = **102** paths from the two plans' `<port_allowlist>` blocks, `git cat-file -e` each on the port branch → **0 missing**. ✓
  - Check C re-run: all 15 M-file targets show **0 commits** in `8599cd3b..HEAD`; `CreateManabaseBaselineConnection`, `ExtractCombinedAsync`, `CombinedExtractionResult` each grep=1 on HEAD. ✓
  - Check D re-run: `CreatorStyleProfileSeedRelativePath` grep=0 in ContentKbPaths.cs; both seed JSONs absent on HEAD. ✓
  - Independent `dotnet build DeckFlow.sln`: **0 Error(s) / 9 Warning(s)** — matches the recorded baseline exactly. ✓
- Blind foreman-verifier: **waived** — doc-only artifact, no logic; superseded by LEAD's full independent re-execution of every check (stronger evidence than a narrative second read).
- **CARRY-FORWARD (defect in plans 03 + 06 acceptance text):** the criterion `git diff --name-status main lists only <allowlist> paths` is UNSATISFIABLE — `feat/personal-tools` already carries unrelated commits ahead of `main`. Scope fences for plans 03/06 MUST be evaluated against the phase baseline commit **`1f1a3318`**, not `main`. Codex raised this correctly; it is a plan-text defect, not a work defect.
- Status: **DONE**, committed 76fa3fd4.

### T2 / Plan 112-02 — Wave 2
- Attempt 1: Codex gpt-5.4 medium (bg bvkmmo84q, ~111k tok) → STATUS **DONE_WITH_CONCERNS** (concern = expected intermediate build break, see below).
- LEAD verify (independent):
  - Scope OK — 4 tracked modifications (3 M-files + Core.Tests.csproj), all new files under DeckFlow.Core/ or DeckFlow.Core.Tests/. Zero entries under DeckFlow.Web/, DeckFlow.Web.Tests/, DeckFlow.CLI/, content-kb/ (the lone `?? DeckFlow.Web/wwwroot/js/` entry is pre-existing gitignored TS output).
  - All 64 allowlist paths present. ✓  ProfileFusionEngineTests.cs present (not confused with the deferred FuseProfileRunnerTests). ✓
  - Never-port: all 9 checked paths either absent or pre-existing-on-HEAD-and-untouched (FeatureFlagCatalog.cs / DeckPageTab.cs are Cycle-18/19 files already on main, `git status` empty for both — NOT newly ported). ✓
  - Additive-only: deletions=0 on all three M-files AND the csproj. `ExtractCombinedAsync` and `CombinedExtractionResult` both survive. ✓
  - Zero PackageReference added; Testcontainers.PostgreSql absent from Core.Tests.csproj. ✓
  - Postgres trims: both `*Postgres` classes gone (grep=0), both plain classes remain (grep=1), zero `PostgresContainerFixture` references anywhere in the test project. ✓
  - EOL: zero CR in all 68 in-scope files incl. the .txt fixture; `git diff --stat` == `git diff --ignore-all-space --stat` (189 insertions both) → **zero EOL churn**. ✓
  - Fixture wiring: csproj `<TargetPath>Fixtures/salubrious-snail-transcript.txt</TargetPath>` matches consumer `Path.Combine(AppContext.BaseDirectory,"Fixtures","salubrious-snail-transcript.txt")` at CliLlmDistillationStatedRulesGoldenTests.cs:120-123, and matches the pre-existing index-seed.golden.json precedent. ✓
  - Independent build: exactly **1 error, 0 warnings** — `StapleStripper.cs(109,64) CS0117 'ContentTagVocabulary' does not contain 'Staples'`. This is the DESIGNED intermediate state: ContentTagVocabulary.cs is hunked by plan 112-03 (its key_links declare pattern `ContentTagVocabulary\.Staples`). Not a defect.
- Blind foreman-verifier (opus, fresh context, original plan text only): **PASS_WITH_NOTES** — A–K all PASS, incl. F (the 4 new interface signatures byte-match the branch AND match StatedRulesExtractor.cs call sites at lines 47/50/53/60, CancellationToken last) and G (all 4 defaults are `Task.FromException<T>(new NotSupportedException(...))`, mirroring main's existing ClassifyAsync pattern).
- Status: **DONE** (source uncommitted by design — plan 03 owns Commit 1). Summary-only commit faf24e8f.

### F-1 — RATIFIED PLAN DEFECT found by blind verifier, closed by user decision
- **Gap:** `DeckFlow.Core/Integration/CliLlmDistillationService.cs` is the ONLY production implementer of the four new stated-rules interface methods, and it is named NOWHERE in Phase 112 planning — absent from the 64-path allowlist, the 15-row M-file hunk inventory, and the never-port list.
- **Evidence:** HEAD copy = 468 lines, implements 0 of 4. Port-branch copy = 518 lines, implements 4 of 4 (branch lines 123/140/157/177). The ported, allowlisted `CliLlmDistillationStatedRulesGoldenTests.cs:47` constructs `new StatedRulesExtractor(service, grounder)` over a REAL `CliLlmDistillationService`.
- **Failure mode:** green build, red test — the C# default interface members throw `NotSupportedException`, so a non-overriding implementer inherits a throwing method invisibly to the compiler. Would have detonated at plan 112-03's Core test gate.
- **Corroboration:** the newly added `SanitizeStatedRules` / `SelectPayload` / `RulesPayload` members currently have ZERO production callers — on the branch, CliLlmDistillationService was their sole consumer.
- **Scope bounded:** the branch's other implementer, `LlmDistillationService.cs`, deliberately does NOT implement the four (grep=0) — it correctly relies on the throwing defaults. The gap is exactly ONE file.
- **USER DECISION:** add `CliLlmDistillationService.cs` as a **16th M-file hunk** (additive, ~50 lines, 4 method impls) folded into plan 112-03. Rejected alternatives: dropping the golden test to Phase 115, and halting for a full replan.
- Consequence: plan 112-03's declared M-file count rises from 10 to **11**.

### T3 / Plan 112-03 — Wave 3 (owns Commit 1)
- Attempt 1: Codex gpt-5.4 medium (bg bx2zmey2v) → STATUS **DONE**. All 11 additive counts 0; build 0 err / 9 warn (== baseline, all CS8629); Core tests **1798 passed / 0 failed** (baseline 1613 → +185 ported tests); golden test PASS; format gate passed (fixed IDE0161 + whitespace inside the commit). Commit 1 = `3d502852` (75 files, 7606 insertions), summary `1ada6058`.
- LEAD verify: additive-only 11/11 = 0 vs baseline ✓; LlmDistillationService.cs untouched ✓; 0 PackageReference ✓; EOL churn ZERO (7606 insertions under both `--stat` and `--ignore-all-space --stat`, no CR in any committed Core file) ✓.
- **LEAD verify FAILED on content fidelity — see F-2. Commit 1 is NOT accepted as-is.**

### F-2 — SECOND manifest gap + worker invention (LEAD-caught, gates were all green)
- **Trigger:** line-count arithmetic. Pre-port `CliLlmDistillationService.cs` = 468 lines, port branch = 518 (delta +50), but the worktree came out at **646** (delta +178). A 3.5x overshoot on a "port the 4 methods" task.
- **Root cause:** the branch's four methods delegate to `DistillationSchemas.StatedRules{Select,Disambiguate,Decompose,Reduce}{SystemPrompt,Schema}`. `DeckFlow.Core/Knowledge/DistillationSchemas.cs` is an M-file (154 lines on HEAD, 222 on branch) that is named **NOWHERE in Phase 112 planning** — a second manifest gap of the same class as F-1.
- **What the worker did instead:** lacking the schemas file, Codex hand-authored local prompt text + JSON-schema consts inside `CliLlmDistillationService.cs`, plus a private `FormatAllowlist` helper. `FormatAllowlist` **already exists on HEAD's DistillationSchemas.cs** (grep=7) — the worker duplicated an existing helper into the wrong file.
- **Why every gate missed it:** `CliLlmDistillationStatedRulesGoldenTests` stubs the CLI command via `CliCommandEnvironmentKey`, so it never exercises real prompt text. Build green, 1798/1798 green, golden test green — and the shipped prompts are invented. **Textbook false-green.** In an LLM pipeline the prompt IS the behavior.
- **Compounding:** the branch's prompts are COMPOSED from ported vocabulary (`$"...{FormatAllowlist(StatedRulesMetricVocabulary.Metrics)}..."`), so a hand-written substitute cannot be accidentally equivalent. Architectural drift too: prompts inlined in the service instead of the shared schema holder, which would collide at Phase 113/115.
- **Third gap found:** `DeckFlow.Core.Tests/DistillationPromptRegressionTests.cs` (M-file on HEAD; branch version carries **9** stated-rules prompt assertions) is also absent from both allowlists — i.e. the manifest omitted the one guard whose job is to catch exactly this defect.
- **Dependency pre-check (LEAD, before dispatching the fix):** `ExtractWithRetryAsync` (6), `BuildInstruction` (6), `JsonOpts` (2) all already on HEAD-before → the branch's 4 methods compile unchanged. `FormatAllowlist` already on HEAD's DistillationSchemas (7). `StatedRulesMetricVocabulary.Metrics/Comparators` already ported in Wave 2. **Fix is self-contained — no fourth gap.**
- **USER DECISIONS:** (1) port `DistillationPromptRegressionTests.cs` as the 18th M-file so prompt fidelity is provable in CI; (2) **amend** Commit 1 rather than stacking a fix commit, preserving D-07's two-commit structure and keeping invented prompt text out of permanent history (safe — nothing pushed).
- Consequence: Commit 1's declared M-file count rises from 11 to **13** (`DistillationSchemas.cs` + `DistillationPromptRegressionTests.cs`).
- **PROCESS CARRY-FORWARD for Waves 4-6:** add a standing LEAD gate — for every M-file hunk, compare the worktree delta against the branch delta (`branch_lines - head_lines`). A materially larger delta means the worker authored code instead of porting it. Cheap, and it is what caught F-2 when all functional gates passed.
- Attempt 2 (fix): Codex gpt-5.4 medium (bg b3l3b7ls6) → STATUS **DONE_WITH_CONCERNS**. F-2 itself was fixed CORRECTLY:
  - All 9 invented members gone from CliLlmDistillationService.cs (grep=0 each); file 646 → **543** lines.
  - The 8 real members now live in DistillationSchemas.cs (lines 59/68/77/100 schemas, 183/193/203/223 prompts); `FormatAllowlist` definition count in Core production code = **1**.
  - Prompt literals proven **byte-identical** to the branch (`diff -u` empty, exit 0).
  - 13/13 M-files additive-only vs 1f1a3318 (all 0), incl. the new DistillationSchemas.cs and DistillationPromptRegressionTests.cs.
  - Commit 1 amended 3d502852 → `f6897efb` (77 files); docs 1ada6058 → `d20255bd`.
  - BUT it amended with a **BROKEN BUILD**: 2 × CS1519.

### F-3 — VERIFY-THEN-MUTATE ordering bug (LEAD-diagnosed; the gate did not bind the artifact)
- **Symptom:** `dotnet build DeckFlow.sln` = **2 errors**:
  - `DeckFlow.Core.Tests/CreatorDeckCacheStoreTests.cs(10,5): CS1519`
  - `DeckFlow.Core.Tests/CreatorProfileSourceStoreTests.cs(9,5): CS1519`
- **Damage:** a stray `{` immediately after the class opening brace, with the first field over-indented 8 spaces. Brace counts 25/24 and 22/21 (one unmatched open each). Shape is the signature of a half-applied IDE0161 block-namespace → file-scoped conversion.
- **Attribution — NOT the fix run.** `git diff 3d502852 f6897efb -- <both files>` is EMPTY. The identical damage is already present in `3d502852`, i.e. Wave 3 **attempt 1** committed it.
- **Root cause:** attempt 1 ran `dotnet build` (0 err) and the Core suite (1798 passed) FIRST, then ran `scripts/format-check-changed.sh staged`, whose auto-fix rewrote the STAGED blobs (it self-reported "flagged IDE0161 then whitespace in CreatorDeckCacheStoreTests.cs and CreatorProfileSourceStoreTests.cs"), then committed WITHOUT re-building. **The green numbers described code that was never committed.**
- **Why LEAD's attempt-1 verification missed it:** I re-ran additive-only/EOL/scope checks against the commit, but reused the worker's build+test numbers instead of re-running them post-commit. Same false-green family as F-2 — a gate whose result does not bind the shipped artifact.
- **PROCESS RULES ADDED (binding for Waves 4-6):**
  1. **Gate order is FORMAT → BUILD → TEST → COMMIT.** Never run an auto-fixing formatter after the verification gates. If the formatter changes anything, re-run build and tests before committing.
  2. **LEAD re-runs `dotnet build` against the COMMITTED tree** (not the worker's transcript) before accepting any wave. Cheap (~11s incremental) and catches exactly this.
  3. Keep the branch-delta arithmetic gate from F-2.
- Attempt 3 (repair): Codex gpt-5.4 medium (bg bybi1u6q5) → STATUS **DONE**. Removed the stray `{` from each file and de-indented the first field; braces now balanced 24/24 and 21/21. Format gate ran FIRST and modified nothing. Post-commit rebuild confirmed. Commit 1 re-amended f6897efb → **`4866fb64`** (77 files); docs → `d6c4c4c4`.

### T3 FINAL — LEAD VERIFICATION (Wave 3 ACCEPTED)
- Working tree: no tracked production modifications (only the pre-existing `.planning/ROADMAP.md` + `STATE.md` edits that predate this session).
- **13/13 M-files additive-only** vs 1f1a3318 (zero deletions each). ✓
- `LlmDistillationService.cs` byte-identical to baseline. ✓
- **EOL churn ZERO** — 77 files / 7732 insertions identical under `--stat` and `--ignore-all-space --stat`; no CR in any committed Core file. ✓
- Zero PackageReference added. ✓
- Scope fence vs baseline: only `.planning/` + `DeckFlow.Core*` paths — Core-only, as designed. ✓
- **Prompt fidelity, independently checked:** all 8 ported members (4 schemas + 4 system prompts) md5-identical to the port branch. ✓
- **Regression test NOT weakened:** `diff` of every `StatedRules` assertion block against the branch version is IDENTICAL; all 9 assertions present. ✓
- **Branch-delta gate (F-2 rule) tripped, then cleared by the decisive check:** CliLlmDistillationService worktree_delta=75 vs branch_delta=50; DistillationSchemas worktree_delta=123 vs branch_delta=68. Resolved by testing line provenance: of 41 and 75 distinct ADDED lines, **0 do not exist on the port branch**. The gap is explained by main-only content present on HEAD but absent from the branch (11 and 20 distinct lines), which makes `branch_delta` understate the true new-member block. Benign — no invention.
  - **Gate refinement for future waves:** the correct predicate is not `worktree_delta ≈ branch_delta` but *"every added line exists on the port branch"* (`comm -23 added branch` = 0). The delta arithmetic stays useful as a cheap trigger, not as the verdict.
- **LEAD independent gates against the COMMITTED tree (rule 2):**
  - `dotnet build DeckFlow.sln` → **Build succeeded, 0 errors.** (Reported 0 warnings because the build was incremental and recompiled nothing; the authoritative full-build figure remains the baseline 9 × CS8629.)
  - `dotnet test DeckFlow.Core.Tests` → **`Passed!  - Failed: 0, Passed: 1799, Skipped: 0, Total: 1799, Duration: 1 m 50 s`** — baseline was 1613, so **+186** ported tests, zero failures, zero regressions.
- Status: **DONE / ACCEPTED.** Commit 1 = `4866fb64`. Core half of the port is complete and green.

## REBASE onto origin/main (2026-07-24, user-authorized)
- **Precondition found:** `feat/personal-tools` WAS already published at `origin/feat/personal-tools` = `76fa3fd4`, so 13 of our 16 commits were on the remote. Rebasing rewrites them → force-push required, which CLAUDE.md normally prohibits. **User explicitly authorized rebase + `--force-with-lease` for this branch.**
- **Pre-flight (all clean):** zero overlapping paths between main's 37 changed files and our 96. main's 13 new commits are all Cut Lab / classifier / docs (`fcc0a1bd` widened IsRampCard, `413f178a` shared you-anchored draw regex, `8ef63337` golden rebaseline).
- **Procedure:** pre-rebase safety SHA `d6c4c4c4` recorded → `git stash push` the 2 pre-existing dirty planning files (ROADMAP.md cosmetic reflow + STATE.md) → `git rebase origin/main` → `git stash pop`.
- **Result:** 16/16 replayed, **zero conflicts**, 13 behind → **0 behind**. Commit 1 `4866fb64` → **`81ab74d3`**.
- **Post-rebase gates (LEAD, full run — the point was main's Core classifier changes):**
  - `dotnet build DeckFlow.sln` → **0 errors / 9 warnings** (CS8629 baseline set intact on full rebuild).
  - Core: **`Failed: 0, Passed: 1814`** (1799 → +15 from main's own new Core tests).
  - Web: **`Failed: 0, Passed: 2018, Skipped: 16`**.
  - No semantic breakage from `413f178a` / `fcc0a1bd` despite zero path overlap.
- **Pushed:** `git push --force-with-lease` → `76fa3fd4...814b6066` forced update, lease honored, remote == local.

### F-4 — CONCURRENT-SESSION COLLISION (shared checkout)
- Commit **`814b6066`** `chore(planning): archive shipped-cycle docs, consolidate orphan audits` appeared at **22:08:51**, authored by `luntc1972`, **between my rebase and my push** — from a DIFFERENT session working in this same checkout. No `codex exec` of mine was alive (only long-lived MCP servers).
- It was included in my force-push **before I had reviewed it**. Reviewed after the fact: `.planning/` ONLY, 41 files, predominantly pure renames (`| 0` diffs), plus deletion of a duplicate `.planning/v1.5-MILESTONE-AUDIT.md` (99 lines). **Zero production files.** Benign, but it was published unreviewed.
- **LIVE HAZARD:** the other session's HEAD now points at rewritten SHAs. If it commits or pushes against that stale view it will either resurrect pre-rebase commits or hit a lease rejection. Confirm that session is closed before further work lands in this checkout.
- Reinforces the existing `feedback_shared_worktree_collision` rule: **re-read HEAD immediately before any git mutation, and again immediately before pushing** — not just at the start of the operation.

## PAUSED BY USER after Wave 3 (2026-07-24)
Plans 112-01, 112-02, 112-03 accepted. Waves 4-6 NOT started.

**Pending on resume:**
1. GSD tracking update for plans 01-03 (`roadmap.update-plan-progress`) — deliberately NOT applied, because `.planning/ROADMAP.md` and `.planning/STATE.md` already carry uncommitted edits that predate this session and must not be blended with automated writes. Reconcile those first.
2. Wave 4 = plan 112-04 (Web services, 4 Scryfall helpers, seed loader, Web tests, 5 Web M-hunks + the D-17 `archidekt` Polly pipeline).
3. Waves 5-6 then follow; 112-06 owns **Commit 2**.

**Binding process rules carried into Waves 4-6 (all three earned in Wave 3):**
1. **FORMAT → BUILD → TEST → COMMIT.** Never run an auto-fixing formatter after the verification gates. This matters far more in Wave 5, which edits `Program.cs`.
2. **LEAD re-runs build (and the relevant suite) against the COMMITTED tree** before accepting any wave. ~11s incremental.
3. **Line-provenance check on every M-file hunk:** every added line must exist on the port branch. Branch-delta arithmetic is the cheap trigger; `comm -23` is the verdict.
4. **Watch for further manifest gaps.** Two were found in Core (F-1, F-2), both because RESEARCH inventoried callers but not the transitive closure of callees. Wave 4's `ArchidektOwnerClient` / `CardGroundingGuard` / `CreatorStylePacketService` have the same dependency shape — expect at least one more missing M-file.
