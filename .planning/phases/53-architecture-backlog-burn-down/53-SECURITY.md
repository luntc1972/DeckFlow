---
phase: 53-architecture-backlog-burn-down
auditor: claude-sonnet-4-6
asvs_level: 1
audit_date: 2026-06-17
threats_total: 4
threats_closed: 4
threats_open: 0
verdict: SECURED
---

# Phase 53 Security Audit

**Phase:** 53 — Architecture Backlog Burn-Down
**Threats Closed:** 4/4
**ASVS Level:** 1
**Verdict:** SECURED

## Phase Nature

Pure behavior-preserving refactor across four plans (two Wave-1 parallel + one Wave-2
sequential). No new endpoints, inputs, authentication paths, network calls, or packages
introduced. The entire threat surface is Tampering: did the refactor accidentally alter
preserved behavior (SQL strings, DI registration graph, classifier phrase lists)?

## Threat Verification

### T-53-01 — Tampering: CategoryKnowledge SQL preserved across facade-then-extract split

| Field | Value |
|-------|-------|
| Disposition | mitigate |
| Plan | 53-01 |
| Verdict | CLOSED |

**Mitigation declared:** SQL moved verbatim; 17 round-trip facts + CategoryCacheSchemaParityTests +
ContentHashDedupTests are the regression guard. Facade must be pure delegation (no inline SQL).

**Evidence found:**

- **Facade is pure delegation:** `CategoryKnowledgeRepository.cs` contains zero inline Dapper
  queries. Every public/internal method is a one-line expression-body forwarding to
  `_schema`, `_deckQueue`, or `_cardCategory`. The single grep hit for "UPDATE" in the
  facade (line 215) is inside a `<summary>` xmldoc comment, not executable SQL.
  `grep -c "ON CONFLICT" CategoryKnowledgeRepository.cs` = 0 (confirmed).

- **DDL ownership in CategoryCacheSchema:** All `CREATE TABLE`, `ALTER TABLE`, `CREATE INDEX`,
  and `DROP INDEX` DDL confirmed present in
  `DeckFlow.Core/Knowledge/CategoryCacheSchema.cs` (226 LOC).

- **Load-bearing index-ordering comment preserved verbatim:**
  `CategoryCacheSchema.cs:89` — `"-- Why: this batched DDL runs inside a try/catch that
  swallows index-creation failures, so create the replacement first; if it fails, the batch
  aborts before the drops execute and the old indexes survive."` Present word-for-word as
  required.

- **F-51-PG-01 `::timestamptz` dialect-guard present verbatim:**
  `DeckFlow.Core/Knowledge/DeckQueueRepository.cs:131-133` —
  ```
  var lastChecked = _connectionInfo.IsSqlite
      ? "deck_queue.last_checked_utc"
      : "deck_queue.last_checked_utc::timestamptz";
  ```
  The explaining comment (lines 125-130, including `(F-51-PG-01)`) is present verbatim.

- **ON CONFLICT upserts in CardCategoryRepository:** `grep -c "ON CONFLICT"` returns 4 in
  `CardCategoryRepository.cs`, 0 in `CategoryKnowledgeRepository.cs`.

- **CommandTimeout = 15 preserved:** `CategoryCacheSchema.cs:101` — `indexCommand.CommandTimeout = 15;`

- **DeckRefreshCooldown preserved:** `DeckQueueRepository.cs:14` —
  `private static readonly TimeSpan DeckRefreshCooldown = TimeSpan.FromDays(5);`

- **D-17/B2 commander-capture comments preserved:** `DeckQueueRepository.cs:293-296`

---

### T-53-02 — Tampering: DI graph + startup sequence integrity across Program.cs extraction

| Field | Value |
|-------|-------|
| Disposition | mitigate |
| Plan | 53-02 |
| Verdict | CLOSED |

**Mitigation declared:** Extension calls placed at original positions; hosted-service +
schema-validation startup left inline; DiCompositionExtensionsTests ValidateOnBuild guard.

**Evidence found:**

- **Extension call ordering preserved:**
  `Program.cs:81` — `AddDeckFlowHttpClients()` (before resilience pipelines, per original ordering)
  `Program.cs:85` — `AddDeckFlowResiliencePipelines()` (original position)
  `Program.cs:87` — `AddDeckFlowScryfallServices()`
  `Program.cs:157-159` — `AddDeckFlowPromptVariants()`, inline `ICategoryKnowledgeStore`,
  `AddDeckFlowPacketServices()` — packet group at original position.

