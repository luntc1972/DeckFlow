# Phase 5: Archidekt Bracket Capture — Plan Reviews

> ⚠ **2026-08-01 — R-H1 AND F4-1's SHARED PREMISE IS NO LONGER TRUE.** Both findings rest on
> *"Postgres is provable in zero environments."* It is now provable locally: Docker Desktop plus the
> existing `PostgresContainerFixture` runs the gated suite green from this worktree — **2231 passed /
> 0 failed / 1 skipped**, down from 16 skips, i.e. every `[PostgresFact]` executed. Invocation:
> `WSLENV=DECKFLOW_POSTGRES_TESTS/w DECKFLOW_POSTGRES_TESTS=1 "/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests -c Release`
> (the `/w` is mandatory; without it the flag never reaches the Windows test host and every fact
> silently skips, which reads exactly like a pass).
>
> Consequence: the `archidekt_theorycrafted` `42804` hole must now be **closed with a `[PostgresFact]`
> driving `DeckQueueRepository`'s metadata write**, not disclosed as a NOT-VERIFIED carry-forward.
> The concession was honest when written; the environment limitation behind it is gone. Findings
> below are preserved as the historical record — read them with this banner in force.

**Date:** 2026-07-29
**Workstream:** `cycle21-cut-lab`
**Plans reviewed:** `05-01-PLAN.md`, `05-02-PLAN.md`, `05-03-PLAN.md`
**Recorded baseline:** **`7fe8987b`** (`docs(05): fold plan review round 1 findings into phase plans`) — this is the commit the plans now sit at and the baseline for Round 2. Round 1 below reviewed the plans as they stood at `2f9ab5ef` (`docs(05): create archidekt bracket capture plans`), so every *plan* line number quoted in the Round 1 section is pre-fold and will not resolve against the current files. Every *source* line number still resolves: `git diff --name-only 2f9ab5ef 7fe8987b` returns only `.planning/**` docs, so no `.cs` file moved between the two rounds.

## Round 1 — initial plan review

**Verdict:** **CHANGES REQUIRED** — 3 BLOCK, 4 HIGH, 8 MEDIUM, 5 LOW after dedup. **All 20 findings folded at `7fe8987b`; all graded CLOSED by the Round 2 re-review below.**

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

> **⚠ CORRECTION (Round 3, 2026-07-29) — this resolution named two APIs that must not be used.** Left
> above verbatim as the historical record; superseded in the plans by Round 3 finding **H-1** (and
> **M-1** for the second half):
> - **`JsonElement.TryGetBoolean` does not exist.** Verified against this machine's exact SDK ref pack
>   (`Microsoft.NETCore.App.Ref/10.0.10/ref/net10.0/System.Text.Json.xml`): the `TryGet*` surface is
>   Byte, BytesFromBase64, DateTime, DateTimeOffset, Decimal, Double, Guid, Int16/32/64, Property,
>   SByte, Single, UInt16/32/64. The only boolean accessor is `GetBoolean`, which the same sentence
>   bans. The resolution text was folded verbatim into `05-01-PLAN.md` and graded CLOSED by Round 2,
>   so an executor would have hit `CS1061` against a hard whitelist with no guidance. **Corrected to:**
>   map `JsonValueKind.True`/`JsonValueKind.False` directly (no accessor call, cannot throw), with
>   `bool.TryParse` retained for boolean-valued strings.
> - **`TryGetDouble` has no legitimate consumer here and is actively harmful.** Verified by executing
>   the parses on .NET 10: `3.5` → `TryGetDouble` true (`3.5`), and `1e999` → `TryGetDouble` **true
>   with `∞`**. A `TryGetDouble → (int)` path therefore persists `3` for `3.5` and garbage for `1e999`.
>   `TryGetInt32` already returns `false` for both. **Corrected to:** `TryGetDouble` removed from the
>   whitelist; non-integer and overflow numerics are malformed and parse to null.
> - Also added while correcting the whitelist: `TryGetInt32` throws `InvalidOperationException` on a
>   non-`Number` element (verified), so the plan now mandates a `JsonValueKind` switch *before* any
>   try-parse call, and permits `GetString()` under a `JsonValueKind.String` guard as the one `Get*`
>   that cannot throw.

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

