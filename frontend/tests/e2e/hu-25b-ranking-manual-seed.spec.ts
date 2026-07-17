// HU-25B MANUAL-TEST seed (not a behavior assertion). Seeds a treasure-hunt session with two
// registered teams and REAL, backend-computed ranking data, then leaves it Active so a human can
// open it on mobile and see the TEAMS tab already populated.
//
// Why this no longer hand-writes rankings: the previous version INSERTed score_entries + rankings +
// ranking_rows straight into scoring_monitoring "to bypass RabbitMQ consumers". That made the mobile
// TEAMS tab render rows the scoring backend never produced — so a human eyeballing it could sign off
// on ranking while the real grant->score->recalc pipeline was broken (and the seed's reason codes
// didn't even match production: it wrote 'target-resolved', the real TargetResolvedConsumer writes
// 'treasure-target-resolved'). Instead this seed drives a real participant target scan through the
// gateway: POST /participants/target-scans -> TargetResolved (RabbitMQ) -> TargetResolvedConsumer ->
// RecordScoreEntry -> ScoreEntry.Grant -> ScoreEntryRegistered -> RecalculateRanking -> ranking_rows.
// The row the tester sees is therefore genuine backend output, and staging this session exercises the
// grant pipeline end to end.
//
// Trade-off vs. the old seed: producing real ranking requires gameplay, and gameplay requires the
// session Active — so this hands over an ALREADY-ACTIVE session, not a Preparing one the operator
// starts by hand. Only Gilded Owls is scored here (global-setup seeds a single participant, on that
// team); Crimson Foxes is attached so the lobby shows two teams, but stays unscored until a real
// participant on it plays.
//
// The mission is still authored directly in mission_design via SQL — that's a precondition (mission
// AUTHORING is a different service, covered by missions.spec.ts / mission-hierarchy.spec.ts), not the
// scoring output this seed is about.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'

// Global-setup seeds these (see tests/setup/global-setup.ts):
//   participant-1 / participant123, keyed by its resolved Keycloak sub, with a Gilded Owls membership
//   op-1          / operator123
//   Team Gilded Owls               (id: a0000000-...-0001)

const TEAM_A = 'a0000000-0000-0000-0000-000000000001' // Gilded Owls (global-setup); scoring keys on ReferenceTeamId
const TEAM_B = 'a0000000-0000-0000-0000-000000000002' // Crimson Foxes (upserted here)
const TEAM_A_NAME = 'Gilded Owls'
const TEAM_B_NAME = 'Crimson Foxes'
const MISSION_NAME = 'HU-25B Ranking E2E'
const STAGE_TITLE = 'Main Stage'
const SUBSTAGE_TITLE = 'Treasure Hunt'
// The first, immediately-visible target. Its QR value is what the fake scanner submits; scoring the
// scan grants its 150-point value to Gilded Owls (SnapshotScorePolicy.Award is identity for grants).
const ASTROLABE_QR = 'HR-25B-QR-1'
const ASTROLABE_SCORE = '150'

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

