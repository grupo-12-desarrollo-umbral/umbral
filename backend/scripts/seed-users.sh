#!/usr/bin/env bash
# Usage: ./backend/scripts/seed-users.sh [BASE_URL] [KEYCLOAK_URL]
# Seeds one additional operator and eight participants in Keycloak, then
# bootstraps each authenticated actor into identity-access through the gateway.
# Finally seeds 4 teams (Delta/Echo/Bismarck/Los Panas) and assigns the eight
# participants 2-per-team via the teams API. Idempotent: existing teams (409)
# are reused and existing memberships (409) are skipped.

set -euo pipefail

BASE_URL="${1:-http://localhost:8000}"
KEYCLOAK_URL="${2:-http://localhost:8080}"
REALM="${REALM:-umbral}"
WEB_CLIENT_ID="${WEB_CLIENT_ID:-umbral-web}"
MASTER_REALM="${MASTER_REALM:-master}"
KEYCLOAK_ADMIN_USERNAME="${KEYCLOAK_ADMIN_USERNAME:-admin}"
KEYCLOAK_ADMIN_PASSWORD="${KEYCLOAK_ADMIN_PASSWORD:-admin}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-admin123}"
OPERATOR_PASSWORD="${OPERATOR_PASSWORD:-operator123}"
PARTICIPANT_PASSWORD="${PARTICIPANT_PASSWORD:-participant123}"

declare -a USERS=(
  "admin|Administrator|Admin Umbral|admin@umbral.local|$ADMIN_PASSWORD"
  "operator|Operator|Operator Umbral|operator@umbral.local|$OPERATOR_PASSWORD"
  "operator2|Operator|Operator Dos|operator2@umbral.local|$OPERATOR_PASSWORD"
  "operator3|Operator|Operator Tres|operator3@umbral.local|$OPERATOR_PASSWORD"
  "participant|Participant|Participant Umbral|participant@umbral.local|$PARTICIPANT_PASSWORD"
  "participant01|Participant|Participant 01|participant01@umbral.local|$PARTICIPANT_PASSWORD"
  "participant02|Participant|Participant 02|participant02@umbral.local|$PARTICIPANT_PASSWORD"
  "participant03|Participant|Participant 03|participant03@umbral.local|$PARTICIPANT_PASSWORD"
  "participant04|Participant|Participant 04|participant04@umbral.local|$PARTICIPANT_PASSWORD"
  "participant05|Participant|Participant 05|participant05@umbral.local|$PARTICIPANT_PASSWORD"
  "participant06|Participant|Participant 06|participant06@umbral.local|$PARTICIPANT_PASSWORD"
  "participant07|Participant|Participant 07|participant07@umbral.local|$PARTICIPANT_PASSWORD"
  "participant08|Participant|Participant 08|participant08@umbral.local|$PARTICIPANT_PASSWORD"
)

auth_admin() {
  curl -fsS -X POST "$KEYCLOAK_URL/realms/$MASTER_REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "client_id=admin-cli" \
    -d "grant_type=password" \
    -d "username=$KEYCLOAK_ADMIN_USERNAME" \
    -d "password=$KEYCLOAK_ADMIN_PASSWORD" \
    | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p'
}

auth_user() {
  local username="$1"
  local password="$2"

  curl -fsS -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "client_id=$WEB_CLIENT_ID" \
    -d "grant_type=password" \
    -d "username=$username" \
    -d "password=$password" \
    | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p'
}

kc_get_user_id() {
  local admin_token="$1"
  local username="$2"

  curl -fsS "$KEYCLOAK_URL/admin/realms/$REALM/users?username=$username&exact=true" \
    -H "Authorization: Bearer $admin_token" \
    | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' \
    | head -n 1
}

kc_create_user() {
  local admin_token="$1"
  local username="$2"
  local email="$3"
  local display_name="$4"
  local password="$5"

  local first_name="${display_name% *}"
  local last_name="${display_name#* }"
  if [[ "$display_name" != *" "* ]]; then
    last_name="Umbral"
  fi

  local payload
  payload=$(cat <<JSON
{"username":"$username","enabled":true,"emailVerified":true,"email":"$email","firstName":"$first_name","lastName":"$last_name","credentials":[{"type":"password","value":"$password","temporary":false}]}
JSON
)

  curl -fsS -X POST "$KEYCLOAK_URL/admin/realms/$REALM/users" \
    -H "Authorization: Bearer $admin_token" \
    -H "Content-Type: application/json" \
    -d "$payload" \
    >/dev/null
}

kc_get_role_json() {
  local admin_token="$1"
  local role_name="$2"

  curl -fsS "$KEYCLOAK_URL/admin/realms/$REALM/roles/$role_name" \
    -H "Authorization: Bearer $admin_token"
}

kc_sync_role() {
  local admin_token="$1"
  local user_id="$2"
  local target_role="$3"
  local target_role_json="$4"

  local role_name
  for role_name in Administrator Operator Participant; do
    if [[ "$role_name" == "$target_role" ]]; then
      continue
    fi

    local role_json
    role_json=$(kc_get_role_json "$admin_token" "$role_name")

    curl -fsS -X DELETE "$KEYCLOAK_URL/admin/realms/$REALM/users/$user_id/role-mappings/realm" \
      -H "Authorization: Bearer $admin_token" \
      -H "Content-Type: application/json" \
      -d "[$role_json]" \
      >/dev/null || true
  done

  curl -fsS -X POST "$KEYCLOAK_URL/admin/realms/$REALM/users/$user_id/role-mappings/realm" \
    -H "Authorization: Bearer $admin_token" \
    -H "Content-Type: application/json" \
    -d "[$target_role_json]" \
    >/dev/null
}