> **⚠ SUPERSEDED IN PART (Round 3, 2026-07-29) — replaced by ratified decision D-10.** Retained above
> verbatim, not deleted, because the reasoning is the record. What survives: the null-record half —
> a null-metadata URL re-mark must preserve prior captured values — is correct and is still the rule.
> What is replaced: the **non-null-record** half. Per-column `COALESCE` cannot deliver the arbitration's
> own stated intent that "a non-null one overwrites them", because a *captured* record whose
> `EdhBracket` is null (the legitimate captured-absent state under D-03) binds NULL and `COALESCE`
> keeps the stale value — producing a row that asserts "Archidekt declared bracket 3 at T2" when it
> declared nothing, permanently and undetectably, and making "captured, absent" unreachable on the URL
> path. **The side effect this arbitration did not weigh:** it reasoned from the single-field
> `commander_name` idiom at `DeckQueueRepository.cs:413`, and mirroring a one-field idiom across six
> fields is precisely what breaks it — the one-field case has no captured-but-absent state to lose.
> **D-10** (`05-CONTEXT.md`, user-ratified) replaces it: gate **per record, not per column**, via
> `CASE WHEN excluded.archidekt_metadata_captured_utc IS NULL THEN deck_queue.archidekt_x ELSE
> excluded.archidekt_x END`, which is dialect-safe for the same R-M4 reason. The test set was widened
> from a pair to a partition: Test 5 (all-non-null overwrite), **Test 5c (non-null record containing a
> null field — the case no prior test covered)**, Test 5b (null record preserves).

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
| BRKT-02 | 05-02, 05-03 | **Upgradable to full (2026-08-01)** — SQLite fresh + legacy migration, and Postgres is now runnable locally via Docker + `PostgresContainerFixture` (see banner). Add the `[PostgresFact]` and this becomes full coverage. |
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

---

# Round 2 — convergence re-review

**Date:** 2026-07-29
**Baseline:** HEAD `7fe8987b` (the Round 1 fold), plans as folded.
**Reviewer:** fresh-context Claude, general-purpose, claim-vs-code against real source — **substitute for Codex, which is out of credits.**
**Verdict:** **CHANGES REQUIRED** — 1 BLOCK, 3 MEDIUM, 2 LOW, 1 process note.

## ⚠ Assurance disclosure — this round is NOT cross-AI verification

Stated plainly, not buried: **Round 2 is same-family (Claude reviewing Claude) and does not satisfy the
cross-AI gate.** `CLAUDE.md` makes Codex the authoritative plan reviewer; the Codex re-review from the
Round 1 disposition remains **OWED** and is not discharged by this round. Codex was unavailable for the
same deterministic billing reason recorded in Round 1 (`ERROR: Your workspace is out of credits`).

**NEW-1 is direct evidence of the depth limit.** It is a compiler-provable wave-2 build break —
`CategoryKnowledgeStore.cs:110` forwards positionally, so widening `MarkUrlDeckProcessedAsync` binds
`CancellationToken` to `ArchidektDeckMetadata?` (`CS1503`) — and it survived **three** same-family
passes: a `gsd-plan-checker` run, a Round 1 claim-vs-code pass, and the Round 1 fold. Three Claude
readings did not catch a break a compiler catches in one second. That is the shape of the blind spot a
same-family reviewer cannot see past, and it is the argument for re-running the Codex gate before
execution, not merely before closeout.

Neither round ran `dotnet build` or `dotnet test`; NEW-1's `CS1503` is read from signatures, as Round 1's
compile-break claims were. No SQL was executed against Postgres in either round.

## Round 1 findings: all 20 CLOSED

Every Round 1 finding was verified as folded into the plans at `7fe8987b`:

| Severity | Findings | Status |
|----------|----------|--------|
| BLOCK | R-B1, R-B2, R-B3, R-B4 | **CLOSED** (4/4) |
| HIGH | R-H1, R-H2, R-H3, R-H4 | **CLOSED** (4/4) |
| MEDIUM | R-M1, R-M2, R-M3, R-M4, R-M5, R-M6, R-M7, R-M8 | **CLOSED** (8/8) |
| LOW | R-L1, R-L2, R-L3, R-L4, R-L5 | **CLOSED** (5/5) |

Both Round 1 user-decision items were resolved and ratified before the fold: R-M5 became **D-09** in
`05-CONTEXT.md` (ratify, no backfill), and R-H1 was resolved as **do not touch
`.github/workflows/ci.yml`** — the Postgres path stays unproven in CI as a recorded, accepted risk.

## Round 2 findings

