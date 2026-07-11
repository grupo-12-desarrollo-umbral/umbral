import { execSync } from 'child_process'

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

export function runSql(db: string, sql: string, label: string): void {
  // Pipe SQL on stdin rather than `docker cp` to a temp file: docker cp fails with
  // "file exists" when the container has a single-file bind-mount (init-dbs.sql), and
  // stdin needs no temp file or cleanup.
  execSync(`docker exec -i ${DB_CONTAINER} psql -v ON_ERROR_STOP=1 -U postgres -d ${db}`, {
    input: sql,
    stdio: ['pipe', 'pipe', 'pipe'],
    timeout: 20000,
  })
  console.log(`[global-setup] ${label}`)
}

function seedViaDocker(): void {
  runSql('identity_access', `
DELETE FROM registered_team_memberships;
DELETE FROM registered_teams;
-- op-1 is seeded later (seedOperatorIdentity) with its resolved Keycloak sub, not the literal
-- username: only operator session-listing goes gateway→JWT, which keys the actor by sub.
INSERT INTO users ("ExternalIdentityId", "DisplayName", "Email", "Role", "IsActive", "Created", "LastModified")
VALUES
  ('admin-1',        'Administrator One', 'admin-1@umbral.local',      'Administrator', true,  NOW(), NOW()),
  ('participant-1',  'Participant One',   'participant-1@umbral.local','Participant',   true,  NOW(), NOW()),
  ('deactivated-1',  'Deactivated User',  'deactivated-1@umbral.local','Operator',      false, NOW(), NOW())
ON CONFLICT ("ExternalIdentityId") DO UPDATE SET
  "DisplayName" = EXCLUDED."DisplayName",
  "Email" = EXCLUDED."Email",
  "Role" = EXCLUDED."Role",
  "IsActive" = EXCLUDED."IsActive",
  "LastModified" = NOW();

INSERT INTO registered_teams (id, display_name, team_code, is_active, created_at, updated_at)
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
  // a selectable mission in e2e tests. Resolves the quiz by title+status rather than a
  // hardcoded id: seed-dev-data.sh DELETEs and re-inserts quizzes without resetting the
  // identity sequence, so 'Filosofos de Atenas' lands at a different id on every run —
  // pinning a literal id silently points the substage at a Draft/Archived/missing quiz,
  // which passes the catalog's persisted-'Ready' check but fails live create eligibility.
  runSql('mission_design', `
DO $$
DECLARE
  v_mission_id INT;
  v_stage_id   INT;
  v_quiz_id    INT;
BEGIN
  SELECT "Id" INTO v_quiz_id
  FROM "TriviaQuizzes"
  WHERE "Title" = 'Filosofos de Atenas' AND "Status" = 'Published'
  ORDER BY "Id" DESC LIMIT 1;

  IF v_quiz_id IS NULL THEN
    RAISE EXCEPTION 'No Published "Filosofos de Atenas" quiz found — run seed-dev-data.sh first.';
  END IF;

  SELECT "Id" INTO v_mission_id FROM "Missions" WHERE "Name" = 'E2E Seed Mission' LIMIT 1;

  IF v_mission_id IS NULL THEN
    INSERT INTO "Missions" ("Name", "Description", "Difficulty", "MaximumTimeMinutes", "IsActive", "ActivationState", "Created", "LastModified")
    VALUES ('E2E Seed Mission', 'Seeded for e2e tests — do not delete', 'Easy', 60, true, 'Ready', NOW(), NOW())
    RETURNING "Id" INTO v_mission_id;

    INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
    VALUES (v_mission_id, 'Stage 1', 1)
    RETURNING "Id" INTO v_stage_id;

    INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode", "TriviaQuizId")
    VALUES (v_stage_id, 'Trivia Substage', 1, 'Trivia', v_quiz_id);
  ELSE
    UPDATE "Missions"
    SET "IsActive" = true, "ActivationState" = 'Ready', "LastModified" = NOW()
    WHERE "Id" = v_mission_id;

    -- Re-point the existing substage: the quiz id drifts across reseeds (see above).
    UPDATE "MissionSubstages" ms
    SET "TriviaQuizId" = v_quiz_id
    FROM "MissionStages" st
    WHERE ms."StageId" = st."Id" AND st."MissionId" = v_mission_id
      AND ms."PlayMode" = 'Trivia';
  END IF;
END $$;
`, 'E2E seed mission ensured.')

  // Seed a mission that is Draft but has a complete runtime plan (one stage with a
  // trivia substage selecting the published 'Filosofos de Atenas' quiz), so the
  // activate-flow e2e test can drive Draft -> Ready. There is no hierarchy-authoring UI
  // yet (deferred to DES-15), so the runtime plan has to be seeded directly. Reset to
  // Draft on every run so the test is repeatable after a prior run flipped it to Ready.
  // Quiz resolved by title+status, not a literal id (see the note on the seed mission above).
  runSql('mission_design', `
DO $$
DECLARE
  v_mission_id INT;
  v_stage_id   INT;
  v_quiz_id    INT;
BEGIN
  SELECT "Id" INTO v_quiz_id
  FROM "TriviaQuizzes"
  WHERE "Title" = 'Filosofos de Atenas' AND "Status" = 'Published'
  ORDER BY "Id" DESC LIMIT 1;

  IF v_quiz_id IS NULL THEN
    RAISE EXCEPTION 'No Published "Filosofos de Atenas" quiz found — run seed-dev-data.sh first.';
  END IF;

  SELECT "Id" INTO v_mission_id FROM "Missions" WHERE "Name" = 'E2E Activatable Mission' LIMIT 1;

  IF v_mission_id IS NULL THEN
    INSERT INTO "Missions" ("Name", "Description", "Difficulty", "MaximumTimeMinutes", "IsActive", "ActivationState", "Created", "LastModified")
    VALUES ('E2E Activatable Mission', 'Seeded Draft mission with a complete runtime plan — do not delete', 'Easy', 45, true, 'Draft', NOW(), NOW())
    RETURNING "Id" INTO v_mission_id;

    INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
    VALUES (v_mission_id, 'Stage 1', 1)
    RETURNING "Id" INTO v_stage_id;

    INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode", "TriviaQuizId")
    VALUES (v_stage_id, 'Trivia Substage', 1, 'Trivia', v_quiz_id);
  ELSE
    UPDATE "Missions"
    SET "IsActive" = true, "ActivationState" = 'Draft', "LastModified" = NOW()
    WHERE "Id" = v_mission_id;

    -- Rebuild a clean runtime plan. mission-hierarchy.spec.ts drives this same shared
    -- mission and leaves stray stages/substages (unfinished treasure hunts, empty stages)
    -- that keep readiness false, so the activate test can't enable its button. Wipe the
    -- whole hierarchy (FKs cascade to substages/targets/clues) and re-create the single
    -- Stage 1 + Trivia substage, keeping the mission id stable and repeatable on a persistent DB.
    DELETE FROM "MissionStages" WHERE "MissionId" = v_mission_id;

    INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
    VALUES (v_mission_id, 'Stage 1', 1)
    RETURNING "Id" INTO v_stage_id;

    INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode", "TriviaQuizId")
    VALUES (v_stage_id, 'Trivia Substage', 1, 'Trivia', v_quiz_id);
  END IF;
END $$;
`, 'E2E activatable mission ensured.')

  // Seed a mission that stays not-runtime-ready (Draft, IsActive) for the whole run. The session
  // dropdown gating tests need a stable disabled option; they must NOT reuse 'E2E Activatable
  // Mission' because missions.spec.ts activates that one Draft -> Ready mid-run, which would flip
  // it to enabled and break those tests in a full-suite run (they pass in isolation otherwise).
  // Nothing activates this mission, so it remains a reliable "not runtime-ready" fixture.
  runSql('mission_design', `
DO $$
DECLARE
  v_mission_id INT;
  v_stage_id   INT;
  v_quiz_id    INT;
BEGIN
  SELECT "Id" INTO v_quiz_id
  FROM "TriviaQuizzes"
  WHERE "Title" = 'Filosofos de Atenas' AND "Status" = 'Published'
  ORDER BY "Id" DESC LIMIT 1;

  IF v_quiz_id IS NULL THEN
    RAISE EXCEPTION 'No Published "Filosofos de Atenas" quiz found — run seed-dev-data.sh first.';
  END IF;

  SELECT "Id" INTO v_mission_id FROM "Missions" WHERE "Name" = 'E2E Not-Ready Mission' LIMIT 1;

  IF v_mission_id IS NULL THEN
    INSERT INTO "Missions" ("Name", "Description", "Difficulty", "MaximumTimeMinutes", "IsActive", "ActivationState", "Created", "LastModified")
    VALUES ('E2E Not-Ready Mission', 'Seeded Draft mission that stays not-runtime-ready — do not activate or delete', 'Easy', 45, true, 'Draft', NOW(), NOW())
    RETURNING "Id" INTO v_mission_id;

    INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
    VALUES (v_mission_id, 'Stage 1', 1)
    RETURNING "Id" INTO v_stage_id;

    INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode", "TriviaQuizId")
    VALUES (v_stage_id, 'Trivia Substage', 1, 'Trivia', v_quiz_id);
  ELSE
    -- Idempotent reset: force back to Draft and rebuild a clean plan in case a prior run drifted.
    UPDATE "Missions"
    SET "IsActive" = true, "ActivationState" = 'Draft', "LastModified" = NOW()
    WHERE "Id" = v_mission_id;

    DELETE FROM "MissionStages" WHERE "MissionId" = v_mission_id;

    INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
    VALUES (v_mission_id, 'Stage 1', 1)
    RETURNING "Id" INTO v_stage_id;

    INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode", "TriviaQuizId")
    VALUES (v_stage_id, 'Trivia Substage', 1, 'Trivia', v_quiz_id);
  END IF;
END $$;
`, 'E2E not-ready mission ensured.')
}

// Seed op-1's identity-access row keyed by its resolved Keycloak sub (UUID). Deletes any prior
// op-1 rows (literal-username or a stale sub from an earlier run) by email first, so a persistent
// DB never accumulates duplicates. Must run after seedKeycloak() so the sub is known.
function seedOperatorIdentity(sub: string): void {
  runSql('identity_access', `
DELETE FROM users WHERE "Email" = 'op-1@umbral.local';
INSERT INTO users ("ExternalIdentityId", "DisplayName", "Email", "Role", "IsActive", "Created", "LastModified")
VALUES ('${sub}', 'Operator One', 'op-1@umbral.local', 'Operator', true, NOW(), NOW());
`, `Operator identity seeded with Keycloak sub ${sub}.`)
}

// Seed participant-1's identity-access row keyed by its resolved Keycloak sub (UUID), then link it to the
// Gilded Owls reference team via registered_team_memberships. Deletes any prior participant-1 row (literal
// username or a stale sub) by email first — the FK cascade drops its old membership too — so a persistent DB
// never accumulates duplicates. user_id is resolved by email in the same batch because the fresh users row
// gets a new identity Id; the membership uses the Gilded Owls REFERENCE id, matching the reference id the
// mobile client submits, so the HU-36A answer-flip's ParticipantMembershipAccessAuthorizationProxy authorizes
// it. Must run after seedKeycloak() (for the sub) and after seedViaDocker() seeds registered_teams.
function seedParticipantIdentity(sub: string): void {
  runSql('identity_access', `
DELETE FROM users WHERE "Email" = 'participant-1@umbral.local';
INSERT INTO users ("ExternalIdentityId", "DisplayName", "Email", "Role", "IsActive", "Created", "LastModified")
VALUES ('${sub}', 'Participant One', 'participant-1@umbral.local', 'Participant', true, NOW(), NOW());
INSERT INTO registered_team_memberships (id, team_id, user_id)
SELECT gen_random_uuid(), 'a0000000-0000-0000-0000-000000000001', "Id"
FROM users WHERE "Email" = 'participant-1@umbral.local';
`, `Participant identity seeded with Keycloak sub ${sub} and Gilded Owls membership.`)
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

// Returns each user's resolved Keycloak id (sub) keyed by username, so identity-access rows can
// be seeded to match the sub the gateway forwards as X-User-Id.
async function seedKeycloak(): Promise<Map<string, string>> {
  const adminToken = await getAdminToken()
  const subsByUsername = new Map<string, string>()

  for (const user of keycloakUsers) {
    const userId = await ensureUser(adminToken, user)
    await syncRole(adminToken, userId, user.role)
    await assertUserCanAuthenticate(user)
    subsByUsername.set(user.username, userId)
  }

  console.log('[global-setup] E2E Keycloak users ensured.')
  return subsByUsername
}

async function main() {
  try {
    seedViaDocker()
  } catch (err) {
    console.warn('[global-setup] Could not seed test users:', (err as Error).message?.slice(0, 200))
  }

  try {
    const subsByUsername = await seedKeycloak()
    const operatorSub = subsByUsername.get('op-1')
    if (operatorSub) {
      seedOperatorIdentity(operatorSub)
    }
    const participantSub = subsByUsername.get('participant-1')
    if (participantSub) {
      seedParticipantIdentity(participantSub)
    }
  } catch (err) {
    console.warn('[global-setup] Could not seed Keycloak users:', (err as Error).message?.slice(0, 200))
  }
}

export default main