// Authors a simple treasure-hunt mission: one stage, one TreasureHunt substage, two targets with QR
// codes (Astrolabe visible-on-start, Tapestry operator-release). Only Astrolabe is scanned by this
// seed; Tapestry is left for the human to release + resolve on mobile. Precondition only — the mission
// authoring endpoints are not what this seed verifies.
function authorTreasureHuntMission(): string {
  const missionId = sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='${MISSION_NAME}' AND "IsActive"=true LIMIT 1`)
  if (missionId) return missionId

  runSql('mission_design', `
DO $$
DECLARE
  v_mission_id    INT;
  v_stage_id      INT;
  v_sub_id        INT;
  v_clue_id       INT;
BEGIN
  INSERT INTO "Missions" ("Name", "Description", "Difficulty", "MaximumTimeMinutes", "IsActive", "ActivationState", "Created", "LastModified")
  VALUES ('${MISSION_NAME}', 'Seeded treasure-hunt mission for HU-25B ranking manual test', 'Beginner', 60, true, 'Ready', NOW(), NOW())
  RETURNING "Id" INTO v_mission_id;

  INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
  VALUES (v_mission_id, '${STAGE_TITLE}', 1)
  RETURNING "Id" INTO v_stage_id;

  INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode")
  VALUES (v_stage_id, '${SUBSTAGE_TITLE}', 1, 'TreasureHunt')
  RETURNING "Id" INTO v_sub_id;

  -- Clue 1: visible immediately
  INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
  VALUES (v_sub_id, 'First Clue', 1, 'Find the brass astrolabe near the fountain.', 'VisibleWhenSubstageStarts')
  RETURNING "Id" INTO v_clue_id;

  INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId")
  VALUES (v_sub_id, 'Astrolabe', '${ASTROLABE_QR}', 1, true, ${ASTROLABE_SCORE}, v_clue_id);

  -- Clue 2: operator release
  INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
  VALUES (v_sub_id, 'Second Clue', 2, 'Look behind the tapestry in the great hall.', 'HiddenUntilOperatorRelease')
  RETURNING "Id" INTO v_clue_id;

  INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId")
  VALUES (v_sub_id, 'Tapestry', 'HR-25B-QR-2', 2, true, 100, v_clue_id);
END $$;
`)

  return sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='${MISSION_NAME}' AND "IsActive"=true ORDER BY "Id" DESC LIMIT 1`)
}

test.setTimeout(120000)

