# HU-171 — Manual Test: mixed-play-mode substage progress on mobile

Goal: a **participant** (mobile) joins a session with mixed play modes (Trivia →
Treasure Hunt) and sees the ordered substage sequence — active substage name
plus a compact chip row showing completed/active/upcoming substages — above the
play-mode-specific content.

You need two things running side by side: the operator on `localhost:3000`, and
the mobile app on a phone/emulator pointed at the same backend (gateway
`localhost:8000`).

---

## 1. Bring up the stack (once)

**⚠ seed-all.sh is destructive.** It deletes every mission containing a trivia
substage before rebuilding its quiz catalog with fresh IDs. If you re-run it
mid-test any manually staged session will be destroyed — re-run the Playwright
seed (step below) after.

```bash
# backend + gateway + canonical seed data (including the dedicated HU-171 quiz)
cd backend && ./scripts/seed-all.sh

# operator web app
cd ../frontend && pnpm install && pnpm dev       # -> http://localhost:3000

# Playwright global setup supplies the E2E identities/team membership; the spec then
# seeds a startable mixed-play-mode session (run from frontend; needs backend + seed-all done)
pnpm exec playwright test tests/e2e/session-substage-progress-manual-seed.spec.ts

# mobile app (separate terminal)
cd ../mobile && pnpm install && pnpm start        # open in Expo Go / emulator
```

## 2. Grab the seeded session code

The seed authors a mission with **two substages** in one stage: a **Trivia** substage (uses the dedicated two-question published quiz "HU-171 Progreso de substages" from `seed-all.sh`) followed by a **TreasureHunt** substage (2 targets with clues). The session is left in **Preparing**, one operator "Start" click from Active.

It prints the session you need — **copy the session code**:

```
  HU-171 manual-test session ready (Preparing):
    title: Substage Progress E2E
    session code: <COPY THIS>
    -> join on mobile as participant-1 / Gilded Owls, then Start as op-1 once staged.
```

## 3. Credentials

| Who         | Where                | Login                                           |
| ----------- | -------------------- | ----------------------------------------------- |
| Operator    | web `localhost:3000` | `op-1` / `operator123`                          |
| Participant | mobile app           | `participant-1@umbral.local` / `participant123` |

`participant-1` is pre-seeded onto the **Gilded Owls** team, which is the team the
seed attaches to the session — join that team on mobile.

## 4. Run it

### Step A — Participant (mobile): join the team

1. Sign in as **participant-1@umbral.local / participant123**.
2. Tap **Join your session** → enter the **session code** printed by the seed.
3. In the team lobby, pick **Gilded Owls** → join.
4. You land in the team space. It shows "Restoring your team space…" until the
   operator starts the session.

### Step B — Operator (web): Start the session

1. Sign in as **op-1 / operator123** and open **Substage Progress E2E**.
2. Click **Start** (drives Preparing → Active). Entering Active sets the active
   substage to the first (Trivia) substage.

### Step C — Verify the substage progress (Trivia active)

Once Active, the participant's screen shows the **SubstageProgress** component
above the trivia question stage. You must see:

- **"Now playing"** label + **"Trivia Round"** as the active substage name.
- **Chip row** with two chips:
  - **`1· Trivia`** — ember/active styling (current substage).
  - **`2· Treasure Hunt`** — muted/upcoming styling.
- Below it, the normal trivia question stage (question 1 of 2).

### Step D — Operator: advance through trivia questions

1. As operator, wait for the question timer to expire (or advance manually if
   supported). The trivia substage advances through both questions.
2. After the last trivia question completes, the session auto-advances to the
   **TreasureHunt** substage.

### Step E — Verify the substage progress (TreasureHunt active)

After advancement, the participant's screen flips to the treasure-hunt board.
The **SubstageProgress** component now shows:

- **"Now playing"** label + **"Treasure Hunt"** as the active substage name.
- **Chip row** with two chips:
  - **`✓ Trivia`** — completed styling (checkmark prefix).
  - **`2· Treasure Hunt`** — ember/active styling (its fixed position in the sequence).
- Below it, the treasure-hunt board (score, clues, target progress).

That substage progress component — showing the ordered sequence with correct
status styling — is HU-171 working. ✅

## 5. Quick extra checks (optional)

