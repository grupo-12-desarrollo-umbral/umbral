# HU-36A — Operator Answered / Not-Answered Monitor: Frontend UI Test

Branch: `feature/hu-36a-trivia-answered-monitor`
App: `frontend/` (Next.js, runs on `http://localhost:3000`)
Backend: `docker compose up -d` in `backend/` (gateway on `http://localhost:8000`)

What changed: the operator's **live operation** view now mounts an **Answered monitor** board
(below the trivia round panel). During an **active synchronized trivia question** it lists every
team with an **Answered / Not answered yet** status and an **`X / N answered`** count, updating live
as answers arrive. It is **option-free by design** — it never shows the chosen option, correctness,
or points (the pre-close no-leak invariant). New endpoint: `GET /api/sessions/{liveSessionId}/answered-monitor`.

---

## Prerequisites

1. **Backend up:**
   ```bash
   cd backend && docker compose up -d
   ```
2. **An active, runtime-ready mission** seeded, so a session can be created and driven to a live
   trivia question. The e2e `globalSetup` (`tests/setup/global-setup.ts`) seeds **"E2E Seed Mission"**
   (active + Ready) and the **"Gilded Owls"** registered team (`a0000000-…-0001`) into the DBs, and it
   persists. If you have never run the suite on this stack, run it once:
   ```bash
   cd frontend && pnpm exec playwright test tests/e2e/session-answered-monitor.spec.ts
   ```
3. **Frontend up:**
   ```bash
   cd frontend && pnpm install && pnpm dev
   ```

## Reach a live board (fastest path)

The board only appears for an **operator** viewing an **Active** session that currently has an
**active trivia question**. Getting a session to that state by hand is several steps, so the quickest
way to observe the board is to let the e2e helper drive one, then open it in the browser:

1. Run the answered-monitor spec (step 2 above). Its `beforeAll` creates a session titled
   **"Answered Monitor E2E"**, assigns it to operator **`op-1`**, associates the **Gilded Owls** team,
   transitions it **Preparing → Active**, and waits out the pre-game countdown until the **first trivia
   question is active**. The session persists in the dev DB after the run.
2. In the browser, log in as that operator (see below) and open the session **promptly** — trivia
   questions are time-boxed, so after the active window closes the board returns to its empty state
   (which is itself a valid state to verify — see row 2).

> **By-hand alternative:** as **admin**, create a session from the seed mission and assign it to your
> operator; as that **operator**, associate a team, then transition the session **Preparing → Active**.
> Once Active, the automated trivia round activates the first question after the pre-game countdown and
> the board renders. This exercises the real product flow but is timing-sensitive.

## Log in

- Open `http://localhost:3000` → redirected to Keycloak.
- Sign in as the **assigned operator** — for the e2e-seeded session that is **`op-1` / `operator123`**.
- For the role-gating check, also have an **admin** (`admin-1` / `admin123`) ready.

Keycloak realm `umbral` seeds these actors for the e2e fixtures: `admin-1/admin123`, `op-1/operator123`.
(The BFF-direct demo users `admin/admin123`, `operator/operator123` also exist but are not the ones the
e2e assigns.)

## Reach the panel

Dashboard left nav → **My sessions** (the `sessions` panel) → click the assigned session card
(**"Answered Monitor E2E"**) → click **Open live operation**. The overview hero mounts the live panels;
the **Answered monitor** (`data-testid="answered-monitor-panel"`) sits **below** the trivia round panel.

---

## Manual test matrix

Run as the **assigned operator** unless noted. Each row maps a state to how you produce it and exactly
what the board must show. (`data-testid`s are given so you can confirm in DevTools.)

