# Umbral — End-to-End Manual Test (blank database, no seeds)

Prove the product works from **nothing** — no Docker volumes, no seeded rows, no Playwright
fixtures. Every row is created **through the real UI**, in the order the app imposes.

**Flow:** participant self-registration (mobile) → operator authors a trivia quiz → admin
authors **one mixed mission** (1 trivia + 2 treasure-hunt substages) → admin assigns the
operator → operator wires up teams + session → participants join → operator runs and tracks the
session (with a Firefox window open on the treasure-hunt QRs) → `Finished`.

> **Why no seeds.** The `*-manual-seed.spec.ts` fixtures write through the API and skip the
> gates this test exists for: JIT user provisioning, Keycloak email verification, mission
> readiness. A seeded run can pass where the natural flow is broken — the exact failure this
> document catches.

---

## Two things that otherwise read as bugs

1. **"Mission over" is not an operator button.** The operator's only manual transitions are
   Prepare, Start (needs ≥1 team), Pause, Resume, Cancel. `Finished` is reached only by the
   mission **running out of substages** (`SubstageAdvanceCoordinator`) or the session's
   **`MaximumTime` expiring** (`AuthoritativeSessionTimerWorker`, cuts off mid-question). The
   session `MaximumTime` minimum is **1 min** — the cheap way to see the deadline path.

2. **Every substage ends on a ~10s ranking reveal.** A treasure-hunt substage advances when the
   **first team clears all its active targets**; a trivia substage advances when it **runs out of
   questions**. Both go through the same coordinator, so a **mixed mission runs end-to-end**. The
   `~10s` pause between substages is the reveal (`SubstageRankingRevealStarted`), not a hang. The
   last substage is terminal: the mission finishes **on** that reveal.

---

## 0. Prerequisites

- Docker + Docker Compose v2.24+, Node + pnpm
- **Two mobile clients** (Alice + Bob) — two phones/emulators, or one device + one simulator.
- **A Firefox window** on the machine, for displaying the treasure-hunt QR previews to scan (§9).
- Ports free: `3000 8000 8080 8025 5432 5672 15672 8341 5001–5004`

### Wipe to blank

```bash
cd backend
docker compose down -v          # -v drops the postgres-data volume — the point
docker volume ls | grep -i umbral   # expect: no rows
```

Keycloak re-imports the `umbral` realm on every boot (in-memory H2); Postgres is the only durable
state, and `-v` removed it.

---

## 1. Bring up the stack

```bash
cd backend
docker compose up -d            # first run builds; expect several minutes
make -C backend doctor          # catches "watcher disarmed → serves stale code"; fix: make rewire SVC=<svc>
for p in 5001 5002 5003 5004; do curl -fsS localhost:$p/health && echo " ok:$p"; done
curl -fsS localhost:8000/health && echo " ok:gateway"
```

### Web (admin + operator)

```bash
cd frontend && cp .env.local.example .env.local && pnpm install && pnpm dev   # → :3000
```

### Mobile (participants)

```bash
cd mobile && cp .env.example .env.local
```

Set the host — **#1 mobile failure**: physical device → your LAN IP
(`ip route get 1 | awk '{print $7; exit}'`); Android emulator → `10.0.2.2`; iOS sim → `localhost`.

```
EXPO_PUBLIC_API_BASE_URL=http://<host>:8000
EXPO_PUBLIC_KEYCLOAK_URL=http://<host>:8080
EXPO_PUBLIC_KEYCLOAK_REALM=umbral
EXPO_PUBLIC_KEYCLOAK_CLIENT_ID=umbral-mobile
```

```bash
pnpm install && pnpm start      # restart Metro after ANY .env.local edit
```

### Seeded Keycloak identities (realm only — **no DB rows yet**, see §3)

| Username | Password | Role |
| -------- | -------- | ---- |
| `admin` | `admin123` | Administrator |
| `operator` | `operator123` | Operator |
| `participant` | `participant123` | Participant |

**Checkpoint ✅** — web `:3000`, gateway `:8000`, Keycloak `:8080`, Mailpit `:8025`, Metro up.

