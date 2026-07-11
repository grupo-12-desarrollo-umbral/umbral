// HU-36A MANUAL-TEST seed (not a behavior assertion — see session-answered-monitor.spec.ts for that).
// Creates -> assigns -> attaches the "Answered Monitor E2E" session and leaves it in PREPARING, one
// operator "Start" click away from Active. The ~95s live clock (5s pre-game countdown + 3×30s seed
// questions) does NOT run until the operator presses Start, so a human can stage both web + mobile
// first and then catch the live answered/not-answered flip instead of always finding a Finished card.
//
// global-setup seeds participant-1 + the Gilded Owls registered_team_memberships row (so a mobile answer
// is authorized) and op-1's sub-keyed identity; this spec only mirrors session-answered-monitor.spec.ts's
// beforeAll up to — but not including — the drive to Active.
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

test.setTimeout(60000)

// Seeds a startable session; no browser is driven. The assertions guard the seed itself so a broken
// stack fails loudly instead of silently handing the tester a card they can't operate.
test('seeds "Answered Monitor E2E" in Preparing, ready for the operator to Start on demand', async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  // Sub-keyed admin identity for the gateway-JWT assign path (mirrors session-answered-monitor.spec.ts).
  const adminSub = subOf(admin)
  sql('identity_access', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)
  const opId = Number(sql('identity_access', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))
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
  const sessionCode = created.sessionCode as string

  expect(
    (await api('PATCH', `/api/sessions/${liveSessionId}/operator-assignment`, admin, { operatorUserId: opId })).ok,
  ).toBe(true)
  expect((await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM })).ok).toBe(true)

  // Stop at Preparing — deliberately NOT Active. The operator presses "Start" in the UI to begin.
  expect((await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })).ok).toBe(true)

  // Surfaced to the console so the tester can copy the code the mobile participant needs.
  console.log(`\n  HU-36A manual-test session ready (Preparing):`)
  console.log(`    title: Answered Monitor E2E`)
  console.log(`    session code: ${sessionCode}`)
  console.log(`    -> open it as op-1, click "Start" once web + mobile are staged.\n`)
})
