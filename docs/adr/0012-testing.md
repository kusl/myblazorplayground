# 0012 Tests

Status: accepted

- `tests/MyBlazorPlayground.Domain.Tests`: rich text (including what may never be emitted), hashing, setup codes, account, work and file rules, the access policy.
- `tests/MyBlazorPlayground.Data.Tests`: every store against a real PostgreSQL. Each test creates its own database, migrates it and drops it. They run when `PLAYGROUND_TEST_DATABASE` names a server that may create databases; `scripts/test.sh` starts a throwaway container for them, CI uses a service container, and otherwise they are skipped.
- `tests/MyBlazorPlayground.Web.Tests`: the chaos and nest unit tests, ported, plus homes persistence and tombstones, form defaults and return URLs.
- `tests/js`: the chaos and nest Vitest suites plus the editor helpers, the enhancer, the shared status helper and the circuit tracker, with `tests/fixtures/nest` shared with C# as before.
- xUnit v3 on Microsoft Testing Platform (`global.json`).
