#!/usr/bin/env bash
#
# The whole site in containers behind a Cloudflare quick tunnel. Needs podman (or docker), no .NET SDK.
#
#   scripts/instance.sh up [--no-build] [--new-url]   build, start, open the tunnel, print the URL, exit
#   scripts/instance.sh url                            print the current public URL
#   scripts/instance.sh setup-code                     print the one-time code for /setup
#   scripts/instance.sh status                         what is running
#   scripts/instance.sh logs [web|postgres|tunnel] [-f]
#   scripts/instance.sh backup                         dump the database to backups/
#   scripts/instance.sh restore <file>                 restore a dump (replaces everything)
#   scripts/instance.sh down                           stop everything, keep the data
#   scripts/instance.sh reset --yes                    stop everything and delete the data
#
# A quick tunnel's URL changes whenever the tunnel restarts. Nothing on the site depends on it.
#

set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
LOG_TAG=instance
cd "$ROOT"

TUNNEL_CONTAINER="${PLAYGROUND_TUNNEL_CONTAINER:-myblazorplayground-tunnel}"
CLOUDFLARED_IMAGE="${CLOUDFLARED_IMAGE:-docker.io/cloudflare/cloudflared:latest}"
URL_WAIT="${PLAYGROUND_URL_WAIT:-120}"
READY_WAIT="${PLAYGROUND_READY_WAIT:-300}"

usage() {
    awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"
}

command="${1:-up}"
[[ $# -gt 0 ]] && shift

detect_engine
ensure_env
WEB_PORT="$(env_value PLAYGROUND_WEB_PORT 8090)"
TARGET="http://127.0.0.1:${WEB_PORT}"

tunnel_url() {
    "$ENGINE" logs "$TUNNEL_CONTAINER" 2>&1 | grep -oE 'https://[A-Za-z0-9.-]+\.trycloudflare\.com' | tail -n 1 || true
}

tunnel_running() {
    [[ "$("$ENGINE" inspect -f '{{.State.Running}}' "$TUNNEL_CONTAINER" 2>/dev/null || true)" == "true" ]]
}

wait_ready() {
    local deadline=$(( SECONDS + READY_WAIT ))
    while (( SECONDS < deadline )); do
        http_ok "${TARGET}/health/ready" && return 0
        sleep 3
    done
    return 1
}

start_tunnel() {
    local fresh="$1"
    if (( fresh )) || ! tunnel_running; then
        "$ENGINE" rm -f "$TUNNEL_CONTAINER" >/dev/null 2>&1 || true
        log "opening a quick tunnel to ${TARGET}"
        "$ENGINE" run -d --name "$TUNNEL_CONTAINER" --network host --restart unless-stopped \
            "$CLOUDFLARED_IMAGE" tunnel --no-autoupdate --url "$TARGET" >/dev/null
    fi
    local deadline=$(( SECONDS + URL_WAIT )) url=""
    while (( SECONDS < deadline )); do
        url="$(tunnel_url)"
        [[ -n "$url" ]] && break
        tunnel_running || { "$ENGINE" logs "$TUNNEL_CONTAINER" >&2 || true; die "cloudflared stopped before announcing a URL."; }
        sleep 2
    done
    [[ -n "$url" ]] || die "no tunnel URL after ${URL_WAIT}s (see: $0 logs tunnel)."
    printf '%s\n' "$url"
}

setup_code() {
    "${COMPOSE[@]}" logs web 2>&1 | grep -oE 'SETUP CODE: [A-Z0-9-]+' | tail -n 1 | sed 's/SETUP CODE: //' || true
}

case "$command" in
    up)
        build=1
        fresh=0
        for arg in "$@"; do
            case "$arg" in
                --no-build) build=0 ;;
                --new-url) fresh=1 ;;
                *) die "unknown flag '$arg' (try --help)." ;;
            esac
        done
        if (( build )); then
            if git -C "$ROOT" rev-parse HEAD >/dev/null 2>&1; then
                export SOURCE_REVISION="$(git -C "$ROOT" rev-parse HEAD)"
            fi
            log "building the web image"
            "${COMPOSE[@]}" build web
        fi
        log "starting postgres and web"
        "${COMPOSE[@]}" up -d postgres web
        log "waiting for ${TARGET}/health/ready"
        wait_ready || { "${COMPOSE[@]}" logs --tail 60 web >&2 || true; die "the site did not become ready in ${READY_WAIT}s."; }
        url="$(start_tunnel "$fresh")"
        code="$(setup_code)"
        cat >&2 <<BANNER

  my blazor playground is up.

  Public URL:  ${url}
  Local URL:   ${TARGET}
$( [[ -n "$code" ]] && printf '  Setup code:  %s  (open %s/setup to create the first administrator)\n' "$code" "$url" )

  The quick tunnel URL changes when the tunnel restarts. '$0 url' prints the current one.

BANNER
        printf '%s\n' "$url"
        ;;
    url)
        url="$(tunnel_url)"
        [[ -n "$url" ]] || die "no tunnel is running (try: $0 up --no-build)."
        printf '%s\n' "$url"
        ;;
    setup-code)
        code="$(setup_code)"
        [[ -n "$code" ]] || die "no setup code in the log: setup is done, or the site has not started."
        printf '%s\n' "$code"
        ;;
    status)
        "${COMPOSE[@]}" ps
        "$ENGINE" ps --filter "name=${TUNNEL_CONTAINER}"
        if http_ok "${TARGET}/health/ready"; then log "site: ready at ${TARGET}"; else warn "site: not answering at ${TARGET}"; fi
        url="$(tunnel_url)"
        [[ -n "$url" ]] && log "tunnel: ${url}"
        ;;
    logs)
        what="${1:-web}"
        shift || true
        case "$what" in
            web | postgres) "${COMPOSE[@]}" logs "$@" "$what" ;;
            tunnel) "$ENGINE" logs "$@" "$TUNNEL_CONTAINER" ;;
            *) die "logs takes web, postgres or tunnel." ;;
        esac
        ;;
    backup)
        mkdir -p "$ROOT/backups"
        file="$ROOT/backups/playground-$(date -u +%Y%m%dT%H%M%SZ).dump"
        user="$(env_value POSTGRES_USER playground)"
        db="$(env_value POSTGRES_DB playground)"
        "${COMPOSE[@]}" exec -T postgres pg_dump -U "$user" -d "$db" --format=custom >"$file"
        [[ -s "$file" ]] || { rm -f "$file"; die "the dump is empty."; }
        log "wrote ${file}"
        ;;
    restore)
        file="${1:-}"
        [[ -f "$file" ]] || die "restore needs a dump file made by '$0 backup'."
        user="$(env_value POSTGRES_USER playground)"
        db="$(env_value POSTGRES_DB playground)"
        "${COMPOSE[@]}" stop web
        "${COMPOSE[@]}" exec -T postgres pg_restore -U "$user" -d "$db" --clean --if-exists --no-owner <"$file"
        "${COMPOSE[@]}" start web
        log "restored ${file}"
        ;;
    down)
        "$ENGINE" rm -f "$TUNNEL_CONTAINER" >/dev/null 2>&1 || true
        "${COMPOSE[@]}" down
        ;;
    reset)
        [[ "${1:-}" == "--yes" ]] || die "reset deletes the database. Run '$0 reset --yes' if you mean it."
        "$ENGINE" rm -f "$TUNNEL_CONTAINER" >/dev/null 2>&1 || true
        "${COMPOSE[@]}" down -v
        ;;
    -h | --help | help)
        usage
        ;;
    *)
        die "unknown command '$command' (try --help)."
        ;;
esac
