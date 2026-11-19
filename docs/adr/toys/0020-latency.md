# 0020 Shared latency test

Status: accepted

- `/latency` hosts `LatencyIsland`. `LatencyCatalog` lists the endpoints: this site's `/time` (browser only), large public sites and 17 AWS DynamoDB `/ping` regions. All are anonymous GETs.
- `Begin` is refused (`busy`) while a test runs or within `LatencyGapSeconds` (default 10) of the last start. There is no per-client cooldown. Accepted starts reply `ok <id>` and broadcast `running`. A busy browser retries after one to two gaps. The old `chaos-latency-next` key is removed from `localStorage`.
- The server probes in the background (`HttpLatencyProbe`): one warm-up request, then the fastest of 3 time-to-headers, 6 endpoints at a time, 4 s per request, `LatencyBudgetSeconds` overall. `LatencyServerProbe=false` turns it off.
- The browser does the same with `fetch` (`no-cors`, `no-store`, no credentials, no referrer) and sends `Finish(id, ms[])`, -1 for failures. The server waits up to the budget plus 15 s, then publishes either way.
- A result is `{ id, text }` with text `key=browser,server;…` in integer ms, empty for none. Offers must re-serialize to the same text. Every run is stored in PostgreSQL (ADR 0015); the live window keeps 50, browsers 10,000 (`chaos-latency`), same merge rules as slots.
- Ranking: a run's browser or server median counts only when at least half its endpoints answered on that side (`LatencyRun.Rank`, `rank()` in JS, tested with the same cases).
- Leaderboards (`LatencyLeaders`): jackpot winners (a counted median under 10 ms) and losers (100 ms or more), newest `LatencyJackpotCapacity` (default 50) each; and the `LatencyRankCapacity` (default 10) lowest and highest counted medians per side, ties to the older run. Every measured or offered run is a candidate.
- Snapshots carry `boards`: six ordered id lists (`fastJackpots`, `slowJackpots`, `browserFastest`, `browserSlowest`, `serverFastest`, `serverSlowest`) and `runs`, the texts the client has not seen. Deltas carry `boards` only when a list changes, with only new texts. Browsers save leader runs to local history.
- After a restart, browsers read every local run and offer their 10 best and 10 worst counted medians per side through `OfferLeaders` (40 per circuit, boards only, same validation as offers). Offers are trusted like slot jackpots; the caps bound forgeries.
- The page lists Results, then Leaderboard, then Jackpots.
- No identity: results hold only timings. The browser marks its own as `mine`. Test count (`chaos-latency-runs`) is in `localStorage`; 10 tests unlock auto run, which tests whenever the machine is free, including in hidden tabs.
