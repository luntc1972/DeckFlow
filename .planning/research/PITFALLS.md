# Pitfalls Research

**Domain:** MTG Commander/cEDH deck-evaluation features layered on DeckFlow's existing paste-artifact engine (interaction audit, win-con/combo map, mulligan evaluator)
**Researched:** 2026-06-30
**Confidence:** HIGH (grounded in this repo's source: `DeckStatClassifier`, `CommanderSpellbookService`, `CastabilitySimulator`, ADR-0001, the flag-seed + parity test patterns, and CLAUDE.md test/format constraints)

These are pitfalls specific to ADDING these three features to THIS system — not generic web/MTG advice. The core-value constraint dominates: **every artifact must paste into ChatGPT/Claude/Gemini and produce a useful answer in one round-trip with zero reformatting.** A feature that produces a subtly-wrong artifact is worse than no feature. Each pitfall maps to a Cycle 14 phase (79 = Interaction audit, 80 = Win-con/combo map, 81 = Mulligan evaluator, plus cross-cutting gates every phase must clear).

---

## Critical Pitfalls

### Pitfall 1: Presenting heuristic substring classification as authoritative counts

**What goes wrong:**
The interaction audit will be built on the same pattern as `DeckStatClassifier` (`DeckFlow.Core/Analysis/DeckStatClassifier.cs`) — naked `oracleText.Contains("destroy target", ...)` substring tests. That logic is wrong on a large fraction of real cards, and if the artifact says "**7 removal spells, 3 counterspells**" the user (and the downstream AI) treat it as fact. Concrete mis-reads the current substrings already produce or will produce:
- `IsInteractionCard` fires on `"destroy target"` — counts a "destroy target **land** you control" / self-sacrifice / Lignify ("destroy target creature" no — Lignify isn't destroy, but a one-sided Tragic Slip is) as opponent-facing interaction, and counts pillowfort/symmetric effects as answers.
- It counts **every `Instant`** as interaction (a tempo cantrip, a ritual, a protection spell, and a counterspell all collapse to "interaction").
- `IsBoardWipeCard` keys on `"destroy all creatures"` — **misses pseudo-wipes** (Toxic Deluge "−X/−X", Cyclonic Rift overload, edicts, Pernicious Deeds), and the `"each creature ... gets -"` branch will **false-positive** on one-sided anthems and minus-X pumps that aren't wipes.
- **Modal / "choose one" cards** (Charms, Commands), **MDFCs**, and adventure cards expose multiple oracle clauses, so one card lands in several buckets or the wrong one.
- **Pseudo-removal** (−X/−X, tap-down, "can't attack or block", sacrifice/edict, exile-by-bounce, fight-via-creature) is invisible to a "destroy/exile/counter" substring set → false negatives.
- **Conditional counters** ("counter target spell unless its controller pays") are caught, but soft/redirect counters and "counter unless" tax effects that don't contain `"counter target spell"` are missed.

**Why it happens:**
Substring matching is cheap, already in the codebase, and passes its unit tests on the curated cards the tests pick. The error only shows on the long tail of real decklists, which the fixtures don't cover. The temptation is to ship "interaction = 7" because the number renders cleanly.

**How to avoid:**
- **Frame the output as a heuristic first-pass read the AI re-checks, never as ground-truth.** Every paste artifact labels it "DeckFlow's automated first-pass read — verify against the actual cards," and the prompt explicitly instructs the AI to re-classify interaction from the card list rather than trust the counts. This is the core-value-preserving move: a wrong count the AI is told to re-derive is harmless; a wrong count presented as authoritative poisons the one round-trip. Mirrors the existing manabase honesty pattern (`UnsupportedInteractions` discloses what the model only approximates).
- **Show the cards in each bucket, not just a number** — a user scanning "Removal: Swords, Pongify, [Soul-Guide Lantern?]" spots the misfire; a bare "7" hides it.
- **Add a borderline/confidence tier:** cards that match on a weak signal (bare `Instant`, bare `"destroy target"`) go into a "possible / review" tier, not the confident tier.
- **Extend the shared `DeckStatClassifier`** for the known pseudo-removal classes and self-target exclusions ("you control") rather than inlining a second divergent `Contains` chain in the new service — one classifier to fix, not two.

**Warning signs:**
- The view/artifact shows a count with no card list behind it.
- A test deck of 5 hand-picked clean removal spells passes, but nothing exercises Toxic Deluge, Cyclonic Rift, a Charm, an edict, or an MDFC.
- The prompt prose says "the deck **has** N counterspells" in the imperative rather than "approximately / verify."

**Phase to address:** Phase 79 (Interaction & answers audit) — and the framing rule is a milestone-wide invariant.

---

### Pitfall 2: Over-claiming combos from Commander Spellbook (reachability + dropped ranking signal)

**What goes wrong:**
The win-con/combo map deepens use of `CommanderSpellbookService`. Two failure modes:
1. **"In deck" ≠ "reachable / castable / realistic win line."** `find-my-combos` returns combos whose pieces are all present in the 99; it does not know whether the pieces are realistically assemblable (color identity is checked upstream, but mana cost, tutorability, and "needs the commander on the battlefield" are not). Listing every returned combo as "this is how the deck wins" massively over-claims. `manaValueNeeded` is exactly the signal separating a turn-3 combo from a 12-mana parlor trick, and **the parser captures it but nothing consumes it** (`SpellbookCombo.ManaValueNeeded`, populated at `CommanderSpellbookService.cs:210`; `Popularity` at :204 — see the SpellbookCombo ranking note).
2. **Partial / almost-combos conflated with real combos.** `SpellbookAlmostCombo` (one-card-away, `ParseAlmostVariants`, `missing.Count == 1`) is a separate, weaker signal; the redundancy/assembly-turn read must not count almost-combos as win lines.
3. **Unranked truncation noise.** The service caps at `MaxIncluded = 20` / `MaxAlmostIncluded = 15` with **no ranking** — whatever 20 the API returned is what surfaces, so "deeper combo use" naively becomes "show more of an unranked list," amplifying noise.

**Why it happens:**
The API returns a flat list; the ranking fields exist in the record but were never wired to a sort or display, so the path of least resistance shows more unranked combos. `FindCombosAsync` returns `null` on API failure (graceful degradation) — easy to render that as "no win conditions" (a damaging false negative).

**How to avoid:**
- **Wire `ManaValueNeeded` and `Popularity` into ranking and the assembly-turn read** (the documented follow-up — [SpellbookCombo ranking fields]). Sort included combos by reachability (low `manaValueNeeded` + high `popularity` first); use `manaValueNeeded` as the assembly-turn proxy the feature promises.
- **Keep included vs almost-included strictly separated**, labeling almost-combos "one card away (not currently a win line)."
- **Frame combos as candidate win lines the AI confirms** (same discipline as Pitfall 1): "Commander Spellbook reports these piece-sets are present; confirm castability and board/color requirements," not "the deck wins via X."
- **Respect the null-graceful contract:** distinguish "combo data unavailable for this run" from "no win conditions found." A false negative is as damaging as a false positive here.
- **Do not add a second upstream fetch** (see Pitfall 7) — the call is already made and cached 30 min (`CacheDuration`).

**Warning signs:**
- The combo list order/length changes run-to-run for the same deck (unranked truncation at 20).
- `ManaValueNeeded` / `Popularity` still unread in the new code (grep shows only the parser touches them).
- Almost-combos appear under a "win conditions" heading.
- An API `null` renders as "this deck has no combos/win lines."

**Phase to address:** Phase 80 (Win-condition & combo map).

---

### Pitfall 3: Over-promising on subjective mulligan-keep heuristics

**What goes wrong:**
The mulligan evaluator surfaces a "keepable-hand probability." "Keepable" is genuinely subjective (depends on the line, the pod, the matchup, on-the-play vs draw), but a single percentage reads as objective truth. Worse, `CastabilitySimulator` **already contains a London-mulligan keep heuristic** (`LondonMulligan`, keep-on-land-count-band logic around `CastabilitySimulator.cs:1166+`, plus the MQ-05 `ColorKeepCap` color-keep rule). If the new evaluator invents a *second*, different keep rule, the site reports a mulligan-keep% that contradicts the manabase tool's own internal mulligan model for the same deck.

**Why it happens:**
"Keepable hand probability" sounds like a clean derivable metric, so it's tempting to write a fresh hand-evaluation rule (e.g. "2–5 lands and a payload"). The existing London mulligan logic is buried in the simulator's private methods, so a builder who doesn't read it reimplements it with slightly different thresholds.

**How to avoid:**
- **Reuse the existing mulligan logic in `CastabilitySimulator`** as the single source of "keepable" — extract/expose it rather than forking a parallel definition. One keep rule across the product.
- **State the keep criterion explicitly and narrowly** next to the number ("keepable = 2–5 lands with at least one early play, on the London mulligan; not a strategic keep judgment"). Define keepable; don't imply a universal verdict.
- **Frame as a consistency signal, not advice** — a hand-quality band feeding the AI, which makes the real keep call. Avoid "you should keep X% of hands."
- **Surface a band/distribution, not one false-precision percentage** (e.g. "~62% land-keepable; color-screwed openers ~Y%"), and pair it with the **color/curve read** (the actionable part the feature scopes).

**Warning signs:**
- A new keep-threshold constant appears in the mulligan-evaluator code that doesn't match `ColorKeepCap`/the land-band in `CastabilitySimulator`.
- Manabase tool and mulligan evaluator give different mulligan numbers for the same deck.
- Artifact/UI copy uses prescriptive "keep/mulligan this" language.

**Phase to address:** Phase 81 (Opening-hand / mulligan evaluator).

---

### Pitfall 4: Breaking flag-OFF byte-identity (pages AND zips)

**What goes wrong:**
Each feature is flag-gated and "byte-identical when OFF." It's easy to break this in a way that passes a casual eyeball but fails the real invariant: when the flag is OFF, the rendered Razor page, **every one of the three paste artifacts, AND the zip round-trip artifact** must be byte-for-byte what they were before the feature existed. A stray newline, a `null`-vs-empty-section difference, or an always-emitted (empty) heading breaks it. The zip path is the easy miss — `ResultContractTests` and the AISEL-04 zip round-trip invariant make the packaged artifact part of the contract, not just the on-screen page.

**Why it happens:**
Developers verify the flag-ON path visually and assume OFF is "just don't show it." But inserting a section conditionally still often leaves a blank line, trailing space, or wrapper element when the data is null. The Phase 77 score block is the template that got this right (`AnalysisScorePromptParityTests`: "the flag-OFF (null scoreBlockText) path must stay byte-identical ... minus the contiguous score block").

**How to avoid:**
- **Copy the Phase 77 pattern exactly:** the new block is a single contiguous, fully-suppressible unit; when its data is null the output equals the pre-feature output with that block removed — no orphan whitespace, no empty heading.
- **Write a flag-OFF byte-identity test per surface** (page + all 3 artifacts + zip), mirroring `AnalysisScorePromptParityTests` and `ResultContractTests`. Assert OFF output `==` baseline string.
- **Seed the flag OFF in BOTH SqliteSeedSql and PostgresSeedSql**, and add it to the tool registry so `ToolFlagSeedConsistencyTests` / `ToolFlagPostgresSeedTests` cover it (they assert every registry flag key is seeded in each dialect; a new tool flag missing from one dialect's seed SQL fails them).

**Warning signs:**
- The flag-OFF artifact diff against `main` shows a whitespace-only change.
- A new tool flag exists in the registry but not in one of the seed SQL blocks.
- The zip artifact wasn't checked, only the page.

**Phase to address:** Every phase (79, 80, 81) — gate condition for each.

---

### Pitfall 5: Violating prompt-variant parity / ADR-0001 (no shared helper, hand-edit all 3)

**What goes wrong:**
Each new artifact section must render in the ChatGpt, Claude, AND Gemini variants. ADR-0001 (`docs/decisions/0001-prompt-variants-decoupled.md`) forbids extracting a shared helper/constants holder across variants — a consolidation was attempted (`a1fa5ad`) and reverted same-day (`b2ffba7`). Two opposite failures: (a) a dev "DRYs up" the new section into a shared string and reintroduces the exact coupling ADR-0001 bans; or (b) a dev hand-edits only ChatGpt and Claude and **forgets Gemini**, so one platform silently lacks the section (semantic divergence — the real defect ADR-0001 cares about).

**Why it happens:**
Three near-identical edits feel like duplication begging for extraction (reviewers "repeatedly flag this as duplication/drift" per the ADR). And three separate files are easy to under-edit — Gemini is also flag-gated (`DECKFLOW_GEMINI_ENABLED`), so its omission may not surface in a default-config smoke test.

**How to avoid:**
- **Hand-apply the new section to all three variants** (`PromptBuilders/Analysis/{ChatGpt,Claude,Gemini}*PromptVariant.cs` and any new family), accepting near-identical prose per ADR-0001. Platform-format wording differences (Claude XML-tag prose vs ChatGPT/Gemini markdown bullets) are correct, not drift.
- **Add a 3-platform parity test** like `AnalysisScorePromptParityTests`: assert the section appears in all three, the same figures survive into each, instantiating each concrete variant directly (no shared helper).
- Do **not** let a code-review/`/simplify` pass "fix" the duplication — ADR-0001 says reviews must not report cross-variant prose duplication as a finding.

**Warning signs:**
- A new `*PromptShared`/`*Constants` type referenced by more than one variant.
- The parity test only checks ChatGpt and Claude.
- Gemini artifact (flag on) lacks the new section.

**Phase to address:** Every phase that adds a paste-artifact section (79, 80, 81).

---

### Pitfall 6: WSL VSTest instability masking real failures (the Dapper-PG crash)

**What goes wrong:**
A full local `dotnet test` run is unreliable in WSL (CLAUDE.md: "VSTest unreliable in WSL"), and the Postgres/Dapper integration block makes it worse — Cycle 13 shipped with **2 CI failures that local runs masked**. A green-looking local run is not evidence the suite passes. If a phase is closed on "tests pass locally," broken tests reach `main`/CI.

**Why it happens:**
The Postgres integration tests (`Integration/PostgresFactAttribute`, `PostgresContainerFixture`, `DapperTypeHandlerRoundTripTests`) skip by default (need `DECKFLOW_POSTGRES_TESTS=1` + Docker), and the WSL VSTest socket issue can crash or hang the runner before it reports — so the run either dies or silently omits results, and the developer reads the absence of red as green.

**How to avoid:**
- **Treat CI, not the local run, as the authoritative gate** (CLAUDE.md: "rely on `dotnet build` clean + ... push-and-watch CI"). Push the branch and watch GitHub Actions before declaring tests pass; honor the `no-ship-failing-tests` rule against CI results.
- **Build the test projects** (`dotnet build` of `DeckFlow.Web.Tests` + `DeckFlow.Core.Tests`) even when you can't run them — a clean build catches interface/signature drift (the documented "verify builds test project" rule).
- **Run targeted filters** for the new tests (`dotnet test --filter <NewTestClass>`) to dodge the PG/Dapper block that crashes the full run.
- Do not rely on the WSL `gstack` headless daemon for UI checks (CLAUDE.md: observed unstable).

**Warning signs:**
- "Tests pass" claimed from a local run that printed no PASS/FAIL summary or crashed mid-run.
- CI red after a "green local" close (the Cycle 13 signature).
- New tests never executed in CI because they live behind a skip attribute.

**Phase to address:** Every phase — close-out / verification gate.

---

### Pitfall 7: Adding a second simulation pass or extra upstream fetch

**What goes wrong:**
All three features are explicitly "new readouts on top of" the existing Monte-Carlo sim and combo call — not new compute. The mulligan evaluator's keepable% and the combo map both have an obvious-but-wrong implementation: run `CastabilitySimulator` again with mulligan instrumentation, or call `find-my-combos` again for the combo map. Either doubles cost. On the 512MB Render web tier and Basic-256mb Postgres, a second 20k-trial sim per analysis (`DefaultTrials = 20_000`) is a real latency/RAM hit, and a duplicate upstream call burns the Scryfall/Spellbook rate budget and defeats the existing 30-min cache.

**Why it happens:**
The mulligan and castability sims feel like different questions, so re-running is the path of least resistance. Likewise the combo map "needs combo data" so it calls the API — not realizing the data is already fetched and cached upstream in the same request.

**How to avoid:**
- **Thread the existing single sim pass through.** The London mulligan already runs inside `CastabilitySimulator`; surface keepable-hand/color-curve stats as an additional output of that one pass (the Phase 77 score did exactly this — derived axes from the sim it already runs).
- **Consume the cached `CommanderSpellbookResult`** the analysis path already produced; pass it into the combo map. No new RestSharp request.
- Mind the 512MB cap: no per-feature re-simulation, no new long-lived caches without measuring RAM.

**Warning signs:**
- A second `CastabilitySimulator.Simulate(...)` / sim invocation in the request path.
- A new POST to `backend.commanderspellbook.com` or Scryfall in the new services.
- Analysis latency or web-tier memory climbs after the feature lands.

**Phase to address:** Phase 80 (combo fetch reuse) and Phase 81 (sim reuse).

---

### Pitfall 8: Tripping the changed-lines format gate / carve-outs

**What goes wrong:**
The format gate is changed-lines-only and CI-authoritative (`format-gate`). Two ways the new work fails it: (a) editing a prompt-variant file and letting an editor reflow lines you didn't intend (raw-string literals especially), or (b) a formatter "fixing" one of the five bug-driven carve-outs — converting `{ get; init; }` to `{ get; }` (breaks System.Text.Json on `SpellbookCombo`-style records — has broken `EdhTop16Client` before), re-indenting a C# raw-string literal (**changes the prompt bytes shipped to the AI — directly corrupts the product**), inlining an `[Attribute]`, collapsing a switch expression, or flipping LF→CRLF.

**Why it happens:**
Prompt variants and the new artifact sections are full of raw-string literals; auto-format on save silently re-indents them, which `CarveOutGuardTests` and the changed-lines gate reject — and the re-indent also silently changes the AI output. The records carrying combo/score data use `{ get; init; }` / `required`, which a formatter may "simplify."

**How to avoid:**
- **Touch only the lines you mean to change** (the gate is changed-lines-only; existing files aren't mass-reflowed). Run `scripts/format-check-changed.sh staged` locally; opt into the `.githooks` pre-commit hook (`git config core.hooksPath .githooks`).
- Treat `.editorconfig` carve-outs as law: never auto-convert `init`→get-only on the DTO records, never re-indent raw-string prompt literals, preserve switch expressions / LF.
- Verify new prompt-literal bytes are intentional — a re-indent that passes the gate by accident still corrupts the artifact (Pitfall 4's byte-identity test catches this; they reinforce each other).

**Warning signs:**
- `CarveOutGuardTests` red, or `format-gate` CI red on lines you didn't think you touched.
- A diff showing whole-block re-indentation of a raw string.
- `{ get; }` appearing on a record that needs JSON deserialization.

**Phase to address:** Every phase — commit-time gate.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Bare substring `Contains` for new interaction categories (copy `DeckStatClassifier` style) | Fast, matches existing code | Compounds false positives/negatives; a second copy to keep in sync if inlined | Only if extending the shared `DeckStatClassifier` AND framed as heuristic + card list shown |
| Show interaction/combo as a bare count, defer the card list | Cleaner UI | Hides misclassification; user can't sanity-check; AI trusts a wrong number | Never for confident counts — always show the cards |
| Leave `ManaValueNeeded`/`Popularity` unranked, just show more combos | "Deeper combo use" ships sooner | Noise/over-claim; assembly-turn read unfounded; documented follow-up stays open | Never — fields already parsed; wiring them is the feature |
| Fresh keep-rule for the mulligan evaluator instead of reusing the sim's London mulligan | Decoupled, easy to write | Two contradicting mulligan numbers across tools; double maintenance | Never — extract the existing rule |
| Verify flag-OFF on the page only, skip the zip | Less test work | Zip round-trip drift escapes; AISEL-04 contract silently broken | Never |
| Close a phase on a green-looking local `dotnet test` | Feels done | PG/Dapper block masks failures → reach CI/main (Cycle 13 ×2) | Never — push-and-watch CI |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Commander Spellbook (`find-my-combos`) | Re-calling the API; treating every returned combo as a real win line; counting almost-combos; rendering `null` as "no combos" | Reuse the cached `CommanderSpellbookResult`; rank by `ManaValueNeeded`/`Popularity`; separate included vs almost; disclose "unavailable" on `null` |
| Scryfall (oracle text source) | Classifying off raw oracle text without normalization or modal/MDFC handling; assuming one card = one bucket | Use normalized oracle text via the existing pipeline; handle multi-clause/modal cards; exclude self-targeting ("you control") |
| `CastabilitySimulator` (Monte-Carlo) | Running a second 20k-trial pass for mulligan stats; forking a new keep rule | Surface mulligan/color-curve stats from the single existing pass; reuse `LondonMulligan` + `ColorKeepCap` |
| Feature-flag store (SQLite + Postgres seed SQL) | Seeding the new flag in only one dialect; not registering it | Seed OFF in both SqliteSeedSql + PostgresSeedSql; add to the tool registry so seed-consistency tests cover it |
| Prompt-variant families | Extracting a shared helper (ADR-0001 violation) or editing only 2 of 3 variants | Hand-edit all three; parity test asserts presence + figures in each |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Second Monte-Carlo sim per analysis (mulligan) | Analysis latency ~doubles; web-tier RAM spikes | Derive from the one existing 20k-trial pass | Immediately on the 512MB Render tier under any concurrency |
| Duplicate combo API fetch | Extra upstream latency; cache-hit assumption broken; rate-budget burn | Consume the upstream-cached result | When two requests race or upstream rate-limits |
| Rendering full card lists per category unbounded | Large artifact; Gemini ~30k paste ceiling exceeded → instructions truncated | Cap/group long lists; keep within the Gemini ceiling | Big toolbox decks (40+ interaction pieces) |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| New JSON API endpoint for the readouts without the same-origin guard | CSRF on the new endpoint | Apply `SameOriginRequestValidator` like every other API endpoint (skipping it is a documented anti-pattern) |
| Echoing raw upstream combo/oracle JSON into a page without encoding | Stored/reflected injection if upstream text is rendered as HTML | Render through the existing encoded/Markdig-`DisableHtml` path; treat upstream text as untrusted |

(Domain note: these features are read-only analysis over public card data — no new secrets, auth, or PII surface. The real risk is correctness, not data exposure.)

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| False-precision single number ("62% keepable", "7 removal", "3 win lines") | User over-trusts a heuristic; bad analysis in the one round-trip | Bands/ranges + underlying card lists + "verify" framing |
| "This deck has no win conditions / no interaction" on a heuristic miss or API null | Alarming false negative; user distrusts the whole tool | Distinguish "none found" from "couldn't determine / data unavailable" |
| Prescriptive mulligan advice ("keep this hand") | Implies a universal verdict that doesn't hold across pods/matchups | Frame as a consistency signal feeding the AI, not a keep instruction |
| New section visible/relabeled even when flag OFF | Breaks the byte-identity guarantee; confuses users on a half-shipped feature | Fully suppress (page + artifacts + zip) when OFF |

## "Looks Done But Isn't" Checklist

- [ ] **Interaction audit:** Often missing the **card list behind the count** and the **"heuristic — verify" framing** — verify the artifact instructs the AI to re-classify, and that pseudo-removal/modal/self-target cases are tiered "review," not silently mis-bucketed.
- [ ] **Combo map:** Often missing **ranking by `ManaValueNeeded`/`Popularity`** and **included-vs-almost separation** — verify the parser fields are actually consumed and almost-combos aren't counted as win lines.
- [ ] **Mulligan evaluator:** Often missing **reuse of the existing London-mulligan rule** and the **single-sim-pass** constraint — verify no second `CastabilitySimulator` call and no contradicting keep number vs the manabase tool.
- [ ] **Flag-OFF byte-identity:** Often missing the **zip artifact** and **both DB dialect seeds** — verify page + all 3 artifacts + zip are byte-identical to baseline, flag seeded OFF in SQLite and Postgres.
- [ ] **Variant parity:** Often missing the **Gemini variant** edit — verify all three variants render the section and a parity test asserts it.
- [ ] **Tests:** Often "passed locally" but never run in CI — verify GitHub Actions is green (PG/Dapper block masks local results).
- [ ] **Format gate:** Often a stray raw-string re-indent — verify `format-check-changed.sh staged` clean and `CarveOutGuardTests` green; confirm prompt-literal bytes are intentional.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Flag-OFF byte-identity broken (shipped) | LOW (flag is OFF in prod) | Add the missing byte-identity test, fix the whitespace/suppression, re-verify all surfaces; no prod impact since OFF |
| Mis-bucketed interaction reaches a flipped flag | MEDIUM | Re-frame as heuristic + add card list, extend classifier for the failing class, add fixture for the offending card; flip flag OFF if egregious |
| Over-claimed combos reach users | MEDIUM | Wire ranking, re-separate almost-combos, add reachability framing; flag OFF until ranked |
| Second sim/fetch shipped (latency/RAM regression) | MEDIUM | Refactor to thread the existing pass/result; measure RAM on the 512MB tier before re-enable |
| CI red after "green local" close | LOW–MEDIUM | Honor no-ship-failing-tests: fix on branch, push, confirm CI green before merge |
| Format/carve-out regression on prompt literal | LOW (but check artifact bytes) | Revert the re-indent, confirm AI artifact bytes unchanged, re-run the changed-lines gate |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Heuristic classification presented as authoritative | Phase 79 (Interaction audit) | Artifact shows card lists + "verify" framing; fixtures cover pseudo-removal/modal/self-target; extends `DeckStatClassifier` |
| Combo over-claim / dropped ranking signal | Phase 80 (Win-con/combo map) | `ManaValueNeeded`/`Popularity` consumed in ranking + assembly-turn; included≠almost; null disclosed not zeroed; no new fetch |
| Subjective mulligan over-promise | Phase 81 (Mulligan evaluator) | Reuses sim's London-mulligan rule; keep criterion stated; number matches manabase tool; framed as signal not advice |
| Flag-OFF byte-identity (pages + zips) | All phases (79/80/81) | Per-surface byte-identity test (page + 3 artifacts + zip); flag seeded OFF in both dialects; seed-consistency tests green |
| Variant parity / ADR-0001 | All paste-artifact phases | 3-platform parity test; no shared helper; Gemini included |
| WSL VSTest masking | All phases (close-out) | CI green (push-and-watch), test projects build, targeted `--filter` runs |
| Second sim / extra fetch | Phases 80/81 | No second `Simulate`/`find-my-combos` call in request path; latency + RAM unchanged |
| Format gate / carve-out | All phases (commit) | `format-check-changed.sh staged` clean; `CarveOutGuardTests` green; prompt bytes intentional |

## Sources

- This repo (HIGH): `DeckFlow.Core/Analysis/DeckStatClassifier.cs` (heuristic substrings); `DeckFlow.Web/Services/CommanderSpellbookService.cs` (combo parse, dropped `ManaValueNeeded`/`Popularity`, `MaxIncluded`=20, almost-combo `missing.Count==1`, null-graceful, 30-min cache); `DeckFlow.Core/Manabase/CastabilitySimulator.cs` (single 20k-trial seeded sim + London mulligan + `ColorKeepCap`); `docs/decisions/0001-prompt-variants-decoupled.md` (ADR-0001 no-shared-helper).
- This repo tests (HIGH): `AnalysisScorePromptParityTests.cs` (flag-OFF byte-identity + 3-variant parity template); `ResultContractTests.cs`; `Tools/ToolFlagSeedConsistencyTests.cs` + `Integration/ToolFlagPostgresSeedTests.cs` (dual-dialect seed); `Integration/PostgresFactAttribute.cs` (PG skip → WSL masking); `Manabase/ManabaseFlagBaselineHarness.cs` (env-gated harness pattern).
- Project context (HIGH): `.planning/PROJECT.md` (Cycle 14 scope, byte-identical-OFF, "readouts on top of" the engine, Gemini paste ceiling); `CLAUDE.md` + global instructions (VSTest unreliable in WSL, format-gate carve-outs, `.editorconfig` source of truth, no-ship-failing-tests, 512MB/256mb hosting caps).
- MEMORY (HIGH): SpellbookCombo ranking-fields follow-up (`manaValueNeeded`/`popularity` captured but unconsumed); Cycle 13's 2 CI failures masked by local runs.

---
*Pitfalls research for: DeckFlow Cycle 14 — Deeper Deck Evaluation (interaction audit, win-con/combo map, mulligan evaluator)*
*Researched: 2026-06-30*
