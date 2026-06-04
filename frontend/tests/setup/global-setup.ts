import { execSync } from 'child_process'
import { writeFileSync, unlinkSync, mkdtempSync } from 'fs'
import { join } from 'path'
import { tmpdir } from 'os'

const DB_CONTAINER = 'backend-postgres-1'
const DB_NAME = 'identity_access'
const MISSION_DB_NAME = 'mission_design'

function seedViaDocker(): void {
  const sql = `DELETE FROM team_memberships;
DELETE FROM teams;
INSERT INTO users ("ExternalIdentityId", "DisplayName", "Email", "Role", "IsActive", "Created", "LastModified")
VALUES
  ('admin-1',        'Administrator One', 'admin@umbral.local',        'Administrator', true,  NOW(), NOW()),
  ('op-1',           'Operator One',      'op@umbral.local',           'Operator',      true,  NOW(), NOW()),
  ('participant-1',  'Participant One',   'participant@umbral.local',  'Participant',   true,  NOW(), NOW()),
  ('deactivated-1',  'Deactivated User',  'deactivated@umbral.local',  'Operator',      false, NOW(), NOW())
ON CONFLICT ("ExternalIdentityId") DO UPDATE SET
  "DisplayName" = EXCLUDED."DisplayName",
  "Email" = EXCLUDED."Email",
  "Role" = EXCLUDED."Role",
  "IsActive" = EXCLUDED."IsActive",
  "LastModified" = NOW();

INSERT INTO teams (id, display_name, team_code, is_active, created_at, updated_at)
VALUES
  ('a0000000-0000-0000-0000-000000000001', 'Gilded Owls',    'OWLS',  true,  NOW(), NOW()),
  ('a0000000-0000-0000-0000-000000000002', 'Maple Runners',  'MAPLE', true,  NOW(), NOW()),
  ('a0000000-0000-0000-0000-000000000003', 'Brass Lanterns', 'BRASS', true,  NOW(), NOW()),
  ('a0000000-0000-0000-0000-000000000004', 'Iron Magnolias', 'IRON',  true,  NOW(), NOW())
ON CONFLICT (id) DO UPDATE SET
  is_active = EXCLUDED.is_active,
  updated_at = NOW();`

  const tmpDir = mkdtempSync(join(tmpdir(), 'umbral-e2e-seed-'))
  const tmpFile = join(tmpDir, 'seed.sql')
  writeFileSync(tmpFile, sql, 'utf-8')

  try {
    execSync(`docker cp "${tmpFile}" ${DB_CONTAINER}:/tmp/seed.sql`, { stdio: 'pipe', timeout: 10000 })
    execSync(`docker exec ${DB_CONTAINER} psql -U postgres -d ${DB_NAME} -f /tmp/seed.sql`, { stdio: 'pipe', timeout: 15000 })
    execSync(`docker exec ${DB_CONTAINER} rm /tmp/seed.sql`, { stdio: 'pipe', timeout: 5000 })
    console.log('[global-setup] Test users seeded.')

    // Seed trivia quizzes in mission_design (all three statuses for edit-gate + manual testing).
    // Filosofos de Atenas is Published and includes questions so session creation works.
    const mSql = `DELETE FROM "TriviaOptions";
DELETE FROM "TriviaQuestions";
DELETE FROM "TriviaQuizzes";
WITH quiz AS (
  INSERT INTO "TriviaQuizzes" ("Title", "Description", "Status", "Created", "LastModified")
  VALUES ('Filosofos de Atenas', 'Los pensadores que marcaron la antiguedad.', 'Published', NOW(), NOW())
  RETURNING "Id"
),
q1 AS (
  INSERT INTO "TriviaQuestions" ("TriviaQuizId", "Prompt", "SequenceOrder", "ScoreValue", "TimeLimitSeconds", "Explanation", "IsActive")
  SELECT "Id", '¿Quién fue el maestro de Platón?', 1, 100, 30, 'Sócrates fue el maestro de Platón.', true FROM quiz
  RETURNING "Id"
),
q2 AS (
  INSERT INTO "TriviaQuestions" ("TriviaQuizId", "Prompt", "SequenceOrder", "ScoreValue", "TimeLimitSeconds", "Explanation", "IsActive")
  SELECT "Id", '¿Qué filósofo fundó la Academia de Atenas?', 2, 100, 30, 'Platón fundó la Academia de Atenas.', true FROM quiz
  RETURNING "Id"
),
q3 AS (
  INSERT INTO "TriviaQuestions" ("TriviaQuizId", "Prompt", "SequenceOrder", "ScoreValue", "TimeLimitSeconds", "Explanation", "IsActive")
  SELECT "Id", '¿Cuál de estos filósofos fue discípulo de Platón?', 3, 100, 30, 'Aristóteles fue discípulo de Platón.', true FROM quiz
  RETURNING "Id"
)
INSERT INTO "TriviaOptions" ("TriviaQuestionId", "OptionText", "SequenceOrder", "IsCorrect")
SELECT "Id", 'Sócrates', 1, true FROM q1
UNION ALL SELECT "Id", 'Aristóteles', 2, false FROM q1
UNION ALL SELECT "Id", 'Pitágoras', 3, false FROM q1
UNION ALL SELECT "Id", 'Demócrito', 4, false FROM q1
UNION ALL SELECT "Id", 'Platón', 1, true FROM q2
UNION ALL SELECT "Id", 'Sócrates', 2, false FROM q2
UNION ALL SELECT "Id", 'Aristóteles', 3, false FROM q2
UNION ALL SELECT "Id", 'Epicuro', 4, false FROM q2
UNION ALL SELECT "Id", 'Aristóteles', 1, true FROM q3
UNION ALL SELECT "Id", 'Sócrates', 2, false FROM q3
UNION ALL SELECT "Id", 'Heráclito', 3, false FROM q3
UNION ALL SELECT "Id", 'Tales', 4, false FROM q3;

INSERT INTO "TriviaQuizzes" ("Title", "Description", "Status", "Created", "LastModified")
VALUES
  ('Musica y su historia',   'Un recorrido por los generos musicales.',   'Draft',     NOW(), NOW()),
  ('Guitarristas mas queridos', 'Los maestros de la guitarra.',           'Archived',  NOW(), NOW());`
    const mFile = join(tmpDir, 'seed-mission.sql')
    writeFileSync(mFile, mSql, 'utf-8')
    execSync(`docker cp "${mFile}" ${DB_CONTAINER}:/tmp/seed-mission.sql`, { stdio: 'pipe', timeout: 10000 })
    execSync(`docker exec ${DB_CONTAINER} psql -U postgres -d ${MISSION_DB_NAME} -f /tmp/seed-mission.sql`, { stdio: 'pipe', timeout: 15000 })
    execSync(`docker exec ${DB_CONTAINER} rm /tmp/seed-mission.sql`, { stdio: 'pipe', timeout: 5000 })
    console.log('[global-setup] Trivia quizzes seeded.')
  } finally {
    try { unlinkSync(tmpFile) } catch { /* ignore */ }
    try { unlinkSync(tmpDir) } catch { /* ignore */ }
  }
}

async function main() {
  try {
    seedViaDocker()
  } catch (err) {
    console.warn('[global-setup] Could not seed test users:', (err as Error).message?.slice(0, 200))
  }
}

export default main
