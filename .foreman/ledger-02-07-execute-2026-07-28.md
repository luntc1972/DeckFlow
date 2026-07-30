# Foreman Ledger — Cycle 21 Phase 2 Wave 5 (plan 02-07)

- **Run:** 2026-07-28 execute
- **Worktree:** /mnt/c/users/chrislunt/source/personal/deckflow-role-floors
- **Branch:** gsd/cycle21-cut-lab
- **Baseline commit:** a38c01335729208ccb94340f15cb6acd736bf1d9
- **Baseline tree:** clean except permanent-untracked set (`.foreman/`, `_role-floor-research/`, `_edhrec-brackets/`, sibling `02-0N-PLAN.md`)
- **Mode:** Codex-boosted (Agent tool + real shell + consented Codex)
- **Seats:** Codex `gpt-5.4` @ `model_reasoning_effort=medium`, `-s danger-full-access`, `approval_policy=never` (user-confirmed 2026-07-28)
- **Plan:** `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/02-07-PLAN.md`
- **Dispatch order:** SEQUENTIAL. Tasks 1, 2, 4 all write `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs`; Task 3 shares the git index. No parallel wave.

## Plan-level notes carried into every ticket

- Runner is **LF** (`grep -c $'\r'` = 0) at baseline — preserve.
- Scope fence, forbidden commands (`git stash|checkout|reset|rebase|push`), and the "do NOT set MTG_DATA_DIR" / "do NOT run against a live database" rules are pasted verbatim into each ticket.
- **Plan inconsistency flagged by foreman before dispatch:** Task 4's `<files>` lists only `CategoryCacheSchemaParityTests.cs`, but its `<action>` also requires a "Known gaps" line in `BuildMarkdownReport` via Task 2's notice block. Task 4's acceptance criteria do NOT forbid touching the runner (only `CardCategoryRepository.cs` / `CategoryKnowledgeRepository.cs`), and the plan frontmatter's `files_modified` includes the runner. **Resolution: Task 4 MAY edit `RoleFloorResearchCommandRunner.cs` for that one Known-gaps line and nothing else.** Recorded so it is not later read as scope creep.

## Task rows

| # | Task | Seat | Write set | Status |
|---|---|---|---|---|
| 1 | Lands calibration verdict vs the 2026-07-16 prior + ramp no-prior block | Codex gpt-5.4/med | `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs` | PENDING |
| 2 | Named Phase-3 roles, null-result section, protection + corpus-hygiene + casual-bias disclosures | Codex gpt-5.4/med | `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs` | PENDING |
| 3 | Pre-write `NO-GO-TEMPLATE.md` | Codex gpt-5.4/med | `.planning/.../NO-GO-TEMPLATE.md` | PENDING |
| 4 | Verify `boardFilter: "mainboard"` excludes sideboard/maybeboard | Codex gpt-5.4/med | `DeckFlow.Core.Tests/CategoryCacheSchemaParityTests.cs` (+ 1 Known-gaps line in the runner) | PENDING |
| V | Blind verification of the wave | `foreman-verifier` (Claude, fresh context) | read-only | PENDING |

## Attempts (append-only)

