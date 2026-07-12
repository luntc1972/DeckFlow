# Phase 97: Profile Fusion + Conflict Ledger - Context

**Gathered:** 2026-07-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Reconcile the Phase 95 **measured** style metrics against the Phase 96 **stated**
rules into weighted numeric `FusedTarget`s plus a **say-vs-do conflict ledger**.
Admin-visible only (a read-only Studio page) — **no public UI, no LLM call**. The
fusion + conflict computation is **pure-Core, fully unit-tested** (CS-20) — this is
the deterministic, falsifiable rubric the later artifact engine (Phase 99) diffs
against.

Requirements: **CS-16, CS-16a, CS-17, CS-18, CS-19, CS-20** (see REQUIREMENTS.md).

**In scope:** additive `FusedTarget` extension; the fusion join on `(metric, condition)`;
band-relative conflict detection with a coverage floor; recency-collapse of stated
rules; a read-only Studio ledger page.
**Out of scope:** the artifact/prompt engine (P99), the public tool surface + flag (P100),
the Scryfall card-grounding guard (P98), any mass backfill of the 106-artifact corpus.

</domain>

<decisions>
## Implementation Decisions

### CI-2 calibration timing — ground on prototype now, gate the distill in plan-phase (Area 1)
- **D-01:** **A live Snail re-distill is NOT feasible cheaply this session** and was
  ruled out on evidence, not preference: (a) the live studio DB
  (`/mnt/c/DeckFlowData/studio/content-kb.db`) has **no Salubrious Snail source/video/transcript**
  (enabled sources are 3/3 Elk, Play to Win, Command Zone, Based Deck Dept, ComedIan MTG);
  (b) the only in-repo Snail transcript (`DeckFlow.Core.Tests/StatedRulesExtraction/Fixtures/salubrious-snail-transcript.txt`)
  is a **6-line synthetic stub**, not a real transcript; (c) the git-shipped
  `content-kb/salubrioussnail/*.md` are **pre-P96 artifacts (Jul 6)** with **no `stated_rules:` block**;
  (d) transcript-harvest tooling (yt-dlp / captions + Whisper/ffmpeg fallback) is **not on PATH**;
  (e) the live studio DB was **mid-write by another session** (creator inserts, 11:28).
  The `distill` CLI (`RunDistillAsync`, `ThrowingTranscriptSource`) requires an
  already-harvested transcript, so there was nothing to distill.
- **D-02:** **Ground the fusion thresholds/weighting on the REAL prototype data in
  `docs/research/p89-p90-prototype-snail.md` now.** The Fable P90 prototype (2026-07-05)
  already ran fusion on **39 real Snail decks + 27 real stated rules** and produced a
  discriminating say-vs-do table (land 37-42 vs avg 37.4 ✅; ramp 7-12 vs avg 12.0 ✅;
  draw 13-18 vs avg 11.1 ⚠ mild; wipes 3-5 vs ~1.2 ✅-**philosophy-match**; counters ≥8
  vs control-only ⚠). This IS empirical grounding — it just came from the prototype, not
  a fresh distill.
- **D-03 (⚠ plan-phase gate — MANDATORY):** **Plan-phase MUST add an isolated
  harvest+distill pre-step** (throwaway temp DB via `distill --db /tmp/...`,
  `DECKFLOW_LLM_PROVIDER=claude` subscription = $0, proper harvest tooling verified)
  to confirm the **shipped P96 Select/Disambiguate/Decompose prompts actually reproduce
  the ~27 prototype rules** (37-42 lands, 8-14 removal, ≥8 counters, 3-5 wipes) BEFORE the
  executor locks final fusion numbers. Do NOT mutate the live studio corpus for this —
  use a temp DB. This retires the CI-2 prompt-quality risk; the prototype table
  (D-02) covers the fusion *design* in the meantime.

### Conflict threshold form — band-relative % + coverage floor (Area 2)
- **D-04:** **Conflict form = band-relative % beyond the stated band edge.** A conflict
  fires when the measured value is outside `[value_min, value_max]` by more than X% (of the
  band width or edge value). **Scale-free** — one X travels across land counts (~40) and
  tutor counts (~3) without a per-metric magnitude table. The exact X is an empirical
  number locked at plan/executor time against D-02/D-03 data, not now.