| ID | Severity | Summary |
|----|----------|---------|
| NEW-1 | **BLOCK** | Wave 2 leaves `DeckFlow.Web` uncompilable. `05-02` widens both `DeckQueueRepository` mark-processed methods, but `CategoryKnowledgeStore.cs:110` forwards positionally (`CS1503`) and sat in `05-03`'s write set — one wave too late. The blindness is structural: `05-02` Tasks 1-2 verify only `DeckFlow.Core.Tests`, whose csproj references `DeckFlow.Core` + `DeckFlow.CLI` and **not** `DeckFlow.Web`, so they go green with `DeckFlow.Web` already broken. |
| NEW-2 | MEDIUM | `IsBoardCategory` attributed to the wrong file in `05-03-PLAN.md` and inside ratified decision **D-09** (`05-CONTEXT.md`). It is `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs:150`, applied at `:79` of that same file — not `AdminHarvestController.cs:152`. Attribution error only; **D-09's substance is unchanged and stays ratified.** |
| NEW-3 | MEDIUM | A `must_haves` proxy that fails on correct code — the inverse of the R-H3 defect class. `05-03`'s session `key_links` required the literal `metadata: import.Metadata`, but the plan's own instruction to return metadata from `PersistDeckAsync` produces a destructured `metadata: metadata` at the `RunAsync` call site. |
| NEW-4 | MEDIUM | Wave-1 tests had no seam for synthesized JSON. Tests 2, 3, 5 and 6 need arbitrary payload bodies, but both specified factories take a fixture **file name** and read from disk (`ArchidektApiDeckImporterTests.cs:36-48`), so the executor would have had to invent the content-accepting overload. |
| NEW-5 | LOW | Four count/citation errors: `05-01` "13 doubles across 12 files" (actually 13 files); `05-02` "18 `[Fact]`s … `:22-:478`" (actually 16 `[Fact]`s, 0 `[Theory]`s, 557-line file) — the load-bearing claim behind it, that no legacy or double-`EnsureSchemaAsync` test exists, is **confirmed true**; `05-03` `ContentHashDedupTests` `CreateSession` sites `:190/:224/:254/:281` (actually `:193/:226/:256/:283`); `05-03` `ImportAsync` at `:264` (actually `:265`). |
| NEW-6 | LOW | `05-01` Task 1 claimed all its tests fail red before Task 2. Test 5 exercises only `ImportAsync`, which ignores `edhBracket` today, so it is green at HEAD — correctly, as a regression guard that becomes falsifiable when Task 2 adds parsing. Needed an explicit carve-out so an executor does not "fix" it into something weaker. |
| NEW-7 | process | This document recorded its baseline as `2f9ab5ef`; the fold was committed as `7fe8987b`. |

## NEW-1 caller audit (full, so the BLOCK cannot recur)

Every call site of the two widened methods, checked at `7fe8987b`. Positional callers break with
`CS1503`; named callers are immune:

| Call site | Form | Effect | Owned by |
|-----------|------|--------|----------|
| `CategoryKnowledgeRepository.cs:257` | positional | breaks | `05-02` write set |
| `CategoryKnowledgeRepository.cs:300` | positional | breaks | `05-02` write set |
| `CategoryKnowledgeStore.cs:110` | positional | **breaks — the gap** | was `05-03`, now pulled into `05-02` |
| `ArchidektDeckCacheSession.cs:125`, `:137` | named (`skip:`, `cancellationToken:`) | safe | `05-03` (edits anyway) |
| `CategoryCacheSchemaParityTests.cs` ×12, `CategoryKnowledgeRepositoryTests.cs:388`, `ContentHashDedupTests.cs:306`, `PostgresStorageTests.cs:137`/`:161`, `CategoryKnowledgeStoreTests.cs:161-163` | 2 arguments | safe | — |
| `AdminHarvestController.cs:273` | calls the web store's 3-arg member, which `05-03` leaves untouched | safe | — |

R4's claim is **confirmed**: `CategoryKnowledgeStore.cs:110` is the only unprotected site outside the
plans' write sets. No additional site was found.

## Round 2 disposition

