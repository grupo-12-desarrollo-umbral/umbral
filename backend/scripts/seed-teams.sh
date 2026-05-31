#!/usr/bin/env bash
# Usage: ./scripts/seed-teams.sh [BASE_URL] [KEYCLOAK_URL]
# Defaults to services running locally via docker-compose.

set -euo pipefail

BASE_URL="${1:-http://localhost:8000}"
KEYCLOAK_URL="${2:-http://localhost:8080}"

echo "Authenticating as admin against $KEYCLOAK_URL ..."

TOKEN=$(curl -s -X POST "$KEYCLOAK_URL/realms/umbral/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=umbral-web&grant_type=password&username=admin&password=admin123" \
  | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4)

if [[ -z "$TOKEN" ]]; then
  echo "ERROR: Failed to obtain access token. Is Keycloak running?"
  exit 1
fi

echo "Token obtained. Seeding teams against $BASE_URL ..."

teams=(
  '{"displayName":"Delta Team","teamCode":"DELTA"}'
  '{"displayName":"Echo Team","teamCode":"ECHO"}'
  '{"displayName":"Bismarck Team","teamCode":"BISMARCK"}'
  '{"displayName":"Los Panas","teamCode":"PANAS"}'
)

for body in "${teams[@]}"; do
  name=$(echo "$body" | grep -o '"displayName":"[^"]*"' | cut -d'"' -f4)
  response=$(curl -s -o /dev/null -w "%{http_code}" \
    -X POST "$BASE_URL/api/teams" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $TOKEN" \
    -d "$body")

  if [[ "$response" == "201" ]]; then
    echo "  created: $name"
  elif [[ "$response" == "409" ]]; then
    echo "  skipped (already exists): $name"
  else
    echo "  FAILED ($response): $name"
  fi
done

echo "Done."