| # | State | How to produce it | Expected UI |
|---|-------|-------------------|-------------|
| 1 | **Active-question board** | Open live operation while a trivia question is active | Header **`Question N`** (`answered-monitor-active-question`); one row per team (`team-answer-status-<runtimeTeamId>`); each row shows the **team name + code** and **"Not answered yet"** with `data-answered="false"`; count reads **`0 / N answered`** (`answered-monitor-count`) |
| 2 | **No active question (empty)** | Before the first question activates, between questions, or after the session ends | Only the eyebrow + **"No active trivia question."** (`answered-monitor-empty`). No team rows |
| 3 | **Live answer flip** | A team submits an accepted answer to the active question (see note below) | That team's row flips to **"Answered"** with `data-answered="true"`; the **`X / N answered`** count increments (announced via `aria-live`). Other rows unchanged. **No** option/correctness/points appears |
| 4 | **New question resets the board** | Let the active question close and the next one activate | Header advances to **`Question N+1`**; **all** rows return to **"Not answered yet"** (`data-answered="false"`); count back to **`0 / N`** |
| 5 | **Question close / substage advance clears** | Let the current question close (or the substage advance) with no next question | Board returns to **"No active trivia question."** (`answered-monitor-empty`) |
| 6 | **No-leak invariant** | In any of the above, read the whole panel | The words **option / correct / incorrect / points / score** appear **nowhere** in the board. Only answered-yes/no status is shown |
| 7 | **Reconnect reseeds** | With the board open, briefly bounce the SignalR link (e.g. `docker compose restart api-gateway`, then let the client reconnect) | After reconnect the board **re-fetches the snapshot** and shows the correct current roster + answered set — not a stale or blank board |
| 8 | **Transient backend error** | Stop the read backend mid-view: `docker compose stop session-operations-service`, then reselect the session | Board shows **"Couldn't load answered status. It will refresh automatically."** (`answered-monitor-error`) — **not** the "not authorized" text. Restart the service and reconnect to recover |

Confirm while running the matrix:

- The board is **operator-only** and mounts **below** the trivia round panel in live operation.
- Every non-happy state (rows 2, 5, 8) renders a readable message — never a blank or broken panel.
- Rows are keyed by **runtime team id**; a team with no answer is derived as not-answered by roster
  enumeration, never by the absence of a broadcast.

---

## Notes on hard-to-force states

- **Row 3 (live flip) needs a real accepted participant answer.** A `TeamAnswered` event is emitted
  only after the backend *accepts* an answer, which is gated by cross-service participant-membership
  validation. Producing one against the live stack requires a participant client answering with a team
  whose runtime id passes `registered_teams` membership — the same seeding the backend's own
  integration tests stub. If you cannot drive a real participant answer, this mechanic is covered
  deterministically by the unit tests (`TeamAnswered` subscription normalizer + the panel's
  `data-answered="true"` render). See the e2e spec header and the session handoff for the full rationale.
- **"Waiting for the team roster…"** (`answered-monitor-no-roster`) is a transient state: a question is
  active but the roster snapshot has not seeded yet (or the session has no associated team). You may
  catch it briefly on first open; it is otherwise unit-tested.
- **"You are not authorized to monitor this session."** (`answered-monitor-unauthorized`) is shown when
  the snapshot read returns 401/403 for the actor (or a non-operator role). It is not cleanly reachable
  by hand — the sessions panel only lists sessions you are assigned to — and is covered by unit tests.

---

## Automated coverage

```bash
cd frontend
pnpm exec next build                                                        # typecheck + build
pnpm exec vitest run tests/unit/app/dashboard/answered-monitor-panel.test.ts    # panel states + no-leak sweep
pnpm exec vitest run tests/unit/app/lib/realtime/session-state-client.test.ts   # TeamAnswered subscription + normalizer
pnpm exec playwright test tests/e2e/session-answered-monitor.spec.ts            # live board over the real snapshot endpoint (needs stack up)
```

> The e2e needs a real authenticated operator session (the app `session` cookie **and** a valid sealed
> `kc_session` cookie); the Playwright fixture seeds the `op-1` Keycloak actor and injects both cookies.
> The spec asserts the row-1 and row-6 behavior (active-question roster, all not-answered, no leak); the
> live flip (row 3) is unit-tested for the reasons above.
