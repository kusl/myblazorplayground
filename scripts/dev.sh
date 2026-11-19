#!/usr/bin/env bash
#
# Local development: postgres in a container, the site with dotnet watch on the host.
#
#   scripts/dev.sh           start postgres, then dotnet watch on http://127.0.0.1:5090 (Ctrl+C stops the site)
#   scripts/dev.sh --db      start postgres only and print the connection string
#

set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
LOG_TAG=dev
cd "$ROOT"

detect_engine
ensure_env

user="$(env_value POSTGRES_USER playground)"
password="$(env_value POSTGRES_PASSWORD playground)"
db="$(env_value POSTGRES_DB playground)"
port="$(env_value PLAYGROUND_DB_PORT 5433)"
export ConnectionStrings__Playground="Host=127.0.0.1;Port=${port};Database=${db};Username=${user};Password=${password}"

log "starting postgres on 127.0.0.1:${port}"
"${COMPOSE[@]}" up -d postgres

if [[ "${1:-}" == "--db" ]]; then
    printf '%s\n' "$ConnectionStrings__Playground"
    exit 0
fi

require dotnet
log "the setup code is printed below on first start; open http://127.0.0.1:5090/setup"
exec dotnet watch --project src/MyBlazorPlayground.Web --launch-profile http
