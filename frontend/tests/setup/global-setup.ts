import { execSync } from 'child_process'
import { writeFileSync, unlinkSync, mkdtempSync } from 'fs'
import { join } from 'path'
import { tmpdir } from 'os'

const DB_CONTAINER = 'backend-postgres-1'
const KEYCLOAK_URL = process.env.KEYCLOAK_URL ?? 'http://localhost:8080'
const KEYCLOAK_REALM = process.env.KEYCLOAK_REALM ?? 'umbral'
const KEYCLOAK_CLIENT_ID = process.env.KEYCLOAK_CLIENT_ID ?? 'umbral-web'
const KEYCLOAK_ADMIN_USERNAME = process.env.KEYCLOAK_ADMIN_USERNAME ?? 'admin'
const KEYCLOAK_ADMIN_PASSWORD = process.env.KEYCLOAK_ADMIN_PASSWORD ?? 'admin'

type E2EKeycloakUser = {
  id: string
  username: string
  password: string
  email: string
  displayName: string
  role: 'Administrator' | 'Operator' | 'Participant'
  enabled: boolean
}

const keycloakUsers: E2EKeycloakUser[] = [
  {
    id: 'admin-1',
    username: 'admin-1',
    password: 'admin123',
    email: 'admin-1@umbral.local',
    displayName: 'Administrator One',
    role: 'Administrator',
    enabled: true,
  },
  {
    id: 'op-1',
    username: 'op-1',
    password: 'operator123',
    email: 'op-1@umbral.local',
    displayName: 'Operator One',
    role: 'Operator',
    enabled: true,
  },
  {
    id: 'participant-1',
    username: 'participant-1',
    password: 'participant123',
    email: 'participant-1@umbral.local',
    displayName: 'Participant One',
    role: 'Participant',
    enabled: true,
  },
  {
    id: 'deactivated-1',
    username: 'deactivated-1',
    password: 'operator123',
    email: 'deactivated-1@umbral.local',
    displayName: 'Deactivated User',
    role: 'Operator',
    enabled: false,
  },
]

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
  ('admin-1',        'Administrator One', 'admin-1@umbral.local',      'Administrator', true,  NOW(), NOW()),
  ('op-1',           'Operator One',      'op-1@umbral.local',         'Operator',      true,  NOW(), NOW()),
  ('participant-1',  'Participant One',   'participant-1@umbral.local','Participant',   true,  NOW(), NOW()),
  ('deactivated-1',  'Deactivated User',  'deactivated-1@umbral.local','Operator',      false, NOW(), NOW())
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

async function requestJson<T>(
  url: string,
  init: RequestInit,
  expectedStatuses: number[],
): Promise<T | null> {
  const response = await fetch(url, init)
  if (!expectedStatuses.includes(response.status)) {
    const body = await response.text().catch(() => '')
    throw new Error(`${init.method ?? 'GET'} ${url} failed: ${response.status} ${body.slice(0, 200)}`)
  }
  if (response.status === 204 || response.headers.get('Content-Length') === '0') {
    return null
  }
  return (await response.json().catch(() => null)) as T | null
}

async function getAdminToken(): Promise<string> {
  const body = new URLSearchParams()
  body.set('client_id', 'admin-cli')
  body.set('grant_type', 'password')
  body.set('username', KEYCLOAK_ADMIN_USERNAME)
  body.set('password', KEYCLOAK_ADMIN_PASSWORD)

  const response = await requestJson<{ access_token?: string }>(
    `${KEYCLOAK_URL}/realms/master/protocol/openid-connect/token`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: body.toString(),
    },
    [200],
  )

  if (!response?.access_token) {
    throw new Error('Keycloak admin token response did not include access_token')
  }

  return response.access_token
}

function keycloakHeaders(adminToken: string, contentType = 'application/json'): HeadersInit {
  return {
    Authorization: `Bearer ${adminToken}`,
    'Content-Type': contentType,
  }
}

function toUserPayload(user: E2EKeycloakUser): Record<string, unknown> {
  const [firstName, ...lastNameParts] = user.displayName.split(' ')
  return {
    id: user.id,
    username: user.username,
    enabled: user.enabled,
    emailVerified: true,
    email: user.email,
    firstName,
    lastName: lastNameParts.join(' ') || 'Umbral',
  }
}

async function createUser(adminToken: string, user: E2EKeycloakUser): Promise<void> {
  await requestJson<null>(
    `${KEYCLOAK_URL}/admin/realms/${KEYCLOAK_REALM}/users`,
    {
      method: 'POST',
      headers: keycloakHeaders(adminToken),
      body: JSON.stringify(toUserPayload(user)),
    },
    [201, 204],
  )
}

