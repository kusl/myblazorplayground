# Architecture decision records

| ADR | Decision |
| --- | --- |
| [0001](0001-record-decisions.md) | Record decisions as ADRs |
| [0002](0002-solution-layout.md) | Three projects, feature folders |
| [0003](0003-rendering-model.md) | Static forms for work and accounts, islands for live pages |
| [0004](0004-data-access.md) | PostgreSQL, Dapper and our own migration runner |
| [0005](0005-accounts.md) | Accounts, sign-in and setup |
| [0006](0006-roles-and-deletion.md) | Roles, deleting and the audit log |
| [0007](0007-homes-in-postgresql.md) | Homes keep everything in PostgreSQL |
| [0008](0008-toys-stay-in-memory.md) | The toys keep their in-memory design (superseded by 0015) |
| [0009](0009-rich-text-and-files.md) | Markdown with pasted screenshots |
| [0010](0010-hosting.md) | Quick tunnels, side by side with MyRestaurant |
| [0011](0011-security.md) | Headers, CSP and forms |
| [0012](0012-testing.md) | Tests |
| [0013](0013-dependencies.md) | Dependencies |
| [0014](0014-two-step-sign-in.md) | Optional two-step sign in with authenticator apps |
| [0015](0015-toys-in-postgresql.md) | The toys keep everything in PostgreSQL |
| [0016](0016-styles.md) | Shared styles stay generic, feature styles stay isolated |

Carried over from chaos (`toys/`) and nest (`homes/`), still describing how those features work: message ids, local history, canvas, realtime delivery, slots, latency, server clock, message details, postcards, reading; entries and handshake, listing identity, links and keys, details, local storage, realtime delivery.
