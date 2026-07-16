// HU-24B live-behavior e2e: the QR half of the operator's EvidenceSubmissionsPanel (AC #2), which
// session-operator-evidence.spec.ts deliberately leaves uncovered — it drives a trivia answer, so it
// exercises the transport and the merge but never the treasure-hunt origin or the rejection copy.
//
// No camera is needed, which is why this is a spec rather than the manual pass the plan's Verification
// section (§1-5) assumed: target-scanner.tsx does no HTTP of its own, it hands the decoded barcode string
// to use-target-scan.ts, which POSTs it as a plain `scannedValue`. A real participant token against the
// gateway reproduces a scan exactly — nothing camera-specific is in the payload.
//
// The four tests pin both origin branches and all three rejection reasons. The prefix is a
// RESOLUTION OUTCOME, not a transport marker (TreasureEvidenceSubmission.DescribeOrigin): a value that
// matches a snapshot yields `target:{guid}`, an unmatched one falls back to `qr:{rawValue}`. So the
// already-resolved rejection still carries `target:` — only the unknown-code case produces `qr:` at all,
// and without test 2 that branch stays untested.
//
// Intake is unconditional — the row is always written and EvidenceSubmissionRegistered always fires; only
// resolution decides Accepted vs Rejected. So a rejected scan is still a live row on the panel, and the
// operator sees the reason. On the wire a rejection is 422 RFC-7807 with the message in `detail`, not an
// error; all three reasons collapse to 422 and differ only by that text.
//
// Setup mirrors session-operator-evidence.spec.ts (which solved the cross-service participant-membership
// seeding a real submission needs) but swaps global-setup's trivia mission for a TreasureHunt-first one.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'
import {
  authorTreasureHuntMission,
  TREASURE_HUNT_OUTSIDE_QR_CODE,
  TREASURE_HUNT_QR_CODES,
} from '../lib/treasure-hunt-mission'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'

// Its own mission name, not the HU-23 seed's: the authoring helper rebuilds by name, and Playwright runs
// spec files in parallel.
const MISSION_NAME = 'Treasure Hunt Evidence E2E'

// team-1 in global-setup's registered_teams seed (Gilded Owls / OWLS). Its id doubles as the reference id.
const TEAM_1_REFERENCE = 'a0000000-0000-0000-0000-000000000001'

// The rejection copy under test, verbatim from TargetResolutionRejectionReason.ToMessage(). The panel
// renders the backend's message rather than mapping it to copy of its own, so these strings are the
// contract — if they drift, the operator's explanation drifts with them.
const UNKNOWN_CODE_MESSAGE = 'The scanned value does not resolve to a target.'
const ALREADY_RESOLVED_MESSAGE = 'The target has already been resolved by this team.'
const OUTSIDE_SUBSTAGE_MESSAGE = 'The resolved target does not belong to the active treasure-hunt substage.'

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

// The fake mobile scanner. `teamId` is the REFERENCE team id, not the runtime one — team-space.tsx passes
// referenceTeamId down to the scanner. Goes through the gateway because the service authenticates on
// X-User-* trusted headers that only the gateway may mint (TrustedHeadersTransform strips client copies).
// Returns the parsed body alongside the status: a Response body can only be read once, and the status
// assertions below want the raw text as their failure message.
async function scan(
  scannedValue: string,
): Promise<{ status: number; body: string; json: Record<string, unknown> }> {
  const participant = await token('participant-1', 'participant123')
  const response = await api('POST', `/api/sessions/${liveSessionId}/participants/target-scans`, participant, {
    teamId: TEAM_1_REFERENCE,
    scannedValue,
    token: null,
  })
  const body = await response.text()
  return { status: response.status, body, json: JSON.parse(body) }
}

let sessionCode = ''
let liveSessionId = ''
let team1RuntimeId = ''

test.describe.configure({ mode: 'serial' })
test.setTimeout(120000)