bootstrap_user() {
  local access_token="$1"
  local display_name="$2"

  curl -fsS -X POST "$BASE_URL/api/users/authenticated" \
    -H "Authorization: Bearer $access_token" \
    -H "Content-Type: application/json" \
    -d "{\"displayName\":\"$display_name\"}" \
    >/dev/null
}

# Internal int UserId of the authenticated actor (needed by the assign API,
# which keys on the identity_access user id, not the Keycloak GUID).
app_user_id() {
  local user_token="$1"
  curl -fsS "$BASE_URL/api/users/me" \
    -H "Authorization: Bearer $user_token" \
    | sed -n 's/.*"userId":\([0-9]*\).*/\1/p'
}

# Register a team; on 409 (teamCode exists) resolve the existing id. Echoes the
# team GUID, or nothing on hard failure.
app_register_team() {
  local token="$1" display="$2" code="$3"
  local resp http body
  resp="$(curl -s -w $'\n%{http_code}' -X POST "$BASE_URL/api/teams" \
    -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
    -d "{\"displayName\":\"$display\",\"teamCode\":\"$code\"}")"
  http="${resp##*$'\n'}"
  body="${resp%$'\n'*}"
  if [[ "$http" == "201" ]]; then
    sed -n 's/.*"teamId":"\([^"]*\)".*/\1/p' <<<"$body"
  elif [[ "$http" == "409" ]]; then
    curl -fsS "$BASE_URL/api/teams?page=1&pageSize=100" \
      -H "Authorization: Bearer $token" \
      | grep -o "{\"teamId\":\"[^\"]*\",\"displayName\":\"[^\"]*\",\"teamCode\":\"$code\"[^}]*}" \
      | sed -n 's/.*"teamId":"\([^"]*\)".*/\1/p' | head -n 1
  fi
}

# Assign a participant to a team; echoes the HTTP status (201 created, 409
# already a member, anything else = failure).
app_assign_participant() {
  local token="$1" team_id="$2" user_id="$3"
  curl -s -o /dev/null -w '%{http_code}' -X POST \
    "$BASE_URL/api/teams/$team_id/participants" \
    -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
    -d "{\"userId\":$user_id}"
}

# username -> internal int UserId, filled for participant01..08 during the loop.
declare -A PARTICIPANT_IDS

ADMIN_TOKEN="$(auth_admin)"
if [[ -z "$ADMIN_TOKEN" ]]; then
  echo "ERROR: Failed to obtain Keycloak admin token from $KEYCLOAK_URL."
  exit 1
fi

echo "Admin token obtained. Seeding users in realm '$REALM' ..."

for spec in "${USERS[@]}"; do
  IFS='|' read -r username role display_name email password <<<"$spec"

  user_id="$(kc_get_user_id "$ADMIN_TOKEN" "$username")"
  if [[ -z "$user_id" ]]; then
    kc_create_user "$ADMIN_TOKEN" "$username" "$email" "$display_name" "$password"
    user_id="$(kc_get_user_id "$ADMIN_TOKEN" "$username")"
    echo "  created identity: $username"
  else
    echo "  identity exists: $username"
  fi

  if [[ -z "$user_id" ]]; then
    echo "  FAILED: could not resolve user id for $username after create"
    exit 1
  fi

  role_json="$(kc_get_role_json "$ADMIN_TOKEN" "$role")"
  kc_sync_role "$ADMIN_TOKEN" "$user_id" "$role" "$role_json"

  user_token="$(auth_user "$username" "$password")"
  if [[ -z "$user_token" ]]; then
    echo "  FAILED: could not authenticate seeded user $username"
    exit 1
  fi

  bootstrap_user "$user_token" "$display_name"
  echo "  bootstrapped app user: $username ($role)"

  case "$username" in
    participant0[1-8])
      PARTICIPANT_IDS["$username"]="$(app_user_id "$user_token" || true)"
      ;;
  esac
done

# ---------------------------------------------------------------------------
# Teams: 4 teams, eight participants assigned 2-per-team via the teams API.
# ---------------------------------------------------------------------------
echo "Seeding teams and memberships ..."

APP_ADMIN_TOKEN="$(auth_user admin "$ADMIN_PASSWORD" || true)"
if [[ -z "$APP_ADMIN_TOKEN" ]]; then
  echo "ERROR: could not obtain an app token for 'admin' to call the teams API."
  exit 1
fi

declare -a TEAMS=(
  "Delta|DELTA|participant01 participant02"
  "Echo|ECHO|participant03 participant04"
  "Bismarck|BISMARCK|participant05 participant06"
  "Los Panas|PANAS|participant07 participant08"
)

for spec in "${TEAMS[@]}"; do
  IFS='|' read -r tdisplay tcode tmembers <<<"$spec"

  team_id="$(app_register_team "$APP_ADMIN_TOKEN" "$tdisplay" "$tcode" || true)"
  if [[ -z "$team_id" ]]; then
    echo "  FAILED: could not register or resolve team '$tcode'"
    exit 1
  fi
  echo "  team ready: $tdisplay ($tcode)"

  for member in $tmembers; do
    uid="${PARTICIPANT_IDS[$member]:-}"
    if [[ -z "$uid" ]]; then
      echo "    SKIP $member (no resolved user id)"
      continue
    fi
    status="$(app_assign_participant "$APP_ADMIN_TOKEN" "$team_id" "$uid")"
    case "$status" in
      201) echo "    assigned: $member (userId $uid)" ;;
      409) echo "    already a member: $member" ;;
      *)   echo "    FAILED ($status): $member (userId $uid)" ;;
    esac
  done
done

echo "Done."
