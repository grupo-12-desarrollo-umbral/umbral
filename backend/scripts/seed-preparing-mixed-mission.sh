#!/usr/bin/env bash
# Usage: ./scripts/seed-preparing-mixed-mission.sh
#
# Seeds ONE end-to-end demo so you can walk the whole app workflow:
#
#   * A mixed-play-mode mission authored directly in mission_design (ActivationState=Ready),
#     with a single stage holding FOUR substages in this order:
#       1. Treasure Hunt — 3 targets
#       2. Trivia        — 5 questions   (published quiz "Demo Trivia 5 Preguntas")
#       3. Trivia        — 2 questions   (published quiz "Demo Trivia 2 Preguntas")
#       4. Treasure Hunt — 1 target
#   * A live session created from that mission and driven to the **Preparing** state.
#   * Two participants, one per team, both associated to the session:
#       - samuelpl888@gmail.com / samuel1234  (team "Samuel Team",  code SAMUEL)
#       - grisel@gmail.com      / grisel1234  (team "Grisel Team",  code GRISEL)
#     Both are created in Keycloak with emailVerified=true and the Participant role.
#
# It also (idempotently) ensures the admin + operator identities exist, because the
# session lifecycle needs an assigned operator.
#
# Environment variables (all optional):
#   PGHOST / PGPORT / PGUSER / PGPASSWORD  — postgres (defaults: localhost / 5432 / postgres / postgres)
#   BASE_URL                                — gateway (default: http://localhost:8000)
#   KEYCLOAK_URL                            — Keycloak (default: http://localhost:8080)
#
# Idempotent: safe to run multiple times. Re-running re-authors the mission with fresh
# quiz ids and re-drives a fresh session to Preparing.

set -euo pipefail

BACKEND_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$BACKEND_DIR"

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-postgres}"
PGPASSWORD="${PGPASSWORD:-postgres}"
export PGPASSWORD

BASE_URL="${BASE_URL:-http://localhost:8000}"
KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8080}"
REALM="${REALM:-umbral}"
WEB_CLIENT_ID="${WEB_CLIENT_ID:-umbral-web}"
MASTER_REALM="${MASTER_REALM:-master}"
KEYCLOAK_ADMIN_USERNAME="${KEYCLOAK_ADMIN_USERNAME:-admin}"
KEYCLOAK_ADMIN_PASSWORD="${KEYCLOAK_ADMIN_PASSWORD:-admin}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-admin123}"
OPERATOR_PASSWORD="${OPERATOR_PASSWORD:-operator123}"

MISSION_NAME="Preparing Demo Mixed Mission"
QUIZ5_TITLE="Demo Trivia 5 Preguntas"
QUIZ2_TITLE="Demo Trivia 2 Preguntas"
SESSION_TITLE="Preparing Demo Mixed"

psql_md() { psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d mission_design "$@"; }
# -q suppresses the "INSERT 0 1" command tag so INSERT ... RETURNING yields only the tuple.
psql_md_val() { psql -qAt -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d mission_design -c "$1" | tr -d '[:space:]'; }

# ============================================================================
# Part 1 — author quizzes + mission directly in mission_design (psql)
# ============================================================================

echo "=== 1/3  Authoring quizzes and mixed mission (psql) …"

echo "  waiting for mission_design schema (Missions table) …"
_ready=0
for i in $(seq 1 60); do
  psql_md -c "SELECT 1 FROM \"Missions\" LIMIT 1;" >/dev/null 2>&1 && { _ready=1; break; }
  echo "    attempt $i/60 — not ready yet, waiting 5s …"
  sleep 5
done
if [[ "$_ready" -eq 0 ]]; then
  echo "ERROR: mission_design.\"Missions\" never became available. Aborting." >&2
  exit 1
fi

