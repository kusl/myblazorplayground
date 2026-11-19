# 0008 The toys keep their in-memory design

Status: accepted

- Board, postcard, canvas, slots, latency and read are ported from chaos unchanged in behavior: the server keeps a small window in memory, browsers keep history in IndexedDB and offer it back after a restart (ADRs in `toys/`).
- They do not use the database. Forgetting is part of what they are, and their windows are sized for memory, not for storage.
- Their limits (`Chaos__*`) and protocols are as before. Shared connection and request limits moved to `Site__*`.
- `wwwroot/nytimes.txt` is not part of this repository; without it `/read` shows a notice.
