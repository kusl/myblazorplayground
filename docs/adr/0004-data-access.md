# 0004 PostgreSQL, Dapper and our own migration runner

Status: accepted

- PostgreSQL is the only store. Dapper maps rows to classes with `init` properties; `DefaultTypeMap.MatchNamesWithUnderscores` maps `snake_case` columns.
- Migrations are numbered SQL files embedded in `MyBlazorPlayground.Data` (`Migrations/0001_initial.sql`). `MigrationRunner` applies the missing ones in order, each in its own transaction, under a PostgreSQL advisory lock, and records them in `schema_migration`. It retries for two minutes while the database is starting. No migration library: the runner is fifty lines.
- Migrations run before the host starts (`StartupTasks`), so nothing (including the Data Protection key ring) touches the database before its schema exists.
- Ids are UUIDv7 from `Guid.CreateVersion7()`; they sort by creation time, which audit paging and nest rely on.
- Dates are passed as text and cast in SQL, and returned with `to_char`, so nothing depends on how a driver maps `date`. Timestamps are set by `now()` in SQL.
- Multi-step changes and every deletion run in one transaction together with their audit row.
- Attachments are stored in `attachment_chunk` rows of 1 MiB (`STORAGE EXTERNAL`, no recompression). Upload and download stream chunk by chunk, so memory stays flat whatever the file size, and no single value approaches PostgreSQL's 1 GB limit.
- Data Protection keys (antiforgery and sign-in cookies) live in `data_protection_key`, so a restart or a new container keeps everyone signed in and one database dump is a complete backup. They are not encrypted at rest; the database is.
