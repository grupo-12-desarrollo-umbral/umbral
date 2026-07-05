#!/usr/bin/env bash
# Usage: ./scripts/seed-all.sh
# Seeds the entire dev stack: trivia quizzes, live sessions, teams, users,
# and team memberships — all in one shot.
#
# 1. Seeds trivia + sessions + teams directly in postgres
# 2. Waits for the gateway to come online
# 3. Seeds users in Keycloak and bootstraps them into identity-access
# 4. Seeds team memberships
#
# Environment variables (all optional):
#   PGHOST / PGPORT / PGUSER / PGPASSWORD  — postgres (defaults: localhost / 5432 / postgres / postgres)
#   BASE_URL                                — gateway (default: http://localhost:8000)
#   KEYCLOAK_URL                            — Keycloak (default: http://localhost:8080)
#
# Idempotent: safe to run multiple times.

set -euo pipefail

BACKEND_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$BACKEND_DIR"

# ============================================================================
# Part 1 — psql-based seed: quizzes, sessions, teams
# ============================================================================

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-postgres}"
PGPASSWORD="${PGPASSWORD:-postgres}"
export PGPASSWORD

SEEDED_LIVE_TRIVIA_TITLE="Seeded Live Trivia"
SEEDED_LIVE_TRIVIA_SOURCE_TITLE="Filosofos de Atenas"
SEEDED_LIVE_TRIVIA_MISSION_NAME="Seeded Live Trivia Mission"

echo "=== 1/3  Seeding trivia, sessions, and teams (psql) …"

declare -A SESSIONS=(
  [SMOKE1]=b1000000-0000-0000-0000-000000000000:Scheduled:b1000000-0000-0000-0000-000000000016:DV-SOON:Soon
  [SMOKE2]=b1000000-0000-0000-0000-000000000001:Active:b1000000-0000-0000-0000-000000000010:DV-SMK:Smoke
  [SMOKE3]=b1000000-0000-0000-0000-000000000002:Scheduled:b1000000-0000-0000-0000-000000000011:DV-ECH:Echo
  [SMOKE4]=b1000000-0000-0000-0000-000000000003:Preparing:b1000000-0000-0000-0000-000000000012:DV-FOX:Foxtrot
  [SMOKE5]=b1000000-0000-0000-0000-000000000004:Paused:b1000000-0000-0000-0000-000000000013:DV-GLF:Golf
  [SMOKE6]=b1000000-0000-0000-0000-000000000005:Finished:b1000000-0000-0000-0000-000000000014:DV-HTL:Hotel
  [SMOKE7]=b1000000-0000-0000-0000-000000000006:Cancelled:b1000000-0000-0000-0000-000000000015:DV-IND:India
)

# Wait for EF Core migrations to apply all columns (check for the one that
# is added last among pending migrations — reference_team_id from
# AddAssociatedTeamReferenceCorrelation).
echo "Waiting for session_operations migrations (checking reference_team_id) …"
_ready=0
for i in $(seq 1 60); do
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
    SELECT column_name
    FROM information_schema.columns
    WHERE table_name = 'live_session_teams' AND column_name = 'reference_team_id';
  " 2>/dev/null | grep -q reference_team_id && { _ready=1; break; }
  echo "  attempt $i/60 — not ready yet, waiting 5s …"
  sleep 5
done
if [[ "$_ready" -eq 0 ]]; then
  echo "ERROR: session_operations.live_sessions never became available. Aborting." >&2
  exit 1
fi

# Delete from session_operations first (cascades to teams/participants).
# identity_access rows are handled by ON CONFLICT DO NOTHING below.
for CODE in "${!SESSIONS[@]}"; do
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
    DELETE FROM live_sessions WHERE session_code = '$CODE';
  " 2>/dev/null || true
done

while IFS= read -r live_session_id; do
  [[ -z "$live_session_id" ]] && continue
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d identity_access -c "
    DELETE FROM session_team_associations WHERE live_session_id = '$live_session_id';
    DELETE FROM live_sessions WHERE id = '$live_session_id';
  " 2>/dev/null || true