# Idempotency: drop the mission first (cascades stages/substages/targets/clues), then the
# two demo quizzes it selects (options -> questions -> quizzes). A persisted substage must
# never keep a dangling TriviaQuizId, so the mission goes before the quizzes.
echo "  cleaning previous demo mission + quizzes …"
psql_md -c "DELETE FROM \"Missions\" WHERE \"Name\" = '$MISSION_NAME';"
psql_md -c "
  DELETE FROM \"TriviaOptions\" WHERE \"TriviaQuestionId\" IN (
    SELECT q.\"Id\" FROM \"TriviaQuestions\" q
    JOIN \"TriviaQuizzes\" z ON z.\"Id\" = q.\"TriviaQuizId\"
    WHERE z.\"Title\" IN ('$QUIZ5_TITLE', '$QUIZ2_TITLE'));
  DELETE FROM \"TriviaQuestions\" WHERE \"TriviaQuizId\" IN (
    SELECT \"Id\" FROM \"TriviaQuizzes\" WHERE \"Title\" IN ('$QUIZ5_TITLE', '$QUIZ2_TITLE'));
  DELETE FROM \"TriviaQuizzes\" WHERE \"Title\" IN ('$QUIZ5_TITLE', '$QUIZ2_TITLE');
"

# ---- Quiz with 5 questions (Published) -------------------------------------
echo "  quiz: $QUIZ5_TITLE (5 questions) …"
psql_md -c "
WITH quiz AS (
  INSERT INTO \"TriviaQuizzes\" (\"Title\", \"Description\", \"Status\", \"Created\", \"LastModified\")
  VALUES ('$QUIZ5_TITLE', 'Cinco preguntas de cultura general para la demo.', 'Published', NOW(), NOW())
  RETURNING \"Id\"
),
q1 AS (INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Cuál es el planeta más grande del sistema solar?', 100, 30, 'Júpiter es el planeta más grande.', true FROM quiz RETURNING \"Id\"),
q2 AS (INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿En qué país se encuentra la Torre Eiffel?', 100, 30, 'La Torre Eiffel está en Francia.', true FROM quiz RETURNING \"Id\"),
q3 AS (INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Cuántos lados tiene un hexágono?', 100, 30, 'Un hexágono tiene seis lados.', true FROM quiz RETURNING \"Id\"),
q4 AS (INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Quién pintó La Mona Lisa?', 100, 30, 'Leonardo da Vinci pintó La Mona Lisa.', true FROM quiz RETURNING \"Id\"),
q5 AS (INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Cuál es el océano más grande del mundo?', 100, 30, 'El océano Pacífico es el más grande.', true FROM quiz RETURNING \"Id\")
INSERT INTO \"TriviaOptions\" (\"TriviaQuestionId\", \"OptionText\", \"SequenceOrder\", \"IsCorrect\")
SELECT \"Id\", 'Júpiter', 1, true FROM q1
UNION ALL SELECT \"Id\", 'Saturno', 2, false FROM q1
UNION ALL SELECT \"Id\", 'Neptuno', 3, false FROM q1
UNION ALL SELECT \"Id\", 'La Tierra', 4, false FROM q1
UNION ALL SELECT \"Id\", 'Francia', 1, true FROM q2
UNION ALL SELECT \"Id\", 'Italia', 2, false FROM q2
UNION ALL SELECT \"Id\", 'España', 3, false FROM q2
UNION ALL SELECT \"Id\", 'Alemania', 4, false FROM q2
UNION ALL SELECT \"Id\", 'Seis', 1, true FROM q3
UNION ALL SELECT \"Id\", 'Cinco', 2, false FROM q3
UNION ALL SELECT \"Id\", 'Siete', 3, false FROM q3
UNION ALL SELECT \"Id\", 'Ocho', 4, false FROM q3
UNION ALL SELECT \"Id\", 'Leonardo da Vinci', 1, true FROM q4
UNION ALL SELECT \"Id\", 'Pablo Picasso', 2, false FROM q4
UNION ALL SELECT \"Id\", 'Vincent van Gogh', 3, false FROM q4
UNION ALL SELECT \"Id\", 'Miguel Ángel', 4, false FROM q4
UNION ALL SELECT \"Id\", 'Pacífico', 1, true FROM q5
UNION ALL SELECT \"Id\", 'Atlántico', 2, false FROM q5
UNION ALL SELECT \"Id\", 'Índico', 3, false FROM q5
UNION ALL SELECT \"Id\", 'Ártico', 4, false FROM q5;
"

# ---- Quiz with 2 questions (Published) -------------------------------------
echo "  quiz: $QUIZ2_TITLE (2 questions) …"
psql_md -c "
WITH quiz AS (
  INSERT INTO \"TriviaQuizzes\" (\"Title\", \"Description\", \"Status\", \"Created\", \"LastModified\")
  VALUES ('$QUIZ2_TITLE', 'Dos preguntas rápidas para cerrar la ronda.', 'Published', NOW(), NOW())
  RETURNING \"Id\"
),
q1 AS (INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿De qué color es el cielo despejado durante el día?', 100, 30, 'El cielo despejado se ve azul.', true FROM quiz RETURNING \"Id\"),
q2 AS (INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Cuántos días tiene una semana?', 100, 30, 'Una semana tiene siete días.', true FROM quiz RETURNING \"Id\")
INSERT INTO \"TriviaOptions\" (\"TriviaQuestionId\", \"OptionText\", \"SequenceOrder\", \"IsCorrect\")
SELECT \"Id\", 'Azul', 1, true FROM q1
UNION ALL SELECT \"Id\", 'Verde', 2, false FROM q1
UNION ALL SELECT \"Id\", 'Rojo', 3, false FROM q1
UNION ALL SELECT \"Id\", 'Amarillo', 4, false FROM q1
UNION ALL SELECT \"Id\", 'Siete', 1, true FROM q2
UNION ALL SELECT \"Id\", 'Cinco', 2, false FROM q2
UNION ALL SELECT \"Id\", 'Seis', 3, false FROM q2
UNION ALL SELECT \"Id\", 'Ocho', 4, false FROM q2;
"

QUIZ5_ID="$(psql_md_val "SELECT \"Id\" FROM \"TriviaQuizzes\" WHERE \"Title\" = '$QUIZ5_TITLE' ORDER BY \"Id\" DESC LIMIT 1;")"
QUIZ2_ID="$(psql_md_val "SELECT \"Id\" FROM \"TriviaQuizzes\" WHERE \"Title\" = '$QUIZ2_TITLE' ORDER BY \"Id\" DESC LIMIT 1;")"
if [[ -z "$QUIZ5_ID" || -z "$QUIZ2_ID" ]]; then
  echo "ERROR: could not resolve seeded quiz ids (5:'$QUIZ5_ID' 2:'$QUIZ2_ID')." >&2
  exit 1
fi
echo "  quizzes ready — 5Q id=$QUIZ5_ID, 2Q id=$QUIZ2_ID"

# ---- Mission: one stage, four substages ------------------------------------
# ActivationState is stamped 'Ready' directly (mirroring the Pana Exito seed) so the
# session API accepts it without walking the authoring endpoints. Targets carry their own
# score and default coordinates (0,0); readiness only needs >=1 active target per TH substage
# and a selected published quiz per Trivia substage.
echo "  mission: $MISSION_NAME (4 substages) …"
MISSION_ID="$(psql_md_val "
  INSERT INTO \"Missions\" (\"Name\", \"Description\", \"Difficulty\", \"MaximumTimeMinutes\", \"IsActive\", \"ActivationState\", \"Created\", \"LastModified\")
  VALUES ('$MISSION_NAME', 'Demo de flujo completo: caza del tesoro y trivia intercaladas.', 'Beginner', 60, true, 'Ready', NOW(), NOW())
  RETURNING \"Id\";")"
STAGE_ID="$(psql_md_val "
  INSERT INTO \"MissionStages\" (\"MissionId\", \"Title\", \"SequenceOrder\")
  VALUES ($MISSION_ID, 'Stage 1', 1) RETURNING \"Id\";")"

# Substage 1 — Treasure Hunt, 3 targets (target 1 carries a visible clue).
SUB1_ID="$(psql_md_val "
  INSERT INTO \"MissionSubstages\" (\"StageId\", \"Title\", \"SequenceOrder\", \"PlayMode\")
  VALUES ($STAGE_ID, 'Treasure Hunt A', 1, 'TreasureHunt') RETURNING \"Id\";")"
# Two clues on this treasure-hunt substage (mirrors docs/e2e-manual-test.md §5):
#   - one "Visible when substage starts" (reaches the participant board on arrival), attached to target 1
#   - one "Hidden until operator releases it" (populates the operator's clue-release panel), attached to
#     target 2. For a TreasureHunt substage the release picker is target-keyed (LiveSession
#     .ProjectReleasableClues), so a releasable clue MUST hang off an active target — a substage-scoped
#     clue would be ignored. A target carries at most one clue, hence one clue per target.
SUB1_CLUE_ID="$(psql_md_val "
  INSERT INTO \"MissionClues\" (\"SubstageId\", \"Title\", \"SequenceOrder\", \"Text\", \"Visibility\")
  VALUES ($SUB1_ID, 'Pista inicial', 1, 'Busca la estatua del parque central y escanea su placa.', 'VisibleWhenSubstageStarts')
  RETURNING \"Id\";")"
SUB1_REL_CLUE_ID="$(psql_md_val "
  INSERT INTO \"MissionClues\" (\"SubstageId\", \"Title\", \"SequenceOrder\", \"Text\", \"Visibility\")
  VALUES ($SUB1_ID, 'Pista liberable', 2, 'El segundo objetivo está frente a la plaza principal.', 'HiddenUntilOperatorRelease')
  RETURNING \"Id\";")"
