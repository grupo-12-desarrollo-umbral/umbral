// HU-38 live-behavior e2e: the operator applies a justified penalty from the dashboard.
// Drives a real session create -> assign -> team -> Active through the gateway, seeds a 350-point
// grant baseline into scoring, then clicks through the real PenaltyPanel (select team + reason +
// Apply) so the whole chain runs: server action -> gateway POST -> ApplyPenalty -> ScoreEntry(Penalty)
// -> ScoreEntryRegistered (RabbitMQ) -> ranking recalculation. Asserts the UI success note (−100 pts)
// AND the DB recalc (350 -> 250, calculation_version bumps, team name preserved).
//
// beforeAll ADDS a second admin row keyed by admin-1's Keycloak sub so the gateway-JWT actor
// resolution on operator-assignment succeeds (mirrors session-operator-panel.spec.ts). Assigning
// through `PATCH .../operator-assignment` publishes LiveSessionOperatorAssignedIntegrationEvent
// (session-ops outbox, committed 34c203e), which scoring's LiveSessionOperatorAssignedConsumer
// projects into session_operator_assignments — beforeAll waits for that row rather than hand-patching
// it. Without the projection ScoringSessionAuthorizationProxy 403s and the panel shows "not authorized".
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'
const TEAM_A = 'a0000000-0000-0000-0000-000000000001' // Gilded Owls (global-setup)
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

// Seeds a 350-point grant baseline for TEAM_A directly into scoring_monitoring (two grants + a v1
// ranking snapshot), mirroring hu-25b-ranking-manual-seed. The penalty subtracts a flat 100 from it,
// so the recalculated total must land on exactly 250 — a value that is meaningful only against a
// positive baseline the fresh session otherwise lacks.
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

  // Sub-keyed admin row so operator-assignment (gateway->JWT) resolves the actor.
  const adminSub = subOf(admin)
  sql('users', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)
  const opId = Number(sql('users', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))

  // Resolve the seeded mission by NAME, not a literal id: seed-all.sh reseeds push mission ids up
  // across runs, so a hardcoded `missionId: 1` 404s on a reused volume. global-setup guarantees the
  // 'E2E Seed Mission' row exists and is Ready (see tests/setup/global-setup.ts).
  const missionId = Number(sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='E2E Seed Mission' ORDER BY "Id" DESC LIMIT 1`))
  expect(missionId).toBeGreaterThan(0)

  const createRes = await api('POST', '/api/sessions', admin, {
    missionId,
    title: 'Operator Penalty E2E',
    maximumTimeMinutes: 60,
    scheduledAt: '2026-07-05T10:00:00Z',
  })
  // Fail loudly here rather than let an unchecked non-2xx leave liveSessionId undefined and surface
  // later as a cryptic 'invalid input syntax for type uuid: "undefined"' in the projection insert.
  expect(createRes.ok, `session create failed: ${createRes.status}`).toBeTruthy()
  const created = await createRes.json()
  lsid = created.liveSessionId as string
  activeCode = created.sessionCode as string
  expect(lsid, 'liveSessionId missing from create response').toBeTruthy()

  await api('PATCH', `/api/sessions/${lsid}/operator-assignment`, admin, { operatorUserId: opId })
  await api('POST', `/api/sessions/${lsid}/teams`, op, { referenceTeamId: TEAM_A })
  await api('PATCH', `/api/sessions/${lsid}/state`, op, { targetState: 'Preparing' })
  await api('PATCH', `/api/sessions/${lsid}/state`, op, { targetState: 'Active' })

  // The assignment above publishes the operator-assigned integration event; ScoringSessionAuthorizationProxy
  // compares assigned_operator_user_id against the JWT sub, so wait for scoring's consumer to project op-1's
  // sub (RabbitMQ hop, ~1-3s) instead of hand-patching. If this times out, the running session-ops / scoring
  // container predates the publisher wiring — `make rewire` both and retry.
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
  await page.getByRole('button', { name: 'Abrir operación en vivo' }).click()
  await expect(page.locator('[data-testid="operator-session-panel"]')).toBeVisible({ timeout: 15000 })
}

test('operator applies a justified penalty: UI confirms −100 and ranking recalculates 350 -> 250', async ({ operatorPage: page }) => {
  await openLiveOperation(page, activeCode)

  // The panel only exposes its form once the session is Active (seeded so above) and the operator
  // snapshot has surfaced teamProgress over SignalR — wait for the team select, not just the panel.
  const teamSelect = page.getByTestId('penalty-team-select')
  await expect(teamSelect).toBeVisible({ timeout: 20000 })

  // AC: apply a penalty WITH a justification against Gilded Owls. Select by visible LABEL: the
  // option value is the operator panel's runtime team id (definitions.ts OperatorTeamProgressDto.teamId),
  // not the referenceTeamId, so keying the selection on the GUID is neither possible nor the point.
  await teamSelect.selectOption({ label: TEAM_A_NAME })
  await page.getByTestId('penalty-reason-input').fill('Used a phone during a no-device substage.')
  await page.getByTestId('penalty-submit').click()

  // UI: success note confirms the append-only entry magnitude (flat 100), not a team total.
  await expect(page.getByTestId('penalty-success')).toContainText('100 pts', { timeout: 15000 })
  await expect(page.getByTestId('penalty-error')).toHaveCount(0)

  // Backend: penalty persisted as a Penalty ScoreEntry carrying the reason.
  await expect
    .poll(() =>
      sql('scoring_monitoring',
        `SELECT entry_type FROM score_entries WHERE live_session_id='${lsid}' AND entry_type='Penalty' LIMIT 1`),
    { timeout: 20000 })
    .toBe('Penalty')
  expect(
    sql('scoring_monitoring',
      `SELECT penalty_reason FROM penalties p JOIN score_entries s ON s.id=p.score_entry_id WHERE s.live_session_id='${lsid}' LIMIT 1`),
  ).toContain('Used a phone')

  // Ranking recalculated: penalty SUBTRACTED (350 -> 250), version bumped past the seeded v1, and the
  // real team name preserved (not overwritten by the penalty entry's blank display name).
  await expect
    .poll(() =>
      sql('scoring_monitoring',
        `SELECT rr.total_score FROM rankings r JOIN ranking_rows rr ON rr.ranking_id=r.id
         WHERE r.live_session_id='${lsid}' AND rr.team_id='${TEAM_A}'
         ORDER BY r.calculation_version DESC LIMIT 1`),
    { timeout: 20000 })
    .toBe('250')
  expect(
    Number(sql('scoring_monitoring', `SELECT MAX(calculation_version) FROM rankings WHERE live_session_id='${lsid}'`)),
  ).toBeGreaterThanOrEqual(2)
  expect(
    sql('scoring_monitoring',
      `SELECT rr.team_display_name FROM rankings r JOIN ranking_rows rr ON rr.ranking_id=r.id
       WHERE r.live_session_id='${lsid}' AND rr.team_id='${TEAM_A}'
       ORDER BY r.calculation_version DESC LIMIT 1`),
  ).toBe(TEAM_A_NAME)
})
