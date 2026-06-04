#!/usr/bin/env bash
set -euo pipefail

# seed-dev-data.sh
#
# Seeds dev data across three databases:
#   - mission_design:  trivia quizzes in various lifecycle states
#   - identity_access: teams, sessions, and team memberships
#   - session_operations: live sessions in every lifecycle state
#
# Sessions in each lifecycle state so you can validate the mobile UI:
#
#   SMOKE1  Scheduled  → first join succeeds (scheduled 3 min after seed)
#   SMOKE2  Active     → "The session has moved on — late join isn't allowed."
#   SMOKE3  Scheduled  → first join succeeds
#   SMOKE4  Preparing  → first join succeeds
#   SMOKE5  Paused     → "The session has moved on — late join isn't allowed."
#   SMOKE6  Finished   → "The session has moved on — late join isn't allowed."
#   SMOKE7  Cancelled  → "The session has moved on — late join isn't allowed."
#
# Trivia quizzes seeded:
#   - Filosofos de Atenas         (Published, 3 questions)
#   - Mejores guitarristas...     (Published, 3 questions)
#   - Peliculas mas vistas...     (Published, 3 questions)
#   - Musica y su historia        (Draft, 3 questions)
#   - Capitales del mundo         (Archived, 3 questions)
#
#
# Usage:
#   ./scripts/seed-dev-data.sh
#   PGHOST=192.168.1.50 ./scripts/seed-dev-data.sh
#
# Idempotent: safe to run multiple times.

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-postgres}"
PGPASSWORD="${PGPASSWORD:-postgres}"
export PGPASSWORD

# Wait for EF Core migrations to create the tables in each database
echo "Waiting for session_operations.live_sessions table …"
for i in $(seq 1 20); do
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c '
    SELECT 1 FROM live_sessions LIMIT 1;
  ' &>/dev/null && break
  echo "  attempt $i/20 — not ready yet, waiting 2s …"
  sleep 2
done

echo "Cleaning dirty dev data …"
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d identity_access -c "
  DELETE FROM live_sessions WHERE session_code = 'RSF231';
"
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
  DELETE FROM live_sessions WHERE session_code = 'RSF231';
" 2>/dev/null || true

# ---------------------------------------------------------------------------
# Each session gets a deterministic base ID and team ID.
# Teams share the same GUID in both databases.
# ---------------------------------------------------------------------------
declare -A SESSIONS=(
  [SMOKE1]=b1000000-0000-0000-0000-000000000000:Scheduled:b1000000-0000-0000-0000-000000000016:DV-SOON:Soon
  [SMOKE2]=b1000000-0000-0000-0000-000000000001:Active:b1000000-0000-0000-0000-000000000010:DV-SMK:Smoke
  [SMOKE3]=b1000000-0000-0000-0000-000000000002:Scheduled:b1000000-0000-0000-0000-000000000011:DV-ECH:Echo
  [SMOKE4]=b1000000-0000-0000-0000-000000000003:Preparing:b1000000-0000-0000-0000-000000000012:DV-FOX:Foxtrot
  [SMOKE5]=b1000000-0000-0000-0000-000000000004:Paused:b1000000-0000-0000-0000-000000000013:DV-GLF:Golf
  [SMOKE6]=b1000000-0000-0000-0000-000000000005:Finished:b1000000-0000-0000-0000-000000000014:DV-HTL:Hotel
  [SMOKE7]=b1000000-0000-0000-0000-000000000006:Cancelled:b1000000-0000-0000-0000-000000000015:DV-IND:India
)

# ---------------------------------------------------------------------------
echo "Seeding mission_design (trivia quizzes) …"
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d mission_design -c "
DELETE FROM \"TriviaOptions\";
DELETE FROM \"TriviaQuestions\";
DELETE FROM \"TriviaQuizzes\";
"
# Filosofos de Atenas — Published (3 questions)
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
# Mejores guitarristas de la historia — Published (3 questions)
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
# Peliculas mas vistas en los ultimos 5 anos — Published (3 questions)
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
# Musica y su historia — Draft (3 questions)
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
# Capitales del mundo — Archived (3 questions)
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

# ---------------------------------------------------------------------------
echo "Seeding identity_access …"
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

# ---------------------------------------------------------------------------
echo "Seeding session_operations …"
for CODE in "${!SESSIONS[@]}"; do
  IFS=: read -r SID STATE TID TCODE TDISPLAY <<< "${SESSIONS[$CODE]}"
  SCHEDULED_AT="now()"
  [[ "$CODE" == "SMOKE1" ]] && SCHEDULED_AT="now() + interval '3 minutes'"

  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
    INSERT INTO live_sessions (
      id, session_mode, source_entity_type, source_entity_id,
      session_code, title_snapshot, state,
      scheduled_at, last_state_changed_at, maximum_time_minutes,
      created_at, updated_at
    ) VALUES (
      '$SID', 'TreasureHunt', 'Mission', gen_random_uuid(),
      '$CODE', '$TDISPLAY Team', '$STATE',
      $SCHEDULED_AT, now(), 60,
      now(), now()
    )
    ON CONFLICT (id) DO NOTHING;
  "
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
    INSERT INTO live_session_teams (
      id, live_session_id, team_code, display_name,
      team_capacity, current_score, released_clue_count, join_status
    ) VALUES (
      '$TID', '$SID', '$TCODE', '$TDISPLAY Team',
      10, null, 0, 'Open'
    )
    ON CONFLICT (id) DO NOTHING;
  "
done

# ---------------------------------------------------------------------------
# Second team per session
# ---------------------------------------------------------------------------
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
      team_capacity, current_score, released_clue_count, join_status
    ) VALUES (
      '$TID', '$SID', '$TCODE', '$TDISPLAY Team',
      10, null, 0, 'Open'
    )
    ON CONFLICT (id) DO NOTHING;
  "
done

# ---------------------------------------------------------------------------
echo "Done."
echo ""
for CODE in "${!SESSIONS[@]}"; do
  IFS=: read -r SID STATE TID TCODE TDISPLAY <<< "${SESSIONS[$CODE]}"
  echo "  $CODE  → $STATE"
done
