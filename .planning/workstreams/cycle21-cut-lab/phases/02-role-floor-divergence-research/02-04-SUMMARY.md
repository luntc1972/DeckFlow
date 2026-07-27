---
phase: 02-role-floor-divergence-research
plan: 04
wave: 2
status: complete
executed: 2026-07-27
commits:
  - 196b71f7  # refactor(02-04): delete synthetic fixture writer and widen TargetRoles to the shipped taxonomy
  - 398a3f4e  # test(02-04): make the taxonomy guard testable from Core and cover it
  - 3f301738  # feat(02-04): stamp run provenance and exit non-zero when no commander qualifies
  - 19994733  # test(02-04): cover credential-bearing connection-string shapes in the host describer
  - 28ab041b  # fix(02-04): point output defaults at the current phase folder and accept the connection string from the environment
  - 4342b146  # test(02-04): drop tautological credential assertions from the whitespace case
gates:
  build: "0 errors; 9 pre-existing CS8629 warnings (all in ManabaseBaselineWeightingTests.cs), 0 new"
  core_tests: "1708 passed / 0 failed (baseline at wave start: 1659)"
  web_tests: "2095 passed / 16 skipped / 0 failed — unchanged throughout"
  eol: "zero churn on every commit; all touched files LF before and after"
verification: "blind foreman-verifier PASS on all five code commits, fresh context each time"
---

# Phase 2 Plan 04 — Summary

The plan shipped as three tasks plus three follow-ups: **T1b** (a testability correction demanded by a
standing user rule), **T2b** (closing a verifier finding), and a one-commit test cleanup found during
review. No user-visible change; the harness lives in `DeckFlow.CLI` and is referenced by no controller,
view or web service. No CalVer bump, no tag.

## 1. The deleted fixture writer (ROADMAP criterion 1)

Four methods deleted in full from `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs`:

| Method | Lines at plan time | Callers |
|---|---|---|
| `WriteSyntheticVerificationOutputs` | 818-822 | none |
| `BuildSyntheticVerificationComputation` | 824-865 | only the above |
| `BuildSyntheticCommander` | 867-891 | only the above |
| `BuildSyntheticRoleStat` | 893-908 | only the above |

Proof: `grep -rni "synthetic" DeckFlow.CLI --include=*.cs` returns **no match**, exit code 1.
Independently reproduced by the blind verifier.

Deletion mattered rather than orphaning because `BuildSyntheticRoleStat` took `clearsBar` as an
**independently hardcoded `bool`**, decoupled from the hardcoded ratio/z/d literals beside it — which
is why the deleted `RESEARCH-FINDINGS.md` once showed identical statistics yielding different
verdicts, an outcome impossible via the real computation path.

## 2. The taxonomy (ROADMAP criteria 4 and 9)

`TargetRoles` as committed, in `CutLabRoleAssigner.RoleKeys` order so the findings tables read in the
same order as the shipped UI:

```
lands, ramp, draw, interaction-targeted, interaction-mass, protection, engines, payoffs, wincons
```

**`draw` is IN** — a first-class shipped role with its own floor key, display label and
`WeakFloorCase` participation. **`other` is OUT** — it is `CutLabRoleAssigner`'s residual bucket
(`:191-194`), assigned only when nothing else matched, so its count measures classifier coverage
rather than deck construction. The exclusion and its reason are recorded in the emitted "Known gaps"
list so a reader does not assume it was forgotten.

**What the old value would have done.** The previous five-role list carried the merged `interaction`
key that `CutLabRoleAssigner` no longer emits. The tally loop only increments keys already seeded, so
the harness would have recorded **zero interaction cards for every deck, for every commander** — a
corpus-wide null result presented as a measurement. The ROADMAP did not mention this at all.

**Downstream consumers needing edits beyond compiling: none.** Every consumer — the tally seed,
`BuildCorpusBaseline`, the per-commander loop, `BuildGoNoGo`, the markdown baseline table, the
per-role sections, the go/no-go list, and the JSON emitter — is a `foreach (string role in TargetRoles)`
or `TargetRoles.ToDictionary(...)` and scaled from five to nine keys unchanged. Each was read and
confirmed individually.

