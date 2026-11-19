# my blazor playground

A project tracker for small self-organizing teams, a shared house hunting notebook, and a handful of shared toys, in one Blazor app over PostgreSQL. It lives behind a Cloudflare quick tunnel on the same box as MyRestaurant.

- **Work** (`/work`, needs an account): projects with members and editors, a ranked backlog, sprints, a board, stories, tasks, bugs and epics, Markdown descriptions and comments with pasted screenshots and attachments stored in PostgreSQL, and a change history per item. Mobile first.
- **Homes** (`/homes`, no account): paste links from Zillow, Redfin, Realtor.com or anywhere, fill in details together, comment. Everything is stored in PostgreSQL and in each browser, which sync with each other (from nest).
- **Toys** (no account): board, postcard, canvas, slots, latency test and reader, shared live (from chaos). Everything they produce is stored in PostgreSQL and survives restarts.
- **Accounts**: user name and password, with optional two-step sign in through any authenticator app (TOTP) and one-time recovery codes. No passkeys, because quick tunnel addresses change.
- **Roles**: the first administrator is created at `/setup` with a one-time code from the server log. Anyone can edit; only project editors (or home editors) and administrators can delete, deletions go to a trash, only administrators delete for good, and all of it is in the audit log.

## Run it on a server (no .NET SDK needed)

```
scripts/instance.sh up          # build, start, open a quick tunnel, print the URL and the setup code
scripts/instance.sh setup-code  # the code for /setup, until setup is done
scripts/instance.sh url         # the current tunnel URL
scripts/instance.sh status | logs [web|postgres|tunnel] [-f] | backup | restore <file> | down | reset --yes
```

Needs podman with podman-compose (or docker with compose). It uses `127.0.0.1:8090` and `127.0.0.1:5433`, compose project `myblazorplayground` and container `myblazorplayground-tunnel`, so it runs next to MyRestaurant. The first run writes `.env` with a random database password; keep it.

## Develop

```
scripts/dev.sh                  # postgres in a container, dotnet watch on http://127.0.0.1:5090
scripts/test.sh [--no-db] [--no-js]
scripts/export.sh               # docs/llm/dump.txt
```

Everything persists to PostgreSQL: accounts, work, homes and the toys. `/read` serves `src/MyBlazorPlayground.Web/wwwroot/nytimes.txt`, which is part of the repository.

Requires the .NET 11 SDK (RC 3 or later), Node.js 22 or later for the JavaScript tests, and podman or docker for the database. The containers use stable .NET 11 and PostgreSQL 18.

## Layout

| Path | What |
| --- | --- |
| `src/MyBlazorPlayground.Domain` | Rules without I/O: rich text, passwords, access policy |
| `src/MyBlazorPlayground.Data` | PostgreSQL stores (Npgsql and Dapper) and SQL migrations |
| `src/MyBlazorPlayground.Web` | The site: `Work`, `Nest`, `Chaos`, `Identity`, `Administration` |
| `tests/` | xUnit v3 (domain, data against PostgreSQL, web) and Vitest |
| `scripts/` | Instance, development, test and export scripts |
| `docs/adr/` | Architecture decision records |

## Configuration

Environment variables, or `.env` for the containers:

| Variable | Default |
| --- | --- |
| `ConnectionStrings__Playground` | set by compose from `POSTGRES_*` |
| `Site__Name`, `Site__TimeZone`, `Site__SourceUrl` | `my blazor playground`, `America/New_York`, this repository |
| `Site__AllowedOrigins__0` | none; the request host is always allowed |
| `Work__MaxAttachmentBytes` | 99614720 (95 MiB, under Cloudflare's 100 MB request limit) |
| `Chaos__*`, `Nest__*` | limits of the toys and homes (live window sizes; the database keeps everything) |

## License

AGPL-3.0-or-later. See `LICENSE` and `REUSE.toml`. The footer links to the source, as section 13 requires.
