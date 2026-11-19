# 0008 The toys keep their in-memory design

Status: superseded by [0015](0015-toys-in-postgresql.md).

- Board, postcard, canvas, slots, latency and read were ported from chaos with their in-memory windows and browser history (ADRs in `toys/`). They now also store everything in PostgreSQL.
- Their limits (`Chaos__*`) and protocols are as before. Shared connection and request limits moved to `Site__*`.
