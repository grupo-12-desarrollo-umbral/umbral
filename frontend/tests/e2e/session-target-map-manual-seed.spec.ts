// #153 MANUAL-TEST seed (not a behavior assertion). Authors a runtime-ready TreasureHunt mission whose
// targets carry real geographic coordinates, and creates -> assigns -> attaches a "Target Map E2E" session,
// left in PREPARING one operator "Start" click from Active. Once Active, the mobile participant's Map tab
// renders a real Leaflet map (WebView) with ember pins — the #156 real map view.
//
// Delta vs. session-treasure-hunt-manual-seed (HU-23): "Latitude" and "Longitude" columns populated on
// MissionTargets so the runtime snapshot carries ActiveTargets with coordinates. No coordinates = the mobile
// Map tab shows the "NO MAP LOCATION YET" empty state (still tested by the HU-23 seed).
//
// The operator dashboard also covers the map editor (#156) — coordinate inputs, click-to-pick, overview —
// but that path is authored entirely through the UI (no seed), covered in the manual-test doc.
//
// global-setup seeds participant-1 + the Gilded Owls registered_team_memberships row (so the participant is
// authorized on that team) and op-1's sub-keyed identity; this spec only authors the mission and stages
// the session up to — but not including — the drive to Active.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'
const TEAM = 'a0000000-0000-0000-0000-000000000001' // Gilded Owls (global-setup seed)
const MISSION_NAME = 'Target Map E2E'

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

// Idempotently authors a runtime-ready TreasureHunt mission with three targets at real-world coordinates.
// One target per city so the Leaflet map shows three distinct pins spread across the globe — the centre is
// set to Target 1 (Bogotá, sequence order 1), which becomes markers[0] on the mobile map.
// Rebuilds the hierarchy on every run (DELETE cascades) so a persistent DB stays clean.
function authorTargetMapMission(): void {
  runSql('mission_design', `
DO $$
DECLARE
  v_mission_id  INT;
  v_stage_id    INT;
  v_substage_id INT;
  v_clue_id     INT;
BEGIN
  SELECT "Id" INTO v_mission_id FROM "Missions" WHERE "Name" = '${MISSION_NAME}' LIMIT 1;

  IF v_mission_id IS NULL THEN
    INSERT INTO "Missions" ("Name", "Description", "Difficulty", "MaximumTimeMinutes", "IsActive", "ActivationState", "Created", "LastModified")
    VALUES ('${MISSION_NAME}', 'Seeded runtime-ready treasure-hunt mission with coordinates for the #156 manual test — do not delete', 'Beginner', 60, true, 'Ready', NOW(), NOW())
    RETURNING "Id" INTO v_mission_id;
  ELSE
    UPDATE "Missions" SET "IsActive" = true, "ActivationState" = 'Ready', "LastModified" = NOW() WHERE "Id" = v_mission_id;
    DELETE FROM "MissionStages" WHERE "MissionId" = v_mission_id;
  END IF;

  INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
  VALUES (v_mission_id, 'Stage 1', 1)
  RETURNING "Id" INTO v_stage_id;

  INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode")
  VALUES (v_stage_id, 'Map Hunt Substage', 1, 'TreasureHunt')
  RETURNING "Id" INTO v_substage_id;

  -- Target 1 — Bogotá (centre of the mobile map)
  INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
  VALUES (v_substage_id, 'Clue 1', 1, 'Find the landmark near Plaza de Bolívar.', 'VisibleWhenSubstageStarts')
  RETURNING "Id" INTO v_clue_id;
  INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId", "Latitude", "Longitude")
  VALUES (v_substage_id, 'Plaza de Bolívar', 'MAP-E2E-QR-1', 1, true, 50, v_clue_id, 4.7110, -74.0721);

  -- Target 2 — Madrid (sequence 2, so it appears after Target 1; second ember pin)
  INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
  VALUES (v_substage_id, 'Clue 2', 2, 'Find the landmark near Puerta del Sol.', 'VisibleWhenSubstageStarts')
  RETURNING "Id" INTO v_clue_id;
  INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId", "Latitude", "Longitude")
  VALUES (v_substage_id, 'Puerta del Sol', 'MAP-E2E-QR-2', 2, true, 50, v_clue_id, 40.4168, -3.7038);

  -- Target 3 — Tokyo (sequence 3; third ember pin)
  INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
  VALUES (v_substage_id, 'Clue 3', 3, 'Find the landmark near Shibuya Crossing.', 'VisibleWhenSubstageStarts')
  RETURNING "Id" INTO v_clue_id;
  INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId", "Latitude", "Longitude")
  VALUES (v_substage_id, 'Shibuya Crossing', 'MAP-E2E-QR-3', 3, true, 50, v_clue_id, 35.6762, 139.6503);
END $$;
`)
}

test.setTimeout(60000)

test('seeds "Target Map E2E" in Preparing, ready for the operator to Start on demand', async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  authorTargetMapMission()

  const adminSub = subOf(admin)
  sql('users', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)
  const opId = Number(sql('users', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))
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
  expect((await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM })).ok).toBe(true)

  expect((await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })).ok).toBe(true)

  console.log(`\n  #156 manual-test session ready (Preparing):`)
  console.log(`    title: ${MISSION_NAME}`)
  console.log(`    session code: ${sessionCode}`)
  console.log(`    team: Gilded Owls — 3 targets with coordinates (Bogotá, Madrid, Tokyo)`)
  console.log(`    -> join on mobile as participant-1 / Gilded Owls, then Start as op-1 once staged.\n`)
})