done < <(psql -At -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
  SELECT id FROM live_sessions WHERE title_snapshot = '$SEEDED_LIVE_TRIVIA_TITLE';
" 2>/dev/null || true)

psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
  DELETE FROM live_sessions WHERE title_snapshot = '$SEEDED_LIVE_TRIVIA_TITLE';
" 2>/dev/null || true

echo "  mission_design (trivia quizzes) …"
# Delete the seeded mission first (cascades to its stages/substages). Quizzes are wiped
# and recreated with fresh serial ids every run, so the mission's quiz selection must be
# reauthored each run too — otherwise it dangles and the mission stops being runtime-ready.
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d mission_design -c "
  DELETE FROM \"Missions\" WHERE \"Name\" = '$SEEDED_LIVE_TRIVIA_MISSION_NAME';
  DELETE FROM \"TriviaOptions\";
  DELETE FROM \"TriviaQuestions\";
  DELETE FROM \"TriviaQuizzes\";
"
# Filosofos de Atenas — Published
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d mission_design -c "
WITH quiz AS (
  INSERT INTO \"TriviaQuizzes\" (\"Title\", \"Description\", \"Status\", \"Created\", \"LastModified\")
  VALUES ('Filosofos de Atenas', 'Los pensadores que marcaron la antiguedad.', 'Published', NOW(), NOW())
  RETURNING \"Id\"
),
q1 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Quién fue el maestro de Platón?', 1, 100, 30, 'Sócrates fue el maestro de Platón.', true FROM quiz
  RETURNING \"Id\"
),
q2 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Qué filósofo fundó la Academia de Atenas?', 2, 100, 30, 'Platón fundó la Academia de Atenas.', true FROM quiz
  RETURNING \"Id\"
),
q3 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Cuál de estos filósofos fue discípulo de Platón?', 3, 100, 30, 'Aristóteles fue discípulo de Platón.', true FROM quiz
  RETURNING \"Id\"
)
INSERT INTO \"TriviaOptions\" (\"TriviaQuestionId\", \"OptionText\", \"SequenceOrder\", \"IsCorrect\")
SELECT \"Id\", 'Sócrates', 1, true FROM q1
UNION ALL SELECT \"Id\", 'Aristóteles', 2, false FROM q1
UNION ALL SELECT \"Id\", 'Pitágoras', 3, false FROM q1
UNION ALL SELECT \"Id\", 'Demócrito', 4, false FROM q1
UNION ALL SELECT \"Id\", 'Platón', 1, true FROM q2
UNION ALL SELECT \"Id\", 'Sócrates', 2, false FROM q2
UNION ALL SELECT \"Id\", 'Aristóteles', 3, false FROM q2
UNION ALL SELECT \"Id\", 'Epicuro', 4, false FROM q2
UNION ALL SELECT \"Id\", 'Aristóteles', 1, true FROM q3
UNION ALL SELECT \"Id\", 'Sócrates', 2, false FROM q3
UNION ALL SELECT \"Id\", 'Heráclito', 3, false FROM q3
UNION ALL SELECT \"Id\", 'Tales', 4, false FROM q3;
"
# Mejores guitarristas de la historia — Published
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d mission_design -c "
WITH quiz AS (
  INSERT INTO \"TriviaQuizzes\" (\"Title\", \"Description\", \"Status\", \"Created\", \"LastModified\")
  VALUES ('Mejores guitarristas de la historia', 'Los maestros de las seis cuerdas.', 'Published', NOW(), NOW())
  RETURNING \"Id\"
),
q1 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Qué guitarrista es conocido como \"Slowhand\"?', 1, 100, 30, 'Eric Clapton es apodado Slowhand.', true FROM quiz
  RETURNING \"Id\"
),
q2 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Cuál de estos guitarristas revolucionó el rock con su técnica en los años 60?', 2, 100, 30, 'Jimi Hendrix revolucionó la guitarra eléctrica.', true FROM quiz
  RETURNING \"Id\"
),
q3 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Qué guitarrista popularizó la técnica del tapping en el rock?', 3, 100, 30, 'Eddie Van Halen popularizó el tapping.', true FROM quiz
  RETURNING \"Id\"
)
INSERT INTO \"TriviaOptions\" (\"TriviaQuestionId\", \"OptionText\", \"SequenceOrder\", \"IsCorrect\")
SELECT \"Id\", 'Eric Clapton', 1, true FROM q1
UNION ALL SELECT \"Id\", 'Jimmy Page', 2, false FROM q1
UNION ALL SELECT \"Id\", 'Jeff Beck', 3, false FROM q1
UNION ALL SELECT \"Id\", 'B.B. King', 4, false FROM q1
UNION ALL SELECT \"Id\", 'Jimi Hendrix', 1, true FROM q2
UNION ALL SELECT \"Id\", 'Eric Clapton', 2, false FROM q2
UNION ALL SELECT \"Id\", 'Pete Townshend', 3, false FROM q2
UNION ALL SELECT \"Id\", 'Carlos Santana', 4, false FROM q2
UNION ALL SELECT \"Id\", 'Eddie Van Halen', 1, true FROM q3
UNION ALL SELECT \"Id\", 'Steve Vai', 2, false FROM q3
UNION ALL SELECT \"Id\", 'Joe Satriani', 3, false FROM q3
UNION ALL SELECT \"Id\", 'Yngwie Malmsteen', 4, false FROM q3;
"
# Peliculas mas vistas en los ultimos 5 anos — Published
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d mission_design -c "
WITH quiz AS (
  INSERT INTO \"TriviaQuizzes\" (\"Title\", \"Description\", \"Status\", \"Created\", \"LastModified\")
  VALUES ('Peliculas mas vistas en los ultimos 5 anos', 'El cine que marco la decada.', 'Published', NOW(), NOW())
  RETURNING \"Id\"
),
q1 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Cuál fue la película más taquillera de 2023?', 1, 100, 30, 'Barbie fue la película más taquillera de 2023.', true FROM quiz
  RETURNING \"Id\"
),
q2 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Qué película de 2022 rompió récords como secuela de un clásico de los 80?', 2, 100, 30, 'Top Gun: Maverick fue un éxito masivo en 2022.', true FROM quiz
  RETURNING \"Id\"
),
q3 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Cuál de estas películas ganó el Oscar a Mejor Película en 2024?', 3, 100, 30, 'Oppenheimer ganó el Oscar a Mejor Película en 2024.', true FROM quiz
  RETURNING \"Id\"
)
INSERT INTO \"TriviaOptions\" (\"TriviaQuestionId\", \"OptionText\", \"SequenceOrder\", \"IsCorrect\")
SELECT \"Id\", 'Barbie', 1, true FROM q1
UNION ALL SELECT \"Id\", 'Oppenheimer', 2, false FROM q1
UNION ALL SELECT \"Id\", 'Super Mario Bros', 3, false FROM q1
UNION ALL SELECT \"Id\", 'Guardianes de la Galaxia Vol. 3', 4, false FROM q1
UNION ALL SELECT \"Id\", 'Top Gun: Maverick', 1, true FROM q2
UNION ALL SELECT \"Id\", 'Avatar: The Way of Water', 2, false FROM q2
UNION ALL SELECT \"Id\", 'Jurassic World: Dominion', 3, false FROM q2
UNION ALL SELECT \"Id\", 'Doctor Strange in the Multiverse of Madness', 4, false FROM q2
UNION ALL SELECT \"Id\", 'Oppenheimer', 1, true FROM q3
UNION ALL SELECT \"Id\", 'Barbie', 2, false FROM q3
UNION ALL SELECT \"Id\", 'Killers of the Flower Moon', 3, false FROM q3
UNION ALL SELECT \"Id\", 'Poor Things', 4, false FROM q3;
"
# Musica y su historia — Draft
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d mission_design -c "
WITH quiz AS (
  INSERT INTO \"TriviaQuizzes\" (\"Title\", \"Description\", \"Status\", \"Created\", \"LastModified\")
  VALUES ('Musica y su historia', 'Un recorrido por los generos musicales.', 'Draft', NOW(), NOW())
  RETURNING \"Id\"
),
q1 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Qué género musical se originó en Nueva Orleans a principios del siglo XX?', 1, 100, 30, 'El jazz nació en Nueva Orleans.', true FROM quiz
  RETURNING \"Id\"
),
q2 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Cuál de estos artistas es conocido como el \"Rey del Pop\"?', 2, 100, 30, 'Michael Jackson es el Rey del Pop.', true FROM quiz
  RETURNING \"Id\"
),
q3 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Qué banda británica lanzó el álbum \"The Dark Side of the Moon\"?', 3, 100, 30, 'Pink Floyd lanzó The Dark Side of the Moon.', true FROM quiz
  RETURNING \"Id\"
)
INSERT INTO \"TriviaOptions\" (\"TriviaQuestionId\", \"OptionText\", \"SequenceOrder\", \"IsCorrect\")
SELECT \"Id\", 'Jazz', 1, true FROM q1
UNION ALL SELECT \"Id\", 'Blues', 2, false FROM q1
UNION ALL SELECT \"Id\", 'Rock and Roll', 3, false FROM q1
UNION ALL SELECT \"Id\", 'Country', 4, false FROM q1
UNION ALL SELECT \"Id\", 'Michael Jackson', 1, true FROM q2
UNION ALL SELECT \"Id\", 'Prince', 2, false FROM q2
UNION ALL SELECT \"Id\", 'Madonna', 3, false FROM q2
UNION ALL SELECT \"Id\", 'Elvis Presley', 4, false FROM q2
UNION ALL SELECT \"Id\", 'Pink Floyd', 1, true FROM q3
UNION ALL SELECT \"Id\", 'Led Zeppelin', 2, false FROM q3
UNION ALL SELECT \"Id\", 'The Beatles', 3, false FROM q3
UNION ALL SELECT \"Id\", 'Queen', 4, false FROM q3;
"
# Capitales del mundo — Archived
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d mission_design -c "
WITH quiz AS (
  INSERT INTO \"TriviaQuizzes\" (\"Title\", \"Description\", \"Status\", \"Created\", \"LastModified\")
  VALUES ('Capitales del mundo', 'Pon a prueba tus conocimientos geograficos.', 'Archived', NOW(), NOW())
  RETURNING \"Id\"
),
q1 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Cuál es la capital de Francia?', 1, 100, 30, 'París es la capital de Francia.', true FROM quiz
  RETURNING \"Id\"
),
q2 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Cuál es la capital de Japón?', 2, 100, 30, 'Tokio es la capital de Japón.', true FROM quiz
  RETURNING \"Id\"
),
q3 AS (
  INSERT INTO \"TriviaQuestions\" (\"TriviaQuizId\", \"Prompt\", \"SequenceOrder\", \"ScoreValue\", \"TimeLimitSeconds\", \"Explanation\", \"IsActive\")
  SELECT \"Id\", '¿Cuál es la capital de Australia?', 3, 100, 30, 'Canberra es la capital de Australia.', true FROM quiz
  RETURNING \"Id\"
)
INSERT INTO \"TriviaOptions\" (\"TriviaQuestionId\", \"OptionText\", \"SequenceOrder\", \"IsCorrect\")
SELECT \"Id\", 'París', 1, true FROM q1
UNION ALL SELECT \"Id\", 'Lyon', 2, false FROM q1
UNION ALL SELECT \"Id\", 'Marsella', 3, false FROM q1
UNION ALL SELECT \"Id\", 'Toulouse', 4, false FROM q1
UNION ALL SELECT \"Id\", 'Tokio', 1, true FROM q2
UNION ALL SELECT \"Id\", 'Osaka', 2, false FROM q2
UNION ALL SELECT \"Id\", 'Kioto', 3, false FROM q2
UNION ALL SELECT \"Id\", 'Yokohama', 4, false FROM q2
UNION ALL SELECT \"Id\", 'Canberra', 1, true FROM q3
UNION ALL SELECT \"Id\", 'Sídney', 2, false FROM q3
UNION ALL SELECT \"Id\", 'Melbourne', 3, false FROM q3
UNION ALL SELECT \"Id\", 'Brisbane', 4, false FROM q3;
"

