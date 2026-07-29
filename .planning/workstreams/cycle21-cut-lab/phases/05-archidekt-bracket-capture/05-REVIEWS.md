# Phase 5: Archidekt Bracket Capture — Plan Review Round 1

**Date:** 2026-07-29
**Workstream:** `cycle21-cut-lab`
**Plans reviewed:** `05-01-PLAN.md`, `05-02-PLAN.md`, `05-03-PLAN.md` (all at HEAD `2f9ab5ef`)
**Verdict:** **CHANGES REQUIRED** — 3 BLOCK, 4 HIGH, 8 MEDIUM, 5 LOW after dedup.

## Reviewer roster and assurance level

| Reviewer | Lens | Verdict | Assurance |
|----------|------|---------|-----------|
| `gsd-plan-checker` (Claude, fresh context) | plan-text vs phase goal / ROADMAP success criteria / D-01..D-08 | FAIL — 3 BLOCK | Normal |
| Claude fresh-context reviewer (general-purpose) | claim-vs-code against real source | CHANGES REQUIRED — 3 BLOCK, 3 HIGH | **REDUCED — same-family, not cross-AI verified** |
| Codex `gpt-5.6-luna` (authoritative gate per CLAUDE.md) | claim-vs-code | **NOT RUN** | **ABSENT** |

> **⚠ ASSURANCE DISCLOSURE.** The authoritative cross-AI gate did not run. Codex failed twice with
> `ERROR: Your workspace is out of credits. Ask your workspace owner to refill in order to continue.`
> — deterministic (billing), not transient; the retry returned it verbatim on a 5-token probe, and
> zero bytes of review were produced. The user authorized a same-family Claude reviewer as substitute
> on 2026-07-29. Consequence, stated plainly: **findings below are real, but silence is weak evidence.**
> A same-family reviewer cannot catch a blind spot it shares with the planner. Precedent for why this
> matters: the Phase 1 `HasWipeCategoryTag` blocker survived three Codex plan-review rounds *and* a
> green suite. Re-run the Codex gate before phase closeout if credits return.
> Neither reviewer ran `dotnet build` or `dotnet test`; every compile-break claim is read from
> signatures, not from a compiler. No SQL was executed against Postgres.

## The load-bearing claim: plans were RIGHT

`05-03` asserts "`RunAsync` still calls `MarkDeckProcessedAsync` after `PersistDeckAsync`; use that
existing post-import mark to write metadata even for unchanged card lists." **CONFIRMED CORRECT.**
`ArchidektDeckCacheSession.cs:109` destructures the result; `:110-121` is a pure counter
`if/else if/else` with **no `continue` and no early return** — the `Unchanged` branch (`:114-117`) only
increments a counter, and control falls through to `:125`
`MarkDeckProcessedAsync(deckId, commanderName, skip: false, …)`. `PersistDeckAsync` returns early from
*itself* (`:194`), not from the loop body. `05-03` T1 Test 2 and T2 are satisfiable as written.
The LEAD's pre-review suspicion that this was a BLOCK was **wrong**; recorded so the correction is durable.

Also refuted as concerns: **Dapper is real** (`using Dapper;` `DeckQueueRepository.cs:2`; all writes via
`ExecuteAsync(new CommandDefinition(...))`), so `05-02`'s threat model is accurate. **`dotnet.exe`
resolves** (`/mnt/c/Program Files/dotnet//dotnet.exe`) and **all 8 named test classes exist**.
**All five payload fields are real**, verified in the captured fixture: `createdAt`, `updatedAt`,
`deckFormat: 3`, `edhBracket: null`, `theorycrafted: false` — and the D-01-excluded `viewCount: 805` /
`points: 1` are present too, so the exclusion is meaningful rather than theoretical.

---

## BLOCK findings

### R-B1 — Interface evolution is undecided, and the write sets undercount by 12 of 14
**Corroborated by both reviewers.** `05-01` T2 offers "either require implementations … **or** make any
compatibility default throw `NotSupportedException`". A plan must decide. `IArchidektDeckImporter` has
**no default members today** (`DeckImporterInterfaces.cs:54-63`) and **14 implementers** exist (1 prod +
13 test doubles across 12 files); only **2** appear in any write set.

