// HU-24A live-behavior e2e: the operator's live session panel (all-teams progress rollup).
// Drives a real session create -> assign -> team -> Active through the gateway so the panel renders
// from the real backend snapshot + SignalR, then asserts the state readout + per-team rollup and
// that a session-state transition updates the panel via OperatorSessionPanelUpdated (no manual reload).
//
// beforeAll ADDS a second admin row keyed by admin-1's Keycloak sub so the gateway-JWT actor
// resolution on operator-assignment succeeds (global-setup seeds admin keyed by the literal
// 'admin-1' for the BFF-direct admin surface; the assign path needs the sub). The insert is
// additive — the literal-'admin-1' row stays untouched — so this file is parallel-safe with
// the BFF-direct admin specs. Non-owner 403 is covered at the unit/action layer (a second
// sub-keyed operator against the live stack is flaky), mirroring the timer/answered specs.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'
const TEAM = 'a0000000-0000-0000-0000-000000000001'

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

let activeCode = ''

test.describe.configure({ mode: 'serial' })
test.setTimeout(60000)

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

  // Resolve the seeded mission by NAME, not a literal id: seed-all.sh reseeds push mission ids up
  // across runs, so a hardcoded `missionId: 1` 404s on a reused volume. global-setup guarantees the
  // 'E2E Seed Mission' row exists and is Ready (see tests/setup/global-setup.ts).
  const missionId = Number(sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='E2E Seed Mission' ORDER BY "Id" DESC LIMIT 1`))

  const created = await (await api('POST', '/api/sessions', admin, {
    missionId,
    title: 'Operator Panel E2E Active',
    maximumTimeMinutes: 60,
    scheduledAt: '2026-07-05T10:00:00Z',
  })).json()
  const lsid = created.liveSessionId as string
  await api('PATCH', `/api/sessions/${lsid}/operator-assignment`, admin, { operatorUserId: opId })
  await api('POST', `/api/sessions/${lsid}/teams`, op, { referenceTeamId: TEAM })
  await api('PATCH', `/api/sessions/${lsid}/state`, op, { targetState: 'Preparing' })
  await api('PATCH', `/api/sessions/${lsid}/state`, op, { targetState: 'Active' })
  activeCode = created.sessionCode as string
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

test('operator opens the session panel: state readout + per-team progress rollup render', async ({ operatorPage: page }) => {
  await openLiveOperation(page, activeCode)

  // AC2/AC3: a live session-state readout plus at least one team rollup row (score cell present).
  await expect(page.locator('[data-testid="panel-session-state"]')).toBeVisible()
  await expect(page.locator('[data-testid="panel-session-state"]')).toHaveText(/Active|Paused|Preparing/)
  await expect(page.locator('[data-testid^="team-progress-score-"]').first()).toBeVisible()
  // No "winner" copy leaks into the operator dashboard — there is no winner-declaration feature.
  // NOTE: "ranking" is deliberately NOT asserted absent anymore. HU-24B landed a real RankingPanel
  // (heading "Ranking") on this same dashboard; its live behaviour has its own coverage in
  // session-operator-ranking.spec.ts. "Penalt" is likewise not matched — the HU-38 PenaltyPanel is a
  // legitimate operator control, not a leak.
  await expect(page.getByText(/winner/i)).toHaveCount(0)
})

test('a session-state transition updates the panel state without manual reload', async ({ operatorPage: page }) => {
  await openLiveOperation(page, activeCode)
  await expect(page.locator('[data-testid="panel-session-state"]')).toHaveText(/Active/)

  // AC5: pausing pushes OperatorSessionPanelUpdated (re-projected on SessionStateChangedEvent);
  // the panel state readout reflects Paused with no page reload.
  await page.locator('[data-testid="session-action-Paused"]').click()
  await expect(page.locator('[data-testid="panel-session-state"]')).toHaveText('Paused', { timeout: 15000 })
})