# Coordinates from docs/e2e-manual-test.md (§5): the two documented Caracas targets, plus a
# third real Caracas landmark so every pin is meaningful on the mobile Map tab.
psql_md -c "
  INSERT INTO \"MissionTargets\" (\"SubstageId\", \"Name\", \"QrCode\", \"SequenceOrder\", \"IsActive\", \"Latitude\", \"Longitude\", \"Score\", \"ClueId\")
  VALUES
    ($SUB1_ID, 'Universidad Católica Andrés Bello', 'MIXED-A-1', 1, true, 10.46426, -66.97629, 50, $SUB1_CLUE_ID),
    ($SUB1_ID, 'Plaza Altamira', 'MIXED-A-2', 2, true, 10.49559, -66.84886, 50, $SUB1_REL_CLUE_ID),
    ($SUB1_ID, 'Plaza Bolívar de Caracas', 'MIXED-A-3', 3, true, 10.50661, -66.91461, 50, NULL);
"

# Substage 2 — Trivia, 5 questions.
SUB2_ID="$(psql_md_val "
  INSERT INTO \"MissionSubstages\" (\"StageId\", \"Title\", \"SequenceOrder\", \"PlayMode\", \"TriviaQuizId\")
  VALUES ($STAGE_ID, 'Trivia 5 Preguntas', 2, 'Trivia', $QUIZ5_ID) RETURNING \"Id\";")"

