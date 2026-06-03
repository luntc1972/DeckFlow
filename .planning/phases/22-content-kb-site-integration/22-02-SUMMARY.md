---
phase: 22-content-kb-site-integration
plan: 02
task_scope: tasks-1-and-3-complete
subsystem: content-kb-cli-seed-and-runtime-delivery
tags: [content-kb, cli, seed-json, docker-runtime]

requires: [22-01]
provides:
  - content-index-export CLI verb
  - committed content-kb/seed/index-seed.json with 10 index-only rows
  - tracked content-kb/{source-slug}/{video_id}.md publish artifacts
  - Dockerfile runtime COPY delivery to /app/content-kb
affects: [content-kb-seed-loader, content-kb-public-browse, render-runtime-image]

tech-stack:
  added: []
  patterns: [CLI/Core store composition, whitelist JSON export DTO, commit-then-deploy content publish, Dockerfile runtime COPY]

key-files:
  created:
    - content-kb/README.md
    - content-kb/edhrecast/ZSfzhBcLM9Q.md
    - content-kb/edhrecast/zkAmYkIOx98.md
    - content-kb/mtggoldfish/OihCV9qvCrk.md
    - content-kb/mtggoldfish/mDmI-gypvGw.md
    - content-kb/playing-with-power/Bq-nFi0f1jA.md
    - content-kb/playing-with-power/J-QU1G0ZQg0.md
    - content-kb/the-command-zone/f8782tCIwmk.md
    - content-kb/the-command-zone/s_B1wCIWGR0.md
    - content-kb/tolarian-community-college/SMxRbH11oiM.md
    - content-kb/tolarian-community-college/smOZcfAHjpQ.md
  modified:
    - .gitignore
    - .dockerignore
    - Dockerfile
    - .planning/phases/22-content-kb-site-integration/22-02-SUMMARY.md

requirements-completed: [KB-08]
completed: 2026-06-02
---

# Phase 22: Content KB Site Integration Plan 02 Summary

## Scope

Task 1 was already implemented and committed before this run. Task 3 was implemented after the operator approved the protected-file edits for `.gitignore`, `.dockerignore`, and `Dockerfile`. Tasks 2 and 4 remain human checkpoints.

No `DeckFlow.CLI/*`, `.cs`, or `content-kb/seed/*` files were changed during Task 3.

## What Built

- Removed the `.gitignore` rule that ignored `content-kb/`, while leaving `artifacts/` ignored.
- Added `.dockerignore` negations after the blanket `*.md` exclusion so `content-kb/` markdown remains in the Docker build context.
- Added `COPY content-kb/ ./content-kb/` immediately after `COPY --from=build /app/publish .` in the runtime stage.
- Copied the 10 seed-declared markdown artifacts from `artifacts/content-kb/**` into tracked `content-kb/{source-slug}/{video_id}.md` paths.
- Added `content-kb/README.md` documenting the commit-then-deploy flow: run `content-index-export`, copy `artifacts/content-kb/*` into `content-kb/*`, commit, deploy.

## Delivery Decisions Recorded

- Delivery route: Dockerfile explicit `COPY content-kb/ ./content-kb/`.
- Realized runtime artifact directory: `/app/content-kb`.
- Plan 03 resolver base: `ContentRootPath`; seed `artifactPath` values keep the `content-kb/` prefix, so resolving from `/app` yields `/app/content-kb/{source-slug}/{video_id}.md`.
- Docker image `ls /app/content-kb` verification was not run here. It is deferred to Task 4 / deploy; Docker is unavailable in this WSL distro.

## Verification

- `grep -c '^content-kb/' .gitignore` -> `0`.
- `git check-ignore artifacts/content-kb/edhrecast` -> `artifacts/content-kb/edhrecast`.
- `git check-ignore content-kb/seed/index-seed.json` -> no output.
- `grep -nA3 '^\*\.md' .dockerignore | grep -c 'content-kb'` -> `3`.
- `grep -c 'COPY content-kb' Dockerfile` -> `1`.
- Seed-declared artifact path check: `seed_paths=10`, `missing=0`; copied markdown count under `content-kb/{slug}/` = `10`.
- Protected-file diff review: `.gitignore` has only the approved `content-kb/` removal; `.dockerignore` has only the three approved negation insertions; `Dockerfile` has only the approved runtime `COPY content-kb/ ./content-kb/` insertion.
- `git diff --cached --check -- .gitignore .dockerignore Dockerfile content-kb/README.md .planning/phases/22-content-kb-site-integration/22-02-SUMMARY.md` exited `0`.
