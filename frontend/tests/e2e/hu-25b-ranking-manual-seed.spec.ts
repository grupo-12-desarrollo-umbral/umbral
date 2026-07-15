// HU-25B MANUAL-TEST seed (not a behavior assertion). Seeds a treasure-hunt session
// with two registered teams and pre-computed ranking data in the scoring database.
// The session is left in PREPARING, one operator click from Active.
//
// Once Active, the mobile participant joins and sees the TEAMS tab populated with
// real ranking rows via GET /api/sessions/{id}/ranking (REST) and live
// RankingChanged pushes over the ScoringHub when new score entries are processed.
//
// Why a dedicated seed: global-setup only seeds single-team trivia sessions.
// HU-25B needs a multi-team treasure-hunt session with pre-seeded ranking data
// so the mobile ranking surface has something to render immediately after join.
//
// The mission is authored directly in mission_design via SQL. Score entries and
// ranking rows are seeded into scoring_monitoring to bypass RabbitMQ consumers.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'

// Global-setup seeds these (see tests/e2e/global-setup.ts):
//   participant-1 / tester123      (sub: a0000000-...-0001)
//   op-1          / operator123
//   Team Gilded Owls                (id: a0000000-...-0001)

const TEAM_A = 'a0000000-0000-0000-0000-000000000001' // Gilded Owls (global-setup)
const TEAM_B = 'a0000000-0000-0000-0000-000000000002' // Crimson Foxes (upserted here)
// Canonical team names — used for BOTH the reference-catalog row (identity_access,
// which the lobby snapshots) and the ranking rows (scoring_monitoring). Keep them in
// one place so the two contexts can never drift into showing different names for the
// same team on the TEAMS tab.
const TEAM_A_NAME = 'Gilded Owls'
const TEAM_B_NAME = 'Crimson Foxes'
const MISSION_NAME = 'HU-25B Ranking E2E'
const STAGE_TITLE = 'Main Stage'
const SUBSTAGE_TITLE = 'Treasure Hunt'

function sql(db: string, query: string): string {
  return execSync(`docker exec ${DB} psql -U postgres -d ${db} -t -A -c "${query.replace(/"/g, '\\"')}"`)
    .toString()
    .trim()
}

function runSql(db: string, sqlText: string): void {
  execSync(`docker exec -i ${DB} psql -v ON_ERROR_STOP=1 -U postgres -d ${db}`, {
    input: sqlText,
    stdio: ['pipe', 'pipe', 'pipe'],
    timeout: 20000,
  })
}

