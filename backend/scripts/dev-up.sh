#!/usr/bin/env bash
set -euo pipefail

# dev-up.sh — optional convenience: (re)create the hot-reload stack and seed it.
#
# This is not the canonical local pipeline. Normal development can use
# `docker compose up -d --wait`; this wrapper exists for the explicit case where
# recreating (unless --keep), starting, and seeding in one command is useful.
#
# It deliberately does NOT run the local CI contract. That is a separate
# host-side flow — `make -C backend ci SVC=<service>` or `ci-all` — which uses
# Testcontainers and never needs this persistent stack. The dev override runs
# with the configured host UID/GID and redirects .NET build output away from
# the bind-mounted source tree.
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

echo "▶ Seeding all dev data (seed-all.sh — psql + gateway API) …"
./scripts/seed-all.sh \
  || echo "⚠ seed-all failed (gateway/keycloak still warming up?) — rerun ./scripts/seed-all.sh shortly."

echo "✓ Dev stack up and seeded.  Gateway: http://localhost:8000"