# Substage 3 — Trivia, 2 questions.
SUB3_ID="$(psql_md_val "
  INSERT INTO \"MissionSubstages\" (\"StageId\", \"Title\", \"SequenceOrder\", \"PlayMode\", \"TriviaQuizId\")
  VALUES ($STAGE_ID, 'Trivia 2 Preguntas', 3, 'Trivia', $QUIZ2_ID) RETURNING \"Id\";")"

# Substage 4 — Treasure Hunt, 1 target (with a visible clue).
SUB4_ID="$(psql_md_val "
  INSERT INTO \"MissionSubstages\" (\"StageId\", \"Title\", \"SequenceOrder\", \"PlayMode\")
  VALUES ($STAGE_ID, 'Treasure Hunt B', 4, 'TreasureHunt') RETURNING \"Id\";")"
SUB4_CLUE_ID="$(psql_md_val "
  INSERT INTO \"MissionClues\" (\"SubstageId\", \"Title\", \"SequenceOrder\", \"Text\", \"Visibility\")
  VALUES ($SUB4_ID, 'Pista final', 1, 'El último objetivo está junto a la fuente principal.', 'VisibleWhenSubstageStarts')
  RETURNING \"Id\";")"
psql_md -c "
  INSERT INTO \"MissionTargets\" (\"SubstageId\", \"Name\", \"QrCode\", \"SequenceOrder\", \"IsActive\", \"Latitude\", \"Longitude\", \"Score\", \"ClueId\")
  VALUES ($SUB4_ID, 'Parque del Este', 'MIXED-B-1', 1, true, 10.49820, -66.83630, 50, $SUB4_CLUE_ID);
"

echo "  mission authored: id=$MISSION_ID (stage $STAGE_ID; subs $SUB1_ID/$SUB2_ID/$SUB3_ID/$SUB4_ID)"

