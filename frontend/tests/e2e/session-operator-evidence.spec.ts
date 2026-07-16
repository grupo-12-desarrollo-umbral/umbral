// HU-24B live-behavior e2e: the operator's live EvidenceSubmissionsPanel.
//   1) A REAL participant submission drives an evidence row onto the panel LIVE, with no manual
//      reload — the EvidenceSubmissionRegistered/Resolved pushes reach the operator-only group on
//      /hubs/sessions and the panel merges them. Before this work the evidence domain events reached
//      RabbitMQ only, so the operator saw nothing until a refetch.
//   2) When the session hub cannot connect, the panel still shows its REST snapshot but honestly flags
//      itself paused rather than presenting a possibly-incomplete feed as live.
//
// Why a trivia answer rather than a QR scan: the codebase treats a trivia answer AS an evidence
// submission, and LiveSession.RegisterTriviaAnswer goes through the same RegisterEvidenceCore that
// raises EvidenceSubmissionRegisteredEvent (then AcceptRegisteredAnswer raises the Accepted fact). So
// this drives BOTH new pushes through the real domain path with no bespoke treasure-hunt fixture. It
// exercises the transport and the merge, NOT the treasure-hunt origin or the rejection copy — those are
// covered by session-operator-evidence-qr.spec.ts, which needs a TreasureHunt-first mission to seed.
//
// The setup mirrors session-team-answer-flip.spec.ts, which already solved the cross-service
// participant-membership seeding a real submission needs; see its header for why each row is required.
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

  // Upsert rather than DELETE+INSERT: session-operator-evidence-qr.spec.ts seeds the same admin sub and
  // runs in parallel, so a DELETE here would momentarily strip the row out from under it. Both specs write
  // identical content and ExternalIdentityId is the only unique index on users.
  const adminSub = subOf(admin)
  sql('identity_access', `
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW())
    ON CONFLICT ("ExternalIdentityId") DO UPDATE SET "IsActive"=true, "LastModified"=NOW();
  `)
  const opId = Number(sql('identity_access', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))
  const missionId = Number(sql('mission_design',
    `SELECT "Id" FROM "Missions" WHERE "Name"='E2E Seed Mission' AND "IsActive"=true AND "ActivationState"='Ready' ORDER BY "Id" DESC LIMIT 1`))

  // No participant seeding here: global-setup's seedParticipantIdentity already inserts participant-1 keyed
  // by the resolved sub AND the Gilded Owls registered_team_memberships row, with the same values this spec
  // used to write. Re-seeding it per-spec collided with session-operator-evidence-qr.spec.ts under
  // Playwright's fullyParallel (duplicate key on IX_registered_team_memberships_team_id_user_id).

  const created = await (await api('POST', '/api/sessions', admin, {
    missionId, title: 'Operator Evidence E2E', maximumTimeMinutes: 60, scheduledAt: '2026-07-05T10:00:00Z',
  })).json()
  liveSessionId = created.liveSessionId as string
  sessionCode = created.sessionCode as string

  await api('PATCH', `/api/sessions/${liveSessionId}/operator-assignment`, admin, { operatorUserId: opId })
  for (const t of TEAMS) await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: t })

  const teams = await (await api('GET', `/api/sessions/${liveSessionId}/teams`, op)).json()
  team1RuntimeId = teams.teams.find((t: { referenceTeamId: string }) => t.referenceTeamId === TEAM_1_REFERENCE)
    .runtimeTeamId as string
  const participant = await token('participant-1', 'participant123')
  const join = await api('POST', `/api/sessions/by-code/${sessionCode}/teams/${team1RuntimeId}/join`, participant)
  expect(join.status, 'participant self-join should succeed').toBe(200)

  // Stop at Preparing — the UI performs Start (Preparing -> Active), reproducing the operator's real flow.
  await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })
})

async function openAndStart(page: import('@playwright/test').Page) {
  await page.goto('/dashboard')
  await page.getByTestId('nav-sessions').click()
  const card = page.locator('[data-testid="assigned-session-button"]', { hasText: sessionCode })
  await expect(card).toBeVisible({ timeout: 15000 })
  await card.click()
  await page.getByRole('button', { name: 'Open live operation' }).click()
  await expect(page.locator('[data-testid="operator-session-panel"]')).toBeVisible({ timeout: 15000 })
}

test('a real participant submission lands on the operator evidence panel live, without a manual reload', async ({ operatorPage: page }) => {
  await openAndStart(page)

  // Baseline: the panel renders and this session has no submissions yet.
  await expect(page.getByTestId('evidence-panel')).toBeVisible({ timeout: 15000 })
  await expect(page.getByTestId('evidence-empty')).toBeVisible()
  // Live channel healthy once the session hub connects + joins the operator group.
  await expect(page.getByTestId('evidence-live-paused')).toHaveCount(0, { timeout: 20000 })

  // Preparing -> Active (Start), then wait out the pre-game until the first trivia question is active.
  await page.locator('[data-testid="session-action-Active"]').click()
  await expect(page.locator('[data-testid="trivia-round-panel"][data-phase="question-active"]')).toBeVisible({ timeout: 30000 })

  // Read the active-question identity the submit needs, then submit as the participant — the fake
  // mobile client, exactly as session-team-answer-flip does.
  const op = await token('op-1', 'operator123')
  const monitor = await (await api('GET', `/api/sessions/${liveSessionId}/answered-monitor`, op)).json()
  const participant = await token('participant-1', 'participant123')
  const submit = await api('POST', `/api/sessions/${liveSessionId}/participants/answers`, participant, {
    teamId: TEAM_1_REFERENCE,
    triviaSubstageSnapshotId: monitor.substageSnapshotId,
    questionSequenceOrder: monitor.questionSequenceOrder,
    selectedOptionSequenceOrder: 1,
    token: null,
  })
  expect(submit.status, await submit.text()).toBe(200)

  // A row appears on the SAME page instance (no page.reload()): the operator's group join delivered
  // EvidenceSubmissionRegistered and the reducer inserted it.
  const row = page.locator('[data-testid^="evidence-row-"]')
  await expect(row).toHaveCount(1, { timeout: 20000 })
  await expect(row).toContainText('Gilded Owls')
  await expect(row).toContainText('Trivia answer')
  // ...and the Accepted fact (raised by the same write) resolves it — the flip AC #2 asks for.
  await expect(row).toContainText('Accepted', { timeout: 20000 })
  // The trivia form's origin grain, proving the registered push's payload survived the merge.
  await expect(row).toContainText(`question:${monitor.questionSequenceOrder}`)
})

test('evidence panel flags itself paused when the session hub cannot connect', async ({ operatorPage: page }) => {
  // Block the browser's session-hub socket (negotiate + websocket) while leaving the REST snapshot —
  // fetched server-side by the operator action, not the browser — intact. Unlike the ranking spec this
  // cuts the whole dashboard's live channel, since evidence rides /hubs/sessions rather than a second hub.
  await page.route('**/hubs/sessions**', (route) => route.abort())

  await openAndStart(page)

  // The REST snapshot still renders the submission the previous test produced...
  await expect(page.locator('[data-testid^="evidence-row-"]')).toHaveCount(1, { timeout: 20000 })
  // ...but the operator is told the live channel is down instead of trusting the feed as complete.
  await expect(page.getByTestId('evidence-live-paused')).toBeVisible({ timeout: 20000 })
})
