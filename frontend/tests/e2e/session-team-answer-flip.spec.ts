// HU-36A answered-flip e2e: drives a REAL participant answer end-to-end and asserts the operator's
// answered-monitor board flips that team's row live. This is the path session-answered-monitor.spec.ts
// deliberately skips (it stubs the flip in unit tests) because producing a real TeamAnswered needs
// cross-service participant-membership seeding. There is no mobile submit UI yet (DES-84 unstarted), so
// this spec plays the mobile client itself: it POSTs to the same gateway endpoint the app will call —
// POST /api/sessions/{id}/participants/answers — and watches the operator board react over SignalR.
//
// The seeding this needs, beyond the live-start fixture:
//   1. an identity-access users row for participant-1 keyed by its RESOLVED Keycloak sub (global-setup
//      seeds it under the literal 'participant-1'; the gateway/identity-access resolve the actor by sub,
//      same reason op-1 is re-seeded by sub) — so GetParticipantEligibleTeams + the membership guard see
//      an active Participant; and
//   2. a registered_team_memberships row (team = the REFERENCE id, user = that participant) so the
//      ParticipantMembershipAccessAuthorizationProxy authorizes the team.
// The participant JOINS while the session is still Scheduled (self-join freezes once it leaves
// Scheduled/Preparing). The answer is submitted with the REFERENCE team id: LiveSession.GetTeam resolves
// by runtime-id OR reference-id, and identity-access keys registered_teams by the reference id, so one id
// satisfies both. The accepted answer is keyed internally by the RUNTIME team id, which is what the board
// row is keyed by — so the correct row flips.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'

// team-1 in global-setup's registered_teams seed (Gilded Owls / OWLS). Its id doubles as the reference id.
const TEAM_1_REFERENCE = 'a0000000-0000-0000-0000-000000000001'
const TEAMS = [
  TEAM_1_REFERENCE,
  'a0000000-0000-0000-0000-000000000002',
  'a0000000-0000-0000-0000-000000000003',
]

function sql(db: string, query: string): string {
  return execSync(`docker exec ${DB} psql -U postgres -d ${db} -t -A -c "${query.replace(/"/g, '\\"')}"`)
    .toString().trim()
}
async function token(username: string, password: string): Promise<string> {
  const res = await fetch(`${KC}/realms/umbral/protocol/openid-connect/token`, {
    method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: `grant_type=password&client_id=umbral-web&username=${username}&password=${password}`,
  })
  return (await res.json()).access_token as string
}
function subOf(jwt: string): string {
  return JSON.parse(Buffer.from(jwt.split('.')[1], 'base64').toString()).sub
}
async function api(method: string, path: string, tok: string, body?: unknown): Promise<Response> {
  return fetch(`${GW}${path}`, {
    method, headers: { Authorization: `Bearer ${tok}`, 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  })
}

let sessionCode = ''
let liveSessionId = ''
let team1RuntimeId = ''

test.describe.configure({ mode: 'serial' })
test.setTimeout(120000)

test.beforeAll(async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  // Sub-keyed admin identity for the gateway-JWT assign path (mirrors the other HU-36A specs).
  const adminSub = subOf(admin)
  sql('identity_access', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)
  const opId = Number(sql('identity_access', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))
  const missionId = Number(sql('mission_design',
    `SELECT "Id" FROM "Missions" WHERE "Name"='E2E Seed Mission' AND "IsActive"=true AND "ActivationState"='Ready' ORDER BY "Id" DESC LIMIT 1`))

  // Participant identity keyed by its resolved Keycloak sub, plus a team-1 membership. The old sub-keyed
  // row (and its membership, via the FK cascade) is cleared first; user_id is resolved AFTER the insert
  // because the fresh row gets a new identity Id.
  const participantSub = subOf(await token('participant-1', 'participant123'))
  sql('identity_access', `
    DELETE FROM users WHERE "Email"='participant-1@umbral.local';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${participantSub}','Participant One','participant-1@umbral.local','Participant',true,NOW(),NOW());
    INSERT INTO registered_teams (id, display_name, team_code, is_active, created_at, updated_at)
    VALUES ('${TEAM_1_REFERENCE}','Gilded Owls','OWLS',true,NOW(),NOW())
    ON CONFLICT (id) DO UPDATE SET is_active=true, updated_at=NOW();
  `)
  const participantUserId = Number(sql('identity_access',
    `SELECT "Id" FROM users WHERE "Email"='participant-1@umbral.local'`))
  sql('identity_access', `
    INSERT INTO registered_team_memberships (id, team_id, user_id)
    VALUES (gen_random_uuid(), '${TEAM_1_REFERENCE}', ${participantUserId});
  `)

  const created = await (await api('POST', '/api/sessions', admin, {
    missionId, title: 'Team Answer Flip', maximumTimeMinutes: 60, scheduledAt: '2026-07-05T10:00:00Z',
  })).json()
  liveSessionId = created.liveSessionId as string
  sessionCode = created.sessionCode as string

  await api('PATCH', `/api/sessions/${liveSessionId}/operator-assignment`, admin, { operatorUserId: opId })
  for (const t of TEAMS) await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: t })

  // Resolve the runtime id of team-1 (the board keys rows by it) and self-join it as the participant
  // while the session is still Scheduled — self-join is closed once it leaves Scheduled/Preparing.
  const teams = await (await api('GET', `/api/sessions/${liveSessionId}/teams`, op)).json()
  team1RuntimeId = teams.teams.find((t: { referenceTeamId: string }) => t.referenceTeamId === TEAM_1_REFERENCE)
    .runtimeTeamId as string
  const participant = await token('participant-1', 'participant123')
  const join = await api('POST', `/api/sessions/by-code/${sessionCode}/teams/${team1RuntimeId}/join`, participant)
  expect(join.status, 'participant self-join should succeed').toBe(200)

  // Stop at Preparing — the UI performs Start (Preparing -> Active), reproducing the operator's real flow.
  await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })
})

