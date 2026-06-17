# 0002 — CalVer public versioning, named planning milestones

Date: 2026-06-17

## Context

Through v1.0–v1.7 the project used a single `vMAJOR.MINOR` number for two
distinct things at once: the GSD *planning milestone* (a batch of phases planned,
executed, audited, archived) and the *public product version*. Incrementing the
minor each milestone made the number march toward `2.0`, which carries a SemVer
connotation — a major/breaking release — that does not match reality: v1.7 was a
local operator tool + UI polish + internal refactors, nowhere near a "2.0" event.

DeckFlow is a solo-maintained, continuously-deployed web app (deckflow.gg) with
**no public API, SDK, or installed clients**. SemVer's core purpose — signalling
breaking changes to downstream consumers — barely applies. The minor count was
creating false "approaching 2.0" pressure with no real meaning.

Two corrections underpin this decision: (1) planning-milestone identity and
public-version identity are different things and need not share a number;
(2) a `MAJOR` bump is a deliberate decision (a breaking/identity change), never an
arithmetic destination — `1.x` is unbounded (1.9 → 1.10 → 1.27).

## Decision

Adopt **Calendar Versioning (CalVer) for the public product** and **named,
decoupled planning milestones** for GSD.

- **Public release version = `YYYY.MM`** (zero-padded month), e.g. `2026.06`. If
  more than one public release ships in the same month, append a patch counter:
  `2026.06.1`, `2026.06.2`. Git tags for public releases use the bare CalVer string
  (no `v` prefix) so they are visually distinct from the historical `v1.x` SemVer
  tags.
- **GSD planning milestones are named, not SemVer-numbered.** Each milestone gets a
  sequential cycle id + a short name, e.g. `Cycle 8 — <name>`. This is the unit that
  GSD plans / audits / archives (`.planning/milestones/<id>-*.md`); it no longer
  pretends to be a product version. When a milestone ships, it is tagged with the
  CalVer of its ship date.
- **`v1.7` is the final SemVer tag.** CalVer starts with the next public release.
  Existing `v1.0`–`v1.7` tags and `.planning/milestones/v1.*` archives are
  historical record and are NOT renamed.
- **`2.0`/major resets are abolished as a routine target.** A whole-number identity
  reset is only considered for a deliberate "new product era" event (e.g. a
  framework migration off ASP.NET, a ground-up rewrite, a brand relaunch). Absent
  that, the date-based scheme simply continues.

## Consequences

- GSD `/gsd-new-milestone` is given the cycle name/id (e.g. `Cycle 8 — <name>`) as
  its milestone label, not a `vX.Y` string. At milestone completion the release is
  tagged `YYYY.MM` (ship date), not `v1.8`.
- README "What's new in vX.Y" section headers switch to CalVer/date-or-cycle
  headers (e.g. "What's new — 2026.06" or "Cycle 8 — <name> (2026.06)"). The
  changelog maps cycle name ↔ CalVer tag.
- The version number no longer signals release size; the README "What's new" /
  changelog body carries that context instead. Accepted trade-off for a solo CD app.
- The `v1.x` ↔ phase-number mapping in `.planning/` history is unaffected; phase
  numbers keep incrementing globally across milestones as before.
- Tooling note: a milestone whose label/tag collides with the working git branch
  name caused a `git push origin <ref>` "matches more than one" ambiguity at the
  v1.7 close. CalVer tags (`2026.06`) will not collide with feature branch names,
  removing that footgun.
