# 0007 Homes keep everything in PostgreSQL

Status: accepted. Supersedes the in-memory capacity in `homes/0004`.

- Every accepted entry, posted or offered back by a browser, is written to `nest_entry` by `NestJournal`, a background writer with retries, so posting never waits for the database. If the database stays down the entry is still in every browser that saw it and is offered back later.
- On start-up the newest `Nest__Capacity` entries (default 100,000) are loaded into `NestService`, which then works exactly as before. Browsers still keep their own copy in IndexedDB and still offer back what the server lacks.
- Deleting an entry marks it deleted in the database, removes it from memory, and remembers its id as a tombstone. Snapshots and deltas carry `deleted` ids; browsers drop those from memory and IndexedDB. Offers of a deleted id are refused, so a browser that was offline cannot bring it back.
- Deleting for good clears the entry's content but keeps the row as a tombstone (`purged_at`), for the same reason.
- Restoring puts the entry back into memory and every browser receives it as an addition.