test.beforeAll(async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  // The second substage exists solely to own an out-of-substage target for test 4; Start activates only the
  // first, so the three codes above stay the active set and the existing tests are unaffected.
  authorTreasureHuntMission(MISSION_NAME, { withSecondSubstage: true })

  // Sub-keyed admin identity for the gateway-JWT assign path. Upsert rather than DELETE+INSERT: this
  // spec runs in parallel with session-operator-evidence.spec.ts, which seeds the same admin sub, and a
  // DELETE there would momentarily strip the row out from under this one. ExternalIdentityId is the only
  // unique index on users, and both specs write identical content, so the upsert converges either way.
  const adminSub = subOf(admin)
  sql('identity_access', `
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW())
    ON CONFLICT ("ExternalIdentityId") DO UPDATE SET "IsActive"=true, "LastModified"=NOW();
  `)
  const opId = Number(sql('identity_access', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))
  const missionId = Number(sql('mission_design',
    `SELECT "Id" FROM "Missions" WHERE "Name"='${MISSION_NAME}' AND "IsActive"=true AND "ActivationState"='Ready' ORDER BY "Id" DESC LIMIT 1`))
  expect(missionId, 'treasure-hunt mission should have been authored').toBeGreaterThan(0)

  // No participant seeding here: global-setup's seedParticipantIdentity already inserts participant-1 keyed
  // by the resolved sub AND the Gilded Owls registered_team_memberships row, with the same values this spec
  // would write. Re-seeding it per-spec is what made this file collide with session-operator-evidence.spec.ts
  // under Playwright's fullyParallel (duplicate key on IX_registered_team_memberships_team_id_user_id).

  const created = await (await api('POST', '/api/sessions', admin, {
    missionId, title: 'Operator Evidence QR E2E', maximumTimeMinutes: 60, scheduledAt: '2026-07-05T10:00:00Z',
  })).json()
  liveSessionId = created.liveSessionId as string
  sessionCode = created.sessionCode as string

  await api('PATCH', `/api/sessions/${liveSessionId}/operator-assignment`, admin, { operatorUserId: opId })
  // Only team-1 is attached: the already-resolved rejection is scoped per team, so a single team keeps
  // test 3's duplicate unambiguous.
  await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM_1_REFERENCE })

  const teams = await (await api('GET', `/api/sessions/${liveSessionId}/teams`, op)).json()
  team1RuntimeId = teams.teams.find((t: { referenceTeamId: string }) => t.referenceTeamId === TEAM_1_REFERENCE)
    .runtimeTeamId as string
  const participant = await token('participant-1', 'participant123')
  const join = await api('POST', `/api/sessions/by-code/${sessionCode}/teams/${team1RuntimeId}/join`, participant)
  expect(join.status, 'participant self-join should succeed').toBe(200)

  // Stop at Preparing — test 1 performs Start through the UI, reproducing the operator's real flow.
  await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })
})

async function openPanel(page: import('@playwright/test').Page) {
  await page.goto('/dashboard')
  await page.getByTestId('nav-sessions').click()
  const card = page.locator('[data-testid="assigned-session-button"]', { hasText: sessionCode })
  await expect(card).toBeVisible({ timeout: 15000 })
  await card.click()
  await page.getByRole('button', { name: 'Open live operation' }).click()
  await expect(page.locator('[data-testid="operator-session-panel"]')).toBeVisible({ timeout: 15000 })
  await expect(page.getByTestId('evidence-panel')).toBeVisible({ timeout: 15000 })
  // Live channel healthy once the session hub connects + joins the operator group. Asserted before every
  // scan: without it a missing row would be ambiguous between "no push" and "hub never connected".
  await expect(page.getByTestId('evidence-live-paused')).toHaveCount(0, { timeout: 20000 })
}

// Unlike trivia there is no pre-game countdown gating a treasure-hunt substage: EnterActiveSessionState
// sets ActiveSubstageId to the first substage synchronously, so the scan is legal the instant the Start
// PATCH commits. This wait is about the operator panel being fresh, not the backend being ready.
// `0/0 targets` is the null-activeSubstage render, hence the [1-9] — it must not satisfy the wait.
async function start(page: import('@playwright/test').Page) {
  await page.locator('[data-testid="session-action-Active"]').click()
  await expect(page.locator('[data-testid="panel-session-state"]')).toHaveText('Active', { timeout: 15000 })
  await expect(page.locator(`[data-testid="team-progress-targets-${team1RuntimeId}"]`))
    .toHaveText(/^\d+\/[1-9]\d* targets$/, { timeout: 15000 })
}

