// HU-36A live-start regression: the operator opens a session while it is Preparing, then clicks Start.
// Guards two bugs seen on that path (a fresh session, not a pre-driven one):
//   1) the trivia-round countdown froze at 0 for the FIRST question while the authoritative "Question
//      timer" ticked — a Start-transition placeholder snapshot clobbered the live SignalR countdown; and
//   2) the answered board stayed on "Waiting for the team roster…" because the roster was only fetched
//      at mount (Preparing → empty) and never refetched when the question activated.
// beforeAll mirrors session-answered-monitor.spec.ts but stops at Preparing so the UI performs Start.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'
const TEAMS = [
  'a0000000-0000-0000-0000-000000000001',
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

test.describe.configure({ mode: 'serial' })
test.setTimeout(90000)

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
  const missionId = Number(sql('mission_design',
    `SELECT "Id" FROM "Missions" WHERE "Name"='E2E Seed Mission' AND "IsActive"=true AND "ActivationState"='Ready' ORDER BY "Id" DESC LIMIT 1`))

  const created = await (await api('POST', '/api/sessions', admin, {
    missionId, title: 'Live Start Regression', maximumTimeMinutes: 60, scheduledAt: '2026-07-05T10:00:00Z',
  })).json()
  const liveSessionId = created.liveSessionId as string
  sessionCode = created.sessionCode as string
  await api('PATCH', `/api/sessions/${liveSessionId}/operator-assignment`, admin, { operatorUserId: opId })
  for (const t of TEAMS) await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: t })
  // Stop at Preparing — the UI performs Preparing -> Active, reproducing the operator's real flow.
  await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })
})

// "00:29" (mm:ss, top panel) or "29s" (trivia panel) -> 29.
function secondsOf(text: string): number {
  const mmss = text.match(/(\d+):(\d{2})/)
  if (mmss) return Number(mmss[1]) * 60 + Number(mmss[2])
  return Number(text.replace(/[^\d]/g, ''))
}

test('starting a session keeps both countdowns in sync and renders the answered roster', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.getByTestId('nav-sessions').click()
  const card = page.locator('[data-testid="assigned-session-button"]', { hasText: sessionCode })
  await expect(card).toBeVisible({ timeout: 15000 })
  await card.click()
  await page.getByRole('button', { name: 'Open live operation' }).click()

  // Preparing -> Active (Start). The UI then runs the ~5s pre-game before the first question activates.
  await page.locator('[data-testid="session-action-Active"]').click()

  // Wait out the pre-game until the first trivia question is active.
  const trivia = page.locator('[data-testid="trivia-round-panel"][data-phase="question-active"]')
  await expect(trivia).toBeVisible({ timeout: 30000 })

  const timeLeft = page.locator('[data-testid="trivia-round-time-left"]')
  const topTimer = page.locator('[data-testid="timer-remaining"]')

  // Bug 1 regression: the first question's trivia countdown must NOT be frozen at 0.
  const triviaStart = secondsOf((await timeLeft.textContent()) ?? '')
  const topStart = secondsOf((await topTimer.textContent()) ?? '')
  expect(triviaStart).toBeGreaterThan(0)
  expect(topStart).toBeGreaterThan(0)
  // The two clocks are the same question window — they must agree within a small skew, not diverge.
  expect(Math.abs(triviaStart - topStart)).toBeLessThanOrEqual(3)

  // Both must actually tick down (not sit frozen) — sample again after a few seconds.
  await page.waitForTimeout(4000)
  const triviaLater = secondsOf((await timeLeft.textContent()) ?? '')
  const topLater = secondsOf((await topTimer.textContent()) ?? '')
  expect(triviaLater).toBeLessThan(triviaStart)
  expect(topLater).toBeLessThan(topStart)
  expect(Math.abs(triviaLater - topLater)).toBeLessThanOrEqual(3)

  // Bug 2 regression: the answered board renders the associated roster (not "Waiting for the roster…").
  await expect(page.locator('[data-testid="answered-monitor-active-question"]')).toContainText('Question')
  await expect(page.locator('[data-testid^="team-answer-status-"]').first()).toBeVisible()
  await expect(page.locator('[data-testid="answered-monitor-count"]')).toContainText('/ 3 answered')

  // Pause must still freeze BOTH countdowns on the same second (the handoff pause-freeze fix, which the
  // non-live-snapshot refactor runs through): a paused question carries remaining time, so it is hydrated
  // frozen rather than skipped. Sample twice and assert neither clock moved and they agree.
  await page.locator('[data-testid="session-action-Paused"]').click()
  await expect(page.locator('[data-testid="timer-chip"]')).toHaveText('Paused', { timeout: 15000 })
  const triviaPaused = secondsOf((await timeLeft.textContent()) ?? '')
  const topPaused = secondsOf((await topTimer.textContent()) ?? '')
  await page.waitForTimeout(3000)
  expect(secondsOf((await timeLeft.textContent()) ?? '')).toBe(triviaPaused)
  expect(secondsOf((await topTimer.textContent()) ?? '')).toBe(topPaused)
  expect(Math.abs(triviaPaused - topPaused)).toBeLessThanOrEqual(1)
})
