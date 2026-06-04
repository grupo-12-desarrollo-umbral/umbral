#!/usr/bin/env bash
set -euo pipefail

# dev-up.sh — one-command dev stack: (re)create the hot-reload stack and seed it.
#
# This is the local "Track A" runbook wrapped into a single command: bring the
# stack up and load test data so the frontend/mobile (localhost:8000) always
# have something to click through.
#
# It deliberately does NOT run the coverage gate. That is a separate host-side
# flow — `make -C backend gate-all` — which uses Testcontainers and never
# touches this stack. Don't chain them: the hot-reload containers run as root
# over the bind-mounted source, so running the stack pollutes bin/obj with
# root-owned files that block a later host-run gate (clear them with a one-time
# `sudo find services -type d \( -name bin -o -name obj \) -exec rm -rf {} +`).
#
# Usage:
#   ./scripts/dev-up.sh           # down -v, up, wait, seed sessions (fresh slate)
#   ./scripts/dev-up.sh --keep    # skip `down -v` — keep existing DB data
#
# Run from anywhere — it resolves the backend directory itself.

BACKEND_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$BACKEND_DIR"

DOWN=1
for arg in "$@"; do
  case "$arg" in
    --keep)    DOWN=0 ;;
    -h|--help) sed -n '4,21p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $arg (try --help)" >&2; exit 1 ;;
  esac
done

if [[ $DOWN -eq 1 ]]; then
  echo "▶ docker compose down -v  (wiping volumes for a clean slate) …"
  docker compose down -v --remove-orphans
fi

# --wait blocks until services with healthchecks (postgres, keycloak) are
# healthy and the rest are running. The .NET services have no healthcheck, so
# `dotnet watch` may still be compiling when this returns — fine for the psql
# seed below (it only needs postgres), and you'd wait for them by hand anyway.
echo "▶ docker compose up -d --wait  (postgres + keycloak healthchecks) …"
docker compose up -d --wait

echo "▶ Seeding sessions (seed-dev-data.sh — direct psql) …"
./scripts/seed-dev-data.sh

# seed-users goes through the gateway + Keycloak (users, teams, memberships).
# The gateway has no healthcheck, so it may still be compiling after --wait;
# poll until it answers anything (even 401), then seed best-effort so a slow
# start doesn't tear down the run.
echo "▶ Waiting for the gateway to listen on :8000 …"
for _ in $(seq 1 30); do
  code="$(curl -s -o /dev/null -w '%{http_code}' http://localhost:8000/ || echo 000)"
  [[ "$code" != "000" ]] && break
  sleep 2
done

echo "▶ Seeding users, teams and memberships (seed-users.sh — gateway API) …"
./scripts/seed-users.sh \
  || echo "⚠ seed-users failed (gateway/keycloak still warming up?) — rerun ./scripts/seed-users.sh shortly."

echo "✓ Dev stack up and seeded.  Gateway: http://localhost:8000"
