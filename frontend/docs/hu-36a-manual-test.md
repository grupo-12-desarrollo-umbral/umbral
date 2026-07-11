# HU-36A — Manual Test: operator sees answered / not-answered, live

Goal: an **operator** (web) watches the Answered monitor board while a
**participant** (mobile) answers the active trivia question, and the
participant's row flips **Not answered → Answered** in real time — never
revealing the chosen option.

You need two things running side by side: the operator on `localhost:3000`, and
the mobile app on a phone/emulator pointed at the same backend (gateway
`localhost:8000`).

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + Keycloak
cd backend && docker compose up -d

# seed the DBs and create a fresh session, left one click away from going live
#   (also seeds participant-1 + the Gilded Owls membership so answers are accepted)
cd ../frontend && pnpm install && pnpm exec playwright test tests/e2e/session-answered-monitor-manual-seed.spec.ts

# operator web app
pnpm dev                       # -> http://localhost:3000

# mobile app (separate terminal)
cd ../mobile && pnpm install && pnpm start   # open in Expo Go / emulator
```

The Playwright run creates a **new** session named **"Answered Monitor E2E"**,
assigns it to operator **op-1**, attaches the **Gilded Owls** team, and leaves it
in **Preparing** — the trivia clock has **not started**. The console prints the
**session code**; copy it. **You** press **Start** in the operator UI once both
web and mobile are staged, which begins the live clock on demand.

> Why this matters: once live, the whole session lasts only **~95 seconds**
> (5s pre-game countdown + 3 seed questions × 30s each), then it auto-finishes.
> Because you control when to press Start, you get all the setup time you need
> and the ~95s window doesn't run out before you can answer. (Do **not** run
> `session-answered-monitor.spec.ts` for this — that spec drives the session to
> Active itself and the clock runs out before you can touch it; it's the
> automated CI assertion, not the manual flow.)
>
> If a stale **"Answered Monitor E2E"** card is already **Finished** or the clock
> ran out, just rerun the seed command and use the freshly created card/code.

## 2. Credentials

| Who         | Where                | Login                                           |
| ----------- | -------------------- | ----------------------------------------------- |
| Operator    | web `localhost:3000` | `op-1` / `operator123`                          |
| Participant | mobile app           | `participant-1@umbral.local` / `participant123` |

## 3. Run it — stage both sides, THEN press Start

The order matters: get web **and** mobile fully staged **while the session is
still Preparing** (no clock running), and only then press **Start**. That's what
makes the flip catchable.

### Step A — Operator (web): open the session, don't start it yet

1. Open `localhost:3000`, sign in as **op-1 / operator123**.
2. Left nav → **My sessions** → open the fresh **"Answered Monitor E2E"** card.
   Confirm the code matches the one the seed printed. If multiple cards share
   the title, ignore any **Finished** one and use the newest.
3. Click **Open live operation**. The session is **Preparing** — the timer reads
   **"Not started"** and the **Answered monitor** panel (below the trivia round
   panel) shows **"No active trivia question."** That's expected; the board only
   populates once you Start. **Leave this open. Do not press Start yet.**

### Step B — Participant (mobile): join the team while still Preparing

1. Sign in as **participant-1@umbral.local / participant123**.
2. Tap **Join your session** → enter the **session code**.
3. In the team lobby, pick **Gilded Owls (OWLS)** → join. You land in the team
   space. (Joining works pre-start; the question area stays empty until Start.)

### Step C — Operator: press Start, then the participant answers

1. With mobile parked in the Gilded Owls space, the operator clicks **Start**.
   After the ~5s countdown, **`Question 1`** opens and the Answered monitor shows
   one row per team, **"Not answered yet"**, count **`0 / N answered`**.
2. On mobile, **select an option and tap Submit** while the question is showing.
   You have 30s per question (3 questions), so there's no rush now.

### What the operator must see the instant the participant submits

- The **Gilded Owls** row flips to **"Answered"**, and the count ticks to
  **`1 / N answered`**.
- **No option, correctness, or points appear** anywhere on the board — only
  answered / not-answered.
- Other teams' rows are unchanged.

That flip is HU-36A working. ✅

## 4. Quick extra checks (optional)

| Check                      | How                                                    | Expect                                                                                 |
| -------------------------- | ------------------------------------------------------ | -------------------------------------------------------------------------------------- |
| Board resets each question | Let the current question close and the next activate   | Header → `Question N+1`, all rows back to **Not answered yet**, count `0 / N`          |
| Empty state                | Before Start, between questions, or after the substage ends | **"No active trivia question."**, no rows                                          |
| No leak                    | Read the whole panel at any point                      | The words option / correct / points / score appear **nowhere**                         |
| Reconnect                  | `docker compose restart api-gateway`, let it reconnect | Board re-fetches and shows the correct current roster + answered set (not blank/stale) |

---

## Troubleshooting

- **Row doesn't flip after submitting.** The answer was rejected. Most likely
  the membership seed didn't run — re-run the seed command in step 1 (its
  global-setup seeds `participant-1` + the Gilded Owls `registered_team_memberships`
  row). Confirm you joined **Gilded Owls**, not another team.
- **Mobile can't reach the backend.** It targets the gateway on
  `localhost:8000`; on a physical device use your machine's LAN IP, not
  `localhost`.
- **No "Start" button on the session.** You opened the wrong (Finished) card, or
  you ran `session-answered-monitor.spec.ts` instead of the `-manual-seed` one
  (that spec drives itself to Active and finishes). Re-run the seed command and
  open the fresh Preparing card.
- **Session finished before you could answer.** Once live, the whole session is
  only ~95s. Stage both web and mobile **before** pressing Start (step 3), so the
  clock only starts when you're ready. If it slipped away, re-run the seed
  command, reload **My sessions**, and open the new card.