test('seeds "HU-25B Ranking E2E" Active with a real backend-computed ranking row', async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  // Resolve existing live-session IDs for this mission before deleting anything
  const existingSessionIds = sql('session_operations',
    `SELECT COALESCE(string_agg("id"::text, ','), '') FROM live_sessions WHERE title_snapshot = '${MISSION_NAME}'`)

  // Drop stale ranking/score data from scoring_monitoring first (no FK to session_operations).
  if (existingSessionIds) {
    const ids = existingSessionIds.split(',').filter(Boolean).map(id => `'${id}'`).join(',')
    runSql('scoring_monitoring', `
      DELETE FROM ranking_rows  WHERE ranking_id IN (SELECT id FROM rankings WHERE live_session_id IN (${ids}));
      DELETE FROM rankings      WHERE live_session_id IN (${ids});
      DELETE FROM score_entries WHERE live_session_id IN (${ids});
    `)
  }

  // Clean previous run of this fixture from session_operations
  runSql('session_operations', `
    DELETE FROM live_sessions WHERE title_snapshot = '${MISSION_NAME}';
  `)

  // Ensure admin-1 exists in identity_access (sub-keyed, for the gateway-JWT operator-assignment path)
  const adminSub = subOf(admin)
  sql('identity_access', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}' OR "Email"='admin-1@umbral.local';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)

  // Resolve op-1 identity
  const opId = Number(sql('identity_access', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))
  expect(opId).toBeGreaterThan(0)

  // participant-1's identity (keyed by resolved Keycloak sub) and its Gilded Owls membership are seeded
  // by global-setup's seedParticipantIdentity — the same reason session-operator-evidence-qr relies on
  // it rather than re-seeding per spec. The scan below authenticates as that participant.

  // Ensure the second team exists AND carries the canonical name (global-setup only creates Gilded
  // Owls). Upsert so an earlier seed can't leave the lobby showing a stale name. Crimson Foxes is
  // attached for a two-team lobby but is not scored by this seed.
  sql('identity_access', `
    INSERT INTO registered_teams (id, display_name, team_code, is_active, created_at, updated_at)
    VALUES ('${TEAM_B}', '${TEAM_B_NAME}', 'CF-01', true, NOW(), NOW())
    ON CONFLICT (id) DO UPDATE
      SET display_name = EXCLUDED.display_name,
          team_code    = EXCLUDED.team_code,
          is_active    = true,
          updated_at   = NOW();
  `)

  // Author the treasure-hunt mission
  const missionId = Number(authorTreasureHuntMission())
  expect(missionId).toBeGreaterThan(0)
  expect(
    sql('mission_design', `SELECT "ActivationState" FROM "Missions" WHERE "Id"=${missionId}`),
  ).toBe('Ready')

  // Create the live session
  const created = await (await api('POST', '/api/sessions', admin, {
    missionId,
    title: MISSION_NAME,
    maximumTimeMinutes: 60,
    scheduledAt: '2026-07-15T10:00:00Z',
  })).json()
  const liveSessionId = created.liveSessionId as string
  const sessionCode = created.sessionCode as string
  expect(liveSessionId).toBeTruthy()

  // Assign operator
  expect(
    (await api('PATCH', `/api/sessions/${liveSessionId}/operator-assignment`, admin, { operatorUserId: opId })).ok,
  ).toBe(true)

  // Add both teams
  expect((await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM_A })).ok).toBe(true)
  expect((await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM_B })).ok).toBe(true)

  // Resolve Gilded Owls' runtime id and self-join it as participant-1 while the session is still
  // Scheduled (self-join freezes once the session leaves Scheduled/Preparing). The scan below submits
  // with the REFERENCE team id, which satisfies both the domain team resolution and the identity-access
  // membership guard.
  const teams = await (await api('GET', `/api/sessions/${liveSessionId}/teams`, op)).json()
  const teamARuntimeId = teams.teams.find((t: { referenceTeamId: string }) => t.referenceTeamId === TEAM_A)
    .runtimeTeamId as string
  const participant = await token('participant-1', 'participant123')
  const join = await api('POST', `/api/sessions/by-code/${sessionCode}/teams/${teamARuntimeId}/join`, participant)
  expect(join.status, 'participant self-join should succeed').toBe(200)

  // Drive to Active. Treasure-hunt has no pre-game countdown: EnterActiveSessionState sets
  // ActiveSubstageId to the first substage synchronously, so the scan is legal the instant this commits.
  expect((await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })).ok).toBe(true)
  expect((await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Active' })).ok).toBe(true)

  // The real scan: Gilded Owls resolves the Astrolabe target. This is the ONLY thing that puts a score
  // into scoring_monitoring — no hand-written score_entries/rankings anymore.
  const scan = await api('POST', `/api/sessions/${liveSessionId}/participants/target-scans`, participant, {
    teamId: TEAM_A,
    scannedValue: ASTROLABE_QR,
    token: null,
  })
  expect(scan.status, await scan.text()).toBe(200)

  // Wait for the backend to compute the ranking from that grant (scan -> TargetResolved -> RecordScoreEntry
  // -> ScoreEntryRegistered -> RecalculateRanking, a couple of RabbitMQ hops). The row the mobile TEAMS
  // tab renders is exactly this one — genuine backend output, not a seed.
  await expect
    .poll(
      () => sql('scoring_monitoring',
        `SELECT rr.total_score FROM rankings r JOIN ranking_rows rr ON rr.ranking_id = r.id
         WHERE r.live_session_id='${liveSessionId}' AND rr.team_id='${TEAM_A}'
         ORDER BY r.calculation_version DESC LIMIT 1`),
      { timeout: 25000, message: 'the target scan never produced a backend ranking row' },
    )
    .toBe(ASTROLABE_SCORE)

  console.log(`\n  HU-25B manual-test session ready (Active, real ranking):`)
  console.log(`    title: ${MISSION_NAME}`)
  console.log(`    session code: ${sessionCode}`)
  console.log(`    live session id: ${liveSessionId}`)
  console.log(`    teams: ${TEAM_A_NAME} (${TEAM_A}), ${TEAM_B_NAME} (${TEAM_B})`)
  console.log(`    ranking: ${TEAM_A_NAME}=${ASTROLABE_SCORE} (backend-computed from a real Astrolabe scan); ${TEAM_B_NAME} unscored`)
  console.log(`    -> open on mobile as participant-1 / Gilded Owls (already joined; reconnect into the`)
  console.log(`       Active session). The TEAMS tab shows the real ranking row immediately. Release +`)
  console.log(`       scan the Tapestry target to watch it recalculate live.\n`)
})