test('a QR scan of a seeded target lands on the operator evidence panel live, with a target: origin', async ({ operatorPage: page }) => {
  await openPanel(page)
  await expect(page.getByTestId('evidence-empty')).toBeVisible()
  await start(page)

  const scanned = await scan(TREASURE_HUNT_QR_CODES[0])
  expect(scanned.status, scanned.body).toBe(200)
  expect(scanned.json.isResolved, 'a seeded in-substage target should resolve').toBe(true)

  // A row appears on the SAME page instance (no page.reload()): the operator's group join delivered
  // EvidenceSubmissionRegistered and the reducer inserted it.
  const row = page.locator('[data-testid^="evidence-row-"]')
  await expect(row).toHaveCount(1, { timeout: 20000 })
  await expect(row).toContainText('Gilded Owls')
  await expect(row).toContainText('QR scan')
  // The origin the trivia-driven spec cannot reach: a matched scan is identified by the target it
  // resolved to, not by the raw scanned text.
  await expect(row).toContainText(`target:${scanned.json.targetSnapshotId}`)
  // ...and the Accepted fact (raised by the same write) resolves it — the flip AC #2 asks for.
  await expect(row).toContainText('Accepted', { timeout: 20000 })
})

test('an unmatched scanned value is rejected live, echoing the raw value as a qr: origin', async ({ operatorPage: page }) => {
  await openPanel(page)
  // The session is already Active from test 1 (serial); the REST snapshot carries its row.
  await expect(page.locator('[data-testid^="evidence-row-"]')).toHaveCount(1, { timeout: 20000 })

  const scanned = await scan('NOT-A-REAL-CODE')
  // Rejected, but not an error: 422 RFC-7807 carrying the display message.
  expect(scanned.status, scanned.body).toBe(422)
  expect(scanned.json.detail).toBe(UNKNOWN_CODE_MESSAGE)

  // Rejected or not, the submission is real evidence and reaches the operator live.
  const row = page.locator('[data-testid^="evidence-row-"]', { hasText: UNKNOWN_CODE_MESSAGE })
  await expect(row).toHaveCount(1, { timeout: 20000 })
  // Nothing matched, so the origin falls back to the scanned text itself — the operator can see WHAT was
  // scanned, which is the whole point of the fallback.
  await expect(row).toContainText('qr:NOT-A-REAL-CODE')
  await expect(row).toContainText('Rejected')
  await expect(page.locator('[data-testid^="evidence-row-"]')).toHaveCount(2)
})

test('rescanning a target the team already resolved is rejected live with the already-resolved reason', async ({ operatorPage: page }) => {
  await openPanel(page)
  await expect(page.locator('[data-testid^="evidence-row-"]')).toHaveCount(2, { timeout: 20000 })

  // Same code test 1 accepted. Duplicate detection counts only prior ACCEPTED rows for this team+target,
  // so this is a duplicate while test 2's rejected row is not.
  const scanned = await scan(TREASURE_HUNT_QR_CODES[0])
  expect(scanned.status, scanned.body).toBe(422)
  expect(scanned.json.detail).toBe(ALREADY_RESOLVED_MESSAGE)

  const row = page.locator('[data-testid^="evidence-row-"]', { hasText: ALREADY_RESOLVED_MESSAGE })
  await expect(row).toHaveCount(1, { timeout: 20000 })
  await expect(row).toContainText('Rejected')
  // A rejected duplicate still resolved to a target, so it keeps the target: origin — the prefix tracks
  // what the value matched, not whether the scan was allowed.
  await expect(row).toContainText('target:')
  await expect(page.locator('[data-testid^="evidence-row-"]')).toHaveCount(3)
  // The accepted row is untouched: "once terminal always terminal" — the duplicate must not revert it.
  await expect(page.locator('[data-testid^="evidence-row-"]', { hasText: 'Accepted' })).toHaveCount(1)
})

test('scanning a target from a non-active substage is rejected live with the outside-substage reason', async ({ operatorPage: page }) => {
  await openPanel(page)
  await expect(page.locator('[data-testid^="evidence-row-"]')).toHaveCount(3, { timeout: 20000 })

  // DetermineTargetResolutionRejection also rejects an inactive target with this same reason; the seed keeps
  // this one active so the substage mismatch is the only thing under test.
  const scanned = await scan(TREASURE_HUNT_OUTSIDE_QR_CODE)
  expect(scanned.status, scanned.body).toBe(422)
  expect(scanned.json.detail).toBe(OUTSIDE_SUBSTAGE_MESSAGE)

  const row = page.locator('[data-testid^="evidence-row-"]', { hasText: OUTSIDE_SUBSTAGE_MESSAGE })
  await expect(row).toHaveCount(1, { timeout: 20000 })
  await expect(row).toContainText('Rejected')
  // The scan matched a real snapshot before the substage check rejected it, so the origin is target:, not
  // qr: — the prefix tracks what the value matched, not whether the scan was allowed.
  await expect(row).toContainText('target:')
  await expect(page.locator('[data-testid^="evidence-row-"]')).toHaveCount(4)
})
