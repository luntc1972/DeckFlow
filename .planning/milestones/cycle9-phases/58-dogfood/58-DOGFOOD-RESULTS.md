# Phase 58 — Dogfood Results

> **Validation artifact — no product code or schema changed this phase.** This document is the
> deliverable. The operator fills the `[FILL AT RUN]` slots during plan `58-02`; plan `58-03`
> completes SC4 and the Overall Verdict. Record only status strings, SHAs, spend numbers, and
> proof paths here — never connection strings or secrets.

This scaffold is created by plan `58-01` Task 2 and overwritten with real evidence during the run.
See `58-01-PLAN.md` for the authoritative section spec. Placeholder body intentionally minimal.

## Overall Verdict

**PASS — Cycle-9 machinery validated end-to-end on fresh content, with one integration gap found and fixed.**

| SC | Result |
|----|--------|
| SC1 — new video distilled, higher-quality than baseline | **PASS** — `e3qGnuupp8U` distilled with the Phase-57 prompt; tag discipline 3 vs 12 archetype tags, cleaner paste-ready clips. |
| SC2 — Published in Studio AND /Admin/ContentKb | **PASS (after fix)** — surfaces initially disagreed (Studio Pushed-hidden); dogfood exposed that DirectPush never set local `is_visible`. Fixed (DirectPush publishes visible, prod-then-local); both surfaces now Published. |
| SC3 — spend within cap | **PASS** — $0 metered on subscription CLI ≤ $15 cap. |
| SC4 — no-regression | **PASS** — prod corpus additive 108→109, nothing flipped/removed. |

**What dogfooding caught:** the publish-state feature (Phases 55–57) worked on the prod-facing surface but
the Studio surface could never reach Published via DirectPush — a real cross-surface gap invisible to the unit
tests. Found, decided (publish-visible), fixed, and reviewed in-session.

**Open follow-ups (non-blocking for cycle close):**
1. Prod harvest green-run not yet observed since the F-51-PG-01 deploy (fix is live; awaiting a scheduled/manual run).
2. Durability: `e3qGnuupp8U` is in the prod DB but not in the committed git seed — a future reset+reseed would
   omit it until a full git-Publish runs with the complete approved corpus.
3. The 22 isolate-rejected local rows were re-approved post-push (operator-confirmed).

## SC1 — New video distilled + higher-quality than baseline

**Run date:** 2026-06-18 (Phase-57 reworked distill prompt, commit `00c3bc7`).

- **New video:** `e3qGnuupp8U` — "Stop Building Like Everyone Else | The Command Zone 743"
  (`https://www.youtube.com/watch?v=e3qGnuupp8U`)
- **New entry:** `DeckFlow.Studio/artifacts/studio/content-kb/the-command-zone/e3qGnuupp8U.md`
- **Baseline (pre-Cycle-9, old prompt):** `content-kb/the-command-zone/6oS1E5BGi0U.md`
  ("Why Your Deck Feels Clunky", CZ 730, `generated_utc 2026-06-15`, pre-`00c3bc7`). Same creator/format.
- **Distill provider:** subscription claude CLI (`llm_calls=3`, `spend_usd=0.000000`).

### Before / After

| Dimension | Baseline (old prompt) | New (Phase-57 prompt) |
|-----------|-----------------------|-----------------------|
| Archetype tags | **12** (voltron/aristocrats/combo/control/reanimator/aggro/midrange… on a mana-curve video = over-tagged noise) | **3** (tribal, value-engine, tokens) — restrained, on-topic |
| Card-category tags | 10 (incl. board-wipe/protection/tutor/counter, many off-center) | 5 (ramp, draw, recursion, removal, finishers) |
| Clips | Strong but several raw transcript quotes | Cleaner paraphrase + concrete cards/combos, self-contained, paste-ready |
| Summary | Flowing thesis paragraph | Dense enumeration of commander→sub-archetype ideas (faithful to a list-format video) |

**Verdict: HIGHER QUALITY (yes).** Primary DIST-01 win = tag discipline (3 vs 12 noisy archetype tags) and cleaner paste-ready clips; summary comparable. (Provisional — operator to confirm.)

## SC2 — Published in both Studio and /Admin/ContentKb