# ============================================================================
# Part 2 — wait for gateway + mission-design upstream
# ============================================================================

echo "=== 2/3  Waiting for gateway on :8000 …"
for _ in $(seq 1 30); do
  code="$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/" || echo 000)"
  [[ "$code" != "000" ]] && break
  sleep 2
done

echo "    waiting for mission-design upstream (via gateway) …"
for _ in $(seq 1 60); do
  code="$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/api/missions" || echo 000)"
  case "$code" in 000|502|503|504) sleep 2 ;; *) break ;; esac
done

# ============================================================================
# Part 3 — Keycloak users, bootstrap, teams, session -> Preparing
# ============================================================================

echo "=== 3/3  Seeding users, teams, and the Preparing session …"

# username|role|display name|email|password
declare -a USERS=(
  "admin|Administrator|Admin Umbral|admin@umbral.local|$ADMIN_PASSWORD"
  "operator|Operator|Operator Umbral|operator@umbral.local|$OPERATOR_PASSWORD"
  "samuelpl888|Participant|Samuel Palacios|samuelpl888@gmail.com|samuel1234"
  "grisel|Participant|Grisel Demo|grisel@gmail.com|grisel1234"
)

auth_admin() {
  curl -fsS -X POST "$KEYCLOAK_URL/realms/$MASTER_REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "client_id=admin-cli" -d "grant_type=password" \
    -d "username=$KEYCLOAK_ADMIN_USERNAME" -d "password=$KEYCLOAK_ADMIN_PASSWORD" \
    | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p'
}

auth_user() {
  curl -fsS -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "client_id=$WEB_CLIENT_ID" -d "grant_type=password" \
    -d "username=$1" -d "password=$2" \
    | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p'
}

kc_get_user_id() {
  curl -fsS "$KEYCLOAK_URL/admin/realms/$REALM/users?username=$2&exact=true" \
    -H "Authorization: Bearer $1" \
    | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -n 1
}

kc_create_user() {
  local admin_token="$1" username="$2" email="$3" display_name="$4" password="$5"
  local first_name="${display_name% *}" last_name="${display_name#* }"
  [[ "$display_name" != *" "* ]] && last_name="Umbral"
  curl -fsS -X POST "$KEYCLOAK_URL/admin/realms/$REALM/users" \
    -H "Authorization: Bearer $admin_token" -H "Content-Type: application/json" \
    -d "{\"username\":\"$username\",\"enabled\":true,\"emailVerified\":true,\"email\":\"$email\",\"firstName\":\"$first_name\",\"lastName\":\"$last_name\",\"credentials\":[{\"type\":\"password\",\"value\":\"$password\",\"temporary\":false}]}" \
    >/dev/null
}

kc_get_role_json() {
  curl -fsS "$KEYCLOAK_URL/admin/realms/$REALM/roles/$2" -H "Authorization: Bearer $1"
}

kc_sync_role() {
  local admin_token="$1" user_id="$2" target_role="$3" target_role_json="$4" role_name role_json
  for role_name in Administrator Operator Participant; do
    [[ "$role_name" == "$target_role" ]] && continue
    role_json="$(kc_get_role_json "$admin_token" "$role_name")"
    curl -fsS -X DELETE "$KEYCLOAK_URL/admin/realms/$REALM/users/$user_id/role-mappings/realm" \
      -H "Authorization: Bearer $admin_token" -H "Content-Type: application/json" \
      -d "[$role_json]" >/dev/null || true
  done
  curl -fsS -X POST "$KEYCLOAK_URL/admin/realms/$REALM/users/$user_id/role-mappings/realm" \
    -H "Authorization: Bearer $admin_token" -H "Content-Type: application/json" \
    -d "[$target_role_json]" >/dev/null
}

bootstrap_user() {
  curl -fsS -X POST "$BASE_URL/api/users/authenticated" \
    -H "Authorization: Bearer $1" -H "Content-Type: application/json" \
    -d "{\"displayName\":\"$2\"}" >/dev/null
}

app_user_id() {
  curl -fsS "$BASE_URL/api/users/me" -H "Authorization: Bearer $1" \
    | sed -n 's/.*"userId":\([0-9]*\).*/\1/p'
}