- **D-05:** **A coverage floor gates whether a conflict may fire at all.** The measured
  leg is sparse (prototype: draw labeled on ~28% of decks, wipes ~3%). A conflict fires
  **only if** the measured metric meets a minimum effective-sample / label-coverage floor
  — **reuse the P95 `MetricDistribution.EffectiveSampleSize`** already on `MeasuredMetric`.
  Below the floor → the ledger row is marked **`insufficient-measured`** (informational),
  **never a conflict**. Prevents the 3%-labeled wipes metric from screaming false hypocrisy.

### Weighting, fusion resolution & supersession (Area 3)
- **D-06:** **Weight assignment = hard partition by metric key (CS-17).** Each metric key
  is statically classified **observable** (counts/curve/ratios) or **philosophy**
  (un-measurable stances) via a vocabulary map:
  - **Observable** → resolved target = the **measured value**; the stated band is retained
    as the guard/ledger reference and drives conflict detection.
  - **Philosophy / stated-only** (no P95 counterpart, e.g. `power_level_philosophy`) →
    resolved target = the **stated value**; there is no measured value, so these
    **never produce a conflict** (this is the CI-1 stated-only routing, made concrete).
  Fully deterministic + unit-testable. No synthetic blended numbers (a blend would match
  neither the creator's words nor their decks and be indefensible in the ledger).
- **D-07:** **`FusedTarget` retains everything, additively (CI-1).** Per CS-16 the fused
  target keeps: the stated band (`statedMin`/`statedMax` or a stated-rule reference), the
  measured value + `numDecks`/coverage/distribution, the resolved target, `weight`,
  `source`, and a populated `conflict` payload (stated + measured numbers + which won).
  **Additive-only** — do not break the P94 `FusedTarget`/`FusedConflict` round-trip tests
  already green. The current `FusedTarget` (single `Value`) and `FusedConflict`
  (stated/measured/delta) in `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` are the
  extension target.
- **D-08:** **The fusion join keys on `(metric, condition)`, never `metric` alone (CS-16a).**
  P96 captured `condition` (`archetype:control`, `curve:low`, `bracket:2`) specifically so a
  conditional rule does not create a false delta against an unconditional aggregate. This is
  the ROADMAP's "highest-risk modeling decision this cycle." Stated `metric` vocabulary was
  aligned to the P95 `MeasuredMetric` keys on purpose — join on those exact keys.
- **D-09:** **Recency-collapse stated rules BEFORE fusion (retires D-04 from Phase 96).**
  When two stated rules share the same `(metric, condition)`, keep the **newest by
  `video_date`** and shadow the older before joining to measured — prevents stale positions
  from creating phantom conflicts. The **superseded rule stays visible in the ledger as
  history**, not as an active target.
- **D-10:** **`confidence` is a coarse band, not a precise multiplier.** It is an
  uncalibrated LLM scalar — treat as low/med/high, informational in the ledger; it does not
  scale the fused number (D-06 picks, it does not blend).

### Conflict ledger surface — read-only Studio page (Area 4)
- **D-11:** **The CS-19 say-vs-do ledger renders as a new read-only Studio Blazor page**,
  neighbor to `DeckFlow.Studio/Pages/CreatorSources.razor` / `Harvest` / `Publish`. Studio
  is the operator's creator-profile workbench, loopback-only (**no public/theme/mobile
  surface** — the web-page tests+themes+mobile rule does not apply), and already reads the
  content-KB. Fused-profile data is local tooling data; it is NOT synced to the deployed
  app this phase.
- **D-12:** **Each ledger row = the full say-vs-do row**, per `(metric, condition)`:
  stated band · measured value + `numDecks`/coverage · resolved target · **verdict badge**
  (`agree` / `conflict` / `insufficient-measured` / `philosophy-stated-only`) · source-clip
  link + `video_date`. Mirrors the prototype's discriminating table so the operator sees
  **why** each verdict landed — including case-(ii) "deviates-from-canon-but-matches-own-
  philosophy" (the flagship board-wipes story), which a conflicts-only view would hide.

### Claude's Discretion
- Exact numeric X for the band-relative % (D-04) and the coverage-floor value (D-05) —
  empirical, locked at plan/executor time against the D-02 prototype table + D-03 confirmation run.
- The precise additive field names/shape on `FusedTarget` (D-07) — planner's call, subject
  to the additive-only / round-trip-preserving constraint.
- Studio page layout details beyond the D-12 row contract.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Grounding data (empirical — read FIRST)
- `docs/research/p89-p90-prototype-snail.md` — the REAL Fable P90 fusion table (39 decks,
  27 stated rules) that grounds every threshold/weighting decision here (D-02). The
  say-vs-do verdicts (land/ramp/draw/wipes/counters) are the calibration target.
