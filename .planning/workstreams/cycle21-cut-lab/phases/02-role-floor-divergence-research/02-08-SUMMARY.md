# Plan 02-08 Summary — Run it for real, behind an operator checkpoint

**Wave 7 of Phase 2. Executed 2026-07-28. Closing output for the phase.**

---

## Developer disposition (Task 4 checkpoint, 2026-07-28)

**GO on six roles. LANDS IS PULLED.**

| Verdict | Roles |
|---|---|
| **In scope for Phase 3** | ramp · draw · interaction-targeted · engines · payoffs · wincons |
| **PULLED by developer decision** | lands |
| Signal present, insufficient breadth | interaction-mass · protection |

The harness computed `lands` as a GO with 411 clearing commanders. The developer pulled it at this
checkpoint on measured evidence, described below. **The generated artifact was NOT hand-edited** —
`RESEARCH-FINDINGS.md` and `.json` are committed exactly as the harness emitted them, and still record
`lands` in `rolesInScopeForPhase3`. This document is the authority on the disposition; the artifact is
the authority on what was computed. Per plan D-08, a disagreement with a generated artifact is resolved
here, never by editing it.

---

## Why lands was pulled — measured, not inferred

`RESEARCH-FINDINGS.md` line 82 states that Postgres decks are classified as **singleton card sets**,
because Commander is singleton for the target nonland roles, while EDHREC cells preserve real decklist
quantities. That assumption is correct for the six nonland roles and **wrong for lands**: `8x Island`
collapses to 1. The corpus mean of 18.354 lands against a real Commander deck's ~36 is consistent with
this. The Postgres arm's "lands" figure is therefore **distinct land names**, not land count.

Tested rather than asserted. All 841 qualifying commanders were joined against `color_identity` in
`_role-floor-research/cards_full.json` (841/841 matched, 0 unmatched, front-face fallback for DFC names).

Pearson r, colour count vs role mean:

| Role | r | r² |
|---|---:|---:|
| **lands** | **0.734** | **0.539** |
| interaction-targeted | −0.130 | 0.017 |
| draw | −0.109 | 0.012 |
| payoffs | 0.053 | 0.003 |
| engines | −0.043 | 0.002 |
| wincons | 0.043 | 0.002 |
| ramp | 0.034 | 0.001 |

Colour count explains **54% of the variance in lands** and under 2% in every other role.

Mean "lands" by colour count is monotonic: 1c = 10.06, 2c = 17.09, 3c = 22.13, 4c = 24.14, 5c = 25.73.

The decisive evidence is the **direction** commanders clear in:

| Colours | n | clears LOW | clears HIGH |
|---:|---:|---:|---:|
| 1 | 155 | **118** | 3 |
| 2 | 347 | 14 | 31 |
| 3 | 271 | **0** | **191** |
| 4 | 11 | 0 | 10 |
| 5 | 46 | 0 | 39 |

A clean sign flip with no crossover above two colours. The two-colour band clears least of any group
(13.0%) because it sits at the corpus median and fails the 1.5× / 0.667× ratio gate *from the middle* —
exactly what a pure colour-count gradient predicts, and not what a genuine commander-specific land
requirement would produce.

**Consequence:** a commander-aware land floor built from this arm would encode *how many colours you
play* as if it were *how many lands you need*. Krenko does not want 10 lands; Krenko wants roughly 36,
most of them Mountains that collapse to one distinct name.

### The lands calibration control did not fail — it was miscalibrated

The ROADMAP set lands as a deliberate control against the 2026-07-16 study, whose verdict was
*"commander identity barely moves land count; bracket is the only driver."* This run appeared to
contradict it. It does not: **the prior study measured TOTAL lands; this run measured DISTINCT land
names.** Two different quantities, so no contradiction ever existed. The control was invalidated by the
singleton-set assumption, not by a disagreement about the world.

The six nonland GOs are unaffected — singleton treatment is correct for them, and r² ≤ 0.017 across all
six.

### If lands is to be measured properly in a later pass