---

## 2. Register two participants on mobile (self-registration)

The true participant entry point — seeds skip it.

1. **login → Register** → display name, email, password (**min 8 chars**; realm policy is the
   authority). Use `Alice Ruiz / alice@umbral.local / participant123`, then
   `Bob Neri / bob@umbral.local / participant123`.
2. `POST /api/users/register` provisions a Keycloak account with role **fixed to Participant**
   and sends a verification email. **No DB row yet.**
3. **Verify:** Mailpit → http://localhost:8025 → open each "Verify email" → click the link.
   The realm sets `verifyEmail: true`, so **login is blocked until verified**.

Negative checks: same email → 409; password < 8 → inline block; rapid attempts → 429;
sign in before verifying → rejected (correct).

**Checkpoint ✅** — two verified participants in Keycloak, zero rows in `identity_access`.

---

## 3. First sign-in for every actor (JIT provisioning)

**The blank-DB trap.** Users are provisioned into `identity_access` on **first authenticated
call**, not at registration. Until an actor signs in once, the admin cannot assign them.

Sign in once, in order:

1. `admin / admin123` — web → dashboard
2. `operator / operator123` — web (private window / sign out) → dashboard
3. `alice@umbral.local` — mobile → participant home
4. `bob@umbral.local` — mobile → participant home

> Mobile login takes the **email**. `admin`/`operator` on mobile correctly show **Access
> Denied** — that app is participants-only.

```bash
docker compose exec postgres psql -U postgres -d identity_access -c 'select * from users;'  # expect 4
```

---

## 4. Operator authors and publishes the trivia quiz (web)

Quizzes come **before** missions — a Trivia substage can only pick a **published** quiz.

Signed in as **`operator`** → **Trivia quizzes**:

1. **Create quiz** — title `Downtown Warm-Up`, a description.
2. **Add question ×3.** Each: a prompt; **2–4 options** with **exactly one** Correct; an
   explanation (verifiable on reveal); **score ≤ 100** (defaults 100); **time limit 5–120s** —
   **use 5–15s** (you wait these out on a real clock in §9).
3. **Publish** → confirm **Status → Published**.

> A Draft quiz is invisible to mission authoring. Empty quiz picker in §5 = not published.

**Checkpoint ✅** — one Published quiz with 3 short-timer questions.

---

## 5. Admin authors the mission (web) — 1 stage, 3 substages

Signed in as **`admin`** → **Missions**. Structure is
**Mission → Stage → Substage → (Targets | Quiz) → Clues**; it must reach **Ready** before any
session can be created.

**Readiness (`MissionActivationPolicy`):** ≥1 stage, each with ≥1 substage; every treasure-hunt
substage has ≥1 active target; every trivia substage selects a **published** quiz; target QR codes
**unique across the whole mission**.

1. **Create mission** — name `Caracas City Run`, description, **difficulty `Intermediate`**,
   **Maximum Time** (e.g. 30 min).

   > **Difficulty sets every target's score** — derived, not authored: `50 × factor` →
   > Beginner 50 / Intermediate 100 / Advanced 150. There is no target score field.

2. Add **Stage 1**, then add these **three substages** under it, in order:

   **Substage 1 — Trivia.** Play mode Trivia → select the published **`Downtown Warm-Up`** quiz.
   *(Optional)* add one substage-scoped clue — trivia has no target path, so that's the only route
   a clue reaches a trivia substage.

   **Substage 2 — Treasure Hunt.** Play mode Treasure Hunt → add **1 target**:
   | Field | Value |
   | ----- | ----- |
   | Name | `Universidad Católica Andrés Bello` |
   | QR code | use the generator (opaque `TGT-XXXXXXXX`) |
   | Latitude | `10.46426` |
   | Longitude | `-66.97629` |

   **Substage 3 — Treasure Hunt.** Play mode Treasure Hunt → add **1 target**:
   | Field | Value |
   | ----- | ----- |
   | Name | `Plaza Altamira` |
   | QR code | use the generator |
   | Latitude | `10.49559` |
   | Longitude | `-66.84886` |

   On each treasure-hunt substage add **2 clues**: one **"Visible when substage starts"** and one
   **"Hidden until operator releases it"** ← required, or the operator's clue-release panel is empty.

