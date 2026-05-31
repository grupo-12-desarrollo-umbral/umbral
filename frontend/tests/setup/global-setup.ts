import { execSync } from 'child_process'
import { writeFileSync, unlinkSync, mkdtempSync } from 'fs'
import { join } from 'path'
import { tmpdir } from 'os'

const DB_CONTAINER = 'backend-postgres-1'
const DB_NAME = 'identity_access'

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
