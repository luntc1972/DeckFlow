# Phase 38: Controller SRP Split — Discussion Log

**Date:** 2026-06-12
**Mode:** discuss (default, single-pass — user gave decisive answers)

> Human-reference record of the discuss-phase Q&A. Not consumed by downstream agents (they read CONTEXT.md).

## Area 1 — Controller split granularity
- **Options:** By tool family (~5-6) [rec] / By nav group (~4) / One per tool (~10)
- **Selected:** By tool family → D-01

## Area 2 — Shared shell + cross-cutting
- **Options:** Base controller (`DeckToolControllerBase` + `ShellController`) [rec] / Static helpers, no base / You decide
- **Selected:** Base controller → D-03

## Area 3 — CommandRunners split shape
- **Options:** Two classes, confirm naming (`DeckCommandRunners` + `ContentKbCommandRunners`, helpers-first two-commit) [rec] / Discuss naming-boundary
- **Selected:** Two classes, confirmed naming → D-04

## Area 4 — Test file layout
- **Options:** Mirror new controllers [rec] / Minimal-touch / You decide
- **Selected:** Mirror new controllers → D-05

## Deferred ideas
None — stayed in scope (pure refactor).

## Claude's discretion captured
- Exact `DeckPacketController` boundary (Primer split?) + `JudgeQuestionsController` placement → planner within D-01.