## 3. The taxonomy drift guard

`ValidateTaxonomyAgainstAssigner(ManabaseMode mode)` — **`internal static`**, called from `RunAsync`
immediately after `resolvedMode` and before `LoadCommanderRowsAsync`; on a non-null result it writes to
`Console.Error` and returns **1** (a configuration failure, not a null result).

### Half 1 — the reflected authoritative list

`CutLabRoleAssigner.RoleKeys` is `private static readonly` and cannot be referenced directly. It is
read via `RoleFloorGuards.TryReadShippedRoleKeys(typeof(CutLabRoleAssigner), "RoleKeys", out string[]? shippedRoleKeys)`,
using `BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static`.

**On failure the guard ERRORS — it never degrades to "no check".** Three branches, each returning a
non-null message and a null out-param:

```
Unable to read {type}.{field}: expected a static string[] field named {field}.
Unable to read {type}.{field}: expected the static field {field} to hold a non-null string[].
Unable to read {type}.{field}: expected the static field {field} to be a string[] but was {actualType}.
```

Reflection was chosen over widening `RoleKeys` to `internal` + `InternalsVisibleTo`, which would have
expanded a shipped web service's surface for a research harness's benefit and edited a file this
plan's scope fence forbids.

### Half 2 — one emission probe per shipped key

All nine, each sourced from an existing passing test rather than invented. Note the guard calls
`AssignRoles(probe, [], isComboPiece: false, mode)` with **empty categories**; where a cited test
passes a non-empty `categories` argument, the probe's oracle text independently satisfies the
oracle-heuristic path, and the probe comments say so.

| Shipped key | Probe card | Source test |
|---|---|---|
| `lands` | Forest | `CutLabRoleAssignerTests.AssignRoles_Forest_MapsToExactlyLands` |
| `ramp` | Cultivate | `CutLabRoleAssignerTests.AssignRoles_Cultivate_MapsToRampOnly` |
| `draw` | Quick Study | `CutLabRoleAssignerTests.AssignRoles_OneShotDrawSpell_NotEngine` |
| `interaction-targeted` | Swords to Plowshares | `CutLabRoleAssignerTests.AssignRoles_SwordsToPlowshares_IsTargetedOnlyInCasualViaPreGateSignal` |
| `interaction-mass` | Wrath of God | `CutLabRoleAssignerTests.AssignRoles_WipeByOracleHeuristic_IsMassOnly` |
| `protection` | Protection Wand | `Manabase/PlanRoleClassifierTests.Classify_ProtectionPermanent_IsInteractionAndNothingElse` |
| `engines` | Phyrexian Arena | `CutLabRoleAssignerTests.AssignRoles_PermanentDrawEngine_IsEngine` |
| `payoffs` | Avatar Finisher | `Manabase/PlanRoleClassifierTests.Classify_PermanentPayoff_IsKept` |
| `wincons` | Torment of Hailfire | `CutLabRoleAssignerTests.AssignRoles_TormentOfHailfire_IsWinconDespitePlanRolePermanentGate` |

`wincons` is earned through `IsClosingPowerCard` (the `"each opponent loses"` substring), never by
setting `isComboPiece: true` — `grep -c 'isComboPiece: true'` on the runner returns 0.

**Drift proof.** Two independent reproductions, neither trusting the other: Codex removed `protection`
then `wincons` from `TargetRoles` in throwaway edits and saw the guard name each; the blind verifier
loaded the built assemblies out-of-repo, mutated the live `TargetRoles` array reflectively, and got

```
Role-floor taxonomy drift: shipped keys missing from TargetRoles: protection;
TargetRoles entries not shipped by Cut Lab: protection-renamed;
probe-emitted keys outside TargetRoles and residual 'other': protection.
```

## 4. `RoleFloorGuards` — public surface

