// HU-28 live-behavior e2e: the operator AUTHORS a free-text operative clue from the operator hero and
// assigns it to one team, several, or all. Drives a real create -> assign -> attach two teams -> Active
// through the gateway (mirroring session-clue-release.spec.ts), then asserts the success note + assigned
// count (one team and all-teams via the select-all toggle), the Active-or-Paused render gate (a Preparing
// session shows the inactive note and no submit), and the disabled-submit validation pre-empt.
//
// Unlike HU-26 release, operative clues carry NO target — so no target seed is needed; the mission only
// has to be runtime-ready with two teams attached. beforeAll adds an admin row keyed by admin-1's
// Keycloak sub so the gateway-JWT operator-assignment path resolves the actor (additive, parallel-safe).
// Non-owner 403 is covered at the unit/action layer (addOperativeClue 403 -> IdentityError -> action
// { unauthorized }); a second sub-keyed operator against the live stack is flaky, matching the sibling
// clue-release / timer / answered specs. The board reveal / team-isolation properties are participant-
// board (mobile, Track B) assertions — out of scope here.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'
const TEAM_1 = 'a0000000-0000-0000-0000-000000000001' // Gilded Owls
const TEAM_2 = 'a0000000-0000-0000-0000-000000000002' // Maple Runners
const MISSION_NAME = 'Operative Clue E2E'

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

// Authors a runtime-ready TreasureHunt mission with a single target, rebuilt on each run. Operative
// clues need no target, but a runtime-ready TreasureHunt substage requires one — so seed the minimum.
function authorMission(): void {
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
    VALUES ('${MISSION_NAME}', 'Seeded runtime-ready mission for the HU-28 operative-clue e2e — do not delete', 'Easy', 60, true, 'Ready', NOW(), NOW())
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

  INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
  VALUES (v_substage_id, 'Clue 1', 1, 'Find landmark #1 and scan its code.', 'VisibleWhenSubstageStarts')
  RETURNING "Id" INTO v_clue_id;

  INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId")
  VALUES (v_substage_id, 'Target 1', 'OC-E2E-QR-1', 1, true, 50, v_clue_id);
END $$;
`)
}

let activeCode = ''
let prepCode = ''

test.describe.configure({ mode: 'serial' })
test.setTimeout(90000)

// Stages two sessions: one Active (the authoring surface) and one Preparing (for the inactive gate).
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
  sql('users', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)
  const opId = Number(sql('users', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))

  authorMission()
  const missionId = Number(
    sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='${MISSION_NAME}' AND "IsActive"=true AND "ActivationState"='Ready' ORDER BY "Id" DESC LIMIT 1`),
  )
  expect(missionId).toBeGreaterThan(0)

  const active = await stageSession(admin, op, opId, missionId, 'Operative Clue E2E Active', true)
  activeCode = active.code
  const prep = await stageSession(admin, op, opId, missionId, 'Operative Clue E2E Preparing', false)
  prepCode = prep.code
})

async function openLiveOperation(page: import('@playwright/test').Page, code: string) {
  await page.goto('/dashboard')
  await page.getByTestId('nav-sessions').click()
  const card = page.locator('[data-testid="assigned-session-button"]', { hasText: code })
  await expect(card).toBeVisible({ timeout: 15000 })
  await card.click()
  await page.getByRole('button', { name: 'Abrir operación en vivo' }).click()
  await expect(page.locator('[data-testid="operator-panel"]')).toBeVisible({ timeout: 15000 })
  await expect(page.locator('[data-testid="operative-clue-panel"]')).toBeVisible({ timeout: 15000 })
}

// AC1, AC2: authoring an operative clue and assigning it to one team shows the success note + count.
test('operator assigns an operative clue to one team: success note + assigned count', async ({ operatorPage: page }) => {
  await openLiveOperation(page, activeCode)

  await page.locator('[data-testid="operative-clue-text-input"]').fill('Look beneath the blue banner.')
  // The "Assign to" dropdown is keyed by the RUNTIME live-session team id (not the reference id), so pick
  // Gilded Owls by its visible option label.
  await page.locator('[data-testid="operative-clue-team-select"]').selectOption({ label: 'Gilded Owls' })
  await page.locator('[data-testid="operative-clue-submit"]').click()

  await expect(page.locator('[data-testid="operative-clue-success"]')).toBeVisible({ timeout: 15000 })
  await expect(page.locator('[data-testid="operative-clue-success"]')).toHaveText(/Assigned to 1 team\./)
  await expect(page.locator('[data-testid="operative-clue-error"]')).toHaveCount(0)
})

// AC2: the "All teams" option (the dropdown's default) assigns to every attached team; assigning reports
// the full team count.
test('operator assigns an operative clue to all teams via the All teams option', async ({ operatorPage: page }) => {
  await openLiveOperation(page, activeCode)

  await page.locator('[data-testid="operative-clue-text-input"]').fill('Regroup at the fountain in ten minutes.')
  // "All teams" is the dropdown's default (value ''); select it explicitly for clarity.
  await page.locator('[data-testid="operative-clue-team-select"]').selectOption({ label: 'All teams' })
  await page.locator('[data-testid="operative-clue-submit"]').click()

  await expect(page.locator('[data-testid="operative-clue-success"]')).toBeVisible({ timeout: 15000 })
  await expect(page.locator('[data-testid="operative-clue-success"]')).toHaveText(/Assigned to 2 teams\./)
  await expect(page.locator('[data-testid="operative-clue-error"]')).toHaveCount(0)
})

// AC1 (live gate): a non-live (Preparing) session renders the inactive note and no submit control.
test('the operative-clue control is inactive until the session is Active or Paused', async ({ operatorPage: page }) => {
  await openLiveOperation(page, prepCode)

  await expect(page.locator('[data-testid="operative-clue-inactive"]')).toBeVisible()
  await expect(page.locator('[data-testid="operative-clue-submit"]')).toHaveCount(0)
})

// AC4 (validation pre-empt): submit is disabled while the clue text is empty ("All teams" is the default
// target, so a non-empty clue is the only gate).
test('submit is disabled with empty clue text', async ({ operatorPage: page }) => {
  await openLiveOperation(page, activeCode)

  const submit = page.locator('[data-testid="operative-clue-submit"]')
  // Empty text → disabled.
  await expect(submit).toBeDisabled()

  // Non-empty text → enabled (All teams is a valid default target).
  await page.locator('[data-testid="operative-clue-text-input"]').fill('Head to the north gate.')
  await expect(submit).toBeEnabled()

  // Clear the text again → disabled once more.
  await page.locator('[data-testid="operative-clue-text-input"]').fill('')
  await expect(submit).toBeDisabled()
})