echo "  identity_access (sessions + teams) …"
for CODE in "${!SESSIONS[@]}"; do
  IFS=: read -r SID STATE TID TCODE TDISPLAY <<< "${SESSIONS[$CODE]}"

  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d identity_access -c "
    INSERT INTO live_sessions (id, session_code, created_at, updated_at)
    VALUES ('$SID', '$CODE', now(), now())
    ON CONFLICT (id) DO NOTHING;
  "
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d identity_access -c "
    INSERT INTO teams (id, display_name, team_code, is_active, created_at, updated_at)
    VALUES ('$TID', '$TDISPLAY Team', '$TCODE', true, now(), now())
    ON CONFLICT (id) DO NOTHING;
  "
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d identity_access -c "
    INSERT INTO session_team_associations (id, live_session_id, team_id)
    VALUES (gen_random_uuid(), '$SID', '$TID')
    ON CONFLICT (live_session_id, team_id) DO NOTHING;
  "
done

echo "  session_operations (live sessions + teams) …"
for CODE in "${!SESSIONS[@]}"; do
  IFS=: read -r SID STATE TID TCODE TDISPLAY <<< "${SESSIONS[$CODE]}"
  # The runtime TeamId (session-operations) is independent of the Identity team id.
  # A real association mints a fresh runtime id and stores the Identity id separately
  # in reference_team_id; mirror that here (c-prefixed) instead of reusing $TID for both,
  # so seeded sessions exercise the same team-resolution path as API-created ones.
  RUNTIME_TID="c${TID:1}"
  SCHEDULED_AT="now()"
  [[ "$CODE" == "SMOKE1" ]] && SCHEDULED_AT="now() + interval '3 minutes'"

  ADVANCING_SINCE="null"
  [[ "$STATE" == "Active" ]] && ADVANCING_SINCE="now() - interval '5 minutes'"

  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
    INSERT INTO live_sessions (
      id, source_entity_type, source_entity_id,
      session_code, title_snapshot, state,
      scheduled_at, last_state_changed_at, maximum_time_minutes,
      created_at, updated_at,
      session_timer_total_duration, session_timer_remaining_duration,
      session_timer_advancing_since, session_timer_expired_at
    ) VALUES (
      '$SID', 'Mission', gen_random_uuid(),
      '$CODE', '$TDISPLAY Team', '$STATE',
      $SCHEDULED_AT, now(), 60,
      now(), now(),
      interval '60 minutes', interval '60 minutes',
      $ADVANCING_SINCE, null
    )
    ON CONFLICT (id) DO UPDATE SET
      session_timer_total_duration = EXCLUDED.session_timer_total_duration,
      session_timer_remaining_duration = EXCLUDED.session_timer_remaining_duration,
      session_timer_advancing_since = EXCLUDED.session_timer_advancing_since,
      session_timer_expired_at = EXCLUDED.session_timer_expired_at,
      state = EXCLUDED.state,
      title_snapshot = EXCLUDED.title_snapshot,
      updated_at = now();
  "
  # A live session owns a REQUIRED immutable MissionRuntimeSnapshot (HU-17); without it the
  # repository deep-load throws InvalidCastException ('Column live_session_id is null') → 500.
  # Mirror a well-formed GUID-seeded snapshot: one snapshot root + Stage + Trivia substage +
  # trivia questions/options. Deterministic ids derived from the session id (d0/d1/d2/d3 prefix).
  # DELETE-then-insert (FK ON DELETE CASCADE) keeps it idempotent even if the live_session row
  # survived a prior run.
  SNAP_ID="d1${SID:2}"; STAGE_ID="d2${SID:2}"; SUB_ID="d3${SID:2}"; MISSION_ID="d0${SID:2}"
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
    DELETE FROM live_session_mission_runtime_snapshots WHERE live_session_id = '$SID';
    INSERT INTO live_session_mission_runtime_snapshots
      (live_session_id, id, source_mission_id, mission_title, maximum_time_minutes)
    VALUES ('$SID', '$SNAP_ID', '$MISSION_ID', '$TDISPLAY Mission', 60);
    INSERT INTO live_session_mission_runtime_snapshot_stages
      (id, title, sequence_order, live_session_id)
    VALUES ('$STAGE_ID', 'Stage 1', 1, '$SID');
    INSERT INTO live_session_mission_runtime_snapshot_substages
      (id, title, sequence_order, play_mode, winner_score, stage_snapshot_id)
    VALUES ('$SUB_ID', 'Trivia Substage', 1, 'Trivia', null, '$STAGE_ID');
    WITH q AS (
      INSERT INTO live_session_mission_runtime_snapshot_trivia_questions
        (substage_snapshot_id, prompt, sequence_order, score_value, time_limit_seconds, explanation, live_session_id)
      VALUES
        ('$SUB_ID', '¿Quién fue el maestro de Platón?', 1, 100, 30, 'Sócrates fue el maestro de Platón.', '$SID'),
        ('$SUB_ID', '¿Qué filósofo fundó la Academia de Atenas?', 2, 100, 30, 'Platón fundó la Academia de Atenas.', '$SID'),
        ('$SUB_ID', '¿Cuál de estos filósofos fue discípulo de Platón?', 3, 100, 30, 'Aristóteles fue discípulo de Platón.', '$SID')
      RETURNING id, sequence_order
    )
    INSERT INTO live_session_mission_runtime_snapshot_trivia_options
      (option_text, sequence_order, is_correct, trivia_question_snapshot_id)
    SELECT o.option_text, o.seq, o.is_correct, q.id
    FROM q
    JOIN (VALUES
      (1, 'Sócrates', 1, true), (1, 'Aristóteles', 2, false), (1, 'Pitágoras', 3, false), (1, 'Demócrito', 4, false),
      (2, 'Platón', 1, true), (2, 'Sócrates', 2, false), (2, 'Aristóteles', 3, false), (2, 'Epicuro', 4, false),
      (3, 'Aristóteles', 1, true), (3, 'Sócrates', 2, false), (3, 'Heráclito', 3, false), (3, 'Tales', 4, false)
    ) AS o(qseq, option_text, seq, is_correct) ON o.qseq = q.sequence_order;
  "
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
    INSERT INTO live_session_teams (
      id, live_session_id, team_code, display_name,
      team_capacity, current_score, released_clue_count, join_status,
      reference_team_id
    ) VALUES (
      '$RUNTIME_TID', '$SID', '$TCODE', '$TDISPLAY Team',
      10, null, 0, 'Open',
      '$TID'
    )
    ON CONFLICT (id) DO UPDATE SET reference_team_id = EXCLUDED.reference_team_id;
  "
