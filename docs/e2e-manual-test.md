# Umbral — End-to-End Manual Test (blank database, no seeds)

Purpose: prove the whole product works from **nothing** — no Docker volumes, no seeded
rows, no Playwright fixtures — on a machine that has never run Umbral. Every row this
test needs is created **through the real UI**, in the order the app actually imposes.

Covers: participant self-registration (mobile) → trivia authoring → mission authoring
(both substage modes) → teams → session → participant join → play → operator
interventions (penalties, authored and released clues, pause/resume) → reconnect recovery →
session ending.

> **Why no seeds.** `frontend/tests/e2e/*-manual-seed.spec.ts` exists and is faster, but it
> writes rows through the API and skips the gates this test is for: JIT user provisioning,
> Keycloak email verification, and mission readiness. A seeded run can pass on a machine
> where the natural flow is broken. That is the exact failure this document is meant to catch.

---

## READ THIS FIRST — two things that will otherwise read as bugs

### 1. "Mission over" is not an operator action

`Finished` is **not** an operator-triggerable state. The operator's full set of manual
transitions is:

| Current state | Operator may trigger |
| ------------- | -------------------- |
| Scheduled | **Prepare** (→ Preparing), **Cancel** |
| Preparing | **Start** (→ Active, needs ≥1 associated team), **Cancel** |
| Active | **Pause** (→ Paused), **Cancel** |
| Paused | **Resume** (→ Active), **Cancel** |
| Finished / Cancelled | *(terminal — no actions)* |

Both the backend (`Domain/Services/SessionStates/ActiveLiveSessionState.cs:12`) and the
frontend (`frontend/app/lib/session-lifecycle.ts`) exclude it deliberately. `Finished` is
reached in exactly two ways, neither of them a button:

1. **The mission runs out of substages** — `LiveSession.CompleteActiveSubstageAndAdvance`,
   called from `Application/Sessions/Common/SubstageAdvanceCoordinator.cs:95`.
2. **The session's `MaximumTime` deadline expires** — the timer worker calls
   `FinishOnMissionDeadlineAsync` (`Infrastructure/Realtime/AuthoritativeSessionTimerWorker.cs:180`).
   This cuts the mission off wherever it is, **mid-question included**.

The live deadline uses the **session's** `MaximumTime` (`maximumTimeMinutes` on
`POST /api/sessions`), whose minimum is **1**. Mission authoring also records a Maximum Time in
the runtime snapshot, but that value does not replace the session's live cutoff. A one-minute
session is the cheap way to see path 2 without waiting out a real mission.

### 2. Both play modes advance, and every substage ends on a ~10s ranking reveal

A treasure-hunt substage advances when the **first team clears all of its active targets** (by
scanning), ending that substage for every team. A trivia substage advances when it **runs out of
questions**. Advancement is mode-agnostic: both go through `SubstageAdvanceCoordinator`, so a
**mixed-mode mission runs end-to-end** and reaches `Finished`.

