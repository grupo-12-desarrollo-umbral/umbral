# HU-26 — Manual Test: operator releases a hidden clue, the team's board reveals it

Goal: an **operator** (web) releases a treasure-hunt target's **hidden** clue to one
team, and that team's **participant** (mobile) board reveals the newly-visible clue
live — without the clue leaking to other teams, without releasing twice, and
without advancing the substage.

> Applies once **HU-26 (DES-36)** has landed: the backend `POST /clues/release`,
> the operator release control (web), and the participant reveal (mobile board push).
> Until then use this as the acceptance script.

You need two things running side by side: the operator on `localhost:3000`, and
the mobile app on a phone/emulator pointed at the same backend (gateway
`localhost:8000`).

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + Keycloak
cd backend && docker compose up -d

# operator web app
cd ../frontend && pnpm install && pnpm dev       # -> http://localhost:3000

# seed a startable treasure-hunt session with HIDDEN clues (separate terminal; needs the backend up)
cd frontend && pnpm exec playwright test tests/e2e/session-clue-release-manual-seed.spec.ts

# mobile app (separate terminal)
cd ../mobile && pnpm install && pnpm start        # open in Expo Go / emulator
```

## 2. Grab the seeded session code

The release flow only works when the active substage is a **`TreasureHunt`** with a
clue whose visibility is **`HiddenUntilOperatorRelease`** (a `VisibleWhenSubstageStarts`
clue is already on the board, so there is nothing to release). No default seed creates
one, so the seed run above authors a runtime-ready treasure-hunt mission whose target
clues are all **hidden**, attaches **two** teams, and stages a session in **Preparing**,
one operator "Start" click from Active.

It prints what you need — **copy the session code**:

```
  HU-26 manual-test session ready (Preparing):
    title: Clue Release E2E
    session code: <COPY THIS>
    teams attached: Gilded Owls (participant's team) + Maple Runners (other team)
    -> join on mobile as participant-1 / Gilded Owls, then Start as op-1 once staged.
```

The seeded mission has one treasure-hunt substage with **3 active targets**, each
carrying a **hidden** clue (so the board's CLUES tab starts **empty**).

## 3. Credentials

| Who         | Where                | Login                                           |
| ----------- | -------------------- | ----------------------------------------------- |
| Operator    | web `localhost:3000` | `op-1` / `operator123`                          |
| Participant | mobile app           | `participant-1@umbral.local` / `participant123` |

`op-1` is the **assigned** operator of the seeded session (the ownership `Proxy`
lets only the assigned operator release). `participant-1` is pre-seeded onto the
**Gilded Owls** team — join that team on mobile.

## 4. Run it

### Step A — Participant (mobile): join the team

1. Sign in as **participant-1@umbral.local / participant123**.
2. Tap **Join your session** → enter the **session code** printed by the seed.
3. In the team lobby, pick **Gilded Owls** → join.
4. You land in the team space ("Restoring your team space…" until the operator starts).

### Step B — Operator (web): Start the session

1. Sign in as **op-1 / operator123** and open **Clue Release E2E**.
2. Click **Start** (drives Preparing → Active). Entering Active sets the active
   substage to the first (treasure-hunt) substage.
3. On mobile, the participant's screen flips to the treasure-hunt board. The
   **CLUES tab is empty** and the MAP tab shows **`0 / 3 targets`** — the clues are
   hidden, waiting for release.

### Step C — Operator (web): release a clue, participant sees it

1. In the operator session view, open the **release clue** control, pick **Target 1**,
   choose team **Gilded Owls**, and **Release**.
2. On mobile (no reload), the **CLUES tab reveals exactly one clue card**
   ("Find landmark #1 and scan its code.") — pushed live over `team:{teamId}`.

That live reveal on the released team's board — with the clue hidden until you
released it — is HU-26 working. ✅

## 5. Quick extra checks (the HU-26 acceptance guard)

| Check                 | How                                                                        | Expect                                                                             |
| --------------------- | -------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| No cross-team leak    | Release **Target 2** to **Maple Runners only** (a team you are NOT on)      | The participant's Gilded Owls board does **not** reveal it (still shows 1 clue)     |
| No duplicate          | Release **Target 1** to **Gilded Owls** again                              | Rejected — 409 / ProblemDetails ("already released"); the board is unchanged        |
| Release to all teams  | Release **Target 3** with **all teams** selected (no single team)          | Gilded Owls board reveals it; each attached team gets its own release record        |
| Does not advance      | After any release, check the substage / target progress                    | Still the same active substage; `0 / 3 targets` — release changes visibility only   |
| Ownership guard       | *(optional, needs a second operator)* release as a non-assigned operator   | 403 / ProblemDetails (the `Proxy` blocks non-owning operators)                      |
| Reconnect snapshot    | Background the mobile app 10s, reopen                                       | Board re-fetches REST and still shows the already-released clue (not stale/lost)    |

## Known limitations (by design — not bugs)

Don't treat these as HU-26 failures:

- **No async audit event yet (DES-92).** HU-26 records the release locally (the
  append-only per-team `ClueReleaseRecord`) and pushes the board over SignalR, but it
  does **not** publish `ClueReleased` to RabbitMQ. The queryable session history
  (DES-56/HU-40A) consumes that event, which lands with **DES-92**.
- **Score and target progress stay 0.** `currentScore` and `resolvedTargets` don't
  move on release — releasing a clue is visibility only, not progress. Target
  resolution / scoring land with HU-31.
- **The countdown reads `00:00 / Expired` (DES-93).** A treasure-hunt substage has no
  authoritative countdown yet; ignore the timer chip. Tracked in **DES-93**.
- **Conditional / auto release and operator-added clues are out of scope.** Rule-based
  auto-release is HU-27; operator-authored runtime clues are HU-28.

## Troubleshooting

- **CLUES tab shows a clue before you release anything.** The seeded clue is
  `VisibleWhenSubstageStarts`, not `HiddenUntilOperatorRelease` — re-run the seed spec
  below (it authors hidden clues) and use the code it prints.
- **Release control is missing / does nothing.** The operator web slice (HU-26 Step 9)
  may not be built in the checkout under test. Verify the backend directly with the
  curl fallback in the appendix.
- **Board doesn't reveal after release (you must reload to see it).** The live push
  didn't arrive — confirm the mobile client is on the `team:{teamId}` group (it joins on
  reconnect) and that `TeamBoardUpdated` fires on release; the REST `team-board` fetch on
  reconnect is the fallback.
- **Release returns 403.** You're not the assigned operator — release as **op-1**, the
  operator the seed assigns.
- **Board doesn't appear (you see the A/B/C/D question screen).** The active substage is
  not `TreasureHunt` — you joined a Trivia session. Re-seed with the spec below.
- **Stuck on "Restoring your team space…".** The operator hasn't pressed Start yet, or no
  team is attached. Start the session as op-1.
- **Mobile can't reach the backend.** It targets the gateway on `localhost:8000`; on a
  physical device use your machine's LAN IP, not `localhost`.

### Curl fallback (backend-only, no operator UI)

```bash
# as op-1, fetch the operator session panel to read the active target ids + attached team ids,
# then release Target 1's hidden clue to Gilded Owls:
curl -X POST "http://localhost:8000/api/sessions/{liveSessionId}/clues/release" \
  -H "Authorization: Bearer <op-1 token>" -H "Content-Type: application/json" \
  -d '{ "targetId": "<target-1 id>", "teamId": "<Gilded Owls id>" }'   # omit teamId to release to all teams
```
`{ targetId, teamId }` shapes follow the verified HU-26 contract; read the ids off the
operator panel / runtime snapshot for the seeded session.

---

## Appendix — the seed spec

The seed run in step 2 lives at
`frontend/tests/e2e/session-clue-release-manual-seed.spec.ts`. It mirrors the HU-23
treasure-hunt seed, with two changes for HU-26: the target clues are authored
**`HiddenUntilOperatorRelease`** (so there is something to release), and a **second**
team (Maple Runners) is attached (so the no-leak check is real). Keep this block in sync
if the spec changes.

```ts
// HU-26 MANUAL-TEST seed (not a behavior assertion). Authors a runtime-ready TreasureHunt mission whose
// target clues are HIDDEN (HiddenUntilOperatorRelease), and creates -> assigns -> attaches a "Clue Release
// E2E" session with TWO teams (Gilded Owls + Maple Runners), left in PREPARING one operator "Start" click
// from Active. Once Active, the participant on Gilded Owls lands on the HU-23 board with an EMPTY clues
// list; the operator then releases a target's hidden clue (HU-26) and the board reveals it live.
//
// Two deltas vs. session-treasure-hunt-manual-seed (HU-23): (1) clue Visibility is HiddenUntilOperatorRelease
// so the board withholds the clues until an operator releases them; (2) a second registered team is attached
// so releasing to one team can be shown NOT to leak to the other. currentScore/resolvedTargets stay 0 until
// HU-31; the timer reads Expired until DES-93; ClueReleased is not published to RabbitMQ until DES-92.
//
// global-setup seeds participant-1 + the Gilded Owls registered_team_memberships row (so the participant is
// authorized on that team), the Maple Runners registered team, and op-1's sub-keyed identity; this spec only
// authors the mission and stages the session up to — but not including — the drive to Active.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'
const TEAM_1 = 'a0000000-0000-0000-0000-000000000001' // Gilded Owls  (participant-1's team)
const TEAM_2 = 'a0000000-0000-0000-0000-000000000002' // Maple Runners (other team — for the no-leak check)
const MISSION_NAME = 'Clue Release E2E'

// Reads a single scalar via `psql -c`; fine for simple SELECTs. Multi-statement authoring uses runSql().
function sql(db: string, query: string): string {
  return execSync(`docker exec ${DB} psql -U postgres -d ${db} -t -A -c "${query.replace(/"/g, '\\"')}"`)
    .toString()
    .trim()
}

// Pipes SQL on stdin (no -c), so the PascalCase-quoted DDL below needs no shell escaping. ON_ERROR_STOP
// makes a broken statement fail the seed loudly instead of silently handing the tester a half-built mission.
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

// Idempotently authors a runtime-ready TreasureHunt mission: one stage, one TreasureHunt substage (the
// first, so it becomes active on Start), and three active targets each linked to a HIDDEN clue. Rebuilds the
// hierarchy on every run (DELETE cascades stages -> substages -> targets/clues) so a persistent DB stays
// clean and the mission id survives. The only HU-26 delta from the HU-23 seed is Visibility:
// 'HiddenUntilOperatorRelease' — the board withholds the clue until the operator releases it.
function authorHiddenClueMission(): void {
  runSql('mission_design', `
DO $$
DECLARE
  v_mission_id  INT;
  v_stage_id    INT;
  v_substage_id INT;
  v_clue_id     INT;
  v_seq         INT;
BEGIN
  SELECT "Id" INTO v_mission_id FROM "Missions" WHERE "Name" = '${MISSION_NAME}' LIMIT 1;

  IF v_mission_id IS NULL THEN
    INSERT INTO "Missions" ("Name", "Description", "Difficulty", "MaximumTimeMinutes", "IsActive", "ActivationState", "Created", "LastModified")
    VALUES ('${MISSION_NAME}', 'Seeded runtime-ready treasure-hunt mission with hidden clues for the HU-26 manual test — do not delete', 'Easy', 60, true, 'Ready', NOW(), NOW())
    RETURNING "Id" INTO v_mission_id;
  ELSE
    UPDATE "Missions" SET "IsActive" = true, "ActivationState" = 'Ready', "LastModified" = NOW() WHERE "Id" = v_mission_id;
    DELETE FROM "MissionStages" WHERE "MissionId" = v_mission_id; -- FKs cascade to substages/targets/clues
  END IF;

  INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
  VALUES (v_mission_id, 'Stage 1', 1)
  RETURNING "Id" INTO v_stage_id;

  INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode")
  VALUES (v_stage_id, 'Treasure Hunt Substage', 1, 'TreasureHunt')
  RETURNING "Id" INTO v_substage_id;

  -- One HIDDEN clue per target; the target links to its clue via ClueId so the runtime plan carries the clue
  -- text, but HiddenUntilOperatorRelease keeps it OFF the board's visibleClues until an operator releases it.
  FOR v_seq IN 1..3 LOOP
    INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
    VALUES (v_substage_id, 'Clue ' || v_seq, v_seq, 'Find landmark #' || v_seq || ' and scan its code.', 'HiddenUntilOperatorRelease')
    RETURNING "Id" INTO v_clue_id;

    INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId")
    VALUES (v_substage_id, 'Target ' || v_seq, 'CR-E2E-QR-' || v_seq, v_seq, true, 50, v_clue_id);
  END LOOP;
END $$;
`)
}

test.setTimeout(60000)

// Seeds a startable treasure-hunt session with two teams; no browser is driven. The assertions guard the
// seed itself so a broken stack fails loudly instead of silently handing the tester a card they can't operate.
test('seeds "Clue Release E2E" in Preparing with hidden clues + two teams, ready for the operator to Start', async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  authorHiddenClueMission()

  // Sub-keyed admin identity for the gateway-JWT assign path (mirrors the HU-23 seed).
  const adminSub = subOf(admin)
  sql('identity_access', `
    DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}';
    INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
    VALUES ('${adminSub}','Administrator One','admin-1@umbral.local','Administrator',true,NOW(),NOW());
  `)
  const opId = Number(sql('identity_access', `SELECT "Id" FROM users WHERE "Email"='op-1@umbral.local'`))
  const missionId = Number(
    sql('mission_design', `SELECT "Id" FROM "Missions" WHERE "Name"='${MISSION_NAME}' AND "IsActive"=true AND "ActivationState"='Ready' ORDER BY "Id" DESC LIMIT 1`),
  )
  expect(missionId).toBeGreaterThan(0)

  const created = await (await api('POST', '/api/sessions', admin, {
    missionId,
    title: MISSION_NAME,
    maximumTimeMinutes: 60,
    scheduledAt: '2026-07-05T10:00:00Z',
  })).json()
  const liveSessionId = created.liveSessionId as string
  const sessionCode = created.sessionCode as string
  expect(liveSessionId).toBeTruthy()

  expect(
    (await api('PATCH', `/api/sessions/${liveSessionId}/operator-assignment`, admin, { operatorUserId: opId })).ok,
  ).toBe(true)
  // Both teams must be associated while the session is Scheduled (before Preparing). Two teams lets the
  // tester release to one and confirm the other's board does NOT reveal the clue (no leak).
  expect((await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM_1 })).ok).toBe(true)
  expect((await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM_2 })).ok).toBe(true)

  // Stop at Preparing — deliberately NOT Active. The operator presses "Start" in the UI to begin; entering
  // Active sets the active substage to the first (TreasureHunt) substage, and the mobile board renders (empty
  // clues). The operator then releases a hidden clue (HU-26) and the released team's board reveals it.
  expect((await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })).ok).toBe(true)

  // Surfaced to the console so the tester can copy the code the mobile participant needs.
  console.log(`\n  HU-26 manual-test session ready (Preparing):`)
  console.log(`    title: ${MISSION_NAME}`)
  console.log(`    session code: ${sessionCode}`)
  console.log(`    teams attached: Gilded Owls (participant's team) + Maple Runners (other team)`)
  console.log(`    -> join on mobile as participant-1 / Gilded Owls, then Start as op-1 once staged.\n`)
})
```