done

declare -A SECOND_TEAMS=(
  [SMOKE1]=b2000000-0000-0000-0000-000000000016:DV-SN2:Soon2
  [SMOKE2]=b2000000-0000-0000-0000-000000000010:DV-SM2:Smoke2
  [SMOKE3]=b2000000-0000-0000-0000-000000000011:DV-EC2:Echo2
  [SMOKE4]=b2000000-0000-0000-0000-000000000012:DV-FX2:Foxtrot2
  [SMOKE5]=b2000000-0000-0000-0000-000000000013:DV-GL2:Golf2
  [SMOKE6]=b2000000-0000-0000-0000-000000000014:DV-HT2:Hotel2
  [SMOKE7]=b2000000-0000-0000-0000-000000000015:DV-IN2:India2
)

for CODE in "${!SECOND_TEAMS[@]}"; do
  IFS=: read -r TID TCODE TDISPLAY <<< "${SECOND_TEAMS[$CODE]}"
  IFS=: read -r SID _ _ _ _ <<< "${SESSIONS[$CODE]}"
  RUNTIME_TID="c${TID:1}"

  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d identity_access -c "
    INSERT INTO teams (id, display_name, team_code, is_active, created_at, updated_at)
    VALUES ('$TID', '$TDISPLAY Team', '$TCODE', true, now(), now())
    ON CONFLICT (id) DO NOTHING;
  "
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d identity_access -c "
    INSERT INTO session_team_associations (id, live_session_id, team_id)
    VALUES (gen_random_uuid(), '$SID', '$TID')
    ON CONFLICT (live_session_id, team_id) DO NOTHING;
  "
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
    INSERT INTO live_session_teams (
      id, live_session_id, team_code, display_name,
      team_capacity, current_score, released_clue_count, join_status,
      reference_team_id
    ) VALUES (
      '$RUNTIME_TID', '$SID', '$TCODE', '$TDISPLAY Team',
      10, null, 0, 'Open',
      '$TID'
    )
    ON CONFLICT (id) DO UPDATE SET reference_team_id = EXCLUDED.reference_team_id;
  "