- **All 7 findings folded**, no user decision required. NEW-1 was fixed both ways: the file moved into
  `05-02` Task 2 with a named-argument instruction (the local fix), **and** `dotnet.exe build
  DeckFlow.sln --no-restore` was added to `05-02` Task 2 and `05-03` Task 2 (the structural fix, mirroring
  R-H4's addition to `05-01` Task 2). The build gate was deliberately **not** added to any TDD-red task,
  where a clean solution build is not a meaningful gate.
- **Codex authoritative re-review: still OWED.** Round 1 owed it and Round 2 does not discharge it.
  Re-run before execution if credits return; if they do not, the phase executes with two same-family
  review rounds and zero cross-AI verification, and NEW-1 is the recorded evidence for what that costs.

---

# Round 3 — three-reviewer convergence pass

**Date:** 2026-07-29
**Baseline:** HEAD `1511dd95` working tree, plans as folded at `7fe8987b`.
**Verdict:** **CHANGES REQUIRED** — 1 BLOCKER, 2 HIGH, 4 MEDIUM, 5 LOW. All folded in one consolidated pass.

## Reviewer roster

| Reviewer | Model | Lens | Verdict |
|----------|-------|------|---------|
| R5 | Claude Opus | proof-vs-claim delta against real source — re-verify every citation the plans make | CONVERGED (no new BLOCK from this lens) |
| R6 | **Fable 5** | full-plan read, cross-family | **CHANGES REQUIRED** — 2 HIGH |
| R7 | `gsd-plan-checker` (Claude, fresh context) | goal-backward: what would detect each success criterion being unmet | **FAIL** — 1 BLOCKER |

Two of the three findings graded most severe were independently re-verified by the LEAD before folding
(the phantom `TryGetBoolean` against the SDK ref pack; the `COALESCE` corruption against the live
`DeckQueueRepository` SQL). The fold worker re-verified all thirteen against real source before writing.

## Findings and disposition

| ID | Sev | Finding | Disposition |
|----|-----|---------|-------------|
| B-1 | **BLOCKER** | The URL write path's final persistence hop had no test that could fail. Bulk proves every hop to SQLite; URL proved controller→store (against `FakeCategoryKnowledgeStore`) and repository→`deck_queue`, but nothing proved `CategoryKnowledgeStore`'s new 4-arg overload → `CategoryKnowledgeRepository`. Load-bearing because `05-02` Task 2 rewrites the adjacent line in that same file and `05-03` Task 3 then says "add the overload beside it" — a metadata-dropping copy-paste of the neighbour is the most likely single mistake in the plan set, and **every command in `05-VALIDATION.md` stayed green if it happened.** | **APPLIED.** `DeckFlow.Web.Tests/CategoryKnowledgeStoreTests.cs` added to `05-03` `files_modified` + Task 3 `<files>`; one test specified against the file's existing real-store harness (`:180-228`, `CreateStore` `:261-262`, `store.DatabasePath` `CategoryKnowledgeStore.cs:62`), reading `deck_queue` back over a raw `SqliteConnection`. No production read API. `05-VALIDATION.md` row `05-03-03` updated; task count unchanged at eight (a test was added, not a task). |
| H-1 | HIGH | `05-01` Task 2 mandated `JsonElement.TryGetBoolean`, **which does not exist**, while banning `GetBoolean` — the only real boolean accessor. Executor hits `CS1061` against a hard whitelist with no guidance, on the very boolean path the whitelist exists to protect (the Postgres `42804` hazard). | **APPLIED.** Whitelist rewritten per field: booleans map `JsonValueKind.True`/`False` directly, `bool.TryParse` retained for boolean strings. Prohibition re-scoped to numeric/boolean/date `Get*` so `GetString()` under a `String`-kind guard stays legal and the sentence stays coherent. R-M1's resolution text — the origin of the phantom API — corrected in place with a dated correction block rather than a silent rewrite. |
| H-2 | HIGH | URL upsert's per-column `COALESCE` cannot express its own stated intent. A captured record with `EdhBracket = null` binds NULL, so the stale bracket survives and the row falsely attributes it to the later capture timestamp — permanent, undetectable, and divergent from the bulk path against D-05. **User ratified the fix.** | **APPLIED** as **D-10** in `05-CONTEXT.md`: gate per record via `CASE WHEN excluded.archidekt_metadata_captured_utc IS NULL ...`, valid on both dialects because Step A binds `captured` as text. `05-02` Task 2 rewritten with the SQL shape; R-M3 marked superseded-in-part, not deleted. Missing test added as **Test 5c** (non-null record containing a null field), completing a three-way partition with Tests 5 and 5b. |
| M-1 | MED | `edhBracket` non-integer numerics were unspecified (`3.5` fits neither "numeric" nor "malformed"), and `TryGetDouble` sat on the whitelist with no stated consumer — inviting `(int)3.5 → 3` from one executor and `null` from another, both passing every specified test. | **APPLIED.** Rule stated: non-integer and overflow numerics are malformed → null, which falls out of `TryGetInt32` alone. `TryGetDouble` removed from the whitelist with the reason recorded. Test 2's case list now enumerates `3.5` and `1e999` explicitly. |
| M-2 | MED | False fixture-uniqueness claim: the plan invited generalizing the string-replacement helper to `createdAt`/`updatedAt`, which would silently rewrite 79 card objects and could flip Test 5's 79-entry assertion. | **APPLIED.** Re-measured independently: `edhBracket`/`deckFormat`/`theorycrafted` = 1 each; **`createdAt` and `updatedAt` = 80 each** (1 top-level + 79 per-card). Sentence corrected to name only the three safe keys, with the full-`key:value` workaround stated for anything else. |
| M-3 | MED | `05-02` Tests 5 and 5b were in tension: the natural way to make Test 5's record "different" is to null a field, which reds a correct implementation under the old rule, and the likely repair reds 5b. | **APPLIED.** Test 5 now reads "all six values non-null and each differing from the first"; the three tests are introduced as one set that partitions the update branch on the *record*, consistent with D-10. |
| M-4 | MED | `05-03` Task 1's Tests 4/5 need a commander-bearing importer, but the `Build()`-parameterization instruction sat in Task 3, two tasks downstream. | **APPLIED.** Instruction moved into Task 1 with the concrete signature and all six existing `Build(...)` call sites enumerated; Task 3's text now reads as consuming that work rather than owing it. |
| L-1 | LOW | Test 2's "missing" case was assigned to `FixtureWithEdhBracket(...)`, which substitutes a value and cannot delete a key. | **APPLIED.** Sibling `FixtureWithoutEdhBracket()` specified, removing `"edhBracket":null,` including its trailing comma — verified valid because the key sits between `"deckFormat":3,` and `"game":null`, and the payload stays recognizably Archidekt so the case lands on "captured, absent" and not Test 6. |
| L-2 | LOW | "Add exactly one new plumbing method"; the fence below adds three plus a rewritten fourth, plus `FixtureWithEdhBracket`. | **APPLIED.** Count corrected and the additions itemized. |
| L-3 | LOW | "Test 5 passes immediately" — at the Task-1 gate the assembly does not compile at all. | **APPLIED.** Qualified: green the first time it *can* run, which is after Task 2; the regression-guard point is unchanged. |
| L-4 | LOW | `FactSnapshot` cited at `ContentHashDedupTests.cs:210`; it is declared at `:432` (`:210` is an assertion). | **APPLIED.** Corrected to `:432` (produced by `ReadFactSnapshotAsync` `:371`), `DeckQueueRow` `:434` (`ReadDeckQueueRowAsync` `:412-430`). |
| L-5 | LOW | `WSLENV` entry lacked the **`/w`** flag in `05-02-PLAN.md` and `05-VALIDATION.md`, so the Postgres gate would fail the same silent way the `WSLENV` fix was meant to prevent. | **APPLIED** in both files, plus the explanatory note (`/w` is WSL→Win32; `/u` is the wrong direction). |

## Two structural lessons — the durable value of this round

**1. B-1 was invisible to both proof-vs-claim reviewers, and that is a property of the lens, not of the
reviewers.** Every test the plans specify is real, every citation resolves, and R5 confirmed that. The
defect was a *missing* claim — an unproven hop between two proven ones — and nothing in a claim-vs-code
pass looks for absence. Only R7's goal-backward lens, which asks "what would detect this criterion being
unmet?", could surface it. Keep a goal-backward reviewer in the roster even when the claim-checkers
converge; the two lenses do not substitute for each other in either direction.

**2. Both of R6's HIGH findings were introduced by prior rounds' own fixes, and three same-model passes
propagated them.** The phantom `TryGetBoolean` originated in **R-M1's resolution text**, was folded into
`05-01-PLAN.md` verbatim, and was graded **CLOSED** by Round 2. The `COALESCE` corruption originated in
**R-M3's LEAD arbitration**. Both then survived a `gsd-plan-checker` run, a Round 1 claim-vs-code pass,
the Round 1 fold, and the Round 2 re-review — because each pass checked the plan against the *resolution*
rather than against the API surface and the SQL semantics. A different model family caught both in one
pass: one by checking the assembly, one by tracing what the SQL actually does to a null field.

This is the second recorded instance in this phase of same-family depth failing where a compiler or a
different model succeeds in one step — NEW-1 was the first. **The Codex authoritative cross-AI review
remains OWED**, from Round 1, undischarged by Rounds 2 and 3. Round 3 is direct evidence that adding more
same-family passes does not substitute for it: the two most severe plan defects at this point in the
phase were *created by* same-family review output and *ratified* by same-family review.

## Round 3 disposition

- **All 13 findings folded** in one consolidated pass across `05-01-PLAN.md`, `05-02-PLAN.md`,
  `05-03-PLAN.md`, `05-CONTEXT.md`, `05-VALIDATION.md`, and this file. No source file was touched;
  `.github/workflows/ci.yml` remains out of scope per the ratified user decision.
- **One new decision ratified by the user:** **D-10** (per-record URL-upsert gating), recorded in
  `05-CONTEXT.md` and superseding R-M3's non-null-record arbitration.
- **Codex authoritative review: still OWED.** Three rounds, zero cross-AI verification of the
  claim-vs-code kind `CLAUDE.md` requires.

---

# Round 4 — cross-family review plus compiler adjudication

**Date:** 2026-07-30
**Baseline:** plans as folded through Round 3; worktree HEAD `6f51bd7b` (`gsd/cycle21-cut-lab`).
**Verdict:** **CHANGES REQUIRED** — 1 HIGH, 2 MEDIUM, 3 LOW. **No BLOCKER.** All 6 folded in one consolidated pass.

## Reviewer roster

| Reviewer | Model | Lens | Verdict |
|----------|-------|------|---------|
| R8 | **Fable 5** | claim-vs-code, adversarial; explicitly instructed to distrust this file's own resolution text and verify against real source | **CHANGES REQUIRED** — 1 HIGH, 2 MEDIUM, 3 LOW |
| R9 | **the C# compiler** (`dotnet build DeckFlow.sln`, .NET 10) | proof-by-construction on a throwaway worktree: apply only the plans' interface deltas and see what the compiler says | **CONVERGED** — the interface design is correct |

Round 3 closed by recording that the Codex authoritative review was still owed and that
same-family passes were not substituting for it. Codex was re-probed at the start of this round
and returned `ERROR: Your workspace is out of credits.`, so Round 4 substituted the two lenses
above. **The Codex claim-vs-code review remains OWED** — see the disposition below.

## R9 — what the compiler settled

Throwaway branch `probe/p5-throwing-defaults` off `6f51bd7b`, applying *only* the two interface
deltas the plans specify — no concrete implementations — so the compiler answers the actual
question: do the unlisted implementers survive on the defaults alone? Branch and worktree
deleted after the run; nothing committed.

| Prior finding | Compiler evidence | Verdict |
|---|---|---|
| **R-B3** — widening with an optional parameter raises `CS0535` in 5 unlisted implementers | negative control reproduced `CS0535` at **all five cited lines exactly**: `CategorySuggestionServiceTests.cs:149`, `CutLabAnalysisContextBuilderTests.cs:662`, `CutLabPageServiceTests.cs:2918`, `HarvestStatsAggregatorTests.cs:67` and `:138`. A 6th hit, `FakeCategoryKnowledgeStore.cs:12`, is inside plan 05-03's own write set — so "five **unlisted**" is exactly right | plan **CORRECT** |
| **R-B3** — the throwing-default overload avoids it | both members as throwing defaults → **0 errors, 9 warnings**, the 9 being the pre-existing `CS8629` baseline in `ManabaseBaselineWeightingTests.cs` | plan **CORRECT** |
| **R-H4** — a Core interface change vs 10 unedited `DeckFlow.Web.Tests` implementers | `DeckFlow.Web.Tests` compiled clean on the throwing default | plan **CORRECT** |
| **Round 3 H-1** — phantom `JsonElement.TryGetBoolean` | `CS1061: 'JsonElement' does not contain a definition for 'TryGetBoolean'` | Round-3 fold **CORRECT** |
| plan 05-01's `TryGetDouble` ban and kind-guard rules | executed on .NET 10: `TryGetInt32` false for both `3.5` and `1e999`; `TryGetDouble` **true** for both (`3.5`, and `∞`→`(int)2147483647`); `TryGetInt32` throws `InvalidOperationException` on `String` and `Null` elements; `1e999` has `ValueKind.Number` | plan **CORRECT** on every claim |

Implementer censuses independently confirmed by both reviewers: `ICategoryKnowledgeStore` has
exactly **7** implementers, `IArchidektDeckImporter` exactly **14** across 14 files, with no
default members before this phase.

**A note worth keeping.** The negative control needed three builds to reach the test project:
`DeckFlow.Web` failing meant `DeckFlow.Web.Tests` never compiled, so the first control run
reported 1 error, not 6. A solution build stops at the first failing project and therefore
*systematically hides* downstream implementer breakage. This is independent evidence that
plan 05-01's insistence on running the full `dotnet build DeckFlow.sln` in **wave 1** rather
than wave 3 is load-bearing rather than ceremony.

## R8 findings and disposition

All six folded. Every one is a plan-text edit; none changes the design.

| ID | Sev | Finding | Where folded | Independently verified? |
|----|-----|---------|--------------|--------------------------|
| **F4-1** | **HIGH** | T-05-13's "cannot ship undetected" is false for `archidekt_theorycrafted`. Test 7 proves `From` is correct but nothing proves `DeckQueueRepository` *calls* it. A raw `bool?` bind is byte-identical to `From`'s `int?` on SQLite (`DapperTypeHandlers.cs:111-116`) and fails only on Postgres (`42804`), which is provable in zero environments. Timestamps *are* detectable, but only by an accident of format: the handler emits `DateTime`-`"O"` (`…Z`, `:148-154`) while `From` emits `DateTimeOffset`-`"O"` (`…+00:00`) | `05-02` Task 2 Step A (per-field detectability trace + prefer passing the parameters instance); `05-02` Task 1 (pin which `"O"`); T-05-13 row downgraded to **partial** with an explicit NOT-VERIFIED-on-Postgres carry into `05-02-SUMMARY.md`; Task 2 `<done>`; `05-VALIDATION.md` row `05-02-02` | **YES** — both handlers read at the cited lines |
| **F4-2** | MED | Test 7's type-only assertions pass over an inverted `Theorycrafted ? 0 : 1` mapping and over any wrong timestamp rendering; `Assert.IsType<int>` on the null case throws rather than failing meaningfully | `05-02` Task 1 Test 7 — assert values as well as types; `true`→`1`, `false`→`0`, `null`→`Assert.Null` | reasoning sound; not re-executed |
| **F4-3** | MED | 05-03 Test 2's second harvest pass is impossible as specified — the 5-day `DeckRefreshCooldown` requeue predicate means a naive second `RunAsync` processes zero decks | `05-03` Task 1 Test 2 — backdate `last_checked_utc` and re-run `AddDeckIdsAsync`, copying `ContentHashDedupTests.SetLastCheckedUtcAsync` | **YES** — helper at `ContentHashDedupTests.cs:345`, used 5×; `ArchidektDeckCacheSessionTests.cs` has zero occurrences |
| **F4-4** | LOW | D-10's SQL fragment never says to extend the INSERT column list, which the `excluded.*` discriminator depends on — absent columns make `excluded.<col>` NULL, so the discriminator reads "caller passed null" on every conflict | `05-02` Task 2 Step B — explicit widen-the-INSERT instruction; `05-VALIDATION.md` row `05-02-02` | not independently verified (standard SQL semantics) |
| **F4-5** | LOW | `using DeckFlow.Core.Integration;` is needed in `ICategoryKnowledgeStore.cs` too, since the new default member's signature names `ArchidektDeckMetadata?`; the plan names only `CategoryKnowledgeStore.cs` | `05-03` Task 3 | **YES** — `CS0246` reproduced on the probe branch |
| **F4-6** | LOW | 05-01's third artifact gate has no `contains:` and the file exists at HEAD, so it cannot fail | `05-01` frontmatter — added `contains: "ImportWithMetadataAsync"` | **YES** — `git cat-file -e HEAD:` confirms |

## One claim retracted, for the record

An earlier pass in this round reported the `CS1503` positional-caller break at
`CategoryKnowledgeStore.cs:110` as a **new** finding the reviews had missed. That was **wrong**:
it is already fully documented at `05-02-PLAN.md:95-98` (complete caller audit) and recorded in
this file as Round 3's **NEW-1 BLOCK** with a per-caller table, having been pulled from 05-03's
write set into 05-02. R8 caught the error. Recorded so the correction is durable and so the
finding is not re-raised a third time.

## The structural lesson of this round

**The two lenses caught disjoint defect sets, and neither could have found the other's.** R9
found nothing R8 found; R8 found nothing R9 could reach. That is not redundancy failing — it is
the lenses being genuinely orthogonal:

- The compiler adjudicates *existence and signature* questions absolutely — `CS1061`, `CS0535`,
  `CS1503`. Rounds 1-3 spent three same-family passes on exactly this class and still shipped a
  phantom API into a folded plan. One build settled all of it.
- The compiler is blind to F4-1, because F4-1 **is** a statement about undetectability. No build
  can surface a defect whose whole nature is that every runnable environment stays green.

The practical consequence for the remaining phases: reach for a throwaway-branch build *before*
adding another review round whenever the open questions are about APIs, signatures, or
implementer counts — it is faster, free, and decisive. Reserve model review for the questions a
compiler cannot express, which is where F4-1 lived.

## Round 4 disposition

- **All 6 findings folded** across `05-01-PLAN.md`, `05-02-PLAN.md`, `05-03-PLAN.md`,
  `05-VALIDATION.md`, and this file. No source file was touched. `.github/workflows/ci.yml`
  remains out of scope per the ratified user decision.
- **No new decision required.** F4-1's fix is honest rewording plus one test-computation
  pin; D-10 stands as ratified in Round 3.
- **The three BLOCK-class interface findings that gated this phase (R-B3, R-H4, Round 3 H-1) are
  now discharged with compiler evidence** rather than review consensus.
- **Codex authoritative review: still OWED**, now across four rounds. Blocked on workspace
  credits, re-probed and confirmed dead this round. The nearest available substitute is
  `cursor-agent --mode=plan --model gpt-5`, which is installed but **not logged in** and needs
  the developer to authenticate.
- **No BLOCKER outstanding.** Nothing in Round 4 blocks execution once the fold is committed.

---

# Round 5 — 2026-08-01. THE OWED CODEX REVIEW, finally run. `gpt-5.6-sol`, medium, read-only.
# Verdict: CHANGES REQUIRED.

1 BLOCK · 2 HIGH · 1 MEDIUM · 1 LOW. **NOT folded yet.**

⚠ **This overturns Round 4's closing line.** Round 4 ended "No BLOCKER outstanding — nothing blocks
execution once the fold is committed", while recording that the authoritative Codex review was owed
across four rounds and blocked on credits. Credits were restored 2026-08-01 and the review found a
BLOCK on its first run. Round 4's all-clear was a same-family verdict standing in for a gate that had
never actually run. **Do not treat an owed gate as discharged by the reviews that ran in its absence.**

**B1 — BLOCK. The red-test tasks cannot satisfy their own automated gates.** `05-01:161-168` requires
tests 1/2/3/4/6 to be RED, then uses a plain `dotnet test` as the task's `<automated>` verification;
`05-02:149-154` and `05-03:141-145` do the same. A GSD executor treats failed task verification as
blocking, so a nonzero `dotnet test` can never clear the task — the plan instructs the executor into a
state its own gate rejects. The tests are genuinely red at that point: `ImportWithMetadataAsync` does
not exist (`DeckImporterInterfaces.cs:54-63`), `CategoryKnowledgeRepository` has neither metadata
parameter (`:252-300`), and both call sites still use the old paths (`ArchidektDeckCacheSession.cs:109-137`,
`AdminHarvestController.cs:265-273`). → Fix: either merge each red task into its implementation task as
one RED→GREEN TDD task, or replace the `<automated>` entry with an expected-failure harness that exits
0 only when the intended tests fail for the intended reason — with the later GREEN gate running a
normal `dotnet test`. Confidence 10/10.

**H1 — HIGH. The `[PostgresFact]` upgrade is an INCOMPLETE FOLD — the same anti-pattern that cost
Phase 4 three rounds.** `05-02:177-189` correctly mandates a required `[PostgresFact]` on the grounds
that Postgres became provable. Four operative copies still contradict it: `05-02:232` (routing enforced
by review only, Postgres unprovable), `05-02:245-258` (Core mapping test "authoritative", Postgres an
accepted risk, `NOT VERIFIED on Postgres` permitted), `05-02:284` (repeats the fallback), and
`05-VALIDATION.md:47,68` (retains the fallback and the "provable in no environment" claim). The risk is
concrete, not theoretical: `BoolTypeHandler` binds SQLite as `1/0` but Postgres as a native boolean
(`DapperTypeHandlers.cs:98-116`), so a SQLite-only proof cannot cover `Theorycrafted`. Infrastructure is
confirmed present — `[PostgresFact]` gates on `DECKFLOW_POSTGRES_TESTS == "1"`
(`PostgresFactAttribute.cs:10-18`) and `PostgresContainerFixture` starts the container (`:36-86`).
→ Fix: one fully specified required `[PostgresFact]` driving `CategoryKnowledgeRepository` /
`DeckQueueRepository` with a non-null `Theorycrafted`, asserting all six values round-trip, run in the
task that implements the repository; then delete every "authoritative Core substitute", "unprovable",
"accepted risk" and optional NOT-VERIFIED copy. Confidence 10/10.

**H2 — HIGH. The importer tests mutation-lock only one of the parsers they claim to cover.**
`05-01:98` promises the complete numeric matrix for both `edhBracket` and `deckFormat`, and
`:184-195` specifies boolean and timestamp parsing plus no-throw behavior — but the actual recipe at
`:136-158` mutates **only** `edhBracket`. The fixture supplies a single `deckFormat: 3`, a single
`theorycrafted: false`, and valid timestamps, against 80 occurrences each of `createdAt`/`updatedAt`.
The current importer ignores every top-level field and parses only `cards[]`
(`ArchidektApiDeckImporter.cs:56-105`). Three mutations survive: hard-coding `Theorycrafted = false`,
rejecting a numeric-string `deckFormat`, and using a throwing accessor for malformed timestamps.
→ Fix: add field-specific synthesis cases for `deckFormat` and `theorycrafted` (true / false / string /
null / missing / malformed), plus malformed and missing top-level timestamp cases using unique full
`key:value` replacement, asserting both nullable output and unchanged `ImportAsync` entries.
Confidence 9.5/10.

**M1 — MEDIUM. The store-to-repository test permits partial metadata loss.** `05-03:195-200` calls it
the proof that the four-argument store overload forwards metadata, but reads back only
`archidekt_edh_bracket` and `archidekt_metadata_captured_utc`. The adapter is a one-line forward
(`CategoryKnowledgeStore.cs:109-110`) and the planned overload is the sole hop between controller and
repository, so forwarding `metadata with { Theorycrafted = null, CreatedUtc = null, UpdatedUtc = null }`
keeps both assertions green while silently dropping curated provenance. → Fix: assert exact round-trip
values for all six columns. Confidence 9/10.

**L1 — LOW, and directly confirmed by observation this session.** `05-01:200`, `05-02:256` and
`05-03:207` end with `scripts/format-check-changed.sh staged`, which reads only `git diff --cached`
(`:290-295`) and exits successfully printing "no changed C# files" (`:304-307`). Every planning commit
made on 2026-08-01 printed exactly that line, so the gate demonstrably passes without inspecting
anything. → Fix: stage the task-owned C# files before the command, or use a working-tree mode covering
unstaged changes. Confidence 8.5/10.

## Status

Round 5 **not folded.** Round 6 owed after folding. The Codex authoritative review is no longer owed —
it ran, and it disagreed with the standing verdict.
