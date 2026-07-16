// HU-24B live-behavior e2e: the operator's live RankingPanel.
//   1) A penalty applied through the gateway lands on the panel LIVE (350 -> 250) with no manual
//      reload — the RankingChanged push reaches the operator's scoring-hub group and the panel
//      re-renders. This is the path the group-join fix protects: a strand-out-of-group would leave
//      the panel frozen on its REST snapshot.
//   2) When the scoring hub cannot connect, the panel still shows its REST snapshot but honestly
//      flags itself paused rather than presenting stale standings as live.
//
// Setup: create -> assign -> team -> Active through the gateway, then seed a 350-point baseline into
// scoring. Unlike the older specs, this one does NOT hand-patch scoring's session_operator_assignments:
// assigning through `PATCH .../operator-assignment` publishes LiveSessionOperatorAssignedIntegrationEvent
// (session-ops outbox, committed 34c203e), which scoring's LiveSessionOperatorAssignedConsumer projects.
// beforeAll waits for that projection row to land, so this spec also proves the real event path end to
// end — without it, ScoringSessionAuthorizationProxy would 403 both the REST snapshot and the hub join.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'
const TEAM_A = 'a0000000-0000-0000-0000-000000000001' // Gilded Owls (global-setup); scoring keys on ReferenceTeamId
const TEAM_A_NAME = 'Gilded Owls'

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

// A 350-point grant baseline for TEAM_A (two grants + a v1 ranking snapshot), mirroring
// hu-38-operator-penalty / hu-25b-ranking-manual-seed. The flat-100 penalty must land on exactly 250,
// a value meaningful only against a positive baseline the fresh session otherwise lacks.
function seedGrantBaseline(liveSessionId: string): void {
  runSql('scoring_monitoring', `
DO $$
DECLARE
  v_ranking_id UUID := gen_random_uuid();
  v_session_id UUID := '${liveSessionId}';
  v_team_a    UUID := '${TEAM_A}';
BEGIN
  INSERT INTO score_entries (id, live_session_id, team_id, team_display_name, entry_type, reason_code, score_value, recorded_at, source_entity_type, source_entity_id, recorded_by_user_id, created_at, created_by, updated_at, updated_by)
  VALUES
    (gen_random_uuid(), v_session_id, v_team_a, '${TEAM_A_NAME}', 'Grant', 'trivia-answer-correct', 200, '2026-07-15T10:00:00Z'::timestamptz, 'TriviaAnswerSubmission', gen_random_uuid(), NULL, NOW(), 'seed', NOW(), 'seed'),
    (gen_random_uuid(), v_session_id, v_team_a, '${TEAM_A_NAME}', 'Grant', 'target-resolved',      150, '2026-07-15T10:05:00Z'::timestamptz, 'TargetResolution',    gen_random_uuid(), NULL, NOW(), 'seed', NOW(), 'seed');

  INSERT INTO rankings (id, live_session_id, generated_at, calculation_version, created_at, created_by, updated_at, updated_by)
  VALUES (v_ranking_id, v_session_id, NOW(), 1, NOW(), 'seed', NOW(), 'seed');

  INSERT INTO ranking_rows (ranking_id, team_id, position, total_score, resolution_time, team_display_name)
  VALUES (v_ranking_id, v_team_a, 1, 350, INTERVAL '5 minutes', '${TEAM_A_NAME}');
END $$;
`)
}

let activeCode = ''
let lsid = ''

test.describe.configure({ mode: 'serial' })
test.setTimeout(90000)