done

# ============================================================================
# Part 2 — wait for gateway (needed by Part 3)
# ============================================================================

echo "=== 2/3  Waiting for gateway on :8000 …"
for _ in $(seq 1 30); do
  code="$(curl -s -o /dev/null -w '%{http_code}' http://localhost:8000/ || echo 000)"
  [[ "$code" != "000" ]] && break
  sleep 2
done

# ============================================================================
# Part 3 — Keycloak users, gateway bootstrap, teams
# ============================================================================

echo "=== 3/3  Seeding users and team memberships …"

BASE_URL="${BASE_URL:-http://localhost:8000}"
KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8080}"
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

app_user_id() {
  local user_token="$1"
  curl -fsS "$BASE_URL/api/users/me" \
    -H "Authorization: Bearer $user_token" \
    | sed -n 's/.*"userId":\([0-9]*\).*/\1/p'
}

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

app_assign_participant() {
  local token="$1" team_id="$2" user_id="$3"
  curl -s -o /dev/null -w '%{http_code}' -X POST \
    "$BASE_URL/api/teams/$team_id/participants" \
    -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
    -d "{\"userId\":$user_id}"
}

seed_ready_mission() {
  # Authors (idempotently) a runtime-ready, mission-only Trivia mission from a published
  # quiz and echoes its mission id. A single Stage + single Trivia substage with a quiz
  # selected is sufficient for readiness (no TreasureHunt ⇒ no target/winner-score reqs).
  local token="$1" trivia_quiz_id="$2"
  local name="$SEEDED_LIVE_TRIVIA_MISSION_NAME"
  local resp http body mission_id stage_id substage_id

  # Always authored fresh: the cleanup block deletes the prior mission each run because the
  # quiz it selects is wiped and recreated with a new id every run (see Part 1 cleanup).

  # POST /api/missions
  resp="$(curl -sS -w $'\n%{http_code}' -X POST "$BASE_URL/api/missions" \
    -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
    -d "{\"name\":\"$name\",\"description\":\"Seeded mission-only Trivia for the live session fixture\",\"difficulty\":\"Beginner\",\"maximumTimeMinutes\":10}")"
  http="${resp##*$'\n'}"; body="${resp%$'\n'*}"
  if [[ "$http" != "201" ]]; then
    echo "  FAILED ($http): could not create seeded mission" >&2; echo "$body" >&2; return 1
  fi
  mission_id="$(sed -n 's/^{"id":\([0-9]*\).*/\1/p' <<<"$body")"
  if [[ -z "$mission_id" ]]; then
    echo "  FAILED: created mission returned no id" >&2; echo "$body" >&2; return 1
  fi

  # POST /api/missions/{id}/nodes — Stage
  resp="$(curl -sS -w $'\n%{http_code}' -X POST "$BASE_URL/api/missions/$mission_id/nodes" \
    -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
    -d "{\"nodeType\":\"Stage\",\"title\":\"Stage 1\",\"sequenceOrder\":1}")"
  http="${resp##*$'\n'}"; body="${resp%$'\n'*}"
  if [[ "$http" != "200" ]]; then
    echo "  FAILED ($http): could not add stage to seeded mission" >&2; echo "$body" >&2; return 1
  fi
  stage_id="$(sed -n 's/.*"stages":\[{"id":\([0-9]*\).*/\1/p' <<<"$body")"
  if [[ -z "$stage_id" ]]; then
    echo "  FAILED: seeded mission stage returned no id" >&2; echo "$body" >&2; return 1
  fi

  # POST /api/missions/{id}/nodes — Trivia substage
  resp="$(curl -sS -w $'\n%{http_code}' -X POST "$BASE_URL/api/missions/$mission_id/nodes" \
    -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
    -d "{\"nodeType\":\"Substage\",\"title\":\"Trivia\",\"sequenceOrder\":1,\"stageId\":$stage_id,\"playMode\":\"Trivia\"}")"
  http="${resp##*$'\n'}"; body="${resp%$'\n'*}"
  if [[ "$http" != "200" ]]; then
    echo "  FAILED ($http): could not add trivia substage to seeded mission" >&2; echo "$body" >&2; return 1
  fi
  substage_id="$(sed -n 's/.*"substages":\[{"id":\([0-9]*\).*/\1/p' <<<"$body")"
  if [[ -z "$substage_id" ]]; then
    echo "  FAILED: seeded mission substage returned no id" >&2; echo "$body" >&2; return 1
  fi

  # POST .../trivia-quiz-selection
  resp="$(curl -sS -w $'\n%{http_code}' -X POST \
    "$BASE_URL/api/missions/$mission_id/stages/$stage_id/substages/$substage_id/trivia-quiz-selection" \
    -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
    -d "{\"triviaQuizId\":$trivia_quiz_id}")"
  http="${resp##*$'\n'}"; body="${resp%$'\n'*}"
  if [[ "$http" != "200" ]]; then
    echo "  FAILED ($http): could not select trivia quiz for seeded mission" >&2; echo "$body" >&2; return 1
  fi

  # GET .../readiness — must be ready before activation.
  body="$(curl -sS "$BASE_URL/api/missions/$mission_id/readiness" -H "Authorization: Bearer $token")"
  if ! grep -q '"isReady":true' <<<"$body"; then
    echo "  FAILED: seeded mission is not runtime-ready" >&2; echo "$body" >&2; return 1
  fi

  # POST /api/missions/{id}/activate
  resp="$(curl -sS -w $'\n%{http_code}' -X POST "$BASE_URL/api/missions/$mission_id/activate" \
    -H "Authorization: Bearer $token" -H "Content-Type: application/json")"
  http="${resp##*$'\n'}"; body="${resp%$'\n'*}"
  if [[ "$http" != "200" ]]; then
    echo "  FAILED ($http): could not activate seeded mission" >&2; echo "$body" >&2; return 1
  fi

  echo "$mission_id"
}

