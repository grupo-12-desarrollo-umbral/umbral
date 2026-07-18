// HU-26 MANUAL-TEST seed (not a behavior assertion). Authors a runtime-ready TreasureHunt mission whose
// target clues are HIDDEN (HiddenUntilOperatorRelease), and creates -> assigns -> attaches a "Clue Release
// E2E" session with TWO teams (Gilded Owls + Maple Runners), left in PREPARING one operator "Start" click
// from Active. Once Active, the participant on Gilded Owls lands on the HU-23 board with an EMPTY clues
// list; the operator then releases a target's hidden clue (HU-26) and the released team's board reveals it
// live. Full manual-test walkthrough: backend/docs/hu-26-manual-test.md.
//
// Two deltas vs. session-treasure-hunt-manual-seed (HU-23): (1) clue Visibility is HiddenUntilOperatorRelease
// so the board withholds the clues until an operator releases them; (2) a second registered team is attached
// so releasing to one team can be shown NOT to leak to the other. currentScore/resolvedTargets stay 0 until
// HU-31; the timer reads Expired until DES-93; ClueReleased is not published to RabbitMQ until DES-92.
//
// global-setup seeds participant-1 + the Gilded Owls registered_team_memberships row (so the participant is
// authorized on that team), the Maple Runners registered team, and op-1's sub-keyed identity; this spec only
// authors the mission and stages the session up to — but not including — the drive to Active.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'
const TEAM_1 = 'a0000000-0000-0000-0000-000000000001' // Gilded Owls  (participant-1's team)
const TEAM_2 = 'a0000000-0000-0000-0000-000000000002' // Maple Runners (other team — for the no-leak check)
const MISSION_NAME = 'Clue Release E2E'

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
// first, so it becomes active on Start), and three active targets each linked to a HIDDEN clue. Rebuilds the
// hierarchy on every run (DELETE cascades stages -> substages -> targets/clues) so a persistent DB stays
// clean and the mission id survives. The only HU-26 delta from the HU-23 seed is Visibility:
// 'HiddenUntilOperatorRelease' — the board withholds the clue until the operator releases it.
function authorHiddenClueMission(): void {
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
    VALUES ('${MISSION_NAME}', 'Seeded runtime-ready treasure-hunt mission with hidden clues for the HU-26 manual test — do not delete', 'Beginner', 60, true, 'Ready', NOW(), NOW())
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

  -- One HIDDEN clue per target; the target links to its clue via ClueId so the runtime plan carries the clue
  -- text, but HiddenUntilOperatorRelease keeps it OFF the board's visibleClues until an operator releases it.
  FOR v_seq IN 1..3 LOOP
    INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
    VALUES (v_substage_id, 'Clue ' || v_seq, v_seq, 'Find landmark #' || v_seq || ' and scan its code.', 'HiddenUntilOperatorRelease')
    RETURNING "Id" INTO v_clue_id;

    INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId")
    VALUES (v_substage_id, 'Target ' || v_seq, 'CR-E2E-QR-' || v_seq, v_seq, true, 50, v_clue_id);
  END LOOP;
END $$;
`)
}

test.setTimeout(60000)

// Seeds a startable treasure-hunt session with two teams; no browser is driven. The assertions guard the
// seed itself so a broken stack fails loudly instead of silently handing the tester a card they can't operate.
test('seeds "Clue Release E2E" in Preparing with hidden clues + two teams, ready for the operator to Start', async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  authorHiddenClueMission()

  // Sub-keyed admin identity for the gateway-JWT assign path (mirrors the HU-23 seed).
  const adminSub = subOf(admin)
  sql('users', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}' OR "Email"='admin-1@umbral.local';
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
  // Both teams must be associated while the session is Scheduled (before Preparing). Two teams lets the
  // tester release to one and confirm the other's board does NOT reveal the clue (no leak).
  expect((await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM_1 })).ok).toBe(true)
  expect((await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM_2 })).ok).toBe(true)

  // Stop at Preparing — deliberately NOT Active. The operator presses "Start" in the UI to begin; entering
  // Active sets the active substage to the first (TreasureHunt) substage, and the mobile board renders (empty
  // clues). The operator then releases a hidden clue (HU-26) and the released team's board reveals it.
  expect((await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })).ok).toBe(true)

  // Surfaced to the console so the tester can copy the code the mobile participant needs.
  console.log(`\n  HU-26 manual-test session ready (Preparing):`)
  console.log(`    title: ${MISSION_NAME}`)
  console.log(`    session code: ${sessionCode}`)
  console.log(`    teams attached: Gilded Owls (participant's team) + Maple Runners (other team)`)
  console.log(`    -> join on mobile as participant-1 / Gilded Owls, then Start as op-1 once staged.\n`)
})
