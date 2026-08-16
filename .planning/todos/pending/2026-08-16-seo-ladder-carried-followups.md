---
created: 2026-08-16T20:24:00.000Z
title: Three carried followups rescued from the SEO ladder .continue-here checkpoint
area: testing, architecture
files:
  - DeckFlow.Web.Tests/PageMetadataViewTests.cs
  - DeckFlow.Web/Models/ToolDefinition.cs
  - DeckFlow.Web/Services/ToolRegistry.cs
---

## Why this file exists

These three items lived only in `.planning/.continue-here.md`, the SEO ladder P0-P3 checkpoint
(`feat/seo-ladder`, last written 2026-08-05). That checkpoint's two blockers are both discharged —
the Codex review gate on 2026-08-16 (`.planning/reviews/2026-08-16-seo-ladder-codex-gate.md`) and
the push/merge (`a5f51b0d` is an ancestor of `main`) — so the file was deleted as a stale resume
trap. Its `<followups>` block was the only thing in it not recorded elsewhere. Rescued verbatim
below. None is a defect; all three are deliberate deferrals.

## 1. No `<h1>` totality guard across indexable views

`PageMetadataViewTests` could gain a `[Theory]` asserting exactly one `<h1>` across all 21 indexable
views. Today that guard exists only on the newest page.

**Deferred because** it risks surfacing failures on existing views, which is separate scope from the
ladder. Fixing those is the actual work; the assertion is the easy half.

## 2. Hand-rolled reflection walk duplicated in three test files

The "get action methods carrying an `HttpMethodAttribute`" reflection walk is hand-rolled in three
separate test files. A shared helper is warranted.

**Deferred because** it is pre-existing debt, not introduced by the ladder.

## 3. `ToolDefinition` conflates capability with presentation

`ToolDefinition` mixes capability (`Route`, `AdditionalRoutes`, `FlagKey`) with presentation (`Tab`,
`TileTitle`, `IsPrimaryTile`). That conflation is why Help cannot cheaply join `ToolRegistry`.

**Trigger to act:** extract a `GatedRoute` list when a **second** non-tool gated route appears. One
special case is not a pattern — do not refactor on the strength of the current single case.
