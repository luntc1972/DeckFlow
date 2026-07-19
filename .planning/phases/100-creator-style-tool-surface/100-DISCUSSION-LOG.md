# Phase 100: Creator-Style Tool Surface - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-19
**Phase:** 100-creator-style-tool-surface
**Areas discussed:** D-14 prod profile delivery, Flag topology & cache wiring, Page UX & input flow, Output & degraded states

---

## D-14 prod profile delivery

| Option | Description | Selected |
|--------|-------------|----------|
| Git-shipped seed | Export fused profiles (+ exemplar decks) to repo-committed seed, loaded at startup like ContentKbSeedLoader | ✓ |
| DirectPush to prod Postgres | Migrate store to Postgres; Studio pushes like Cycle 16 KB DirectPush | |
| Hybrid: seed now, Postgres later | Seed in Phase 100, Postgres as backlog | |

**User's choice:** Git-shipped seed (recommended option)

| Option | Description | Selected |
|--------|-------------|----------|
| Profiles + exemplar decks | Everything the engine reads locally — full prod/local parity | ✓ |
| Profiles + pre-baked exemplars | Frozen exemplar/whitelist snapshot; prod path diverges | |
| You decide | Planner picks from read-path analysis | |

**User's choice:** Profiles + exemplar decks

| Option | Description | Selected |
|--------|-------------|----------|
| JSON seed + startup loader | Diffable text in repo, hydrated at startup (ContentKbSeedLoader pattern) | ✓ |
| Ship SQLite file directly | Prebuilt .db read-only; binary churn in public repo | |
| You decide | — | |

**User's choice:** JSON seed + startup loader

| Option | Description | Selected |
|--------|-------------|----------|
| CLI export command | New DeckFlow.CLI command; run after crawl/fusion, commit output | ✓ |
| Studio export button | Studio UI work in last phase | |
| Build-time auto-export | CI has no local db — not viable | |

**User's choice:** CLI export command

---

## Flag topology & cache wiring

| Option | Description | Selected |
|--------|-------------|----------|
| Single tool flag | `tool.creator-style.enabled` does visibility + cache-bypass; roadmap name retired as alias | ✓ |
| Two flags | Tool visibility + separate engine flag; dead combination exists | |
| Keep roadmap name as tool flag | `creator.style-artifact` as FlagKey; breaks tool.* convention | |

**User's choice:** Single tool flag

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, cache + bypass set | PacketSessionCache + flag in service's prompt-mutating bypass list | ✓ |
| No caching at all | Every submit rebuilds; hammers throttled Scryfall | |
| You decide | — | |

**User's choice:** Cache + bypass set

| Option | Description | Selected |
|--------|-------------|----------|
| Hidden + 404, sitemap gated | SeoPaths/sitemap lists route only when flag ON | ✓ |
| Hidden + 404, sitemap static | Deck-history precedent: sitemap advertises a 404 | |
| You decide | — | |

**User's choice:** Sitemap gated on flag

| Option | Description | Selected |
|--------|-------------|----------|
| Fold the cheap ones | IN-01/03/04/08 ride Phase 100; hygiene deferred | ✓ |
| Fold all 10 | Cycle ends clean but pads user-visible phase | |
| Defer all 10 | Surface-only phase | |

**User's choice:** Fold the cheap ones

---

## Page UX & input flow

| Option | Description | Selected |
|--------|-------------|----------|
| Dropdown w/ profile stats | Select listing seeded creators with evidence depth labels | ✓ |
| Card grid | Tile per creator; CSS cost across 10 themes | |
| Plain dropdown | Names only | |

**User's choice:** Dropdown with profile stats

| Option | Description | Selected |
|--------|-------------|----------|
| Standardized import component | Site dropdown + URL/paste toggle (meta-gap/comparison parity) | ✓ |
| Paste-only textarea | Loses URL import parity | |
| You decide | — | |

**User's choice:** Standardized import component

| Option | Description | Selected |
|--------|-------------|----------|
| Single form → result | One page, packet renders below (meta-gap/manabase shape) | ✓ |
| Workflow step tabs | _WorkflowStepTabs strip like deck-analysis | |
| You decide | — | |

**User's choice:** Single form → result

| Option | Description | Selected |
|--------|-------------|----------|
| Analyze section, craft-first copy | Lead with measured style + exemplars; AI limited to "ChatGPT-ready packet"; no crawl/KB claims | ✓ |
| Analyze section, AI-forward copy | Violates standing promo-copy preference | |
| Build section | Tool analyzes finished decks — wrong section | |

**User's choice:** Analyze section, craft-first copy

---

## Output & degraded states

| Option | Description | Selected |
|--------|-------------|----------|
| Packet + summary strip | Copy block + rubric verdict chips + exemplar names on page | ✓ |
| Packet only | Nothing human-readable on page | |
| Full rendered report | Duplicates the ChatGPT response | |

**User's choice:** Packet + summary strip

| Option | Description | Selected |
|--------|-------------|----------|
| ChatGPT-only at ship | One arm; add Claude/Gemini after tool proves value | ✓ |
| All three arms | 3x hand-maintained golden prompts immediately | |
| You decide | — | |

**User's choice:** ChatGPT-only at ship

| Option | Description | Selected |
|--------|-------------|----------|
| Packet + warning banner | Deliver packet, visible Notice banner; wording branches per IN-03 | ✓ |
| Block on degradation | Overkill — guard already fail-closes per card | |
| Silent degradation | Notice only inside packet text | |

**User's choice:** Packet + warning banner

| Option | Description | Selected |
|--------|-------------|----------|
| Distinct error, picker = source of truth | Picker lists only stored creators; empty store → clear message; IN-04 status split | ✓ |
| Generic error message | Conflates operator problem with upstream weather | |
| You decide | — | |

**User's choice:** Distinct error, picker as source of truth

---

## Claude's Discretion

- JSON seed schema/layout, staleness/versioning metadata
- Closest sibling tool to use as controller/view template
- Whether IN-10's test rides with IN-03/IN-04 work
- Summary-strip visual detail within theme-token constraints

## Deferred Ideas

- Postgres migration of profile/deck-cache stores (live prod updates, DirectPush-style) — next cycle
- Claude/Gemini prompt arms for creator-style packet
- P99 hygiene items IN-02, IN-05, IN-06, IN-07, IN-09 (and IN-10 unless bundled)
- deck-input-store restore desync (pre-existing backlog, all split-input tools)