async function getUserId(adminToken: string, user: E2EKeycloakUser): Promise<string | null> {
  const byId = await fetch(`${KEYCLOAK_URL}/admin/realms/${KEYCLOAK_REALM}/users/${user.id}`, {
    headers: keycloakHeaders(adminToken),
  })
  if (byId.ok) return user.id
  if (byId.status !== 404) {
    throw new Error(`GET Keycloak user ${user.id} failed: ${byId.status}`)
  }

  const matches = await requestJson<Array<{ id?: string }>>(
    `${KEYCLOAK_URL}/admin/realms/${KEYCLOAK_REALM}/users?username=${encodeURIComponent(user.username)}&exact=true`,
    { headers: keycloakHeaders(adminToken) },
    [200],
  )

  return matches?.[0]?.id ?? null
}

async function ensureUser(adminToken: string, user: E2EKeycloakUser): Promise<string> {
  const existingUserId = await getUserId(adminToken, user)
  if (existingUserId === user.id) {
    await requestJson<null>(
      `${KEYCLOAK_URL}/admin/realms/${KEYCLOAK_REALM}/users/${existingUserId}`,
      {
        method: 'PUT',
        headers: keycloakHeaders(adminToken),
        body: JSON.stringify(toUserPayload(user)),
      },
      [204],
    )
  } else if (existingUserId) {
    await requestJson<null>(
      `${KEYCLOAK_URL}/admin/realms/${KEYCLOAK_REALM}/users/${existingUserId}`,
      {
        method: 'DELETE',
        headers: keycloakHeaders(adminToken),
      },
      [204],
    )
    await createUser(adminToken, user)
  } else {
    await createUser(adminToken, user)
  }

  const resolvedUserId = await getUserId(adminToken, user)
  if (!resolvedUserId) {
    throw new Error(`Could not resolve Keycloak user id for ${user.username}`)
  }

  await requestJson<null>(
    `${KEYCLOAK_URL}/admin/realms/${KEYCLOAK_REALM}/users/${resolvedUserId}/reset-password`,
    {
      method: 'PUT',
      headers: keycloakHeaders(adminToken),
      body: JSON.stringify({ type: 'password', value: user.password, temporary: false }),
    },
    [204],
  )

  return resolvedUserId
}

async function getRealmRole(adminToken: string, role: E2EKeycloakUser['role']): Promise<unknown> {
  const roleJson = await requestJson<unknown>(
    `${KEYCLOAK_URL}/admin/realms/${KEYCLOAK_REALM}/roles/${role}`,
    { headers: keycloakHeaders(adminToken) },
    [200],
  )
  if (!roleJson) throw new Error(`Could not load Keycloak role ${role}`)
  return roleJson
}

async function syncRole(
  adminToken: string,
  userId: string,
  role: E2EKeycloakUser['role'],
): Promise<void> {
  const targetRole = await getRealmRole(adminToken, role)
  const currentRoles = await requestJson<Array<{ name?: string }>>(
    `${KEYCLOAK_URL}/admin/realms/${KEYCLOAK_REALM}/users/${userId}/role-mappings/realm`,
    { headers: keycloakHeaders(adminToken) },
    [200],
  )
  const managedRoles = (currentRoles ?? []).filter((currentRole) =>
    currentRole.name === 'Administrator' ||
    currentRole.name === 'Operator' ||
    currentRole.name === 'Participant',
  )

  if (managedRoles.length > 0) {
    await requestJson<null>(
      `${KEYCLOAK_URL}/admin/realms/${KEYCLOAK_REALM}/users/${userId}/role-mappings/realm`,
      {
        method: 'DELETE',
        headers: keycloakHeaders(adminToken),
        body: JSON.stringify(managedRoles),
      },
      [204],
    )
  }

  await requestJson<null>(
    `${KEYCLOAK_URL}/admin/realms/${KEYCLOAK_REALM}/users/${userId}/role-mappings/realm`,
    {
      method: 'POST',
      headers: keycloakHeaders(adminToken),
      body: JSON.stringify([targetRole]),
    },
    [204],
  )
}

async function assertUserCanAuthenticate(user: E2EKeycloakUser): Promise<void> {
  if (!user.enabled) return

  const body = new URLSearchParams()
  body.set('grant_type', 'password')
  body.set('client_id', KEYCLOAK_CLIENT_ID)
  body.set('username', user.username)
  body.set('password', user.password)

  await requestJson<unknown>(
    `${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/token`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: body.toString(),
    },
    [200],
  )
}

async function seedKeycloak(): Promise<void> {
  const adminToken = await getAdminToken()

  for (const user of keycloakUsers) {
    const userId = await ensureUser(adminToken, user)
    await syncRole(adminToken, userId, user.role)
    await assertUserCanAuthenticate(user)
  }

  console.log('[global-setup] E2E Keycloak users ensured.')
}

async function main() {
  try {
    seedViaDocker()
  } catch (err) {
    console.warn('[global-setup] Could not seed test users:', (err as Error).message?.slice(0, 200))
  }

  try {
    await seedKeycloak()
  } catch (err) {
    console.warn('[global-setup] Could not seed Keycloak users:', (err as Error).message?.slice(0, 200))
  }
}

export default main
