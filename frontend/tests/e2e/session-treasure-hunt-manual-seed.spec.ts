// HU-23 MANUAL-TEST seed (not a behavior assertion). Authors a runtime-ready TreasureHunt mission and
// creates -> assigns -> attaches a "Treasure Hunt E2E" session, left in PREPARING one operator "Start"
// click away from Active. Once Active, the live session's first (TreasureHunt) substage becomes the active
// substage, so a mobile participant on the Gilded Owls team lands on the HU-23 live team board (score /
// target progress / visible clues) instead of the trivia question stage.
//
// Why author a mission here (unlike session-answered-monitor-manual-seed, which reuses global-setup's
// "E2E Seed Mission"): global-setup only seeds Trivia missions, and the HU-23 board renders solely when
// activeSubstage.playMode === 'TreasureHunt'. There is no seeded treasure-hunt mission otherwise.
//
// The mission is authored directly in mission_design via SQL (mirroring global-setup's mission seeds).
// POST /api/sessions then builds the immutable runtime snapshot from it automatically — no manual
// snapshot SQL. For clues to surface on the board each target must link to a clue (MissionTargets.ClueId),
// so the seed authors one clue per target. currentScore starts at 0 and resolvedTargets is always 0 until
// target-resolution lands (HU-31); the board still renders with the substage title, timer, target count
// and visible clues. The backend now emits a TeamBoardUpdated SignalR push on session-state change and
// substage advancement, so the board flips live when the operator Starts (no manual reload); score and
// target-progress still don't push until HU-31, and the REST team-board fetch remains the reconnect fallback.
//
// global-setup seeds participant-1 + the Gilded Owls registered_team_memberships row (so the participant
// is authorized on that team) and op-1's sub-keyed identity; this spec only authors the mission and stages
// the session up to — but not including — the drive to Active.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'
const TEAM = 'a0000000-0000-0000-0000-000000000001' // Gilded Owls (global-setup seed)
const MISSION_NAME = 'Treasure Hunt E2E'

// Reads a single scalar via `psql -c`; fine for simple SELECTs. Multi-statement authoring uses runSql().
function sql(db: string, query: string): string {
  return execSync(`docker exec ${DB} psql -U postgres -d ${db} -t -A -c "${query.replace(/"/g, '\\"')}"`)
    .toString()
    .trim()
}

// Pipes SQL on stdin (no -c), so the PascalCase-quoted DDL below needs no shell escaping. ON_ERROR_STOP
// makes a broken statement fail the seed loudly instead of silently handing the tester a half-built mission.
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

// Idempotently authors a runtime-ready TreasureHunt mission: one stage, one TreasureHunt substage (the
// first substage, so it becomes active on Start), and three active targets each linked to a clue. Rebuilds
// the hierarchy on every run (DELETE cascades stages -> substages -> targets/clues) so a persistent DB stays
// clean and the mission id survives. Difficulty 'Easy' matches global-setup's SQL seeds; per-target Score is
// supplied explicitly (50) because it is NOT NULL and, unlike API authoring, is not derived from difficulty.
function authorTreasureHuntMission(): void {
  runSql('mission_design', `
DO $$
DECLARE
  v_mission_id  INT;
  v_stage_id    INT;
  v_substage_id INT;
  v_clue_id     INT;
  v_seq         INT;
BEGIN
  SELECT "Id" INTO v_mission_id FROM "Missions" WHERE "Name" = '${MISSION_NAME}' LIMIT 1;

  IF v_mission_id IS NULL THEN
    INSERT INTO "Missions" ("Name", "Description", "Difficulty", "MaximumTimeMinutes", "IsActive", "ActivationState", "Created", "LastModified")
    VALUES ('${MISSION_NAME}', 'Seeded runtime-ready treasure-hunt mission for the HU-23 manual test — do not delete', 'Easy', 60, true, 'Ready', NOW(), NOW())
    RETURNING "Id" INTO v_mission_id;
  ELSE
    UPDATE "Missions" SET "IsActive" = true, "ActivationState" = 'Ready', "LastModified" = NOW() WHERE "Id" = v_mission_id;
    DELETE FROM "MissionStages" WHERE "MissionId" = v_mission_id; -- FKs cascade to substages/targets/clues
  END IF;

  INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
  VALUES (v_mission_id, 'Stage 1', 1)
  RETURNING "Id" INTO v_stage_id;

  INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode")
  VALUES (v_stage_id, 'Treasure Hunt Substage', 1, 'TreasureHunt')
  RETURNING "Id" INTO v_substage_id;

  -- One clue per target; the target links to its clue via ClueId so the runtime plan (and thus the board's
  -- visibleClues) carries the clue text. VisibleWhenSubstageStarts => shown as soon as the substage is active.
  FOR v_seq IN 1..3 LOOP
    INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
    VALUES (v_substage_id, 'Clue ' || v_seq, v_seq, 'Find landmark #' || v_seq || ' and scan its code.', 'VisibleWhenSubstageStarts')
    RETURNING "Id" INTO v_clue_id;

    INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId")
    VALUES (v_substage_id, 'Target ' || v_seq, 'TH-E2E-QR-' || v_seq, v_seq, true, 50, v_clue_id);
  END LOOP;
END $$;
`)
}

test.setTimeout(60000)

// Seeds a startable treasure-hunt session; no browser is driven. The assertions guard the seed itself so a
// broken stack fails loudly instead of silently handing the tester a card they can't operate.
test('seeds "Treasure Hunt E2E" in Preparing, ready for the operator to Start on demand', async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  authorTreasureHuntMission()

  // Sub-keyed admin identity for the gateway-JWT assign path (mirrors session-answered-monitor-manual-seed).
  const adminSub = subOf(admin)
  sql('identity_access', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)
  const opId = Number(sql('identity_access', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))
  const missionId = Number(
    sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='${MISSION_NAME}' AND "IsActive"=true AND "ActivationState"='Ready' ORDER BY "Id" DESC LIMIT 1`),
  )
  expect(missionId).toBeGreaterThan(0)

  const created = await (await api('POST', '/api/sessions', admin, {
    missionId,
    title: MISSION_NAME,
    maximumTimeMinutes: 60,
    scheduledAt: '2026-07-05T10:00:00Z',
  })).json()
  const liveSessionId = created.liveSessionId as string
  const sessionCode = created.sessionCode as string
  expect(liveSessionId).toBeTruthy()

  expect(
    (await api('PATCH', `/api/sessions/${liveSessionId}/operator-assignment`, admin, { operatorUserId: opId })).ok,
  ).toBe(true)
  // Teams must be associated while the session is Scheduled (before Preparing).
  expect((await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM })).ok).toBe(true)

  // Stop at Preparing — deliberately NOT Active. The operator presses "Start" in the UI to begin; entering
  // Active sets the active substage to the first (TreasureHunt) substage, and the mobile board renders.
  expect((await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })).ok).toBe(true)

  // Surfaced to the console so the tester can copy the code the mobile participant needs.
  console.log(`\n  HU-23 manual-test session ready (Preparing):`)
  console.log(`    title: ${MISSION_NAME}`)
  console.log(`    session code: ${sessionCode}`)
  console.log(`    -> join on mobile as participant-1 / Gilded Owls, then Start as op-1 once staged.\n`)
})