| Check                     | How                                                           | Expect                                                                  |
| ------------------------- | ------------------------------------------------------------- | ----------------------------------------------------------------------- |
| Single-substage fallback  | Join a single-substage session (e.g. the HU-23 seed)          | SubstageProgress shows only the name label (no chip row)                |
| Reconnect snapshot        | Background the app for 10s, reopen                            | Board re-fetches the REST snapshot; SubstageProgress shows same state   |
| SignalR reconnect         | `docker compose restart api-gateway`, let it reconnect        | SubstageProgress re-fetches and shows correct current state             |
| No cross-team leak        | Read the substage progress at any point                       | Only Gilded Owls data is shown — no other team's real score             |

---

## Known limitations (by design — not bugs)

- **Live push is partial.** The backend emits `TeamBoardUpdated` on session-state
  change and substage advancement, so the substage progress auto-updates when the
  operator Starts or when substages advance. There is no push on score or
  target-progress changes (those land with HU-31), so mid-substage those values
  won't move on their own.

- **Score and target progress stay 0.** `currentScore` starts at 0 and
  `resolvedTargets` is hard-coded to 0 until target-resolution lands (HU-31).
  HU-171 is about the **substage sequence rendering** with correct status chips —
  not live scoring.

- **The timer reads `00:00 / Expired` for treasure-hunt substages (DES-93).** The
  authoritative timer only tracks the active trivia-question window; a
  treasure-hunt substage has no active question, so remaining time is `Zero` →
  `Expired`. Not a bug; tracked in DES-93.

## Troubleshooting

- **seed-all.sh fails with a missing/non-null `SequenceOrder` column.** The
  latest MissionDesign migrations are not applied. Start `docker compose up`
  (applies pending migrations) then re-run seed-all.sh.
- **Playwright seed fails with "No Published HU-171 Progreso de substages
  quiz found".** Run `seed-all.sh` first — the dedicated quiz lives in that
  seeder, not in the Playwright setup.
- **No chip row appears (only the name label).** The session has only one
  substage. The seed creates two substages — re-seed with the spec above.
- **Stuck on "Restoring your team space…".** The operator hasn't pressed Start
  yet, or no team is attached. Start the session as op-1.
- **Substages don't advance.** Trivia auto-advances on timer expiry. If stuck,
  check the operator's session state and the trivia question timer.
- **Mobile can't reach the backend.** It targets the gateway on
  `localhost:8000`; on a physical device use your machine's LAN IP, not
  `localhost`.

---

## Appendix — the seed spec

> **Keep this appendix in sync with the source spec.** If you change
> `frontend/tests/e2e/session-substage-progress-manual-seed.spec.ts`, update
> this block to match.

The seed run in step 2 lives at
`frontend/tests/e2e/session-substage-progress-manual-seed.spec.ts`. Its full
contents are inlined here for reference (keep this block in sync if the spec
changes):

```ts
// HU-171 MANUAL-TEST seed (not a behavior assertion). Authors a mixed-play-mode mission
// (Trivia → TreasureHunt, two substages in one stage) and creates -> assigns -> attaches a
// "Substage Progress E2E" session, left in PREPARING one operator "Start" click away from Active.
//
// Once Active, the live session's first (Trivia) substage becomes the active substage. The mobile
// participant sees the SubstageProgress component above the trivia surface: "Now playing" + the
// active substage name + an ordered chip row (Trivia active, TreasureHunt upcoming). Advancing
// to the treasure-hunt substage flips the chips and shows the treasure-hunt board.
//
// Why a dedicated seed: global-setup only seeds single-play-mode missions, and #171's
// SubstageProgress renders the ordered multi-substage sequence. A mixed-mode mission is required.
//
// The mission is authored directly in mission_design via SQL (mirroring global-setup's mission
// seeds). POST /api/sessions then builds the immutable runtime snapshot from it automatically.
// global-setup seeds participant-1 + the Gilded Owls registered_team_memberships row and op-1's
// sub-keyed identity; this spec only authors the mission and stages the session.
import { execSync } from 'child_process'
import { test, expect } from '../fixtures/auth'

const DB = 'backend-postgres-1'
const KC = 'http://localhost:8080'
const GW = 'http://localhost:8000'
const TEAM = 'a0000000-0000-0000-0000-000000000001' // Gilded Owls (global-setup seed)
const MISSION_NAME = 'Substage Progress E2E'
const QUIZ_TITLE = 'HU-171 Progreso de substages'

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

// Authors a mixed-play-mode mission: one stage with two substages (Trivia then TreasureHunt).
// Resolves an existing Published trivia quiz by title (seed-all.sh creates several) so the
// trivia substage is runtime-ready. The TreasureHunt substage has two targets with clues. This
// exercises the SubstageProgress chip row in both directions (Trivia active / TreasureHunt
// upcoming, then TreasureHunt active / Trivia completed).
//
// Quiz resolved by title+status, not a hardcoded id: seed-all.sh wipes and recreates quizzes
// with fresh serial ids every run, so pinning a literal id would break after the first reseed.
function authorMixedMission(): void {
  runSql('mission_design', `