**Publish path:** DirectPush (upsert-only; never git-Publish — that gutted the seed earlier this cycle).
Operator isolated the single new entry by rejecting the 22 already-pushed approved rows locally (local-only,
reversible), so the diff was **New: 1** (`e3qGnuupp8U`), then ran Stage 1→3 (compute diff → SCP upload →
prod DB write). AI never wrote prod; all AI prod reads were SELECT-only via the Render read-only MCP.

- **Prod `/Admin/ContentKb` surface:** `e3qGnuupp8U` derives **Published** — verified read-only on prod
  Postgres (`pushed_to_prod_utc` non-null AND `is_visible=true`). Prod totals: 109 rows, 23 pushed, 25 visible.
- **Studio `/review` "Publish State" column:** **Published** — operator-confirmed.

### Dogfood finding (SC2 blocker) + fix

The first DirectPush left the two surfaces **disagreeing**: prod `/Admin` showed Published (after the operator
unhid via the admin Publish action), but Studio stayed **Pushed-hidden**. Root cause: DirectPush stamped
`pushed_to_prod_utc` but never set `is_visible`; new prod rows insert hidden ("ships dark"), the admin unhide
wrote only the prod row, and Studio's badge derives from the **local** store — which had no visibility toggle
and no sync-back. So Studio could never derive Published via DirectPush alone.

Operator decision: **DirectPush publishes visible.** Fix (Claude-coded, Codex gpt-5.4 reviewed):

- New keyed `IContentSiteIndexStore.SetVisibilityAsync(keys, visible)` (one transaction, mirrors
  `StampPushedToProdAsync`; clears `is_hidden`).
- `DirectPush.WriteRowsAsync` now writes **prod-then-local**: stamp + publish-visible on prod, then local
  (local never over-reports prod). Both surfaces derive Published in the same batch.
- Tests: 4 Core SQLite tests + 1 DirectPush bUnit (`DirectPush_Success_PublishesRowsVisible_LocalAndProd`).
- Suite after fix: build clean, **Studio 49/49, Core 475/475**. Codex review: 1 HIGH + 1 MED fixed,
  1 LOW (re-push un-hides admin-hidden rows) accepted-by-design per the operator decision.

After the fix + re-push, Studio shows **Published** for `e3qGnuupp8U` — SC2 satisfied on **both** surfaces.

## SC3 — Spend within cap (DECKFLOW_LLM_MONTHLY_CAP_USD)

- **Cap:** `$15.00` (default; not overridden).
- **Provider:** subscription claude CLI → metered `spend_usd=0.000000` for this run (`llm_calls=3`).
- **Result:** $0.00 metered spend ≤ $15.00 cap. **Within cap.** (Subscription usage is real but un-metered/$0 on the ledger.)

> Note: an env gotcha first resolved the provider as metered (`openai` default) because WSL→Windows
> `dotnet.exe` did not inherit `DECKFLOW_LLM_PROVIDER` without `WSLENV`; the classifier correctly
> refused to run an unmetered classifier on a metered provider. Fixed via `WSLENV` forwarding, then
> distill ran $0 on the subscription CLI.

## SC4 — No-regression: no previously-Published entry flipped

**PASS — additive only.** Prod `content_site_index` went **108 → 109 rows** (exactly +1 = the dogfood
`e3qGnuupp8U`); pushed 22 → 23. No row removed, none flipped Published → hidden. The only prod writes this
session were upsert-only (DirectPush content-columns-only + `pushed_to_prod_utc` stamp + publish-visible) and
the operator's admin unhide — all additive. DirectPush never deletes; git-Publish (which had gutted the seed)
was deliberately avoided. Verified read-only via Render MCP.

> Separate finding (not a KB-corpus regression): the prod harvest Run Log showed `42883: operator does not
> exist: text <= timestamp with time zone` failures — all dated **2026-06-17 ≤ 21:04Z**, i.e. *before* the
> F-51-PG-01 fix went live. The live Render deploy (`d0bb913`, live 2026-06-17 21:19Z) carries the fix; the
> errors are stale, pre-fix rows. No harvest run has fired since the deploy, so a green run is not yet
> observed (scheduled bulk harvest) — tracked as a follow-up, independent of the dogfood corpus.
