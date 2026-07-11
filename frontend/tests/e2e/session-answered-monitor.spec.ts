// HU-36A live-behavior e2e: the operator's answered/not-answered trivia monitor board.
// Drives a real create -> assign -> team -> Active session through the gateway, then asserts the
// operator hero mounts the board over the REAL snapshot endpoint: the active question's roster renders
// with every team not-answered-yet (derived by roster enumeration), and the pre-close board leaks NO
// chosen option / correctness / points.
//
// The live "answered" flip (TeamAnswered SignalR → data-answered="true") is covered deterministically by
// the unit tests (session-state-client TeamAnswered subscription + AnsweredMonitorPanel render). It is not
// re-driven here because producing a REAL TeamAnswered requires an accepted participant answer, which
// depends on cross-service participant-membership seeding that the backend's own integration tests stub
// (FakeParticipantMembershipAccessClient) — reproducing it against the live stack would be flaky.
//
// beforeAll mirrors session-operator-timer.spec.ts: it adds a sub-keyed admin identity row so the
// gateway-JWT actor resolution succeeds on operator-assignment, and resolves the eligible seed mission
// id from mission_design (a persistent dev DB does not keep it at id 1).
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'
const TEAM = 'a0000000-0000-0000-0000-000000000001' // Gilded Owls (global-setup seed)

function sql(db: string, query: string): string {
  return execSync(`docker exec ${DB} psql -U postgres -d ${db} -t -A -c "${query.replace(/"/g, '\\"')}"`)
    .toString()
    .trim()
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

let sessionCode = ''
let runtimeTeamId = ''

test.describe.configure({ mode: 'serial' })
test.setTimeout(60000)

test.beforeAll(async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  // Sub-keyed admin identity for the gateway-JWT assign path (see file header). Additive; only clears a
  // stale sub-keyed row from a prior run of THIS spec (the sub is a UUID, never the literal 'admin-1').
  const adminSub = subOf(admin)
  sql('identity_access', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)
  const opId = Number(sql('identity_access', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))
  // global-setup seeds "E2E Seed Mission" active + runtime-ready; its id drifts in a persistent DB.
  const missionId = Number(
    sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='E2E Seed Mission' AND "IsActive"=true AND "ActivationState"='Ready' ORDER BY "Id" DESC LIMIT 1`),
  )

  const created = await (await api('POST', '/api/sessions', admin, {
    missionId,
    title: 'Answered Monitor E2E',
    maximumTimeMinutes: 60,
    scheduledAt: '2026-07-05T10:00:00Z',
  })).json()
  const liveSessionId = created.liveSessionId as string
  sessionCode = created.sessionCode as string

  await api('PATCH', `/api/sessions/${liveSessionId}/operator-assignment`, admin, { operatorUserId: opId })
  await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM })

  // The run-time team id keys the board's rows (and matches the TeamAnswered event's teamId).
  const teams = await (await api('GET', `/api/sessions/${liveSessionId}/teams`, op)).json()
  runtimeTeamId = teams.teams[0].runtimeTeamId as string

  await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })
  await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Active' })
  // Wait out the pre-game countdown until the first trivia question is active.
  for (let i = 0; i < 12; i++) {
    const snap = await (await api('GET', `/api/sessions/${liveSessionId}/timer`, op)).json()
    if (snap.activeQuestion) break
    await new Promise((r) => setTimeout(r, 1200))
  }
})

async function openMonitor(page: import('@playwright/test').Page) {
  await page.goto('/dashboard')
  await page.getByTestId('nav-sessions').click()
  const card = page.locator('[data-testid="assigned-session-button"]', { hasText: sessionCode })
  await expect(card).toBeVisible({ timeout: 15000 })
  await card.click()
  await page.getByRole('button', { name: 'Open live operation' }).click()
  await expect(page.locator('[data-testid="answered-monitor-panel"]')).toBeVisible({ timeout: 15000 })
}

test('board renders the active-question roster with every team not-answered and leaks no option/correctness/points', async ({ operatorPage: page }) => {
  await openMonitor(page)

  // Active-question identity is exposed as the sequence order only — never a prompt/option.
  await expect(page.locator('[data-testid="answered-monitor-active-question"]')).toContainText('Question')

  const row = page.locator(`[data-testid="team-answer-status-${runtimeTeamId}"]`)
  await expect(row).toBeVisible()
  await expect(row).toHaveAttribute('data-answered', 'false')
  await expect(page.locator('[data-testid="answered-monitor-count"]')).toContainText('answered')

  // No-leak: the pre-close board must not render the chosen option, correctness, or points.
  const panelText = (await page.locator('[data-testid="answered-monitor-panel"]').textContent()) ?? ''
  expect(panelText).not.toMatch(/correct|incorrect|points|score/i)
})
