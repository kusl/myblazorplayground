#!/usr/bin/env bash
#
# Build with warnings as errors, then run the .NET and JavaScript tests.
#
#   scripts/test.sh              everything; database tests use a throwaway postgres container
#   scripts/test.sh --no-db      skip the database tests
#   scripts/test.sh --no-js      skip the JavaScript tests
#
# Set PLAYGROUND_TEST_DATABASE to a connection string that may create databases to use your own server.
#

set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
LOG_TAG=test
cd "$ROOT"

use_db=1
use_js=1
for arg in "$@"; do
    case "$arg" in
        --no-db) use_db=0 ;;
        --no-js) use_js=0 ;;
        *) printf 'unknown argument %s\n' "$arg" >&2; exit 2 ;;
    esac
done

require dotnet
container=""
cleanup() {
    if [[ -n "$container" ]]; then
        "$ENGINE" rm -f "$container" >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT

if (( use_db )) && [[ -z "${PLAYGROUND_TEST_DATABASE:-}" ]]; then
    detect_engine
    container="myblazorplayground-test-db-$$"
    step "Starting a throwaway PostgreSQL"
    "$ENGINE" run -d --rm --name "$container" -e POSTGRES_PASSWORD=test -p 127.0.0.1::5432 \
        "${TEST_POSTGRES_IMAGE:-docker.io/library/postgres:18-alpine}" >/dev/null
    host_port="$("$ENGINE" port "$container" 5432/tcp | head -n 1 | sed 's/.*://')"
    for _ in $(seq 1 60); do
        "$ENGINE" exec "$container" pg_isready -U postgres >/dev/null 2>&1 && break
        sleep 1
    done
    export PLAYGROUND_TEST_DATABASE="Host=127.0.0.1;Port=${host_port};Database=postgres;Username=postgres;Password=test"
fi

step "Build"
dotnet build MyBlazorPlayground.slnx --configuration Release -p:ContinuousIntegrationBuild=true

step ".NET tests"
dotnet test --solution MyBlazorPlayground.slnx --configuration Release --no-build --results-directory "$ARTIFACTS/test-results"

if (( use_js )); then
    require node npm
    step "JavaScript tests"
    (cd tests/js && npm install --no-audit --no-fund && npx vitest run)
fi

step "All tests passed"