- `docs/research/creator-style-roadmap.md` — the locked Cycle-17 arc (Foundation →
  {Extractor, Distiller} → {Fusion, Guard} → Artifact → Surface).

### Requirements & roadmap
- `.planning/REQUIREMENTS.md` §CS-16..CS-20 — the locked requirements for this phase.
- `.planning/ROADMAP.md` — Phase 97 boundary + Cycle-17 design stance.

### Schema to EXTEND (additively)
- `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` — `FusedTarget` (single `Value` today —
  extend per D-07), `FusedConflict` (stated/measured/delta today), `MeasuredMetric` +
  `MetricDistribution.EffectiveSampleSize` (reuse for D-05 coverage floor), `StatedRule`.
  P94 round-trip tests are green — keep them green.
- `DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRuleCandidate.cs` — the P96 stated
  shape fusion consumes: `Category, Metric, Value, ValueMin, ValueMax, Comparator, Condition,
  ClipTimestampSeconds, SourceClip, Confidence, CardReference, CardGrounded`.
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` — `SerializeStatedRules` + the
  `stated_rules:` YAML block contract (the P96 single locked contract; snake_case keys
  incl. `video_date`). This is the stated ingestion path fusion parses (CI-1).

### Upstream inputs (COMPLETE)
- `DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs` — Phase 95 measured
  input; its `MeasuredMetric` keys are the fusion join target (D-08). COMPLETE + verified.
- `.planning/phases/96-stated-rules-distiller/96-CONTEXT.md` — P96 decisions D-01..D-07
  (metric-vocab alignment D-02a, `condition` D-02, `video_date` D-04, no-backfill D-05/D-05-DEP).

### Surface pattern
- `DeckFlow.Studio/Pages/CreatorSources.razor` — the neighbor Studio page pattern for D-11.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MetricDistribution.EffectiveSampleSize` (already on `MeasuredMetric`, P95 D-10) — the
  coverage-floor signal for D-05; no new plumbing needed.
- `FusedTarget` / `FusedConflict` records — extend additively, do not replace (P94
  round-trip tests depend on the existing shape).
- `ContentArtifactSpec.SerializeStatedRules` + `content_stated_rules` rows — the stated
  input surface; fusion reads the `stated_rules:` artifact/DB contract, NOT the (lossy) P94
  `ICreatorStyleProfileStore` stated slot (CI-1).
- Studio pages (`CreatorSources`/`Harvest`/`Publish`) + `StudioComponentBase` — the D-11 page host.

### Established Patterns
- Pure-Core, dialect-guarded persistence (`CreatorStyleProfileStore`) — fusion computation
  is Core + unit-tested (CS-20); the Studio page is a thin read-only view over it.
- P95/P96 substrate-only phases persist to the content-KB and are consumed by later phases —
  P97 is the first phase that produces an admin-visible view but still ships no public value.

### Integration Points
- Fusion consumes P95 `MeasuredMetric[]` + P96 `stated_rules:` → writes `FusedTarget[]` onto
  `CreatorStyleProfile.FusedTargets` via `CreatorStyleProfileStore.UpsertAsync`.
- Studio ledger page reads the fused profile by creator slug (`GetBySlugAsync`).

</code_context>

<specifics>
## Specific Ideas

- The board-wipes case is the flagship the ledger must render well: stated "3-5 max,
  overrated" vs measured ~1.2/deck (19 decks run zero) is **agreement, not hypocrisy** —
  the decks deviate from format canon but match the creator's own stated philosophy. The
  `agree`/`philosophy` verdict badges (D-12) exist to make this legible; a conflicts-only
  view would misrepresent it as a delta.
- Counterspells are the conditionality proof: ≥8 stated is true only in blue *control*
  shells (1-3 in blue *splash*) — exactly why the join must key on `(metric, condition)`
  (D-08) or it emits a false delta.

</specifics>

<deferred>
## Deferred Ideas

- **Mass corpus backfill** (re-distill all ~106 artifacts to populate `stated_rules:`) —
  operator-driven, deferred per P96 D-05; P97 uses the single confirmed Snail profile.
- **Syncing fused profiles to the deployed app** — no consumer until P99/P100; the ledger
  stays Studio-local this phase.
- **Card-level grounding of stated rules** — Phase 98's guard, not fusion's job.

*None — discussion otherwise stayed within phase scope.*

</deferred>

---

*Phase: 97-profile-fusion-conflict-ledger*
*Context gathered: 2026-07-12*
