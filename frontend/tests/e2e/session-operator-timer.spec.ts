// HU-22 live-behavior e2e: the operator's active-substage (question) timer panel.
// Drives a real session create -> assign -> team -> Active through the gateway so the
// panel renders from the real backend snapshot + SignalR, then asserts the re-pointed
// states: active-question countdown, pause freeze, and the no-active-question state (OD-1).
//
// beforeAll ADDS a second admin row keyed by admin-1's Keycloak sub so the gateway-JWT actor
// resolution on operator-assignment succeeds (global-setup seeds admin keyed by the literal
// 'admin-1' for the BFF-direct admin surface; the assign path needs the sub). The insert is
// additive — the literal-'admin-1' row stays untouched — so this file is parallel-safe with
// the BFF-direct admin specs.
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
let scheduledCode = ''

test.describe.configure({ mode: 'serial' })
test.setTimeout(60000)

test.beforeAll(async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  // Actor resolution (/api/users/me) keys by sub; global-setup only seeds admin keyed by the
  // literal 'admin-1'. Add a second row keyed by the sub so the gateway-JWT assign path
  // resolves the actor — additive, keeping the literal-'admin-1' row the BFF-direct admin
  // specs need (Email isn't unique, so both admin rows coexist). Only clear a stale sub-keyed
  // row left by a prior run of this spec; the sub is a UUID, never the literal 'admin-1'.
  const adminSub = subOf(admin)
  sql('identity_access', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)
  const opId = Number(sql('identity_access', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))

  // Resolve the trivia seed mission by name: ids are never 1 and drift across reseeds
  // (see tests/setup/global-setup.ts), so a literal id 404s on session create.
  const missionId = Number(sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='E2E Seed Mission' AND "IsActive"=true AND "ActivationState"='Ready' ORDER BY "Id" DESC LIMIT 1`))
  expect(missionId).toBeGreaterThan(0)

  async function makeSession(title: string, activate: boolean): Promise<string> {
    const created = await (await api('POST', '/api/sessions', admin, {
      missionId,
      title,
      maximumTimeMinutes: 60,
      scheduledAt: '2026-07-05T10:00:00Z',
    })).json()
    const lsid = created.liveSessionId as string
    await api('PATCH', `/api/sessions/${lsid}/operator-assignment`, admin, { operatorUserId: opId })
    await api('POST', `/api/sessions/${lsid}/teams`, op, { referenceTeamId: TEAM })
    if (activate) {
      await api('PATCH', `/api/sessions/${lsid}/state`, op, { targetState: 'Preparing' })
      await api('PATCH', `/api/sessions/${lsid}/state`, op, { targetState: 'Active' })
      // Wait out the pre-game countdown until the first trivia question is active.
      for (let i = 0; i < 12; i++) {
        const snap = await (await api('GET', `/api/sessions/${lsid}/timer`, op)).json()
        if (snap.activeQuestion) break
        await new Promise((r) => setTimeout(r, 1200))
      }
    }
    return created.sessionCode as string
  }

  activeCode = await makeSession('Timer E2E Active', true)
  scheduledCode = await makeSession('Timer E2E Scheduled', false)
})

async function selectSession(page: import('@playwright/test').Page, code: string) {
  await page.goto('/dashboard')
  await page.getByTestId('nav-sessions').click()
  const card = page.locator('[data-testid="assigned-session-button"]', { hasText: code })
  await expect(card).toBeVisible({ timeout: 15000 })
  await card.click()
  // Moving into live operation switches to the overview hero, which mounts the timer panel.
  await page.getByRole('button', { name: 'Open live operation' }).click()
  await expect(page.locator('[data-testid="session-timer-panel"]')).toBeVisible({ timeout: 15000 })
}

test('active trivia question renders the question-timer countdown', async ({ operatorPage: page }) => {
  await selectSession(page, activeCode)
  await expect(page.locator('[data-testid="timer-remaining"]')).toBeVisible()
  await expect(page.locator('[data-testid="timer-remaining"]')).toHaveText(/^\d{1,2}:\d{2}$/)
  await expect(page.locator('[data-testid="timer-chip"]')).toHaveText('Running')
  await expect(page.locator('[data-testid="timer-no-countdown"]')).toHaveCount(0)
})

test('pausing the session freezes the question timer (chip Paused)', async ({ operatorPage: page }) => {
  await selectSession(page, activeCode)
  await page.locator('[data-testid="session-action-Paused"]').click()
  await expect(page.locator('[data-testid="timer-chip"]')).toHaveText('Paused')
  // Frozen remainder is still shown — the panel keeps rendering the question window, not 00:00-reset.
  await expect(page.locator('[data-testid="timer-remaining"]')).toBeVisible()
})

test('no active question renders the no-countdown state (OD-1)', async ({ operatorPage: page }) => {
  await selectSession(page, scheduledCode)
  await expect(page.locator('[data-testid="timer-no-countdown"]')).toBeVisible()
  await expect(page.locator('[data-testid="timer-chip"]')).toHaveText('No question')
  await expect(page.locator('[data-testid="timer-remaining"]')).toHaveCount(0)
})
