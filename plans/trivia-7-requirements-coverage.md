# Plan: Meet the 7 Product Requirements (Trivia form)

> Source: `backend/docs/sprint_1_final_tickets.md` (remaining HUs) +
> `mobile/plans/post-hu-34a-mobile-trivia-breakdown.md` (mobile participant flow),
> mapped onto the 7 academic/product requirements.

## Framing

The 7 requirements read like a full TreasureHunt system, but the sprint builds **Trivia**
(the simplified mode). Every requirement is satisfiable in its Trivia form without building
missions-with-clues. Requirement 1 (Mission Management) is **already done** via the quiz HUs
(`HU-11/12/13/14`), so it is a prerequisite, not a phase.

Requirement → phase coverage:

| # | Requirement | Trivia form | Phase(s) |
|---|---|---|---|
| 1 | Mission Management | Quiz authoring | ✅ done (prereq) |
| 2 | Live Session Management | create + state + timer | 1, 2 |
| 3 | Participating team management | teams in session + operator | 1 |
| 4 | Operator dashboard | answered/not-answered live | 2, 3, 4 |
| 5 | Team dashboard | question, timer, score, submit | 3, 4 |
| 6 | Real-time monitoring | SignalR ticks/state/events | 2, 3, 4 |
| 7 | Asynchronous processing | AnswerRegistered produce→consume | 4 |

Each phase is a **vertical slice** cutting backend → transport → client, demoable on its own.
Phases are serial along the trivia spine; frontend/mobile work attaches inside each phase.

## Architectural decisions

Durable decisions referenced by every phase:

- **REST routes (session-operations-service):** base `/api/sessions`
  - `POST /api/sessions/` — create trivia session (done, `HU-16`)
  - `GET /api/sessions/` — list assignable sessions (operator-scoped, `HU-19`)
  - `PATCH /api/sessions/{liveSessionId:guid}/operator-assignment` — assign operator (`HU-19`)
  - `POST /api/sessions/{liveSessionId:guid}/participants/reconnect` — reconnect (done, `HU-07B`)
  - new lifecycle/question routes added in their phases follow `…/{liveSessionId:guid}/<verb>`
- **SignalR transport:** single `SessionsHub` (session-operations-service). Groups already
  defined: `live-session:{id}`, `team:{id}`, `participant:{id}`. Server→client broadcasts go
  out via `IHubContext<SessionsHub>` from background services / domain-event handlers — clients
  subscribe, they do not poll. Auth: `Participant` policy for players; operator broadcasts are
  scoped to the operator's session group.
- **Session state:** `SessionState` enum (`Scheduled/Preparing/Active/Paused/Finished/Cancelled`)
  and `SessionStateTransitionPolicy` + `LiveSession.MoveTo()` **already exist**. Transitions
  raise `SessionStateChangedEvent`. No new state machine is built — phases *expose and broadcast*
  it.
- **Authoritative time:** the server owns the clock. Timer values are computed from
  `StartedAt` + `MaximumTime` server-side and pushed; clients render, never invent. Expiry
  auto-closes the active question (no separate operator "close" action).
- **Async boundary:** `HU-34A` publishes `AnswerRegistered` to RabbitMQ; the scoring service
  (`HU-37A`) is the only consumer → writes a score ledger. This is the demonstrated end-to-end
  workflow; nothing else crosses the broker in this scope.
- **First-answer-per-team:** enforced inside the single answer handler (`HU-34B` folded in) —
  take the first valid submission, reject the rest.
- **Clients:** web operator dashboard = `frontend/app/dashboard/SessionOperatorPanel.tsx`
  (extend the existing shell; replace mock `useState` "connected" indicator with a real hub
  connection). Mobile participant gameplay extends `mobile/src/app/(app)/team-space.tsx` and
  `mobile/src/lib/realtime/*`. Keep `team-lobby` as the admission screen only.

---

## Phase 1: Operator owns the session

**Requirements:** 3 (participating team management — operator binding), part of 2.
**Spine:** finish `HU-19` (DES-26). `HU-18` team-association assumed landed.

### What to build

Close out operator assignment end-to-end. The backend command, `PATCH …/operator-assignment`,
and `GET /api/sessions/` (operator-scoped list) already exist; the gap is the **web operator
panel** consuming them. An admin assigns an operator to an already-created trivia session; that
operator signs in and sees exactly the sessions assigned to them. Proxy authorization keeps a
non-assigned operator from seeing or acting on a session.