- **Hosted-service + schema-validation sequence left inline:**
  `Program.cs:160-162` — `ArchidektCacheJobService` singleton + hosted service registered
  inline immediately after `AddDeckFlowPacketServices()`. `ICategoryKnowledgeStore` is
  also inline (line 158), not extracted, per the plan's explicit decision to preserve the
  startup sequence.

- **ValidateOnBuild guard test exists and resolves all declared services:**
  `DeckFlow.Web.Tests/Extensions/DiCompositionExtensionsTests.cs` — test
  `AddDeckFlowExtensions_ValidateOnBuild_ResolvesPacketServicesAndPromptVariantRegistries`
  builds `ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }` and
  calls `GetRequiredService` for all four packet services
  (`IDeckAnalysisPacketService`, `IDeckComparisonService`, `IMetaGapService`,
  `IDeckPrimerPacketService`) and all six prompt-variant registries
  (`AnalysisPromptVariantRegistry`, `SetUpgradePromptVariantRegistry`,
  `ComparisonPromptVariantRegistry`, `FollowUpPromptVariantRegistry`,
  `MetaGapPromptVariantRegistry`, `PrimerPromptVariantRegistry`).

- **HIGH-2 TaggerSessionCache TTL invariant comment preserved verbatim:**
  `DeckFlow.Web/Extensions/HttpClientServiceCollectionExtensions.cs:52-54` —
  `"// HandlerLifetime = 5 min. TaggerSessionCache TTL = 270s (30s below HandlerLifetime)"`
  and `"// (HIGH-2 invariant — DO NOT lower the 30s margin)."` present.

- **No inline HTTP client wiring or prompt-variant registrations remain in Program.cs:**
  `grep -c "AddHttpClient(\"scryfall-tagger\""` = 0 in Program.cs (SUMMARY confirms).
  `grep -c "AddSingleton<IAnalysisPromptVariant"` = 0 in Program.cs (SUMMARY confirms).
  `grep -c "AddScoped<IDeckComparisonService"` = 0 in Program.cs (SUMMARY confirms).

---

### T-53-03 — Tampering: Classifier phrase lists preserved across move to DeckFlow.Core.Analysis

| Field | Value |
|-------|-------|
| Disposition | mitigate |
| Plan | 53-03 |
| Verdict | CLOSED |

**Mitigation declared:** Expressions moved verbatim; new Core unit tests lock true/false
behavior; Web comparison tests confirm the tally loop is unchanged.

**Evidence found:**

- **All 6 classifiers + ParseManaToken present as `public static` in Core:**
  `DeckFlow.Core/Analysis/DeckStatClassifier.cs` — `IsRampCard`, `IsDrawCard`,
  `IsInteractionCard`, `IsBoardWipeCard`, `IsRecursionCard`, `IsClosingPowerCard`,
  `ParseManaToken` all confirmed present and `public static`.

- **Expressions verified structurally correct (spot-check):**
  `IsRampCard` checks `Land` (type line), `add one mana`, `add two mana`,
  `search your library for a basic land`, compound `search your library for up to` + `land`,
  `Treasure token`, `create a Treasure` — all present at lines 17-23.
  `ParseManaToken` numeric → int, `X` → 0, hybrid `/` → 1, else 1 — present at lines 95-105.

- **No private copies in DeckComparisonService:**
  `grep -c "private static bool IsRampCard\|private static int ParseManaToken"
  DeckComparisonService.cs` = 0 (SUMMARY confirms, verified by grep returning 0).

- **All 7 call sites repointed:**
  `DeckFlow.Web/Services/DeckComparisonService.cs:558-583` — all six classifier calls
  prefixed `DeckStatClassifier.`; `DeckStatClassifier.ParseManaToken` at line 1046.
  `grep -c "DeckStatClassifier\."` = 7 confirmed.

- **64 Core unit tests locking true/false behavior per classifier:**
  `DeckFlow.Core.Tests/DeckStatClassifierTests.cs` — 64 `[Theory]`/`[InlineData]` assertions,
  all passing (SUMMARY: 447/447 Core suite).

