import { execSync } from 'child_process'
import { writeFileSync, unlinkSync, mkdtempSync } from 'fs'
import { join } from 'path'
import { tmpdir } from 'os'

const DB_CONTAINER = 'backend-postgres-1'
const DB_NAME = 'identity_access'

function seedViaDocker(): void {
  const sql = `INSERT INTO users ("ExternalIdentityId", "DisplayName", "Email", "Role", "IsActive", "Created", "LastModified")
VALUES
  ('op-1',           'Operator One',     'op@umbral.local',           'Operator',      true,  NOW(), NOW()),
  ('participant-1',  'Participant One',  'participant@umbral.local',  'Participant',   true,  NOW(), NOW()),
  ('deactivated-1',  'Deactivated User', 'deactivated@umbral.local',  'Operator',      false, NOW(), NOW())
ON CONFLICT ("ExternalIdentityId") DO UPDATE SET
  "IsActive" = EXCLUDED."IsActive",
  "LastModified" = NOW();`

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