async function token(username: string, password: string): Promise<string> {
  const res = await fetch(`${KC}/realms/umbral/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: `grant_type=password&client_id=umbral-web&username=${username}&password=${password}`,
  })
  return (await res.json()).access_token as string
}

function subOf(jwt: string): string {
  return JSON.parse(Buffer.from(jwt.split('.')[1], 'base64').toString()).sub
}

async function api(method: string, path: string, tok: string, body?: unknown): Promise<Response> {
  return fetch(`${GW}${path}`, {
    method,
    headers: { Authorization: `Bearer ${tok}`, 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  })
}

// Authors a simple treasure-hunt mission: one stage, one TreasureHunt substage,
// two targets with QR codes (one immediate, one operator-release). No trivia
// dependency means the mobile board shows the treasure-hunt surface + TEAMS tab
// immediately on start.
function authorTreasureHuntMission(): string {
  const missionId = sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='${MISSION_NAME}' AND "IsActive"=true LIMIT 1`)
  if (missionId) return missionId

  runSql('mission_design', `
DO $$
DECLARE
  v_mission_id    INT;
  v_stage_id      INT;
  v_sub_id        INT;
  v_clue_id       INT;
BEGIN
  INSERT INTO "Missions" ("Name", "Description", "Difficulty", "MaximumTimeMinutes", "IsActive", "ActivationState", "Created", "LastModified")
  VALUES ('${MISSION_NAME}', 'Seeded treasure-hunt mission for HU-25B ranking manual test', 'Easy', 60, true, 'Ready', NOW(), NOW())
  RETURNING "Id" INTO v_mission_id;

  INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
  VALUES (v_mission_id, '${STAGE_TITLE}', 1)
  RETURNING "Id" INTO v_stage_id;

  INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode")
  VALUES (v_stage_id, '${SUBSTAGE_TITLE}', 1, 'TreasureHunt')
  RETURNING "Id" INTO v_sub_id;

  -- Clue 1: visible immediately
  INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
  VALUES (v_sub_id, 'First Clue', 1, 'Find the brass astrolabe near the fountain.', 'VisibleWhenSubstageStarts')
  RETURNING "Id" INTO v_clue_id;

  INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId")
  VALUES (v_sub_id, 'Astrolabe', 'HR-25B-QR-1', 1, true, 150, v_clue_id);

  -- Clue 2: operator release
  INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
  VALUES (v_sub_id, 'Second Clue', 2, 'Look behind the tapestry in the great hall.', 'HiddenUntilOperatorRelease')
  RETURNING "Id" INTO v_clue_id;

  INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId")
  VALUES (v_sub_id, 'Tapestry', 'HR-25B-QR-2', 2, true, 100, v_clue_id);
END $$;
`)

  return sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='${MISSION_NAME}' AND "IsActive"=true ORDER BY "Id" DESC LIMIT 1`)
}

// Seeds score entries and a ranking snapshot directly into scoring_monitoring.
// The REST endpoint and ScoringHub broadcasts read from this database table.
function seedRankingData(liveSessionId: string): void {
  runSql('scoring_monitoring', `
DO $$
DECLARE
  v_ranking_id UUID := gen_random_uuid();
  v_session_id UUID := '${liveSessionId}';
  v_team_a    UUID := '${TEAM_A}';
  v_team_b    UUID := '${TEAM_B}';
BEGIN
  -- Score entries for Team A (higher score, faster resolution).
  -- team_display_name is snapshotted on each entry (matches production, where scoring reads the name
  -- off the integration event); ranking recalculation names rows from it, so it must be populated here.
  INSERT INTO score_entries (id, live_session_id, team_id, team_display_name, entry_type, reason_code, score_value, recorded_at, source_entity_type, source_entity_id, recorded_by_user_id, created_at, created_by, updated_at, updated_by)
  VALUES
    (gen_random_uuid(), v_session_id, v_team_a, '${TEAM_A_NAME}', 'Grant', 'trivia-answer-correct', 200, '2026-07-15T10:00:00Z'::timestamptz, 'TriviaAnswerSubmission', gen_random_uuid(), NULL, NOW(), 'seed', NOW(), 'seed'),
    (gen_random_uuid(), v_session_id, v_team_a, '${TEAM_A_NAME}', 'Grant', 'target-resolved',      150, '2026-07-15T10:05:00Z'::timestamptz, 'TargetResolution',    gen_random_uuid(), NULL, NOW(), 'seed', NOW(), 'seed');

  -- Score entries for Team B (lower score, slower resolution)
  INSERT INTO score_entries (id, live_session_id, team_id, team_display_name, entry_type, reason_code, score_value, recorded_at, source_entity_type, source_entity_id, recorded_by_user_id, created_at, created_by, updated_at, updated_by)
  VALUES
    (gen_random_uuid(), v_session_id, v_team_b, '${TEAM_B_NAME}', 'Grant', 'trivia-answer-correct', 100, '2026-07-15T10:02:00Z'::timestamptz, 'TriviaAnswerSubmission', gen_random_uuid(), NULL, NOW(), 'seed', NOW(), 'seed'),
    (gen_random_uuid(), v_session_id, v_team_b, '${TEAM_B_NAME}', 'Grant', 'target-resolved',      100, '2026-07-15T10:10:00Z'::timestamptz, 'TargetResolution',    gen_random_uuid(), NULL, NOW(), 'seed', NOW(), 'seed');

  -- Ranking snapshot
  INSERT INTO rankings (id, live_session_id, generated_at, calculation_version, created_at, created_by, updated_at, updated_by)
  VALUES (v_ranking_id, v_session_id, NOW(), 1, NOW(), 'seed', NOW(), 'seed');

  -- Ranking rows (Team A 1st, Team B 2nd).
  -- team_display_name mirrors what the GET /api/sessions/{id}/ranking endpoint
  -- returns; the mobile hook reads it from the REST response and SignalR push.
  INSERT INTO ranking_rows (ranking_id, team_id, position, total_score, resolution_time, team_display_name)
  VALUES
    (v_ranking_id, v_team_a, 1, 350, INTERVAL '5 minutes', '${TEAM_A_NAME}'),
    (v_ranking_id, v_team_b, 2, 200, INTERVAL '8 minutes', '${TEAM_B_NAME}');
END $$;
`)
}

test.setTimeout(120000)

test('seeds "HU-25B Ranking E2E" in Preparing with pre-loaded ranking data', async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  // Resolve existing live-session IDs for this mission before deleting anything
  const existingSessionIds = sql('session_operations',
    `SELECT COALESCE(string_agg("id"::text, ','), '') FROM live_sessions WHERE title_snapshot = '${MISSION_NAME}'`)

  // Drop stale ranking/score data from scoring_monitoring first (no FK to session_operations).
  if (existingSessionIds) {
    const ids = existingSessionIds.split(',').filter(Boolean).map(id => `'${id}'`).join(',')
    runSql('scoring_monitoring', `
      DELETE FROM ranking_rows  WHERE ranking_id IN (SELECT id FROM rankings WHERE live_session_id IN (${ids}));
      DELETE FROM rankings      WHERE live_session_id IN (${ids});
      DELETE FROM score_entries WHERE live_session_id IN (${ids});
    `)
  }

  // Clean previous run of this fixture from session_operations
  runSql('session_operations', `
    DELETE FROM live_sessions WHERE title_snapshot = '${MISSION_NAME}';
  `)

  // Ensure admin-1 exists in identity_access
  const adminSub = subOf(admin)
  sql('identity_access', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}' OR "Email"='admin-1@umbral.local';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)

  // Resolve op-1 identity
  const opId = Number(sql('identity_access', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))
  expect(opId).toBeGreaterThan(0)

  // Ensure the second team exists AND carries the canonical name (global-setup only
  // creates Gilded Owls). An earlier seed may already own this id under a different
  // name (e.g. "Maple Runners"); a plain WHERE-NOT-EXISTS insert would skip it and
  // leave the lobby showing that stale name while the ranking rows below say
  // "Crimson Foxes" — two names for one team. Upsert so the catalog is authoritative.
  sql('identity_access', `
    INSERT INTO registered_teams (id, display_name, team_code, is_active, created_at, updated_at)
    VALUES ('${TEAM_B}', '${TEAM_B_NAME}', 'CF-01', true, NOW(), NOW())
    ON CONFLICT (id) DO UPDATE
      SET display_name = EXCLUDED.display_name,
          team_code    = EXCLUDED.team_code,
          is_active    = true,
          updated_at   = NOW();
  `)

  // Author the treasure-hunt mission
  const missionId = Number(authorTreasureHuntMission())
  expect(missionId).toBeGreaterThan(0)

  // Confirm the mission is Ready
  expect(
    sql('mission_design', `SELECT "ActivationState" FROM "Missions" WHERE "Id"=${missionId}`),
  ).toBe('Ready')

  // Create the live session
  const created = await (await api('POST', '/api/sessions', admin, {
    missionId,
    title: MISSION_NAME,
    maximumTimeMinutes: 60,
    scheduledAt: '2026-07-15T10:00:00Z',
  })).json()
  const liveSessionId = created.liveSessionId as string
  const sessionCode = created.sessionCode as string
  expect(liveSessionId).toBeTruthy()

  // Assign operator
  expect(
    (await api('PATCH', `/api/sessions/${liveSessionId}/operator-assignment`, admin, { operatorUserId: opId })).ok,
  ).toBe(true)

  // Add both teams
  expect((await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM_A })).ok).toBe(true)
  expect((await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM_B })).ok).toBe(true)

  // Transition to Preparing (operator can Start on demand)
  expect(
    (await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })).ok,
  ).toBe(true)

  // Seed the ranking data directly into the scoring database
  seedRankingData(liveSessionId)

  console.log(`\n  HU-25B manual-test session ready (Preparing):`)
  console.log(`    title: ${MISSION_NAME}`)
  console.log(`    session code: ${sessionCode}`)
  console.log(`    live session id: ${liveSessionId}`)
  console.log(`    teams: ${TEAM_A_NAME} (${TEAM_A}), ${TEAM_B_NAME} (${TEAM_B})`)
  console.log(`    ranking: pre-seeded (Team A=350, Team B=200)`)
  console.log(`    -> join on mobile as participant-1 / Gilded Owls,`)
  console.log(`       then Start as op-1 to activate the treasure-hunt board.`)
  console.log(`       TEAMS tab shows real ranking rows immediately.\n`)
})