test.beforeAll(async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  // Sub-keyed admin row so operator-assignment (gateway->JWT) resolves the actor (mirrors the sibling specs).
  const adminSub = subOf(admin)
  sql('identity_access', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)
  const opId = Number(sql('identity_access', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))

  const missionId = Number(sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='E2E Seed Mission' ORDER BY "Id" DESC LIMIT 1`))
  expect(missionId).toBeGreaterThan(0)

  const createRes = await api('POST', '/api/sessions', admin, {
    missionId,
    title: 'Operator Ranking E2E',
    maximumTimeMinutes: 60,
    scheduledAt: '2026-07-05T10:00:00Z',
  })
  expect(createRes.ok, `session create failed: ${createRes.status}`).toBeTruthy()
  const created = await createRes.json()
  lsid = created.liveSessionId as string
  activeCode = created.sessionCode as string
  expect(lsid, 'liveSessionId missing from create response').toBeTruthy()

  await api('PATCH', `/api/sessions/${lsid}/operator-assignment`, admin, { operatorUserId: opId })
  await api('POST', `/api/sessions/${lsid}/teams`, op, { referenceTeamId: TEAM_A })
  await api('PATCH', `/api/sessions/${lsid}/state`, op, { targetState: 'Preparing' })
  await api('PATCH', `/api/sessions/${lsid}/state`, op, { targetState: 'Active' })

  // Prove the real propagation path: the assignment above publishes the operator-assigned integration
  // event; scoring-monitoring's consumer must upsert the projection with op-1's Keycloak sub. Wait for
  // it (RabbitMQ hop, ~1-3s) rather than hand-patching. If this times out, the running session-ops /
  // scoring container predates the publisher wiring — `make rewire` both and retry.
  await expect
    .poll(
      () => sql('scoring_monitoring', `SELECT assigned_operator_user_id FROM session_operator_assignments WHERE live_session_id='${lsid}'`),
      { timeout: 20000, message: 'operator-assignment event never reached the scoring projection' },
    )
    .toBe(subOf(op))

  seedGrantBaseline(lsid)
})

async function openLiveOperation(page: import('@playwright/test').Page, code: string) {
  await page.goto('/dashboard')
  await page.getByTestId('nav-sessions').click()
  const card = page.locator('[data-testid="assigned-session-button"]', { hasText: code })
  await expect(card).toBeVisible({ timeout: 15000 })
  await card.click()
  await page.getByRole('button', { name: 'Open live operation' }).click()
  await expect(page.locator('[data-testid="operator-session-panel"]')).toBeVisible({ timeout: 15000 })
}

test('operator ranking updates live on a penalty push, without a manual reload', async ({ operatorPage: page }) => {
  await openLiveOperation(page, activeCode)

  const score = page.getByTestId(`ranking-score-${TEAM_A}`)
  await expect(score).toHaveText('350 pts', { timeout: 20000 })
  // Live channel healthy once the scoring hub connects + joins: no stale-standings notice.
  await expect(page.getByTestId('ranking-live-paused')).toHaveCount(0, { timeout: 20000 })

  // Apply a flat-100 penalty to Gilded Owls straight through the gateway (the UI penalty flow has its
  // own coverage in hu-38-operator-penalty). The point here is the RankingChanged push, not the form.
  const op = await token('op-1', 'operator123')
  const penaltyRes = await api('POST', `/api/sessions/${lsid}/penalties`, op, {
    teamId: TEAM_A,
    reason: 'Used a phone during a no-device substage.',
  })
  expect(penaltyRes.ok, `penalty failed: ${penaltyRes.status}`).toBeTruthy()

  // The recalculated total lands on the SAME page instance (no page.reload()): the operator's
  // scoring-hub join delivered RankingChanged and the panel replaced its rows.
  await expect(score).toHaveText('250 pts', { timeout: 20000 })
})

test('ranking panel flags itself paused when the scoring hub cannot connect', async ({ operatorPage: page }) => {
  // Block the browser's scoring-hub socket (negotiate + websocket) while leaving the REST snapshot —
  // fetched server-side by the operator action, not the browser — intact. Only /hubs/scoring is cut;
  // the session hub (/hubs/sessions) is untouched, so the rest of the dashboard stays live.
  await page.route('**/hubs/scoring**', (route) => route.abort())

  await openLiveOperation(page, activeCode)

  // The REST snapshot still renders the last-known standings for the team...
  await expect(page.getByTestId(`ranking-score-${TEAM_A}`)).toBeVisible({ timeout: 20000 })
  // ...but the operator is told the live channel is down instead of trusting the rows as current.
  await expect(page.getByTestId('ranking-live-paused')).toBeVisible({ timeout: 20000 })
})