- **Option A (require implementations) = 11 compile-breaking files.** `05-01`'s own verify compiles all
  of `DeckFlow.Core.Tests`, so **wave 1 goes red on files no plan lists.**
- **Option B (throwing default) = exactly 1 real gap** (see R-B2) and matches documented house
  precedent: `90-04-PLAN.md:99`, `90-03-PLAN.md:108` — "THROWING DEFAULT INTERFACE METHOD … so
  `FakeProdContentReader` compiles WITHOUT edits (no CS0535)".
- **The dangerous escape hatch:** the option `05-01` forbids — a default that wraps `ImportAsync` and
  fabricates `CapturedUtc` — violates **D-03 and D-06** and lets non-Archidekt importers claim
  "captured". An undecided plan invites exactly this.
- Neither option creates a false-captured path in *production*: `ArchidektApiDeckImporter` is the sole
  production implementer (`Program.cs:203`), and all production consumers
  (`DeckEntryLoader.cs:69`, `CategorySuggestionService.cs:62`, `AdminHarvestController.cs:28`,
  `ArchidektDeckCacheSession.cs:17`, `DeckCommandRunners.cs:361`) resolve to it.

**RESOLUTION (LEAD):** Delete option A. Mandate `ImportWithMetadataAsync` as a **throwing default
interface method**, real-implemented only on `ArchidektApiDeckImporter`. Delete the contradicting
sentence "the default interface method should keep them source-compatible" from `05-03` T3 and replace
it with the explicit list of fakes that must override. Fakes needing edits: `ArchidektDeckCacheSessionTests.cs:118`
(listed), `AdminHarvestControllerTests.cs:334` (listed), `ContentHashDedupTests.cs:436` (**not listed** — R-B2).
The other 10 stay untouched, satisfying `05-PATTERNS` Pitfall 2.

### R-B2 — `ContentHashDedupTests` breaks and sits inside `05-03` T2's own verify command
**Corroborated.** `ContentHashDedupTests.FakeDeckImporter` (`:436`) implements **only** `ImportAsync`
(`:440`) and is the importer for `ArchidektDeckCacheSession` in 4 tests (`:190`, `:224`, `:254`, `:281`
via `CreateSession :339`). Once `PersistDeckAsync` calls `ImportWithMetadataAsync` it breaks —
compile error under option A, `NotSupportedException` at runtime under option B. The file is in
**no** plan's `files_modified`, yet `05-03-PLAN.md:148` runs it.

`05-RESEARCH.md:236` flagged this file for the **wrong reason** (expectation changes) and the plans then
dropped it entirely. The expectation-change worry is a false lead: its `FactSnapshot` (`:210`) and
`DeckQueueRow` (`:425-430`) read no metadata columns.

**RESOLUTION:** Add `DeckFlow.Core.Tests/ContentHashDedupTests.cs` to `05-03` T2 `<files>` and
`files_modified`, with the instruction to implement `ImportWithMetadataAsync` on `FakeDeckImporter`
returning the same entries plus a deterministic metadata record. State that no existing expectation changes.

### R-B3 — `ICategoryKnowledgeStore` optional parameter = CS0535 in 5 unlisted implementers
**Corroborated.** An **optional** parameter still changes the signature. `ICategoryKnowledgeStore.MarkUrlDeckProcessedAsync`
is abstract (`ICategoryKnowledgeStore.cs:71`); 7 implementers exist, **2 listed**. Missing:
`CategorySuggestionServiceTests.cs:149`, `CutLabAnalysisContextBuilderTests.cs:662`,
`CutLabPageServiceTests.cs:2918`, `HarvestStatsAggregatorTests.cs:67`, `HarvestStatsAggregatorTests.cs:138`.
`DeckFlow.Web.Tests` will not compile, so **both** `05-03` verify commands fail for a reason unrelated to
the code under test. `05-03` T3's prose says "update impacted test doubles across Web tests" but names none —
and this project hands the executor a hard file fence, so an incomplete fence means it stalls or edits
un-fenced files.