app_create_session() {
  local token="$1" mission_id="$2" title="$3"
  local scheduled_at resp http body
  scheduled_at="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  resp="$(curl -sS -w $'\n%{http_code}' -X POST "$BASE_URL/api/sessions" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "{\"missionId\":$mission_id,\"title\":\"$title\",\"maximumTimeMinutes\":10,\"scheduledAt\":\"$scheduled_at\"}")"
  http="${resp##*$'\n'}"
  body="${resp%$'\n'*}"
  if [[ "$http" != "201" ]]; then
    echo "  FAILED ($http): could not create live trivia session" >&2
    echo "$body" >&2
    return 1
  fi

  sed -n 's/.*"liveSessionId":"\([^"]*\)".*/\1/p' <<<"$body"
}

app_assign_operator_to_session() {
  local token="$1" live_session_id="$2" operator_user_id="$3"
  local resp http body
  resp="$(curl -sS -w $'\n%{http_code}' -X PATCH \
    "$BASE_URL/api/sessions/$live_session_id/operator-assignment" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "{\"operatorUserId\":$operator_user_id}")"
  http="${resp##*$'\n'}"
  body="${resp%$'\n'*}"
  if [[ "$http" != "200" ]]; then
    echo "  FAILED ($http): could not assign operator to live trivia session" >&2
    echo "$body" >&2
    return 1
  fi
}