Three options, none actioned here:
1. Carry quantities for the lands role in the Postgres arm.
2. Restrict the lands role to nonbasics.
3. Drop lands from the Postgres arm entirely and take land floors from the EDHREC arm (which preserves
   quantities) plus the already-shipped `EdhrecAveragesConverter` → `ManabaseBaselineSnapshot` →
   `CutLabFloorDefaults.ResolveLandsDefault` path.

---

## What ran

| Arm | Outcome |
|---|---|
| Smoke run (`MIN_DECKS=999999 LIMIT=50`) | exit **2**, no artifact written, ~2 min |
| EDHREC grid (`edhrec-role-grid`) | 249/249 batches, 14,150,219 rows, 31,788 distinct cards |
| Real run (`--min-decks 40 --mode cedh`) | exit **0**, ~27 min, artifacts written |

Real-run figures: RawDecks 130,075 · DedupedDecks 128,407 · Commanders enumerated 4,011 · with
membership 3,958 · **qualifying at DEDUPED N ≥ 40: 841**. Scryfall: 14 unresolved, all `not_found`,
**0 rate-limited-after-retry**.

Clearing-commander counts: lands 411 (pulled), engines 379, ramp 277, draw 275, interaction-targeted
273, wincons 153, payoffs 124.

---

## Success criteria

| Criterion | Status |
|---|---|
| Criterion 3 — zero qualifying → exit 2 AND no artifact | **PROVEN** by the smoke run |
| Criterion 8 / decision D-A — hybrid corpus | **MET** — 1,525 EDHREC cells fetched, 805 qualifying, 305 commanders reached. This was not a Postgres-only run |
| No credential in any artifact, log, or commit | **VERIFIED** — unpiped grep across all ten staged evidence files, all clean |
| D-08 per-row source attribution in the generated markdown | **PASSES** — 18 figure tables, 21,294 data rows, 0 empty source values |
| No CalVer bump, no tag | **HONOURED** |
| Developer dispositions go/no-go and lands calibration | **DONE** — this document |
| Plan 02-09 grid arm run or declined on the record | **RUN**, not declined |

### Criterion 3 — how it was proven, and a correction

The smoke run exited 2 and wrote no artifact. Independently verified with a glob-free mtime sweep
(`find -maxdepth 1 -mmin -10 -type f`), which returned only `role-floor-research-smoke.{log,exit}`.

**Correction on the record:** the watcher scripts used the glob `ROLE-FLOOR*` and printed
"NO ROLE-FLOOR\* artifact written". That filename was invented; the harness writes
`RESEARCH-FINDINGS.{md,json}`. The message was therefore false for the *real* run, which wrote 7.2 MB
of artifacts. The criterion-3 conclusion survives only because the mtime sweep was glob-independent.
Lesson recorded: pair a named-pattern check with a mtime sweep, and never let a self-authored glob be
the sole evidence of absence.

---

## Open gaps — stated, not hidden

1. **Provenance degraded: the artifact cannot name its own code state.**
   `| Harness Commit SHA | unknown |`, with the harness's own warning line. Diagnosed:
   `DescribeHarnessCommitSha` shells `git rev-parse --short HEAD`, but the harness runs under **Windows**
   `dotnet.exe` while this worktree's `.git` is a pointer file containing a **WSL** path
   (`gitdir: /mnt/c/users/...`). Windows git cannot resolve `/mnt/c`, so it fails and falls back to
   `unknown`. From WSL bash the same command returns the SHA correctly. This is a WSL-worktree +
   Windows-dotnet interaction, not a harness defect. **A research artifact that cannot be tied to a
   commit is weak evidence if cited externally.** Recommend re-stamping or re-running before these
   findings are quoted outside this phase.

2. **`protection` is PROVISIONAL.** `DeckStatClassifier.IsProtectionCard` under-detects in both
   directions (inconsistent verb agreement across four oracle needles; shroud and regeneration absent
   entirely). Its measured floors are a stated **LOWER BOUND**, pending the deferred Phase 01.2. The
   predicate is shared by three consumers — `InteractionAuditAggregator.cs:58`,
   `CutLabRoleAssigner.cs:165`, `PlanRoleClassifier.cs:236` — so widening it is not a one-line fix.