3. **Screenshot / print both QR previews** — you display these in Firefox and scan them in §9.
4. **Activate** → status **Draft → Ready**. If it fails, the error lists the exact readiness gaps.

**Checkpoint ✅** — one **Ready** mixed mission (Trivia → TH → TH), two authored targets in Caracas.

> **Snapshot semantics.** Creating a session copies the mission into a `MissionRuntimeSnapshot`.
> **Editing the mission afterwards does not affect an existing session** — finish authoring now.

---

## 6. Admin assigns the operator (web)

Signed in as **`admin`** → **Operator assignment** → **Assign operators** → assign **`operator`**.

Do not skip — it drives two things: the operator only sees **My sessions** for sessions assigned
to them, and **penalties depend on it** (assignment publishes a RabbitMQ event that
`scoring-monitoring-service` projects into `session_operator_assignments`; scoring **rejects a
penalty from an unassigned operator**). It's async — allow a few seconds.

```bash
docker compose exec postgres psql -U postgres -d scoring_monitoring \
  -c 'select * from session_operator_assignments;'
```

---

## 7. Operator wires up the session (web)

Signed in as **`operator`**:

1. **Create session** from `Caracas City Run` — title `Caracas City Run — E2E`, **Maximum time**,
   **Scheduled at**. **Copy the 6-char session code** (goes to the phones) and the live session id.
   Created in **Scheduled**; rejected unless the mission is active + Ready.
2. **Teams** — create two (`Gilded Owls`, `Crimson Foxes`), or reuse existing ones, then
   **authorize a participant on each**: `Alice Ruiz → Gilded Owls`, `Bob Neri → Crimson Foxes`.
   The picker only lists provisioned Participant-role users (needs §3).
3. **Associate both teams to this session.** **Preparing → Active requires ≥1 associated team**
   — attaching zero and pressing Start is a good negative check.

> **Authorization is a whitelist, not a gate.** Alice sees Gilded Owls as **"Your team"** and
> Crimson Foxes as **"Locked"**; an un-whitelisted participant falls into Open Team Selection and
> may pick any attached team.

**Checkpoint ✅** — session Scheduled, two teams attached, operator assigned.

---

## 8. Participants join — **before** Start

> Team selection is open **only in Scheduled/Preparing**. Once the operator hits Start, a
> participant who hasn't joined **cannot get in** ("Open team selection closed"). Join now.

- **Alice** → Join session → enter the session code → lobby shows Gilded Owls **"Your team"**,
  Crimson Foxes **"Locked"** → tap **Gilded Owls**.
- **Bob** → same code → **Crimson Foxes**.

Negative check: a non-existent code → clean "not found", no crash.

**Checkpoint ✅** — both phones in their team space, session pre-start.

---

## 9. Operator runs and tracks the session (web)

**Open the two QR previews from §5 in a Firefox window** so you can point Alice's camera at them
when the treasure-hunt substages come up.

### 9.1 Start

Operator → **My sessions** → the session → **Prepare** (Scheduled → Preparing) → **Start**
(Preparing → Active). Both phones react live over SignalR — no refresh. Active seeds the **first
substage** (Trivia) and starts the **mission timer**.

### 9.2 Substage 1 — Trivia

On Start the first question pushes to both phones with its timer. Per question:

1. **Alice answers, Bob does not** (a real answered/unanswered split).
2. Operator's **trivia** panel shows the active question + countdown; the **answered monitor**
   shows Alice answered, Bob not — live.
3. Let the timer **expire** → reveal: correct option + explanation on both phones; operator's
   **answer review** shows Alice's selection, Bob unanswered.
4. Next question activates automatically (no skip control, by design).

After the **last question**, the substage ends → **~10s full-screen ranking reveal** → advance to
substage 2.

### 9.3 Substages 2 & 3 — Treasure Hunt