app_associate_team_to_session() {
  local token="$1" live_session_id="$2" reference_team_id="$3"
  local resp http body
  resp="$(curl -sS -w $'\n%{http_code}' -X POST \
    "$BASE_URL/api/sessions/$live_session_id/teams" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "{\"referenceTeamId\":\"$reference_team_id\"}")"
  http="${resp##*$'\n'}"
  body="${resp%$'\n'*}"
  if [[ "$http" != "200" ]]; then
    echo "  FAILED ($http): could not associate team to live trivia session" >&2
    echo "$body" >&2
    return 1
  fi
}

app_transition_session() {
  local token="$1" live_session_id="$2" target_state="$3"
  local resp http body
  resp="$(curl -sS -w $'\n%{http_code}' -X PATCH \
    "$BASE_URL/api/sessions/$live_session_id/state" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "{\"targetState\":\"$target_state\"}")"
  http="${resp##*$'\n'}"
  body="${resp%$'\n'*}"
  if [[ "$http" != "200" ]]; then
    echo "  FAILED ($http): could not transition live trivia session to $target_state" >&2
    echo "$body" >&2
    return 1
  fi
}

declare -A PARTICIPANT_IDS

ADMIN_TOKEN="$(auth_admin)"
if [[ -z "$ADMIN_TOKEN" ]]; then
  echo "  ERROR: Failed to obtain Keycloak admin token from $KEYCLOAK_URL."
  exit 1
fi

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

echo "  team memberships …"

APP_ADMIN_TOKEN="$(auth_user admin "$ADMIN_PASSWORD" || true)"
if [[ -z "$APP_ADMIN_TOKEN" ]]; then
  echo "  ERROR: could not obtain an app token for 'admin' to call the teams API."
  exit 1
fi

declare -a TEAMS=(
  "Delta|DELTA|participant01 participant02"
  "Echo|ECHO|participant03 participant04"
  "Bismarck|BISMARCK|participant05 participant06"
  "Los Panas|PANAS|participant07 participant08"
)

DELTA_TEAM_ID=""