3. **EDHREC bracket support is uneven.** `exhibition` is NOT REPORTED (1 qualifying cell) and `cedh` is
   THIN (40 qualifying cells). Bracket-5 conclusions are weakly supported.

4. **Corpus hygiene.** 4.3% of sampled deck ids are dead (404 / private / deleted), which inflates every
   deck count including the deduped counts the breadth bar is applied against. There is **no recency
   window** — the corpus stores no `createdAt`/`updatedAt` at all.

5. **`.gitignore` conflict with D-05, resolved without editing the file.** D-05 requires the run log to
   be committed as evidence. `.gitignore:7` ignores `*.log`. `.gitignore` is on CLAUDE.md's
   "Do Not Modify Without Explicit Permission" list and D-05 forbids editing it under any checkpoint
   authorization. Resolved with `git add -f` for the three run logs, which commits the evidence without
   modifying the ignore file. Flagged here because it is a deviation from the frictionless path the plan
   assumed.

---

## FOLLOW-UP RECOMMENDATIONS — for the developer, outside these execution plans

1. **Should `_role-floor-research/` and `_edhrec-brackets/` be gitignored?** Carried forward from D-05,
   deliberately not acted on. Precedent: the sibling cEDH pipeline gitignores its `_calib/` cache. This
   needs the specific permission CLAUDE.md's "Do Not Modify" list requires; a checkpoint answer inside a
   plan is not a substitute.

2. **Artifact size in a public repo.** This commit adds roughly 19 MB of generated artifacts
   (`RESEARCH-FINDINGS.json` 5.6 MB, `EDHREC-ROLE-GRID.json` 9.5 MB, `EDHREC-ROLE-GRID.md` 2.5 MB,
   `RESEARCH-FINDINGS.md` 1.6 MB). The plan requires committing them; whether they should live in git
   long-term is worth a separate decision.

3. **Dead `normalizeForScryfall` parameter.** After plan 02-10, this parameter is unused in
   `ScryfallReferenceResolver.ResolveBatchAsync`. Four production callers still pass it.

4. **Plan 02-08's own text has wrong operator paths.** Lines 692–693 specify `/mnt/c/...` for the grid
   arm, but `dotnet.exe` is a Windows process and needs `C:\...`. Corrected in practice during the run;
   the plan text was not edited.

5. **Fix the harness SHA detection for WSL worktrees** (gap 1 above) so future artifacts are
   self-identifying.

---

## Unplanned work absorbed into this wave

Two plans were written and executed mid-wave, both forced by the failed first smoke attempt:

- **02-10** — Scryfall `cards/collection` requires **single-face** names; the combined `A // B` form
  returns `not_found`. Fixed at all four call sites behind a shared
  `DeckFlow.Core/Normalization/ScryfallCollectionIdentifier.cs` helper. Verified live against the
  Scryfall API, not inferred from the failure. Commits `0246fedf`, `f8c960a5`.
  **Note on the record:** the 429 that killed the 14:47 attempt was **pre-existing** and independent of
  the DFC defect — the first Scryfall event in that log is already `attempt 1/4` of a 429, with no
  ramp-up. An earlier causal claim blaming the DFC fallbacks was wrong and is corrected here.
- **02-11** — `--limit` for a cheap criterion-3 smoke run, so proving the exit-2 guard costs seconds
  instead of a full membership load plus Scryfall pass. Commits `5a10ce78`, `15958f3d`, plus `e21fdd78`
  hardening the wrapper to refuse a non-positive `LIMIT`.
  **Process note:** commit `5a10ce78` is labelled `test:` but contains the implementation, so no repo
  state exists where those tests fail. The failing output *was* observed before implementation, but the
  commit split does not preserve that evidence.

Because `--limit` takes the first N of an `ORDER BY deck_count DESC` query, it is not an arbitrary
sample — `LIMIT=100` yields the top 100 commanders by popularity, which makes it usable as a cheap
pilot as well as a smoke-run device.
