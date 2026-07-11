# Cycle 16 Pre-Flip Checklist (D-08)

**Purpose:** This is the pre-flip gate for the two Cycle-16 flags that shipped **OFF**: `sync.directpush-gitbody` and `sync.reconcile`. Both flips are **operator-owned** — no code in this repo flips them automatically. The SYNC-16 round-trip test (`RoundTripSyncLoopTests` / `RoundTripSmokeTests` under `DeckFlow.Web.Tests/Integration/RoundTrip/`) is the automated proof standing behind this gate: it exercises distill → Publish/DirectPush → prod store → web body resolution → deploy/reseed → PullFromProd → reconcile against a real Testcontainers Postgres and a real git tree. Before flipping either flag in prod, work through this checklist in order.

---

## D-07 gate note — the SYNC-16 test is a LOCAL/manual Docker gate, not CI coverage

- [ ] **Understand this before relying on green CI as proof.** The round-trip test class is `[PostgresFact]` + `IClassFixture<PostgresContainerFixture>`. It **auto-skips** wherever `DECKFLOW_POSTGRES_TESTS=1` is unset **or** Docker is unavailable — and CI runs `dotnet test --no-build` with neither set, so **CI always skips this test today**. A green CI run does NOT mean the round-trip test ran.
- [ ] **Run it for real before a flip**, one of:
  - Locally with Docker: `DECKFLOW_POSTGRES_TESTS=1 dotnet.exe test DeckFlow.Web.Tests --filter FullyQualifiedName~RoundTrip`
  - Or push-and-watch a branch where you've manually confirmed a Docker-backed CI runner picked it up.
- [ ] `.github/workflows/` is **intentionally untouched** this phase — no CI Postgres service was added (deferred, see `93-CONTEXT.md` D-07). Do not mistake the auto-skip for coverage.

---

## FU-1 — Updated-visible row shows stale-but-visible content during the deploy window (MED, code DEFERRED)

**Mechanism:** For an already-visible prod row being *updated* via DirectPush, the content-only upsert changes title/tags/artifact/`body_sha256` while `is_visible` stays `true` (visibility is intentionally excluded from the content-only upsert). Until Render redeploys, the row serves updated metadata over the *old* deployed body. If the deploy fails, the row stays visible with stale content indefinitely.