### Acceptance criteria

- [ ] Admin can assign an operator to a session from the web dashboard (real API call).
- [ ] Operator's dashboard lists only sessions assigned to them, fed by `GET /api/sessions/`.
- [ ] A user who is not the assigned operator is rejected (Proxy/authorization) from the session.
- [ ] Reassignment updates the operator and is reflected in both operators' lists.
- [ ] Integration + unit tests cover assign, list-scoping, and forbidden access.

---

## Phase 2: Session goes live + authoritative timer

**Requirements:** 2 (live session management), 6 (real-time monitoring). Advances 4, 5.
**Spine:** `HU-21A` (DES-28, expose + broadcast existing state machine), `HU-22` (DES-30, timer).

### What to build

Let the operator drive the session through its lifecycle and run a server-authoritative timer.
Add the command(s)/endpoint to invoke `LiveSession.MoveTo()` for the gameplay path
(`Scheduled → Preparing → Active → Finished`, plus `Paused`/`Cancelled`), and a background timer
service that, while a session is `Active`, broadcasts countdown ticks and the current
`SessionState` to the `live-session:{id}` group over `SessionsHub`. On expiry the timer drives
the session/question close (auto-close slice). The web operator panel opens a **real** hub
connection (retiring the fake indicator) and renders live state + ticking time; the
participant client (web read-only and/or mobile) shows the same authoritative countdown.

### Acceptance criteria

- [ ] Operator can transition a session to `Active` (and `Pause/Finish/Cancel`) via the API,
      guarded by `SessionStateTransitionPolicy` (e.g. cannot activate with zero teams).
- [ ] `SessionStateChangedEvent` results in a broadcast to the session group; connected
      operator + participants see the new state without polling.
- [ ] An active session emits authoritative timer ticks over SignalR; clients render server time,
      not device-local time.
- [ ] Timer reaching zero auto-closes the round (stops accepting further input) — no manual close.
- [ ] Web operator panel shows live state + countdown from the real hub (mock indicator removed).
- [ ] Reconnecting mid-session restores the correct state + remaining time.
- [ ] Tests: transition guards, timer expiry/auto-close, broadcast on state change.

---

## Phase 3: Active question presented to players

**Requirements:** 4 (operator dashboard), 5 (team dashboard), 6 (real-time). Advances 2.
**Spine:** thin `HU-33A` (DES-44, activate question + auto-close), `EN-M1`, `HU-M1`.

### What to build

Give players something to answer. A thin slice of round orchestration: the operator (or the
timer advancing) activates a question from the trivia snapshot; the backend broadcasts the
active-question snapshot (text, options, remaining time, whether the caller's team already
answered) to the `live-session:{id}` group, and auto-closes it on timer expiry. `EN-M1` freezes
this participant-facing runtime contract as typed DTOs in `mobile/src/lib/realtime/` so mobile
builds against verified shapes, not guesses. `HU-M1` replaces the `team-space` placeholder with
a real gameplay screen rendering the question + options + authoritative countdown, with explicit
empty states (reconnecting / waiting for next question / no active question / session closed).
The operator panel shows the current question; the web participant view optionally mirrors it.

### Acceptance criteria

- [ ] Activating a question broadcasts its snapshot to all session participants in real time.
- [ ] `EN-M1`: typed trivia-runtime DTOs + hub vocabulary frozen in `mobile/src/lib/realtime/`,
      matching the backend contract (active-question, question-closed, remaining-time shapes).
- [ ] Mobile participant entering during an active question sees that question + live countdown.
- [ ] Participant entering between questions sees a waiting state, not stale question UI.
- [ ] Question auto-closes on expiry; clients move to a closed/waiting state.
- [ ] Operator dashboard reflects the currently-active question.
- [ ] Tests: active-question broadcast, empty/waiting states, auto-close transition.

---

## Phase 4: First answer → score (RabbitMQ end-to-end)

**Requirements:** 7 (asynchronous processing), 4 (operator dashboard), 5 (team dashboard), 6.
**Spine:** `HU-34A` (+`34B` folded) → `HU-37A`, `HU-36A`, mobile `HU-M2` + `HU-M3`.
**Kept as one end-to-end slice** (per decision): proves the full async gate in a single demo.