### Dispatch A — Task 1 (attempt 1)
- **Seat:** Codex gpt-5.4 @ medium, danger-full-access, approval_policy=never
- **Dispatched:** 2026-07-28, background job `bn78jsajm`
- **Write set:** `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs`
- **Foreman ground truth captured BEFORE dispatch returned** (so the diff is graded against sources, not against the worker's narration):
  - Archive `2026-07-16-edhrec-bracket-land-data.md:21` — bracket means `36.3 / 35.7 / 35.2 / 34.5 / 30.0`, overall `34.9`
  - Archive `:22` — bracket SDs `2.24* / 1.19 / 1.25 / 1.20 / 2.14`, overall `1.45`. The `*` on 2.24 IS the B1 outlier caveat the plan requires carried into the comment.
  - `DeckFlow.Web/Data/manabase-baseline/latest.json` → `brackets`: `2->35.9 (124221)`, `3->35.5 (140632)`, `4->34.5 (72399)`, `5->30.5 (4761)`. **No bracket-1 entry.** `generatedUtc` `2026-07-17T21:38:00Z`, `schemaVersion` 1, `source` `edhrec-pilot-aggregate`.
  - Conclusion: the plan's own summary tables match both sources value-for-value. No plan-level transcription defect. Any divergence in the commit is the worker's.
- **Status:** DISPATCHED
- **Returned:** `DONE_WITH_CONCERNS`, commit `52f18df5`, +359/-0, single file.
- **Foreman grading of the diff (not the narrative):**
  - Constants match foreman's independently-captured ground truth value-for-value. B1 outlier caveat IS carried into the comment (`:49`). PASS.
  - `LiveBaselineLandBracketMeans` has no B1 entry; B1 renders `n/a (no B1 row in the snapshot)`. PASS.
  - `ClassifyLandsCalibration`: `no-go->reproduces`, `signal-present->insufficient data`, `go` gated on `ClearingCommanderCount >= BreadthMinimum` BY NAME else `insufficient data`, unknown status THROWS. Stricter than required. PASS (D-06 satisfied).
  - Emission order: `AppendLandsCalibrationControl` called at `:1301`, `## Go/No-Go` at `:1303`. Correct. NOTE: raw grep line numbers are MISLEADING here (method body sits at `:1462`, after the call site) — verified by call order, not by grep position.
  - Verdict line emitted at `:1473` before any branch => unconditional. PASS.
  - Agreement spread loop correctly EXCLUDES B1-no-live-row and NOT REPORTED fresh cells via `values.Count < 2` guard. PASS.
  - EOL: `--stat` == `--ignore-all-space --stat` (359 both) => zero churn. `grep -c $'\r'` = 0. PASS.
  - Scope gate: only the runner changed; nothing outside the permanent-untracked set. PASS.
  - Build 0 errors / 9 warnings (9 is the known pre-existing CS8629 count from wave 4). Core 1736/0/0, Web 2095/0/16. PASS.
- **FOREMAN-FOUND DEFECTS (not self-reported by the worker):**
  - **F1 (medium):** `BuildThreeReferenceAgreement` computes `priorAverageDistance` over all 5 prior brackets but `liveAverageDistance` over 4 (no B1). B1=36.3 is the farthest outlier, so including it for the prior and not for the live systematically biases the emitted "sits closest on average to X" away from the prior. The per-bracket spread loop guards this correctly; the distance loop does not. Asymmetric comparison sets.
  - **F2 (low-medium):** the Postgres column prints the same corpus-wide `lands` mean/SD/P25 in all five bracket rows. Values are correct but the shape implies bracket resolution the Postgres corpus does not have (per D-08 it has no bracket at all).
- **Status:** ACCEPTED WITH FIX PENDING -> dispatching amendment before Task 2 (same file, must land first).

### Dispatch A-fix — Task 1 defects F1/F2 (attempt 1)
- **Returned:** `DONE`, commit `916e3b9c` `fix(02-07): compare the three land reference sets over a common bracket set`, +75/-12, single file.
- **Graded:** F1 fixed via a `sharedBracketMeans` intersection requiring live AND fresh present; all three averages now over the same brackets; `// Why:` records the outlier-bias reason. Empty-intersection path returns `ClosestReferenceSet = null` + empty `ComparisonBrackets` and emits "No bracket carries all three reference sets, so no closest-set statement can be made" — no partial fallback. F2 fixed: Postgres removed from the per-bracket table body, restated once beneath it labelled "not bracket-resolved; the Postgres corpus has no bracket dimension". `MaximumSpread`/`DivergentBrackets`/`ClassifyLandsCalibration` untouched as instructed.
- `ComparisonBrackets` declared `{ get; init; }` — respects the repo's System.Text.Json get-only carve-out.
- EOL clean (stat == ignore-all-space stat), scope clean, build 0 errors / 9 pre-existing warnings, Core 1736/0/0, Web 2095/0/16, format-check 0.
- **Status:** TASK 1 COMPLETE (`52f18df5` + `916e3b9c`).

### Ground truth captured for Task 2 (BEFORE dispatch)
- `DeckStatClassifier.cs:226` signature; needles at `:227-231` verbatim: `gains hexproof`, `gains indestructible`, `gain protection from`, `phases out`.
- Three consumers CONFIRMED to exist: `InteractionAuditAggregator.cs:58`, `CutLabRoleAssigner.cs:165`, `PlanRoleClassifier.cs:236`. Plan's claim is accurate.
- Lightning Greaves inferred-not-measured wording is at `01.1-02-DELTA.md:47`: "used the plan-provided oracle text and a reasoned `Artifact — Equipment` type line because no local facts entry for that card was present in the measured corpus files used here". Its `:37` line records `IsProtectionCard == false` / `PlanRole.None`.

### Dispatch B — Task 2 (attempt 1)
- **Seat:** Codex gpt-5.4 @ medium. **Write set:** `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs`.
- **Status:** DISPATCHED
- **Returned:** `DONE`, commit `a6924006`, +320/-0, single file.
- **Graded:**
  - Needles at `:98-101` match `DeckStatClassifier.cs:227-231` against foreman's independently captured ground truth. PASS.
  - **Unconditional emission PROVEN structurally:** the Go/No-Go loop iterates the static `TargetRoles` array (not the result set), and `protection` is a literal member of it, so the in-full notice cannot be skipped by any status/threshold/empty-result path. The only guard is `role == "protection"`. Known-gaps placement uses a pointer (plan permits). PASS.
  - Five cards defined ONCE at `:105-109` and that single table feeds both the markdown notice and the JSON `knownMissedCards` — grades cannot drift between the two representations. Exactly one `"inferred"`, on Lightning Greaves, wording taken from `01.1-02-DELTA.md:47`. PASS.
  - `LOWER BOUND` x3, `PROVISIONAL` x3. Phase-5 arithmetic present in BOTH prose (`:1987`) and JSON (`:1569`). Corpus-hygiene figures with n=300 stated. PASS.
  - `casualBiasObjection.thisRunSays` is COMPUTED, not asserted: per-role `Avg_commander(max(0, mean - P25)) / PopStdDev_commander(mean)`, guarded for <2 commanders and zero between-commander SD, with an explicit "cannot yet quantify" path. Contains no rebuttal; adds an explicit statement of what the run cannot say. PASS (D-07).
  - `recencyWindow: null`, `blockedBy: "Phase 01.2"`, `rolesInScopeForPhase3`, `signalPresentRoles` all present. Null-result block emits on `rolesInScopeForPhase3.Count == 0` and points at the template. PASS.
  - EOL clean, scope clean, build 0 errors / 9 pre-existing warnings, Core 1736/0/0, Web 2095/0/16, format-check 0.
- **QUALITY NOTE (non-blocking, batched to a wave-end simplify pass):** `BuildCasualBiasThisRunSays` duplicates the same ~12-line ratio computation across two LINQ passes (`perRoleRatios` and `comparableRatios`). Correct but repeated; CLAUDE.md's post-implementation checklist wants it extracted.
- **Status:** TASK 2 COMPLETE.

### Dispatch C — Task 3 (attempt 1)
- **Seat:** Codex gpt-5.4 @ medium. **Write set:** `.planning/.../NO-GO-TEMPLATE.md` (new, LF).
- **Status:** DISPATCHED
- **Returned:** `DONE`, commit `3aeea130`, +61/-0, `NO-GO-TEMPLATE.md` only, no `.cs`.
- **Graded:** 61 lines (<120). Ten sections present and in order, verdict at section 2 before any methodology. ROADMAP Phase 3 dependency line quoted BYTE-ACCURATE against `ROADMAP.md:304` (independently checked by foreman). Section 10 lists FIVE named failure modes incl. all four required. Section 7 links the protection disclosure rather than duplicating it. LF (`grep -c $'\r'` = 0). Scope clean.
- **DEFECT (to fix in cleanup):** line 37 embeds a machine-specific ABSOLUTE path `/mnt/c/users/chrislunt/source/personal/deckflow-role-floors/...` in a markdown link. This repo is PUBLIC and the path breaks in any other checkout. Must become repo-relative. Also cites `01.1-02-DELTA.md:45` where the Lightning Greaves note is at `:47` (`:45` is likely the Measurement-notes heading) — verify while fixing.
- **Status:** TASK 3 COMPLETE (cleanup pending).

### Ground truth captured for Task 4 (BEFORE dispatch)
- Board column: `card_category_observations.board`, `TEXT NOT NULL DEFAULT 'mainboard'` (`CategoryCacheSchema.cs:156`, also `:173` on `card_deck_totals`).
- Filter clause: `AND o.board = @board`; parameter is `NormalizeBoard(boardFilter)` = `Trim().ToLowerInvariant()`, defaulting to `mainboard` on null/whitespace. So the SQL LOOKS correct — expect the verification to PASS.
- Membership query is `SELECT DISTINCT o.category AS Category, o.card_name AS CardName, q.id AS DeckId`, joined `sources` -> `deck_queue`, requiring `q.processed = 1`.
- **TRAP not named in the plan:** results are post-filtered by `FilterGenericMembershipWithFallback`, which drops a card's generic categories when a more specific one is present. A fixture seeded with generic categories would return FEWER rows than seeded, which would misread as a broken board filter. Ticket warns the worker explicitly.

### Dispatch D — Task 4 (attempt 1)
- **Seat:** Codex gpt-5.4 @ medium. **Write set:** `DeckFlow.Core.Tests/CategoryCacheSchemaParityTests.cs` + the one Known-gaps line in the runner (authorized above).
- **Status:** DISPATCHED
- **Returned:** `DONE_WITH_CONCERNS`, commit `8d77d2dd`, +62/-0 across the two authorized files.
- **Worker's concern was a FALSE ALARM — foreman reproduced and refuted it.** Codex reported `DeckFlow.Web.Tests` not green, naming `DeckAnalysisPacketServiceTests.BuildAsync_IsByteIdentical_WhenCommandZoneAwarenessFlagOff(targetAiPlatform: "ChatGPT")` as failing plus a stalled test host. Task 4 touched only a test file and ONE Known-gaps line in the CLI runner — neither can reach DeckFlow.Web packet building. Foreman found 6 orphaned `dotnet.exe` processes (same stale-testhost signature as the prior wave) and then RE-RAN rather than assuming:
  - Targeted filter on the named test: **3 passed, 0 failed** (all platform variants).
  - Full `DeckFlow.Web.Tests`: **2095 passed, 0 failed, 16 skipped** — exactly the established baseline.
  - Full `DeckFlow.Core.Tests`: **1737 passed, 0 failed** (1736 + the one new test).
  Conclusion: concurrent-testhost contention in the worker's environment, NOT a regression. No code defect. Nothing was killed or cleaned to achieve this.
- **Graded:** test file is pure addition (0 removed lines, no existing member touched); runner diff is exactly 1 added line; `CardCategoryRepository.cs` NOT in the diff; EOL clean both files (stat == ignore-all-space stat); format-check 0.
- **VERIFICATION RESULT (the task's actual deliverable): `boardFilter: "mainboard"` WORKS.** Seeded 220 rows across `mainboard`/`sideboard`/`maybeboard`; unfiltered returned 220 AND contained both named non-mainboard cards; filtered returned exactly 100 and contained neither. Two-sided as required, plus a stronger-than-specified `Contains` on the unfiltered side. Categories `RampPlan`/`InteractionPlan`/`ValueEngine` chosen to survive `FilterGenericMembershipWithFallback` — the trap foreman flagged pre-dispatch was avoided.
- **Status:** TASK 4 COMPLETE. Phase 2 Postgres role counts are NOT inflated by sideboard/maybeboard cards.

### Dispatch E — cleanup (batched, per CLAUDE.md post-implementation checklist)
- Two items: (1) NO-GO-TEMPLATE.md line 37 absolute machine path -> repo-relative (PUBLIC repo); (2) `BuildCasualBiasThisRunSays` duplicate ratio computation -> extract.
- **Status:** DISPATCHED

### Blind verification — `foreman-verifier` (agent ad841df0c2b31fca3)
- **Verdict:** `PASS_WITH_NOTES`. All 9 plan `<verification>` items PASS. Needles proven BYTE-IDENTICAL by programmatic extraction (not eyeballed). Constants independently re-derived from archive + latest.json. Scope gate clean. HEAD unchanged, tree clean after the pass.
- Six findings; disposition:
  1. MEDIUM — `NO-GO-TEMPLATE.md:21` listed `board-wipes`/`recursion`/`tutors` (stale pre-Phase-1 taxonomy), omitted `engines`/`payoffs`/`wincons`. CONFIRMED by foreman against `TargetRoles:135-146`. FIXED `09f552c3`.
  2. MEDIUM — `02-07-SUMMARY.md` missing (plan `<output>` requires it). FIXED `089f808e`, 249 lines.
  3. LOW/MED — boardFilter test seeded 220 one-card decks, not one >200-card deck; the `must_haves` truth names "a deck carrying more than 200 total cards across all boards", which is the D-08 hazard shape. The acceptance criterion (">200 rows for one commander") passed and MASKED the stricter truth. FIXED `0d322354`: single deck, 100 mainboard + 60 sideboard + 60 maybeboard; unfiltered 220, filtered 100, named non-mainboard cards absent. Filter HOLDS on the hazard shape too.
  4. LOW — table has 3 source-labelled column groups, plan's Task 1 criterion says 4. **REJECTED by foreman — authorized deviation.** The 3-column shape was a REVIEWER-DIRECTED change (F2 above); restoring the 4th column would reinstate a repeated corpus-wide Postgres figure down five bracket rows, implying a bracket resolution the Postgres corpus does not have per D-08. The plan's criterion conflicts with its own D-08; D-08 wins. Recorded in `02-07-SUMMARY.md` as an authorized deviation rather than silently satisfying the criterion's letter.
  5. LOW — `AppendBlock` emitted `\r\r\n` on Windows: blocks built with `StringBuilder.AppendLine` (`\r\n`), then split on `'\n'`, leaving a trailing `\r` per line before `AppendLine` added another. `BuildProtectionUnderDetectionPointer`'s `.Split('\n')[0]` also stranded a CR mid-sentence. CONFIRMED by foreman by reading both methods. FIXED `6b82fead` via `ReplaceLineEndings("\n")` + trailing-empty-element handling, with TWO reflection-based regression tests in `RoleFloorTaxonomyGuardTests.cs` asserting no `\r\r` AND exact expected output. Fence held: no `InternalsVisibleTo` added, no production restructuring.
  6. INFO — 11 commits, not the 4 `<success_criteria>` states. All four required subjects present; extras are review-driven fixes. Enumerated in the SUMMARY.
- Verifier's "not checked": runtime behaviour of emitted markdown/JSON (plan FORBIDS a live run — that is plan 02-08), and `ClassifyLandsCalibration` verified by code-read plus the worker's throwaway exercise rather than by a committed unit test.

### WAVE 5 CLOSED
- 11 commits `a38c0133..089f808e`. All four plan tasks complete + 2 foreman-found defects + 4 verifier findings + cleanup.
- **Substantive result:** `boardFilter: "mainboard"` VERIFIED on both the row-level and single-oversized-deck shapes. Phase 2's Postgres role counts are NOT inflated by sideboard/maybeboard cards.
- User confirmed push of committed work as of 2026-07-28 10:30 MDT (through `09f552c3`; `6b82fead`/`0d322354`/`089f808e` landed after and are NOT yet pushed).

### Wave 6 = plan 02-09 (NOT 02-08)
- `02-08` is **wave 7**, `autonomous: false`, `depends_on` includes `02-09`. It needs an OPERATOR-supplied `DECKFLOW_ROLE_FLOOR_CONNECTION_STRING`, a Render IP allowlist check, and TWO blocking developer checkpoints. Not dispatchable by an agent.
- `02-09` is **wave 6**, `autonomous: true`, `user_setup: []`, depends on 02-05 + 02-06 (both done). 3 sequential tasks, Task 1 is RED-then-GREEN.
- **Preconditions verified by foreman:** `edhrec.csv` 618,464,510 B (header `commander,card,count`) and `averages.csv` (header carries `commander,commander2,...,number_decks`) exist ONLY in the MAIN worktree `/mnt/c/users/chrislunt/source/personal/deckflow/artifacts/edhrec/`, NOT in this one. `_role-floor-research/cards_full.json` present here (8.2 MB). The `commander2` column is why D-03's SOLO-row denominator rule matters.

### WAVE 5 FINAL VERIFICATION (foreman-run, serial)
- `dotnet build DeckFlow.sln`: **0 errors, 0 warnings**
- `DeckFlow.Core.Tests`: **1740 passed, 0 failed, 0 skipped**
- `DeckFlow.Web.Tests`: **2095 passed, 0 failed, 16 skipped**
- Wave 5 CLOSED at `089f808e`. Commits `6b82fead`, `0d322354`, `089f808e` are NOT yet pushed (user pushed through `09f552c3`).

---

# WAVE 6 — plan 02-09 (EDHREC bulk card-counts arm)

- **Baseline:** `089f808e`. Seat: Codex gpt-5.4 @ medium. 3 sequential tasks; Task 1 is `tdd="true"` (RED then GREEN).
- **Dispatch F — Task 1: DISPATCHED** (job `bs6w0kx45`). Write set: `DeckFlow.Core/Research/EdhrecCardCountsReader.cs` + `DeckFlow.Core.Tests/EdhrecCardCountsReaderTests.cs`, both new/LF.

### Ground truth captured BEFORE dispatch — corpus counts INDEPENDENTLY CONFIRMED
Verified with a real CSV parser (python `csv` module), reading the archives in the MAIN worktree:

| Figure | Plan claims | Foreman measured | Match |
|---|--:|--:|---|
| `averages.csv` data rows | 6,585 | 6,585 | YES |
| solo rows (`commander2` empty) | 3,372 | 3,372 | YES |
| partner-pair rows | 3,213 | 3,213 | YES |
| `edhrec.csv` rows | ~14.15M | 14,150,219 | YES |
| distinct commanders | 3,378 | 3,378 | YES |
| distinct cards | 31,788 | 31,788 | YES |

**Every number in plan 02-09's D-03 and D-05 is accurate. No plan defect.**

### METHODOLOGY WARNING — foreman reproduced the exact bug D-05 warns about
A first pass using `awk -F','` (naive comma split) returned **solo: 938** (true: 3,372) and **distinct commanders: 2,875** (true: 3,378). Commander/card names containing commas (`Adrix and Nev, Twincasters`) are silently split across fields and land in the wrong bucket — no error, just a plausible-looking wrong count. Had that pass been trusted, a FALSE finding would have been opened against a correct plan.
Carry into grading: if Codex's reader uses `Split(',')` anywhere, its counts will look reasonable and be wrong. The quoted comma-bearing card-name test (D-05, required) is the gate that catches it. **`EdhrecAveragesConverter.ParseCsvLine` (`:110`, quote-aware, handles doubled quotes) is the reference implementation, but that file is OUT of 02-09's write set — the new reader must MIRROR it, not edit it for visibility.**

### Preconditions re-confirmed
- Archives exist ONLY in MAIN worktree `/mnt/c/users/chrislunt/source/personal/deckflow/artifacts/edhrec/`; `artifacts/edhrec/` is absent from this worktree, so the reader MUST take paths as parameters and hardcode neither.
- `_role-floor-research/cards_full.json` (8.2 MB) present here and shared with the role-floor harness — must not be deleted or truncated.

### Wave 6 dispatches and grading

**Dispatch F — Task 1** -> `DONE`, `9be93f82` `feat(02-09): stream EDHREC card counts with a denominator gate`.
- RED was a genuine compile failure on missing production types; GREEN 7/7. Two-pass `StreamReader.ReadLine()` streaming confirmed at `:106/:111`, `:142/:146`, `:196/:212`. Plan's own greps pass: forbidden-reference grep returns NO match; `grep -c 'get; }'` = 0. Quoted comma-bearing names tested with `"Adrix and Nev, Twincasters"` / `"Fire // Ice"`. `artifacts/` untouched, no 618 MB copy.
- **FOREMAN-FOUND DEFECT:** `DenominatorMismatches` was `IReadOnlyList<string>` — each entry a formatted string. It satisfied the criterion's letter ("with all four values") but broke D-03's OTHER requirement, "report the worst five ratios", since ranking prose needs a re-parse. Same flattening anti-pattern `02-07` D-04 forbade for `knownMissedCards`; here the plan did not name the shape, so it recurred. FIXED `4cc6abdf`: structured `EdhrecDenominatorMismatch` record, ordered DESCENDING by `Ratio` at construction so Task 3's "worst five" is `Take(5)`. Exclusion logic unchanged — still `continue` with no clamping.
- **FOREMAN ERROR (mine):** my ticket invented the commit subject instead of quoting the plan's Task 1 criterion (`feat(02-09): add streaming EDHREC bulk card-counts reader with denominator gate`). Codex correctly followed my instruction over the plan. Content correct; subject differs. NOT amended — rewriting history over wording is disproportionate. Record in `02-09-SUMMARY.md`.

**Dispatch G — Task 2** -> `DONE_WITH_CONCERNS`, `df94707e` `feat(02-09): add EdhrecBulk source tag and expected-role-count figure type`.
- `EdhrecBulk = 3` added as a THIRD tag (D-01). `EdhrecBulkRoleExpectation` carries exactly the plan's fields; `EdhrecBulkColumns` has `Source` first and no distribution column. Both required xmldocs present, including the criterion-7 warning to future contributors.
- `MaxCardInclusion` naming is the PLAN'S OWN instruction (Task 2 A.2: `MaxCardRatio` would trip the naive `Ratio` substring guard, so rename and explain in xmldoc — "prefer renaming the property over loosening the assertion"). NOT name-laundering by the worker; foreman verified the xmldoc explains it.
- Test method renamed `RoleFloorSource_HasExactlyTwoExplicitNonZeroMembers` -> `...HasExactlyThree...` alongside the 2->3 assertion (foreman flagged this trap pre-dispatch; a renamed-not assertion would have left a method name asserting the opposite of its body). `:60`'s `Assert.Equal(3, properties.Length)` (the `IRoleFloorFigure` property count, unrelated, coincidental 3) UNTOUCHED — diff has no hunk near it.
- Worker's "concern": `grep -c 'get; }'` on `RoleFloorFigure.cs` = 3, not 0. **Worker is RIGHT and foreman was WRONG** — those are the `IRoleFloorFigure` INTERFACE declarations at `:38/:43/:48`, which are correct C#; every record property is `{ get; init; }`. The "expect 0" was FOREMAN'S paraphrase; the plan's grep-0 criterion belongs to Task 1's `EdhrecCardCountsReader.cs`.
- **FOREMAN PRACTICE CORRECTION:** twice now (subject, grep-0) a paraphrased criterion in a ticket diverged from the plan. Quote plan criteria VERBATIM in tickets.