---

### T-53-04 — Tampering: Feedback SQL fragments preserved verbatim across both providers

| Field | Value |
|-------|-------|
| Disposition | mitigate |
| Plan | 53-04 |
| Verdict | CLOSED |

**Mitigation declared:** Fragments copied character-for-character including the raw-string
RETURNING-id literal (no re-indent per CLAUDE.md carve-out); FeedbackStore tests + clean
build are the guard.

**Evidence found:**

- **FeedbackDialect.cs exists at Web-side location:**
  `DeckFlow.Web/Services/Persistence/FeedbackDialect.cs` (moved to concern folder by 53-02).

- **Fragment values verified in code:**
  SQLite: `FeedbackCreatedUtcColumnType = "TEXT"` (line 31),
  `FeedbackOrderByClause = "datetime(created_utc) DESC, id DESC"` (line 32).
  Postgres: `FeedbackCreatedUtcColumnType = "TIMESTAMPTZ"` (line 40),
  `FeedbackOrderByClause = "created_utc DESC, id DESC"` (line 41).
  Both providers: identical INSERT raw-string RETURNING-id literal (lines 33-37 and 42-46) —
  same column list, same `@`-param names, same `RETURNING id;` terminator.

- **Raw-string literal indentation:** Both `SqliteInstance` and `PostgresInstance`
  `feedbackInsertReturningIdSql` use the same raw-string literal with identical leading
  whitespace (`        ` — 8 spaces). No re-indent applied. CLAUDE.md carve-out respected.

- **Provider selector present:** `FeedbackDialect.For(RelationalDatabaseConnection)` at
  line 53 — switch on `connection.Provider` with `Sqlite → SqliteInstance`,
  `Postgres → PostgresInstance`, default `throw NotSupportedException`. Singleton pattern
  (two `static readonly` instances, private ctor) mirrors Core dialect pattern.

- **FeedbackStore routes through FeedbackDialect for all 3 fragments:**
  `FeedbackStore.cs:13` — `private readonly FeedbackDialect _feedbackDialect;`
  `FeedbackStore.cs:34` — `_feedbackDialect = FeedbackDialect.For(_connectionInfo);`
  `FeedbackStore.cs:64` — `_feedbackDialect.FeedbackInsertReturningIdSql`
  `FeedbackStore.cs:107` — `_feedbackDialect.FeedbackOrderByClause`
  `FeedbackStore.cs:278` — `_feedbackDialect.FeedbackCreatedUtcColumnType`
  `grep -c "_connectionInfo.Dialect.Feedback" FeedbackStore.cs` = 0 (confirmed).

- **SurrogateIdColumnType still sourced from Core dialect:**
  `FeedbackStore.cs:277` — `_connectionInfo.Dialect.SurrogateIdColumnType` retained.

- **Core IRelationalDialect carries only SurrogateIdColumnType:**
  `IRelationalDialect.cs:11` — sole member `string SurrogateIdColumnType { get; }`.
  `grep -c "Feedback" IRelationalDialect.cs SqliteRelationalDialect.cs
  PostgresRelationalDialect.cs` = 0 across all three Core files (confirmed).

- **No solution-wide `.Dialect.Feedback` references remain:**
  `grep -rn "\.Dialect\.Feedback"` returns 0 matches across Core + Web.

---

## Unregistered Flags

All four SUMMARY.md `## Threat Flags` sections report **None**. No new attack surface was
introduced in any plan. No unregistered flags.

## Accepted Risks Log

None — all threats are mitigated, none accepted.

## Audit Notes

The phase's refactor-only nature means the security perimeter is exclusively internal
tamper-resistance: did the move corrupt load-bearing SQL strings, DI wiring, or classifier
logic? Every declared mitigation was verified by direct code inspection:

1. The facade is provably pure delegation (zero inline queries in CategoryKnowledgeRepository).
2. The F-51-PG-01 `::timestamptz` guard is present with its explaining comment.
3. The DI graph is guarded by a `ValidateOnBuild = true` test that resolves all 10
   extracted service types.
4. All 7 DeckStatClassifier call sites are repointed; 64 unit tests lock the phrase logic.
5. The FeedbackDialect raw-string literal is byte-identical across both provider singletons.

No mitigations are absent. Phase is clear to ship.