test('a real participant answer flips that team on the operator answered-monitor board', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.getByTestId('nav-sessions').click()
  const card = page.locator('[data-testid="assigned-session-button"]', { hasText: sessionCode })
  await expect(card).toBeVisible({ timeout: 15000 })
  await card.click()
  await page.getByRole('button', { name: 'Open live operation' }).click()

  // Preparing -> Active (Start), then wait out the pre-game until the first trivia question is active.
  await page.locator('[data-testid="session-action-Active"]').click()
  await expect(page.locator('[data-testid="trivia-round-panel"][data-phase="question-active"]')).toBeVisible({ timeout: 30000 })

  // Baseline: the board renders the roster and team-1 has not answered yet.
  const row = page.locator(`[data-testid="team-answer-status-${team1RuntimeId}"]`)
  const count = page.locator('[data-testid="answered-monitor-count"]')
  await expect(row).toBeVisible()
  await expect(row).toHaveAttribute('data-answered', 'false')
  await expect(count).toContainText('0 / 3 answered')

  // Read the active-question identity the submit needs from the operator monitor snapshot (SubstageSnapshotId
  // + QuestionSequenceOrder are not on the timer snapshot). Option orders are 1-based; the first is valid.
  const op = await token('op-1', 'operator123')
  const monitor = await (await api('GET', `/api/sessions/${liveSessionId}/answered-monitor`, op)).json()

  // Submit as the participant — the fake mobile client. Reference team id satisfies both the domain team
  // resolution and the identity-access membership guard. Token is threaded but currently unused (join-token
  // issuance moved to session-operations, issue #87), so null is accepted.
  const participant = await token('participant-1', 'participant123')
  const submit = await api('POST', `/api/sessions/${liveSessionId}/participants/answers`, participant, {
    teamId: TEAM_1_REFERENCE,
    triviaSubstageSnapshotId: monitor.substageSnapshotId,
    questionSequenceOrder: monitor.questionSequenceOrder,
    selectedOptionSequenceOrder: 1,
    token: null,
  })
  expect(submit.status, await submit.text()).toBe(200)

  // The accepted answer rides the operator-only TeamAnswered SignalR event: team-1's row flips live and
  // the count advances — without a reload.
  await expect(row).toHaveAttribute('data-answered', 'true', { timeout: 15000 })
  await expect(count).toContainText('1 / 3 answered')

  // No-leak still holds after the flip: the pre-close board never renders the chosen option/correctness/points.
  const panelText = (await page.locator('[data-testid="answered-monitor-panel"]').textContent()) ?? ''
  expect(panelText).not.toMatch(/correct|incorrect|points|score/i)
})