for spec in "${TEAMS[@]}"; do
  IFS='|' read -r tdisplay tcode tmembers <<<"$spec"

  team_id="$(app_register_team "$APP_ADMIN_TOKEN" "$tdisplay" "$tcode" || true)"
  if [[ -z "$team_id" ]]; then
    echo "  FAILED: could not register or resolve team '$tcode'"
    exit 1
  fi
  echo "  team ready: $tdisplay ($tcode)"
  [[ "$tcode" == "DELTA" ]] && DELTA_TEAM_ID="$team_id"

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

echo "  live trivia session fixture …"

SOURCE_TRIVIA_QUIZ_ID="$(psql -At -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d mission_design -c "
  SELECT \"Id\"
  FROM \"TriviaQuizzes\"
  WHERE \"Title\" = '$SEEDED_LIVE_TRIVIA_SOURCE_TITLE'
    AND \"Status\" = 'Published'
  ORDER BY \"Id\" DESC
  LIMIT 1;
" | tr -d '[:space:]')"

if [[ -z "$SOURCE_TRIVIA_QUIZ_ID" ]]; then
  echo "  FAILED: could not resolve published trivia quiz '$SEEDED_LIVE_TRIVIA_SOURCE_TITLE'"
  exit 1
fi

READY_MISSION_ID="$(seed_ready_mission "$APP_ADMIN_TOKEN" "$SOURCE_TRIVIA_QUIZ_ID")"
if [[ -z "$READY_MISSION_ID" ]]; then
  echo "  FAILED: could not author a runtime-ready mission for the live session fixture"
  exit 1
fi

if [[ -z "$DELTA_TEAM_ID" ]]; then
  echo "  FAILED: could not resolve seeded Delta team id"
  exit 1
fi

OPERATOR_TOKEN="$(auth_user operator "$OPERATOR_PASSWORD" || true)"
if [[ -z "$OPERATOR_TOKEN" ]]; then
  echo "  ERROR: could not obtain an app token for 'operator' to start the live trivia fixture."
  exit 1
fi

OPERATOR_USER_ID="$(app_user_id "$OPERATOR_TOKEN" || true)"
if [[ -z "$OPERATOR_USER_ID" ]]; then
  echo "  ERROR: could not resolve app user id for seeded operator."
  exit 1
fi

# Assign the seeded operator to the psql-seeded SMOKE sessions. The transition path resolves
# operator ownership by calling identity-access GET /api/users/me (keyed by the operator's
# current Keycloak sub, which the USERS loop above bootstrapped) and comparing the returned
# app user id against live_sessions.assigned_operator_user_id. Without this assignment the
# OperatorAssignmentGate rejects Scheduled→Preparing (operator required) and the resolver's
# ownership check fails. The app user id is minted at bootstrap time, so it must be stamped
# here (Part 3) rather than in the deterministic Part 1 psql insert.
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
  UPDATE live_sessions
  SET assigned_operator_user_id = $OPERATOR_USER_ID, updated_at = now()
  WHERE session_code LIKE 'SMOKE%';
"
echo "  assigned operator (app user $OPERATOR_USER_ID) to SMOKE sessions"

SEEDED_LIVE_TRIVIA_ID="$(app_create_session "$APP_ADMIN_TOKEN" "$READY_MISSION_ID" "$SEEDED_LIVE_TRIVIA_TITLE")"
if [[ -z "$SEEDED_LIVE_TRIVIA_ID" ]]; then
  echo "  FAILED: live trivia session API returned no liveSessionId"
  exit 1
fi

app_assign_operator_to_session "$APP_ADMIN_TOKEN" "$SEEDED_LIVE_TRIVIA_ID" "$OPERATOR_USER_ID"
app_associate_team_to_session "$OPERATOR_TOKEN" "$SEEDED_LIVE_TRIVIA_ID" "$DELTA_TEAM_ID"
app_transition_session "$OPERATOR_TOKEN" "$SEEDED_LIVE_TRIVIA_ID" "Preparing"
app_transition_session "$OPERATOR_TOKEN" "$SEEDED_LIVE_TRIVIA_ID" "Active"

SEEDED_LIVE_TRIVIA_CODE="$(psql -At -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
  SELECT session_code FROM live_sessions WHERE id = '$SEEDED_LIVE_TRIVIA_ID';
" | tr -d '[:space:]')"
echo "  live trivia ready: ${SEEDED_LIVE_TRIVIA_CODE:-$SEEDED_LIVE_TRIVIA_ID} → Active"

echo ""
echo "Done.  Sessions seeded:"
for CODE in "${!SESSIONS[@]}"; do
  IFS=: read -r SID STATE TID TCODE TDISPLAY <<< "${SESSIONS[$CODE]}"
  echo "  $CODE  → $STATE"
done
echo "  ${SEEDED_LIVE_TRIVIA_CODE:-$SEEDED_LIVE_TRIVIA_ID}  → Active (Mission)"
