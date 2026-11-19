ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACTS="$ROOT/artifacts"
PROJECT="myblazorplayground"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1

log() { printf '[%s] %s\n' "${LOG_TAG:-playground}" "$*" >&2; }
warn() { printf '[%s] warning: %s\n' "${LOG_TAG:-playground}" "$*" >&2; }
die() { printf '[%s] error: %s\n' "${LOG_TAG:-playground}" "$*" >&2; exit 1; }
step() { printf '\n==> %s\n' "$*"; }

require() {
    local tool
    for tool in "$@"; do
        command -v "$tool" >/dev/null 2>&1 || { printf 'error: %s is required but was not found on PATH\n' "$tool" >&2; exit 127; }
    done
}

detect_engine() {
    if [[ -n "${CONTAINER_ENGINE:-}" ]]; then
        command -v "$CONTAINER_ENGINE" >/dev/null 2>&1 || die "CONTAINER_ENGINE is '$CONTAINER_ENGINE' and that is not on PATH."
        ENGINE="$CONTAINER_ENGINE"
    elif command -v podman >/dev/null 2>&1; then
        ENGINE=podman
    elif command -v docker >/dev/null 2>&1; then
        ENGINE=docker
    else
        die "need podman (preferred) or docker on PATH."
    fi

    if [[ "$ENGINE" == podman ]]; then
        if command -v podman-compose >/dev/null 2>&1; then
            COMPOSE=(podman-compose -p "$PROJECT")
        elif podman compose version >/dev/null 2>&1; then
            COMPOSE=(podman compose -p "$PROJECT")
        else
            die "podman is on PATH but no compose is: install podman-compose (Debian: sudo apt install podman-compose)."
        fi
    elif docker compose version >/dev/null 2>&1; then
        COMPOSE=(docker compose -p "$PROJECT")
    else
        die "docker is on PATH but 'docker compose' is not available."
    fi
}

ensure_env() {
    local env_file="$ROOT/.env"
    if [[ -f "$env_file" ]]; then
        return 0
    fi
    local password
    password="$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 32 || true)"
    [[ ${#password} -eq 32 ]] || die "could not generate a database password."
    sed "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=${password}/" "$ROOT/.env.example" >"$env_file"
    chmod 600 "$env_file"
    log "created .env with a random database password (keep it; the database volume is initialized with it)."
}

env_value() {
    local key="$1" fallback="$2" value
    value="$(sed -n "s/^${key}=//p" "$ROOT/.env" 2>/dev/null | tail -n 1)"
    printf '%s\n' "${value:-$fallback}"
}

http_ok() {
    if command -v curl >/dev/null 2>&1; then
        curl -fsS -o /dev/null --max-time 5 "$1" >/dev/null 2>&1
    else
        wget -q -T 5 -O /dev/null "$1" >/dev/null 2>&1
    fi
}