**RESOLUTION:** Prefer the same house pattern — add a **new 4-arg overload as a throwing default
interface method** and leave the 3-arg member untouched (zero CS0535). If instead the signature is
widened, all four files must be listed and the 5 classes named explicitly, each also needing
`using DeckFlow.Core.Integration;`. Re-check whether `05-03` should split; its real size is ~12 files, not 7.

### R-B4 — Metadata wipe: the guarantee has no test that can fail
**Corroborated.** `05-02` T2 directs "when metadata is null, leave all Archidekt metadata columns
**unchanged**". The only proofs (`05-02` T1 Test 4, `05-03` T1 Test 3) run on rows that **never had
metadata**, where "left unchanged" and "overwritten with NULL" are indistinguishable. An implementation
doing `SET archidekt_edh_bracket = NULL, … captured_utc = @captured` passes both.

**Reachable in production, not hypothetical:** `AddDeckIdsAsync` requeues any deck whose
`last_checked_utc` is older than the 5-day `DeckRefreshCooldown` (`DeckQueueRepository.cs:14`, predicate
`:146`, `:151`); a transient Archidekt `HttpRequestException` on re-harvest routes to
`MarkDeckProcessedAsync(skip: true, metadata: null)` (`ArchidektDeckCacheSession.cs:137`). A captured row
silently reverts to "not captured" — destroying the exact three-state guarantee that **is** BRKT-03 and
ROADMAP criterion 3, and violating criterion 2's "existing rows unaffected" via the phase's own code.

**RESOLUTION:** Restate Test 4 as two-step — write non-null metadata first, **then**
`MarkDeckProcessedAsync(skip: true, metadata: null)`, then assert all six columns still hold the
**original** values and `archidekt_metadata_captured_utc` is still non-null. Keep the fresh-row
assertion as a separate additional test. Mirror both for the `MarkUrlDeckProcessedAsync` update branch
(R-M2).

---

## HIGH findings

### R-H1 — Postgres is provable in zero environments, and the verify command is a false green
**Corroborated.** No local Docker; `.github/workflows/ci.yml` never sets `DECKFLOW_POSTGRES_TESTS`;
`PostgresFactAttribute.cs:14-18` requires it `== "1"` **in the Windows process env**, else sets `Skip`.
So `[PostgresFact]` runs nowhere. Worse, `05-02-PLAN.md:156`'s
`DECKFLOW_POSTGRES_TESTS=1 dotnet.exe test …` **cannot enable the gate at all** — WSL bash env vars do
not cross into Windows `dotnet.exe` without `WSLENV` (see `reference_wsl_dotnet_env_secret`,
`reference_wslenv_w_flag_windows_dotnet`). The run exits 0 with everything skipped, indistinguishable
from "Docker missing". `05-02` T3's `<done>` ("green … **or** explicitly recorded as skipped") is then
satisfiable by a false green.

**RESOLUTION:** Command becomes
`WSLENV="${WSLENV:+$WSLENV:}DECKFLOW_POSTGRES_TESTS" DECKFLOW_POSTGRES_TESTS=1 dotnet.exe test …`.
`<done>` must require **positive proof** — output showing `[PostgresFact]` tests *passed*, not skipped.
If Docker is absent, T3 records "**NOT VERIFIED on Postgres**" (never "skipped as expected") and the
phase summary carries R-H2 as an open production risk. **Pending user decision:** whether to add a
postgres service + the env var to `.github/workflows/ci.yml` (that file is on the project's
do-not-modify-without-permission list).

### R-H2 — Postgres type break that SQLite renders invisible
**Corroborated (mechanism SUSPECTED, plan defect CONFIRMED).** `05-02` leaves parameter-binding shape
undecided while forbidding dialect branches, and the idiomatic-looking choice fails on Render:
1. `archidekt_theorycrafted INTEGER NULL` bound from a `bool?` — `BoolTypeHandler.SetValue` binds a
   **native `boolean`** for non-SQLite parameters, and Postgres has no *assignment* cast
   boolean→integer (`42804`). Existing code deliberately never binds a bool: `skipped = skip ? 1 : 0`
   (`DeckQueueRepository.cs:309`, `:456`).