Plan `02-08` reasons about `HasNoQualifyingCommanders`, so the surface is pinned here:

```csharp
namespace DeckFlow.Core.Research;

public static class RoleFloorGuards
{
    public static string? TryReadShippedRoleKeys(Type assignerType, string fieldName, out string[]? shippedRoleKeys);
    public static string? FindTaxonomyDrift(IReadOnlyCollection<string> shippedRoleKeys,
                                            IReadOnlyCollection<string> targetRoles,
                                            IReadOnlyCollection<string> emittedKeys,
                                            string residualRoleKey);
    public static bool HasNoQualifyingCommanders(int qualifyingCommanderCount);
}
```

`FindTaxonomyDrift` returns null when clean, else a message naming all four failure classes
separately: shipped keys missing from `targetRoles`; `targetRoles` entries not shipped; emitted keys
outside `targetRoles` and the residual; and shipped keys no probe emitted. It is the **only** place
the drift verdict is decided — the CLI duplicates no set-difference.

`TryReadShippedRoleKeys` never returns null while leaving `shippedRoleKeys` null; a null return
entitles the caller to assume the out-param is populated.

## 5. Provenance (ROADMAP criterion 2)

`ResearchComputation` now carries `DatabaseHost`, `RunTimestampUtc` and `HarnessCommitSha` alongside
`CommandersEnumerated`, `RawDeckCount` and `DedupedDeckCount` — criterion 2's five fields.
`RunTimestampUtc` is `DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)` captured once
at the top of `RunAsync`, so it records the run's **start**, not serialization time.

Both artifacts carry it: markdown gains a `## Run Provenance` table positioned after the H1 and before
`## Methodology`; JSON gains `databaseHost`, `runTimestampUtc`, `harnessCommitSha` and
`provenanceWarnings` on the `methodology` object.

### How the host is derived, and why no credential can reach an artifact

`RoleFloorProvenance.DescribeDatabaseHost(string?)` parses with `NpgsqlConnectionStringBuilder` and
reads **`Host`, `Port` and `Database` only** — never `Username`, `Password`, `Passfile`, nor the raw
string. Any exception, null/empty input, or null/empty `Host` yields the literal `"unavailable"`;
there is no partial-string fallback that could carry a credential.

This is the phase's highest-risk boundary because the artifacts are committed to a **public**
repository. Evidence beyond code-reading:

- `grep -nE "\.Password|\.Username|Passfile"` returns nothing across the runner, `Program.cs`, and
  `RoleFloorProvenance.cs`.
- The blind verifier **executed** the function out-of-repo against 11 shapes — password, the `Pwd=`
  alias, a Unix-socket host, quoted values with an embedded `;`, a NUL-bearing malformed string,
  garbage, empty, whitespace, null, no-`Host`, trailing unknown key — and no credential fragment
  appeared in any return value, including on exception paths.
- Those shapes are now **checked-in regression tests** (`19994733`), not just a verifier transcript.
  The embedded-`;` case asserts absence of the fragment `semi;colon` as well as the whole secret, so a
  leak truncated at the delimiter cannot slip past.
- The normalized string was traced through `RunAsync`: it reaches only `RelationalDatabaseConnection`
  and `DescribeDatabaseHost`.

**Known latent risk, out of scope and unchanged by this plan:** the pre-existing catch-all at
`RoleFloorResearchCommandRunner.cs:343` writes `exception.Message` to `Console.Error`. Npgsql
connection-refused messages name only `host:port` in observed runs, but a future driver version
embedding connection-string fragments in exception text would surface them. Flagged for a later plan.

### Degraded provenance is a warning, not a value (D-08)

`RoleFloorProvenance.BuildProvenanceWarnings(databaseHost, harnessCommitSha, rawDeckCount, dedupedDeckCount)`
computes warnings **from the field values**, so they cannot be forgotten, and **no flag, threshold or
parameter can suppress them.** Exact strings:

- Host `"unavailable"` with non-zero deck counts:
  > Provenance contradiction: the run reached the corpus (120 raw decks, 100 deduped decks), but the
  > database host could not be derived from the connection string, so this artifact cannot be traced
  > to a specific database by its own contents.

