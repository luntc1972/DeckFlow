# Requirements: Cycle 11 — Security, Visibility Control & Creator-Lens

**Milestone goal:** Close two HIGH-priority security/data holes, give the admin full tool-visibility control over the public site, validate whether the Content KB actually improves AI output, and run a design pass on Studio.

**Core value alignment:** Every supported workflow must still produce paste-ready AI output in one round-trip. This cycle hardens that surface (SSRF), keeps prod content consistent (artifact gap), lets the operator curate which workflows are exposed (toggles), and tests/strengthens the one feature whose value is still unproven (Content KB / creator-lens).

---

## Cycle 11 Requirements

### Security — deck-source host hardening (SEC)

- [x] **SEC-01**: Deck-URL loading treats only exact or approved-subdomain Moxfield/Archidekt hosts as trusted; look-alike hosts (`moxfield.com.evil.tld`, `evilmoxfield.com`, `moxfield.com@evil.tld`) are rejected across every deck tool (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`, `/manabase`, sync, convert, primer).
- [x] **SEC-02**: On the Moxfield fallback path the app forwards only a canonical reconstructed `https://moxfield.com/decks/{deckId}` URL to Commander Spellbook — never the user-submitted URL.
- [x] **SEC-03**: Regression tests lock the spoof-host cases above so the hole cannot silently reopen.

### Data integrity — prod content artifact gap (DATA)

- [ ] **DATA-01**: Determine and document whether the live site serves content-KB body from `/data` `.md` files or from the DB content column (decides the severity and the fix path for the 86 orphaned rows).
- [ ] **DATA-02**: Prod `content_site_index` and its backing artifacts are reconciled to a consistent state — every row either has its `.md` artifact present on `/data`, or is reconciled down / formally downgraded to cosmetic with the decision recorded.

### Admin tool-visibility toggles (TOGGLE)

- [ ] **TOGGLE-01**: A single tool registry is the source of truth for each public tool's route, nav section, display label, help topic, feature-flag key, and home-tile copy.
- [ ] **TOGGLE-02**: Admin can enable or disable any public tool (Analysis, Comparison, cEDH Meta Gap, Mana Base, Sync, Convert, Primer, Card Lookup, Mechanic Rules, Ask a Judge, Knowledge Base, Category tools) from the admin console.
- [ ] **TOGGLE-03**: Disabling a tool hides its home-page tile, its help entry, and its nav dropdown link together — no orphaned surface remains.
- [ ] **TOGGLE-04**: A disabled tool's page is not reachable directly — the route returns not-found (or redirects), not merely hidden from nav.
- [ ] **TOGGLE-05**: When every tool in a nav section is disabled, the section's header/trigger and its dropdown disappear too.
- [ ] **TOGGLE-06**: The existing ad-hoc flags (`feature.manabase.enabled`, `content.kb.enabled`, `feature.categories.enabled`) are folded into the unified registry model with no double-gating, and all tool flags default ON so an existing deploy exposes the same tools as before.
- [ ] **TOGGLE-07**: The admin toggle UI warns (but does not block) when disabling a core-workflow tool such as Deck Analysis.

### Content KB value validation (KBVAL)

- [x] **KBVAL-01**: An A/B harness produces the deck-analysis prompt twice for a representative deck set — once with and once without expert-context clips — so the two outputs can be run through ChatGPT and compared. _(Met — `Spike001KbValueAbHarness.cs`, promoted from spike 001.)_
- [x] **KBVAL-02**: The comparison is judged (blind where feasible) and a clear lift / marginal decision is recorded, gating the creator-philosophy work and the eventual `content.kb.enabled` flip. _(Met — verdict MARGINAL/NEGATIVE, gate NOT cleared; see `phases/67-content-kb-value-a-b-validation/67-DECISION.md`. content.kb.enabled stays OFF; Phase 68 drops.)_

### Creator-philosophy research (CREATOR)

- [ ] ~~**CREATOR-01**~~ ⊘ **DROPPED** (Phase 68 does not run): conditional on KBVAL-02 showing clear lift; the verdict is marginal, so the creator-philosophy research/design phase drops this cycle. Original: A research/design document specifies the per-creator philosophy representation — distilled style-card + RAG-over-transcript grounding, principle-level provenance, contradiction preservation, temporal-drift handling, and a hallucination gate.

### Studio UI design pass (STUI)

- [ ] **STUI-01**: DeckFlow.Studio has a real shell + shared design tokens (color/spacing/type) aligned to the deckflow.gg brand, replacing the stock Blazor template chrome.
- [ ] **STUI-02**: The Studio Home page is a real dashboard showing pipeline state at a glance (counts by status / publish-state) with quick links to Harvest / Review / Publish.
- [ ] **STUI-03**: Studio pages handle responsive/table-overflow and dark mode consistently.

---

## Future Requirements (deferred)

- **Deck Primer generator** — new paste-ready primer workflow (31-section catalog, bracket presets, Spellbook/category/EdhTop16 grounding). Deferred to **Cycle 12** (large, own cycle). Seed + design notes retained.
- **Creator-philosophy build** — the actual style-card synthesizer + persona injection into the analysis prompt. Deferred until after KBVAL confirms lift (this cycle delivers research only).
- **Scheduled / bulk auto-harvest (AUTO-03/04)** — operator prefers manual curation for now.
- **SEO / growth lane (SEO-01..05)** — off-Render growth work.
- **Embedding / vector retrieval (pgvector / ONNX)** — deferred until corpus > ~500 videos (RAM cap risk).
- **Gemini paste-limit workaround** — still flag-gated; needs split-message vs direct-API decision.

## Out of Scope (explicit exclusions)

- **Creator-philosophy production build this cycle** — gated on KBVAL; building before validation risks investing in an unproven KB. Research/design only.
- **New deck workflows** — Deck Primer is the only candidate and it is deferred to Cycle 12.
- **Studio P2 per-page consistency** — already shipped in Cycle 10 (shared `StatusBadge`, creator filtering, grouped nav); not re-done here.
- **Framework / stack migration** — ASP.NET 10 + Razor pinned; Studio stays Blazor Server.
- **Writing to prod from the AI** — the prod artifact-gap fix is operator-run for any write; AI stays read-only against prod.

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| SEC-01 | Phase 64 — Deck-Source Host Hardening | Partial — predicate built (Plan 01); call-site adoption pending (Plan 02) |
| SEC-02 | Phase 64 — Deck-Source Host Hardening | Not started |
| SEC-03 | Phase 64 — Deck-Source Host Hardening | Partial — regression matrix built (Plan 01); call-site tests pending (Plan 02) |
| DATA-01 | Phase 65 — Prod Content Artifact Reconcile | Not started |
| DATA-02 | Phase 65 — Prod Content Artifact Reconcile | Not started |
| TOGGLE-01 | Phase 66 — Admin Tool-Visibility Toggles | ✅ Met (code-complete + reviewed; operator UAT pending) |
| TOGGLE-02 | Phase 66 — Admin Tool-Visibility Toggles | ✅ Met (code-complete + reviewed; operator UAT pending) |
| TOGGLE-03 | Phase 66 — Admin Tool-Visibility Toggles | ✅ Met (code-complete + reviewed; operator UAT pending) |
| TOGGLE-04 | Phase 66 — Admin Tool-Visibility Toggles | ✅ Met (route + API gating; coverage test hardened; operator UAT pending) |
| TOGGLE-05 | Phase 66 — Admin Tool-Visibility Toggles | ✅ Met (code-complete + reviewed; operator UAT pending) |
| TOGGLE-06 | Phase 66 — Admin Tool-Visibility Toggles | ✅ Met (code-complete + reviewed; operator UAT pending) |
| TOGGLE-07 | Phase 66 — Admin Tool-Visibility Toggles | ✅ Met (code-complete + reviewed; operator UAT pending) |
| KBVAL-01 | Phase 67 — Content KB Value A/B Validation | ✅ Met (harness from spike 001) |
| KBVAL-02 | Phase 67 — Content KB Value A/B Validation | ✅ Met (verdict MARGINAL/NEGATIVE recorded) |
| CREATOR-01 | Phase 68 — Creator-Philosophy Research (conditional) | ⊘ Dropped (KBVAL-02 marginal — gate not cleared) |
| STUI-01 | Phase 69 — Studio UI Design Pass | Not started |
| STUI-02 | Phase 69 — Studio UI Design Pass | Not started |
| STUI-03 | Phase 69 — Studio UI Design Pass | Not started |

**Coverage:** 15/15 requirements mapped to exactly one phase. 6 phases (64-69).