2. Preserve-on-null via `COALESCE(excluded.x, deck_queue.x)` with a `DateTime`/`DateTimeOffset` param —
   `COALESCE(timestamptz, text)` cannot be unified by Postgres. `DateTimeTypeHandler.SetValue` passes
   raw `DateTime` for non-SQLite.

Same class as the documented **F-51-PG-01** incident (rationale comment `DeckQueueRepository.cs:125-133`).
SQLite passes both, so local tests go green.

**RESOLUTION (merged, see R-M4):** Make conversions **mandatory and explicit in C#, not SQL** — bind
`metadata.Theorycrafted is null ? (int?)null : (metadata.Theorycrafted.Value ? 1 : 0)`, and bind
timestamps as pre-formatted `"O"` strings so every new parameter is `int?`/`string?` on both dialects.
Add a dialect-independent Core test: extract the mapping into a pure
`ArchidektDeckMetadataParameters.From(metadata)` and assert every emitted value is `int?`/`string?` and
**never** `bool` or `DateTimeOffset`. This gate runs everywhere, with or without Docker.

### R-H3 — `must_haves` proxies that are already true on the unmodified tree
**Corroborated; live counts measured at HEAD `2f9ab5ef`.** `AdminHarvestController.cs` /
`MarkUrlDeckProcessedAsync` = **1**; `CategoryKnowledgeRepository.cs` / `MarkDeckProcessedAsync` = **2**.
Same for `05-03`'s `key_links` patterns (`ArchidektDeckCacheSession.cs:124`, `AdminHarvestController.cs:273`).
These are the **D-05 "both paths share one metadata surface" gates** — the phase's cross-path guarantee —
and every one passes before any change is made. Fifth instance of this defect class in the cycle
(prior four: `"interaction",`, `cycle21-cut-lab == 2`, `DECKFLOW_ROLE_FLOOR_CONNECTION_STRING == 0`,
`CutLabRoleAssigner.AssignRoles == 1`). The three metadata-named criteria are sound (count = 0 today).

**RESOLUTION:** `AdminHarvestController.cs` → `contains: "ImportWithMetadataAsync"`;
`CategoryKnowledgeRepository.cs` → `contains: "ArchidektDeckMetadata?"`; session key_link →
`metadata: import.Metadata`; controller key_link → `result.Metadata`.

### R-H4 — `05-01` mutates a Core interface with 10 Web.Tests implementers but never compiles Web.Tests
**Single-reviewer.** `05-01`'s only verify is
`dotnet.exe test DeckFlow.Core.Tests/... --filter ArchidektApiDeckImporterTests`. A green wave 1 can hide
a broken Web test assembly until wave 3, after two plans are marked done.

**RESOLUTION:** Add `dotnet.exe build DeckFlow.sln --no-restore` to `05-01` Task 2's `<verify>`.

---

## MEDIUM findings

### R-M1 — New failure mode in *user-facing* paths, forbidden by ROADMAP criterion 4
**Single-reviewer, CONFIRMED by reading.** Making `ImportAsync` delegate to `ImportWithMetadataAsync`
(`05-01-PLAN.md:121`) puts new metadata-parsing throw sites in the path of **every** Archidekt import,
including user-facing ones: `DeckEntryLoader.cs:129`, `:198` (deck sync / analysis / comparison) and
`CategorySuggestionService.cs:183`. In harvest, the catch filter is
`when (exception is HttpRequestException or InvalidOperationException)`
(`ArchidektDeckCacheSession.cs:132`) — a `FormatException`/`OverflowException` from `GetInt32()` on
`edhBracket: 3.5` or `1e999` escapes it and aborts the **whole** `RunAsync` loop rather than skipping one
deck. Today a malformed top-level field cannot affect any of these paths. ROADMAP criterion 4
(`ROADMAP.md:360`) forbids exactly this.