- Host `"unavailable"` with zero deck counts:
  > Database host could not be derived from the connection string, and this run did not reach any deck
  > rows, so the artifact cannot identify which database it was meant to query by its own contents.

- SHA `"unknown"`:
  > The harness revision could not be determined, so this artifact cannot be tied to a specific code
  > state.

In markdown each renders as its own `> **WARNING — provenance degraded:** …` blockquote immediately
beneath the provenance table; nothing renders when the list is empty. In JSON they populate
`provenanceWarnings` (empty array when clean).

The deck counts are parameters specifically so the host warning can distinguish *connected but host
underivable* from *never reached the corpus* — the first is self-contradictory and must say so.

### Commit SHA

The CLI spawns `git rev-parse --short HEAD` and, on success, `git status --porcelain`, via
`ProcessStartInfo` + `ArgumentList` (never a constructed shell string), `UseShellExecute = false`.
Those are the only two git subcommands the harness may invoke. The **decision** lives in Core:
`RoleFloorProvenance.FormatCommitSha(int exitCode, string? revParseStdout, string? statusPorcelainStdout)`
returns `"unknown"` on non-zero exit or blank stdout, else the trimmed SHA with `-dirty` appended when
the porcelain output is non-blank.

## 6. Exit-code contract (ROADMAP criterion 3)

| Code | Meaning |
|---|---|
| `0` | Success — at least one qualifying commander, artifacts written |
| `1` | Bad arguments, taxonomy drift, or unhandled exception |
| `2` | Ran successfully, but zero commanders cleared the minimum deck count — **no artifact written** |

Documented in the `role-floor-research` command description (`Program.cs:78`), so `--help` states it.

The exit-2 guard calls the unit-tested `RoleFloorGuards.HasNoQualifyingCommanders(...)` rather than
inlining a comparison, and **does not throw** — an exception would be swallowed by the catch at
`:343` and re-reported as a generic failure, destroying the diagnostic. `thresholdCounts` is computed
**before** the guard so the operator sees which lower threshold would have qualified somebody.

Placement verified by line number: guard `:280` → `return 2` `:296` → `WriteFindingsFiles` `:316`.
Placement is the one thing no unit test pins; plan `02-08`'s `--min-decks 999999` smoke run is what
proves it end to end.

## 7. Connection string — precedence and exact error text (D-07)

For plan `02-08`'s wrapper, pinned precisely:

`RoleFloorProvenance.ResolveConnectionString(string? flagValue, string? environmentValue)` returns
`flagValue` when non-null and not whitespace; else `environmentValue` when non-null and not
whitespace; else `null`. **Explicit beats ambient — the flag wins when both are present.**

`--connection-string` is now `IsRequired = false`. `DECKFLOW_ROLE_FLOOR_CONNECTION_STRING` appears
**exactly once** in the runner — a single resolution point. Neither value is ever echoed, logged, or
interpolated.

Exact `Console.Error` text when neither is supplied (followed by exit code 1):

```
Either --connection-string or the DECKFLOW_ROLE_FLOOR_CONNECTION_STRING environment variable is required.
```

Verified end to end without a database: env-var-only with a fake unreachable value passed argument
validation and failed at connect time; with **both** set to distinguishable fakes, the error named the
**flag's** host, proving precedence at the integration level, not merely the unit level.

The motivation is process-list exposure: argv is readable by any other user on the machine via `ps`,
`/proc/<pid>/cmdline` or Task Manager, for the whole of a multi-hour run.

`--cards-cache` was deliberately **left pointing at `_role-floor-research/cards_full.json`** despite
the workstream rename — the 8.2 MB Scryfall cache there must stay reachable, and repointing it would
force a multi-hour rate-limited re-fetch.

## 8. Output defaults (ROADMAP known defect 6)