**Dispatch H — Task 3** -> `DONE`, `0d95f6de` `feat(02-09): add edhrec-role-grid expected-role-count command`. +790 new runner, +23 `Program.cs`, pure addition.
- `RoleFloorGuards.TryReadShippedRoleKeys` CONFIRMED pre-existing at `:20` in `df94707e` — reused, not added; fence held. Taxonomy validated before any file read, fail-closed. Classification call shape identical to the other two arms. Zero-survivor path returns 2 before any write.
- **FENCE VERIFIED INDEPENDENTLY BY FOREMAN, not taken on report:** `cards_full.json` still 8,220,503 bytes / 14,167 entries with its original Jul 26 18:07 mtime; both archives at original sizes and Jul 24 mtimes; NO `EDHREC-ROLE-GRID.*` artifact exists -> no live run, no Scryfall traffic. (31,788 distinct cards vs 14,167 cached = 17,621 misses ~= 1 hour of throttled API traffic avoided.)
- **FOREMAN-FOUND DEFECT:** the `--dry-run` branch returned 0 at `:113`, BEFORE the `File.Exists` checks at `:116`/`:122`, so a dry run could not detect a missing/mistyped input. Observed live: it printed `C:\mnt\c\users\...` (a WSL path handed to Windows `dotnet.exe`, nonexistent) and still reported `taxonomy: OK` + exit 0. This also contradicts the command's own documented contract (exit 1 = "missing input"). Matters because `--dry-run` is the sanctioned PRE-FLIGHT for plan `02-08`'s OPERATOR run. FIXED `bc7c8b39`: existence checks moved before the dry-run return; dry run now prints each input with found/size and exits 1 on a missing CSV; a missing cards-cache is non-fatal ("will be created"). Demonstrated with three dry runs (bad edhrec -> 1, bad averages -> 1, correct Windows paths -> 0 with sizes 618,464,510 / 791,987 matching foreman's independent measurements).

### Wave 6 outstanding
- `02-09-SUMMARY.md` NOT yet written — plan `<output>` requires it with an enumerated content list.
- `<success_criteria>` expects THREE commits, one per task; actual is FIVE (two review-driven fixes). Record as deviation in the SUMMARY.

### Wave 6 blind verification — `foreman-verifier` (fresh agent aedb727487bf213a3)
- **Verdict:** `PASS_WITH_NOTES`. **All 11 `must_haves.truths` PASS.** Fresh agent used deliberately (different plan = different spec; reusing the wave-5 agent would have been stale context, not economy).
- Verifier independently re-measured the fence and matched foreman exactly: `cards_full.json` 8,220,503 B / 14,167 entries (checked twice, before and after its own commands); `edhrec.csv` 618,464,510 B; `averages.csv` 791,987 B; `git -C <main> status --porcelain -- artifacts/` empty; no `EDHREC-ROLE-GRID.*` -> no live run. Ran `--dry-run` only.
- Confirmed the plan's own D-05 grep on `EdhrecCardCountsReader.cs` returns NO match, and that `RoleFloorResearchCommandRunner.cs` is absent from the wave diff (D-07 holds).
- **Eight findings, all minor. Disposition:**
  1. Malformed-row LINE NUMBERS computed (`:141/:144`) then discarded; `out int lineFieldCount` dropped at `:151/:251`. Plan Task 1 `<behavior>` requires the tally "with its line number". Over 14,150,219 rows a bare count is uninvestigable. FIXED `4e16d647`: `EdhrecMalformedRow` (`LineNumber`, `FieldCount`, truncated `RawLineExcerpt`), retained-list cap `MalformedRowDetailsCap = 50` + `MalformedRowDetailsOmittedCount`, excerpt capped 256 chars so a pathological archive cannot produce an unbounded collection. Test pins the fixture's malformed row at line 3, field count 2.
  2. Six commits not three + Task 1 subject differs. Already known — FOREMAN's paraphrase error. No action; recorded in `02-09-SUMMARY.md`.
  3. `RoleFloorFigureTests.cs` has two changed lines (assertion + method rename). The rename was FOREMAN-AUTHORIZED pre-dispatch. Verifier confirmed no assertion weakened and one ADDED; the `IRoleFloorFigure` and `EdhrecRolePointEstimate` assertions are byte-identical. No action.
  4. **FOURTH defective grep criterion of this phase.** `grep -c 'CutLabRoleAssigner.AssignRoles'` = 1 passes only because the second call site (`EdhrecRoleGridCommandRunner.cs:259-260`, the D-06 taxonomy probe) happens to WRAP across lines. The second call is legitimate; the CODE is correct and the CRITERION is defective — it measured formatting, not structure. No code change. **Phase pattern now confirmed 4x: never assert a substring count as a proxy for a structural property.**
  5. Quote-aware parsing proven only through `ReadDistinctCardNames` (the CARD column). The COMMANDER column is the JOIN KEY between the two CSVs, and a slip there does not throw — the commander silently fails to match a denominator and is indistinguishable from one legitimately lacking a solo row. (Foreman's own `awk` blunder was this exact failure at 938-vs-3,372 scale.) FIXED `60de274d`: tests drive `"Adrix and Nev, Twincasters"` through BOTH `ReadSoloDenominators` (denominator 120 retained) and `Accumulate` (joins, NOT in `MissingDenominators`, `RowsConsumed = 2`, `TotalInclusionRate = 0.75`).
  6. `ParseCsvLine` cannot span records — a quoted embedded newline would score two malformed rows. Matches `EdhrecAveragesConverter.cs:112-113`'s own stated assumption. DOCUMENTED `a490e8af` + parser `// Why:`; multi-line CSV support deliberately NOT implemented.
  7. WSL `/mnt/c/...` paths resolve to `C:\mnt\c\...` under the Windows `dotnet.exe` and fail pre-flight. DOCUMENTED `a490e8af` as an OPERATOR NOTE for plan 02-08, whose pre-flight this dry run is.
  8. `TotalInclusionRate` reached JSON but not markdown, though D-03 requires it reported per commander. FIXED `60de274d` via a `## Commander totals` section; `EdhrecBulkColumns` left unchanged so the Task 2 assertion was neither touched nor weakened.
- Verifier's "not checked": Task 2's transient negative verification (unreproducible after the fact, but coverage proven by construction — `RoleFloorFigureTests.cs:196-213` reflects every public static column list); the real-archive run path (forbidden by the plan, code-read only); whether TDD RED-then-GREEN order was genuinely followed.
- Worker concern on the fix batch: the parser `// Why:` landed in commit 1 rather than commit 3. Immaterial — final code and docs correct.

### WAVE 6 CLOSED
- Final foreman-run serial verification: build **0 errors / 0 warnings**, Core **1752 passed / 0 failed**, Web **2095 passed / 0 failed / 16 skipped**. Fence intact: `cards_full.json` 8,220,503 B unchanged, no `EDHREC-ROLE-GRID.*` artifacts.
- Wave 6 = 9 commits `089f808e..a490e8af`. Plan 02-09 COMPLETE.
- **Session total: 25 commits `a38c0133..a490e8af`** (wave 5 = 11, wave 6 = 9, plus the wave-5 verifier fixes counted within). Core 1736 -> 1752 (+16 tests), Web 2095 steady.

### PUSH STATE (IMPORTANT)
User pushed through `09f552c3` at 10:30 MDT. **Everything after it is LOCAL ONLY:**
`6b82fead`, `0d322354`, `089f808e`, `9be93f82`, `4cc6abdf`, `df94707e`, `0d95f6de`, `bc7c8b39`, `639fb09a`, `4e16d647`, `60de274d`, `a490e8af` — 12 unpushed commits.

### PHASE 2 REMAINING = WAVE 7 = plan 02-08 (OPERATOR-GATED, cannot be agent-dispatched)
`autonomous: false`. Requires ALL of:
1. `DECKFLOW_ROLE_FLOOR_CONNECTION_STRING` exported by the OPERATOR in their own shell. No agent may read, echo, obtain or store it; since plan 02-04 D-07 it never appears on a command line either (the harness reads the env var directly, so it stays out of the process list).
2. This machine's public IP in Render Postgres Access Control as `<ip>/32` — otherwise the connection fails with a bare EOF, not a clear auth error.
3. TWO blocking developer checkpoints: the operator dispositions the go/no-go AND the lands calibration verdict before anything is committed.
4. A deliberate `--min-decks 999999` smoke run proving exit code 2 AND no findings artifact written (ROADMAP criterion 3) — required because no unit test can pin the guard's PLACEMENT inside `RunAsync`.
5. Plan 02-09's `edhrec-role-grid` arm run or DECLINED at the SAME checkpoint; a decline must be RECORDED, not left silent.

**Pre-flight now available (built this session):** `edhrec-role-grid --dry-run` validates input paths and the taxonomy without reading the archive or calling Scryfall. **Operator note: pass WINDOWS paths** (`C:\users\chrislunt\source\personal\deckflow\artifacts\edhrec\...`); WSL `/mnt/c/...` resolves to `C:\mnt\c\...` and exits 1.
**Cost warning for the live run:** 31,788 distinct cards vs a 14,167-entry cache = ~17,621 Scryfall misses, roughly an hour of throttled traffic.