Open the mobile **Map** tab: the authored target appears as a pin at its Caracas coordinates
(context only — no GPS/proximity gate on scanning).

On Alice's phone, **scan the QR shown in Firefox**:

| Scan | Expected |
| ---- | -------- |
| The active target | Accepted; score credited; ranking moves |
| Same target twice | Rejected as already resolved |
| A QR from a different mission | Rejected — unresolvable |

Watch the operator's **team progress / ranking / evidence** panels update live. Clearing the
single target ends the substage → ~10s reveal → advance to substage 3 → scan its target the same way.

### 9.4 Operator interventions (during a treasure-hunt substage)

> The phone must be on the board **before** the intervention, or you get the new total with no toast.

- **Penalty** — select `Gilded Owls`, enter a **Reason**, apply. The amount is fixed server-side
  (**−100**); an empty reason is rejected. Ranking drops and re-sorts (async via RabbitMQ); Alice's
  phone shows a **PENALTY APPLIED** toast. Operator-only — an admin session is 403'd.
- **Release clue** — the **"Hidden until operator releases it"** clue is listed (the visible-on-start
  one is not) → target `Gilded Owls` → appears on Alice's phone, **not** Bob's.
- **Operative clue** — free text (e.g. `Look beneath the blue banner.`) → All teams → both phones
  show it live; not part of the mission snapshot.
- **Pause / Resume** — both phones show paused, timer **freezes**; an operative clue to Crimson
  Foxes only reaches Bob; a scan while paused is **rejected**; Resume continues from where it stopped.
- **Reconnect** — background/force-close Alice's app, reopen → it restores session, team, substage,
  score, timer, clues, progress without rejoining.

### 9.5 The finish

The third substage is terminal. Clearing its target broadcasts
`SubstageRankingRevealStarted { isTerminal: true }`:

1. Both phones go **full-screen ranking** (~10s).
2. Session → **Finished**; the ranking stays up as the final one; the operator's action list is
   **empty** (terminal — correct).

Two other exits, worth seeing once but not required: **`MaximumTime` expires** → `Finished` with
**no reveal** (use a session with `maximumTimeMinutes: 1`); **Cancel** (with reason) → `Cancelled`,
**no** ranking shown.

**Checkpoint ✅ — full pass.** A blank database, driven entirely through the real UIs, reached a
legitimately Finished mixed-mode session.

---

## Appendix A — Failure triage

| Symptom | Likely cause |
| ------- | ------------ |
| Registration email never arrives | Mailpit down — `:8025` / `mailpit` container |
| Cannot sign in a new participant | Email not verified — `verifyEmail: true` (§2) |
| Operator/participant not listed | Actor never signed in — no user row (§3) |
| Quiz picker empty in mission authoring | Quiz is Draft, not Published (§4) |
| Activate fails | Read the listed readiness failures (§5) |
| Create session rejected | Mission Draft or inactive (§5) |
| **Start** rejected | No team associated (§7) |
| Participant cannot join | Session already Active — selection closed (§8) |
| Penalty 403s | Operator unassigned, or RabbitMQ projection not landed (§6) |
| Clue release panel empty | No `HiddenUntilOperatorRelease` clue on the **active** substage (§5) |
| Session stuck Active after timer hits zero | Timer worker/projection failure |
| Phones don't react live | SignalR/gateway — check `:8000` and the hub token route |
| Service serves stale code, `/health` 200 | Watcher disarmed → `make -C backend rewire SVC=<svc>` |

## Appendix B — Useful probes

```bash
docker compose exec postgres psql -U postgres -d session_operations  -c 'select * from live_sessions;'
docker compose exec postgres psql -U postgres -d scoring_monitoring  -c 'select * from session_operator_assignments;'
docker compose exec postgres psql -U postgres -d identity_access     -c 'select * from users;'
```

- **Seq**: http://localhost:8341 · **RabbitMQ**: http://localhost:15672 (`guest`/`guest`)
- **Mailpit**: http://localhost:8025 · **Keycloak admin**: http://localhost:8080 (`admin`/`admin`)