`--out` and `--out-json` now default into
`.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/`, replacing the
`cutlab-role-floors` / `01-role-floor-divergence-research` pair that the 2026-07-26 rename and
renumber left dangling. `grep -n "cutlab-role-floors" DeckFlow.CLI/Program.cs` returns nothing.

## 9. Requirement traceability gap

Tasks 1 and 2 carry **RFLR-09**. **The taxonomy widening still has no dedicated requirement ID.**
RFLR-03 covers findings "over the post-ISPL role taxonomy" — that is the interaction split, not
`lands`, `ramp` and `draw`, which enter via decision D-C taken 2026-07-27, after `REQUIREMENTS.md` was
written. No ID was invented.

Recommended for the developer to add to `REQUIREMENTS.md` (a governance doc shared with the whole
cycle, deliberately not edited by this plan):

> **RFLR-12** — The research harness measures the full shipped `CutLabRoleAssigner` role taxonomy,
> including `lands`, `ramp` and `draw`, and fails at startup rather than silently reporting zero if its
> taxonomy drifts from the shipped one.

## 10. Deviations from the plan as written

**A. D-06's premise was factually wrong, and was corrected during execution.** The plan asserted that
CLI-resident code "is covered by grep and code-reading alone" because there is no `DeckFlow.CLI` test
project. There is no such project, but `DeckFlow.Core.Tests.csproj` already references
`DeckFlow.CLI` and `DeckFlow.CLI/AssemblyInfo.cs` already carries
`InternalsVisibleTo("DeckFlow.Core.Tests")` — three suites already rely on it. Under a standing user
rule (*"things added to the console app must use core and have tests added to core for anything new"*),
commit `398a3f4e` lifted the reflection into Core as a `Type`-parameterised helper, made
`ValidateTaxonomyAgainstAssigner` `internal`, and added `RoleFloorTaxonomyGuardTests`, which asserts
the guard passes against the real shipped assigner for **every** `ManabaseMode` via
`Enum.GetValues<ManabaseMode>()` — so a newly added mode is covered automatically. The plan text has
been corrected in place, as has the same claim in `02-08-PLAN.md`.

The real constraint, which survives: `DeckFlow.Core.csproj` has **zero** `<ProjectReference>` entries,
so Core cannot name `CutLabRoleAssigner` / `CardFact` / `PlanRole`. Web-coupled glue stays in the CLI —
but `internal` and tested, not written off.

**B. Two acceptance criteria were self-contradictory and have been replaced with semantic checks.**
`grep -c '"interaction",' … == 0` (Task 1) and `grep -c "cycle21-cut-lab" Program.cs == 2` (Task 3)
are both tripped by `// Why:` comments the same plans' action steps *require*. Corrected in the plan.
Lesson for future plans: assert on the construct, not on a file-wide substring count.

**C. Task 2 could not document the exit codes**, because the description string lives in
`Program.cs:78` and Task 2's scope fence forbade that file. Task 3 owns the edit and made it. The plan
duplicated the instruction across both tasks.

**D. One test was tautological and was cleaned up** (`4342b146`): the whitespace-only case declared
`password`/`username` constants it never fed into the input, so its `DoesNotContain` assertions could
not fail. Renamed to `DescribeDatabaseHost_WhitespaceOnlyInput_ReturnsUnavailable` and reduced to the
contract it actually pins.

## 11. What this plan deliberately did NOT do

Untouched, per the scope fence and `<out_of_scope>`: the `ClearsBar` → `ClearsFloorBar` switch and
output source-tagging (plan `02-05`); all EDHREC ingestion and `--edhrec-data` (plans `02-02`/`02-06`);
the lands calibration verdict, the protection under-detection disclosure and the no-go template (plan
`02-07`); any live-database run (plan `02-08`, behind an operator checkpoint); `REQUIREMENTS.md`;
`.gitignore`; `CutLabRoleAssigner` and every shipped web service.

## 12. Next

Wave 3 — plan `02-05` (switch `ClearsBar` onto `ClearsFloorBar`; source-tag the output types so an
EDHREC figure cannot occupy a percentile column).