As each substage ends, the backend broadcasts **`SubstageRankingRevealStarted`** on the sessions
hub (`Api/Hubs/SignalRSessionQuestionBroadcaster.cs:16`) and holds the next substage for the
reveal window. The event carries **no ranking** — clients present the standings they already hold
(`GET /api/sessions/{id}/ranking` + the scoring hub's `RankingChanged`). Mobile shows the podium
full screen for that window, in **either** play mode.

`IsTerminal` marks the last substage: the mission finishes **on** that reveal rather than
advancing off it, so the ranking stays up and becomes the final one.

**Consequences for this test:**

- A **mixed-mode** mission is a valid, supported shape — put both modes in one mission and it
  will finish.
- Expect a **~10s ranking pause between substages**. That is the reveal, not a hang.
- The **`MaximumTime` path sends no reveal at all** — just `Finished`. Mobile lands on the final
  ranking from the terminal state itself, so this looks the same to a participant.

> **History.** Until `ab84da5` a treasure-hunt substage genuinely never advanced and parked the
> session in `Active` forever, so this document told you to keep the modes in separate missions
> (Run A / Run B below). That gap is closed; the two-run split is kept here only because it
> isolates each mode's operator surface, not because mixing them breaks.

---

## 0. Prerequisites

- Docker + Docker Compose v2.24+
- Node + pnpm
- **Two simultaneous mobile clients** for Alice and Bob: two phones, two simulators/emulators,
  or one physical device plus one simulator/emulator. One client is insufficient for the live
  two-team assertions below.
- Ports free: `3000` (web), `8000` (gateway), `8080` (Keycloak), `8025` (Mailpit),
  `5432`, `5672`, `15672`, `8341`, `5001`–`5004`

### Wipe to a true blank slate

```bash
cd backend
docker compose down -v          # -v is the point: drops the postgres-data volume
docker volume ls | grep -i umbral   # expect: no rows
```

Keycloak runs `start-dev --import-realm` against in-memory H2, so it re-imports the
`umbral` realm from `backend/deploy/keycloak/import/` on every boot — it is always blank.
Postgres is the only durable state, and `-v` just removed it.

---

## 1. Bring up the stack

```bash
cd backend
docker compose up -d            # first run builds; expect several minutes
```

All four services run `Database.MigrateAsync()` on startup, so the blank databases
schema themselves. No manual EF step.

**Verify before going further** — a half-up stack produces misleading failures later:

```bash
make -C backend doctor          # watchers armed, binaries out-of-tree
for p in 5001 5002 5003 5004; do curl -fsS localhost:$p/health && echo " ok:$p"; done
curl -fsS localhost:8000/health && echo " ok:gateway"
```

`make doctor` catches the "watcher disarmed" trap where a service serves **stale code**
while `/health` still returns 200. Health checks alone cannot see it. If it reports a bad
service: `make -C backend rewire SVC=<svc>`.

### Web app

```bash
cd frontend
cp .env.local.example .env.local     # defaults already point at localhost:8000 / :8080
pnpm install
pnpm dev                             # → http://localhost:3000
```

### Mobile app

```bash
cd mobile
cp .env.example .env.local
```

Set the host to match your target — **this is the #1 mobile setup failure**:

| Target | Host to use |
| ------ | ----------- |
| Physical device (Expo Go) | your LAN IP — `ip route get 1 \| awk '{print $7; exit}'` |
| Android emulator | `10.0.2.2` |
| iOS simulator | `localhost` |

```
EXPO_PUBLIC_API_BASE_URL=http://<host>:8000
EXPO_PUBLIC_KEYCLOAK_URL=http://<host>:8080
EXPO_PUBLIC_KEYCLOAK_REALM=umbral
EXPO_PUBLIC_KEYCLOAK_CLIENT_ID=umbral-mobile
```

```bash
pnpm install
pnpm start                           # restart Metro after ANY .env.local edit
```

### Seeded Keycloak identities

These are realm identities only — **no database rows exist for them yet** (see §3).

| Username | Password | Role |
| -------- | -------- | ---- |
| `admin` | `admin123` | Administrator |
| `operator` | `operator123` | Operator |
| `operator2` | `operator123` | Operator |
| `participant` | `participant123` | Participant |

**Checkpoint ✅** — five apps reachable: web `:3000`, gateway `:8000`, Keycloak `:8080`,
Mailpit `:8025`, Metro in your terminal.

---

## 2. Register a participant on mobile (self-registration)

This is the true entry point for a participant and the step seeds always skip.

1. In the mobile app, go from **login → Register**.
2. Fill in display name, email, password (**min 8 chars**; Keycloak's realm policy is the
   real authority and rejects weak passwords with its own message).
   Suggested: `Alice Ruiz` / `alice@umbral.local` / `participant123`.
3. Submit.

**What happens:** `POST /api/users/register` (anonymous, rate-limited) provisions a Keycloak
account with the role **fixed server-side to Participant** — the request cannot mint an
Operator — and sends a verification email. **No database row is written yet**
(`RegisterParticipantCommandHandler`).

### Verify the email — login is blocked until you do

The realm sets `verifyEmail: true`, so the account cannot sign in yet.

1. Open **Mailpit → http://localhost:8025**
2. Open the "Verify email" message for `alice@umbral.local`
3. Click the verification link → Keycloak confirms

> Mail never leaves the machine; Keycloak's SMTP points at the `mailpit` container.

**Register a second participant** the same way (`Bob Neri` / `bob@umbral.local`) — you need
two to see ranking and penalties mean anything.

**Negative checks (worth doing here):**

| Input | Expected |
| ----- | -------- |
| Same email again | "An account with this email already exists." (409) |
| Password < 8 chars | Inline validation, no request sent |
| Many rapid attempts | "Too many attempts…" (429, rate limiter) |
| Sign in before verifying | Rejected — this is correct, not a bug |

**Checkpoint ✅** — two verified participant accounts exist in Keycloak, zero rows in `identity_access`.

---

## 3. First sign-in for every actor (JIT provisioning)

**Do not skip this — it is the blank-DB trap.** Users are provisioned into the
`identity_access` database on **first authenticated call**, not at registration
(`AuthenticateUserCommandHandler:46` → `IdentityProvisioningPolicy.SynchronizeOrCreate`).

Until an actor signs in once, they **do not exist** to the rest of the system: the admin
cannot assign the operator to a session, and cannot authorize a participant onto a team.

Sign in once, in this order:

1. **`admin` / `admin123`** on web `:3000` → dashboard loads
2. **`operator` / `operator123`** on web (use a private window or sign out) → dashboard loads
3. **`alice@umbral.local`** on mobile → participant home screen
4. **`bob@umbral.local`** on mobile → participant home screen

> The mobile login form takes the **email**. Signing in as `admin`/`operator` on *mobile*
> correctly shows **Access Denied** — that app is participants-only.

**Checkpoint ✅** — four rows in `identity_access.users`. Confirm if you like:

```bash
docker compose exec postgres psql -U postgres -d identity_access -c 'select * from users;'
```

---

## 4. Author and publish a trivia quiz (admin, web)

Trivia quizzes come **before** missions: a Trivia substage can only select a **published**
quiz, and an unpublished one fails mission readiness.

Signed in as **`admin`** → **Trivia quizzes** panel:

1. **Create trivia quiz** — title `Downtown Warm-Up`, a description.
2. **Add question** ×3. Each needs:
   - a prompt
   - **2–4 options**, with **exactly one** marked Correct (both enforced server-side)
   - for this test, an explanation that can be verified on the result reveal (the product permits
     omitting it)
   - a **score value** — positive, **max 100** (the form defaults to 100)
   - a **time limit in seconds** — allowed range is **5–120**. **Use the 5–15s end**: in Run B
     you wait through every one of these on a real clock to reach `Finished`.
3. Check **Publication readiness** shows ready, then **Publish**.
4. Confirm **Status → Published**.

> A Draft quiz is invisible to mission authoring. If the quiz picker in §5 is empty,
> you did not publish.

**Checkpoint ✅** — one **Published** quiz with 3 short-timer questions.

---

## 5. Author the missions (admin, web)

Use two missions so each run isolates one play mode's operator surface. Mixed-mode missions are
supported; this split is a test-design choice, not a product constraint (see **READ THIS FIRST §2**).

Mission authoring is **Mission → Stage → Substage → (Targets | Quiz) → Clues**, and a
mission must reach **Ready** before any session can be created from it.

Readiness rules (`MissionActivationPolicy`) — all must hold:

- ≥1 stage; every stage has ≥1 substage
- every **TreasureHunt** substage has ≥1 **active target**
- every **Trivia** substage selects a **published** quiz
- target **QR codes are unique across the whole mission** (not just the substage)

### 5A. Mission A — Treasure Hunt

1. **Create mission** — name `Old Town Hunt`, description, **difficulty**, **Maximum Time**
   (e.g. 30 min — this is the whole-mission budget, seeded at Start).

   > **Difficulty is not cosmetic — it sets every target's score.** Target scores are
   > *derived*, never authored: `50 × difficulty factor` → **Beginner 50 / Intermediate 100 /
   > Advanced 150** (`ScoreValue.BaseTargetScore`, `Difficulty.ScoreFactor`). The editor shows
   > the result **read-only** as "*(set by \<difficulty\> difficulty)*". Changing the mission's
   > difficulty **reprices every target**. Pick `Intermediate` for round numbers.

2. Add a **Stage** — `Stage 1`.
3. Add a **Substage** under it — **Play mode: Treasure Hunt**.
4. Add **2 targets**. For each: name, **QR code** (use the generator — it makes an opaque
   `TGT-XXXXXXXX` token), latitude/longitude. Use distinct, non-zero coordinates so the mobile map
   can prove that authored locations crossed the runtime boundary. **There is no score field** —
   see above.
5. **Print or screenshot the QR previews** — you scan these with the phone in §9.
6. Add **2 clues** on the substage:
   - one **"Visible when substage starts"**
   - one **"Hidden until operator releases it"** ← **required**, or the operator's clue-release
     panel is empty (`ProjectReleasableClues` filters on exactly this policy)
7. **Activate** → status must flip **Draft → Ready**.

### 5B. Mission B — Trivia

1. **Create mission** — name `Downtown Trivia Night`, **Maximum Time** comfortably longer
   than the sum of your question timers.
2. Add a **Stage**, then a **Substage** with **Play mode: Trivia**.
3. Select the published **`Downtown Warm-Up`** quiz.
4. *(Optional)* add one substage-scoped clue — trivia substages have no target path, so this
   is the only route a clue reaches a trivia session.
5. **Activate** → **Ready**.

> **Keep every substage in Mission B set to Trivia** — not because mixing breaks (it no longer
> does, see READ THIS FIRST §2), but so Run B isolates the trivia surface from the scanning one.
> If you do want the mixed-mode path, `frontend/tests/e2e/session-substage-progress-manual-seed.spec.ts`
> seeds a Trivia → TreasureHunt mission for you.

**If Activate fails**, the error lists the exact readiness failures — fix and retry.

**Checkpoint ✅** — two missions, both **Ready**.

> **Snapshot semantics — matters for the rest of the test.** Creating a session copies the
> mission into a `MissionRuntimeSnapshot` (`CreateSessionCommandHandler`). **Editing a mission
> afterwards does not affect any existing session.** Finish authoring *now*; to test an edit,
> create a new session.

---

## 6. Create teams and authorize participants (admin, web)

**Registered teams** panel:

1. **New team** → `Gilded Owls`. Note its **team code**.
2. **New team** → `Crimson Foxes`.
3. For each team, **Select participant** → authorize:
   - `Alice Ruiz` → Gilded Owls
   - `Bob Neri` → Crimson Foxes

This requires §3 to have happened — the picker only lists **provisioned users with the
Participant role** (`AuthorizeParticipantForTeamCommandHandler` rejects non-participants).

> **What authorization actually does.** It is a *whitelist*, not a hard requirement. A
> participant with no whitelisted team among a session's attached teams falls into **Open Team
> Selection** and may pick *any* attached team (`OpenTeamSelectionPolicy`). Authorizing here
> means Alice sees Gilded Owls as **"Your team"** and Crimson Foxes as **"Locked"** — which is
> what you want to verify.

**Checkpoint ✅** — two teams, one authorized participant each.

---

## 7. Create the session and wire it up (Run A — Treasure Hunt)

### 7.1 Create the session (admin)

**Session setup** → **Create session**:

- **Mission**: `Old Town Hunt`
- **Session title**: `Old Town Hunt — E2E`
- **Maximum time**, **Scheduled at**

**Copy the 6-character session code** and the **live session id**. The code goes into the
phone; the id is for DB checks.

The session is created in **Scheduled**. Creation is rejected unless the mission is
**active and Ready** (`SessionCreationPolicy`).

### 7.2 Associate the teams (admin)

**Registered teams** → **Assign team to session** → attach **both** teams to this session.

**Preparing → Active requires ≥1 associated team** (`LiveSessionRequiresAtLeastOneTeamException`).
Attaching zero and pressing Start is a good negative check.

### 7.3 Assign the operator (admin)

**Operator assignment** → **Assign operators** → assign **`operator`** to this session.

Do not skip this. It drives two separate things:

- the operator only sees sessions assigned to them (**My sessions**)
- **penalties depend on it**: assignment raises `LiveSessionOperatorAssignedEvent` →
  published as an integration event → `scoring-monitoring-service` consumes it and upserts
  `session_operator_assignments`, keyed by the operator's Keycloak sub. **Scoring rejects a
  penalty from an unassigned operator.** This is asynchronous over RabbitMQ — give it a few
  seconds. If penalties 403 in §10, this is why:

```bash
docker compose exec postgres psql -U postgres -d scoring_monitoring \
  -c 'select * from session_operator_assignments;'
```

**Checkpoint ✅** — session **Scheduled**, two teams attached, operator assigned.

---

## 8. Participant joins — **before** the session starts

> **Ordering constraint.** Team selection is open **only in Scheduled or Preparing**
> (`OpenTeamSelectionPolicy.IsOpenForSelection`). Once the operator hits **Start**, a
> participant who has not joined **cannot get in** — they get "Open team selection closed".
> Join now.

On **Alice's** phone:

1. **Join your session** → enter the **session code** (auto-uppercases; 6 chars)
2. The **team lobby** lists the attached teams:
   - `Gilded Owls` → **"Your team"** (whitelisted in §6)
   - `Crimson Foxes` → **"Locked"**
3. Tap **Gilded Owls** to join.

Repeat on **Bob's** phone → **Crimson Foxes**.

**Negative check worth doing:** try to join a session code that does not exist → clean
"not found", no crash.

**Checkpoint ✅** — both phones sitting in their team space, session still pre-start.

---

## 9. Start and play (Run A — Treasure Hunt)

### 9.1 Operator starts

Sign in as **`operator`** → **My sessions** → select the session.

- **Prepare** (Scheduled → Preparing)
- **Start** (Preparing → **Active**)

**Watch both phones** — they should react live over SignalR without a manual refresh.
Active seeds the **first substage** and starts the **mission timer**.

Before scanning, open the treasure-hunt **Map** tab on mobile. Expect both authored targets to
appear as pins at the coordinates entered in §5A. The map is context only: there is no GPS or
proximity gate on scanning.

### 9.2 Scan targets

On Alice's phone, scan the **QR previews from §5A**.

| Scan | Expected |
| ---- | -------- |
| Any active target, in either order | Accepted; score credited; ranking moves |
| Same target twice | Rejected as already resolved |
| A QR from a different mission | Rejected — unresolvable |

Watch the operator's **team progress**, **ranking**, and **evidence** panels update live.

**Checkpoint ✅** — a scan on the phone visibly moves the operator's panels.

---

## 10. Operator interventions (Run A)

### 10.1 Apply a penalty

> **Order matters:** the phone must already be on the board **before** you apply the penalty,
> or you get the new total with no toast.

Operator → **Penalty** panel → select **Gilded Owls** → enter a **Reason** → apply.

**The operator picks the team and the reason — not the amount.** The magnitude is a fixed
server-side constant, `ScoreValue.Create(100)` in `ApplyPenaltyCommandHandler:12`. There is no
amount field, and a penalty **without a reason is rejected** (`PenaltyRequiresReasonException`) —
try submitting an empty reason to see it.

Expect:
- the panel confirms **"Penalty applied: −100 pts."** — note this is *the entry*, not the team's
  new total
- operator ranking drops by 100 and re-sorts (recalculated via RabbitMQ, so allow a moment)
- **Alice's phone** shows the new total and a **PENALTY APPLIED** toast, live

Penalties are **operator-only** — an admin session is rejected by the server action.
If this 403s, re-read §7.3.

### 10.2 Release a clue

Operator → **Clue** panel → the **"Hidden until operator releases it"** clue from §5A is
listed (the "visible when substage starts" one is not — by design) → select **Gilded Owls** →
**Release clue**.

Expect: it appears on **Alice's** phone live and does **not** appear on Bob's phone. This proves
that the operator-to-mobile push preserves team targeting rather than broadcasting every clue.

**If the panel is empty:** the active substage has no clue with
`HiddenUntilOperatorRelease`, or the active substage is not the one you authored it on.

### 10.3 Assign an operative clue

Operator → **Operative clue** panel → enter a free-text clue such as
`Look beneath the blue banner.` → leave **Assign to: All teams** → **Assign clue**.

Expect:

- the panel confirms it was assigned to **2 teams**
- both phones show the new clue live, without a refresh
- the clue remains available after dismissing its arrival toast

Unlike a released clue, an operative clue is written during the live session and was not part of
the mission snapshot.

### 10.4 Pause and resume

- **Pause** → both phones show paused; the mission timer **freezes**
- while paused, assign another operative clue to **Crimson Foxes** only → Bob receives it and
  Alice does not (operative clues are allowed in Active or Paused)
- **Resume** → phones return to play; the timer continues **from where it stopped** (a resume
  never reseeds the budget)

Try a scan **while paused** → rejected (Active is the only state that admits evidence).

**Checkpoint ✅** — penalties, released clues, operative clues, and pause/resume all reflected
on the correct mobile clients live.

### 10.5 Reconnect recovery

While the session is Active, background or force-close Alice's app, then reopen it. Expect it to
restore the same session, team, active substage, score, timer, visible clues, and target progress;
Alice must not be asked to join again.

Repeat with a brief network interruption if practical. During SignalR recovery the app may show a
reconnecting state, but after transport restoration it must rejoin the session and scoring groups
without a manual refresh.

### 10.6 End Run A

Run A is a single treasure-hunt substage, so it is also the **last** one. Clear it the way the
product intends — **scan the remaining targets** until every active target is resolved.

On the final resolution the substage ends and the backend broadcasts
`SubstageRankingRevealStarted` with `isTerminal: true`. Expect, in order:

1. Both phones go **full-screen ranking** (~10s).
2. The session transitions to **Finished** — the ranking stays up and becomes the final one.

During the reveal, briefly background and foreground one phone. The timer snapshot carries the
active reveal, so the ranking must restore rather than dropping back to the play surface.

Two other exits are worth seeing once, but neither is required here:

- **Let `MaximumTime` expire** → `Finished` with **no reveal**, cutting the mission off where it
  stands. Use a session created with `maximumTimeMinutes: 1` rather than waiting out the default.
- **Cancel** (with a reason) → `Cancelled`, terminal. Both phones show the host's notice — a
  cancelled session shows **no** ranking, deliberately.

---

## 11. Run B — Trivia, through to `Finished`

Repeat **§7 → §9.1** with **Mission B (`Downtown Trivia Night`)**: create session, attach both
teams, assign the operator, join from both phones **pre-start**, then **Prepare → Start**.

Reuse the same teams and participants — no new registration needed.

### Play the round

On **Start**, the first question activates and pushes to both phones with its timer.

For each question:

1. **Alice answers, Bob does not** — gives you a real answered/unanswered split
2. Operator's **trivia round** panel shows the active question/countdown, and the **answered
   monitor** shows Alice as answered and Bob as not answered, live
3. Let the timer **expire**
4. At close, the **result reveals**: correct option + explanation on both phones; the operator's
   **answer review** shows Alice's selection/result and Bob as unanswered
5. After the reveal window, the **next question activates automatically**

> Everything here is driven by `AuthoritativeSessionTimerWorker` on a real clock. Do not
> rush it — there is no "skip question" control, by design.

### The finish

After the **last question** of the **last substage** reveals, the orchestrator advances,
finds no next substage, and applies **`Finished`**.

Expect:
- session state → **Finished**
- the operator's action list is **empty** (terminal state — correct)
- both phones show the session ended
- final ranking persists

**This is the only genuine "mission over" in the product today.**

**Checkpoint ✅ — full pass.** A blank database, driven entirely through the real UIs,
reached a legitimately Finished session.

---

## Appendix A — Failure triage

| Symptom | Likely cause |
| ------- | ------------ |
| Registration email never arrives | Mailpit not up; check `:8025` and the `mailpit` container |
| Cannot sign in a new participant | Email not verified — realm has `verifyEmail: true` (§2) |
| Operator not listed for assignment | Operator never signed in — no user row yet (§3) |
| Participant not listed for a team | Same, or the user is not Participant-role (§3/§6) |
| Quiz picker empty in mission authoring | Quiz is **Draft**, not Published (§4) |
| Activate fails | Read the listed readiness failures (§5) |
| Create session rejected | Mission is Draft or inactive (§5) |
| **Start** rejected | No team associated (§7.2) |
| Participant cannot join | Session already **Active** — selection closed (§8) |
| Penalty 403s | Operator not assigned, or the RabbitMQ projection has not landed (§7.3) |
| Clue release panel empty | No `HiddenUntilOperatorRelease` clue on the **active** substage (§5A) |
| Session remains Active after its mission timer reaches zero | Timer worker/projection failure — the deadline should transition either play mode to **Finished** |
| Phones do not react live | SignalR/gateway — check `:8000` and the hub token route |
| Service serves stale code, `/health` 200 | Watcher disarmed → `make -C backend rewire SVC=<svc>` |

## Appendix B — Useful probes

> **Column naming differs per service** — `session_operations` uses snake_case
> (`session_code`), `identity_access` uses PascalCase (`"DisplayName"`, needing double quotes
> in psql). These probes use `select *` so they work either way.

```bash
# session state + code
docker compose exec postgres psql -U postgres -d session_operations \
  -c 'select * from live_sessions;'

# scoring's operator projection (the penalty gate — §7.3)
docker compose exec postgres psql -U postgres -d scoring_monitoring \
  -c 'select * from session_operator_assignments;'

# provisioned users (empty until first sign-in — §3)
docker compose exec postgres psql -U postgres -d identity_access \
  -c 'select * from users;'
```

- **Seq** (traces + logs, all four services): http://localhost:8341
- **RabbitMQ management**: http://localhost:15672 (`guest`/`guest`)
- **Mailpit**: http://localhost:8025
- **Keycloak admin**: http://localhost:8080 (`admin`/`admin`)

## Appendix C — Gaps this test surfaces

Findings about the product, not the test. Worth tickets if they are not already tracked:

1. *(Closed in `ab84da5`.)* ~~Treasure-hunt substages never advance~~ — advancement is now
   mode-agnostic via `SubstageAdvanceCoordinator`, and the `MaximumTime` deadline finishes a
   session outright. Mixed-mode missions run end-to-end; `Cancel` is no longer the only exit.
2. **No operator-facing reveal.** The operator dashboard does not surface the ranking reveal or
   the substage it belongs to; only participants see it.
