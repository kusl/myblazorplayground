# 0010 Quick tunnels, side by side with MyRestaurant

Status: accepted

- The site lives behind a Cloudflare quick tunnel permanently. The URL can change and the site can be down; nothing in the site depends on its public URL. All links are relative.
- It runs on the same host as MyRestaurant without clashing: compose project `myblazorplayground`, web on `127.0.0.1:8090`, PostgreSQL on `127.0.0.1:5433` (MyRestaurant uses 8080, 8443 and 5432), its own volume, image name and tunnel container (`myblazorplayground-tunnel`).
- `scripts/instance.sh up` builds, starts PostgreSQL and the site, waits for `/health/ready`, starts cloudflared with host networking and prints the URL and, before setup, the setup code. `url`, `status`, `logs`, `backup`, `restore`, `down` and `reset` cover the rest.
- `.env` is created from `.env.example` on first use with a random database password. Every variable is assigned there because some podman-compose versions ignore `${VAR:-default}`.
- No `depends_on` waits for health: the migration runner retries the database for two minutes instead.
- The web project sets `RequiresAspNetWebAssets`. The SDK normally infers it from `.razor` files, which the Containerfile's restore layer (project files only) does not have, so `blazor.web.js` was silently left out of the image and every interactive page broke. The publish step now fails if `blazor.web*.js` is missing from `wwwroot/_framework`.
- Inside containers everything is stable: `sdk:11.0` and `aspnet:11.0`, PostgreSQL 18. PostgreSQL 19 reaches general availability on 2026-11-19; moving to it is the image tag in `compose.yaml`, `scripts/test.sh` and the workflow, plus a dump and restore of existing data.
- Locally, `global.json` accepts the 11.0 RC 3 SDK and anything newer in the band.
- The web container runs as the image's non-root `app` user and writes nothing to disk.
- Backups are one `pg_dump`, because keys live in the database too.