DO $$
DECLARE
  v_mission_id    INT;
  v_stage_id      INT;
  v_quiz_id       INT;
  v_th_sub        INT;
  v_clue_id       INT;
  v_seq           INT;
BEGIN
  -- Resolve the dedicated Published quiz owned by seed-all.sh.
  SELECT "Id" INTO v_quiz_id
  FROM "TriviaQuizzes"
  WHERE "Title" = '${QUIZ_TITLE}' AND "Status" = 'Published'
  ORDER BY "Id" DESC LIMIT 1;

  IF v_quiz_id IS NULL THEN
    RAISE EXCEPTION 'No Published "${QUIZ_TITLE}" quiz found — run seed-all.sh first.';
  END IF;

  SELECT "Id" INTO v_mission_id FROM "Missions" WHERE "Name" = '${MISSION_NAME}' LIMIT 1;

  IF v_mission_id IS NOT NULL THEN
    DELETE FROM "MissionStages" WHERE "MissionId" = v_mission_id;
    DELETE FROM "Missions" WHERE "Id" = v_mission_id;
  END IF;

  -- Mission + stage
  INSERT INTO "Missions" ("Name", "Description", "Difficulty", "MaximumTimeMinutes", "IsActive", "ActivationState", "Created", "LastModified")
  VALUES ('${MISSION_NAME}', 'Seeded mixed-play-mode mission for the HU-171 manual test — do not delete', 'Easy', 60, true, 'Ready', NOW(), NOW())
  RETURNING "Id" INTO v_mission_id;

  INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
  VALUES (v_mission_id, 'Stage 1', 1)
  RETURNING "Id" INTO v_stage_id;

  -- Substage 1: Trivia (references the resolved quiz)
  INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode", "TriviaQuizId")
  VALUES (v_stage_id, 'Trivia Round', 1, 'Trivia', v_quiz_id);

  -- Substage 2: TreasureHunt (upcoming until trivia completes)
  INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode")
  VALUES (v_stage_id, 'Treasure Hunt', 2, 'TreasureHunt')
  RETURNING "Id" INTO v_th_sub;

  FOR v_seq IN 1..2 LOOP
    INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
    VALUES (v_th_sub, 'Clue ' || v_seq, v_seq, 'Find landmark #' || v_seq || ' and scan its code.', 'VisibleWhenSubstageStarts')
    RETURNING "Id" INTO v_clue_id;

    INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId")
    VALUES (v_th_sub, 'Target ' || v_seq, 'SP-E2E-QR-' || v_seq, v_seq, true, 50, v_clue_id);
  END LOOP;
END $$;
`)
}

test.setTimeout(60000)

test('seeds "Substage Progress E2E" in Preparing, ready for the operator to Start on demand', async () => {
  const admin = await token('admin-1', 'admin123')
  const op = await token('op-1', 'operator123')

  // Keep this manual fixture idempotent and its title unambiguous across repeated runs.
  // The session aggregate owns dependent teams/participants, so the database cascade removes
  // the whole previous fixture before its mission and immutable snapshot are rebuilt.
  runSql('session_operations', `
    DELETE FROM live_sessions WHERE title_snapshot = '${MISSION_NAME}';
  `)

  authorMixedMission()

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
    scheduledAt: '2026-07-12T10:00:00Z',
  })).json()
  const liveSessionId = created.liveSessionId as string
  const sessionCode = created.sessionCode as string
  expect(liveSessionId).toBeTruthy()

  expect(
    (await api('PATCH', `/api/sessions/${liveSessionId}/operator-assignment`, admin, { operatorUserId: opId })).ok,
  ).toBe(true)
  expect((await api('POST', `/api/sessions/${liveSessionId}/teams`, op, { referenceTeamId: TEAM })).ok).toBe(true)
  expect((await api('PATCH', `/api/sessions/${liveSessionId}/state`, op, { targetState: 'Preparing' })).ok).toBe(true)

  console.log(`\n  HU-171 manual-test session ready (Preparing):`)
  console.log(`    title: ${MISSION_NAME}`)
  console.log(`    session code: ${sessionCode}`)
  console.log(`    -> join on mobile as participant-1 / Gilded Owls, then Start as op-1 once staged.\n`)
})
```
