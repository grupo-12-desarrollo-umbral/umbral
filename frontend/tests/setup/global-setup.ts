import { execSync } from 'child_process'
import { writeFileSync, unlinkSync, mkdtempSync } from 'fs'
import { join } from 'path'
import { tmpdir } from 'os'

const DB_CONTAINER = 'backend-postgres-1'

function runSql(db: string, sql: string, label: string): void {
  const tmpDir = mkdtempSync(join(tmpdir(), 'umbral-e2e-seed-'))
  const tmpFile = join(tmpDir, 'seed.sql')
  writeFileSync(tmpFile, sql, 'utf-8')
  try {
    execSync(`docker cp "${tmpFile}" ${DB_CONTAINER}:/tmp/seed.sql`, { stdio: 'pipe', timeout: 10000 })
    execSync(`docker exec ${DB_CONTAINER} psql -U postgres -d ${db} -f /tmp/seed.sql`, { stdio: 'pipe', timeout: 15000 })
    execSync(`docker exec ${DB_CONTAINER} rm /tmp/seed.sql`, { stdio: 'pipe', timeout: 5000 })
    console.log(`[global-setup] ${label}`)
  } finally {
    try { unlinkSync(tmpFile) } catch { /* ignore */ }
    try { unlinkSync(tmpDir) } catch { /* ignore */ }
  }
}

function seedViaDocker(): void {
  runSql('identity_access', `
DELETE FROM team_memberships;
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
  updated_at = NOW();
`, 'Test users and teams seeded.')

  // Seed one active, runtime-ready trivia mission so the session-creation form has
  // a selectable mission in e2e tests. Uses quiz id=6 (Filosofos de Atenas — Published).
  runSql('mission_design', `
DO $$
DECLARE
  v_mission_id INT;
  v_stage_id   INT;
BEGIN
  SELECT "Id" INTO v_mission_id FROM "Missions" WHERE "Name" = 'E2E Seed Mission' LIMIT 1;

  IF v_mission_id IS NULL THEN
    INSERT INTO "Missions" ("Name", "Description", "Difficulty", "MaximumTimeMinutes", "IsActive", "ActivationState", "Created", "LastModified")
    VALUES ('E2E Seed Mission', 'Seeded for e2e tests — do not delete', 'Easy', 60, true, 'Ready', NOW(), NOW())
    RETURNING "Id" INTO v_mission_id;

    INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
    VALUES (v_mission_id, 'Stage 1', 1)
    RETURNING "Id" INTO v_stage_id;

    INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode", "TriviaQuizId")
    VALUES (v_stage_id, 'Trivia Substage', 1, 'Trivia', 6);
  ELSE
    UPDATE "Missions"
    SET "IsActive" = true, "ActivationState" = 'Ready', "LastModified" = NOW()
    WHERE "Id" = v_mission_id;
  END IF;
END $$;
`, 'E2E seed mission ensured.')

  // Seed a mission that is Draft but has a complete runtime plan (one stage with a
  // trivia substage selecting published quiz id=6), so the activate-flow e2e test can
  // drive Draft -> Ready. There is no hierarchy-authoring UI yet (deferred to DES-15),
  // so the runtime plan has to be seeded directly. Reset to Draft on every run so the
  // test is repeatable after a prior run flipped it to Ready.
  runSql('mission_design', `
DO $$
DECLARE
  v_mission_id INT;
  v_stage_id   INT;
BEGIN
  SELECT "Id" INTO v_mission_id FROM "Missions" WHERE "Name" = 'E2E Activatable Mission' LIMIT 1;

  IF v_mission_id IS NULL THEN
    INSERT INTO "Missions" ("Name", "Description", "Difficulty", "MaximumTimeMinutes", "IsActive", "ActivationState", "Created", "LastModified")
    VALUES ('E2E Activatable Mission', 'Seeded Draft mission with a complete runtime plan — do not delete', 'Easy', 45, true, 'Draft', NOW(), NOW())
    RETURNING "Id" INTO v_mission_id;

    INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
    VALUES (v_mission_id, 'Stage 1', 1)
    RETURNING "Id" INTO v_stage_id;

    INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode", "TriviaQuizId")
    VALUES (v_stage_id, 'Trivia Substage', 1, 'Trivia', 6);
  ELSE
    UPDATE "Missions"
    SET "IsActive" = true, "ActivationState" = 'Draft', "LastModified" = NOW()
    WHERE "Id" = v_mission_id;
  END IF;
END $$;
`, 'E2E activatable mission ensured.')
}

async function main() {
  try {
    seedViaDocker()
  } catch (err) {
    console.warn('[global-setup] Could not seed test users:', (err as Error).message?.slice(0, 200))
  }
}

export default main
