# 0015 The toys keep everything in PostgreSQL

Status: accepted. Supersedes 0008.

- Everything the site produces is stored in PostgreSQL: work, homes, accounts and now the toys. Board messages (`board_message`), slot spins (`slot_spin`), latency runs (`latency_run`), canvas strokes and resets (`canvas_stroke`, `canvas_reset`) and postcards with their images (`postcard`, `postcard_image`) are never deleted.
- The services keep their in-memory windows and protocols (ADRs in `toys/`). Accepting anything, drawn, posted or offered back by a browser, appends it to a write-behind journal (`WriteBehindJournal<T>`, the same one homes use), so no toy waits for the database. Inserts are idempotent by id. If the database stays down for five attempts the batch is logged as lost; browsers still hold it and offer it back.
- On start-up each toy has an `IStateLoader` that fills its window before the site accepts requests: newest `BoardCapacity` messages; newest `SlotCapacity` spins, `SlotWinnerCapacity` winners and `SlotJackpotCapacity` jackpots (`winner` and `jackpot` are computed when stored); newest `LatencyCapacity` runs plus every leaderboard and jackpot candidate (medians are stored as columns, computed by `LatencyRun.Rank`); canvas strokes after the newest reset, newest first up to `CanvasMaxPoints`; the newest postcard, whose images are sanitized again. Rows that fail the same validation as browser offers are skipped.
- Strokes are stored when they finish (pen up, abandoned, or offered); a stroke in progress during a restart is lost, as before.
- Browsers keep their IndexedDB history and still offer it back; with the database this mostly restores what a crash lost between journal writes.
- Rate limits, spin locks and the latency gap stay in memory. They are not data.
- `/read` only reads `wwwroot/nytimes.txt`, which is now part of the repository and published with the site; reading history stays in the browser by design.
