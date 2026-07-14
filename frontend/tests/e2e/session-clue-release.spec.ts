// HU-26/HU-28 live-behavior e2e: the operator releases a treasure-hunt target's HIDDEN clue from the
// operator hero. Drives a real create -> assign -> attach two teams -> Active through the gateway
// (mirroring the session-operator-panel header + the session-clue-release-manual-seed hidden-clue
// authoring), then picks the target from the release dropdown (fed by GET /clues/releasable — HU-28) and
// asserts the success note + released count, the no-duplicate conflict message, and the Active-only gate.
//
// beforeAll adds an admin row keyed by admin-1's Keycloak sub so the gateway-JWT operator-assignment path
// resolves the actor (global-setup seeds admin keyed by the literal 'admin-1' for the BFF-direct surface).
// The insert is additive, so this file stays parallel-safe with the BFF-direct admin specs. Non-owner 403
// is covered at the unit/action layer (releaseClue 403 -> IdentityError -> action { unauthorized }); a
// second sub-keyed operator against the live stack is flaky, matching the timer/answered/panel specs.
//
// The board reveal / no-leak properties are participant-board (mobile, Step 9c) assertions — out of scope.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'
const TEAM_1 = 'a0000000-0000-0000-0000-000000000001' // Gilded Owls
const TEAM_2 = 'a0000000-0000-0000-0000-000000000002' // Maple Runners
const MISSION_NAME = 'Clue Release E2E'

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

// Authors a runtime-ready TreasureHunt mission with three HIDDEN-clue targets (Visibility
// HiddenUntilOperatorRelease), rebuilt on each run — identical to session-clue-release-manual-seed.
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
    VALUES ('${MISSION_NAME}', 'Seeded runtime-ready treasure-hunt mission with hidden clues for the HU-26 e2e — do not delete', 'Easy', 60, true, 'Ready', NOW(), NOW())
    RETURNING "Id" INTO v_mission_id;
  ELSE
    UPDATE "Missions" SET "IsActive" = true, "ActivationState" = 'Ready', "LastModified" = NOW() WHERE "Id" = v_mission_id;
    DELETE FROM "MissionStages" WHERE "MissionId" = v_mission_id;
  END IF;

  INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
  VALUES (v_mission_id, 'Stage 1', 1)
  RETURNING "Id" INTO v_stage_id;

  INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode")
  VALUES (v_stage_id, 'Treasure Hunt Substage', 1, 'TreasureHunt')
  RETURNING "Id" INTO v_substage_id;

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

let activeCode = ''
let prepCode = ''
// The active substage's first hidden-clue target renders in the dropdown as "{sequenceOrder}. {name}".
const TARGET_OPTION_LABEL = '1. Target 1'

test.describe.configure({ mode: 'serial' })
test.setTimeout(90000)

// Stages two sessions: one Active (with a readable runtime target id) and one Preparing (for the gate).
async function stageSession(admin: string, op: string, opId: number, missionId: number, title: string, toActive: boolean) {
  const created = await (await api('POST', '/api/sessions', admin, {
    missionId,
    title,
    maximumTimeMinutes: 60,
    scheduledAt: '2026-07-05T10:00:00Z',
  })).json()
  const lsid = created.liveSessionId as string
  await api('PATCH', `/api/sessions/${lsid}/operator-assignment`, admin, { operatorUserId: opId })
  await api('POST', `/api/sessions/${lsid}/teams`, op, { referenceTeamId: TEAM_1 })
  await api('POST', `/api/sessions/${lsid}/teams`, op, { referenceTeamId: TEAM_2 })
  await api('PATCH', `/api/sessions/${lsid}/state`, op, { targetState: 'Preparing' })
  if (toActive) await api('PATCH', `/api/sessions/${lsid}/state`, op, { targetState: 'Active' })
  return { lsid, code: created.sessionCode as string }
}

test.beforeAll(async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  const adminSub = subOf(admin)
  sql('identity_access', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)
  const opId = Number(sql('identity_access', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))

  authorHiddenClueMission()
  const missionId = Number(
    sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='${MISSION_NAME}' AND "IsActive"=true AND "ActivationState"='Ready' ORDER BY "Id" DESC LIMIT 1`),
  )
  expect(missionId).toBeGreaterThan(0)

  const active = await stageSession(admin, op, opId, missionId, 'Clue Release E2E Active', true)
  activeCode = active.code
  const prep = await stageSession(admin, op, opId, missionId, 'Clue Release E2E Preparing', false)
  prepCode = prep.code
})

async function openLiveOperation(page: import('@playwright/test').Page, code: string) {
  await page.goto('/dashboard')
  await page.getByTestId('nav-sessions').click()
  const card = page.locator('[data-testid="assigned-session-button"]', { hasText: code })
  await expect(card).toBeVisible({ timeout: 15000 })
  await card.click()
  await page.getByRole('button', { name: 'Open live operation' }).click()
  await expect(page.locator('[data-testid="operator-panel"]')).toBeVisible({ timeout: 15000 })
  await expect(page.locator('[data-testid="clue-release-panel"]')).toBeVisible({ timeout: 15000 })
}

// AC1, AC2: releasing a target's hidden clue to one team shows the success note with the released count.
test('operator releases a target hidden clue to one team: success note + released count', async ({ operatorPage: page }) => {
  await openLiveOperation(page, activeCode)

  await page.locator('[data-testid="clue-release-target-select"]').selectOption({ label: TARGET_OPTION_LABEL })
  // The team select is populated from the operator panel's teamProgress, whose teamId is the RUNTIME
  // live-session team id (not the reference id TEAM_1) — so pick Gilded Owls by its visible label.
  await page.locator('[data-testid="clue-release-team-select"]').selectOption({ label: 'Gilded Owls' })
  await page.locator('[data-testid="clue-release-submit"]').click()

  await expect(page.locator('[data-testid="clue-release-success"]')).toBeVisible({ timeout: 15000 })
  await expect(page.locator('[data-testid="clue-release-success"]')).toHaveText(/Released to 1 team\./)
  await expect(page.locator('[data-testid="clue-release-error"]')).toHaveCount(0)
})

// AC3 (no-duplicate): releasing the same target to the same team again surfaces the duplicate conflict.
test('releasing the same target to the same team again shows the duplicate message', async ({ operatorPage: page }) => {
  await openLiveOperation(page, activeCode)

  await page.locator('[data-testid="clue-release-target-select"]').selectOption({ label: TARGET_OPTION_LABEL })
  // Same runtime team as the success test — selected by label (the select's values are runtime team ids).
  await page.locator('[data-testid="clue-release-team-select"]').selectOption({ label: 'Gilded Owls' })
  await page.locator('[data-testid="clue-release-submit"]').click()

  await expect(page.locator('[data-testid="clue-release-error"]')).toBeVisible({ timeout: 15000 })
  await expect(page.locator('[data-testid="clue-release-error"]')).toHaveText(/already released to that team/i)
})

// AC1 (Active gate): a non-Active session renders the inactive note and no submit control.
test('the clue-release control is inactive until the session is Active', async ({ operatorPage: page }) => {
  await openLiveOperation(page, prepCode)

  await expect(page.locator('[data-testid="clue-release-inactive"]')).toBeVisible()
  await expect(page.locator('[data-testid="clue-release-submit"]')).toHaveCount(0)
})
