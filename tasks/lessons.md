# Lessons

## Render service identity vs render.yaml `name`
- The `name:` field in `render.yaml` is only meaningful for **blueprint-managed** services. Render identifies a running service by its **service ID** (`srv-...`) and **display name**, both set in the dashboard.
- The DeckFlow service was created standalone in the dashboard. Its display name was `DeckFlow` from day one. The repo's old `render.yaml` had `name: mtg-deck-studio` — a stale YAML field, not the service's actual name.
- **Rule:** never describe a render.yaml `name:` change as a "service rename". Say "blueprint name field" or just say what it is — a YAML edit. Confirm service identity from the dashboard ID/display, not from the repo.

## Status 139 on Render = SIGSEGV
- Exit code 139 = 128 + 11 (SIGSEGV). Native crash, not a managed exception.
- Common Linux causes: OOM kill by kernel (sometimes 137 instead), native interop crash, stack overflow, glibc/ABI mismatch.
- BUT: an unhandled exception in .NET 10 on Linux can also surface as 139 if the runtime aborts during stack unwinding. Don't assume native bug — **always read stdout logs before guessing**, the stack trace is usually right there.
- For .NET on Render Free (512MB), suspect memory only after ruling out unhandled exceptions.

## Npgsql does NOT accept URI connection strings
- Render (and most cloud Postgres providers) hand out connection strings in URI form: `postgresql://user:pass@host:port/db`.
- Npgsql's `NpgsqlConnectionStringBuilder` inherits .NET's `DbConnectionOptions` parser, which ONLY accepts `key=value;key=value` format.
- Passing a URI throws `System.ArgumentException: Format of the initialization string does not conform to specification starting at index 0`.
- Fix: normalize URI → key=value at the config boundary (e.g., env-var reader). Use `NpgsqlConnectionStringBuilder` to assemble. Decode URL-escaped userinfo. Map `?sslmode=require` query → `SSL Mode=Require`.
- Don't rely on stale memory — when in doubt, test the parser locally before claiming "X version supports URI". I claimed Npgsql 7+ did; it does not.