app_register_team() {
  local token="$1" display="$2" code="$3" resp http body
  resp="$(curl -s -w $'\n%{http_code}' -X POST "$BASE_URL/api/teams" \
    -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
    -d "{\"displayName\":\"$display\",\"teamCode\":\"$code\"}")"
  http="${resp##*$'\n'}"; body="${resp%$'\n'*}"
  if [[ "$http" == "201" ]]; then
    sed -n 's/.*"teamId":"\([^"]*\)".*/\1/p' <<<"$body"
  elif [[ "$http" == "409" ]]; then
    curl -fsS "$BASE_URL/api/teams?page=1&pageSize=100" -H "Authorization: Bearer $token" \
      | grep -o "{\"teamId\":\"[^\"]*\",\"displayName\":\"[^\"]*\",\"teamCode\":\"$code\"[^}]*}" \
      | sed -n 's/.*"teamId":"\([^"]*\)".*/\1/p' | head -n 1
  fi
}

app_assign_participant() {
  curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE_URL/api/teams/$2/participants" \
    -H "Authorization: Bearer $1" -H "Content-Type: application/json" \
    -d "{\"userId\":$3}"
}

app_create_session() {
  local token="$1" mission_id="$2" title="$3" scheduled_at resp http body
  scheduled_at="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  resp="$(curl -sS -w $'\n%{http_code}' -X POST "$BASE_URL/api/sessions" \
    -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
    -d "{\"missionId\":$mission_id,\"title\":\"$title\",\"maximumTimeMinutes\":60,\"scheduledAt\":\"$scheduled_at\"}")"
  http="${resp##*$'\n'}"; body="${resp%$'\n'*}"
  if [[ "$http" != "201" ]]; then
    echo "  FAILED ($http): could not create session" >&2; echo "$body" >&2; return 1
  fi
  sed -n 's/.*"liveSessionId":"\([^"]*\)".*/\1/p' <<<"$body"
}

app_assign_operator_to_session() {
  local resp http body
  resp="$(curl -sS -w $'\n%{http_code}' -X PATCH "$BASE_URL/api/sessions/$2/operator-assignment" \
    -H "Authorization: Bearer $1" -H "Content-Type: application/json" \
    -d "{\"operatorUserId\":$3}")"
  http="${resp##*$'\n'}"; body="${resp%$'\n'*}"
  [[ "$http" == "200" ]] || { echo "  FAILED ($http): assign operator" >&2; echo "$body" >&2; return 1; }
}

app_associate_team_to_session() {
  local resp http body
  resp="$(curl -sS -w $'\n%{http_code}' -X POST "$BASE_URL/api/sessions/$2/teams" \
    -H "Authorization: Bearer $1" -H "Content-Type: application/json" \
    -d "{\"referenceTeamId\":\"$3\"}")"
  http="${resp##*$'\n'}"; body="${resp%$'\n'*}"
  [[ "$http" == "200" ]] || { echo "  FAILED ($http): associate team $3" >&2; echo "$body" >&2; return 1; }
}

app_transition_session() {
  local resp http body
  resp="$(curl -sS -w $'\n%{http_code}' -X PATCH "$BASE_URL/api/sessions/$2/state" \
    -H "Authorization: Bearer $1" -H "Content-Type: application/json" \
    -d "{\"targetState\":\"$3\"}")"
  http="${resp##*$'\n'}"; body="${resp%$'\n'*}"
  [[ "$http" == "200" ]] || { echo "  FAILED ($http): transition to $3" >&2; echo "$body" >&2; return 1; }
}

declare -A APP_USER_ID

ADMIN_TOKEN="$(auth_admin)"
if [[ -z "$ADMIN_TOKEN" ]]; then
  echo "  ERROR: Failed to obtain Keycloak admin token from $KEYCLOAK_URL." >&2
  exit 1
fi

for spec in "${USERS[@]}"; do
  IFS='|' read -r username role display_name email password <<<"$spec"

  user_id="$(kc_get_user_id "$ADMIN_TOKEN" "$username")"
  if [[ -z "$user_id" ]]; then
    kc_create_user "$ADMIN_TOKEN" "$username" "$email" "$display_name" "$password"
    user_id="$(kc_get_user_id "$ADMIN_TOKEN" "$username")"
    echo "  created identity: $username ($role)"
  else
    echo "  identity exists: $username ($role)"
  fi
  [[ -z "$user_id" ]] && { echo "  FAILED: no user id for $username" >&2; exit 1; }

  role_json="$(kc_get_role_json "$ADMIN_TOKEN" "$role")"
  kc_sync_role "$ADMIN_TOKEN" "$user_id" "$role" "$role_json"

  user_token="$(auth_user "$username" "$password")"
  [[ -z "$user_token" ]] && { echo "  FAILED: could not authenticate $username" >&2; exit 1; }

  bootstrap_user "$user_token" "$display_name"
  APP_USER_ID["$username"]="$(app_user_id "$user_token" || true)"
  echo "  bootstrapped app user: $username → id ${APP_USER_ID[$username]:-?}"
done

APP_ADMIN_TOKEN="$(auth_user admin "$ADMIN_PASSWORD" || true)"
OPERATOR_TOKEN="$(auth_user operator "$OPERATOR_PASSWORD" || true)"
[[ -z "$APP_ADMIN_TOKEN" ]] && { echo "  ERROR: no app token for admin" >&2; exit 1; }
[[ -z "$OPERATOR_TOKEN" ]] && { echo "  ERROR: no app token for operator" >&2; exit 1; }
OPERATOR_USER_ID="$(app_user_id "$OPERATOR_TOKEN" || true)"
[[ -z "$OPERATOR_USER_ID" ]] && { echo "  ERROR: could not resolve operator app user id" >&2; exit 1; }

echo "  teams (one participant each) …"
# team display|team code|member username
declare -a TEAMS=(
  "Samuel Team|SAMUEL|samuelpl888"
  "Grisel Team|GRISEL|grisel"
)
declare -a TEAM_IDS=()
for spec in "${TEAMS[@]}"; do
  IFS='|' read -r tdisplay tcode tmember <<<"$spec"
  team_id="$(app_register_team "$APP_ADMIN_TOKEN" "$tdisplay" "$tcode" || true)"
  [[ -z "$team_id" ]] && { echo "  FAILED: could not register/resolve team '$tcode'" >&2; exit 1; }
  TEAM_IDS+=("$team_id")
  echo "    team ready: $tdisplay ($tcode) → $team_id"

  uid="${APP_USER_ID[$tmember]:-}"
  [[ -z "$uid" ]] && { echo "    SKIP $tmember (no app user id)"; continue; }
  status="$(app_assign_participant "$APP_ADMIN_TOKEN" "$team_id" "$uid")"
  case "$status" in
    201) echo "    assigned: $tmember (userId $uid)" ;;
    409) echo "    already a member: $tmember (userId $uid)" ;;
    *)   echo "    FAILED ($status): $tmember (userId $uid)" >&2 ;;
  esac