**RESOLUTION:** Mandate `TryGetInt32`/`TryGetDouble`/`TryGetBoolean`/`DateTimeOffset.TryParse` only — no
bare `Get*` on payload values — and wrap the metadata block so no exception can escape it. Add a test
that **`ImportAsync`** (not only `ImportWithMetadataAsync`) still returns the correct entry list when
`edhBracket` is `3.5`, `"abc"`, `{}`, and `1e999`.

### R-M2 — `CapturedUtc` on any parsed JSON records a permanent false "captured, absent"
**Single-reviewer.** `CapturedUtc` is set on *any* successfully parsed JSON. A wholesale payload-shape
change (Archidekt renames `edhBracket`, or returns an object) yields `captured_utc NOT NULL` + all
curated fields null — permanently recording "captured, absent" for decks never actually inspected for a
bracket, and undetectable after the fact. The `cards` early-return path
(`ArchidektApiDeckImporter.cs:60`) also reaches this.

**⚠ LEAD ARBITRATION — this finding CONFLICTS with R-M1's fix as written.** R-M1 says wrap the block and
return all-null metadata *with* `CapturedUtc` set; R-M2 says withhold `CapturedUtc`. **Merged
resolution, which satisfies both:** set `CapturedUtc` only when the payload is *recognizably* an
Archidekt deck payload — require at least one of `id`/`name` **plus** at least one of the five curated
keys to be **present** (present-but-null still counts as captured; wholly absent does not) — **and**
separately guarantee no parse exception escapes. Add a test: payload `{}` → no capture recorded, import
still succeeds.

### R-M3 — URL upsert's `DO UPDATE` branch for null metadata is unspecified
**Single-reviewer.** `05-02` T2 says "unchanged for regular processed updates and null for fresh URL
inserts" — silent on the **update** branch. Existing SQL uses
`COALESCE(excluded.commander_name, deck_queue.commander_name)` (`DeckQueueRepository.cs:413`); the plan
does not say to mirror it. Test 5 uses non-null metadata on both insert and update, so the case is
untested — and `metadata = null` being the default on the public store method makes null the easy
accidental call.

**⚠ LEAD ARBITRATION — conflicts with R-H2, which forbids `COALESCE` on timestamps.** Both are correct,
and R-M4's fix dissolves the conflict: once timestamps are bound as `"O"` **strings**,
`COALESCE(text, text)` unifies trivially on Postgres. **Merged resolution:** specify
`DO UPDATE SET archidekt_x = COALESCE(excluded.archidekt_x, deck_queue.archidekt_x)` for all six
columns, **conditional on R-M4 being applied first**, and add a test that a null-metadata URL re-mark
preserves prior captured values.

### R-M4 — Timestamp storage instruction is self-contradictory across dialects
**Corroborated.** `05-02` T2 says "round-trippable UTC text consistently with existing `deck_queue`
timestamp storage"; T1 says "assert they round-trip **by value or ISO string** according to the
repository's existing timestamp storage convention". Existing storage is **not one convention**:
`last_checked_utc` is declared `TEXT` on both dialects (`CategoryCacheSchema.cs:54`) but the value is
bound as a raw `DateTime` (`DeckQueueRepository.cs:308`), which `DateTimeTypeHandler` renders as `"O"`
text for SQLite and hands Postgres as a native `timestamptz` that Postgres renders in its own format. A
single assertion cannot satisfy both — and an OR-assertion pins nothing. The phase's entire
justification is a *future* query over these columns.

**RESOLUTION:** Pin one format — format to `"O"` in C# before binding, so both dialects store
byte-identical ISO text; assert exact string equality. This also removes the timestamp half of R-H2 and
unblocks R-M3.

### R-M5 — Commander-extraction change: real fix, unratified scope, unacknowledged corpus consequence
**Corroborated, and stronger than suspected.** `entry.Category == "Commander"`
(`AdminHarvestController.cs:269`) can **never** match an Archidekt import — `IsBoardCategory` (`:152`)
strips `Commander` from `Category` (filtered `:79`) and `DetermineBoard` sets `Board = "commander"`
(`ArchidektApiDeckImporter.cs:130`). So URL-imported rows currently persist `commander_name = NULL`
**always**. The bulk path is already correct (`ArchidektDeckCacheSession.cs:185-188`).

