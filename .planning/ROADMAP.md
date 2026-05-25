**Risk:** Medium — coordinated deploy + wipe + re-harvest (empty-cache window acceptable since data is reset); new write path must reproduce identical lookup results. Own plan + Codex review.
**Plans:** 2 plans

Plans:
- [ ] 26-01-PLAN.md — Schema + dialect foundation: add IRelationalDialect.SurrogateIdColumnType, reshape EnsureSchemaAsync to the normalized integer-keyed star schema (cards dim + extended deck_queue w/ content_hash reserve + slim integer-keyed facts + compact indexes), RED parity/interning/SQLite-AUTOINCREMENT harness tests (DBO-01)
- [ ] 26-02-PLAN.md — Port write+read paths to integer keys (intern-on-write RETURNING id, remove string-concat commander join), prove parity GREEN, update PG integration test + full-reset runbook table list (DBO-01)