done

echo "  live session → Preparing …"
SESSION_ID="$(app_create_session "$APP_ADMIN_TOKEN" "$MISSION_ID" "$SESSION_TITLE")"
[[ -z "$SESSION_ID" ]] && { echo "  FAILED: session API returned no liveSessionId" >&2; exit 1; }

app_assign_operator_to_session "$APP_ADMIN_TOKEN" "$SESSION_ID" "$OPERATOR_USER_ID"
for team_id in "${TEAM_IDS[@]}"; do
  app_associate_team_to_session "$OPERATOR_TOKEN" "$SESSION_ID" "$team_id"
done
# Stop at Preparing (do NOT advance to Active) per the requested workflow.
app_transition_session "$OPERATOR_TOKEN" "$SESSION_ID" "Preparing"

SESSION_CODE="$(psql -At -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
  SELECT session_code FROM live_sessions WHERE id = '$SESSION_ID';" | tr -d '[:space:]')"

echo ""
echo "Done."
echo "  Mission     : $MISSION_NAME (id $MISSION_ID) — 4 substages [TH×3, Trivia×5, Trivia×2, TH×1]"
echo "  Session     : ${SESSION_CODE:-$SESSION_ID} → Preparing"
echo "  Participants: samuelpl888@gmail.com / samuel1234  → team Samuel Team (SAMUEL)"
echo "                grisel@gmail.com      / grisel1234  → team Grisel Team (GRISEL)"