Consequences no plan acknowledges:
- After the fix, future URL-imported decks newly enter the `commander_name IS NOT NULL` aggregates the
  role-floor corpus groups on (`DeckQueueRepository.cs:74`, `:101`), while historically URL-imported
  rows stay invisible. **A commander-grouped corpus query must not read the URL subset as a time series.**
- The success banner changes from the constant `"Harvested deck: N new observations."` to
  `"Harvested <Commander>: …"` (`AdminHarvestController.cs:286`).
- `AdminHarvestControllerTests` contains **zero `SubmitUrl` tests**, so `05-03` Test 5's "preserving the
  existing success banner shape" is asserted against nothing — and is unfalsifiable as phrased.
- `Build()` (`:170`) hard-codes `StubArchidektDeckImporter` (`:334-338`, returns an empty list) and must
  gain an importer parameter.
- Mapped to **no BRKT requirement**; `05-CONTEXT` lists it under neither Decisions nor Discretion.

**PENDING USER DECISION:** (a) drop from Phase 5 and file as its own quick fix, or (b) ratify as an
explicit decision. Either way `05-03` gains a `must_haves.truths` entry recording the divergence and
stating pre-existing rows are **not** backfilled (mirroring D-04's posture), and Test 5 asserts the
concrete new banner string.

### R-M6 — `05-02` T1's cited "legacy schema parity patterns" do not exist
**Single-reviewer, CONFIRMED.** T1 `read_first` claims `CategoryCacheSchemaParityTests.cs` holds
"fresh/**legacy** schema parity patterns". The legacy half is absent: all 18 `[Fact]`s are fresh-schema
(`:22-:478`), `EnsureSchemaAsync` appears only at `:27` and `:70`, there is **no** double-`EnsureSchemaAsync`
idempotence test, and a repo-wide grep for a legacy `CREATE TABLE deck_queue` / `ADD COLUMN` test returns
nothing. Test 2 must be built from scratch — and because every repository method calls
`EnsureSchemaAsync` first, it must open a raw `SqliteConnection` and create the legacy table **before**
touching the repository.

**RESOLUTION:** Correct the `read_first` wording and add explicit steps: raw `SqliteConnection`,
`CREATE TABLE deck_queue` with the pre-Phase-5 column list (`CategoryCacheSchema.cs:48-57` minus the new
six), insert one row, run `EnsureSchemaAsync` twice via the repository, assert row survival + all six
columns present + `archidekt_metadata_captured_utc IS NULL`. Migration source under test:
`CategoryCacheSchema.cs:61-67`.

### R-M7 — No plan runs the format gate
**Corroborated.** Grep for `format-check|format-gate|editorconfig|line ending|dotnet build|CarveOut`
across all three plans → **zero hits**. `CLAUDE.md` makes `.editorconfig` + the changed-lines gate
authoritative and names CI `format-gate` the authoritative enforcer; the local hook is opt-in via
`core.hooksPath`. Eight tasks of new C# with no format check = CI red after the phase is declared done.

**RESOLUTION:** Add `scripts/format-check-changed.sh staged` as a final `<automated>` in each plan. Note
the new `sealed record` must not have `[Attribute]` inlined onto the property line and files stay LF.

### R-M8 — `05-VALIDATION.md` per-task map is wrong before execution starts
**Corroborated.** The map lists exactly 3 tasks, all as plan `01` / wave 1 (`05-01-01/02/03`), and maps
`BRKT-02/03` to a plan-01 task. The phase has **8 tasks across 3 plans and 3 waves** (2/3/3).

**RESOLUTION:** Regenerate with 8 rows keyed `05-0P-0T`, correct wave/requirement/threat refs per task.

---

## LOW findings

| ID | Finding | Resolution |
|----|---------|------------|
| R-L1 | `05-03` T1 Test 5 "preserving the existing success banner shape" is unfalsifiable, and the banner content actually changes. | Assert the exact expected new banner string. |
| R-L2 | `ArchidektDeckCacheSessionTests.cs` has **no** raw SQL / `SqliteConnection` access at all, but Tests 1-3 must read `deck_queue` metadata columns while `05-02` T2 forbids a read API. | Point `05-03` T1 `read_first` at `ContentHashDedupTests.ReadDeckQueueRowAsync` (`~:420-430`) + its `DeckQueueRow` record as the analog to copy. |
| R-L3 | `05-01` T1 Test 4's request count needs more than "augment the existing handler": `FixtureMessageHandler` is built inline inside `CreateImporterReturningFixture` (`:36-45`), which returns only the importer, so the test cannot reach the handler. | Specify `public int RequestCount` on `FixtureMessageHandler` (`:66-73`) plus a second factory overload returning `(ArchidektApiDeckImporter, FixtureMessageHandler)`; keep the 2-arg factory for untouched tests. |
| R-L4 | No fixture carries a non-null `edhBracket` (`archidekt-background-companion.json` has `edhBracket: null`), so every positive bracket-parse assertion runs against JSON synthesized in-test. | Accept on Phase 2's live sampling evidence, or add one fixture from a live bracketed deck. State the choice in the plan. |
| R-L5 | No README/doc update planned though `deck_queue` gains six columns; `CLAUDE.md` requires README updates when behavior changes. | Add a doc line to `05-03` closeout, or state explicitly that harvest-internal schema needs no README change. |

---

## ROADMAP success-criteria coverage (post-review assessment)

| # | Criterion | Status | Blocking findings |
|---|-----------|--------|-------------------|
| 1 | Bracket parsed from already-fetched payload; request count unchanged | **Executable** — the one clean, falsifiable gate (`05-01` T1 Test 4) | R-L3 (mechanics), tighten `05-03`'s loose prose to `Assert.Equal(1, fake.ImportCalls)` |
| 2 | Nullable column; existing rows and queries unaffected | **PARTIAL** — SQLite only | R-H1, R-H2 (no Postgres proof); R-B4 (own code can wipe rows) |
| 3 | "Not captured" distinguishable from "captured, absent" | **PARTIAL** — holds for fresh/legacy rows only | R-B4 (wipe reverts state); R-M2 (false captured on shape change) |
| 4 | Harvest survives missing/malformed field; no new failure mode | **PARTIAL** — parsing half proven; persistence half not | R-M1 (`FormatException` escapes and aborts `RunAsync`); R-H2 (`NpgsqlException` likewise) |

## Requirement coverage

| Req | Plans | Executable proof |
|-----|-------|------------------|
| BRKT-01 | 05-01, 05-03 | **Yes** — strongest-covered requirement |
| BRKT-02 | 05-02, 05-03 | **Partial** — SQLite fresh + legacy migration; Postgres (the real target, Render) has no runnable proof |
| BRKT-03 | 05-02, 05-03 | **Partial** — three-state proven for fresh/legacy; both corruption paths ungated |

All three IDs appear in a plan's `requirements` field. Dependency graph (`[] → [05-01] → [05-01,05-02]`,
waves 1/2/3) is valid and acyclic. No deferred idea (raw JSON, backfill, commander × bracket derivation,
UI) leaked into any plan — D-01..D-08 compliance is otherwise clean, with R-M5 the one unratified addition.

## Round 1 disposition

- **9 findings ready to fold** with no further input: R-B1, R-B2, R-B3, R-B4, R-H3, R-H4, R-M4, R-M6, R-M7, R-M8, R-L1..R-L5.
- **2 findings blocked on user decisions:** R-M5 (drop vs ratify the commander-extraction change) and
  R-H1 (whether `.github/workflows/ci.yml` may gain a postgres service — do-not-modify list).
- **3 conflicts arbitrated by LEAD** and recorded above: R-M1 vs R-M2 (`CapturedUtc` semantics),
  R-M3 vs R-H2 (`COALESCE` on timestamps), and R-B1's option A vs option B.
- **Codex re-review still owed** before phase closeout if credits return — this round's claim-vs-code
  lens was same-family.