- [x] **DECISION (record your choice before flipping `sync.directpush-gitbody`):**
  - [x] **Option A — Accept-by-design.** Keep rows visible through updates (no flicker/outage on every edit). Correct the Stage-4 UI copy so it no longer claims rows stay "hidden + awaiting-confirm" for this update-to-visible case — the copy should instead say the row remains visible and may briefly serve the prior body until the deploy completes. **✅ APPLIED 2026-07-11**: all 10 copy sites in `DeckFlow.Studio/Pages/DirectPush.razor` (intro, TARGET banner, Stage-4 body + 3 success alerts, awaiting-confirm resume banner, Stage-3 DB-write success alert, Stage-5 description, Stage-5 did-not-confirm warning) now distinguish new-rows-stay-hidden vs already-visible-rows-stay-visible-until-redeploy. Commits `1af8af21` (Claude, 6 Stage-4 sites) + `5763c483` (Codex-authored per role split, Claude-reviewed; 4 remaining sites Codex's plan-review caught). Studio build 0/0; DirectPushPageTests 36/36 green. No coordinator/logic change (is_visible remains operator-owned + excluded from the content-only upsert by design).
  - [ ] **Option B — Hide-then-reconfirm on update.** NOT chosen. (Would change the update path to hide the row until the new body is confirmed deployed, mirroring the insert path — new coordinator logic, not built.)
  - [x] Record which option was chosen and the date: **Option A — 2026-07-11**
- [x] **Coordinator code fix stays DEFERRED** — Option A shipped as a copy-only fix; the underlying keep-visible-through-update behavior is unchanged (accept-by-design). Option B coordinator logic was NOT built.

---

## FU-2 — ON row can strand after a Stage-4 indeterminate flag read (MED, code DEFERRED)

**Mechanism:** If the Stage-4 flag read is indeterminate (prod flag DB briefly unreachable) while prod is genuinely **ON**, the fail-closed read pushes the git commit with `[skip render]` and triggers **no Render redeploy**. Stage 5 then polls `/app`, never finds the new body deployed, and leaves the row `awaiting-confirm`. The resume path (`GetAwaitingConfirmRowsAsync` + resume) only re-polls — it does **not** create a fresh non-`[skip render]` commit or trigger a redeploy, so resume alone cannot un-strand the row.

- [ ] **This is SAFE** — no false publish occurs; the row is operator-visible as `awaiting-confirm`, not silently wrong.
- [ ] **This is RECOVERABLE** — a full re-push of the same content (Stage 4 flag read now succeeds → drops `[skip render]` → redeploys → Stage 5 confirms), or any later normal deploy that makes `/app` catch up, clears the strand.
- [ ] **DECISION/ACTION (before or soon after flipping `sync.directpush-gitbody`):** consider whether to build a fix that lets the resume/awaiting-confirm action re-trigger the git redeploy stage (drop `[skip render]` on resume) so a stranded ON row is self-recoverable without a full re-push. This is a code change — deferred; file as a follow-up if you want it built.
- [ ] Record the decision and date: ______________________

---

## FU-3 — Live Studio-UI + prod-Postgres reconcile walk before flipping `sync.reconcile` (deferred, gate)

**Why this gate exists:** The automated `ReconcileFixtureDriveTests` (and now the SYNC-16 round-trip reconcile assertion) prove the reconcile safety story — read-only detection, flag/stale Apply refusals, seed-owned-only soft-hide, prod-owned-stays-visible — against a real orchestrator + coordinator over a SQLite prod stand-in and a real git tree. They do **NOT** exercise:
  - (a) real Render Postgres prod,
  - (b) the actual Studio `/reconcile` Blazor page interactions,
  - (c) a real `sync.reconcile` flip in the live web DB.

- [ ] **ACTION 1 — Run `/reconcile` live once, dry-run only, flag still OFF.** Confirm the page loads against real prod and produces a report.
- [ ] **ACTION 2 — Review the discrepancy counts against the known baseline.** Expect file-orphans in the hundreds (per the 2026-07-05 live prod drift audit: ~328 file-without-row orphans at that time). A wildly different count (zero, or an order of magnitude higher) is a signal to stop and investigate before proceeding.
- [ ] **ACTION 3 — Review the readable D-06 report** (`content-kb/reconcile-report.md` or equivalent) produced by the dry-run.
- [ ] **ACTION 4 — Confirm no prod write occurred** during the dry-run (dry-run must be read-only; verify via prod row timestamps/audit if available).
- [ ] **ACTION 5 — Flip `sync.reconcile` ON**, then run a **scoped** dry-run → Apply:
  - [ ] Confirm only **seed-owned** rows soft-hide (i.e., rows with `seed_managed=true` that are no longer in the seed/git tree).
  - [ ] Confirm a **known prod-owned row** (one NOT seed-managed) stays visible and untouched by the Apply.
- [ ] Record the date this live walk was completed: ______________________

---

## Live flip steps — `sync.directpush-gitbody` ON

Complete in order:

1. [ ] SYNC-16 round-trip test green — run locally with Docker (`DECKFLOW_POSTGRES_TESTS=1 dotnet.exe test DeckFlow.Web.Tests --filter FullyQualifiedName~RoundTrip`) or confirmed via a Docker-backed CI watch (see D-07 gate note above).
2. [ ] FU-1 decision recorded (Option A or B, above).
3. [ ] FU-2 decision recorded (build the resume-redeploy fix, or accept the safe/recoverable strand as-is).
4. [ ] Flip `sync.directpush-gitbody` to **ON** in the prod web flag store.
5. [ ] **Post-flip smoke:** perform one real DirectPush of a single content row and confirm:
   - [ ] The row serves its body from `/app` (git-deployed tree), not the `/data` SFTP overlay.
   - [ ] `index-seed.json` was re-exported and committed as part of the push (seed reflects the new/updated row).
   - [ ] The next normal deploy does **not** revert the row (no-revert-after-reseed holds in prod, matching the SYNC-16 assertion).

---

## Live flip steps — `sync.reconcile` ON

Complete in order:

1. [ ] FU-3 live walk completed and reviewed (all 5 actions above checked off).
2. [ ] Flip `sync.reconcile` to **ON** in the prod web flag store (if not already flipped as part of FU-3 Action 5 — if so, this step is already done; just confirm the flag state).
3. [ ] Confirm the destructive Apply path is now enabled and scoped correctly:
   - [ ] Seed-owned rows soft-hide when absent from git/seed.
   - [ ] A known prod-owned row (not seed-managed) is never touched by Apply.

---

## Reference

- Source follow-ups: `.planning/phases/90-directpush-correctness-seed-sync/90-FOLLOWUPS.md` (FU-1, FU-2, FU-3)
- Phase context / decisions: `.planning/phases/93-round-trip-integration-test/93-CONTEXT.md` (D-06, D-07, D-08)
- SYNC-16 test location: `DeckFlow.Web.Tests/Integration/RoundTrip/`
- 2026-07-05 live prod drift audit baseline: 106 prod rows / 36 in approved seed / 57 hidden+pending / ~328 file-without-row orphans / 32 mojibake bodies (referenced in FU-3 Action 2).