### What to build

The complete answer loop and the demonstrated async workflow. Mobile (`HU-M2`) lets a team
select an option and submit during an active question, with a pending lock to prevent
double-submit and mapped rejection reasons (late / duplicate / wrong-team / invalid state /
network). The backend answer handler (`HU-34A`) accepts the **first valid answer per team**,
rejects the rest (`HU-34B` folded in), and **publishes `AnswerRegistered`** to RabbitMQ. The
scoring service (`HU-37A`) **consumes** `AnswerRegistered` → writes the score ledger → team
score. `HU-36A` gives the operator a live, restricted answered/not-answered dashboard over
SignalR. Mobile `HU-M3` keeps the participant coherent after submit and after close
(answer-submitted waiting state, question-closed lock, correct restore on reconnect).

This is the gate keystone: produce → broker → consume → score, with real clients on both ends.

### Acceptance criteria

- [ ] A team can submit exactly one answer per question from mobile during an active question.
- [ ] Mobile prevents accidental double-submit while the result is in flight.
- [ ] Backend accepts the first valid submission, rejects 2nd…nth (first-answer-per-team wins).
- [ ] Backend rejections (late / duplicate / wrong-team / closed) map to participant-facing UI
      without inventing domain rules.
- [ ] `HU-34A` publishes `AnswerRegistered` to RabbitMQ on a valid first answer.
- [ ] `HU-37A` consumes `AnswerRegistered` and updates the team's score ledger.
- [ ] Operator dashboard (`HU-36A`) shows live answered / not-answered per team via SignalR.
- [ ] Mobile shows a stable "answer submitted" state, then a locked "question closed" state;
      reconnecting into a closed question restores the correct state.
- [ ] Tests: first-answer-wins, publish on accept, consume→ledger, dashboard counts, mobile
      submit/closed flows. End-to-end: submit → publish → consume → score visible.

---

## Phase 5 (optional / deferred): Reveal + live ranking

**Requirements:** completes the spectacle side of 4, 5, 6.
**Spine:** `HU-33B` (DES-45), `HU-35` (DES-48), `HU-39B` (DES-55) + `HU-37B`, mobile `HU-M4`/`HU-M5`.

> Deferred per the Sprint 1 cut (`sprint_1_final_tickets.md` §3). Documented so the full
> requirement set is captured, **not committed**. Start only once the playable loop (Phases 1–4)
> is solid and the scoring/ranking contracts are real.

### What to build

The payoff after each closed question and at session end. `HU-33B` auto-closes the session and
broadcasts final results from the ledger total. `HU-35` reveals the correct option + explanation
on close. `HU-39B`/`HU-37B` expose live/retroactive ranking. Mobile `HU-M4` shows reveal
(correct/incorrect + correct option + explanation); `HU-M5` shows current team score and optional
leaderboard with real-time updates.

### Acceptance criteria

- [ ] On question close, the correct option + explanation are revealed to participants (`HU-35`).
- [ ] At session end, final results broadcast from the ledger total (`HU-33B`).
- [ ] Mobile participant sees whether their team answer was correct + the correct option (`HU-M4`).
- [ ] Mobile participant sees current team score, with live updates when scoring lands (`HU-M5`).
- [ ] Optional live leaderboard updates in real time (`HU-39B`).

---

## Sequencing notes

- **Phases are serial** along the trivia spine (state → timer → question → answer → score);
  this ordering is irreducible. Frontend and mobile work lives *inside* each phase, not after.
- **Phase 4 is the keystone** — it unblocks the async gate, the operator live dashboard, and
  all of mobile gameplay. Protect it; if anything slips, it slips here last.
- **Owner split** (per delegation roadmap): Samuel drives the backend spine; Salomon takes the
  leaves (`HU-37A`, `HU-36A`) the moment `HU-34A` lands; frontend/mobile wiring attaches per
  phase.
- **Gates covered by Phases 1–4:** design patterns (State/Facade/Proxy/Template Method/Chain of
  Responsibility/Strategy across the spine), WebSockets (SignalR ticks + state + dashboard),
  RabbitMQ end-to-end (`HU-34A → HU-37A`), basic pipeline, SOLID. Phase 5 adds no new gate.
